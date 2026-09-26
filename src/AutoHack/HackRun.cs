namespace AutoHack;

using Hacknet;
using Pathfinder.Port;

/// <summary>单个目标的战果（GUI 展示用）。</summary>
internal sealed record TargetOutcome(string Name, int Opened, int Total, bool Escalated);

/// <summary>
/// 一次入侵运行的执行状态与推进逻辑。
/// 由 OS.Update / OSUpdateEvent 每帧驱动；与绘制解耦，便于单独测试。
/// 每个动作先把对应指令回显到终端（格式同 OS.runCommand），再执行。
/// 端口一律按 Pathfinder 的协议名操作（<c>openPort(protocol, ipFrom)</c>），
/// 而非原版按端口号 —— 原版那条路径已被框架 Prefix 拦下。
///
/// 三个必须尊重的游戏机制：
/// 1. 管理员反扑：断开连接时 OS.handleDisconnection() 会调
///    admin?.disconnectionDetected()，BasicAdministrator 在 0~20 秒后关掉全部端口并把
///    adminIP 还原成机器自己 —— 肉鸡标记当场丢失，这就是「全网扫描失去效果」的根因。
///    必须在断开前把 computer.admin 置 null（<see cref="HackEngine.SuppressCounterattack"/>）。
/// 2. 追踪：TraceTracker 只在「连着被追踪目标」时推进（Update 见 connectedComp 为空
///    即刻置 active = false），故跑完一个目标就断开连接即等于反追踪；
///    否则计时归零会走 OS.timerExpired() 端掉玩家。
/// 3. 跳板：proxyActive 的机器上，需要跳板访问权的破解程序被 OS.addExe 门禁拦下
///    （OS.cs:2165）。解除即把 proxyOverloadTicks 收敛到 0、proxyActive 置 false ——
///    与 ShellExe 过载跑完的终态逐字节相同，只是不等那 30 秒。
///    刻意不照抄 ShellExe.cs:105 的 hostileActionTaken() —— 那只会点燃追踪。
/// </summary>
internal sealed class HackRun
{
    private const float NonPortDelay = 0.35f;

    private readonly List<Computer> _targets;
    private readonly List<HackStep> _steps;

    private int _index;
    private float _timer;

    internal HackRun(OS os, HackOptions options)
    {
        Options = options;
        _targets = new List<Computer>(HackEngine.ResolveTargets(os, options, out var skippedOwned));
        SkippedOwned = skippedOwned;
        _steps = BuildSteps(_targets, options, os);
        Current = _targets.Count > 0 ? _targets[0].name : "-";
        Phase = "Engaging";
    }

    internal HackOptions Options { get; }

    internal List<TargetOutcome> Outcomes { get; } = new();

    internal int Total => _steps.Count;

    internal int Done => Math.Min(_index, _steps.Count);

    internal float Elapsed { get; private set; }

    internal bool Finished { get; private set; }

    internal string Phase { get; private set; }

    internal string Current { get; private set; }

    internal IReadOnlyList<Computer> Targets => _targets;

    /// <summary>因已控（肉鸡）而跳过的机器数。</summary>
    internal int SkippedOwned { get; }

    /// <summary>推进一帧；返回本帧新完成的动作数（供日志节流）。</summary>
    internal int Tick(OS os, float deltaSeconds)
    {
        if (Finished)
        {
            return 0;
        }

        Elapsed += deltaSeconds;

        if (_index >= _steps.Count)
        {
            Finish(os);
            return 0;
        }

        var step = _steps[_index];

        _timer += deltaSeconds;
        var delay = step.Kind == HackStepKind.OpenPort ? Options.PortDelay : NonPortDelay;
        if (_timer < delay)
        {
            return 0;
        }

        _timer = 0f;
        Apply(os, step);
        _index++;
        return 1;
    }

    private void Apply(OS os, HackStep step)
    {
        var target = step.Target;
        Current = target.name + " @ " + target.ip;

        switch (step.Kind)
        {
            case HackStepKind.Connect:
                Phase = "Connecting to " + target.name;
                Echo(os, step.Command);
                Programs.connect(["connect", target.ip], os);
                break;

            case HackStepKind.Neutralize:
                Phase = "Disabling counterattack on " + target.name;
                Neutralize(os, target);
                break;

            case HackStepKind.BypassProxy:
                Phase = "Bypassing proxy on " + target.name;
                BypassProxy(os, target);
                break;

            case HackStepKind.Probe:
                Phase = "Probing " + target.name;
                Echo(os, "probe");
                foreach (var line in HackEngine.ProbeReport(target))
                {
                    os.write(line);
                }

                break;

            case HackStepKind.OpenPort:
                Phase = "Cracking port " + step.Port.DisplayPort;
                Echo(os, step.Command);
                HackEngine.OpenPort(target, step.Port, os.thisComputer.ip);
                break;

            case HackStepKind.Escalate:
                Phase = "Escalating";
                Echo(os, "porthack");

                // 只用 giveAdmin，不用 os.takeAdmin(ip)：后者内部还会 runCommand("connect " + ip)
                // （OS.cs:1871-1879），而 connect 的第一件事就是无条件断开旧连接
                // （Programs.connect，Programs.cs:235-236）—— 那会立刻触发 handleDisconnection
                // 与管理员反扑，等于自找麻烦。此处已连着目标，写所有权标记即可。
                if (HackEngine.CanEscalate(target))
                {
                    target.giveAdmin(os.thisComputer.ip);
                }

                break;

            case HackStepKind.UploadMarker:
                Phase = "Uploading payload";
                UploadMarker(os, target);
                break;

            case HackStepKind.CleanLogs:
                Phase = "Wiping logs";
                foreach (var name in HackEngine.ClearLogs(target))
                {
                    Echo(os, "rm /log/" + name);
                }

                break;

            case HackStepKind.Disconnect:
                Leave(os, target, step.Command ?? "dc");
                break;
        }
    }

    /// <summary>
    /// 离开节点：先解除管理员反扑，再回显并断开。未连接时是空操作（避免多余的 "Disconnected" 噪音）。
    ///
    /// 断开这一步同时干两件事，缺一不可：
    /// <list type="number">
    /// <item>解除延迟反扑 —— 否则 0~20 秒后全部端口被关、<c>adminIP</c> 被还原成机器自己，
    /// 刚打下的肉鸡标记当场丢失（「全网扫描失去效果」的根因）。</item>
    /// <item>中止追踪 —— <c>TraceTracker.Update</c> 在 connectedComp 为空或换目标时置
    /// <c>active = false</c>，断开是确定性的止血动作。</item>
    /// </list>
    /// </summary>
    private static void Leave(OS os, Computer target, string command)
    {
        if (os.connectedComp == null)
        {
            return;
        }

        Neutralize(os, target);
        Echo(os, command);
        Programs.disconnect(["dc"], os);
    }

    /// <summary>解除目标的延迟反扑，仅在实际解除时回显一行（这不是终端指令，故不走 Echo）。</summary>
    private static void Neutralize(OS os, Computer target)
    {
        if (HackEngine.SuppressCounterattack(target))
        {
            os.write("[autohack] " + target.name + " :: admin counterattack disabled");
        }
    }

    /// <summary>解除跳板，仅在实际解除时回显一行（同上，非终端指令）。</summary>
    private static void BypassProxy(OS os, Computer target)
    {
        if (HackEngine.BypassProxy(target))
        {
            os.write("[autohack] " + target.name + " :: proxy bypassed");
        }
    }

    /// <summary>回显一条指令：格式与 OS.runCommand 完全一致（换行 + 当前提示符 + 原文）。</summary>
    private static void Echo(OS os, string command)
    {
        if (string.IsNullOrEmpty(command) || os.terminal == null)
        {
            return;
        }

        os.write("\n" + os.terminal.prompt + command);
    }

    private static void UploadMarker(OS os, Computer target)
    {
        if (!HackEngine.CanEscalate(target))
        {
            return;
        }

        var home = target.getFolderPath("home", true);
        if (home != null)
        {
            target.makeFile(os.thisComputer.ip, "autohack.txt", "== ACCESS CONTROL ==" + Environment.NewLine + "released by autohack" + Environment.NewLine, home);
        }
    }

    private void Finish(OS os)
    {
        Finished = true;
        Phase = "Complete";

        // 收尾反追踪：跑完仍连着且被追踪时，断开是唯一的止血动作。
        AbortTrace(os);

        foreach (var target in _targets)
        {
            var ports = HackEngine.Ports(target).Count;
            var opened = HackEngine.OpenPortCount(target);
            var escalated = HackEngine.CanEscalate(target);
            Outcomes.Add(new TargetOutcome(target.name, opened, ports, escalated));
            os.write("[autohack] " + target.name + " :: " + opened + "/" + ports
                + " ports, admin=" + (escalated ? "yes" : "no"));
        }

        if (SkippedOwned > 0)
        {
            os.write("[autohack] skipped " + SkippedOwned + " node(s) already owned - 'redo' to include them.");
        }

        Current = "done - " + _targets.Count + " target(s)";
    }

    /// <summary>
    /// 若追踪仍在推进则断开连接中止它。
    /// 追踪的推进条件写在 TraceTracker.Update：connectedComp 为空（或已换目标）即
    /// active = false，因此断开是确定性的中止手段 —— 不需要 TraceKill.exe 那样的冻结。
    /// </summary>
    private static void AbortTrace(OS os)
    {
        if (os?.traceTracker is not { active: true } || os.connectedComp == null)
        {
            return;
        }

        Leave(os, os.connectedComp, "dc");
        os.write("[autohack] trace was active - disconnected to abort it.");
    }

    /// <summary>
    /// 展开动作序列：连接 → 侦察 → 解跳板 → 逐端口攻破 → 提权 → 投放 → 清痕 → 断开。
    /// 清痕必须最后（提权与投放都会向 /log 追加记录），断开更在其后。
    /// 已连接的节点不再重复 connect（那会先断开再重连，徒增噪音）。
    /// </summary>
    private static List<HackStep> BuildSteps(List<Computer> targets, HackOptions options, OS os)
    {
        var steps = new List<HackStep>(targets.Count * 9);

        foreach (var target in targets)
        {
            var alreadyConnected = ReferenceEquals(target, os.connectedComp);
            if (options.ConnectFirst && !alreadyConnected)
            {
                steps.Add(new HackStep(HackStepKind.Connect, target, default, "connect " + target.ip));
            }

            // 独立成步而非挂在 Connect 上：direct/已连接路径没有 Connect 步，同样需要解除反扑。
            steps.Add(new HackStep(HackStepKind.Neutralize, target, default, null));

            steps.Add(new HackStep(HackStepKind.Probe, target, default, "probe"));

            var ports = HackEngine.CrackablePorts(target);
            if (ports.Count > 0 && HackEngine.ProxyActive(target))
            {
                steps.Add(new HackStep(HackStepKind.BypassProxy, target, default, null));
            }

            foreach (var port in ports)
            {
                steps.Add(new HackStep(HackStepKind.OpenPort, target, port, HackEngine.CrackCommand(port)));
            }

            steps.Add(new HackStep(HackStepKind.Escalate, target, default, "porthack"));

            if (options.UploadMarker)
            {
                steps.Add(new HackStep(HackStepKind.UploadMarker, target, default, null));
            }

            if (options.ClearLogs)
            {
                steps.Add(new HackStep(HackStepKind.CleanLogs, target, default, null));
            }

            if (options.Disconnect)
            {
                steps.Add(new HackStep(HackStepKind.Disconnect, target, default, "dc"));
            }
        }

        return steps;
    }
}

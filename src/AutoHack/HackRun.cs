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
/// 3. 清痕必须在断开**之后**：Computer.disconnecting 会往目标 /log 写
///    "&lt;玩家IP&gt; Disconnected"（Computer.cs:722-727），先清后断会留下这条痕迹。
///    断开后的清痕不再回显 rm（那时不在目标上），改为一行状态。
/// 4. 跳板：proxyActive 的机器上，需要跳板访问权的破解程序被 OS.addExe 门禁拦下
///    （OS.cs:2165）。解除即把 proxyOverloadTicks 收敛到 0、proxyActive 置 false ——
///    与 ShellExe 过载跑完的终态逐字节相同，只是不等那 30 秒。
///    刻意不照抄 ShellExe.cs:105 的 hostileActionTaken() —— 那只会点燃追踪。
/// </summary>
internal sealed class HackRun
{
    /// <summary>Normal 档的非端口步间隔（真人节奏）。</summary>
    private const float NormalStepDelay = 0.35f;

    private readonly List<Computer> _targets;
    private readonly List<HackStep> _steps;

    private int _index;
    private float _timer;

    /// <summary>本次运行中靠已知凭据登入（即已提权）的机器，其破端口类步骤整体跳过。</summary>
    private readonly HashSet<Computer> _loggedIn = new();

    internal HackRun(OS os, HackOptions options)
    {
        Options = options;
        _targets = new List<Computer>(
            HackEngine.ResolveTargets(os, options, out var skippedOwned, out var skippedHopeless));
        SkippedOwned = skippedOwned;
        SkippedHopeless = skippedHopeless;
        _steps = BuildSteps(_targets, options, os);
        Current = _targets.Count > 0 ? _targets[0].name : "-";
        Phase = "ENGAGING";
    }

    internal HackOptions Options { get; }

    internal List<TargetOutcome> Outcomes { get; } = new();

    internal int Total => _steps.Count;

    internal int Done => Math.Min(_index, _steps.Count);

    internal bool Finished { get; private set; }

    internal string Phase { get; private set; }

    internal string Current { get; private set; }

    internal IReadOnlyList<Computer> Targets => _targets;

    /// <summary>因已控（肉鸡）而跳过的机器数。</summary>
    internal int SkippedOwned { get; }

    /// <summary>因提权门槛高于端口表容量（永远打不通）而跳过的机器数。</summary>
    internal int SkippedHopeless { get; }

    /// <summary>
    /// 面板标题一律大写。在每个 Phase 赋值处转换一次，
    /// 而不是在绘制期每帧对同一字符串重复转换。
    /// </summary>
    private static string Upper(string value)
        => string.IsNullOrEmpty(value) ? string.Empty : value.ToUpperInvariant();

    /// <summary>推进一帧。只允许游戏线程调用 —— 会改动游戏状态。</summary>
    internal void Tick(OS os, float deltaSeconds)
    {
        if (Finished)
        {
            return;
        }

        _timer += deltaSeconds;

        // Instant 档：非端口步不等时间，一帧内连跑到底，直到撞上一个端口步
        // 或本帧的预算耗尽。端口步始终按 Options.PortDelay 等 —— 那是回显
        // 逐条浮现的节奏来源，也是唯一有意义的等待。
        var budget = MaxStepsPerFrame;
        while (!Finished && budget-- > 0)
        {
            if (_index >= _steps.Count)
            {
                Finish(os);
                return;
            }

            var step = _steps[_index];

            // 该目标已靠 login 提权：破端口/解防火墙/porthack 都是无用功，
            // 直接跳过（不回显、不耗时）。停机也计入 Done，进度条才走得准。
            if (IsRedundantAfterLogin(step))
            {
                _index++;
                continue;
            }

            var delay = DelayFor(step.Kind);
            if (_timer < delay)
            {
                return;
            }

            _timer = 0f;
            Apply(os, step);
            _index++;
        }
    }

    /// <summary>单帧最多执行多少步，防止 Instant 档在极端规模下一帧卡死。</summary>
    private const int MaxStepsPerFrame = 512;

    /// <summary>
    /// 该步骤是否已被 login 提权化为无用功。只对<b>本次运行中确实靠 login 拿下</b>
    /// 的目标成立（<see cref="_loggedIn"/>），不影响 redo 模式下重打已控节点的语义。
    /// </summary>
    private bool IsRedundantAfterLogin(HackStep step)
    {
        if (_loggedIn.Count == 0 || !_loggedIn.Contains(step.Target))
        {
            return false;
        }

        return step.Kind is HackStepKind.BypassProxy
            or HackStepKind.OpenPort
            or HackStepKind.SolveFirewall
            or HackStepKind.Escalate;
    }

    /// <summary>该步骤需等待的秒数。Instant 档把非端口步压到 0。</summary>
    private float DelayFor(HackStepKind kind)
    {
        if (kind == HackStepKind.OpenPort)
        {
            return Options.PortDelay;
        }

        return Options.Speed switch
        {
            HackSpeed.Instant => 0f,
            HackSpeed.Fast => HackOptions.FastStepDelay,
            _ => NormalStepDelay,
        };
    }

    private void Apply(OS os, HackStep step)
    {
        var target = step.Target;
        Current = target.name + " @ " + target.ip;

        switch (step.Kind)
        {
            case HackStepKind.Connect:
                Phase = "CONNECTING TO " + Upper(target.name);
                Echo(os, step.Command);
                Programs.connect(["connect", target.ip], os);
                break;

            case HackStepKind.Neutralize:
                Phase = "DISABLING COUNTERATTACK ON " + Upper(target.name);
                Neutralize(os, target);
                break;

            case HackStepKind.BypassProxy:
                Phase = "BYPASSING PROXY ON " + Upper(target.name);
                BypassProxy(os, target);
                break;

            case HackStepKind.Login:
                Phase = "LOGGING IN";

                // login 成功即 giveAdmin（Computer.cs:851-855），与 porthack 终点等价，
                // 但不破端口、不触发追踪。凭据来自目标自身的公开字段与 known 标记。
                if (HackEngine.TryLogin(target, out var credential))
                {
                    Phase = "LOGGED IN";

                    // 只回显 login 本身：原版 login 是交互式的（先问用户名再问密码，
                    // Programs.cs:404-448），没有 "login <user> <pass>" 这种写法，
                    // 拼上参数会是条游戏里不存在的命令。用了哪组凭据由下面的状态行交代。
                    Echo(os, "login");

                    // 本目标已提权，后续破端口/解防火墙/porthack 都不必跑。
                    // 只登记「本次运行中确实靠 login 拿下的」机器 —— 不用
                    // 「adminIP 已是我们」这个更宽的判据，否则 redo 模式
                    // （重打已控节点）会连端口都不破，改变其语义。
                    _loggedIn.Add(target);
                    os.write("[autohack] " + target.name + " :: admin via login (" + credential + ") - skipping port cracks");
                }

                break;

            case HackStepKind.Probe:
                Phase = "PROBING " + Upper(target.name);
                Echo(os, "probe");
                foreach (var line in HackEngine.ProbeReport(target))
                {
                    os.write(line);
                }

                break;

            case HackStepKind.OpenPort:
                Phase = "CRACKING PORT " + step.Port.DisplayPort;
                Echo(os, step.Command);
                HackEngine.OpenPort(target, step.Port, os.thisComputer.ip);
                break;

            case HackStepKind.SolveFirewall:
                Phase = "BYPASSING FIREWALL ON " + Upper(target.name);
                Echo(os, "solve " + (target.firewall?.solution ?? string.Empty));
                SolveFirewall(os, target);
                break;

            case HackStepKind.Escalate:
                Phase = "ESCALATING";

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
                Phase = "UPLOADING PAYLOAD";
                UploadMarker(os, target);
                break;

            case HackStepKind.CleanLogs:
                Phase = "WIPING LOGS";
                var wiped = HackEngine.ClearLogs(target);

                // 回显的 rm 只有在「还连着目标」时才是真命令；清痕现在排在 dc 之后
                // （断开本身会往目标 /log 写一条 "<ip> Disconnected"，先清后断等于白清），
                // 此时已不在目标上，再回显 rm 就是假的 —— 改为一行状态。
                if (os.connectedComp == null)
                {
                    if (wiped.Count > 0)
                    {
                        os.write("[autohack] " + target.name + " :: wiped " + wiped.Count + " log file(s)");
                    }

                    break;
                }

                foreach (var name in wiped)
                {
                    Echo(os, "rm /log/" + name);
                }

                break;

            case HackStepKind.Disconnect:
                Leave(os, target, step.Command ?? "dc");
                break;

            case HackStepKind.KillTrace:
                Phase = "KILLING TRACE";
                KillTrace(os);
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

    /// <summary>
    /// 终止进行中的追踪。排在断开之后：断开已让 TraceTracker 自己失效
    /// （connectedComp 为空），此处只是把「确实停掉了」这件事写进终端；
    /// 若断开没生效（如 stay 模式），这里就是唯一的止血点。
    /// </summary>
    private static void KillTrace(OS os)
    {
        if (HackEngine.KillTrace(os))
        {
            os.write("[autohack] trace killed - timer stopped.");
        }
    }

    /// <summary>解除目标的延迟反扑，仅在实际解除时回显一行（这不是终端指令，故不走 Echo）。</summary>
    private static void Neutralize(OS os, Computer target)
    {
        if (HackEngine.SuppressCounterattack(target))
        {
            os.write("[autohack] " + target.name + " :: admin counterattack disabled");
        }
    }

    /// <summary>解目标防火墙，仅在实际解开时回显一行（非终端指令）。</summary>
    private static void SolveFirewall(OS os, Computer target)
    {
        if (HackEngine.SolveFirewall(target, os))
        {
            os.write("[autohack] " + target.name + " :: firewall solved");
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
        // 按「是否已拿下」判定，而不是 CanEscalate —— 后者要求端口已破，
        // 而靠 login 提权的目标一个端口都没破，用它会漏掉投放。
        if (!HackEngine.IsOwned(target, os))
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
        Phase = "COMPLETE";

        // 收尾反追踪：跑完仍连着且被追踪时，断开是唯一的止血动作。
        AbortTrace(os);

        foreach (var target in _targets)
        {
            var ports = HackEngine.Ports(target).Count;
            var opened = HackEngine.OpenPortCount(target);

            // 所有权看 adminIP（肉鸡标记的真身），不看 CanEscalate ——
            // 后者要求端口已破，靠 login 拿下的目标会因为 0 端口而被误报 admin=no。
            var owned = HackEngine.IsOwned(target, os);
            Outcomes.Add(new TargetOutcome(target.name, opened, ports, owned));
            os.write("[autohack] " + target.name + " :: " + opened + "/" + ports
                + " ports, admin=" + (owned ? "yes" : "no"));
        }

        if (SkippedOwned > 0)
        {
            os.write("[autohack] skipped " + SkippedOwned + " node(s) already owned - 'redo' to include them.");
        }

        if (SkippedHopeless > 0)
        {
            os.write("[autohack] skipped " + SkippedHopeless
                + " node(s) whose port table cannot reach the escalation threshold.");
        }

        Current = "done - " + _targets.Count + " target(s)";
    }

    /// <summary>
    /// 收尾兜底：跑完仍被追踪时直接毙掉。
    /// 正常情况下每个目标的 KillTrace 步已停掉它，这里覆盖「最后一步之后才被点燃」
    /// 的窗口 —— 例如目标机带 tracker、断开时经
    /// <c>TrackerCompleteSequence</c>（OS.cs:950-958）延迟 10~20 秒启动的那种。
    /// </summary>
    private static void AbortTrace(OS os) => KillTrace(os);

    /// <summary>
    /// 展开动作序列：连接 → 侦察 → 解跳板 → 逐端口攻破 → 提权 → 投放 → 清痕 → 断开。
    /// 清痕必须最后（提权与投放都会向 /log 追加记录），断开更在其后。
    /// 已连接的节点不再重复 connect（那会先断开再重连，徒增噪音）。
    /// </summary>
    private static List<HackStep> BuildSteps(List<Computer> targets, HackOptions options, OS os)
    {
        var steps = new List<HackStep>(targets.Count * 10);

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

            // 已知凭据登录排在最前：成功即提权，后面整段破端口动作都不必跑。
            // 放在 probe 之后是为了让终端先有原生端口报告，再看到 login。
            if (options.UseCredentials)
            {
                steps.Add(new HackStep(HackStepKind.Login, target, default, null));
            }

            var ports = HackEngine.CrackablePorts(target);
            if (ports.Count > 0 && HackEngine.ProxyActive(target))
            {
                steps.Add(new HackStep(HackStepKind.BypassProxy, target, default, null));
            }

            foreach (var port in ports)
            {
                steps.Add(new HackStep(HackStepKind.OpenPort, target, port, HackEngine.CrackCommand(port)));
            }

            // 防火墙必须在 porthack 之前解 —— 游戏自己的门禁要求
            // firewall.solved（OS.cs:1918-1930），未解则 porthack 直接被拒。
            if (target.firewall is { solved: false })
            {
                steps.Add(new HackStep(HackStepKind.SolveFirewall, target, default, null));
            }

            steps.Add(new HackStep(HackStepKind.Escalate, target, default, "porthack"));

            if (options.UploadMarker)
            {
                steps.Add(new HackStep(HackStepKind.UploadMarker, target, default, null));
            }

            if (options.Disconnect)
            {
                steps.Add(new HackStep(HackStepKind.Disconnect, target, default, "dc"));
            }

            // 断开已让 TraceTracker 自己失效；这一步是确定性的兜底 ——
            // stay 模式（不断开）下它是唯一的止血点。走 stop()，零每帧开销。
            steps.Add(new HackStep(HackStepKind.KillTrace, target, default, null));

            // 清痕必须是本目标的最后一步：提权、投放、**以及断开**都会向目标 /log
            // 追加记录（Computer.disconnecting 写 "&lt;ip&gt; Disconnected"，
            // Computer.cs:722-727），先清后断等于白清。
            if (options.ClearLogs)
            {
                steps.Add(new HackStep(HackStepKind.CleanLogs, target, default, null));
            }
        }

        return steps;
    }
}

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
/// 3. 清痕必须在断开**之前**：rm 这类命令的目标机取自 os.connectedComp
///    （Programs.rm，Programs.cs:956），且 Programs.disconnect 会清空
///    os.navigationPath —— 断开之后再清，回显的 rm 就是条假命令，正如玩家
///    手敲时那样「没有效果」。断开本身会写 "&lt;玩家IP&gt; Disconnected"
///    （Computer.cs:722-727），故 Leave 断开时把 silent 置真，避免刚清的痕迹被写回。
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

    /// <summary>本次运行的入侵脚本；null = 用内置次序。构造期已解析完成。</summary>
    private readonly HackScript _script;

    internal HackRun(OS os, HackOptions options)
    {
        Options = options;

        // 脚本在构造期解析一次：语法错/文件缺失在这里抛出，由调用方转成终端可见
        // 的错误行。放进 Tick 会让错误每帧重复，放进 BuildSteps 会让「目标为空」
        // 与「脚本坏了」两种情况混在一起。
        _script = string.IsNullOrEmpty(options.Script) ? null : HackScript.Load(options.Script);

        var plan = HackEngine.ResolveTargets(os, options);
        _targets = plan.Targets;
        SkippedOwned = plan.SkippedOwned;
        SkippedHopeless = plan.SkippedHopeless;
        _steps = BuildSteps(_targets, plan.Skipped, options, os, _script);
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

        // 脚本自带 delay 行时以它为准（游戏 HackerScript 的 config 第 4 参同义），
        // 否则回到 speed 档位 —— 两者正交：档位管「多快」，脚本管「什么次序」。
        if (_script?.StepDelay is { } scripted)
        {
            return scripted;
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
                Echo(os, step.Command ?? "probe");
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

                Echo(os, step.Command ?? "porthack");

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

                // 正常目标：清痕排在它自己的 Disconnect 之前，此刻 os.connectedComp
                // 就是 target，回显的 rm 是一条真能跑的命令（正是玩家手敲的那条）。
                // 先回显后执行，与其余步骤同一约定。
                // 命令的作用域由连接决定（Programs.rm，Programs.cs:956 取
                // os.connectedComp），清痕排在 Disconnect 之前，此刻正在目标上。
                var onTarget = os.connectedComp == target;
                if (onTarget)
                {
                    Echo(os, step.Command ?? "rm /log/*");
                }

                var wiped = HackEngine.ClearLogs(target, os.thisComputer.ip);

                // 战果必须可见。原版 rm 逐文件打印 "Deleting <名>." + "Done"
                // （Programs.cs:1018-1031），全自动跑 100+ 台会刷屏，压成一行摘要，
                // 措辞沿用游戏自己的两个词。删 0 条时不吭声 —— 无痕迹的机器是多数。
                if (wiped.Count > 0)
                {
                    os.write(onTarget
                        ? "Deleting " + wiped.Count + " file(s)... Done"
                        : "[autohack] " + target.name + " :: rm /log/* -> "
                            + wiped.Count + " log file(s) wiped");
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
        var leaving = os.connectedComp;
        if (leaving == null)
        {
            return;
        }

        Neutralize(os, target);
        Echo(os, command);

        // 静默断开。<c>silent</c> 是游戏自己的 public 开关（Computer.cs:57），
        // Multiplayer.cs:125-127 就是「set true → 操作 → 还原」这个用法。
        // 断开本身会往目标 /log 写 "<ip> Disconnected"（Computer.disconnecting，
        // Computer.cs:722-727），而清痕排在断开之前（要连着目标的文件系统才作数），
        // 不静音就等于清完立刻被写回一条。
        //
        // 多人对局不对它静音：同一个 <c>!silent</c> 门还守着
        // <c>sendNetworkMessage("cDisconnect ...")</c>（Computer.cs:728-731），
        // 静音会连断线同步一起吞掉，对面看到的还是「连着」。
        // 日志保真让位于联机状态保真 —— 单机下这个分支恒真，多人才走 else。
        if (os.multiplayer)
        {
            Programs.disconnect(["dc"], os);
            return;
        }

        var wasSilent = leaving.silent;
        leaving.silent = true;
        try
        {
            Programs.disconnect(["dc"], os);
        }
        finally
        {
            leaving.silent = wasSilent;
        }
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
    /// <paramref name="skipped"/> 是被剔除的机器，只在末尾追加清痕。
    /// </summary>
    private static List<HackStep> BuildSteps(
        List<Computer> targets, IReadOnlyList<Computer> skipped, HackOptions options, OS os,
        HackScript script)
    {
        var steps = new List<HackStep>((targets.Count + skipped.Count) * 10);

        foreach (var target in targets)
        {
            if (script != null)
            {
                AppendScripted(steps, target, script, options, os);
                continue;
            }

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

            // 清痕必须排在断开**之前**：rm 的语义是「操作当前连接的文件系统」
            // —— Programs.rm 的作用域来自 getCurrentFolder(os)，而它读的是
            // os.connectedComp 与 os.navigationPath（Programs.cs:1531-1534 →
            // getFolderAtDepth :1536-1560），Programs.disconnect 又会把
            // navigationPath 清空。断开之后再清，回显的 rm 就是条假命令。
            if (options.ClearLogs)
            {
                steps.Add(new HackStep(HackStepKind.CleanLogs, target, default, "rm /log/*"));
            }

            if (options.Disconnect)
            {
                steps.Add(new HackStep(HackStepKind.Disconnect, target, default, "dc"));
            }

            // 断开已让 TraceTracker 自己失效；这一步是确定性的兜底 ——
            // stay 模式（不断开）下它是唯一的止血点。走 stop()，零每帧开销。
            steps.Add(new HackStep(HackStepKind.KillTrace, target, default, null));
        }

        // 被剔除的机器照样清痕：它们此前进过、破过、侦察过，/log 里留着痕迹，
        // 「跳过入侵」不等于「放过证据」。排在全部正常步骤之后 —— 正常流程不会再碰
        // 这些机器，此刻清是终点动作，不会有新记录再追加进来。
        // 对没有痕迹的机器是幂等的：ClearLogs 返回空列表，不产生任何输出。
        if (options.ClearLogs)
        {
            foreach (var comp in skipped)
            {
                steps.Add(new HackStep(HackStepKind.CleanLogs, comp, default, null));
            }
        }

        return steps;
    }

    /// <summary>
    /// 按脚本给一个目标展开动作。脚本只描述「怎么打」，目标与源机由 scope 解析
    /// —— 游戏 HackerScript 的 <c>config</c> 行同时指定目标与源机，但那是给 NPC
    /// 用的（它的 connect 走 cConnection，目标机视角），对玩家终端无意义。
    ///
    /// 前置始终补三步（脚本不必写、写了也会去重）：<c>connect</c>（未连接时）、
    /// <c>neutralize</c>（解除管理员反扑，否则断开后 0~20 秒肉鸡标记丢失）、
    /// 末尾的 <c>killtrace</c>（零开销兜底）。这三步是正确性要求而非风格偏好，
    /// 交给玩家手写只会漏。
    /// </summary>
    private static void AppendScripted(
        List<HackStep> steps, Computer target, HackScript script, HackOptions options, OS os)
    {
        var emitted = new HashSet<HackStepKind>();
        var connected = ReferenceEquals(target, os.connectedComp);

        // 连接与解除反扑是前置条件，不是脚本可选项 —— 见方法注释。
        // 但 direct 模式（ConnectFirst=false）下玩家可能就是要靠脚本自己连，
        // 故那种情况不登记 connect，把决定权留给脚本。
        var connectRequired = options.ConnectFirst;
        if (connectRequired && !connected)
        {
            steps.Add(new HackStep(HackStepKind.Connect, target, default, "connect " + target.ip));
            emitted.Add(HackStepKind.Connect);
        }

        steps.Add(new HackStep(HackStepKind.Neutralize, target, default, null));
        emitted.Add(HackStepKind.Neutralize);

        foreach (var action in script.Actions)
        {
            // connect 已被前置步骤登记过时（ConnectFirst 模式），脚本里再写就跳过。
            if (action.Kind == HackStepKind.Connect)
            {
                if (connected || !emitted.Add(HackStepKind.Connect))
                {
                    continue;
                }

                steps.Add(new HackStep(HackStepKind.Connect, target, default, "connect " + target.ip));
                continue;
            }

            // OpenPort 可重复（逐端口展开）。CleanLogs 也可重复 —— 它对空 /log
            // 是幂等的（ClearLogs 返回空列表、不输出），而「证据必须消失」是硬承诺，
            // 让玩家写两次就多清一次比静默吞掉第二个更符合预期。
            // 其余动作改的是游戏状态，重复出现只取首次。
            if (action.Kind is not (HackStepKind.OpenPort or HackStepKind.CleanLogs) &&
                !emitted.Add(action.Kind))
            {
                continue;
            }

            if (action.Kind == HackStepKind.OpenPort)
            {
                foreach (var step in ExpandPorts(target, action.Port))
                {
                    steps.Add(step);
                }

                continue;
            }

            steps.Add(new HackStep(action.Kind, target, default, CommandFor(action.Kind)));
        }

        // 兜底反追踪：与内置次序同理，脚本没写也要有 —— 断开已让它失效，
        // 这一步覆盖「脚本以 stay 结尾」与「最后一步之后才被点燃」的窗口。
        if (emitted.Add(HackStepKind.KillTrace))
        {
            steps.Add(new HackStep(HackStepKind.KillTrace, target, default, null));
        }
    }

    /// <summary>
    /// 展开 <c>openPort</c> 动作。<paramref name="portNumber"/> 为 0 = 该目标上全部
    /// 有原生破解程序的端口；否则只挑匹配的那一个（按显示端口号比对，与玩家在终端里
    /// 敲的 <c>sshcrack 22</c> 同一个数）。指定的端口不存在或不含破解程序时展开为空 ——
    /// 不臆造步骤。
    /// </summary>
    private static IEnumerable<HackStep> ExpandPorts(Computer target, int portNumber)
    {
        foreach (var port in HackEngine.CrackablePorts(target))
        {
            if (portNumber != 0 && port.DisplayPort != portNumber && port.CodePort != portNumber)
            {
                continue;
            }

            yield return new HackStep(HackStepKind.OpenPort, target, port, HackEngine.CrackCommand(port));
        }
    }

    /// <summary>
    /// 脚本动作对应的终端回显原文；返回 null 表示由 Apply 的分支自行处理
    /// （<c>Connect</c> 拼 ip、<c>OpenPort</c> 用破解程序名、其余非终端指令一律不回显）。
    /// </summary>
    private static string CommandFor(HackStepKind kind) => kind switch
    {
        HackStepKind.Disconnect => "dc",
        HackStepKind.Escalate => "porthack",
        HackStepKind.Probe => "probe",
        HackStepKind.CleanLogs => "rm /log/*",
        _ => null,
    };
}

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
/// 五条必须尊重的游戏机制：
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
/// 5. <b>推进没有任何时间节流</b>（v1.34.0 起）。此前非端口步吃 NormalStepDelay
///    （0.35s）、端口步吃 PortDelay（缺省 0.6s），实测存档 167 台 / 642 可破端口
///    一轮要 5 分钟以上，其中 292 秒纯粹是「等」—— 而关掉演出后端口在
///    ApplyOpenPort 里立即开，压根没有可等的东西。现在整轮只剩三道闸门：
///    单帧步数预算、<b>端口步每帧一步（与演出开关无关）</b>、提权前的动画等待
///    （仅演出开着时）。实测同一存档：noshow 一轮由 5.2 min 降到约 11 秒。
/// </summary>
internal sealed class HackRun
{
    /// <summary>
    /// 等本台端口开完的上限（秒），超时就把剩下的端口直接开掉并继续。
    ///
    /// <b>为什么必须有上限。</b>等待的判据是「端口是否已 Cracked」，而端口由动画的
    /// <c>Completed()</c> 去开；动画要挂上 RAM 面板才跑，挂载受 <c>os.ramAvaliable</c>
    /// 门禁。玩家自己开着几个吃内存的 exe（ShellExe 600mb 之类）时，本插件排的动画
    /// 可能永远装不下 —— 那时没有上限就是<b>整轮永久停在 ESCALATING</b>，比少开一个
    /// 端口糟得多。
    ///
    /// 取 180 秒：实测单台最坏 72.3 秒（167 台 / 642 可破端口全量仿真），留 2.5 倍余量。
    /// 超时走 <see cref="NativeExes.Flush"/> —— 把还没开的直接开掉，战果不丢。
    /// </summary>
    private const float NativeWaitCapSeconds = 180f;

    /// <summary>在提权等待上已经花掉的秒数（见 <see cref="NativeWaitCapSeconds"/>）。</summary>
    private float _waitSeconds;

    private readonly List<Computer> _targets;
    private readonly List<HackStep> _steps;

    private int _index;

    /// <summary>本次运行中靠已知凭据登入（即已提权）的机器，其破端口类步骤整体跳过。</summary>
    private readonly HashSet<Computer> _loggedIn = new();

    /// <summary>
    /// 连接被目标主动拒绝（白名单）的机器。
    ///
    /// 它**只跳过依赖连接的步骤**（<see cref="HackStepKind.Connect"/>、
    /// <see cref="HackStepKind.CleanLogs"/>、<see cref="HackStepKind.Disconnect"/>），
    /// 其余（破端口 / 解跳板 / 解防火墙 / login / 提权 / 投放）全部照常执行 ——
    /// 那些步骤直接操作 <c>target</c> 对象，本就不经过连接。
    ///
    /// v1.23.0 曾整段跳过这类目标，理由是「连接没成而后续照样成功 = 假战果」。
    /// 那个顾虑只对<b>依赖连接语义</b>的步骤成立（rm 的目标机取自
    /// <c>os.connectedComp</c>，断开后 rm 会打到自己的文件系统上）；
    /// 对操作对象本身的步骤并不成立 —— 端口表的 <c>Cracked</c> 状态、
    /// <c>giveAdmin</c> 写入的 <c>adminIP</c> 都落在目标机上，是真的战果。
    /// 整段跳过等于把一台本来能拿下的机器直接放弃，这才是真损失。
    /// </summary>
    private readonly HashSet<Computer> _refused = new();


    /// <summary>本次运行的入侵脚本；null = 用内置次序。构造期已解析完成。</summary>
    private readonly HackScript _script;

    internal HackRun(OS os, HackOptions options)
    {
        Options = options;

        // 脚本在构造期解析一次：语法错/文件缺失在这里抛出，由调用方转成终端可见
        // 的错误行。放进 Tick 会让错误每帧重复，放进 BuildSteps 会让「目标为空」
        // 与「脚本坏了」两种情况混在一起。
        _script = string.IsNullOrEmpty(options.Script) ? null : HackScript.Load(options.Script);

        // 先揭图，再解析目标 —— 用户定的次序（「先扫描，后入侵」）。
        //
        // 只改「地图上看得见什么」，不改目标池：ReachableComputers 的池是沿 links
        // 的传递闭包，揭图不改变闭包；ConnectableComputers 的池是地图全表，
        // 与 visibleNodes 无关。故这一步是纯粹的观感前置，零语义风险。
        HackEngine.RevealMap(os, options.AllNodes);

        var plan = HackEngine.ResolveTargets(os, options);
        _targets = plan.Targets;
        SkippedOwned = plan.SkippedOwned;

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

        // 推进不再有任何时间节流（v1.34.0 起，见类注释）。deltaSeconds 只服务于
        // 提权等待的上限计时（见 WaitForNativeAnimation），不参与步进节流。
        // 剩下的三道闸门：
        // ① 端口步每帧只推进一步（与演出开关无关，见循环末尾）—— 它同时管
        //    动画入队速率与终端回显速率；
        // ② 提权前的原生动画等待 —— 仅当演出开着（见 WaitForNativeAnimation）；
        // ③ 单帧步数预算 MaxStepsPerFrame。
        var budget = MaxStepsPerFrame;
        while (!Finished && budget-- > 0)
        {
            if (_index >= _steps.Count)
            {
                Finish(os);
                return;
            }

            var step = _steps[_index];

            // 该目标的连接被拒（白名单）：只跳过**依赖连接**的步骤，其余照常执行。
            // 依据见 _refused 的字段注释 —— 破端口/提权/投放操作的是 target 对象本身，
            // 不经连接，跳过它们等于白白放弃一台能拿下的机器。
            if (_refused.Contains(step.Target) && DependsOnConnection(step.Kind))
            {
                _index++;
                continue;
            }

            // 该目标已靠 login 提权：破端口/解防火墙/porthack 都是无用功，
            // 直接跳过（不回显、不耗时）。停机也计入 Done，进度条才走得准。
            if (IsRedundantAfterLogin(step))
            {
                _index++;
                continue;
            }

            if (WaitForNativeAnimation(step, deltaSeconds, os))
            {
                return;
            }

            _waitSeconds = 0f;
            Apply(os, step);
            _index++;

            // 端口步每帧只推进一步 —— **与演出开关无关**（用户定，v1.34.0）。
            //
            // 这道闸同时管两件事，两件都要求「一帧一个」：
            // ① **动画入队速率**（演出开着时）：一帧至多入队一个动画请求，
            //    动画由 RAM 门禁自行并发（实测峰值 3 个同屏）；
            // ② **终端回显速率**（两种模式都要）：一帧至多一行破解指令。
            //
            // 去掉它，一帧 512 步的预算会把整台的端口（noshow 下是整轮 642 个）
            // 挤进同一帧 —— 终端一次刷出十几到几百行 sshcrack，玩家根本看不清
            // 破了哪个端口，动画请求也会全挤进队列。
            //
            // 代价是端口步恒为 1 帧/个（60 个/秒）：642 个端口约 10.7 秒。
            // 这是刻意的取舍 —— 战果要看得见，而 60/秒已远快于任何人工节奏。
            if (step.Kind == HackStepKind.OpenPort)
            {
                return;
            }
        }
    }

    /// <summary>
    /// 提权前的等待：本台交给动画的端口还没开完就不推进，返回 true 表示本帧就此打住；
    /// 超过上限则把剩余端口直接开掉并回显，随后照常推进。
    /// </summary>
    private bool WaitForNativeAnimation(HackStep step, float deltaSeconds, OS os)
    {
        // 提权读的是已破解端口数（HackEngine.CanEscalate → OpenPortCount >
        // portsNeededForCrack 且防火墙已解）。端口现在由动画的 Completed() 去开，
        // 故必须等本台交给动画的端口全部开完再提权 —— 否则读数是假的，
        // porthack 门禁必然不过，终端会多打一行 escalation gate not met。
        //
        // 等的是战果本身（PortState.Cracked），不是动画内部状态：九种程序的完成标志
        // 各不相同，而 Cracked 一置真就说明该端口确实开好了（见 NativeExes.Settled）。
        //
        // 不推进 _index，下一帧重来 —— 这是「等」，不是「跳过」。
        if (step.Kind != HackStepKind.Escalate || NativeExes.Settled(step.Target))
        {
            return false;
        }

        _waitSeconds += deltaSeconds;
        if (_waitSeconds < NativeWaitCapSeconds)
        {
            return true;
        }

        // 超时兜底：把这一台还没开的端口直接开掉，不再等动画。
        // 必须回显 —— 玩家看到的动画没有跑完，得知道战果是补上的。
        //
        // force 连还在播的一起补，且复位计时：否则下一帧 Settled 仍为假，
        // 会每帧重进这个分支、把同一行刷满终端。
        var forced = NativeExes.Flush(os, step.Target, force: true);
        Trace.Write("escalate wait timed out on " + step.Target.ip + " after " + (int)NativeWaitCapSeconds + "s - forced " + forced + " port(s) open");
        _waitSeconds = 0f;
        os.write("[autohack] " + step.Target.name + " :: animation wait timed out after "
            + (int)NativeWaitCapSeconds + "s - opened " + forced
            + " remaining port(s) directly.");

        return false;
    }

    /// <summary>单帧最多执行多少步，防止 Instant 档在极端规模下一帧卡死。</summary>
    private const int MaxStepsPerFrame = 512;

    /// <summary>
    /// 该步骤是否依赖「已连接」这一状态。
    ///
    /// 只有两个：Connect 自己与 Disconnect（未连接时是空操作）。其余步骤操作的是
    /// <c>target</c> 对象本身，不经连接 —— 端口表的 <c>Cracked</c> 状态、
    /// <c>giveAdmin</c> 写入的 <c>adminIP</c>、以及清痕走的
    /// <c>Computer.deleteFile(ipFrom, 名, folderPath)</c> 都直接落在目标机上。
    ///
    /// 清痕曾在这个名单里 —— 那时它回显并依赖 <c>rm log/*</c>，而 <c>rm</c> 的目标机
    /// 取自 <c>os.connectedComp</c>（Programs.cs:956）。改为按 IP 逐条点名删之后
    /// （见 <see cref="HackEngine.WipeTraces"/>），它连回显都不需要了，自然也不再依赖连接。
    /// </summary>
    private static bool DependsOnConnection(HackStepKind kind) => kind switch
    {
        HackStepKind.Connect => true,
        HackStepKind.Disconnect => true,
        _ => false,
    };

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

    private void Apply(OS os, HackStep step)
    {
        var target = step.Target;
        Current = target.name + " @ " + target.ip;

        switch (step.Kind)
        {
            case HackStepKind.Connect:
                ApplyConnect(os, step);
                break;

            case HackStepKind.Neutralize:
                ApplyNeutralize(os, target);
                break;

            case HackStepKind.BypassProxy:
                ApplyBypassProxy(os, target);
                break;

            case HackStepKind.Login:
                ApplyLogin(os, target);
                break;

            case HackStepKind.Probe:
                ApplyProbe(os, step);
                break;

            case HackStepKind.OpenPort:
                ApplyOpenPort(os, step);
                break;

            case HackStepKind.SolveFirewall:
                ApplySolveFirewall(os, target);
                break;

            case HackStepKind.Escalate:
                ApplyEscalate(os, step);
                break;

            case HackStepKind.BypassWhitelist:
                ApplyBypassWhitelist(os, target);
                break;

            case HackStepKind.UploadMarker:
                ApplyUploadMarker(os, target);
                break;

            case HackStepKind.CleanLogs:
                ApplyCleanLogs(os, target);
                break;

            case HackStepKind.Disconnect:
                ApplyDisconnect(os, step);
                break;

            case HackStepKind.KillTrace:
                ApplyKillTrace(os);
                break;
        }
    }

    /// <summary>
    /// 连接目标：回显 connect、执行连接，并核对是否被目标的白名单拒绝；
    /// 被拒时登记 <see cref="_refused"/> 并报一行状态，后续步骤照常执行。
    /// </summary>
    private void ApplyConnect(OS os, HackStep step)
    {
        var target = step.Target;
        Phase = "CONNECTING TO " + Upper(target.name);
        Echo(os, step.Command);
        Programs.connect(["connect", target.ip], os);

        // 连接可以被目标主动拒绝。带 WhitelistConnectionDaemon 的机器在
        // Computer.connect 里检查白名单，不通过就调 DisconnectTarget() 并
        // return false（Computer.cs:383-388）；Programs.connect 只写一行
        // "External Computer Refused Connection" 再把 os.connectedComp 置 null
        // （Programs.cs:313-322）。两者都不返回值，故只能核对结果。
        //
        // 不核对就是假战果：OpenPort / Escalate / CleanLogs 全部直接对 target
        // 对象操作，压根不经过连接，于是玩家看到 Refused 却收到「入侵成功」。
        if (!ReferenceEquals(os.connectedComp, target))
        {
            _refused.Add(target);
            os.write("[autohack] " + target.name
                + " :: connection refused (whitelist) - continuing without a session");
            return;
        }
    }

    /// <summary>解除目标的延迟反扑，仅在实际解除时由 Neutralize 报一行。</summary>
    private void ApplyNeutralize(OS os, Computer target)
    {
        Phase = "DISABLING COUNTERATTACK ON " + Upper(target.name);
        Neutralize(os, target);
    }

    /// <summary>解除目标的跳板，仅在实际解除时由 BypassProxy 报一行。</summary>
    private void ApplyBypassProxy(OS os, Computer target)
    {
        Phase = "BYPASSING PROXY ON " + Upper(target.name);
        BypassProxy(os, target);
    }

    /// <summary>
    /// 用已知凭据登录目标：成功即提权并登记 <see cref="_loggedIn"/>（后续破端口类步骤整体跳过），
    /// 失败也写一行，避免「为什么没跳过」这条信息静默丢失。
    /// </summary>
    private void ApplyLogin(OS os, Computer target)
    {
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
        else
        {
            // 失败必须可见：静默会把「为什么没跳过」这条最有价值的信息藏起来。
            os.write("[autohack] " + target.name + " :: login unavailable - cracking ports");
        }
    }

    /// <summary>侦察目标：连着目标时才回显 probe（否则是假命令），端口报告无条件输出。</summary>
    private void ApplyProbe(OS os, HackStep step)
    {
        var target = step.Target;
        Phase = "PROBING " + Upper(target.name);

        // 只有连着目标时 probe 才是在探它 —— 未连接时 Programs.probe 的目标是
        // os.connectedComp ?? os.thisComputer（Programs.cs:1387），回显会是假命令。
        // 端口报告本身取自目标对象（HackEngine.ProbeReport），不看连接，故照常输出。
        if (os.connectedComp == target)
        {
            Echo(os, step.Command ?? "probe");
        }
        // 端口报告**无条件**打（v1.34.0 修正）。
        //
        // 曾短暂地在 noshow 下压掉它，理由是「167 台上千行太吵」。那是把
        // 「不要动画」误读成「不要输出」—— 演出开关管的是原生破解程序要不要
        // 挂进 RAM 面板，而 probe 报告是**侦察结果**（目标开了哪些端口、
        // 要破几个才够提权门槛），属于战果本身，不是过程演出。
        // 没有它，noshow 下玩家只能看到一串 sshcrack，看不到在打什么。
        foreach (var line in HackEngine.ProbeReport(target))
        {
            os.write(line);
        }
    }

    /// <summary>
    /// 破解单个端口：连着目标时才回显破解程序；端口优先交给原生动画去开，
    /// 演出不可用时立即直接补开，保证端口不会永远不开。
    /// </summary>
    private void ApplyOpenPort(OS os, HackStep step)
    {
        var target = step.Target;
        Phase = "CRACKING PORT " + step.Port.DisplayPort;

        // 破解程序的作用域是「当前连接」；未连接时它无从打到目标上，
        // 而下面的 HackEngine.OpenPort 是直接写目标机端口表，照样生效。
        if (os.connectedComp == target)
        {
            Echo(os, step.Command);
        }

        // 端口交给动画去开（v1.33.3）：那 9 个 exe 各自在 Completed() 里调
        // openPort(<自己的原始终端口号>, ip)，与这里调 HackEngine.OpenPort 落在
        // 同一个 PortState.Cracked 上（ComputerExtensions.cs:184-197 的 Prefix
        // 直接拿调用方传入的原始端口号匹配 Record.OriginalPortNumber）。
        // 故「动画跑完端口才开」不需要造机制 —— 只要不再提前写。
        //
        // Show 返回 false 的每一种情形都必须立即补开，否则端口永远不开：
        // 未连接 / 已控 / 该端口没有可安全演出的程序（实测 73/642）/
        // 队列满被丢弃。判据见 NativeExes.Show 的文档注释。
        if (!Options.ShowExes || !NativeExes.Show(os, target, step.Port))
        {
            HackEngine.OpenPort(target, step.Port, os.thisComputer.ip);
        }
    }

    /// <summary>解目标防火墙：连着目标时才回显 solve，实际解除时由 SolveFirewall 报一行。</summary>
    private void ApplySolveFirewall(OS os, Computer target)
    {
        Phase = "BYPASSING FIREWALL ON " + Upper(target.name);

        // 同 OpenPort：solve 命令的作用域是当前连接，未连接时是假命令。
        if (os.connectedComp == target)
        {
            Echo(os, "solve " + (target.firewall?.solution ?? string.Empty));
        }

        SolveFirewall(os, target);
    }

    /// <summary>
    /// 提权：连着目标时才回显 porthack；先试原生门禁，过不了就强制写 adminIP，
    /// 并把「门禁没过」这件事写进终端。
    /// </summary>
    private void ApplyEscalate(OS os, HackStep step)
    {
        var target = step.Target;
        Phase = "ESCALATING";

        // 未连接时 porthack 在终端里无从下手（它读 os.connectedComp），
        // 而下面的 giveAdmin 是直接写目标机的 adminIP，照样生效。
        if (os.connectedComp == target)
        {
            Echo(os, step.Command ?? "porthack");
        }

        // 只用 giveAdmin，不用 os.takeAdmin(ip)：后者内部还会 runCommand("connect " + ip)
        // （OS.cs:1871-1879），而 connect 的第一件事就是无条件断开旧连接
        // （Programs.connect，Programs.cs:235-236）—— 那会立刻触发 handleDisconnection
        // 与管理员反扑，等于自找麻烦。此处已连着目标，写所有权标记即可。
        // 两道门禁：先试原生 porthack 语义（端口数越不过门槛且防火墙已解），
        // 过不了就直接写 adminIP —— 与原生提权成功的终态完全一致
        // （<c>giveAdmin</c> 就是游戏自己提权时走的那一步，Computer.cs:851-855）。
        //
        // 这曾是可选开关（v1.33.1 的 ForceEscalate，缺省开），v1.33.2 起常驻：
        // porthack 的门禁对实测存档里 22 台机器恒假（9 台防护机门槛 9999998、
        // 13 台普通机器破满端口也差 1~6 个），留一个「默认开、关了就打不下」
        // 的开关，等于给唯一出路配了个自毁按钮。
        //
        // 门禁过不了时**必须说出来**：静默成功会让玩家以为这台机器是靠破端口拿下的。
        if (HackEngine.CanEscalate(target))
        {
            target.giveAdmin(os.thisComputer.ip);
        }
        else if (HackEngine.ForceEscalate(target, os))
        {
            os.write("[autohack] " + target.name
                + " :: escalation gate not met (needs > " + target.portsNeededForCrack
                + " open port(s), have " + HackEngine.OpenPortCount(target)
                + ") - forced admin anyway");
        }
    }

    /// <summary>
    /// 绕过目标白名单：只对确实连不上的目标动手，放行后重连并解除
    /// <see cref="_refused"/> 登记；连着时是空操作。
    /// </summary>
    private void ApplyBypassWhitelist(OS os, Computer target)
    {
        // 只对确实连不上的目标动手 —— 连接正常的机器没必要（也不该）改它的白名单。
        //
        // 判据不能写成「本轮被拒过」（_refused）：只有 Connect 步失败才会登记，
        // 而「当前节点」模式压根不排 Connect 步（BuildSteps 里 alreadyConnected
        // 为真时跳过，见 :679-683），于是这一步在所有直连路径上都静默空转 ——
        // 正是「当前节点模式下带白名单的机器没反应」的最后一道门。
        // 改为核对连接结果：连着就别碰，连不上才绕。
        if (ReferenceEquals(os.connectedComp, target))
        {
            return;
        }

        Phase = "BYPASSING WHITELIST ON " + Upper(target.name);

        // 这一步不是终端指令（未连接状态下 <c>append</c> 的「当前目录」是玩家
        // 自己的文件系统，回显出来会是一条假命令），故不发 Echo，只报状态。
        var bypassNote = HackEngine.BypassWhitelist(os, target, os.thisComputer.ip);
        if (bypassNote == null)
        {
            os.write("[autohack] " + target.name
                + " :: no /Whitelist folder to touch - staying session-less");
            return;
        }

        os.write("[autohack] " + target.name + " :: " + bypassNote);

        // 白名单已放行，重连应当成功；成了就恢复正常流程
        // （清痕与断开都依赖连接，之前被 DependsOnConnection 挡掉了）。
        Programs.connect(["connect", target.ip], os);
        if (ReferenceEquals(os.connectedComp, target))
        {
            _refused.Remove(target);
            os.write("[autohack] " + target.name + " :: reconnected - whitelist bypassed");
        }
    }

    /// <summary>投放标记文件：只在目标确已拿下（含靠 login 提权）时写 autohack.txt。</summary>
    private void ApplyUploadMarker(OS os, Computer target)
    {
        Phase = "UPLOADING PAYLOAD";
        UploadMarker(os, target);
    }

    /// <summary>
    /// 清痕：按 IP 逐条点名删目标 /log 中提及玩家 IP 的条目，删到才报一行；
    /// 不回显 rm，也不依赖连接。
    /// </summary>
    private void ApplyCleanLogs(OS os, Computer target)
    {
        Phase = "WIPING TRACES";

        // 不回显 rm：这一步已不是「敲一条终端命令」，而是按 IP 逐条点名删
        // 日志条目（见 HackEngine.WipeTraces）。回显 rm log/* 反而误导 ——
        // 玩家会以为整个 /log 被清空了，实际只删了提到自己 IP 的那些。
        //
        // 不需要连接：目标机与目录都由参数给定，不读 os.connectedComp。
        var wiped = HackEngine.WipeTraces(target, os.thisComputer.ip);

        // 战果必须可见。删 0 条时不吭声 —— 无痕迹的机器是多数。
        if (wiped.Count > 0)
        {
            os.write("[autohack] " + target.name + " :: wiped "
                + wiped.Count + " log entr" + (wiped.Count == 1 ? "y" : "ies")
                + " mentioning " + os.thisComputer.ip);
        }
    }

    /// <summary>断开连接：由 Leave 解除反扑、回显并静默断开；未连接时是空操作。</summary>
    private void ApplyDisconnect(OS os, HackStep step)
    {
        Leave(os, step.Target, step.Command ?? "dc");
    }

    /// <summary>终止进行中的追踪，实际停掉时写一行确认。</summary>
    private void ApplyKillTrace(OS os)
    {
        Phase = "KILLING TRACE";
        KillTrace(os);
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
        // 未连接时连回显都不做：Leave 是「收尾」，没连接就没有可收的尾。
        if (os.connectedComp == null)
        {
            return;
        }

        Neutralize(os, target);
        Echo(os, command);

        // 静默断开的全部理由（为什么静音、为什么多人不静音）见
        // <see cref="HackEngine.SilentDisconnect"/> —— 面板的 drop 工具走同一条。
        HackEngine.SilentDisconnect(os);
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

    /// <summary>
    /// 回显一条指令：格式与 <c>OS.runCommand</c> 完全一致（换行 + 当前提示符 + 原文）。
    ///
    /// 命令以「当前连接」为作用域时才回显 —— 未连接时玩家敲同一条命令得到的是
    /// 另一个效果（<c>probe</c> 会探测自己、<c>porthack</c> 无从下手），
    /// 回显它就成了一条假命令。<c>Apply</c> 已在每处按需决定是否回显，
    /// 这里只兜住「拿不准就别写」这一条。
    /// </summary>
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

        // 收尾演出 —— 与反追踪同理，无条件执行：本轮的破解动画不该在「已经打完了」
        // 之后继续拖尾。队列里通常还满着（容量 8，每个动画 8~33 秒，而并发上限由
        // RAM 决定、只有 2~3 个），不截断的话收尾后还要空播半分钟以上。
        // 已在面板上的那个走游戏自己的淡出，2 秒内消失（见 NativeExes.StopAll）。
        NativeExes.StopAll(os);

        // 收尾全网清痕 —— 覆盖本轮目标池之外的历史痕迹（此前访问过、但不在池里的机器）。
        // 排在反追踪与换 IP 之前：判据是玩家当前的 IP，换完之后就清不掉了。
        //
        // 只在这一轮确实要清痕时做：keep 是玩家显式要求留痕，不该被收尾兜底绕过。
        // 被跳过（已控）与玩家自己的机器由 WipeNetwork 一并覆盖，不再单独追加。
        HackEngine.WipeForRun(os, Options);

        // 收尾反追踪 —— 无条件执行，不是选项：止住倒计时 + 清空脱机追踪列表。
        // **不擦追踪者的 /log**：那是目标机的操作史，反追踪不该顺手替玩家做决定。
        // 已知代价 —— 追踪的复发源正是那些日志：OS.handleDisconnection（OS.cs:944-960）
        // 在每次断开时检查刚断开那台的 /log，只要有一行同时含玩家 IP 与
        // FileCopied/FileDeleted/FileMoved，就自动排入一条新的 TrackerDetail
        // （判据见 TrackerCompleteSequence.CompShouldStartTrackerFromLogs，:30-47），
        // 10~20 秒后计时归零端掉玩家。
        //
        // 复发概率已被本版的清痕口径压到很低：CleanLogs 步删的正是「含玩家 IP」的条目
        // （HackEngine.WipeTraces），那恰好就是这条判据的输入。仍留着这一层是因为
        // 收尾清痕与断开之间有窗口（换 IP、ResetIP 等），且脚本模式可能不排清痕步。
        //
        // 排在换 IP 之前：语义上「先收拾追踪、再换身份」；且换 IP 会改掉
        // os.thisComputer.ip，任何按旧 IP 匹配的判断都必须在它之前做完。
        TraceTools.Run(os);

        // 换 IP：游戏原生的「保命」动作（ISPDaemon 的 "Assign New IP"），
        // 并把全图已控机器的归属迁移到新 IP。理由与代价见 IpTools 的文档注释。
        //
        // 排在清追踪之后：TraceTools 判「旧 IP 是否在日志里」只在擦日志时才有意义 ——
        // 现在不擦日志了，但顺序仍然保持，因为语义上「先收拾追踪、再换身份」更清楚。
        if (Options.ResetIP)
        {
            IpTools.Run(os);
        }

        foreach (var target in _targets)
        {
            // 两个数取自同一次快照：分两次查会各分配一份端口表副本
            // （GetAllPortStates 是 ...Values.ToList()），且理论上可能显示
            // opened > total 的不一致比例。PortInfo 自带 Cracked，无需第二次遍历。
            var ports = HackEngine.Ports(target);
            var opened = ports.Count(p => p.Cracked);

            // 所有权看 adminIP（肉鸡标记的真身），不看 CanEscalate ——
            // 后者要求端口已破，靠 login 拿下的目标会因为 0 端口而被误报 admin=no。
            var owned = HackEngine.IsOwned(target, os);
            Outcomes.Add(new TargetOutcome(target.name, opened, ports.Count, owned));
            os.write("[autohack] " + target.name + " :: " + opened + "/" + ports.Count
                + " ports, admin=" + (owned ? "yes" : "no"));
        }

        if (_targets.Count == 0)
        {
            // 目标为 0 是最容易被误读的状态：终端仍会逐台打清痕行，
            // 看着像「每台都重跑了一遍」，实际每台只抹了 log。
            // 必须把原因和出路直接写出来。
            os.write("[autohack] No targets: all " + SkippedOwned
                + " reachable node(s) were already owned.");
            os.write("[autohack]   'redo' includes owned nodes; 'allnodes' sweeps the whole map.");
        }

        if (SkippedOwned > 0)
        {
            os.write("[autohack] skipped " + SkippedOwned + " node(s) already owned - 'redo' to include them.");
        }

        if (_refused.Count > 0)
        {
            os.write("[autohack] " + _refused.Count
                + " node(s) refused the session (whitelist) - cracked without one.");
        }

        Current = "done - " + _targets.Count + " target(s)";
    }

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

            AppendTargetSteps(steps, target, options, os);
        }

        AppendSkippedSteps(steps, skipped, options);
        AppendSelfStep(steps, os, options);

        return steps;
    }

    /// <summary>
    /// 为一个目标展开内置次序：连接 → 侦察 → 解跳板 → 逐端口攻破 → 提权 → 投放 → 清痕 → 断开。
    /// 本方法只负责把四段串起来，各段的判据与注释都在对应的小方法里。
    /// </summary>
    private static void AppendTargetSteps(
        List<HackStep> steps, Computer target, HackOptions options, OS os)
    {
        AppendEngageSteps(steps, target, options, os);
        AppendPortSteps(steps, target);
        AppendEscalateSteps(steps, target);
        AppendCleanupSteps(steps, target, options);
    }

    /// <summary>入场的四步：连接（可选）→ 解除反扑 → 侦察 → 登录（可选）→ 解跳板（可选）。</summary>
    private static void AppendEngageSteps(
        List<HackStep> steps, Computer target, HackOptions options, OS os)
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
        //
        // EOS 设备无条件排这一步：它的端口容量等于提权门槛，破端口是死路，
        // 固定密码 "alpine" 才是游戏设计的正路（见 HackEngine.IsEosDevice）。
        if (options.UseCredentials || HackEngine.IsEosDevice(target))
        {
            steps.Add(new HackStep(HackStepKind.Login, target, default, null));
        }

        var ports = HackEngine.CrackablePorts(target);
        if (ports.Count > 0 && HackEngine.ProxyActive(target))
        {
            steps.Add(new HackStep(HackStepKind.BypassProxy, target, default, null));
        }
    }

    /// <summary>逐端口攻破；每个端口后跟一步反追踪（理由见循环内注释）。</summary>
    private static void AppendPortSteps(List<HackStep> steps, Computer target)
    {
        foreach (var port in HackEngine.CrackablePorts(target))
        {
            steps.Add(new HackStep(HackStepKind.OpenPort, target, port, HackEngine.CrackCommand(port)));

            // 每个端口后立刻清一次追踪（用户定的行为；此前只在每台末尾清一次）。
            //
            // 计时上两者都安全，不是缺陷修复：traceTime = max(10 - security, 3) * 15，
            // 最小 45 秒（Computer.cs:224），而单台的端口步总耗时是毫秒量级
            // （v1.34.0 起已无步进间隔）—— 每台清一次就已经比倒计时快好几个数量级。
            // 每端口一次的实际差别是「追踪状态在端口之间也归零」：演出 exe 的构造体
            // 会调 hostileActionTaken()（PacificPortExe.cs:26、SSHCrackExe.cs:90、
            // SMTPoverflowExe.cs:61 等），traceTime > 0 时即
            // os.traceTracker.start → os.warningFlash()（Computer.cs:300、
            // TraceTracker.cs:110）—— 每端口清一次，跑的过程中就不会出现常亮的
            // 追踪条与闪屏。
            //
            // 成本为零：KillTrace 不吃节流（见 DelayFor），且未激活时
            // HackEngine.KillTrace 直接返回 false、连一行输出都没有。
            //
            // 脚本模式不插这一步 —— 那份次序由玩家显式书写，killtrace 是脚本可写的
            // 动作（HackScript.cs:81），末尾也已有兜底，不该由插件改写脚本语义。
            steps.Add(new HackStep(HackStepKind.KillTrace, target, default, null));
        }
    }

    /// <summary>解防火墙 → 提权 → 白名单绕过 → 投放标记。</summary>
    private static void AppendEscalateSteps(List<HackStep> steps, Computer target)
    {
        // 防火墙必须在 porthack 之前解 —— 游戏自己的门禁要求
        // firewall.solved（OS.cs:1918-1930），未解则 porthack 直接被拒。
        if (target.firewall is { solved: false })
        {
            steps.Add(new HackStep(HackStepKind.SolveFirewall, target, default, null));
        }

        steps.Add(new HackStep(HackStepKind.Escalate, target, default, "porthack"));

        // 白名单机器无条件排一步绕过（与开关解耦）：它的 connect 会被拒，
        // 而破端口/提权不经连接、照样能拿下 —— 拿下之后再把自己的 IP 追加进它的
        // /Whitelist/list.txt，重连即恢复会话，后续清痕与断开才走得通。
        // 这是游戏设计的正路（官方任务 PAE2_Whitelist.xml 的 list_add_manual.txt
        // 明写「append list.txt <你的IP>」），详见 HackEngine.BypassWhitelist。
        if (HackEngine.HasWhitelist(target))
        {
            steps.Add(new HackStep(HackStepKind.BypassWhitelist, target, default, null));
        }
    }

    /// <summary>收尾：投放（可选）→ 清痕（可选）→ 断开（可选）→ 反追踪兜底。</summary>
    private static void AppendCleanupSteps(List<HackStep> steps, Computer target, HackOptions options)
    {
        if (options.UploadMarker)
        {
            steps.Add(new HackStep(HackStepKind.UploadMarker, target, default, null));
        }

        // 清痕排在断开**之前**是硬要求：断开本身会往目标 /log 写一条
        // "<玩家IP> Disconnected"（Computer.disconnecting，Computer.cs:722-727），
        // 那条也含玩家 IP，排在断开后就得再清一遍。
        //
        // 不需要回显也不需要连接：清痕走
        // Computer.deleteFile(ipFrom, 名, folderPath)（HackEngine.WipeTraces），
        // 目标机与目录都由参数给定，不读 os.connectedComp / navigationPath ——
        // 这正是它从 DependsOnConnection 名单里退出来的原因。
        //
        // 这一步**不设开关**：留痕的代价是带 tracker="true" 的机器在断开时
        // 自动排一个脱机追踪（OS.handleDisconnection，OS.cs:944-960 →
        // TrackerCompleteSequence.cs:30-47），10~20 秒后计时归零端掉玩家。
        // 而 wipe 删的只是含玩家 IP 的条目，目标机自己的历史原样保留 ——
        // 没有需要玩家权衡的取舍。要留痕传 'keep'。
        if (options.WipeTraces)
        {
            steps.Add(new HackStep(HackStepKind.CleanLogs, target, default, null));
        }

        if (options.Disconnect)
        {
            steps.Add(new HackStep(HackStepKind.Disconnect, target, default, "dc"));
        }

        // 断开已让 TraceTracker 自己失效；这一步是确定性的兜底 ——
        // stay 模式（不断开）下它是唯一的止血点。走 stop()，零每帧开销。
        steps.Add(new HackStep(HackStepKind.KillTrace, target, default, null));
    }

    /// <summary>为被剔除的机器追加清痕步骤（<c>WipeTraces</c> 为真时）。</summary>
    private static void AppendSkippedSteps(
        List<HackStep> steps, IReadOnlyList<Computer> skipped, HackOptions options)
    {
        // 被剔除的机器照样清痕：它们此前进过、破过、侦察过，/log 里留着痕迹，
        // 「跳过入侵」不等于「放过证据」。排在全部正常步骤之后 —— 正常流程不会再碰
        // 这些机器，此刻清是终点动作，不会有新记录再追加进来。
        // 对没有痕迹的机器是幂等的：WipeTraces 返回空列表，不产生任何输出。
        if (options.WipeTraces)
        {
            foreach (var comp in skipped)
            {
                steps.Add(new HackStep(HackStepKind.CleanLogs, comp, default, null));
            }
        }
    }

    /// <summary>为玩家自己的机器追加清痕步骤（<c>WipeTraces</c> 为真时），必须排在全部步骤之后。</summary>
    private static void AppendSelfStep(List<HackStep> steps, OS os, HackOptions options)
    {
        // 玩家自己的机器也在同一口径内 —— 它的 /log 里同样有指向自己的条目
        // （"<目标IP> Disconnected" 之类），那就是「我的痕迹」。
        //
        // 机器不在 targets 里（ResolveTargets 显式跳过 os.thisComputer，
        // HackEngine.cs:445），也不在 skipped 里 —— 上面两处都够不着，必须单独追加。
        //
        // 排在全部步骤之后是刻意的：玩家的 /log 记的是「谁连过我」，入侵过程中
        // 每连一台都会往自己机器上写一条，提前清会被后续步骤重新写回来。
        if (options.WipeTraces)
        {
            steps.Add(new HackStep(HackStepKind.CleanLogs, os.thisComputer, default, null));
        }
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
            AppendScriptedAction(steps, target, action, emitted, connected);
        }

        // 清痕排在 KillTrace 之前，且**不设开关**（与内置次序同一口径，见 BuildSteps）。
        // 排在最后是刻意的：提权与投放都会往目标 /log 追加条目，早清等于白清。
        //
        // 即便脚本已 dc 也不受影响：WipeTraces 按 folderPath 直取目标 /log
        // （HackEngine.WipeTraces），不读 os.connectedComp，也不再回显那条 rm。
        //
        // 脚本没写 rm 时补上（开关为真才补）—— 留痕的代价是目标机在断开时自动排一个
        // 脱机追踪（TrackerCompleteSequence.cs:30-47）。脚本里写了 rm 就不补：
        // 那是玩家显式指定的位置，多清一次虽幂等，但会把清理点挪到玩家没写的地方。
        if (options.WipeTraces && !emitted.Contains(HackStepKind.CleanLogs))
        {
            steps.Add(new HackStep(HackStepKind.CleanLogs, target, default, null));
        }

        // 兜底反追踪：与内置次序同理，脚本没写也要有 —— 断开已让它失效，
        // 这一步覆盖「脚本以 stay 结尾」与「最后一步之后才被点燃」的窗口。
        if (emitted.Add(HackStepKind.KillTrace))
        {
            steps.Add(new HackStep(HackStepKind.KillTrace, target, default, null));
        }
    }

    /// <summary>
    /// 追加脚本里的单个动作：<c>connect</c> 已登记则跳过、<c>openPort</c> 逐端口展开，
    /// 其余动作重复出现只取首次（<c>cleanlogs</c> 例外，它幂等且可重复）。
    /// </summary>
    private static void AppendScriptedAction(
        List<HackStep> steps, Computer target, HackScript.Action action,
        HashSet<HackStepKind> emitted, bool connected)
    {
        // connect 已被前置步骤登记过时（ConnectFirst 模式），脚本里再写就跳过。
        if (action.Kind == HackStepKind.Connect)
        {
            if (connected || !emitted.Add(HackStepKind.Connect))
            {
                return;
            }

            steps.Add(new HackStep(HackStepKind.Connect, target, default, "connect " + target.ip));
            return;
        }

        // OpenPort 可重复（逐端口展开）。CleanLogs 也可重复 —— 它对空 /log
        // 是幂等的（WipeTraces 返回空列表、不输出），而「证据必须消失」是硬承诺，
        // 让玩家写两次就多清一次比静默吞掉第二个更符合预期。
        // 其余动作改的是游戏状态，重复出现只取首次。
        if (action.Kind is not (HackStepKind.OpenPort or HackStepKind.CleanLogs) &&
            !emitted.Add(action.Kind))
        {
            return;
        }

        if (action.Kind == HackStepKind.OpenPort)
        {
            foreach (var step in ExpandPorts(target, action.Port))
            {
                steps.Add(step);
            }

            return;
        }

        steps.Add(new HackStep(action.Kind, target, default, CommandFor(action.Kind)));
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

        _ => null,
    };
}

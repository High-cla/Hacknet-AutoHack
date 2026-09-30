namespace AutoHack;

using Hacknet;
using Pathfinder.Port;

/// <summary>单个目标的战果（GUI 展示用）。</summary>
internal sealed record TargetOutcome(string Name, int Opened, int Total, bool Escalated);

/// <summary>
/// 一次入侵运行的执行状态与推进逻辑。
/// 由 OS.Update / OSUpdateEvent 每帧驱动；与绘制解耦，便于单独测试。
/// 每个动作都把对应指令回显到终端（格式同 OS.runCommand），并执行之 ——
/// 唯端口步的命令由原生演出去回显（动画挂上面板那一刻，见 HackRun.Steps.ApplyOpenPort）。
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
internal sealed partial class HackRun
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

            // 端口数已达提权门槛：剩下的端口步全部跳过，直接去解防火墙 + porthack。
            // 判据与游戏门禁同源（见 HackEngine.PortQuotaMet），破端口是顺序的，
            // 故「破到够为止」与「按门槛挑着破」等价，且自动覆盖某步没破成的情形。
            //
            // 跳过时静默（不回显）—— 没敲过的命令不该出现在终端里。
            // 也正因为跳过的步不触发循环末尾那道「端口步每帧一步」闸门，
            // 一帧内就能把该台剩余端口步全部跳完，不额外耗时。
            if (IsPortQuotaMet(step))
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

        return false;
    }

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

    /// <summary>
    /// 这一步是不是「端口已经破够了，可以不用再破」的端口步。
    ///
    /// <b>只对 <see cref="HackStepKind.OpenPort"/> 生效。</b>其余步骤都不受端口数量影响 ——
    /// 尤其是 <see cref="HackStepKind.SolveFirewall"/>：porthack 的第二道门禁要求
    /// <c>firewall.solved</c>（<c>OS.cs:1920-1927</c>），防火墙该解还得解。
    ///
    /// <b>两种模式都生效（内置次序与脚本）。</b>与 <see cref="IsRedundantAfterLogin"/> 同一
    /// 取舍：脚本里写 <c>openPort</c> 的目的是拿下这台机器，而门槛一旦越过，多破的端口对
    /// 那个目的毫无贡献。若将来要「无论门槛、破满全部」，那是一个<b>新的选项</b>，
    /// 不该靠脚本模式绕过 —— 否则同一条脚本在内置次序下是另一套行为，更难解释。
    /// </summary>
    private static bool IsPortQuotaMet(HackStep step)
        => step.Kind == HackStepKind.OpenPort && HackEngine.PortQuotaMet(step.Target);

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
        TraceTools.Run(os, announce: false);

        // 换 IP：游戏原生的「保命」动作（ISPDaemon 的 "Assign New IP"），
        // 并把全图已控机器的归属迁移到新 IP。理由与代价见 IpTools 的文档注释。
        //
        // 排在清追踪之后：TraceTools 判「旧 IP 是否在日志里」只在擦日志时才有意义 ——
        // 现在不擦日志了，但顺序仍然保持，因为语义上「先收拾追踪、再换身份」更清楚。
        if (Options.ResetIP)
        {
            IpTools.Run(os, announce: false);
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
        }

        // 目标为 0 是最容易被误读的状态：终端仍会逐台打清痕行，看着像
        // 「每台都重跑了一遍」，实际每台只抹了 log。这一条必须留 ——
        // 它是「为什么什么都没发生」的唯一解释，不是战果报告。
        if (_targets.Count == 0)
        {
            os.write("[autohack] No targets: all " + SkippedOwned
                + " reachable node(s) were already owned.");
            os.write("[autohack]   'redo' includes owned nodes; 'allnodes' sweeps the whole map.");
        }

        Current = "done - " + _targets.Count + " target(s)";
    }
}

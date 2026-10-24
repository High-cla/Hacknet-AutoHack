namespace AutoHack;

using AutoHack.Core;
using Hacknet;
using Pathfinder.Util;

/// <summary>单个目标的战果（GUI 展示用）。</summary>
internal sealed record TargetOutcome(string Name, int Opened, int Total, bool Escalated);

/// <summary>
/// 一次入侵运行的<b>执行器</b>：核心状态机给出动作，这里落到游戏 API 上。
///
/// 本类只剩三件事：构造期解析目标、把快照交给 <see cref="Planner"/>；每帧执行
/// <see cref="Session"/> 给出的动作；收尾。判据、次序、四类跳过都不在这里 ——
/// 它们在 <c>src/AutoHack/Core/</c>，并且有离线单测（<c>tests/core-tests</c>）。
///
/// 三条与游戏时序绑定的硬事实（执行层必须继续尊重，核心不感知）：
/// 1. <b>管理员反扑</b>：断开时 <c>handleDisconnection</c> 调 <c>admin.disconnectionDetected()</c>，
///    0~20 秒后关掉全部端口并把 <c>adminIP</c> 还原 —— 故断开前必须
///    <see cref="HackEngine.SuppressCounterattack"/>。
/// 2. <b>追踪</b>：<c>TraceTracker</c> 只在「连着被追踪目标」时推进，跑完一个目标就断开即等于反追踪。
/// 3. <b>清痕在断开之前</b>：断开本身会往目标 /log 写一条「玩家 IP Disconnected」。
/// 这三条都由 <see cref="Planner"/> 的次序表达，执行层只负责照做。
/// </summary>
internal sealed partial class HackRun
{
    private readonly Session _session;
    private readonly GameWorldView _world;
    private readonly Dictionary<string, Computer> _byIp = new(StringComparer.Ordinal);
    private readonly List<Computer> _targets = new();

    private string _phaseOverride;
    private string _currentIp;
    private string _current;
    private string _currentOverride;

    internal HackRun(OS os, HackOptions options)
    {
        Options = options;

        // 只有「全网扫描 + allnodes」需要揭图：ConnectableComputers 只收集、不 discoverNode，
        // 不揭就是「地图全开跑完一台都不亮」。单节点与显式点名刻意不揭（v1.39.0，用户定）：
        // 它们的池与 visibleNodes 无关，只打这一台却点亮整片连通分量是没人要的副作用。
        if (options.Scope == HackScope.Network && options.AllNodes)
        {
            HackEngine.RevealWholeMap(os);
        }

        var resolved = HackEngine.ResolveTargets(os, options);
        _targets.AddRange(resolved.Targets);
        SkippedOwned = resolved.SkippedOwned;

        Index(_targets);
        Index(resolved.Skipped);
        if (os.thisComputer != null)
        {
            _byIp[os.thisComputer.ip] = os.thisComputer;
        }

        _world = new GameWorldView(os, _byIp);

        var plan = Planner.Build(
            _world.Of(_targets, options),
            _world.Of(resolved.Skipped, options),
            _world.Of(os.thisComputer, options),
            os.connectedComp?.ip,
            options,
            LoadScript(options));

        _session = new Session(plan, options);
        _currentIp = _targets.Count > 0 ? _targets[0].ip : null;
        _current = _targets.Count > 0 ? _targets[0].name : "-";
    }

    internal HackOptions Options { get; }

    internal List<TargetOutcome> Outcomes { get; } = new();

    internal int Total => _session.Total;

    internal int Done => _session.Done;

    internal bool Finished { get; private set; }

    /// <summary>阶段文案：收尾/中止时由本地覆盖，其余时候直接读状态机（惰性构造）。</summary>
    internal string Phase => _phaseOverride ?? _session.Phase;

    /// <summary>当前目标文案 —— 只在换目标时拼一次，不随每个动作重算。</summary>
    internal string Current => _currentOverride ?? _current;

    internal IReadOnlyList<Computer> Targets => _targets;

    /// <summary>因已控（肉鸡）而跳过的机器数。</summary>
    internal int SkippedOwned { get; }

    /// <summary>
    /// 推进一帧。只允许游戏线程调用 —— 会改动游戏状态。
    /// 动作由核心状态机给出，本方法只做「执行」；执行结果不回流，世界视图下一帧自然反映。
    /// </summary>
    internal void Tick(OS os, float deltaSeconds)
    {
        if (Finished)
        {
            return;
        }

        foreach (var action in _session.Tick(_world, deltaSeconds))
        {
            Apply(os, action);
        }

        if (_session.Finished)
        {
            Finish(os);
        }
    }

    /// <summary>把动作落到游戏 API 上。目标按 IP 解析 —— 计划里只有 IP，没有对象引用。</summary>
    private void Apply(OS os, GameAction action)
    {
        var target = Resolve(action.TargetIp);
        if (!string.Equals(_currentIp, action.TargetIp, StringComparison.Ordinal))
        {
            _currentIp = action.TargetIp;
            _current = target != null ? target.name + " @ " + target.ip : action.TargetIp;
        }

        switch (action.Kind)
        {
            case ActionKind.Connect:
                ApplyConnect(os, action, target);
                break;

            case ActionKind.Neutralize:
                ApplyNeutralize(os, target);
                break;

            case ActionKind.Probe:
                ApplyProbe(os, action, target);
                break;

            case ActionKind.Login:
                ApplyLogin(os, target);
                break;

            case ActionKind.BypassProxy:
                ApplyBypassProxy(os, target);
                break;

            case ActionKind.OpenPort:
                ApplyOpenPort(os, action, target);
                break;

            case ActionKind.SolveFirewall:
                ApplySolveFirewall(os, target);
                break;

            case ActionKind.Escalate:
                ApplyEscalate(os, action, target);
                break;

            case ActionKind.BypassWhitelist:
                ApplyBypassWhitelist(os, target);
                break;

            case ActionKind.UploadMarker:
                ApplyUploadMarker(os, target);
                break;

            case ActionKind.CleanLogs:
                ApplyCleanLogs(os, target);
                break;

            case ActionKind.Disconnect:
                ApplyDisconnect(os, action, target);
                break;

            case ActionKind.KillTrace:
                ApplyKillTrace(os);
                break;

            case ActionKind.FlushAnimations:
                NativeExes.Flush(os, target, force: true);
                break;

            case ActionKind.Custom:
                ApplyCustomAction(os, action, target);
                break;
        }
    }

    /// <summary>
    /// 执行扩展登记的脚本动作。解析期已校验过名字存在，故这里查不到只可能是
    /// 登记表在运行中被清空（插件卸载）—— 报一行即可，不抛异常。
    /// </summary>
    private static void ApplyCustomAction(OS os, GameAction action, Computer target)
    {
        if (!ScriptActionRegistry.TryGet(action.Custom, out var definition))
        {
            os.write("[autohack] script action '" + action.Custom + "' is not registered.");
            return;
        }

        definition.Run(os, target);
    }

    private Computer Resolve(string ip)
    {
        if (ip == null)
        {
            return null;
        }

        return _byIp.TryGetValue(ip, out var comp) ? comp : ComputerLookup.Find(ip);
    }

    private void Index(IReadOnlyList<Computer> computers)
    {
        foreach (var comp in computers)
        {
            if (comp?.ip != null)
            {
                _byIp[comp.ip] = comp;
            }
        }
    }

    /// <summary>
    /// 读取脚本。<b>构造期</b>解析一次：语法错/文件缺失在这里抛出，由调用方转成终端可见的错误行。
    /// 放进 Tick 会让错误每帧重复，放进计划构建会让「目标为空」与「脚本坏了」两种情况混在一起。
    /// </summary>
    private static HackScript LoadScript(HackOptions options)
        => string.IsNullOrEmpty(options.Script)
            ? null
            : HackScript.Load(options.Script, ContentPaths.Resolve, ScriptActionRegistry.Names);
    /// <summary>
    /// 异常中止：执行层抛出的异常不该打断游戏帧（见 <see cref="AutoHackExecutable.CatchException"/>）。
    ///
    /// 只做止血 —— 停掉还没播完的动画、掐掉追踪与炸弹、写一行原因；<b>不做全量清痕</b>：
    /// 这一轮没跑完，清到哪台算「干净」没有客观依据，交给玩家用 <c>autohack wipe</c> 决定。
    /// </summary>
    internal void Abort(OS os, Exception error)
    {
        if (Finished)
        {
            return;
        }

        Finished = true;
        _phaseOverride = "ABORTED";
        _currentOverride = "aborted: " + error.GetType().Name;

        NativeExes.StopAll(os);
        TraceTools.Run(os, announce: false);
        HackEngine.KillForkBombs(os);

        Trace.Write("run aborted: " + error);
        os?.write("[autohack] run aborted - " + error.GetType().Name + ": " + error.Message);
    }

    private void Finish(OS os)
    {
        Finished = true;
        _phaseOverride = "COMPLETE";

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
            // opened > total 的不一致比例。PortView 自带 Cracked，无需第二次遍历。
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

        _currentOverride = "done - " + _targets.Count + " target(s)";
    }
}

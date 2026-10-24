namespace AutoHack.Core;

/// <summary>
/// 运行状态机：把 <see cref="Plan"/> 逐帧推进成 <see cref="GameAction"/>。
///
/// <b>它不知道游戏存在。</b>世界只通过 <see cref="IWorldView"/> 回答问题（连没连、拿没拿下、
/// 破了几个端口、多少动画在途），写入一律以动作形式交出去。于是「三道闸门 + 四类跳过」
/// 全部可以在裸进程里被断言 —— 这正是把这段逻辑搬出 <c>HackRun</c> 的目的。
///
/// 三道闸门（与 v1.34.0 起的口径逐条一致）：
/// ① 单帧步数预算 <see cref="MaxStepsPerFrame"/>；
/// ② 端口步每帧只推进一步（与演出开关无关，它同时管动画入队与终端回显速率）；
/// ③ 提权前的原生动画等待，上限 <see cref="NativeWaitCapSeconds"/>，超时先发一次
/// <see cref="ActionKind.FlushAnimations"/> 把战果补上。
///
/// 四类跳过：连接被拒 → 只跳依赖连接的步；本次已靠 login 提权 → 跳破端口/解防火墙/提权/解跳板；
/// 端口已到提权门槛 → 跳剩余端口步；其余一律照常执行。
/// </summary>
internal sealed class Session
{
    /// <summary>单帧最多执行多少步，防止极端规模下一帧卡死。</summary>
    internal const int MaxStepsPerFrame = 512;

    /// <summary>
    /// 等本台端口开完的上限（秒），超时就把剩下的端口直接开掉并继续。
    /// 取 180 秒：实测单台最坏 72.3 秒（167 台 / 642 可破端口全量仿真），留 2.5 倍余量。
    /// </summary>
    internal const float NativeWaitCapSeconds = 180f;

    private readonly Plan _plan;
    private readonly HackOptions _options;
    private readonly List<GameAction> _out = new();
    private readonly HashSet<string> _refused = new(StringComparer.Ordinal);
    private readonly HashSet<string> _loggedIn = new(StringComparer.Ordinal);

    private int _index;
    private float _waitSeconds;

    /// <summary>上一帧刚发出 Connect 的目标 —— 下一帧据世界回答判定是否被拒。</summary>
    private string _expectConnect;

    /// <summary>上一帧刚发出 Login 的目标 —— 下一帧据世界回答判定是否提权成功。</summary>
    private string _expectLogin;

    internal Session(Plan plan, HackOptions options)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        Phase = "IDLE";
    }

    /// <summary>全部步骤是否已处理完（跳过也计入）。</summary>
    internal bool Finished => _index >= _plan.Steps.Count;

    /// <summary>已处理步数（含跳过），供进度条用。</summary>
    internal int Done => _index;

    /// <summary>总步数。</summary>
    internal int Total => _plan.Steps.Count;

    /// <summary>当前阶段的展示文案（面板用）。</summary>
    internal string Phase { get; private set; }

    /// <summary>被目标白名单拒绝的机器（只读视图，供统计）。</summary>
    internal IReadOnlyCollection<string> Refused => _refused;

    /// <summary>本次运行中靠凭据登入的目标。</summary>
    internal IReadOnlyCollection<string> LoggedIn => _loggedIn;

    /// <summary>
    /// 撤掉「被拒」标记 —— 白名单被绕过并重连成功后调用（执行层发现的既成事实），
    /// 之后的清痕与断开步才会重新走一遍依赖连接的路径。
    /// </summary>
    internal void ClearRefused(string ip) => _refused.Remove(ip);

    /// <summary>
    /// 推进一帧，返回本帧应执行的动作（可能为空）。执行器同步跑完后，下一帧的世界视图
    /// 自然反映结果 —— 状态机不接收任何返回值，世界是唯一的信息源。
    /// </summary>
    internal IReadOnlyList<GameAction> Tick(IWorldView world, float deltaSeconds)
    {
        _out.Clear();
        if (Finished) { return _out; }

        Observe(world);

        var budget = MaxStepsPerFrame;
        while (!Finished && budget-- > 0)
        {
            var step = _plan.Steps[_index];

            if (_refused.Contains(step.TargetIp) && DependsOnConnection(step.Kind))
            {
                _index++;
                continue;
            }

            if (IsRedundantAfterLogin(step))
            {
                _index++;
                continue;
            }

            if (IsPortQuotaMet(step, world))
            {
                _index++;
                continue;
            }

            if (WaitForAnimation(step, world, deltaSeconds))
            {
                return _out;
            }

            _waitSeconds = 0f;
            Emit(step);
            _index++;

            // 三种步之后本帧结束：
            // · OpenPort —— 一帧一个端口，同时管动画入队速率与终端回显速率；
            // · Connect / Login —— 它们的结论（是否被白名单拒绝、是否真的提权成功）只有等
            //   执行器跑完、世界更新后才知道，故必须让出本帧，下一帧由 Observe 读回。
            //   在同一帧里继续推进会让 login 成功后紧跟的破端口步白跑（这正是原来的顺序）。
            if (step.Kind is ActionKind.OpenPort or ActionKind.Connect or ActionKind.Login)
            {
                return _out;
            }
        }

        return _out;
    }

    /// <summary>把上一帧动作的结果读回来：连接是否被拒、登录是否提权成功。</summary>
    private void Observe(IWorldView world)
    {
        if (_expectConnect != null)
        {
            if (!string.Equals(world.ConnectedIp, _expectConnect, StringComparison.Ordinal))
            {
                _refused.Add(_expectConnect);
            }

            _expectConnect = null;
        }

        if (_expectLogin != null)
        {
            if (world.IsOwned(_expectLogin))
            {
                _loggedIn.Add(_expectLogin);
            }

            _expectLogin = null;
        }
    }

    /// <summary>
    /// 该步骤是否依赖「已连接」。只有 Connect 自己与 Disconnect —— 其余步骤操作的是目标对象本身，
    /// 不经连接：端口表的 Cracked、giveAdmin 写的 adminIP、按 IP 逐条点名删的 /log 都直接落在目标机上。
    /// </summary>
    private static bool DependsOnConnection(ActionKind kind)
        => kind is ActionKind.Connect or ActionKind.Disconnect;

    /// <summary>本次已靠 login 提权：破端口 / 解防火墙 / 提权 / 解跳板都是无用功。</summary>
    private bool IsRedundantAfterLogin(GameAction step)
    {
        if (_loggedIn.Count == 0 || !_loggedIn.Contains(step.TargetIp)) { return false; }

        return step.Kind is ActionKind.BypassProxy
            or ActionKind.OpenPort
            or ActionKind.SolveFirewall
            or ActionKind.Escalate;
    }

    /// <summary>
    /// 端口是否已到提权门槛（与游戏 porthack 门禁的数量那一半同源）。
    /// 演出模式下把<b>在途动画</b>算作已破 —— 端口由动画的 Completed() 去开，而端口步不等动画，
    /// 不计入就会让判据恒假、整台可破端口全排上动画（v1.40.0 的缺陷，实测多破 163 个）。
    /// </summary>
    private bool IsPortQuotaMet(GameAction step, IWorldView world)
    {
        if (step.Kind != ActionKind.OpenPort) { return false; }
        if (!_plan.Targets.TryGetValue(step.TargetIp, out var target)) { return false; }

        var reserved = _options.ShowExes ? world.AnimationInFlight(step.TargetIp) : 0;
        return world.OpenPortCount(step.TargetIp) + reserved > target.PortsNeeded;
    }

    /// <summary>
    /// 提权前的等待：本台交给动画的端口还没开完就不推进，返回 true 表示本帧就此打住。
    /// 超过上限则发一次 FlushAnimations 并继续 —— 没有上限就是「整轮永久停在提权」。
    /// </summary>
    private bool WaitForAnimation(GameAction step, IWorldView world, float deltaSeconds)
    {
        if (step.Kind != ActionKind.Escalate || world.AnimationSettled(step.TargetIp)) { return false; }

        _waitSeconds += deltaSeconds;
        if (_waitSeconds < NativeWaitCapSeconds) { return true; }

        _waitSeconds = 0f;
        _out.Add(GameAction.Of(ActionKind.FlushAnimations, step.TargetIp));
        return false;
    }

    /// <summary>发出一步，并更新展示阶段与两条「下一帧核对」的标记。</summary>
    private void Emit(GameAction step)
    {
        _out.Add(step);
        Phase = PhaseFor(step);

        if (step.Kind == ActionKind.Connect) { _expectConnect = step.TargetIp; }
        if (step.Kind == ActionKind.Login) { _expectLogin = step.TargetIp; }
    }

    private string PhaseFor(GameAction step)
    {
        var name = _plan.Targets.TryGetValue(step.TargetIp, out var target) && !string.IsNullOrEmpty(target.Name)
            ? target.Name.ToUpperInvariant()
            : step.TargetIp;

        return step.Kind switch
        {
            ActionKind.Connect => "CONNECTING TO " + name,
            ActionKind.Neutralize => "DISABLING COUNTERATTACK ON " + name,
            ActionKind.Probe => "PROBING " + name,
            ActionKind.Login => "LOGGING IN",
            ActionKind.BypassProxy => "BYPASSING PROXY ON " + name,
            ActionKind.OpenPort => "CRACKING " + name,
            ActionKind.SolveFirewall => "SOLVING FIREWALL ON " + name,
            ActionKind.Escalate => "ESCALATING ON " + name,
            ActionKind.BypassWhitelist => "WHITELISTING ON " + name,
            ActionKind.UploadMarker => "UPLOADING MARKER TO " + name,
            ActionKind.CleanLogs => "WIPING LOGS ON " + name,
            ActionKind.Disconnect => "DISCONNECTING",
            ActionKind.KillTrace => "KILLING TRACE",
            ActionKind.ResetIp => "RESETTING IP",
            _ => Phase,
        };
    }
}

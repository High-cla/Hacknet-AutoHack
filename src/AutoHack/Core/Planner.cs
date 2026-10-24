namespace AutoHack.Core;

/// <summary>
/// 计划构建：目标快照 × 选项 → 动作序列。<b>纯函数</b>，不读运行状态、不碰游戏 API。
///
/// 次序沿用 v1.41 的内置流程（内置次序与脚本两条路）：
/// 连接 → 解除反扑 → 侦察 → 登录 → 解跳板 →（逐端口攻破 + 反追踪）× N
/// → 解防火墙 → 提权 → 白名单绕过 → 投放 → 清痕 → 断开 → 反追踪兜底。
/// 清痕必须在断开之前：断开本身会往目标 /log 写一条「玩家 IP Disconnected」。
/// </summary>
internal static class Planner
{
    internal static Plan Build(
        IReadOnlyList<TargetView> targets,
        IReadOnlyList<TargetView> skipped,
        TargetView self,
        string connectedIp,
        HackOptions options,
        HackScript script)
    {
        var steps = new List<GameAction>((targets.Count + skipped.Count) * 10);

        foreach (var target in targets)
        {
            if (script != null)
            {
                AppendScripted(steps, target, script, options, connectedIp);
            }
            else
            {
                AppendTarget(steps, target, options, connectedIp);
            }
        }

        AppendSkipped(steps, skipped, options);
        AppendSelf(steps, self, options);

        return new Plan(steps, Snapshot(targets, skipped, self));
    }

    private static Dictionary<string, TargetView> Snapshot(
        IReadOnlyList<TargetView> targets, IReadOnlyList<TargetView> skipped, TargetView self)
    {
        var map = new Dictionary<string, TargetView>(StringComparer.Ordinal);
        foreach (var target in targets) { map[target.Ip] = target; }
        foreach (var target in skipped) { map[target.Ip] = target; }
        if (self != null) { map[self.Ip] = self; }
        return map;
    }

    private static void AppendTarget(
        List<GameAction> steps, TargetView target, HackOptions options, string connectedIp)
    {
        AppendEngage(steps, target, options, connectedIp);
        AppendPorts(steps, target);
        AppendEscalate(steps, target);
        AppendCleanup(steps, target, options);
    }

    /// <summary>入场：连接（可选）→ 解除反扑 → 侦察 → 登录（可选）→ 解跳板（可选）。</summary>
    private static void AppendEngage(
        List<GameAction> steps, TargetView target, HackOptions options, string connectedIp)
    {
        if (options.ConnectFirst && !string.Equals(connectedIp, target.Ip, StringComparison.Ordinal))
        {
            steps.Add(GameAction.Of(ActionKind.Connect, target.Ip, HackCommands.Connect(target.Ip)));
        }

        // 独立成步而非挂在 Connect 上：direct / 已连接路径没有 Connect 步，同样需要解除反扑。
        steps.Add(GameAction.Of(ActionKind.Neutralize, target.Ip));
        steps.Add(GameAction.Of(ActionKind.Probe, target.Ip, HackCommands.Probe));

        // 凭据登录排在最前：成功即提权，后面整段破端口动作都不必跑。
        // EOS 设备的端口容量等于提权门槛，破端口是死路，固定密码才是游戏设计的正路。
        if (options.UseCredentials || target.IsEos)
        {
            steps.Add(GameAction.Of(ActionKind.Login, target.Ip));
        }

        if (target.Ports.Count > 0 && target.HasProxy && target.ProxyActive)
        {
            steps.Add(GameAction.Of(ActionKind.BypassProxy, target.Ip));
        }
    }

    /// <summary>逐端口攻破；每个端口后跟一步反追踪（把追踪状态在端口之间也归零）。</summary>
    private static void AppendPorts(List<GameAction> steps, TargetView target)
    {
        foreach (var port in target.Ports)
        {
            steps.Add(new GameAction(ActionKind.OpenPort, target.Ip, port));
            steps.Add(GameAction.Of(ActionKind.KillTrace, target.Ip));
        }
    }

    /// <summary>解防火墙 → 提权 → 白名单绕过。</summary>
    private static void AppendEscalate(List<GameAction> steps, TargetView target)
    {
        if (target.HasFirewall && !target.FirewallSolved)
        {
            steps.Add(GameAction.Of(ActionKind.SolveFirewall, target.Ip));
        }

        steps.Add(GameAction.Of(ActionKind.Escalate, target.Ip, HackCommands.Escalate));

        // 白名单机器无条件排一步绕过（与开关解耦）：它的 connect 会被拒，而破端口、提权
        // 不经连接照样拿下；拿下后把自己的 IP 追加进它的 /Whitelist/list.txt，
        // 重连即恢复会话，后续清痕与断开才走得通。
        if (target.HasWhitelist)
        {
            steps.Add(GameAction.Of(ActionKind.BypassWhitelist, target.Ip));
        }
    }

    /// <summary>收尾：投放（可选）→ 清痕（可选）→ 断开（可选）→ 反追踪兜底。</summary>
    private static void AppendCleanup(
        List<GameAction> steps, TargetView target, HackOptions options)
    {
        if (options.UploadMarker)
        {
            steps.Add(GameAction.Of(ActionKind.UploadMarker, target.Ip));
        }

        if (options.WipeTraces)
        {
            steps.Add(GameAction.Of(ActionKind.CleanLogs, target.Ip));
        }

        if (options.Disconnect)
        {
            steps.Add(GameAction.Of(ActionKind.Disconnect, target.Ip, HackCommands.Disconnect));
        }

        // 断开已让 TraceTracker 自己失效；这一步是确定性兜底 —— stay 模式下它是唯一的止血点。
        steps.Add(GameAction.Of(ActionKind.KillTrace, target.Ip));
    }

    /// <summary>被剔除的机器照样清痕：跳过入侵不等于放过证据。排在全部正常步骤之后。</summary>
    private static void AppendSkipped(
        List<GameAction> steps, IReadOnlyList<TargetView> skipped, HackOptions options)
    {
        if (!options.WipeTraces) { return; }
        foreach (var target in skipped)
        {
            steps.Add(GameAction.Of(ActionKind.CleanLogs, target.Ip));
        }
    }

    /// <summary>玩家自己的机器也要清痕（它的 /log 里同样有指向自己的条目），必须排在最后。</summary>
    private static void AppendSelf(List<GameAction> steps, TargetView self, HackOptions options)
    {
        if (options.WipeTraces && self != null)
        {
            steps.Add(GameAction.Of(ActionKind.CleanLogs, self.Ip));
        }
    }

    /// <summary>
    /// 按脚本给一个目标展开动作。前置始终补 <c>neutralize</c>（解除管理员反扑）、
    /// 未连接时补 <c>connect</c>、末尾兜底 <c>killtrace</c> —— 这三步是正确性要求，不是风格。
    /// </summary>
    private static void AppendScripted(
        List<GameAction> steps, TargetView target, HackScript script, HackOptions options, string connectedIp)
    {
        var emitted = new HashSet<ActionKind>();
        var connected = string.Equals(connectedIp, target.Ip, StringComparison.Ordinal);

        if (options.ConnectFirst && !connected)
        {
            steps.Add(GameAction.Of(ActionKind.Connect, target.Ip, HackCommands.Connect(target.Ip)));
            emitted.Add(ActionKind.Connect);
        }

        steps.Add(GameAction.Of(ActionKind.Neutralize, target.Ip));
        emitted.Add(ActionKind.Neutralize);

        foreach (var action in script.Actions)
        {
            AppendScriptedAction(steps, target, action, emitted, connected, options);
        }

        // 脚本没写 rm 时补上（开关为真才补）：留痕会让带 tracker 的机器在断开时排一个脱机追踪。
        if (options.WipeTraces && !emitted.Contains(ActionKind.CleanLogs))
        {
            steps.Add(GameAction.Of(ActionKind.CleanLogs, target.Ip));
        }

        if (emitted.Add(ActionKind.KillTrace))
        {
            steps.Add(GameAction.Of(ActionKind.KillTrace, target.Ip));
        }
    }

    /// <summary>
    /// 脚本里的单个动作：<c>connect</c> 已登记则跳过；<c>openPort</c> 逐端口展开；
    /// <c>cleanLogs</c> 幂等故可重复；其余动作只取首次。
    /// </summary>
    private static void AppendScriptedAction(
        List<GameAction> steps, TargetView target, HackScript.Action action,
        HashSet<ActionKind> emitted, bool connected, HackOptions options)
    {
        if (action.Kind == ActionKind.Connect)
        {
            if (connected || !emitted.Add(ActionKind.Connect)) { return; }
            steps.Add(GameAction.Of(ActionKind.Connect, target.Ip, HackCommands.Connect(target.Ip)));
            return;
        }

        // 扩展动作也允许重复：它的语义由扩展定义（可能是逐文件、逐端口之类），
        // 解析器不该替它决定「第二次没意义」。
        var repeatable = action.Kind is ActionKind.OpenPort or ActionKind.CleanLogs or ActionKind.Custom;
        if (!repeatable && !emitted.Add(action.Kind)) { return; }

        if (action.Kind == ActionKind.OpenPort)
        {
            foreach (var port in target.Ports)
            {
                if (action.Port != 0 && port.DisplayPort != action.Port && port.CodePort != action.Port) { continue; }
                steps.Add(new GameAction(ActionKind.OpenPort, target.Ip, port));
            }

            return;
        }

        if (action.Kind == ActionKind.Custom)
        {
            steps.Add(GameAction.CustomAction(action.Name, target.Ip));
            return;
        }

        steps.Add(GameAction.Of(action.Kind, target.Ip, EchoFor(action.Kind)));
    }

    /// <summary>脚本动作的终端回显原文；null = 该动作不是终端指令（或不回显）。</summary>
    private static string EchoFor(ActionKind kind) => kind switch
    {
        ActionKind.Probe => HackCommands.Probe,
        ActionKind.Escalate => HackCommands.Escalate,
        ActionKind.Disconnect => HackCommands.Disconnect,
        _ => null,
    };
}

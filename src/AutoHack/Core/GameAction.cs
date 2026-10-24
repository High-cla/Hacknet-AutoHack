namespace AutoHack.Core;

/// <summary>核心对世界提出的单个请求。<see cref="ActionKind"/> 是闭集，新增一类要在执行器同步加分支。</summary>
internal enum ActionKind
{
    /// <summary>建立到目标的会话（<c>connect &lt;ip&gt;</c>）。</summary>
    Connect,

    /// <summary>解除目标的延迟反扑（清 <c>admin</c>）。非终端指令，不回显。</summary>
    Neutralize,

    /// <summary>读端口表并回显原生格式报告（<c>probe</c>）。</summary>
    Probe,

    /// <summary>用已知凭据登入（成功即提权）。</summary>
    Login,

    /// <summary>解除跳板（等价于过载跑完，但不等 30 秒）。</summary>
    BypassProxy,

    /// <summary>攻破一个端口（<see cref="GameAction.Port"/> 指定哪个）。</summary>
    OpenPort,

    /// <summary>解目标防火墙（porthack 的门禁之一）。</summary>
    SolveFirewall,

    /// <summary>提权（<c>porthack</c>）。</summary>
    Escalate,

    /// <summary>把玩家 IP 追加进目标的 /Whitelist/list.txt 并重连。</summary>
    BypassWhitelist,

    /// <summary>投放标记文件。</summary>
    UploadMarker,

    /// <summary>抹掉玩家在目标 /log 里的痕迹。</summary>
    CleanLogs,

    /// <summary>断开当前连接（<c>dc</c>）。</summary>
    Disconnect,

    /// <summary>毙掉进行中的追踪（<c>TraceTracker.stop()</c>）。</summary>
    KillTrace,

    /// <summary>给本机换一个新 IP。</summary>
    ResetIp,

    /// <summary>把该目标尚未落地的原生动画直接结清（提权等待超时后的兜底）。</summary>
    FlushAnimations,
}

/// <summary>
/// 计划里的一步，也是每帧交给执行器的请求 —— 计划与执行共用同一形态。
///
/// <see cref="Echo"/> 是要先回显到终端的命令行原文（null = 不回显）。端口步的原文由执行器
/// 现算（破解程序名来自游戏数据表），故计划期留空。
/// </summary>
internal sealed record GameAction(
    ActionKind Kind, string TargetIp, PortView Port = default, string Echo = null)
{
    /// <summary>不需要端口详情的动作。</summary>
    internal static GameAction Of(ActionKind kind, string ip, string echo = null)
        => new(kind, ip, default, echo);
}

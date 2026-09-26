namespace AutoHack;

using System.Globalization;
using Hacknet;

/// <summary>目标选择策略。</summary>
internal enum HackScope
{
    /// <summary>仅当前已连接的节点。</summary>
    Connected,

    /// <summary>从玩家机与已发现节点出发、沿网络连线可达的全部服务器。</summary>
    Network,

    /// <summary>仅显式指定的目标。</summary>
    Explicit,
}

/// <summary>一次入侵的运行参数（由命令行解析）。</summary>
internal sealed record HackOptions(
    HackScope Scope,
    IReadOnlyList<string> Targets,
    float PortDelay,
    bool ClearLogs,
    bool UploadMarker,
    bool ConnectFirst,
    bool Disconnect,
    bool SkipOwned)
{
    internal const float DefaultPortDelay = 0.6f;
    internal const float MinPortDelay = 0.05f;
    internal const float MaxPortDelay = 5f;

    private static readonly string[] ConnectedAliases = ["here", "local", "current", "connected"];
    private static readonly string[] NetworkAliases = ["all", "net", "network", "scan"];
    private static readonly string[] KeepLogsAliases = ["nologs", "keep-logs", "keep"];
    private static readonly string[] NoMarkerAliases = ["nomark", "no-upload"];
    private static readonly string[] DirectAliases = ["direct", "noconnect", "no-connect"];
    private static readonly string[] StayAliases = ["stay", "keepconn", "keep-connection"];
    private static readonly string[] RedoAliases = ["redo", "force", "all-nodes"];

    internal static HackOptions Parse(IReadOnlyList<string> args)
    {
        var ids = new List<string>();
        var scope = HackScope.Network;
        var delay = DefaultPortDelay;
        var clearLogs = true;
        var uploadMarker = true;
        var connectFirst = true;
        var disconnect = true;
        var skipOwned = true;

        foreach (var raw in args ?? Array.Empty<string>())
        {
            var token = raw?.Trim();
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            var lower = token.ToLowerInvariant();

            if (ConnectedAliases.Contains(lower)) { scope = HackScope.Connected; continue; }
            if (NetworkAliases.Contains(lower)) { scope = HackScope.Network; continue; }
            if (KeepLogsAliases.Contains(lower)) { clearLogs = false; continue; }
            if (NoMarkerAliases.Contains(lower)) { uploadMarker = false; continue; }
            if (DirectAliases.Contains(lower)) { connectFirst = false; continue; }
            if (StayAliases.Contains(lower)) { disconnect = false; continue; }
            if (RedoAliases.Contains(lower)) { skipOwned = false; continue; }

            if (lower.StartsWith("delay=", StringComparison.Ordinal) &&
                float.TryParse(lower.Substring(6), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                delay = parsed < MinPortDelay ? MinPortDelay : parsed > MaxPortDelay ? MaxPortDelay : parsed;
                continue;
            }

            ids.Add(token);
        }

        if (ids.Count > 0)
        {
            scope = HackScope.Explicit;
        }

        return new HackOptions(scope, ids, delay, clearLogs, uploadMarker, connectFirst, disconnect, skipOwned);
    }
}

/// <summary>入侵计划中的单个动作；执行顺序即真人敲终端的次序。</summary>
internal enum HackStepKind
{
    /// <summary>connect &lt;ip&gt;：建立会话，后续指令才落在目标上。</summary>
    Connect,

    /// <summary>解除目标的延迟反扑（<c>Computer.admin = null</c>）。非终端指令，故不回显。</summary>
    Neutralize,

    /// <summary>probe：读取目标端口表并回显原生格式报告。</summary>
    Probe,

    /// <summary>绕过跳板：等价于过载跑完，但不等待那 30 秒。</summary>
    BypassProxy,

    /// <summary>&lt;破解程序&gt; &lt;端口&gt;：攻破一个端口。</summary>
    OpenPort,

    /// <summary>porthack：提权。</summary>
    Escalate,

    /// <summary>投放标记文件（非终端指令，故不回显）。</summary>
    UploadMarker,

    /// <summary>rm /log/&lt;文件&gt;：抹除入侵痕迹。</summary>
    CleanLogs,

    /// <summary>dc：断开连接。追踪只在连着目标时推进，断开即中止（反追踪）。</summary>
    Disconnect,
}

/// <summary>
/// 入侵计划中的单步（不可变）。
/// <see cref="Port"/> 仅 OpenPort 步有意义；<see cref="Command"/> 是要回显到终端的指令原文，可为 null。
/// </summary>
internal readonly record struct HackStep(HackStepKind Kind, Computer Target, PortInfo Port, string Command);

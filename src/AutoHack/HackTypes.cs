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

/// <summary>推进节奏档位：控制非端口步是否合并到同一帧。</summary>
internal enum HackSpeed
{
    /// <summary>每个非端口步之间等 <c>NonPortDelay</c>（0.35s），终端逐行浮现，贴近真人操作。</summary>
    Normal,

    /// <summary>非端口步压缩到 0.05s，仍分帧（保留一点节奏感）。</summary>
    Fast,

    /// <summary>非端口步合并到同一帧连续执行；只有端口破解按 <c>PortDelay</c> 等待。</summary>
    Instant,
}

/// <summary>一次入侵的运行参数（由命令行解析）。</summary>
internal sealed record HackOptions(
    HackScope Scope,
    IReadOnlyList<string> Targets,
    float PortDelay,
    bool ClearLogs,
    bool ClearOwnLogs,
    bool UploadMarker,
    bool ConnectFirst,
    bool Disconnect,
    bool SkipOwned,
    bool AllNodes,
    bool UseCredentials,
    HackSpeed Speed,
    string Script)
{
    internal const float DefaultPortDelay = 0.6f;

    /// <summary>端口间隔下限。0.02s = 50 端口/秒，比真人手速快得多但仍逐条回显。</summary>
    internal const float MinPortDelay = 0.02f;

    internal const float MaxPortDelay = 5f;

    /// <summary>Fast 档的非端口步间隔。</summary>
    internal const float FastStepDelay = 0.05f;

    private static readonly string[] ConnectedAliases = ["here", "local", "current", "connected"];
    private static readonly string[] NetworkAliases = ["all", "net", "network", "scan"];
    private static readonly string[] KeepLogsAliases = ["nologs", "keep-logs", "keep"];
    private static readonly string[] OwnLogsAliases = ["ownlogs", "my-logs", "selflogs", "clean-own"];
    private static readonly string[] NoOwnLogsAliases = ["noownlogs", "keep-own", "keep-my-logs"];
    private static readonly string[] NoMarkerAliases = ["nomark", "no-upload"];
    private static readonly string[] MarkerAliases = ["mark", "upload", "marker"];
    private static readonly string[] AllNodesAliases = ["allnodes", "all-nodes", "full", "wide"];
    private static readonly string[] DirectAliases = ["direct", "noconnect", "no-connect"];
    private static readonly string[] StayAliases = ["stay", "keepconn", "keep-connection"];
    private static readonly string[] RedoAliases = ["redo", "force"];
    private static readonly string[] CredentialAliases = ["creds", "credentials", "login", "known"];
    private static readonly string[] NoCredentialAliases = ["nocreds", "no-login", "brute"];
    private static readonly string[] SlowAliases = ["slow", "normal"];
    private static readonly string[] FastAliases = ["fast", "quick"];
    private static readonly string[] InstantAliases = ["instant", "turbo", "sameframe"];

    /// <summary>
    /// 脚本模式：用一份动作表取代内置次序。<c>script=&lt;文件名&gt;</c>，
    /// 文件按游戏的加载前缀解析（扩展目录或 Content/），详见 <see cref="HackScript"/>。
    /// </summary>
    private const string ScriptPrefix = "script=";

    internal static HackOptions Parse(IReadOnlyList<string> args)
    {
        var ids = new List<string>();
        var scope = HackScope.Network;
        var delay = DefaultPortDelay;
        var clearLogs = true;

        // 玩家自己 /log 的清理，独立于目标清痕，且缺省关：
        // 那是玩家自己的操作史，抹掉属于「玩家明确要求才做」的动作。
        var clearOwnLogs = false;
        var uploadMarker = false;
        var connectFirst = true;
        var disconnect = true;
        var skipOwned = true;
        var allNodes = false;
        // 缺省关（v1.15.0 起）：adminPass 是公开字段，开启后能登入全部机器，
        // 端口破解 / 防火墙 / 跳板三套机制实际都不会再被走到，等于架空玩法。
        // 要便利性再显式 creds。
        var useCredentials = false;
        var speed = HackSpeed.Normal;
        string script = null;

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
            if (OwnLogsAliases.Contains(lower)) { clearOwnLogs = true; continue; }
            if (NoOwnLogsAliases.Contains(lower)) { clearOwnLogs = false; continue; }
            if (MarkerAliases.Contains(lower)) { uploadMarker = true; continue; }
            if (NoMarkerAliases.Contains(lower)) { uploadMarker = false; continue; }
            if (AllNodesAliases.Contains(lower)) { allNodes = true; continue; }
            if (DirectAliases.Contains(lower)) { connectFirst = false; continue; }
            if (StayAliases.Contains(lower)) { disconnect = false; continue; }
            if (RedoAliases.Contains(lower)) { skipOwned = false; continue; }
            if (CredentialAliases.Contains(lower)) { useCredentials = true; continue; }
            if (NoCredentialAliases.Contains(lower)) { useCredentials = false; continue; }
            if (SlowAliases.Contains(lower)) { speed = HackSpeed.Normal; continue; }
            if (FastAliases.Contains(lower)) { speed = HackSpeed.Fast; continue; }
            if (InstantAliases.Contains(lower)) { speed = HackSpeed.Instant; continue; }

            if (lower.StartsWith("delay=", StringComparison.Ordinal) &&
                float.TryParse(lower.Substring(6), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                delay = parsed < MinPortDelay ? MinPortDelay : parsed > MaxPortDelay ? MaxPortDelay : parsed;
                continue;
            }

            if (lower.StartsWith(ScriptPrefix, StringComparison.Ordinal))
            {
                var name = token.Substring(ScriptPrefix.Length).Trim();
                if (name.Length > 0)
                {
                    script = name;
                }

                continue;
            }

            ids.Add(token);
        }

        if (ids.Count > 0)
        {
            scope = HackScope.Explicit;
        }

        return new HackOptions(
            scope, ids, delay, clearLogs, clearOwnLogs, uploadMarker, connectFirst, disconnect, skipOwned,
            allNodes, useCredentials, speed, script);
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

    /// <summary>login：用已知账号密码登入（<c>Computer.login</c>），成功后直接提权，跳过全部破端口步骤。</summary>
    Login,

    /// <summary>绕过跳板：等价于过载跑完，但不等待那 30 秒。</summary>
    BypassProxy,

    /// <summary>&lt;破解程序&gt; &lt;端口&gt;：攻破一个端口。</summary>
    OpenPort,

    /// <summary>analyze + solve &lt;解&gt;：解目标防火墙。porthack 门禁要求防火墙已解（OS.cs:1918-1930）。</summary>
    SolveFirewall,

    /// <summary>porthack：提权。</summary>
    Escalate,

    /// <summary>投放标记文件（非终端指令，故不回显）。</summary>
    UploadMarker,

    /// <summary>rm /log/&lt;文件&gt;：抹除入侵痕迹。</summary>
    CleanLogs,

    /// <summary>dc：断开连接。追踪只在连着目标时推进，断开即让它失效。</summary>
    Disconnect,

    /// <summary>TraceTracker.stop()：直接毙掉进行中的追踪，零每帧开销。</summary>
    KillTrace,
}

/// <summary>
/// 入侵计划中的单步（不可变）。
/// <see cref="Port"/> 仅 OpenPort 步有意义；<see cref="Command"/> 是要回显到终端的指令原文，可为 null。
/// </summary>
internal readonly record struct HackStep(HackStepKind Kind, Computer Target, PortInfo Port, string Command);

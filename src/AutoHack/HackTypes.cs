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
    bool ShowExes,
    bool ResetIP,
    HackSpeed Speed,
    string Script)
{
    // 这里曾有 WantsAntiTrace（从 Disconnect 派生的「收尾是否清追踪」）。
    // 用户定：清追踪与断开解耦 —— 清追踪是收拾自己制造的烂摊子（倒计时 + 脱机追踪 +
    // 追踪者的 /log），留着没有好处；断开是玩家的行为选择（终止会话、清空
    // navigationPath），玩家有理由不要。故清追踪改为 HackRun.Finish 里的恒定动作，
    // 不再经过开关；那个属性随之失去调用点，已删。

    internal const float DefaultPortDelay = 0.6f;

    /// <summary>端口间隔下限。0.02s = 50 端口/秒，比真人手速快得多但仍逐条回显。</summary>
    internal const float MinPortDelay = 0.02f;

    internal const float MaxPortDelay = 5f;

    /// <summary>Fast 档的非端口步间隔。</summary>
    internal const float FastStepDelay = 0.05f;

    private static readonly string[] ConnectedAliases = ["here", "local", "current", "connected"];
    private static readonly string[] NetworkAliases = ["all", "net", "network", "scan"];
    private static readonly string[] KeepLogsAliases = ["nologs", "keep-logs", "keep"];
    private static readonly string[] WipeLogsAliases = ["logs", "wipelogs", "wipe-logs"];
    private static readonly string[] OwnLogsAliases = ["ownlogs", "my-logs", "selflogs", "clean-own"];
    private static readonly string[] NoOwnLogsAliases = ["noownlogs", "keep-own", "keep-my-logs"];
    private static readonly string[] NoMarkerAliases = ["nomark", "no-upload"];
    private static readonly string[] MarkerAliases = ["mark", "upload", "marker"];
    private static readonly string[] AllNodesAliases = ["allnodes", "all-nodes", "full", "wide"];
    private static readonly string[] DirectAliases = ["direct", "noconnect", "no-connect"];
    private static readonly string[] StayAliases = ["stay", "keepconn", "keep-connection"];
    private static readonly string[] LeaveAliases = ["dc", "leave", "disconnect"];
    private static readonly string[] RedoAliases = ["redo", "force"];
    private static readonly string[] CredentialAliases = ["creds", "credentials", "login", "known"];
    private static readonly string[] NoCredentialAliases = ["nocreds", "no-login", "brute"];
    private static readonly string[] SlowAliases = ["slow", "normal"];
    private static readonly string[] FastAliases = ["fast", "quick"];
    private static readonly string[] InstantAliases = ["instant", "turbo", "sameframe"];
    private static readonly string[] ShowExesAliases = ["show", "exes", "native", "anim"];
    private static readonly string[] ResetIPAliases = ["newip", "reset-ip", "resetip"];
    private static readonly string[] NoResetIPAliases = ["nonewip", "noknewip", "keep-ip", "keepip"];
    private static readonly string[] NoShowExesAliases = ["noshow", "no-exes", "quiet"];

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

        // 缺省关（v1.16.0 起）：清痕会改写目标机的状态，属于「玩家明确要求才做」的
        // 动作。默认开会让「只想打下来看看」的玩家在不知情时抹掉对方的 /log。
        var clearLogs = false;

        // 玩家自己 /log 的清理，独立于目标清痕，同样缺省关：
        // 那是玩家自己的操作史（谁连过他、他读过什么），更不该被顺手抹掉。
        var clearOwnLogs = false;
        var uploadMarker = false;
        var connectFirst = true;

        // 缺省关（v1.16.0 起）：保持连接是更中性的默认 —— 断开会终止会话、
        // 清空 navigationPath。它只管断开；收尾清追踪已改为恒定动作，不再需要显式开启。
        var disconnect = false;
        var skipOwned = true;
        var allNodes = false;
        // 缺省关（v1.15.0 起）：adminPass 是公开字段，开启后能登入全部机器，
        // 端口破解 / 防火墙 / 跳板三套机制实际都不会再被走到，等于架空玩法。
        // 要便利性再显式 creds。
        var useCredentials = false;

        // 缺省开：原生破解程序是纯演出 —— 端口状态由 HackEngine.OpenPort 同步写好，
        // exe 只是把原版动画挂进 RAM 面板（见 NativeExes），幂等、不影响战果。
        // 缺省开是为了让脚本跑起来有可看的演出；要安静跑用 noshow。
        var showExes = true;

        // 缺省**关**（v1.32.6 起，此前为开）。换 IP 是游戏原生的「保命」动作
        // （ISP 服务器的 Assign New IP），但它会**打断任何要求 IP 不变的任务链**：
        // lelzSec 那条明写「Your IP's been whitelisted (so dont go changing it for now)」
        // （lelzSec/MessageBoardIntro.xml），白名单记的是当时的 IP，换掉即失效。
        // 缺省开时这类任务会莫名其妙进不去，而玩家很难把两件事联系起来。
        // 要换用 newip 显式开启，或直接用面板的 NEW IP 按钮换一次。
        var resetIP = false;
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
            if (WipeLogsAliases.Contains(lower)) { clearLogs = true; continue; }
            if (OwnLogsAliases.Contains(lower)) { clearOwnLogs = true; continue; }
            if (NoOwnLogsAliases.Contains(lower)) { clearOwnLogs = false; continue; }
            if (MarkerAliases.Contains(lower)) { uploadMarker = true; continue; }
            if (NoMarkerAliases.Contains(lower)) { uploadMarker = false; continue; }
            if (AllNodesAliases.Contains(lower)) { allNodes = true; continue; }
            if (DirectAliases.Contains(lower)) { connectFirst = false; continue; }
            if (StayAliases.Contains(lower)) { disconnect = false; continue; }
            if (LeaveAliases.Contains(lower)) { disconnect = true; continue; }
            if (RedoAliases.Contains(lower)) { skipOwned = false; continue; }
            if (CredentialAliases.Contains(lower)) { useCredentials = true; continue; }
            if (NoCredentialAliases.Contains(lower)) { useCredentials = false; continue; }
            if (ShowExesAliases.Contains(lower)) { showExes = true; continue; }
            if (NoShowExesAliases.Contains(lower)) { showExes = false; continue; }
            if (ResetIPAliases.Contains(lower)) { resetIP = true; continue; }
            if (NoResetIPAliases.Contains(lower)) { resetIP = false; continue; }
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
            allNodes, useCredentials, showExes, resetIP, speed, script);
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

    /// <summary>
    /// 白名单服务器被拒后的绕过：把玩家 IP 追加进目标 <c>/Whitelist/list.txt</c> 再重连。
    /// 这是游戏设计的正路（官方任务 PAE2_Whitelist.xml 的 list_add_manual.txt 明写
    /// 「append list.txt &lt;你的IP&gt;」），见 <see cref="HackEngine.BypassWhitelist"/>。
    /// </summary>
    BypassWhitelist,
}

/// <summary>
/// 入侵计划中的单步（不可变）。
/// <see cref="Port"/> 仅 OpenPort 步有意义；<see cref="Command"/> 是要回显到终端的指令原文，可为 null。
/// </summary>
internal readonly record struct HackStep(HackStepKind Kind, Computer Target, PortInfo Port, string Command);

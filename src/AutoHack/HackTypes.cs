namespace AutoHack;

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

/// <summary>
/// 命令行别名对应的语义动作。<see cref="HackOptions.Parse"/> 用一次字典查找取代原先
/// 对每个 token 依次线性扫描各别名数组；成员与 <see cref="HackOptions"/> 里那些别名数组一一对应，
/// 别名文本只写在那里（唯一真源），此处不重复列举。
/// </summary>
internal enum Opt
{
    /// <summary>仅当前已连接的节点。</summary>
    Connected,

    /// <summary>从玩家机与已发现节点出发、沿网络连线可达的全部服务器。</summary>
    Network,

    /// <summary>留着痕迹不抹。</summary>
    KeepTrace,

    /// <summary>抹掉玩家自己留下的痕迹。</summary>
    WipeTrace,

    /// <summary>投放标记文件。</summary>
    Marker,

    /// <summary>不投放标记文件。</summary>
    NoMarker,

    /// <summary>把已控机器也纳入全网扫描。</summary>
    Redo,

    /// <summary>连内网节点一起扫。</summary>
    AllNodes,

    /// <summary>不先连接，直接对着目标干活。</summary>
    Direct,

    /// <summary>结束时保持连接。</summary>
    Stay,

    /// <summary>结束时断开连接。</summary>
    Leave,

    /// <summary>用已知凭据登入。</summary>
    Credentials,

    /// <summary>不用凭据，硬破端口。</summary>
    NoCredentials,

    /// <summary>把原生破解程序挂进 RAM 面板当演出。</summary>
    ShowExes,

    /// <summary>不挂演出。</summary>
    NoShowExes,

    /// <summary>换 IP。</summary>
    ResetIP,

    /// <summary>不换 IP。</summary>
    NoResetIP,
}

/// <summary>一次入侵的运行参数（由命令行解析）。</summary>
internal sealed partial record HackOptions(
    HackScope Scope,
    IReadOnlyList<string> Targets,
    /// <summary>
    /// 抹掉<b>玩家自己留下的</b>痕迹 —— 目标机与玩家机 /log 里含玩家 IP 的条目。
    /// <b>缺省开</b>：留下痕迹会让带 <c>tracker="true"</c> 的机器在断开时自动排一个
    /// 脱机追踪（<c>TrackerCompleteSequence.cs:30-47</c>），那是「留痕 = 自杀」。
    /// 要留着痕迹传 <c>keep</c> / <c>nologs</c>。
    /// 口径与实现见 <see cref="HackEngine.WipeTraces"/>。
    /// </summary>
    bool WipeTraces,
    bool UploadMarker,
    bool ConnectFirst,
    bool Disconnect,

    /// <summary>全网扫描时跳过已拿下的机器（肉鸡），不重复入侵。缺省**开**。</summary>
    bool SkipOwned,
    bool AllNodes,
    bool UseCredentials,
    bool ShowExes,
    bool ResetIP,

    /// <summary>
    /// 允许自动入侵去开的<b>模组端口协议名</b>（大小写不敏感）。缺省空 = 一个都不开。
    /// 由命令行 <c>modports=mqtt,ntp</c> 逐协议累加，理由与取舍见 <see cref="ModPortPolicy"/>。
    /// </summary>
    IReadOnlyCollection<string> ModPorts,
    string Script)
{
    // 这里曾有 WantsAntiTrace（从 Disconnect 派生的「收尾是否清追踪」）。
    // 用户定：清追踪与断开解耦 —— 清追踪是收拾自己制造的烂摊子（倒计时 + 脱机追踪 +
    // 追踪者的 /log），留着没有好处；断开是玩家的行为选择（终止会话、清空
    // navigationPath），玩家有理由不要。故清追踪改为 HackRun.Finish 里的恒定动作，
    // 不再经过开关；那个属性随之失去调用点，已删。

    private static readonly string[] ConnectedAliases = ["here", "local", "current", "connected"];
    private static readonly string[] NetworkAliases = ["all", "net", "network", "scan"];
    /// <summary>留着痕迹不抹（见 <see cref="WipeTraces"/>）。两代反义别名都收在这里 ——
    /// 合并「清目标」与「清自己」两个开关后，旧词必须继续可用。</summary>
    private static readonly string[] KeepTraceAliases =
        ["nologs", "keep-logs", "keep", "noownlogs", "keep-own", "keep-my-logs"];

    /// <summary>抹掉自己的痕迹（缺省行为，写出来只为显式）。</summary>
    private static readonly string[] WipeTraceAliases =
        ["logs", "wipelogs", "wipe-logs", "ownlogs", "my-logs", "selflogs", "clean-own"];
    private static readonly string[] NoMarkerAliases = ["nomark", "no-upload"];
    private static readonly string[] MarkerAliases = ["mark", "upload", "marker"];
    private static readonly string[] AllNodesAliases = ["allnodes", "all-nodes", "full", "wide"];
    private static readonly string[] DirectAliases = ["direct", "noconnect", "no-connect"];
    private static readonly string[] StayAliases = ["stay", "keepconn", "keep-connection"];
    private static readonly string[] LeaveAliases = ["dc", "leave", "disconnect"];
    private static readonly string[] CredentialAliases = ["creds", "credentials", "login", "known"];
    private static readonly string[] NoCredentialAliases = ["nocreds", "no-login", "brute"];
    private static readonly string[] ShowExesAliases = ["show", "exes", "native", "anim"];
    private static readonly string[] ResetIPAliases = ["newip", "reset-ip", "resetip"];
    private static readonly string[] NoResetIPAliases = ["nonewip", "noknewip", "keep-ip", "keepip"];
    private static readonly string[] NoShowExesAliases = ["noshow", "no-exes", "quiet"];

    /// <summary>把已控机器也纳入全网扫描（见 <see cref="HackOptions.SkipOwned"/>）。</summary>
    private static readonly string[] RedoAliases = ["redo", "force"];

    /// <summary>
    /// 别名 → 动作 的一次性查找表。键为小写别名，由上面各别名数组程序化构建 ——
    /// 数组仍是唯一真源，新增别名只需改数组。
    ///
    /// <b>重复别名保留首条</b>（见 <see cref="AddAliases"/>）：这正是旧实现的行为 ——
    /// 原先是一串 <c>if (XxxAliases.Contains(...)) { ...; continue; }</c>，靠前的数组先命中即胜出。
    /// 不用 <c>Dictionary.Add</c>：它抛出的 <see cref="ArgumentException"/> 发生在<b>静态构造</b>里，
    /// 会把整个类型变成永久不可用（<c>TypeInitializationException</c>），而调用链
    /// <c>AutoHackPlugin.AutoHackCommand</c> → <c>HackOptions.Parse</c> 一路无人接，
    /// 最终被 <c>OS.execute</c> 的 <c>catch (Exception)</c>（OS.cs:1833-1837）静默吞掉 ——
    /// 表现为「autohack run 什么都不干且终端零输出」，比重复别名本身糟得多。
    /// </summary>
    private static readonly Dictionary<string, Opt> AliasMap = BuildAliasMap();

    private static Dictionary<string, Opt> BuildAliasMap()
    {
        var map = new Dictionary<string, Opt>(StringComparer.Ordinal);
        AddAliases(map, ConnectedAliases, Opt.Connected);
        AddAliases(map, NetworkAliases, Opt.Network);
        AddAliases(map, KeepTraceAliases, Opt.KeepTrace);
        AddAliases(map, WipeTraceAliases, Opt.WipeTrace);
        AddAliases(map, MarkerAliases, Opt.Marker);
        AddAliases(map, NoMarkerAliases, Opt.NoMarker);
        AddAliases(map, RedoAliases, Opt.Redo);
        AddAliases(map, AllNodesAliases, Opt.AllNodes);
        AddAliases(map, DirectAliases, Opt.Direct);
        AddAliases(map, StayAliases, Opt.Stay);
        AddAliases(map, LeaveAliases, Opt.Leave);
        AddAliases(map, CredentialAliases, Opt.Credentials);
        AddAliases(map, NoCredentialAliases, Opt.NoCredentials);
        AddAliases(map, ShowExesAliases, Opt.ShowExes);
        AddAliases(map, NoShowExesAliases, Opt.NoShowExes);
        AddAliases(map, ResetIPAliases, Opt.ResetIP);
        AddAliases(map, NoResetIPAliases, Opt.NoResetIP);
        return map;
    }

    /// <summary>
    /// 把一个别名数组整体登记进 <see cref="AliasMap"/>。重复别名保留首条 ——
    /// 与旧实现的「靠前数组先命中即胜出」逐字等价，且不会把静态构造变成异常源（见 <see cref="AliasMap"/>）。
    /// </summary>
    private static void AddAliases(Dictionary<string, Opt> map, string[] aliases, Opt opt)
    {
        foreach (var alias in aliases)
        {
            if (!map.ContainsKey(alias))
            {
                map[alias] = opt;
            }
        }
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
/// 目标上的一个端口（统一视图，全部取自 Pathfinder 端口表）。
/// <c>CodePort</c> 是游戏内部端口号（<see cref="PortRecord.OriginalPortNumber"/>），
/// <c>DisplayPort</c> 是玩家在终端看到的端口号（<see cref="PortState.PortNumber"/>）。
/// </summary>
internal readonly record struct PortInfo(string Protocol, int CodePort, int DisplayPort, string DisplayName, bool Cracked);

/// <summary>
/// 入侵计划中的单步（不可变）。
/// <see cref="Port"/> 仅 OpenPort 步有意义；<see cref="Command"/> 是要回显到终端的指令原文，可为 null。
/// </summary>
internal readonly record struct HackStep(HackStepKind Kind, Computer Target, PortInfo Port, string Command);

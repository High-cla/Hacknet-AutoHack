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
internal sealed record HackOptions(
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

    /// <summary>
    /// 脚本模式：用一份动作表取代内置次序。<c>script=&lt;文件名&gt;</c>，
    /// 文件按游戏的加载前缀解析（扩展目录或 Content/），详见 <see cref="HackScript"/>。
    /// </summary>
    private const string ScriptPrefix = "script=";

    /// <summary>
    /// v1.34.0 随「移除全部步进间隔」删掉的命令行 token。
    ///
    /// <b>为什么必须点名拦下。</b>它们已不是别名，也不再被 <see cref="Parse"/> 特判，
    /// 于是会静默掉进末尾的「显式目标」分支（<c>ids.Add(token)</c>）——
    /// 玩家敲 <c>autohack run instant</c> 得到的是「No eligible targets」，
    /// 只会以为目标名写错，而真实原因是那个开关没了。报出来比默默收下一个
    /// 永远匹配不到的目标好。
    ///
    /// 只收「玩家可能真敲过」的那些；别名表里的其余词（<c>here</c> / <c>dc</c> …）
    /// 仍在 <see cref="AliasMap"/> 里，走不到这里。
    /// </summary>
    private static readonly HashSet<string> RetiredTokens = new(StringComparer.Ordinal)
    {
        "slow", "normal", "fast", "quick", "instant", "turbo", "sameframe",
    };

    /// <summary>
    /// 找出参数里第一个已被删除的 token；没有则返回 <c>null</c>。
    /// 判据与 <see cref="Parse"/> 同一套（小写化 + 前缀），避免两处口径漂移。
    /// </summary>
    internal static string RetiredTokenIn(IReadOnlyList<string> args)
    {
        foreach (var raw in args ?? Array.Empty<string>())
        {
            var token = raw?.Trim();
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            var lower = token.ToLowerInvariant();
            if (RetiredTokens.Contains(lower) || lower.StartsWith("delay=", StringComparison.Ordinal))
            {
                return token;
            }
        }

        return null;
    }

    internal static HackOptions Parse(IReadOnlyList<string> args)
    {
        var ids = new List<string>();
        var scope = HackScope.Network;
        // 缺省**开**（v1.33.2 起，此前为关）。
        //
        // 口径变了：不再是「清空对方的 /log」（那会改写目标机的操作史），
        // 而是只删 /log 里含玩家 IP 的条目 —— 抹的是自己的痕迹，不是对方的历史。
        // 口径一变，缺省跟着翻：旧实现默认关是对的（清空整目录属越权），
        // 新实现默认关则是错的 —— 留下的 FileCopied/FileDeleted 条目会让
        // 带 tracker="true" 的机器在断开时自动排一个脱机追踪
        // （TrackerCompleteSequence.cs:30-47），那是「留痕 = 自杀」。
        var wipeTraces = true;
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

        // 缺省开：把原生破解程序挂进 RAM 面板当演出。
        //
        // v1.33.3 起端口改由这些程序自己在 Completed() 里开（见 NativeExes.Show 的文档
        // 注释）—— 看到动画跑完就等于那个端口真的开了。没有对应动画的端口
        // （实测 73/642）由调用方立即开，故关掉演出不影响战果，只是没有动画可看。
        // 缺省开是为了让脚本跑起来有可看的演出；要安静跑用 noshow。
        var showExes = true;

        // 缺省**关**（v1.32.6 起，此前为开）。换 IP 是游戏原生的「保命」动作
        // （ISP 服务器的 Assign New IP），但它会**打断任何要求 IP 不变的任务链**：
        // lelzSec 那条明写「Your IP's been whitelisted (so dont go changing it for now)」
        // （lelzSec/MessageBoardIntro.xml），白名单记的是当时的 IP，换掉即失效。
        // 缺省开时这类任务会莫名其妙进不去，而玩家很难把两件事联系起来。
        // 要换用 newip 显式开启，或直接用面板的 NEW IP 按钮换一次。
        var resetIP = false;

        // 模组端口白名单：缺省空（完全 opt-in）。命令行可给多次 modports=，逐协议累加。
        // 不做成静态累积表 —— 那会让上一轮的协议漏到下一轮，见 ModPortPolicy 的类注释。
        var modPorts = new List<string>();

        string script = null;

        foreach (var raw in args ?? Array.Empty<string>())
        {
            var token = raw?.Trim();
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            var lower = token.ToLowerInvariant();

            // 一次查找定动作；别名全部不中才轮到 script= / ids（判定顺序与旧实现一致）。
            if (AliasMap.TryGetValue(lower, out var opt))
            {
                switch (opt)
                {
                    case Opt.Connected: scope = HackScope.Connected; break;
                    case Opt.Network: scope = HackScope.Network; break;
                    case Opt.KeepTrace: wipeTraces = false; break;
                    case Opt.WipeTrace: wipeTraces = true; break;
                    case Opt.Marker: uploadMarker = true; break;
                    case Opt.NoMarker: uploadMarker = false; break;
                    case Opt.Redo: skipOwned = false; break;
                    case Opt.AllNodes: allNodes = true; break;
                    case Opt.Direct: connectFirst = false; break;
                    case Opt.Stay: disconnect = false; break;
                    case Opt.Leave: disconnect = true; break;
                    case Opt.Credentials: useCredentials = true; break;
                    case Opt.NoCredentials: useCredentials = false; break;
                    case Opt.ShowExes: showExes = true; break;
                    case Opt.NoShowExes: showExes = false; break;
                    case Opt.ResetIP: resetIP = true; break;
                    case Opt.NoResetIP: resetIP = false; break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(opt), opt, "别名动作未在 Parse 中分派");
                }

                continue;
            }

            if (ModPortPolicy.Matches(lower))
            {
                modPorts.AddRange(ModPortPolicy.Parse(token));
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
            scope, ids, wipeTraces, uploadMarker, connectFirst, disconnect, skipOwned,
            allNodes, useCredentials, showExes, resetIP, modPorts, script);
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

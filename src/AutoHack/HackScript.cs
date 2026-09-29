namespace AutoHack;

using System.Globalization;
using System.IO;
using Hacknet;
using Pathfinder.Util;

/// <summary>
/// 入侵脚本：一份动作表，决定每个目标按什么次序被打。
///
/// 格式沿用游戏自己的 HackerScript（<c>Content/HackerScripts/*.txt</c>）的行式写法：
/// 每行一个动作，行尾可带 <c>$#%#$</c> 分隔符（游戏要求，此处兼容并可选），
/// <c>#</c> 开头为注释。
/// （游戏本身没有注释语法 —— 官方文档明说「没有对应命令的行就不执行」，
/// 这里补一个显式注释，是唯一比游戏宽松的地方）。
///
/// 动作集是 AutoHack 自己的，<b>不能直接跑游戏的脚本</b>：游戏那 28 个动作里
/// 没有提权，唯一像「接管」的 <c>systakeover</c> 走 HostileHackerBreakinSequence，
/// 会往真实磁盘写 VMBootloaderTrap.dll / OpenCMD.bat（HostileHackerBreakinSequence.cs:15-21），
/// 是剧情级破坏序列，绝不可用于自动入侵。同样，游戏的 <c>connect</c> 是
/// <c>parseInputMessage("cConnection …")</c> → 目标机视角记「有人连进来」，
/// 根本不设 <c>os.connectedComp</c>（HackerScriptExecuter.cs:121），驱动不了玩家终端。
/// </summary>
internal sealed class HackScript
{
    /// <summary>脚本里的单个动作。<see cref="Port"/> 仅 <c>OpenPort</c> 有意义，0 表示全部可破端口。</summary>
    internal readonly record struct Action(HackStepKind Kind, int Port);

    /// <summary>行尾分隔符。游戏用 " $#%#$\r\n" 切行，此处按行处理后只剩尾巴。</summary>
    private const string Delimiter = "$#%#$";

    /// <summary>脚本默认所在目录（相对游戏的 Content 前缀）。</summary>
    internal const string DefaultDirectory = "HackerScripts";

    private HackScript(List<Action> actions, string source)
    {
        Actions = actions;
        Source = source;
    }

    internal IReadOnlyList<Action> Actions { get; }

    /// <summary>实际读到的文件路径，供终端回显（让玩家确认究竟跑了哪份脚本）。</summary>
    internal string Source { get; }

    /// <summary>
    /// 游戏 HackerScript 有、而 AutoHack 没有对应物的动作
    /// （HackerScriptExecuter.cs 的 28 个 case 减去两边共有的那些）。
    /// 单独列出来只为把错误说清楚 —— 它们要么是 NPC 视角的破坏动作
    /// （forkbomb/flash/trackseq/openCDTray），要么改的是目标 UI
    /// （hide* / show* / clearTerminal），要么对玩家终端无意义
    /// （config/delay 是节奏设置，本插件已不再有步进间隔，见 HackRun 类注释；
    /// systakeover 是写真实磁盘的剧情序列，绝不可用）。
    /// </summary>
    private static readonly HashSet<string> UnsupportedVerbs = new(StringComparer.Ordinal)
    {
        "flash", "trackseq", "instanttrace", "forkbomb", "reboot", "systakeover",
        "clearterminal", "hidenetmap", "hideram", "hidedisplay", "hideterminal",
        "shownetmap", "showram", "showdisplay", "showterminal",
        "stopmusic", "startmusic", "opencdtray", "closecdtray", "setadminpass",
        "makefile", "delete", "write", "writel", "writel_silent", "write_silent",
    };

    /// <summary>动作名 → 步骤种类。大小写不敏感；一个概念允许多种写法，与命令行别名同风格。</summary>
    private static readonly (string[] Names, HackStepKind Kind)[] Vocabulary =
    [
        (["connect", "c"], HackStepKind.Connect),
        (["neutralize", "anticounter", "noadmin"], HackStepKind.Neutralize),
        (["probe", "p"], HackStepKind.Probe),
        (["login", "creds"], HackStepKind.Login),
        (["proxy", "bypass", "overload"], HackStepKind.BypassProxy),
        (["openport", "crack", "port"], HackStepKind.OpenPort),
        (["solve", "firewall", "analyze"], HackStepKind.SolveFirewall),
        (["porthack", "escalate", "admin"], HackStepKind.Escalate),
        (["mark", "upload", "marker"], HackStepKind.UploadMarker),
        (["rm", "clean", "wipe", "logs"], HackStepKind.CleanLogs),
        (["dc", "disconnect"], HackStepKind.Disconnect),
        (["killtrace", "stoptrace", "trace"], HackStepKind.KillTrace),
    ];

    /// <summary>
    /// 读取并解析一份脚本。失败抛异常（<see cref="FormatException"/> 语法错、
    /// <see cref="IOException"/> 读不到）—— 由调用方转成终端可见的错误，
    /// 拒绝「脚本没生效但看起来跑了」这种半成品。
    /// </summary>
    internal static HackScript Load(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new FormatException("script= needs a file name.");
        }

        var path = ResolvePath(name.Trim());
        if (path == null)
        {
            throw new FileNotFoundException(
                "No such script: '" + name + "'. Tried: " + string.Join(", ", Candidates(name.Trim())));
        }

        return Parse(File.ReadAllText(path), path);
    }

    /// <summary>按行的文本解析成脚本。公开给测试与内部复用。</summary>
    internal static HackScript Parse(string text, string source)
    {
        var actions = new List<Action>();
        var lineNumber = 0;

        foreach (var rawLine in (text ?? string.Empty).Split('\n'))
        {
            lineNumber++;
            var line = StripDelimiter(rawLine);

            // 空行与注释。游戏没有注释语法，这里补一个 —— 脚本要能手写。
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var tokens = line.Split(Utils.spaceDelim, StringSplitOptions.RemoveEmptyEntries);
            var verb = tokens[0].ToLowerInvariant();

            var resolved = ResolveKind(verb);
            if (resolved == null)
            {
                // 区分两种「不认识」：喂了游戏的 NPC 脚本 vs 单纯拼错。
                // 前者要解释为什么不能跑，否则玩家只会看到「未知动作」而困惑
                // —— 他那份脚本在游戏里明明是有效的。
                throw UnsupportedVerbs.Contains(verb)
                    ? new FormatException(
                        "Line " + lineNumber + ": '" + tokens[0] + "' is a game NPC action "
                        + "(HackerScript) with no player-side equivalent - it cannot be used here. "
                        + "AutoHack actions: " + string.Join(", ", Vocabulary.SelectMany(v => v.Names.Take(1))) + ".")
                    : new FormatException("Line " + lineNumber + ": unknown action '" + tokens[0] + "'.");
            }

            var kind = resolved.Value;

            // openPort 可带一个端口号；不带 = 该目标上全部有破解程序的端口。
            var port = 0;
            if (kind == HackStepKind.OpenPort && tokens.Length > 1 &&
                !int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out port))
            {
                throw new FormatException(
                    "Line " + lineNumber + ": 'openPort' takes a port number, got '" + tokens[1] + "'.");
            }

            actions.Add(new Action(kind, port));
        }

        if (actions.Count == 0)
        {
            throw new FormatException("Script has no actions.");
        }

        Validate(actions, source);

        return new HackScript(actions, source);
    }

    /// <summary>
    /// 解析期强制安全不变量 —— 违反即拒绝整份脚本，而不是跑一个「看起来能跑」的序列。
    ///
    /// 清痕必须在断开之前：<c>rm</c> 的作用域取自当前连接
    /// （<c>Programs.rm</c>，Programs.cs:956 读 <c>os.connectedComp</c>），
    /// 断开之后再清，删的是玩家自己的文件系统 —— 这正是玩家手敲时
    /// 「命令敲对了却没有效果」的根因。写成 <c>dc</c> 后跟 <c>rm</c> 必然无效，
    /// 与其静默失败不如当场拒绝。
    /// </summary>
    private static void Validate(List<Action> actions, string source)
    {
        // 只看每个 rm 之前是否有「尚未被 connect 抵消」的 dc。
        // 不用「最后一个 dc」这种粗判 —— `dc / connect / rm` 是合法次序
        // （rm 作用于新建立的连接），粗判会把它误拒。
        var connected = true;
        foreach (var action in actions)
        {
            // 用 switch 表达式而非语句：每个分支都只是「这一步之后是否还连着」，
            // 正好是一个值。语句形态要写三处 break、且赋值散在分支体里。
            //
            // `_ => connected` 读的是**赋值前**的值（右侧先求值再赋给左侧），
            // 故等价于「其余动作不改连接状态」。
            connected = action.Kind switch
            {
                HackStepKind.Connect => true,
                HackStepKind.Disconnect => false,

                // 清痕本身已不依赖连接（HackEngine.WipeTraces 按 folderPath 直取目标
                // /log），但脚本里的 'rm' 仍是玩家写下的**意图** —— 他以为它在目标机上
                // 执行。dc 之后它落在自己机器上，意图与效果不符，仍属该拒的写法。
                HackStepKind.CleanLogs when !connected => throw new FormatException(
                    "In '" + source + "': 'rm' comes after 'dc' with no 'connect' in between - "
                    + "it would act on your own file system, not the target's. Swap the two lines."),

                _ => connected,
            };
        }
    }

    private static HackStepKind? ResolveKind(string verb)
    {
        foreach (var (names, kind) in Vocabulary)
        {
            if (names.Contains(verb))
            {
                return kind;
            }
        }

        return null;
    }

    private static string StripDelimiter(string line)
    {
        line = line.Trim();
        if (line.EndsWith(Delimiter, StringComparison.Ordinal))
        {
            line = line.Substring(0, line.Length - Delimiter.Length).Trim();
        }

        return line;
    }

    /// <summary>
    /// 路径解析：先当原样路径试（绝对路径或相对工作目录），再走 <see cref="Candidates"/>
    /// 按内容目录次序找 —— 这样放在 <c>Content/HackerScripts/</c> 的脚本能直接按名字引用。
    /// </summary>
    private static string ResolvePath(string name)
    {
        if (File.Exists(name))
        {
            return name;
        }

        foreach (var candidate in Candidates(name))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// 按次序给出候选路径：先在默认目录（<c>Content/HackerScripts/</c>）里找，
    /// 再退到内容根目录直接按名字找。每条各试「带 <c>.txt</c> / 不带」两个变体
    /// —— 游戏自带脚本都带后缀，但玩家未必写。
    ///
    /// <b>前缀不自己拼</b>：「内容目录在哪」是框架的知识（扩展模式走扩展目录，
    /// 否则补 <c>"Content/"</c>），游戏本体（<c>Utils.GetFileLoadPrefix</c>，
    /// Utils.cs:1386-1393）与 Pathfinder（<see cref="StringExtensions.ContentFilePath"/>，
    /// StringExtensions.cs:19-36）已各实现过一次，本仓库不写第三份。
    ///
    /// <b>本地化也不用自己调</b>：<see cref="LocalizationFix"/> 已把
    /// <c>GetLocalizedFilepath</c> 挂在 <c>ContentFilePath</c> 出口（Postfix），
    /// 于是这里每一次调用都自动命中译文，再套一层是重复。
    ///
    /// <b>与旧写法的一处行为差异</b>：旧实现只给默认目录那组查本地化，根目录回退那组
    /// 直接取原文。改走 <c>ContentFilePath</c> 后两组都会查（非 en-us 且译文存在即命中）。
    /// 统一是有意的 —— 同一件事在两条分支上给出不同答案看着是偶然，而非设计。
    /// en-us 下无任何差异（<see cref="LocalizationFix"/> 的守卫直接放行）。
    /// </summary>
    private static IEnumerable<string> Candidates(string name)
    {
        foreach (var variant in WithTxtSuffix(DefaultDirectory + "/" + name))
        {
            yield return variant.ContentFilePath();
        }

        foreach (var variant in WithTxtSuffix(name))
        {
            yield return variant.ContentFilePath();
        }
    }

    /// <summary>带 <c>.txt</c> 与不带各一次；已带后缀则只给一个，不叠成 <c>.txt.txt</c>。</summary>
    private static IEnumerable<string> WithTxtSuffix(string name)
    {
        yield return name;
        if (!name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            yield return name + ".txt";
        }
    }
}

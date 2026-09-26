namespace AutoHack;

using System.Globalization;
using System.IO;
using Hacknet;

/// <summary>
/// 入侵脚本：一份动作表，决定每个目标按什么次序被打。
///
/// 格式沿用游戏自己的 HackerScript（<c>Content/HackerScripts/*.txt</c>）的行式写法：
/// 每行一个动作，行尾可带 <c>$#%#$</c> 分隔符（游戏要求，此处兼容并可选），
/// <c>delay &lt;秒&gt;</c> 或 <c>config</c> 行设定每步间隔，<c>#</c> 开头为注释
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

    private HackScript(List<Action> actions, float? stepDelay, string source)
    {
        Actions = actions;
        StepDelay = stepDelay;
        Source = source;
    }

    internal IReadOnlyList<Action> Actions { get; }

    /// <summary>脚本自行指定的每步间隔（<c>delay</c> 行）；null = 沿用 speed 档位。</summary>
    internal float? StepDelay { get; }

    /// <summary>实际读到的文件路径，供终端回显（让玩家确认究竟跑了哪份脚本）。</summary>
    internal string Source { get; }

    /// <summary>
    /// 游戏 HackerScript 有、而 AutoHack 没有对应物的动作
    /// （HackerScriptExecuter.cs 的 28 个 case 减去两边共有的那些）。
    /// 单独列出来只为把错误说清楚 —— 它们要么是 NPC 视角的破坏动作
    /// （forkbomb/flash/trackseq/openCDTray），要么改的是目标 UI
    /// （hide* / show* / clearTerminal），要么对玩家终端无意义
    /// （config/delay 已单独处理，systakeover 是写真实磁盘的剧情序列，绝不可用）。
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
        float? stepDelay = null;
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

            // delay/config 不是动作，是整份脚本的节奏设置。游戏脚本里 config 还负责
            // 指定目标与源机，此处目标由 scope 解析（here/network/allnodes/显式），
            // 二者正交 —— 脚本只描述「怎么打」，不描述「打谁」。
            if (verb == "delay")
            {
                if (tokens.Length > 1)
                {
                    stepDelay = ParseSeconds(tokens[1], lineNumber);
                }

                continue;
            }

            // config 行是游戏脚本的「指定目标 + 源机 + 每行延迟」（官方格式
            // config [目标] [源机] [延迟]）。目标与源机对 AutoHack 无意义 ——
            // 目标由 scope 解析，源机恒是玩家自己 —— 但延迟同样适用，
            // 故只取第 4 个参数，其余宽容忽略。
            // 注意：这只是兼容写法，游戏自带的脚本并不能直接跑 —— 见 UnsupportedVerbs。
            if (verb == "config")
            {
                if (tokens.Length > 3)
                {
                    stepDelay = ParseSeconds(tokens[3], lineNumber);
                }

                continue;
            }

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

        return new HackScript(actions, stepDelay, source);
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
            switch (action.Kind)
            {
                case HackStepKind.Connect:
                    connected = true;
                    break;

                case HackStepKind.Disconnect:
                    connected = false;
                    break;

                case HackStepKind.CleanLogs when !connected:
                    throw new FormatException(
                        "In '" + source + "': 'rm' comes after 'dc' with no 'connect' in between - "
                        + "rm would act on your own file system, not the target's. Swap the two lines.");
            }
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

    private static float ParseSeconds(string token, int lineNumber)
    {
        if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            throw new FormatException(
                "Line " + lineNumber + ": expected a number of seconds, got '" + token + "'.");
        }

        return seconds < 0f ? 0f : seconds;
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
    /// 路径解析：先当原样路径试（绝对路径或相对工作目录），再按游戏的加载前缀找
    /// —— <c>Utils.GetFileLoadPrefix()</c> 在扩展模式下返回扩展目录、否则 "Content/"，
    /// 这样放在 <c>Content/HackerScripts/</c> 的脚本能直接按名字引用。
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

    private static IEnumerable<string> Candidates(string name)
    {
        var prefixed = Utils.GetFileLoadPrefix() + DefaultDirectory + "/" + name;

        // 本地化版本优先（游戏自己的脚本就是这么放的），其次是原文件。
        yield return LocalizedFileLoader.GetLocalizedFilepath(prefixed);

        // 带 .txt 与不带 .txt 各试一遍 —— 游戏自带脚本都带后缀，但玩家未必写。
        if (!name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            yield return LocalizedFileLoader.GetLocalizedFilepath(prefixed + ".txt");
        }

        yield return Utils.GetFileLoadPrefix() + name;

        if (!name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            yield return Utils.GetFileLoadPrefix() + name + ".txt";
        }
    }
}

namespace AutoHack;

using BepInEx;
using Hacknet;
using Pathfinder.Meta.Load;

/// <summary>
/// AutoHack — 全自动入侵。
/// 命令与扩展点均通过 Pathfinder 的属性自动扫描注册（AttributeManager 挂载于
/// HacknetChainloader.LoadPlugin），无需手动调用 Register* API。
/// </summary>
[BepInPlugin(Guid, "AutoHack", "1.36.0")]
// Pathfinder 的属性扫描是 IL hook，在 PathfinderAPIPlugin.Load() 里才安装；
// 缺此依赖本插件会先加载，扫描覆盖不到，命令静默失效。
[BepInDependency("com.Pathfinder.API")]
public sealed class AutoHackPlugin : BepInEx.Hacknet.HacknetPlugin
{
    internal const string Guid = "com.highcla.autohack";

    public override bool Load()
    {
        // 开机文字加速（BootBoost.Apply）**不在这里** —— 它已由 BootBoost 上的
        // [ModuleInitializer] 承担，那个入口在模块首次被访问时自动跑，比本方法更早，
        // 且不再依赖「谁记得把它写在第一行」。详见 BootBoost.Init 的说明。

        // 面板设置的落盘通道：用插件自己的 cfg（BepInEx/config/<GUID>.cfg）。
        // 必须在任何 OS 构造之前 —— 面板首次 Open 就要读它。
        PanelSettings.Bind(Config, Log);

        // 诊断追踪通道的日志源。未定义 AUTOHACK_TRACE 时，下面所有 Trace.Write
        // 的调用点会被编译器整条删除；Bind 本身无条件执行（一行赋值，不值得条件化）。
        Trace.Bind(Log);

        // 面板/HUD 靠 patch OS.Draw / OS.Update 叠加到游戏画面上。
        HarmonyInstance.PatchAll(typeof(AutoHackPlugin).Assembly);
        Log.LogInfo("AutoHack loaded (GUI).");
        return true;
    }

    /// <summary>所有插件的 Load() 均已执行完毕；此处自检命令是否真的被扫描注册。</summary>
    public override void PostLoad()
    {
        // ProgramList.programs 是游戏自身的自动补全注册表，由 CommandManager 在
        // 注册自定义命令时填充；命中即证明属性扫描链路完整。
        var registered = ProgramList.programs != null && ProgramList.programs.Contains("autohack");
        Log.LogInfo(registered
            ? "self-check OK: 'autohack' is registered and autocompletes."
            : "self-check FAILED: 'autohack' was not registered.");
    }

    public override bool Unload() => true;

    /// <summary>
    /// autohack                                                      - 开关控制面板
    /// autohack run [all|here] [目标...] [keep] [dc] - 不开面板，直接执行
    ///   （强行提权已常驻：porthack 门禁过不了时直接给目标写 adminIP）
    /// </summary>
    [Command("autohack", addAutocomplete: true, caseSensitive: false)]
    public static void AutoHackCommand(OS os, string[] args)
    {
        if (os == null)
        {
            return;
        }

        // 动词在 args[1]，不在 args[0]。
        //
        // 取证（两处独立，且已装在游戏里的 PathfinderAPI.dll 同此）：
        //   · CommandManager.OnCommandExecute（CommandManager.cs:42）拿**注册名**
        //     "autohack" 去比 args.Args[0]，命中才算这条命令 —— 首参数是命令名本身。
        //   · 游戏侧 os.display.command = args[0]（Programs.cs:269），目标取 args[1]
        //     （Programs.cs:278-281）。整个游戏把 args[0] 当命令名用。
        // 故 autohack 命令收到的 args 是 {"autohack", <动词>, ...}。
        //
        // 此前每一处都按 args[0] 判断动词，于是 args[0] 恒为 "autohack"：
        // 既不是已知工具、也不等于 "run"，**一律掉进「开关面板」分支**。
        // 表现为「autohack run 什么都不干，只会开关面板」。
        var verb = args is { Length: > 1 } ? args[1] : null;

        // 裸 `autohack`（无动词）仍按原设计开关面板；只有显式 help 才打帮助。
        if (verb is "-h" or "--help" or "help")
        {
            WriteHelp(os);
            return;
        }

        // 四个工具与 run 平级，各自独立执行；allnodes 只对 dec / mem 有意义。
        if (ToolDispatch.Handles(verb))
        {
            RunTool(os, verb, args);
            return;
        }

        // 裸 `autohack`（verb 为 null）与任何未知动词都归这里：开关面板。
        // 这也是 v1.32.0 之前**所有**命令行的归宿 —— 当时动词读的是 args[0]（恒为
        // "autohack"），四个分支没一个能命中，于是 run 与全部工具都变成了开面板。
        if (verb == null || !verb.Equals("run", StringComparison.OrdinalIgnoreCase))
        {
            TogglePanel(os);
            return;
        }

        RunHeadless(os, args);
    }

    /// <summary>打印 autohack 的完整帮助文本（选项说明与脚本动作说明）。</summary>
    private static void WriteHelp(OS os)
    {
        // 帮助文本用原始字符串字面量写：逐行 os.write 时每行都要转义引号、手工对齐，
        // 改一个字就得数空格。原始字符串按「收尾定界符的缩进」自动剥掉公共前缀，
        // 故这里的相对缩进即最终输出。
        //
        // <b>仍然逐行 os.write</b>，不把整块一次喂进去：OS.write 内部走
        // DisplayModule.cleanSplitForWidth + terminal.writeLine（OS.cs:1726-1737），
        // 一次喂多行会变成「一条含换行的记录」，滚动/回溯语义与逐行不同。
        // 注意 os.write("") 是空操作（:1728 的 text.Length > 0 门禁），故块内空行不产生空行。
        WriteLines(os, """
            autohack              - toggle the control panel
            autohack run [options] [target...] - run headless
              here      only the connected node (default: servers reachable via links)
              direct    skip connect, crack the node already connected
              stay      keep the connection at the end (this is the default)
              dc        disconnect each target when done (aborts a trace)
              redo      re-hack nodes already owned (default: skip them)
              keep      leave my traces in /log (default: wipe them map-wide)
              allnodes  sweep the whole map (default: only nodes reachable via links)
              mark      drop the marker file (default: no marker)
              creds     use known credentials to log in (default: off)
              nocreds   never log in - always crack ports
              show      play the native cracker animations (this is the default);
                        ports open when each animation finishes
              noshow    no animations: ports open immediately and nothing is ever
                        waited on - this is the fastest mode
              newip     assign a new IP after the run (this is the default)
              keepip    keep the current IP
              modports=A,B  also crack these mod-registered ports (default: none).
                        Names are the protocols other plugins registered, e.g.
                        'modports=mqtt,ntp,Redis'. Case-insensitive. They are
                        opened directly - the plugins' own crackers are NOT run.
              script=F  run a scripted action list from file F (see below)

            There is no step-interval option any more (v1.34.0 removed every
            timing delay). A run is bounded by the per-frame step budget, by one
            port step per frame (so cracks print one line at a time, 60/s), and -
            while 'show' is on - by the wait for each node's animations to finish.
            Every node still prints its full probe report in both modes.

            autohack <tool> [allnodes] - run one tool, no panel needed:
            """);
        foreach (var (tool, help) in ToolDispatch.Help)
        {
            os.write("  " + tool.PadRight(ToolDispatch.HelpVerbWidth) + help);
        }

        WriteLines(os, """
            Script files live in Content/HackerScripts/ and are plain text:
              connect / neutralize / probe / login / proxy / openPort [n]
              solve / porthack / mark / rm / dc / killtrace
              openPort with no number cracks every crackable port on the target.
              connect, neutralize and killtrace are always supplied - do not write them.
              'rm' must come before 'dc' or the script is rejected.
            """);
    }

    /// <summary>把整块文本逐行交给 <c>os.write</c>，空行照旧是空操作（见 <see cref="WriteHelp"/>）。</summary>
    private static void WriteLines(OS os, string block)
    {
        foreach (var line in block.Split('\n'))
        {
            os.write(line);
        }
    }

    /// <summary>执行单个工具动词；allnodes 口径按动词区分后交给 ToolDispatch。</summary>
    private static void RunTool(OS os, string verb, string[] args)
    {
        // 两个口径：
        // · dec / mem —— 显式传 allnodes 才扫地图全表，缺省只碰当前节点；
        // · wipe —— 缺省就是地图全表（命令行没有 SCOPE 段，而「我的痕迹」铺在
        //   哪些机器上与当前连着谁无关），要收窄到当前节点所在的连线分量传 here。
        var allNodes = verb.ToLowerInvariant() == ToolDispatch.Wipe
            ? !args.Skip(2).Any(a => a.Equals("here", StringComparison.OrdinalIgnoreCase))
            : args.Skip(2).Any(a => a.Equals("allnodes", StringComparison.OrdinalIgnoreCase));

        ToolDispatch.Run(os, verb, allNodes);
    }

    /// <summary>开关控制面板并回显开关后的状态。</summary>
    private static void TogglePanel(OS os)
    {
        HackOverlay.Toggle(os);
        os.write(HackOverlay.IsOpen
            ? "[autohack] Panel opened - 'autohack' again to close."
            : "[autohack] Panel closed.");
    }

    /// <summary>不开面板直接执行一次入侵：互斥检查、选项解析、脚本前置校验、入队。</summary>
    private static void RunHeadless(OS os, string[] args)
    {
        // 单写者：面板运行与 headless 运行互斥 —— 两条会同时 connect/disconnect
        // 同一个 os.connectedComp，互相把对方的目标换掉。
        if (HackOverlay.IsRunning)
        {
            os.write("[autohack] A panel run is still in progress - wait for it to finish.");
            return;
        }

        var rest = args.Skip(2).ToArray();

        // 已删除的开关必须在解析前拦下 —— 否则它们会静默变成「显式目标名」，
        // 玩家看到的是「No eligible targets」，与真实原因（那个开关没了）完全脱节。
        if (HackOptions.RetiredTokenIn(rest) is { } retired)
        {
            os.write("[autohack] '" + retired + "' was removed in v1.34.0 - there is no step"
                + " interval any more. Drop it; 'noshow' is the fastest mode.");
            return;
        }

        var options = HackOptions.Parse(rest);

        // 白名单里查不到的协议名报一次：模组未加载或名字拼错时，端口表里根本没有那个
        // 协议，整轮会安静地少开几个端口，玩家只会以为「这个开关没用」。
        // 只提示不拦截 —— 大小写不符也会落进这里（判据见 ModPortPolicy.Unknown 的注释），
        // 那种情况功能其实正常，拦下反而更糟。
        foreach (var missing in ModPortPolicy.Unknown(options.ModPorts))
        {
            os.write("[autohack] modports: no plugin has registered a port named '"
                + missing + "' - check the spelling (names are case-sensitive here).");
        }

        // 脚本在入队前校验一遍：语法错/文件缺失当场报出来，而不是等首帧构造
        // HackRun 时在游戏线程抛出。边界校验前置，错误带原始行号。
        if (!string.IsNullOrEmpty(options.Script))
        {
            try
            {
                var script = HackScript.Load(options.Script);
                os.write("[autohack] Script: " + script.Source + " (" + script.Actions.Count + " action(s)).");
            }
            catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
            {
                os.write("[autohack] Script error: " + ex.Message);
                return;
            }
        }

        // 只入队参数：HackRun 的构造含可达遍历（会改写网络地图），必须在游戏线程做。
        // 目标数与动作数的汇总行由 PendingRuns 在首帧打印。TryAdd 自带互斥。
        if (!PendingRuns.TryEnqueue(os, options))
        {
            os.write("[autohack] A run is already queued on this terminal - wait for it to finish.");
            return;
        }

        os.write("[autohack] Headless run queued.");
    }
}

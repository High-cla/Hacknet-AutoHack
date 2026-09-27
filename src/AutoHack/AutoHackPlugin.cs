namespace AutoHack;

using BepInEx;
using Hacknet;
using Pathfinder.Meta.Load;

/// <summary>
/// AutoHack — 全自动入侵。
/// 命令与扩展点均通过 Pathfinder 的属性自动扫描注册（AttributeManager 挂载于
/// HacknetChainloader.LoadPlugin），无需手动调用 Register* API。
/// </summary>
[BepInPlugin(Guid, "AutoHack", "1.32.0")]
// Pathfinder 的属性扫描是 IL hook，在 PathfinderAPIPlugin.Load() 里才安装；
// 缺此依赖本插件会先加载，扫描覆盖不到，命令静默失效。
[BepInDependency("com.Pathfinder.API")]
public sealed class AutoHackPlugin : BepInEx.Hacknet.HacknetPlugin
{
    internal const string Guid = "com.highcla.autohack";

    public override bool Load()
    {
        // 开机文字加速。必须在 PatchAll 之前、且在 OS 构造之前 ——
        // 它会首次触碰 CrashModule 触发静态初始化，那一刻 BOOT_TIME 才算出来。
        // CrashModule.BOOT_TIME 是 static 字段（CrashModule.cs:15），而 OS 在
        // OS.cs:500-501 才 new CrashModule(...)，本 Load() 一定更早。详见 BootBoost。
        BootBoost.Apply();

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
    /// autohack run [all|here] [目标...] [delay=秒] [logs] [dc] [nomark] - 不开面板，直接执行
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
            os.write("autohack              - toggle the control panel");
            os.write("autohack run [options] [target...] - run headless");
            os.write("  here      only the connected node (default: servers reachable via links)");
            os.write("  delay=s   seconds between port cracks (default 0.6, min 0.02)");
            os.write("  direct    skip connect, crack the node already connected");
            os.write("  stay      keep the connection at the end (this is the default)");
            os.write("  dc        disconnect each target when done (aborts a trace)");
            os.write("  redo      re-hack nodes already owned (default: skip them)");
            os.write("  nologs    keep /log intact (this is the default)");
            os.write("  logs      wipe the target's /log");
            os.write("  allnodes  sweep the whole map (default: only nodes reachable via links)");
            os.write("  mark      drop the marker file (default: no marker)");
            os.write("  creds     use known credentials to log in (default: off)");
            os.write("  nocreds   never log in - always crack ports");
            os.write("  show      play the native cracker animations (default: off)");
            os.write("  noshow    no animations (this is the default)");
            os.write("  instant   run every non-port step in the same frame (fastest)");
            os.write("  fast      shorten the pause between non-port steps (default: normal)");
            os.write("  script=F  run a scripted action list from file F (see below)");
            os.write("");
            os.write("autohack <tool> [allnodes] - run one tool, no panel needed:");
            foreach (var (tool, help) in ToolDispatch.Help)
            {
                os.write("  " + tool.PadRight(ToolDispatch.HelpVerbWidth) + help);
            }

            os.write("");
            os.write("Script files live in Content/HackerScripts/ and are plain text:");
            os.write("  connect / neutralize / probe / login / proxy / openPort [n]");
            os.write("  solve / porthack / mark / rm / dc / killtrace / delay s");
            os.write("  openPort with no number cracks every crackable port on the target.");
            os.write("  connect, neutralize and killtrace are always supplied - do not write them.");
            os.write("  'rm' must come before 'dc' or the script is rejected.");
            return;
        }

        // 四个工具与 run 平级，各自独立执行；allnodes 只对 dec / mem 有意义。
        if (ToolDispatch.Handles(verb))
        {
            var allNodes = args.Skip(2).Any(a => a.Equals("allnodes", StringComparison.OrdinalIgnoreCase));
            ToolDispatch.Run(os, verb, allNodes);
            return;
        }

        // 裸 `autohack`（verb 为 null）与任何未知动词都归这里：开关面板。
        // 这也是 v1.32.0 之前**所有**命令行的归宿 —— 当时动词读的是 args[0]（恒为
        // "autohack"），四个分支没一个能命中，于是 run 与全部工具都变成了开面板。
        if (verb == null || !verb.Equals("run", StringComparison.OrdinalIgnoreCase))
        {
            HackOverlay.Toggle(os);
            os.write(HackOverlay.IsOpen
                ? "[autohack] Panel opened - 'autohack' again to close."
                : "[autohack] Panel closed.");
            return;
        }

        // 单写者：面板运行与 headless 运行互斥 —— 两条会同时 connect/disconnect
        // 同一个 os.connectedComp，互相把对方的目标换掉。
        if (HackOverlay.IsRunning)
        {
            os.write("[autohack] A panel run is still in progress - wait for it to finish.");
            return;
        }

        var options = HackOptions.Parse(args.Skip(2).ToArray());

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

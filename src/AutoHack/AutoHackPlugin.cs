namespace AutoHack;

using BepInEx;
using Hacknet;
using Pathfinder.Meta.Load;

/// <summary>
/// AutoHack — 全自动入侵。
/// 命令与扩展点均通过 Pathfinder 的属性自动扫描注册（AttributeManager 挂载于
/// HacknetChainloader.LoadPlugin），无需手动调用 Register* API。
/// </summary>
[BepInPlugin(Guid, "AutoHack", "1.21.0")]
// Pathfinder 的属性扫描是 IL hook，在 PathfinderAPIPlugin.Load() 里才安装；
// 缺此依赖本插件会先加载，扫描覆盖不到，命令静默失效。
[BepInDependency("com.Pathfinder.API")]
public sealed class AutoHackPlugin : BepInEx.Hacknet.HacknetPlugin
{
    internal const string Guid = "com.highcla.autohack";

    public override bool Load()
    {
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

        if (args is { Length: > 0 } && args[0] is "-h" or "--help" or "help")
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
            os.write("  instant   run every non-port step in the same frame (fastest)");
            os.write("  fast      shorten the pause between non-port steps (default: normal)");
            os.write("  script=F  run a scripted action list from file F (see below)");
            os.write("");
            os.write("autohack <tool> [allnodes] - run one tool, no panel needed:");
            foreach (var (verb, help) in ToolDispatch.Help)
            {
                os.write("  " + help);
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
        if (args is { Length: > 0 } && ToolDispatch.Handles(args[0]))
        {
            var allNodes = args.Skip(1).Any(a => a.Equals("allnodes", StringComparison.OrdinalIgnoreCase));
            ToolDispatch.Run(os, args[0], allNodes);
            return;
        }

        if (args is not { Length: > 0 } || !args[0].Equals("run", StringComparison.OrdinalIgnoreCase))
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

        var options = HackOptions.Parse(args.Skip(1).ToArray());

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

namespace AutoHack;

using BepInEx;
using Hacknet;
using Pathfinder.Meta.Load;

/// <summary>
/// AutoHack — 全自动入侵。
/// 命令与扩展点均通过 Pathfinder 的属性自动扫描注册（AttributeManager 挂载于
/// HacknetChainloader.LoadPlugin），无需手动调用 Register* API。
/// </summary>
[BepInPlugin(Guid, "AutoHack", "1.7.0")]
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
    /// autohack run [all|here] [目标...] [delay=秒] [nologs] [nomark] - 不开面板，直接执行
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
            os.write("  here      only the connected node (default: whole network)");
            os.write("  delay=s   seconds between port cracks (default 0.6)");
            os.write("  direct    skip connect, crack the node already connected");
            os.write("  stay      keep the connection at the end (default: dc, which aborts a trace)");
            os.write("  redo      re-hack nodes already owned (default: skip them)");
            os.write("  nologs    keep /log intact (default: wipe)");
            os.write("  nomark    do not drop the marker file (default: upload)");
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

        var options = HackOptions.Parse(args.Skip(1).ToArray());
        var run = new HackRun(os, options);

        if (run.Total == 0)
        {
            os.write("[autohack] No eligible targets found.");
            return;
        }

        PendingRuns.Enqueue(os, run);
        var skipped = run.SkippedOwned > 0 ? $", {run.SkippedOwned} owned node(s) skipped" : string.Empty;
        os.write($"[autohack] Headless run: {run.Targets.Count} target(s), {run.Total} action(s){skipped}.");
    }
}

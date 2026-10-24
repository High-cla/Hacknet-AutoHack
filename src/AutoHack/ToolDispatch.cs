namespace AutoHack;

using Hacknet;

/// <summary>
/// 工具的唯一入口：命令行与面板按钮都从这里进来，保证「单实现双入口」。
/// 这些工具<b>不</b>参与 <c>autohack run</c> 的自动入侵流程，只在显式调用时执行。

/// <b>列表来自 <see cref="ToolRegistry"/>，不再是编译期常量</b>：内置 14 个与外部的
/// <see cref="AutoHackApi.RegisterTool"/> 登记到同一张表，帮助行与面板按钮都按那份顺序渲染。
/// 加一个工具因此只需在一处登记。
///
/// 异常护栏也在这里，不在调用方 —— 两个入口各包一层就会漏掉一个：命令入口此前
/// 完全裸奔，而 Pathfinder 的事件层自带 catch（EventManager.cs:104-113）会吞掉异常，
/// 玩家在终端看不到任何提示，只表现为「按了没反应」。
/// </summary>
internal static class ToolDispatch
{
    internal const string Scan = "scan";
    internal const string Dec = "dec";
    internal const string Mem = "mem";
    internal const string Exes = "exes";
    internal const string Unbreakable = "unbreakable";
    internal const string Pull = "pull";
    internal const string Purge = "purge";
    internal const string Drop = "drop";
    internal const string Skip = "skip";
    internal const string Trace = "trace";
    internal const string Ip = "ip";
    internal const string Wipe = "wipe";
    internal const string Mods = "mods";
    internal const string Ports = "ports";

    /// <summary>help 里动词列的宽度，供调用方排版。</summary>
    internal const int HelpVerbWidth = 18;

    /// <summary>全部动词（小写）。用于判定与帮助排版。</summary>
    internal static IReadOnlyList<string> Verbs => ToolRegistry.Verbs;

    /// <summary>help 文本来源：子命令与其说明只写一次（注册表），命令入口与文档都读这里。</summary>
    internal static (string Verb, string Help)[] Help => ToolRegistry.HelpRows;

    /// <summary>
    /// 面板 TOOLS 区的按钮表：动词 + 按钮文案 + 是否危险（危险按钮用告警色）。
    /// 顺序即注册顺序，面板按长度自算行数。
    /// </summary>
    internal static (string Verb, string Label, bool Danger)[] Panel => ToolRegistry.PanelRows;

    /// <summary>
    /// 命令动词的归一化。<b>两个入口都必须过这里</b> —— 否则 <see cref="Handles"/> 认了、
    /// 执行时查表查不到，命令会静默什么都不做。
    ///
    /// 为什么必须不区分大小写：游戏自己的 <c>ProgramRunner.ExecuteProgram</c> 大量写的是
    /// <c>array[0].ToLower().Equals("connect")</c>（ProgramRunner.cs:15/46/55），玩家由此
    /// 天然预期终端命令不分大小写；中文输入法下敲英文大小写随机，这条尤其容易踩。
    /// </summary>
    private static string Canonical(string verb) => verb?.ToLowerInvariant();

    internal static bool Handles(string verb) => ToolRegistry.TryGet(Canonical(verb), out _);

    internal static void Run(OS os, string verb, bool allNodes)
    {
        var canonical = Canonical(verb);
        if (!ToolRegistry.TryGet(canonical, out var tool))
        {
            return;
        }

        try
        {
            tool.Run(os, allNodes);
        }
        catch (Exception ex) when (ex is FormatException or NullReferenceException
                                       or ArgumentException or IndexOutOfRangeException
                                       or InvalidOperationException or IOException)
        {
            // 工具读的是存档里的第三方数据，坏数据不该把游戏线程带崩。
            // InvalidOperationException 在列上是因为「Collection was modified」：
            // 命令走 OS.execute 的独立线程（OS.cs:1754-1767），cd 会改 os.navigationPath，
            // 而本方法在游戏线程读它 —— 工具与终端并发时这是唯一的真实竞态面。
            os.write("[autohack] " + canonical + " failed: " + ex.GetType().Name + " - " + ex.Message);
        }
    }
}

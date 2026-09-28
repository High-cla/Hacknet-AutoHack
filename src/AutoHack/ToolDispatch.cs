namespace AutoHack;

using System.IO;
using Hacknet;

/// <summary>
/// 工具的唯一入口：命令行与面板按钮都从这里进来，保证「单实现双入口」。
/// 这些工具**不**参与 autohack run 的自动入侵流程，只在显式调用时执行。
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

    private static readonly string[] Verbs = { Scan, Dec, Mem, Exes, Unbreakable, Pull, Purge, Drop, Trace, Skip, Ip };

    /// <summary>help 文本来源：子命令与其说明只写一次，命令入口与文档都读这里。</summary>
    internal static readonly (string Verb, string Help)[] Help =
    {
        (Scan, "reveal the whole network component around the current node"),
        (Dec, "dec [allnodes]  decrypt every #DEC_ENC file into /home/MemDumps"),
        (Mem, "mem [allnodes]  show + export this machine's memory, scan for dumps"),
        (Exes, "fill /bin with every crack program the game can produce"),
        (Unbreakable, "harden THIS machine (irreversible)"),
        (Pull, "download every file in the current directory to local home"),
        (Purge, "delete every file in the current directory (shared with log wipe)"),
        (Drop, "disconnect and remove the connected node from the map"),
        (Trace, "anti-trace: stop the countdown and every pending tracker"),
        (Skip, "complete the active mission and take the next one"),
        (Ip, "assign this machine a new IP (keeps owned-node tags in sync)"),
    };

    /// <summary>help 里动词列的宽度，供调用方排版。</summary>
    internal const int HelpVerbWidth = 18;

    /// <summary>
    /// 命令动词的归一化。<b>两个入口都必须过这里</b> —— 否则 <see cref="Handles"/> 认了、
    /// <see cref="Dispatch"/> 的 switch 认不得，命令会静默什么都不做。
    ///
    /// 为什么必须不区分大小写：游戏自己的 <c>ProgramRunner.ExecuteProgram</c> 大量写的是
    /// <c>array[0].ToLower().Equals("connect")</c>（ProgramRunner.cs:15/46/55），玩家由此
    /// 天然预期终端命令不分大小写；本插件的 <c>run</c> 分支用的也是 OrdinalIgnoreCase。
    /// 中文输入法下敲英文大小写随机，这条尤其容易踩。
    ///
    /// <b>注意它不负责的事</b>：v1.31.0 曾把 <c>autohack SKIP</c> 无效归因于「工具这边
    /// 用了区分大小写的 <c>Array.IndexOf</c>」—— 那是误诊。真凶是动词读错了参数位
    /// （<c>args[0]</c> 恒为命令名 <c>"autohack"</c>，动词在 <c>args[1]</c>），
    /// 已由调用方修正。归一化修的是另一个真问题，两者曾叠成同一个症状
    /// 「全都掉进开关面板分支」。
    /// </summary>
    private static string Canonical(string verb) => verb?.ToLowerInvariant();

    internal static bool Handles(string verb)
        => Array.IndexOf(Verbs, Canonical(verb)) >= 0;

    internal static void Run(OS os, string verb, bool allNodes)
    {
        var canonical = Canonical(verb);
        try
        {
            Dispatch(os, canonical, allNodes);
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

    private static void Dispatch(OS os, string verb, bool allNodes)
    {
        switch (verb)
        {
            case Scan:
                ScanTools.Run(os);
                break;

            case Dec:
                DecTools.Run(os, allNodes);
                break;

            case Mem:
                MemTools.Run(os, allNodes);
                break;

            case Exes:
                ExeTools.Run(os);
                break;

            case Unbreakable:
                HardenTools.Run(os);
                break;

            case Pull:
                RemoteTools.Pull(os);
                break;

            case Purge:
                RemoteTools.Purge(os);
                break;

            case Drop:
                RemoteTools.Drop(os);
                break;

            case Trace:
                TraceTools.Run(os);
                break;

            case Skip:
                MissionTools.Run(os);
                break;

            case Ip:
                IpTools.Run(os);
                break;
        }
    }
}

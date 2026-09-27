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

    private static readonly string[] Verbs = { Scan, Dec, Mem, Exes, Unbreakable, Pull, Purge, Drop };

    /// <summary>help 文本来源：子命令与其说明只写一次，命令入口与文档都读这里。</summary>
    internal static readonly (string Verb, string Help)[] Help =
    {
        (Scan, "scan                  reveal the whole network component around the current node"),
        (Dec, "dec [allnodes]        decrypt every #DEC_ENC file into /home/MemDumps"),
        (Mem, "mem [allnodes]        show + export this machine's memory, scan for dumps"),
        (Exes, "exes                  fill /bin with every crack program the game can produce"),
        (Unbreakable, "unbreakable           harden THIS machine (irreversible)"),
        (Pull, "pull                  download every file in the current directory to local home"),
        (Purge, "purge                 delete every file in the current directory (shared with log wipe)"),
        (Drop, "drop                  disconnect and remove the connected node from the map"),
    };

    internal static bool Handles(string verb)
        => verb != null && Array.IndexOf(Verbs, verb) >= 0;

    internal static void Run(OS os, string verb, bool allNodes)
    {
        try
        {
            Dispatch(os, verb, allNodes);
        }
        catch (Exception ex) when (ex is FormatException or NullReferenceException
                                       or ArgumentException or IndexOutOfRangeException
                                       or InvalidOperationException or IOException)
        {
            // 工具读的是存档里的第三方数据，坏数据不该把游戏线程带崩。
            // InvalidOperationException 在列上是因为「Collection was modified」：
            // 命令走 OS.execute 的独立线程（OS.cs:1754-1767），cd 会改 os.navigationPath，
            // 而本方法在游戏线程读它 —— 工具与终端并发时这是唯一的真实竞态面。
            os.write("[autohack] " + verb + " failed: " + ex.GetType().Name + " - " + ex.Message);
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
        }
    }
}

namespace AutoHack;

using Hacknet;

/// <summary>
/// 四个工具的唯一入口：命令行与面板按钮都从这里进来，保证「单实现双入口」。
/// 这些工具**不**参与 autohack run 的自动入侵流程，只在显式调用时执行。
/// </summary>
internal static class ToolDispatch
{
    internal const string Dec = "dec";
    internal const string Mem = "mem";
    internal const string Exes = "exes";
    internal const string Unbreakable = "unbreakable";

    private static readonly string[] Verbs = { Dec, Mem, Exes, Unbreakable };

    /// <summary>help 文本来源：子命令与其说明只写一次，命令入口与文档都读这里。</summary>
    internal static readonly (string Verb, string Help)[] Help =
    {
        (Dec, "dec [allnodes]        decrypt every #DEC_ENC file into /home"),
        (Mem, "mem [allnodes]        show + export this machine's memory, scan for dumps"),
        (Exes, "exes                  fill /bin with every crack program the game can produce"),
        (Unbreakable, "unbreakable           harden THIS machine (irreversible)"),
    };

    internal static bool Handles(string verb)
        => verb != null && Array.IndexOf(Verbs, verb) >= 0;

    internal static void Run(OS os, string verb, bool allNodes)
    {
        switch (verb)
        {
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
        }
    }
}

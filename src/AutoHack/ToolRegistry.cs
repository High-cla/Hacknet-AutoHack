namespace AutoHack;

using Hacknet;

/// <summary>一个工具：动词、帮助行、面板按钮文案、是否危险、执行体。</summary>
internal sealed record ToolDefinition(
    string Verb, string Help, string Label, bool Danger, Action<OS, bool> Run);

/// <summary>
/// 工具注册表：内置 14 个与外部的 <see cref="AutoHackApi.RegisterTool"/> 走同一条路。

/// <b>为什么要注册表而不是 switch</b>：加一个工具原本要改常量、动词数组、帮助表、
/// 面板按钮表和 switch 五处；更重要的是<b>外部无法参与</b> —— switch 分支是编译期固定的。
/// 注册表把「有哪些工具」变成运行期数据，插件之间因此可以互相扩展。
///
/// <b>线程模型</b>：写入只发生在插件加载期（<see cref="AutoHackPlugin.Load"/> 前后），
/// 读取发生在命令线程与绘制线程。故用一把锁保护写入，并整体重建快照数组 ——
/// 读侧永远拿到一个不可变的数组，不需要加锁。
/// </summary>
internal static class ToolRegistry
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, ToolDefinition> ByVerb = new(StringComparer.Ordinal);
    private static ToolDefinition[] _ordered = Array.Empty<ToolDefinition>();
    private static string[] _verbs = Array.Empty<string>();
    private static (string Verb, string Help)[] _helpRows = Array.Empty<(string, string)>();
    private static (string Verb, string Label, bool Danger)[] _panelRows =
        Array.Empty<(string, string, bool)>();

    static ToolRegistry()
    {
        RegisterBuiltIns();
    }

    /// <summary>按注册顺序排列的全部工具（只读快照）。</summary>
    internal static IReadOnlyList<ToolDefinition> All => _ordered;

    /// <summary>全部动词（小写，用于判定与帮助排版）。</summary>
    internal static IReadOnlyList<string> Verbs => _verbs;

    /// <summary>帮助表（动词 + 说明），注册时重建一次，读取不分配。</summary>
    internal static (string Verb, string Help)[] HelpRows => _helpRows;

    /// <summary>面板按钮表（动词 + 文案 + 是否危险），注册时重建一次，读取不分配。</summary>
    internal static (string Verb, string Label, bool Danger)[] PanelRows => _panelRows;

    /// <summary>按动词取定义；动词须已归一化（<c>ToLowerInvariant</c>）。</summary>
    internal static bool TryGet(string canonicalVerb, out ToolDefinition definition)
    {
        if (canonicalVerb == null)
        {
            definition = null;
            return false;
        }

        return ByVerb.TryGetValue(canonicalVerb, out definition);
    }

    /// <summary>
    /// 登记一个工具。<b>重复动词一律拒绝</b>：同名工具会让「命令打哪一个」变成
    /// 加载顺序的函数，玩家无从预期；扩展要用别的名字。
    /// </summary>
    internal static bool TryRegister(ToolDefinition definition, out string error)
    {
        error = null;
        if (definition == null || string.IsNullOrEmpty(definition.Verb) || definition.Run == null)
        {
            error = "tool definition needs a verb and an action.";
            return false;
        }

        lock (Gate)
        {
            if (ByVerb.ContainsKey(definition.Verb))
            {
                error = "tool '" + definition.Verb + "' is already registered.";
                return false;
            }

            ByVerb[definition.Verb] = definition;
            Rebuild();
            return true;
        }
    }

    /// <summary>内置工具。顺序即帮助列表顺序。</summary>
    private static void RegisterBuiltIns()
    {
        Add(ToolDispatch.Scan, "scan [allnodes]  reveal the component around the current node - or the whole map",
            "SCAN NETWORK", false, (os, allNodes) => ScanTools.Run(os, allNodes));
        Add(ToolDispatch.Dec, "dec [allnodes]  decrypt every #DEC_ENC file into /home/MemDumps",
            "DEC DECRYPT", false, (os, allNodes) => DecTools.Run(os, allNodes));
        Add(ToolDispatch.Mem, "mem [allnodes]  show + export this machine's memory, scan for dumps",
            "MEMORY DUMP", false, (os, allNodes) => MemTools.Run(os, allNodes));
        Add(ToolDispatch.Exes, "fill /bin with every crack program the game can produce",
            "FILL /BIN", false, (os, _) => ExeTools.Run(os));
        Add(ToolDispatch.Mods, "list + add the crack exes other plugins registered into /bin",
            "MOD EXES", false, (os, _) => ModTools.Run(os));
        Add(ToolDispatch.Ports, "list the connected node's ports (protocol, display port, original port, state)",
            "PORTS", false, (os, _) => PortTools.Run(os));
        Add(ToolDispatch.Unbreakable, "harden THIS machine (irreversible)",
            "HARDEN", true, (os, _) => HardenTools.Run(os));
        Add(ToolDispatch.Pull, "download every file in the current directory to local home",
            "PULL FILES", false, (os, _) => RemoteTools.Pull(os));
        Add(ToolDispatch.Purge, "delete every file in the current directory",
            "PURGE FILES", true, (os, _) => RemoteTools.Purge(os));
        Add(ToolDispatch.Drop, "disconnect and remove the connected node from the map",
            "DROP NODE", true, (os, _) => RemoteTools.Drop(os));
        Add(ToolDispatch.Trace, "anti-trace: stop the countdown + every pending tracker, kill forkbombs",
            "ANTI-TRACE", false, (os, _) => TraceTools.Run(os));
        Add(ToolDispatch.Skip, "complete the active mission and take the next one",
            "SKIP MISSION", false, (os, _) => MissionTools.Run(os));
        Add(ToolDispatch.Ip, "assign this machine a new IP (keeps owned-node tags in sync)",
            "NEW IP", false, (os, _) => IpTools.Run(os));
        Add(ToolDispatch.Wipe, "wipe MY traces  [here]  (/log entries naming your IP; default: whole map)",
            "WIPE TRACES", true, (os, allNodes) => HackEngine.WipeNetwork(os, allNodes));
    }

    private static void Add(
        string verb, string help, string label, bool danger, Action<OS, bool> run)
    {
        ByVerb[verb] = new ToolDefinition(verb, help, label, danger, run);
        Rebuild();
    }

    /// <summary>写入后整体重建快照 —— 读侧永远拿到不可变数组，不需要加锁。</summary>
    private static void Rebuild()
    {
        _ordered = ByVerb.Values.ToArray();
        _verbs = ByVerb.Keys.ToArray();

        _helpRows = new (string Verb, string Help)[_ordered.Length];
        _panelRows = new (string Verb, string Label, bool Danger)[_ordered.Length];
        for (var i = 0; i < _ordered.Length; i++)
        {
            _helpRows[i] = (_ordered[i].Verb, _ordered[i].Help);
            _panelRows[i] = (_ordered[i].Verb, _ordered[i].Label, _ordered[i].Danger);
        }
    }
}

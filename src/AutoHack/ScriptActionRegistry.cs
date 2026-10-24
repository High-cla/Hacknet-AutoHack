namespace AutoHack;

using Hacknet;

/// <summary>一个扩展动作：脚本里的动词、帮助行、执行体。</summary>
internal sealed record ScriptActionDefinition(string Name, string Help, Action<OS, Computer> Run);

/// <summary>
/// 扩展脚本动作表：`script=` 里写一个已登记的名字，就在该位置对该目标执行一次。
///
/// <b>执行上下文</b>：动作按脚本次序执行，此刻的 <c>os.connectedComp</c> 就是当时连着的机器
/// （脚本没写 <c>connect</c> 时可能为空）—— 语义完全交给扩展，本插件不做假设。
///
/// <b>为什么状态仍然留在核心层</b>：注册表只回答「这个名字对应哪个委托」，
/// 计划里存的是名字（<see cref="AutoHack.Core.GameAction"/> 的 <c>Custom</c> 字段），
/// 核心因此不引用任何委托表，仍可离线测试（测试传一个名字列表即可）。
/// </summary>
internal static class ScriptActionRegistry
{
    private static readonly object Gate = new();

    private static readonly Dictionary<string, ScriptActionDefinition> Actions =
        new(StringComparer.OrdinalIgnoreCase);

    private static string[] _names = Array.Empty<string>();

    /// <summary>已登记的动作名快照（传给解析器用；读取零分配）。</summary>
    internal static IReadOnlyList<string> Names => _names;

    /// <summary>按名字取执行体；名字须已归一化（大小写不敏感）。</summary>
    internal static bool TryGet(string name, out ScriptActionDefinition definition)
    {
        if (name == null)
        {
            definition = null;
            return false;
        }

        return Actions.TryGetValue(name, out definition);
    }

    /// <summary>
    /// 登记一个动作。三类拒绝：空名、与内置动词重名、与已登记动作重名 ——
    /// 重名都会让「脚本里这个词到底做什么」取决于加载顺序，玩家无从预期。
    /// </summary>
    internal static bool TryRegister(ScriptActionDefinition definition, out string error)
    {
        error = null;
        if (definition == null || string.IsNullOrWhiteSpace(definition.Name) || definition.Run == null)
        {
            error = "script action needs a name and a delegate.";
            return false;
        }

        var name = definition.Name.Trim();
        if (AutoHack.Core.HackScript.IsBuiltInVerb(name))
        {
            error = "'" + name + "' is a built-in script action and cannot be overridden.";
            return false;
        }

        lock (Gate)
        {
            if (Actions.ContainsKey(name))
            {
                error = "script action '" + name + "' is already registered.";
                return false;
            }

            Actions[name] = definition with { Name = name };
            _names = Actions.Keys.ToArray();
            return true;
        }
    }
}

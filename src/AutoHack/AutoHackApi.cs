namespace AutoHack;

using Hacknet;

/// <summary>
/// 给其它插件的公开入口：把自己的工具挂进 <c>autohack</c> 命令与面板。

/// <b>调用时机</b>：在扩展插件自己的 <c>PostLoad</c> 里调用 —— Pathfinder 的
/// <c>PostLoadEvent</c>（Pathfinder.Event.BepInEx/PostLoadEvent.cs）按程序集派发，
/// 那个回调只有扩展自己收得到；AutoHack 侧不需要扫描，也不知道谁装了它。
///
/// <b>为什么是显式调用而不是属性扫描</b>：标一个 <c>[AutoHackTool]</c> 仍要引用本程序集，
/// 与直接调一行 API 的成本相同；但扫描要自己实现反射遍历与加载顺序容错，是净负债。
///
/// <b>重复动词一律拒绝</b>（抛 <see cref="ArgumentException"/>）：同名工具会让
/// 「命令打哪一个」变成加载顺序的函数，玩家无从预期。
/// </summary>
public static class AutoHackApi
{
    /// <summary>本插件版本，供扩展做兼容判断。</summary>
    public static string Version => AutoHackPlugin.Version;

    /// <summary>
    /// 登记一个工具。登记后立刻出现在 <c>autohack help</c> 与面板按钮区（排在末尾）。
    /// </summary>
    /// <param name="verb">命令动词，大小写不敏感；与已有工具重名会被拒绝。</param>
    /// <param name="help">帮助行（含用法与一句说明）。</param>
    /// <param name="run">执行体；<c>allNodes</c> 与内置工具同义（面板的 whole map 勾选）。</param>
    /// <param name="label">面板按钮文案；省略则用动词大写。</param>
    /// <param name="danger">为真时按钮用告警色。</param>
    public static void RegisterTool(
        string verb, string help, Action<OS, bool> run, string label = null, bool danger = false)
    {
        if (string.IsNullOrWhiteSpace(verb))
        {
            throw new ArgumentException("verb is required.", nameof(verb));
        }

        if (help == null)
        {
            throw new ArgumentNullException(nameof(help));
        }

        if (run == null)
        {
            throw new ArgumentNullException(nameof(run));
        }

        var canonical = verb.Trim().ToLowerInvariant();
        var definition = new ToolDefinition(canonical, help, label ?? canonical.ToUpperInvariant(), danger, run);
        if (!ToolRegistry.TryRegister(definition, out var error))
        {
            throw new ArgumentException(error, nameof(verb));
        }
    }
}

namespace AutoHack;

using Hacknet;
using HarmonyLib;
using Pathfinder.Util;

/// <summary>
/// 修复 Pathfinder 重写的 Action 加载器不做本地化，导致 DLC 的 IRC 对话、开场剧情等
/// 「人物对话」在中文环境下仍显示英文。
///
/// 原生链（<c>decompiled/game-proj/Hacknet/RunnableConditionalActions.cs:90</c>）：
/// <code>
/// File.OpenRead(LocalizedFileLoader.GetLocalizedFilepath(Utils.GetFileLoadPrefix() + filepath))
/// </code>
/// 而 Pathfinder 把 <c>RunnableConditionalActions.LoadIntoOS</c> 整个 Prefix 掉了
/// （<c>decompiled/pathfinder/Pathfinder.Replacements/ActionsLoader.cs:20-41</c>），替换成：
/// <code>
/// new EventExecutor(filepath.ContentFilePath(), isPath: true)
/// </code>
/// <c>ContentFilePath()</c>（<c>decompiled/pathfinder/Pathfinder.Util/StringExtensions.cs:19-36</c>）
/// 只补 <c>"Content/"</c> 前缀或扩展目录，<b>不查本地化路径</b>；随后
/// <c>EventReader</c>（<c>decompiled/pathfinder/Pathfinder.Util.XML/EventReader.cs:50</c>）
/// 直接 <c>File.ReadAllText</c>。于是 <c>Content/Locales/zh-cn/DLC/ActionScripts/*.xml</c>
/// 里已经译好的中文永远不会被读到 —— 这正是「人物对话翻译不全」的来源。
///
/// 旁证：zh-cn 的语言覆盖率与其他语言持平（502 个源文本中译了 372 个，de-de 376、
/// es-ar/ko-kr/ru-ru 371），说明缺口不是官方漏译，而是译文读不到。
///
/// 修法：不改 Pathfinder（它是外部 DLL），只在 <c>ContentFilePath</c> 出口补一次
/// <see cref="LocalizedFileLoader.GetLocalizedFilepath"/>。该方法的语义是「本地化版本
/// 存在才替换，否则原样返回」，故：
/// - <c>en-us</c> 与扩展模式（路径不含 <c>Content/</c>）行为逐字不变；
/// - <c>zh-cn</c> 下自动命中已译文件。
///
/// 覆盖 <c>ContentFilePath</c> 的全部调用点（ActionsLoader / CachedCustomTheme /
/// MissionLoader / SaveLoader / DebugCommands），而不是逐个打补丁。
/// </summary>
[HarmonyPatch]
internal static class LocalizationFix
{
    /// <summary>
    /// 一条 Postfix 覆盖 <c>ContentFilePath</c> 全部调用点。
    ///
    /// 为什么选它而不是 <c>ActionsLoader.LoadActionsIntoOSPrefix</c>：
    /// Pathfinder 已经对 <c>RunnableConditionalActions.LoadIntoOS</c> 挂了 Prefix 并
    /// 返回 false。Harmony 遇到首个返回 false 的 Prefix 就中止后续 Prefix 与原方法，
    /// 再挂一个同类 Prefix 只能二选一（要么 Pathfinder 的自定义 Action 加载失效，
    /// 要么本修复失效）。挂在被它调用的纯函数出口，两者才能共存。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StringExtensions), nameof(StringExtensions.ContentFilePath))]
    private static void AfterContentFilePath(ref string __result)
    {
        // 与原生 Utils.readEntireFile（Utils.cs:329）同一道守卫：en-us 是源语言，
        // 没有 Content/Locales/en-us/ 目录，直取原文即可，也避免将来多一次无谓的
        // File.Exists。非英文时按 locale 改写，命中已译文件才替换。
        if (!string.IsNullOrEmpty(__result) && Settings.ActiveLocale != "en-us")
        {
            __result = LocalizedFileLoader.GetLocalizedFilepath(__result);
        }
    }
}

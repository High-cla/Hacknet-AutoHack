namespace AutoHack;

using Pathfinder.Util;

/// <summary>
/// 内容文件路径解析 —— 核心层（<see cref="AutoHack.Core.HackScript"/>）的唯一外部路径知识源。
///
/// <b>为什么是一个委托而不是核心自己调框架</b>：「内容目录在哪」是游戏/框架的知识
/// （扩展模式走扩展目录，否则补 <c>"Content/"</c>），Pathfinder 的
/// <c>StringExtensions.ContentFilePath</c>（StringExtensions.cs:19-36）已实现过一次；
/// 核心层不引用 Pathfinder，故由本适配器以委托形式注入，测试传恒等函数即可离线跑。
///
/// <b>本地化不用自己调</b>：<see cref="LocalizationFix"/> 已把 <c>GetLocalizedFilepath</c>
/// 挂在 <c>ContentFilePath</c> 出口（Postfix），故这里每次调用都自动命中译文。
/// </summary>
internal static class ContentPaths
{
    /// <summary>把「相对内容根的路径」解析成真实路径（含扩展模式与本地化）。</summary>
    internal static string Resolve(string relative) => relative.ContentFilePath();
}

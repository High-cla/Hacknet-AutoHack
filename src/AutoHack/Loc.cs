namespace AutoHack;

using System.Globalization;
using Hacknet;

/// <summary>
/// 面板文案的中英对照。
///
/// 为什么不硬编码中文：面板走 <c>GuiData.smallfont</c>，该字段在非 CJK locale 下是
/// 内置 <c>Font16.xnb</c>（6 KB，无汉字字形），画中文只会得到一片 default character
/// 方块。汉字字形只存在于 <c>Content/Locales/zh-cn/Fonts/zh-cn_Font16.xnb</c>（701 KB），
/// 而它仅在 locale 为 CJK 时经 <c>LocaleFontLoader.LoadFontConfigForLocale</c> 装进
/// <c>GuiData</c>。故文案必须跟随 locale，不能写死。
///
/// 查表次序：游戏自己的 <see cref="LocaleTerms.Loc"/>（未命中时原样返回）→ 本表 →
/// 英文原文。游戏词表只覆盖 UI 通用词（Back / Delete / Exit…），面板专有词
/// （RUN / SCOPE / SPEED…）不在其中，故本表是主力；把游戏放在前面是为了将来
/// 官方补译时自动跟随。
///
/// <b>不要</b>用 <c>LocaleActivator.ActiveLocaleIsCJK()</c> 决定用不用中文 —— 它回答的
/// 是「当前字体里有没有汉字」（zh / ja / ko 都算），而这里问的是「该给玩家看哪份
/// 文案」。日文玩家拿到中文，比拿到英文更糟，故只认 <c>zh</c>。
/// </summary>
internal static class Loc
{
    private static bool UseChinese
        => Settings.ActiveLocale != null
           && Settings.ActiveLocale.StartsWith("zh", StringComparison.Ordinal);

    /// <summary>英文原文 → 中文。键必须与面板里的字面量逐字一致。</summary>
    private static readonly Dictionary<string, string> Chinese =
        new(StringComparer.Ordinal)
        {
            // 标题与主按钮
            ["AUTOHACK"] = "自动入侵",
            ["RUN"] = "开始",
            ["RUN AGAIN"] = "再次运行",

            // SCOPE
            ["SCOPE"] = "范围",
            ["NETWORK SWEEP"] = "全网扫描",
            ["CURRENT NODE"] = "当前节点",

            // PORT INTERVAL
            ["PORT INTERVAL"] = "端口间隔",

            // 勾选框
            ["use known creds"] = "用已知账密",
            ["whole map"] = "整张地图",
            ["skip owned"] = "跳过已拿下",
            ["wipe target logs"] = "清除目标日志",
            ["connect first"] = "先连接",
            ["anti-trace dc"] = "反追踪断开",
            ["upload marker"] = "上传标记",
            ["wipe my logs"] = "清除我的日志",
            ["native exes"] = "原生演出",

            // TOOLS
            ["TOOLS"] = "工具",
            ["SCAN NETWORK"] = "扫描网络",
            ["DEC DECRYPT"] = "解密 DEC",
            ["MEMORY DUMP"] = "内存转储",
            ["ALL PROGRAMS"] = "全部程序",
            ["UNBREAKABLE"] = "不可摧毁",
            ["PULL FILES"] = "拉取文件",
            ["PURGE FILES"] = "清除文件",
            ["DROP NODE"] = "摘除节点",

            // 运行态
            ["ENGAGING"] = "执行中",
            ["READY"] = "就绪",
        };

    /// <summary>查一条静态文案。</summary>
    internal static string T(string english)
    {
        if (string.IsNullOrEmpty(english))
        {
            return english;
        }

        // 游戏自己的词表优先（未命中时 Loc 原样返回，故比较是否变化即知有无命中）。
        var fromGame = LocaleTerms.Loc(english);
        if (!string.Equals(fromGame, english, StringComparison.Ordinal))
        {
            return fromGame;
        }

        return UseChinese && Chinese.TryGetValue(english, out var zh) ? zh : english;
    }

    // ── 带数字的文案 ────────────────────────────────────────────
    // 中英语序不同（"3 NODE(S)" vs "3 个节点"），故每种语言各留一份完整格式串，
    // 而不是拼接 T(...) 的碎片 —— 后者一旦语序不同就拼不成句。

    internal static string Running(int done, int total)
        => F("RUNNING {0}/{1}", "运行中 {0}/{1}", done, total);

    internal static string Done(int nodes)
        => F("DONE {0} NODE(S)", "完成 {0} 个节点", nodes);

    internal static string LastRun(int nodes)
        => F("LAST RUN  {0} NODE(S)", "上次运行  {0} 个节点", nodes);

    internal static string More(int nodes)
        => F("+{0} more", "还有 {0} 个", nodes);

    internal static string Ports(int opened, int total)
        => F("{0}/{1} ports", "{0}/{1} 端口", opened, total);

    /// <summary>战果行里的提权成功标记（前置两个空格作分隔）。</summary>
    internal static string AdminTag()
        => UseChinese ? "  管理员" : "  admin";

    private static string F(string english, string chinese, params object[] args)
        => string.Format(CultureInfo.InvariantCulture, UseChinese ? chinese : english, args);
}

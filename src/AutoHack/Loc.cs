namespace AutoHack;

using System.Globalization;
using System.Threading;
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

            ["connect first"] = "先连接",
            ["disconnect when done"] = "跑完断开",
            ["upload marker"] = "上传标记",
            ["wipe my traces"] = "清除我的痕迹",
            ["native exes"] = "原生演出",
            ["new IP after run"] = "跑完换 IP",

            // TOOLS
            ["TOOLS"] = "工具",
            ["SCAN NETWORK"] = "扫描网络",
            ["DEC DECRYPT"] = "解密 DEC",
            ["MEMORY DUMP"] = "内存转储",
            ["ALL PROGRAMS"] = "全部程序",
            ["MOD PROGRAMS"] = "模组程序",
            ["UNBREAKABLE"] = "不可摧毁",
            ["PULL FILES"] = "拉取文件",
            ["PURGE FILES"] = "清除文件",
            ["DROP NODE"] = "摘除节点",
            ["WIPE TRACES"] = "全网清痕",
            ["NEW IP"] = "换 IP",

            // 运行态
            ["ENGAGING"] = "执行中",
            ["TRACKERS"] = "追踪中",
            ["READY"] = "就绪",
        };

    /// <summary>
    /// <see cref="T(string)"/> 的查表结果缓存。
    ///
    /// 失效键 = <see cref="Settings.ActiveLocale"/> 与 <see cref="LocaleTerms.ActiveTerms"/>.Count 的组合：
    /// 换语言时游戏对词表 <c>Clear()</c> 后重填，两者任一变化即整体失效、丢弃全部条目。
    /// 未命中游戏词表、也未命中本表的输入同样入缓存（英文原文映射到英文原文），故每帧约 20 次
    /// 的面板查表在首帧后都是纯字典命中，不再进入 <see cref="LocaleTerms.Loc"/> 的 ContainsKey + 索引器双查。
    ///
    /// 实例发布后不可变：<see cref="Entries"/> 只读，更新一律新建实例并用 <see cref="Volatile"/>
    /// 整体替换引用 —— 这样即便有并发调用也不会读到撕裂对象。
    /// （<see cref="T(string)"/> 的实际调用点全在绘制路径上，都在游戏线程；
    /// 用 Volatile 是廉价保险，不是必需。）
    /// </summary>
    private sealed class Cache
    {
        /// <summary>建立本快照时的 <see cref="Settings.ActiveLocale"/>。</summary>
        internal readonly string Locale;

        /// <summary>建立本快照时的 <see cref="LocaleTerms.ActiveTerms"/> 条目数。</summary>
        internal readonly int TermCount;

        /// <summary>英文原文 → 已解析文案。发布后不再写入。</summary>
        internal readonly Dictionary<string, string> Entries;

        internal Cache(string locale, int termCount)
        {
            Locale = locale;
            TermCount = termCount;
            Entries = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <summary>当前游戏状态是否仍与本快照的失效键一致。</summary>
        internal bool IsFresh
            => TermCount == LocaleTerms.ActiveTerms.Count
               && string.Equals(Locale, Settings.ActiveLocale, StringComparison.Ordinal);
    }

    /// <summary>当前生效的缓存快照；<c>null</c> 表示尚未建立。整体替换，绝不原地改。</summary>
    private static Cache _cache;

    /// <summary>
    /// 查一条静态文案：缓存 → 游戏词表 → 本表 → 英文原文。
    ///
    /// <b>游戏词表命中的那一条不进缓存</b>。原因：本缓存的失效键是
    /// <see cref="Settings.ActiveLocale"/> + <c>ActiveTerms.Count</c>，而 workshop mod
    /// （如 ZeroDayToolKit）会写 <c>ActiveTerms[已有键] = 新值</c> —— 原地覆盖，Count 不变，
    /// 失效键察觉不到，缓存会把旧译文一直发下去。
    /// 游戏词表<b>未</b>命中时结果只由本类的静态 <see cref="Chinese"/> 表与英文原文决定，
    /// 运行期恒定，缓存它没有这个风险 —— 而面板专有词（RUN / SCOPE / PORT INTERVAL…）
    /// 恰好都落在这一支，故缓存收益基本不受影响。
    /// </summary>
    internal static string T(string english)
    {
        if (string.IsNullOrEmpty(english))
        {
            return english;
        }

        var cache = Volatile.Read(ref _cache);
        if (cache != null && cache.IsFresh && cache.Entries.TryGetValue(english, out var cached))
        {
            return cached;
        }

        // 游戏自己的词表优先（未命中时 Loc 原样返回，故比较是否变化即知有无命中）。
        var fromGame = LocaleTerms.Loc(english);
        if (!string.Equals(fromGame, english, StringComparison.Ordinal))
        {
            return fromGame;
        }

        var resolved = UseChinese && Chinese.TryGetValue(english, out var zh) ? zh : english;
        Volatile.Write(ref _cache, Next(cache, english, resolved));
        return resolved;
    }

    /// <summary>
    /// 生成下一份快照：失效键变了就丢弃全部条目重建，否则复制现有条目再补一条（写时复制，
    /// 避免并发写已发布的字典）。两个线程同时补不同条目时可能基于同一旧快照各复制一份，
    /// 后发布者覆盖先发布者 —— 被覆盖的条目下次查询重新解析即可，不影响正确性。
    /// </summary>
    private static Cache Next(Cache current, string english, string resolved)
    {
        if (current == null || !current.IsFresh)
        {
            var rebuilt = new Cache(Settings.ActiveLocale, LocaleTerms.ActiveTerms.Count);
            rebuilt.Entries[english] = resolved;
            return rebuilt;
        }

        var next = new Cache(current.Locale, current.TermCount);
        foreach (var pair in current.Entries)
        {
            next.Entries[pair.Key] = pair.Value;
        }

        next.Entries[english] = resolved;
        return next;
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

    /// <summary>按当前语言取格式串，不做替换。</summary>
    private static string Template(string english, string chinese)
        => UseChinese ? chinese : english;

    /// <summary>
    /// 单数字文案。用 <see cref="string.Replace(string, string)"/> 替掉 <c>string.Format</c>：
    /// 调用点传的都是 <c>int</c>，走 <c>params object[]</c> 会逐个装箱并多分配一个数组。
    /// 格式串里只有 <c>{0}</c>/<c>{1}</c>，替换值只含数字与负号，不会引入新的占位符。
    /// </summary>
    private static string F(string english, string chinese, int arg0)
        => Template(english, chinese).Replace("{0}", arg0.ToString(CultureInfo.InvariantCulture));

    /// <summary>双数字文案：<c>{0}</c> 先于 <c>{1}</c> 替换，数字不含占位符，故次序无歧义。</summary>
    private static string F(string english, string chinese, int arg0, int arg1)
        => Template(english, chinese)
            .Replace("{0}", arg0.ToString(CultureInfo.InvariantCulture))
            .Replace("{1}", arg1.ToString(CultureInfo.InvariantCulture));
}

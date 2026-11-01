// PanelCanvas.cs —— 自绘控件、基础绘制原语与文本度量（含测量缓存）。
//
// 由 HackPanel 拆出（v1.45.0 审计 P1-1）：这些成员只依赖 HackPanel.Palette 与 GuiData，
// 不含任何面板状态或布局决策，故能独立成一个类型；HackPanel 只留布局、状态机与动作。
namespace AutoHack;

using Hacknet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

internal static class PanelCanvas
{
    internal static bool Segment(int id, Rectangle r, bool active, string text, HackPanel.Palette c)
    {
        var hot = Track(id, r, out var clicked);

        Fill(r, active ? c.Accent : hot ? c.Raised : c.Track);
        Outline(r, active ? Lighten(c.Accent, 0.3f) : c.Faint, 1);

        var size = Measure(text, 0.9f);
        DrawText(
            text,
            r.X + (r.Width - size.X) / 2f,
            r.Y + (r.Height - size.Y) / 2f,
            active ? c.Ink : c.Dim,
            0.9f);

        return clicked;
    }

    internal static bool Glyph(int id, Rectangle r, string glyph, Color tint, HackPanel.Palette c)
    {
        var hot = Track(id, r, out var clicked);

        if (hot)
        {
            Fill(r, c.Raised);
        }

        Outline(r, hot ? tint : c.Faint, 1);

        var size = Measure(glyph, 1f);
        DrawText(glyph, r.X + (r.Width - size.X) / 2f, r.Y + (r.Height - size.Y) / 2f, hot ? Color.White : tint, 1f);
        return clicked;
    }

    internal static bool Check(int id, int x, int y, int labelWidth, bool on, string label, HackPanel.Palette c)
    {
        // 命中区包含标签，整行可点。
        var hot = Track(id, new Rectangle(x, y, labelWidth, 18), out var clicked);

        if (clicked)
        {
            on = !on;
        }

        var box = new Rectangle(x, y, 16, 16);
        Fill(box, hot ? Lighten(on ? c.Accent : c.Track, 0.08f) : on ? c.Accent : c.Track);
        Outline(box, on ? Lighten(c.Accent, 0.3f) : c.Faint, 1);
        if (on)
        {
            Fill(new Rectangle(x + 5, y + 5, 6, 6), c.Ink);
        }

        // 标签始终绘制 —— 原生 CheckBox 只在悬停时才画，这正是旧面板显脏的主因。
        DrawText(Ellipsize(label, labelWidth - 24), x + 24, y + 1, on ? c.Text : c.Dim, 1f);
        return on;
    }

    internal static bool PrimaryButton(int id, Rectangle r, string text, Color accent, Color ink)
    {
        var hot = Track(id, r, out var clicked);
        var down = hot && GuiData.active == id && GuiData.mouse.LeftButton == ButtonState.Pressed;

        Fill(r, down ? Darken(accent, 0.25f) : hot ? Lighten(accent, 0.18f) : accent);
        Outline(r, Lighten(accent, 0.35f), 1);

        var size = Measure(text, 1.1f);
        DrawText(text, r.X + (r.Width - size.X) / 2f, r.Y + (r.Height - size.Y) / 2f, ink, 1.1f);

        return clicked;
    }

    /// <summary>
    /// 把控件登记进游戏的 hot/active 状态机，返回「本帧在其上按下并抬起」。
    /// 语义照抄 <c>Button.doButton</c>（Hacknet.Gui/Button.cs:32-70）：
    /// 命中置 hot，按下时若 active 空闲则抢占，在 active 上抬起才算点击。
    /// </summary>
    internal static bool Track(int id, Rectangle r, out bool clicked)
    {
        var hot = r.Contains(GuiData.getMousePoint());

        if (hot)
        {
            GuiData.hot = id;

            if (GuiData.mouseWasPressed() && GuiData.active == -1)
            {
                GuiData.active = id;
            }
        }
        else if (GuiData.hot == id)
        {
            GuiData.hot = -1;
        }

        clicked = GuiData.active == id && GuiData.mouseLeftUp();
        if (clicked)
        {
            GuiData.active = -1;
        }

        return hot;
    }

    internal static void Fill(Rectangle rect, Color color)
        => GuiData.spriteBatch.Draw(Hacknet.Utils.white, rect, color);

    internal static void Outline(Rectangle rect, Color color, int thickness)
        => Hacknet.Gui.RenderedRectangle.doRectangleOutline(
            rect.X, rect.Y, rect.Width, rect.Height, thickness, color);

    internal static void Section(string text, int x, int y, HackPanel.Palette c)
        => DrawText(text, x, y, c.Dim, 0.9f);

    internal static void Progress(Rectangle r, float ratio, Color fill, HackPanel.Palette c)
    {
        Fill(r, c.Track);
        Fill(new Rectangle(r.X, r.Y, (int)(r.Width * ratio), r.Height), fill);

        // 十等分刻度线，便于读进度。
        for (var i = 1; i < 10; i++)
        {
            Fill(new Rectangle(r.X + r.Width * i / 10, r.Y, 1, r.Height), new Color(0, 0, 0, 90));
        }

        Outline(r, c.Edge, 1);
    }

    internal static void DrawText(string text, float x, float y, Color color, float scale)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // 统一用 smallfont（Font12/14/16）加显式缩放，得到 0.9/1.0/1.1/1.3 四级阶梯。
        // 原生控件用 tinyfont（Font10/12/14）自动缩放，字号不可控。
        // 注意不可用 GuiData.UISmallfont / UITinyfont —— 这两个字段从未被赋值。
        GuiData.spriteBatch.DrawString(
            GuiData.smallfont,
            text,
            Hacknet.Utils.ClipVec2ForTextRendering(new Vector2(x, y)),
            color,
            0f,
            Vector2.Zero,
            scale,
            SpriteEffects.None,
            0.5f);
    }

    internal static Vector2 Measure(string text, float scale)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Vector2.Zero;
        }

        var font = GuiData.smallfont;

        // 字体引用变了（locale / 字体重载）→ 旧度量作废，整体丢弃。
        if (!ReferenceEquals(font, _measureCacheFont))
        {
            ResetMeasureCache(font);
        }

        // 先取快照：清空是换引用，快照要么是旧的完整字典、要么是新的空字典，两者都可用。
        var cache = _measureCache;
        var key = new MeasureKey(text, scale);

        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        // 未命中才真正测量 —— 这是本方法唯一还会走 SpriteFont.MeasureString 的分支。
        var size = font.MeasureString(text) * scale;

        if (cache.Count >= MeasureCacheCapacity)
        {
            cache = ResetMeasureCache(font);
        }

        cache[key] = size;
        return size;
    }

    /// <summary>
    /// 换一张新字典并登记当前字体。用新实例而非 <c>Clear()</c>：容量超限时整体作废最省事，
    /// 也让旧字典连同它的全部条目一起被 GC 掉，不必逐条移除。
    /// （调用方在游戏线程，无并发读者，故不需要为「换引用」附加线程安全语义。）
    /// </summary>
    internal static Dictionary<MeasureKey, Vector2> ResetMeasureCache(SpriteFont font)
    {
        var fresh = new Dictionary<MeasureKey, Vector2>(MeasureCacheCapacity);
        _measureCacheFont = font;
        _measureCache = fresh;
        return fresh;
    }

    /// <summary>
    /// 超宽则截断并加省略号。
    ///
    /// 全串平均字宽的比例估算 + 常数步修正，而非逐字符重测：后者每帧要为每个标签
    /// 调用 O(长度) 次 <c>MeasureString</c>，而这是每帧都在跑的绘制路径。
    /// 修正循环保证结果与逐字符法完全一致。
    ///
    /// 修正循环里的 <see cref="Measure"/> 走同帧测量缓存：同一个标签每帧截在同一位置，
    /// 故除首帧外这些探测基本都是字典命中，不再触碰 <c>MeasureString</c>。
    /// </summary>
    internal static string Ellipsize(string value, float maxWidth)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var full = Measure(value, 1f).X;
        if (full <= maxWidth)
        {
            return value;
        }

        // 比例估算落点，再向两侧微调到位。
        var cut = (int)(value.Length * maxWidth / full);
        cut = ClampInt(cut, 1, value.Length - 1);

        while (cut > 1 && Measure(Mid(value, cut), 1f).X > maxWidth)
        {
            cut--;
        }

        while (cut < value.Length - 1 && Measure(Mid(value, cut + 1), 1f).X <= maxWidth)
        {
            cut++;
        }

        return Mid(value, cut);
    }

    /// <summary>取前 <paramref name="count"/> 个字符并追加省略号（<b>一次</b>分配）。</summary>
    internal static string Mid(string value, int count)
    {
        // 原实现 Substring + 拼接 = 两次分配（中间串随后即弃）。net472 没有 string.Create，
        // 也没有 Concat(ReadOnlySpan, ReadOnlySpan)，故借线程本地缓冲拼好后只 new 一次。
        var buffer = _midBuffer;
        if (buffer == null || buffer.Length < count + 2)
        {
            buffer = new char[Math.Max(count + 2, 64)];
            _midBuffer = buffer;
        }

        value.CopyTo(0, buffer, 0, count);
        buffer[count] = '.';
        buffer[count + 1] = '.';
        return new string(buffer, 0, count + 2);
    }

    internal static Color Lighten(Color color, float amount)
        => Color.Lerp(color, Color.White, amount);

    internal static Color Darken(Color color, float amount)
        => Color.Lerp(color, Color.Black, amount);

    internal static int ClampInt(int value, int min, int max)
        => value < min ? min : value > max ? max : value;

    /// <summary>
    /// 测量缓存的容量上限，超过即整体清空。
    ///
    /// 必须设上限：面板上的标签几乎全是常量串（跨帧重复测量同样的串纯属浪费，正是本缓存要
    /// 解决的），但运行期还有不断变化的串 —— 进度 "3 / 128"、当前目标名、战果行 —— 它们
    /// 每帧都产生新键，不设上限字典会随运行时间无界增长。选择整体清空而不是逐条淘汰：
    /// 绘制路径上不能维护链表或做遍历，丢弃的代价只是下一帧重测少量串。
    /// </summary>
    internal const int MeasureCacheCapacity = 256;

    /// <summary>
    /// 同帧内有效的测量缓存：键 = (文本, 缩放)，值 = 已乘缩放的尺寸。
    ///
    /// <b>失效条件</b>：<see cref="GuiData.smallfont"/> 的<b>引用</b>变化即整体换一张新字典。
    /// locale 切换、字体/分辨率重载都会换掉这个 SpriteFont 实例，旧度量随之作废；用引用比较
    /// 而不是比较字体属性，是因为前者 O(1) 且不会漏掉任何换字体的路径。
    ///
    /// <b>线程模型：仅游戏线程</b>。本类的唯一入口是 <c>HackOverlay</c> 的 <c>OS.Draw</c> Postfix，
    /// 故读写都在游戏线程、无并发。注释里刻意不宣称线程安全：未命中路径是
    /// <c>cache[key] = size</c> 的<b>原地写入</b>，并非「整体换引用」，宣称并发安全会误导后人。
    /// （<see cref="ResetMeasureCache"/> 换引用只是为了在容量超限/换字体时让旧字典整体作废，
    /// 不是为了并发。）
    /// </summary>
    internal static Dictionary<MeasureKey, Vector2> _measureCache = new Dictionary<MeasureKey, Vector2>(MeasureCacheCapacity);

    /// <summary>建立当前缓存时所用的字体，用于检测 <see cref="GuiData.smallfont"/> 是否换了实例。</summary>
    internal static SpriteFont _measureCacheFont;

    /// <summary>
    /// 测量缓存的键：文本 + 缩放。
    ///
    /// 必须实现 <see cref="IEquatable{T}"/>：不实现的话 <see cref="Dictionary{TKey, TValue}"/>
    /// 会退回 <see cref="ValueType.Equals(object)"/> —— 反射比较并给每个字段装箱，在每帧的绘制
    /// 路径上比重新测量还贵。
    /// </summary>
    internal readonly struct MeasureKey : IEquatable<MeasureKey>
    {
        private readonly string _text;
        private readonly float _scale;

        internal MeasureKey(string text, float scale)
        {
            _text = text;
            _scale = scale;
        }

        public bool Equals(MeasureKey other)
            => string.Equals(_text, other._text, StringComparison.Ordinal) && _scale == other._scale;

        public override bool Equals(object obj) => obj is MeasureKey other && Equals(other);

        public override int GetHashCode()
        {
            // net472 没有 System.HashCode，手工组合两个字段的哈希。
            unchecked
            {
                return (_text.GetHashCode() * 397) ^ _scale.GetHashCode();
            }
        }
    }

    [ThreadStatic]
    internal static char[] _midBuffer;
}
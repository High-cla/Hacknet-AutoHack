namespace AutoHack;

using System.Globalization;
using Hacknet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

/// <summary>
/// 面板的交互状态（用户可编辑的选项）。纯数据，不含绘制逻辑。
/// </summary>
internal sealed class HackPanelState
{
    private static int _nextId = 7000;

    /// <summary>控件 ID 基址；多个面板实例共存时不串扰。</summary>
    internal readonly int IdBase = _nextId += 64;

    internal bool Open { get; set; } = true;

    /// <summary>收起为一条状态栏，避免长期遮挡终端。</summary>
    internal bool Collapsed { get; set; }

    /// <summary>面板左上角；<see cref="int.MinValue"/> = 尚未拖动过，按屏幕右下角自动定位。</summary>
    internal int X { get; set; } = int.MinValue;

    internal int Y { get; set; } = int.MinValue;

    internal HackScope Scope { get; set; } = HackScope.Network;

    /// <summary>端口间隔，秒。</summary>
    internal float PortDelay { get; set; } = HackOptions.DefaultPortDelay;

    internal bool ClearLogs { get; set; } = true;

    /// <summary>是否上传 ~/autohack.txt 标记（缺省关）。</summary>
    internal bool UploadMarker { get; set; } = false;

    /// <summary>每个目标先 connect 再动手（原生 probe/upload 都要求已连接）。</summary>
    internal bool ConnectFirst { get; set; } = true;

    /// <summary>每个目标跑完就 dc：追踪只在连着目标时推进，断开即中止。</summary>
    internal bool Disconnect { get; set; } = true;

    /// <summary>全网扫描时跳过已拿下的机器（肉鸡），不重复入侵。</summary>
    internal bool SkipOwned { get; set; } = true;

    /// <summary>全网扫描口径：true = 地图全表（含不在连线上的机器），缺省 false = 沿连线广度优先。</summary>
    internal bool AllNodes { get; set; } = false;

    /// <summary>用已知账密登入（成功即提权，跳过全部破端口）。</summary>
    internal bool UseCredentials { get; set; } = true;

    /// <summary>推进节奏档位。缺省 Normal = 与旧版行为一致，快档需显式选。</summary>
    internal HackSpeed Speed { get; set; } = HackSpeed.Normal;

    internal HackOptions ToOptions() => new(
        Scope,
        Array.Empty<string>(),
        PortDelay,
        ClearLogs,
        UploadMarker,
        ConnectFirst,
        Disconnect,
        SkipOwned,
        AllNodes,
        UseCredentials,
        Speed);
}

/// <summary>
/// 自绘的 AutoHack 控制面板。
///
/// 不使用 <c>Button</c>/<c>CheckBox</c>/<c>SliderBar</c> 这类原生控件，原因：
/// 1. <c>CheckBox.doCheckBox(id,x,y,on,color,text)</c> 只在 <c>GuiData.hot == id</c>
///    时才画文字，且画在方框上方 20px —— 标签平时不可见，正是旧面板显脏的主因。
/// 2. 原生控件用 <c>tinyfont</c>（Font10）自动缩放文字，字号/间距不受控。
/// 3. 原生 Button 在宽度 &gt; 65 时会额外画一条 13px 颜色标签条，与紧凑面板不搭。
///
/// 配色取自 <see cref="OS"/> 的当前主题（<c>highlightColor</c> /
/// <c>terminalTextColor</c>），换主题时面板跟随，避免与游戏自身 UI 撞色。
/// 所有控件直接写 <c>GuiData.spriteBatch</c>，必须在一帧已 Begin 的绘制中调用。
/// </summary>
internal static class HackPanel
{
    internal const int Width = 396;
    internal const int HeaderHeight = 34;
    internal const int CollapsedHeight = 30;

    private const int Padding = 12;
    private const int ContentWidth = Width - Padding * 2;

    /// <summary>两列控件的单列宽（含 8px 列间距）。</summary>
    private const int ColumnWidth = (ContentWidth - 8) / 2;

    private const int SectionHeight = 20;
    private const int SegmentHeight = 26;
    private const int SliderHeight = 18;
    private const int CheckRowHeight = 22;
    private const int ButtonHeight = 34;
    private const int OutcomeRowHeight = 16;

    /// <summary>战果区最多展示的行数，超出部分折叠成 "+N more"。</summary>
    private const int MaxOutcomeRows = 5;
    private const int Gap = 14;
    private const int BottomPadding = 12;
    private const int TopPadding = 10;

    /// <summary>运行视图的行高：阶段行 / 进度条 / 当前目标行。</summary>
    private const int PhaseRowHeight = 24;
    private const int ProgressRowHeight = 18;
    private const int CurrentRowHeight = 26;

    /// <summary>选项块从正文起点到 RUN 按钮顶部的总高。必须与 <see cref="DrawOptions"/> 的推进量一致。</summary>
    private const int OptionsBlockHeight =
        SectionHeight + SegmentHeight + Gap
        + SectionHeight + SliderHeight + Gap
        + SectionHeight + SegmentHeight + Gap
        + CheckRowHeight * 4 + Gap;

    /// <summary>本帧面板占据的矩形。供 Update 阶段提前阻断下层控件点击。</summary>
    internal static Rectangle LastFrame { get; private set; }

    /// <summary>该帧用户点下的动作。</summary>
    internal enum PanelAction
    {
        None,
        Start,
        Close,
    }

    internal static PanelAction Draw(HackPanelState state, HackRun run, OS os, Rectangle screen)
    {
        var c = new Palette(os);
        var action = PanelAction.None;

        var height = state.Collapsed ? CollapsedHeight : HeaderHeight + BodyHeight(run);
        var (px, py) = ResolveOrigin(state, screen, height);

        LastFrame = new Rectangle(px, py, Width, height);
        Chrome(LastFrame, c);
        Drag(state, screen);

        var left = px + Padding;
        var right = px + Width - Padding;

        // ── 标题栏 ──────────────────────────────────────────────
        var status = Status(run, c);
        Fill(new Rectangle(px + 12, py + 15, 6, 6), status.Color);
        DrawText("AUTOHACK", px + 24, py + 9, c.Text, 1.3f);

        if (state.Collapsed)
        {
            DrawText(status.Text, px + 134, py + 10, status.Color, 0.9f);

            if (Glyph(state.IdBase + 1, new Rectangle(right - 44, py + 5, 20, 20), "v", c.Accent, c))
            {
                state.Collapsed = false;
            }

            if (Glyph(state.IdBase + 2, new Rectangle(right - 20, py + 5, 20, 20), "x", c.Bad, c))
            {
                action = PanelAction.Close;
            }

            return action;
        }

        DrawText(status.Text, right - 64, py + 10, status.Color, 0.9f);

        if (Glyph(state.IdBase + 1, new Rectangle(right - 20, py + 7, 20, 20), "-", c.Dim, c))
        {
            state.Collapsed = true;
        }

        if (Glyph(state.IdBase + 2, new Rectangle(right - 44, py + 7, 20, 20), "x", c.Bad, c))
        {
            action = PanelAction.Close;
        }

        // ── 正文 ────────────────────────────────────────────────
        var y = py + HeaderHeight + TopPadding;

        if (run is { Finished: false })
        {
            DrawRunning(run, left, y, c);
            return action;
        }

        DrawOptions(state, left, y, c, out y);

        if (run is { Finished: true })
        {
            if (DrawResult(state, run, left, y, c, out _))
            {
                action = PanelAction.Start;
            }
        }
        else if (PrimaryButton(state.IdBase + 6, new Rectangle(left, y, ContentWidth, ButtonHeight), "RUN", c.Accent, c.Ink))
        {
            action = PanelAction.Start;
        }

        return action;
    }

    // ── 正文区块 ────────────────────────────────────────────────

    private static void DrawOptions(HackPanelState state, int left, int y, Palette c, out int next)
    {
        var half = ContentWidth / 2;

        Section("SCOPE", left, y, c);
        y += SectionHeight;

        if (Segment(state.IdBase + 10, new Rectangle(left, y, half - 2, SegmentHeight),
                state.Scope == HackScope.Network, "NETWORK SWEEP", c))
        {
            state.Scope = HackScope.Network;
        }

        if (Segment(state.IdBase + 11, new Rectangle(left + half + 2, y, ContentWidth - half - 2, SegmentHeight),
                state.Scope == HackScope.Connected, "CURRENT NODE", c))
        {
            state.Scope = HackScope.Connected;
        }

        y += SegmentHeight + Gap;

        Section("PORT INTERVAL", left, y, c);
        var value = state.PortDelay.ToString("0.00", CultureInfo.InvariantCulture) + " s";
        DrawText(value, left + ContentWidth - Measure(value, 1f).X, y, state.PortDelay <= 0.15f ? c.Warn : c.Text, 1f);
        y += SectionHeight;

        state.PortDelay = Slider(
            state.IdBase + 12, left, y, ContentWidth,
            state.PortDelay, HackOptions.MinPortDelay, HackOptions.MaxPortDelay, 0.05f, c);

        y += SliderHeight + Gap;

        Section("SPEED", left, y, c);
        var speedHint = state.Speed switch
        {
            HackSpeed.Instant => "same frame",
            HackSpeed.Fast => "fast",
            _ => "normal",
        };
        DrawText(speedHint, left + ContentWidth - Measure(speedHint, 0.9f).X, y, c.Dim, 0.9f);
        y += SectionHeight;

        // 三档：Normal 保留真人节奏，Fast 压缩非端口步，Instant 把非端口步合并到同帧。
        var third = ContentWidth / 3;
        if (Segment(state.IdBase + 13, new Rectangle(left, y, third - 2, SegmentHeight),
                state.Speed == HackSpeed.Normal, "NORMAL", c))
        {
            state.Speed = HackSpeed.Normal;
        }

        if (Segment(state.IdBase + 14, new Rectangle(left + third, y, third - 2, SegmentHeight),
                state.Speed == HackSpeed.Fast, "FAST", c))
        {
            state.Speed = HackSpeed.Fast;
        }

        if (Segment(state.IdBase + 15, new Rectangle(left + third * 2, y, ContentWidth - third * 2, SegmentHeight),
                state.Speed == HackSpeed.Instant, "INSTANT", c))
        {
            state.Speed = HackSpeed.Instant;
        }

        y += SegmentHeight + Gap;

        state.UseCredentials = Check(state.IdBase + 16, left, y, ColumnWidth, state.UseCredentials, "use known creds", c);
        state.AllNodes = Check(state.IdBase + 17, left + ColumnWidth + 8, y, ColumnWidth, state.AllNodes, "whole map", c);
        y += CheckRowHeight;

        state.SkipOwned = Check(state.IdBase + 18, left, y, ColumnWidth, state.SkipOwned, "skip owned", c);
        state.ClearLogs = Check(state.IdBase + 19, left + ColumnWidth + 8, y, ColumnWidth, state.ClearLogs, "wipe logs", c);
        y += CheckRowHeight;

        state.ConnectFirst = Check(state.IdBase + 20, left, y, ColumnWidth, state.ConnectFirst, "connect first", c);
        state.Disconnect = Check(state.IdBase + 21, left + ColumnWidth + 8, y, ColumnWidth, state.Disconnect, "anti-trace dc", c);
        y += CheckRowHeight;

        state.UploadMarker = Check(state.IdBase + 22, left, y, ColumnWidth, state.UploadMarker, "upload marker", c);
        y += CheckRowHeight;

        next = y + Gap;
    }

    private static void DrawRunning(HackRun run, int left, int y, Palette c)
    {
        var right = left + ContentWidth;
        var ratio = run.Total == 0 ? 0f : Math.Min(1f, run.Done / (float)run.Total);

        DrawText(run.Phase ?? "ENGAGING", left, y, c.Accent, 1f);
        var percent = (int)(ratio * 100) + "%";
        DrawText(percent, right - Measure(percent, 1f).X, y, c.Text, 1f);
        y += PhaseRowHeight;

        Progress(new Rectangle(left, y, ContentWidth, 10), ratio, c.Ok, c);
        y += ProgressRowHeight;

        DrawText(Ellipsize(run.Current, ContentWidth - 76), left, y, c.Text, 1f);
        var count = run.Done + " / " + run.Total;
        DrawText(count, right - Measure(count, 1f).X, y, c.Dim, 1f);
        y += CurrentRowHeight;

        for (var i = 0; i < Math.Min(run.Outcomes.Count, MaxOutcomeRows); i++)
        {
            Outcome(run.Outcomes[i], left, right, y, c);
            y += OutcomeRowHeight;
        }
    }

    private static bool DrawResult(HackPanelState state, HackRun run, int left, int y, Palette c, out int next)
    {
        var right = left + ContentWidth;

        Section($"LAST RUN  {run.Outcomes.Count} NODE(S)", left, y, c);
        y += SectionHeight;

        for (var i = 0; i < Math.Min(run.Outcomes.Count, MaxOutcomeRows); i++)
        {
            Outcome(run.Outcomes[i], left, right, y, c);
            y += OutcomeRowHeight;
        }

        if (run.Outcomes.Count > MaxOutcomeRows)
        {
            DrawText($"+{run.Outcomes.Count - MaxOutcomeRows} more", left, y, c.Dim, 1f);
            y += OutcomeRowHeight;
        }

        next = y + Gap;
        return PrimaryButton(state.IdBase + 6, new Rectangle(left, next, ContentWidth, ButtonHeight), "RUN AGAIN", c.Accent, c.Ink);
    }

    private static void Outcome(TargetOutcome outcome, int left, int right, int y, Palette c)
    {
        DrawText(Ellipsize(outcome.Name, ContentWidth - 110), left, y, outcome.Escalated ? c.Ok : c.Warn, 1f);

        var stats = $"{outcome.Opened}/{outcome.Total} ports" + (outcome.Escalated ? "  admin" : "");
        DrawText(stats, right - Measure(stats, 1f).X, y, outcome.Escalated ? c.Ok : c.Dim, 1f);
    }

    // ── 自绘控件 ────────────────────────────────────────────────

    private static bool Segment(int id, Rectangle r, bool active, string text, Palette c)
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

    private static bool Glyph(int id, Rectangle r, string glyph, Color tint, Palette c)
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

    private static bool Check(int id, int x, int y, int labelWidth, bool on, string label, Palette c)
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

    /// <summary>
    /// 自维护状态机的滑条。不借用 <see cref="Track"/>：拖拽中 <c>GuiData.active == id</c>，
    /// 而 <c>Track</c> 在抬起那一帧会先把 active 复位，导致最后一段位移丢失。
    /// 此处先落值再复位，保证松手位置被采纳。
    /// </summary>
    private static float Slider(
        int id, int x, int y, int width, float value, float min, float max, float step, Palette c)
    {
        var hot = new Rectangle(x, y - 4, width, SliderHeight + 8).Contains(GuiData.getMousePoint());

        if (hot)
        {
            GuiData.hot = id;

            if (GuiData.mouseWasPressed() && GuiData.active == -1)
            {
                GuiData.active = id;
            }

            if (GuiData.active == -1)
            {
                var scroll = GuiData.getMouseWheelScroll();
                if (scroll != 0)
                {
                    value += step * scroll;
                }
            }
        }
        else if (GuiData.hot == id)
        {
            GuiData.hot = -1;
        }

        if (GuiData.active == id)
        {
            var t = Clamp((GuiData.getMousePoint().X - x) / (float)width, 0f, 1f);
            value = min + t * (max - min);

            if (GuiData.mouse.LeftButton == ButtonState.Released)
            {
                GuiData.active = -1;
            }
        }

        value = Clamp(value, min, max);

        var ratio = max - min <= 0f ? 0f : (value - min) / (max - min);
        Fill(new Rectangle(x, y + 5, width, 6), c.Track);
        Fill(new Rectangle(x, y + 5, (int)(width * ratio), 6), c.Accent);

        var knob = x + (int)(width * ratio);
        Fill(new Rectangle(knob - 2, y, 4, 16), hot || GuiData.active == id ? Color.White : c.Accent);

        return value;
    }

    private static bool PrimaryButton(int id, Rectangle r, string text, Color accent, Color ink)
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
    private static bool Track(int id, Rectangle r, out bool clicked)
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

    // ── 框架与外框 ──────────────────────────────────────────────

    private static void Chrome(Rectangle frame, Palette c)
    {
        Fill(frame, c.Panel);
        Outline(frame, c.Edge, 1);

        Fill(new Rectangle(frame.X + 1, frame.Y + 1, frame.Width - 2, HeaderHeight - 1), c.Header);
        Fill(new Rectangle(frame.X + 1, frame.Y + 1, 3, HeaderHeight - 1), c.Accent);
    }

    private static Point _dragOffset;

    /// <summary>标题栏拖动；命中测试与其它控件一致，都用 <see cref="GuiData.getMousePoint"/>。</summary>
    private static void Drag(HackPanelState state, Rectangle screen)
    {
        const int DragId = 7099;
        var mp = GuiData.getMousePoint();
        var header = new Rectangle(LastFrame.X, LastFrame.Y, LastFrame.Width, HeaderHeight);

        if (GuiData.active == DragId)
        {
            if (GuiData.mouse.LeftButton == ButtonState.Released)
            {
                GuiData.active = -1;
            }
            else
            {
                state.X = ClampInt(mp.X - _dragOffset.X, screen.Left + 4, MaxX(screen));
                state.Y = ClampInt(mp.Y - _dragOffset.Y, screen.Top + 4, MaxY(screen, LastFrame.Height));
                GuiData.blockingInput = true;
            }
        }
        else if (header.Contains(mp) && GuiData.mouseWasPressed() && GuiData.hot == -1)
        {
            // hot == -1 保证标题栏上的收起/关闭按钮优先，不会误触发拖动。
            GuiData.active = DragId;
            _dragOffset = new Point(mp.X - LastFrame.X, mp.Y - LastFrame.Y);
        }
    }

    private static (int X, int Y) ResolveOrigin(HackPanelState state, Rectangle screen, int height)
    {
        var x = state.X == int.MinValue ? screen.Right - Width - 18 : state.X;
        var y = state.Y == int.MinValue ? screen.Bottom - height - 18 : state.Y;

        return (ClampInt(x, screen.Left + 4, MaxX(screen)), ClampInt(y, screen.Top + 4, MaxY(screen, height)));
    }

    private static int MaxX(Rectangle screen)
        => Math.Max(screen.Left + 4, screen.Right - Width - 4);

    /// <summary>纵向上限按面板当前高度算：展开态比收起条高得多，用收起高度会让它坠出屏幕。</summary>
    private static int MaxY(Rectangle screen, int height)
        => Math.Max(screen.Top + 4, screen.Bottom - height - 4);

    // ── 尺寸 ────────────────────────────────────────────────────

    /// <summary>正文高度。必须与各 Draw* 方法的推进量一致，否则按钮会画到框外。</summary>
    private static int BodyHeight(HackRun run) => run switch
    {
        { Finished: false } => TopPadding + PhaseRowHeight + ProgressRowHeight + CurrentRowHeight
                               + Math.Min(run.Outcomes.Count, MaxOutcomeRows) * OutcomeRowHeight + BottomPadding,

        { Finished: true } => TopPadding + OptionsBlockHeight + SectionHeight
                              + Math.Min(run.Outcomes.Count, MaxOutcomeRows) * OutcomeRowHeight
                              + (run.Outcomes.Count > MaxOutcomeRows ? OutcomeRowHeight : 0)
                              + Gap + ButtonHeight + BottomPadding,

        _ => TopPadding + OptionsBlockHeight + ButtonHeight + BottomPadding,
    };

    private static (string Text, Color Color) Status(HackRun run, Palette c) => run switch
    {
        { Finished: false } => ($"RUNNING {run.Done}/{run.Total}", c.Ok),
        { Finished: true } => ($"DONE {run.Outcomes.Count} NODE(S)", c.Warn),
        _ => ("READY", c.Dim),
    };

    // ── 基础绘制 ────────────────────────────────────────────────

    private static void Fill(Rectangle rect, Color color)
        => GuiData.spriteBatch.Draw(Hacknet.Utils.white, rect, color);

    private static void Outline(Rectangle rect, Color color, int thickness)
        => Hacknet.Gui.RenderedRectangle.doRectangleOutline(
            rect.X, rect.Y, rect.Width, rect.Height, thickness, color);

    private static void Section(string text, int x, int y, Palette c)
        => DrawText(text, x, y, c.Dim, 0.9f);

    private static void Progress(Rectangle r, float ratio, Color fill, Palette c)
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

    private static void DrawText(string text, float x, float y, Color color, float scale)
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

    private static Vector2 Measure(string text, float scale)
        => string.IsNullOrEmpty(text) ? Vector2.Zero : GuiData.smallfont.MeasureString(text) * scale;

    /// <summary>
    /// 超宽则截断并加省略号。
    ///
    /// 首字符宽度的比例估算 + 常数步修正，而非逐字符重测：后者每帧要为每个标签
    /// 调用 O(长度) 次 <c>MeasureString</c>，而这是每帧都在跑的绘制路径。
    /// 修正循环保证结果与逐字符法完全一致。
    /// </summary>
    private static string Ellipsize(string value, float maxWidth)
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

    /// <summary>取前 <paramref name="count"/> 个字符并追加省略号。</summary>
    private static string Mid(string value, int count)
        => value.Substring(0, count) + "..";

    private static Color Lighten(Color color, float amount)
        => Color.Lerp(color, Color.White, amount);

    private static Color Darken(Color color, float amount)
        => Color.Lerp(color, Color.Black, amount);

    private static float Clamp(float value, float min, float max)
        => value < min ? min : value > max ? max : value;

    private static int ClampInt(int value, int min, int max)
        => value < min ? min : value > max ? max : value;

    /// <summary>配色取自游戏当前主题，换主题时面板跟随。</summary>
    private readonly struct Palette
    {
        internal Palette(OS os)
        {
            Accent = os?.highlightColor ?? new Color(0, 139, 199);
            Text = os?.terminalTextColor ?? new Color(213, 245, 255);
            Dim = new Color(122, 134, 150);
            Faint = new Color(62, 72, 86);
            Raised = new Color(30, 38, 48);
            Ok = new Color(92, 212, 142);
            Warn = new Color(235, 175, 75);
            Bad = new Color(222, 104, 104);
            Panel = new Color(8, 10, 14, 242);
            Header = new Color(15, 21, 29);
            Edge = new Color(56, 66, 80);
            Track = new Color(26, 32, 40);
            Ink = new Color(6, 10, 14);
        }

        internal readonly Color Accent;
        internal readonly Color Text;
        internal readonly Color Dim;
        internal readonly Color Faint;
        internal readonly Color Raised;
        internal readonly Color Ok;
        internal readonly Color Warn;
        internal readonly Color Bad;
        internal readonly Color Panel;
        internal readonly Color Header;
        internal readonly Color Edge;
        internal readonly Color Track;
        internal readonly Color Ink;
    }
}

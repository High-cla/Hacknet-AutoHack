// HackPanel.Draw.cs —— 面板自身的边框与拖动（与面板状态强耦合，故留在 HackPanel）。
//
// 绘制原语与文本度量已拆到 PanelCanvas.cs（v1.45.0 审计 P1-1）。
namespace AutoHack;

using Hacknet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

internal static partial class HackPanel
{
    private static void Chrome(Rectangle frame, Palette c)
    {
        PanelCanvas.Fill(frame, c.Panel);
        PanelCanvas.Outline(frame, c.Edge, 1);

        PanelCanvas.Fill(new Rectangle(frame.X + 1, frame.Y + 1, frame.Width - 2, HeaderHeight - 1), c.Header);
        PanelCanvas.Fill(new Rectangle(frame.X + 1, frame.Y + 1, 3, HeaderHeight - 1), c.Accent);
    }

    /// <summary>标题栏拖动；命中测试与其它控件一致，都用 <see cref="GuiData.getMousePoint"/>。</summary>
    private static void Drag(HackPanelState state, Rectangle screen)
    {
        var dragId = state.DragId;
        var mp = GuiData.getMousePoint();
        var header = new Rectangle(LastFrame.X, LastFrame.Y, LastFrame.Width, HeaderHeight);

        if (GuiData.active == dragId)
        {
            if (GuiData.mouse.LeftButton == ButtonState.Released)
            {
                GuiData.active = -1;
            }
            else
            {
                state.X = PanelCanvas.ClampInt(mp.X - _dragOffset.X, screen.Left + 4, MaxX(screen));
                state.Y = PanelCanvas.ClampInt(mp.Y - _dragOffset.Y, screen.Top + 4, MaxY(screen, LastFrame.Height));
                GuiData.blockingInput = true;
            }
        }
        else if (header.Contains(mp) && GuiData.mouseWasPressed() && GuiData.hot == -1)
        {
            // hot == -1 保证标题栏上的收起/关闭按钮优先，不会误触发拖动。
            GuiData.active = dragId;
            _dragOffset = new Point(mp.X - LastFrame.X, mp.Y - LastFrame.Y);
        }
    }
}
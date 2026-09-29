namespace AutoHack;

using System;
using System.Globalization;
using Hacknet;
using Hacknet.Gui;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// 「谁在追踪我」的常驻显示 —— 游戏把 <c>OS.TrackersInProgress</c> 那批追踪
/// <b>完全没有做成可见</b>：全项目只有 5 处引用，唯一的读点是每帧递减循环
/// （OS.cs:823-839），<b>没有任何 Draw</b>。玩家唯一的感知是 10~20 秒后机器突然崩掉。
/// 本 HUD 把那张表画出来，补上这个缺口。
///
/// 位置与配色刻意对齐另一套追踪 <see cref="TraceTracker.Draw"/>（TraceTracker.cs:122-133）：
/// 同样贴屏幕左下角、同样用告警红（<c>new Color(170, 0, 0)</c>，TraceTracker.cs:37）。
/// 只是排在它<b>上方</b> —— 两套追踪可能同时存在，叠在一起会互相盖住。
///
/// 为什么必须挂在 <c>OS.Draw</c> 而不是面板里：本 HUD 回答的是「现在危不危险」，
/// 而危险恰恰发生在玩家没开面板、正在终端里操作的时候。挂在面板上等于没有。
///
/// 与 <see cref="TraceTools"/> 的分工：工具是「动手掐掉」，HUD 是「让玩家先看见」。
/// 只有 HUD 没有工具就看得见却掐不掉，只有工具没有 HUD 就想不起去掐。
/// </summary>
[HarmonyPatch]
internal static class TraceHud
{
    /// <summary>与 <c>TraceTracker.timerColor</c> 同色（TraceTracker.cs:37）。</summary>
    private static readonly Color Alert = new Color(170, 0, 0);

    /// <summary>
    /// 距屏幕底部的起点。TraceTracker 自己占住最下面约 50px（数字一行 +
    /// 其上方 25px 的 <c>TRACE :</c> 标签，TraceTracker.cs:128-131），
    /// 故本 HUD 从它上方 78px 起画，任何字号下都不重叠。
    /// </summary>
    private const int BottomOffset = 78;

    private const int LeftMargin = 10;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(OS), nameof(OS.Draw))]
    private static void OnOSDraw(OS __instance)
    {
        if (__instance == null || !__instance.IsActive)
        {
            return;
        }

        if (!TryRead(__instance, out var count, out var nearest))
        {
            return;
        }

        var spriteBatch = GuiData.spriteBatch;
        var font = GuiData.smallfont;
        if (spriteBatch?.GraphicsDevice == null || font == null)
        {
            return;
        }

        // 小数分隔符随 locale 变（zh-cn 是 "."，但 de-de 是 ","），
        // 而这是 HUD 不是本地化文案 —— 固定用不变文化，免得同一条读数的样子跟着语言漂。
        var text = Loc.T("TRACKERS") + " : " + count + "   "
                   + nearest.ToString("0.0", CultureInfo.InvariantCulture) + "s";
        var y = spriteBatch.GraphicsDevice.Viewport.Height - BottomOffset;

        // 与 HackOverlay.OnOSDraw（HackOverlay.cs:94-114）同一路数：此处上一批 spriteBatch
        // 已 End，自行 Begin/End 叠加。必须带 begun 标志 —— Begin 自身就可能抛
        // InvalidOperationException（上一批没 End），若 finally 无条件 End，就会在
        // finally 里再抛一次，异常直接从绘制帧冒出去带崩游戏。HUD 比面板更该容忍：
        // 它每帧都画，错一帧的代价是没了两行字，不是整个存档。
        var begun = false;
        try
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            begun = true;
            spriteBatch.DrawString(font, text, new Vector2(LeftMargin, y), Alert);
        }
        catch (InvalidOperationException)
        {
            return;
        }
        finally
        {
            if (begun)
            {
                spriteBatch.End();
            }
        }
    }

    /// <summary>
    /// 读一次追踪表。<c>count</c> 是台数，<c>nearest</c> 是最近的一台还剩几秒
    /// （追踪 10~20 秒后归零，故最小值就是最紧的那条命）。
    /// </summary>
    /// <returns>表非空且至少读出一条时为真。</returns>
    private static bool TryRead(OS os, out int count, out float nearest)
    {
        count = 0;
        nearest = float.MaxValue;

        var trackers = os.TrackersInProgress;
        if (trackers == null)
        {
            return false;
        }

        try
        {
            for (var i = 0; i < trackers.Count; i++)
            {
                var left = trackers[i].timeLeft;
                count++;
                if (left < nearest)
                {
                    nearest = left;
                }
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            // 工具的命令行入口跑在 OS.execute 的独立线程上（OS.cs:1754-1767），
            // 它可能刚好在本次遍历中间 Clear() 了表。丢掉这一帧即可 ——
            // 下一帧读数必自洽，绝不能为此让游戏线程崩在绘制里。
            return count > 0;
        }

        return count > 0;
    }
}

namespace AutoHack;

using BepInEx.Logging;
using Hacknet;
using Hacknet.Gui;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// 叠加层：把 AutoHack 控制面板画在游戏画面上。
///
/// 注入点选 <see cref="OS.Draw"/>，理由：
/// 1. 能直接拿到正在绘制的 OS 实例 —— <c>OS.currentInstance</c> 靠不住，它只在
///    构造函数中赋值且从不置空，主菜单里的 <c>new OS()</c> 也会改写它。
/// 2. <see cref="GameScreen.IsActive"/> 可可靠判断该画面是否真的可见（主菜单/
///    弹窗覆盖时自动隐藏面板）。
/// 3. 此处上一批 spriteBatch 已 End（Draw 内的 startDraw/endDraw 成对），
///    且 PostProcessor.end() 已把渲染目标交还后台缓冲，故自行 Begin/End 即可叠加。
///
/// Prefix 与 Postfix 是一对：
/// - Prefix 在正文绘制前把 <c>GuiData.blockingInput</c> 置真，使鼠标悬停面板时
///   游戏自身的控件（终端、模块标题栏、按钮）不再响应点击 —— 面板因此是模态的。
///   必须放在 Prefix：正文里的控件在 Draw 期间就消费输入，Postfix 已经太晚。
/// - Postfix 在正文之后自绘面板。
///
/// 不用自定义 ExeModule 的原因：exe 被限制在 RAM 面板内且占内存，
/// 叠加层则自由定位、不占 RAM、可随时开关。
/// </summary>
[HarmonyPatch]
internal static class HackOverlay
{
    private static readonly ManualLogSource Log = Logger.CreateLogSource("AutoHack");

    private static HackPanelState _state;
    private static HackRun _run;
    private static OS _os;

    /// <summary>首次成功绘制后记一条日志，作为「渲染路径真的跑通」的可核查证据。</summary>
    private static bool _drawLogged;

    internal static bool IsOpen => _state is { Open: true };

    /// <summary>面板运行是否正在推进。供命令入口做互斥。</summary>
    internal static bool IsRunning => _run is { Finished: false };

    internal static void Open(OS os)
    {
        _os = os;
        _state ??= new HackPanelState();
        _state.Open = true;
        _run = null;
    }

    internal static void Toggle(OS os)
    {
        if (IsOpen)
        {
            _state.Open = false;
            return;
        }

        Open(os);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(OS), "Draw")]
    private static void OnOSDrawPrefix(OS __instance)
    {
        if (!Visible(__instance))
        {
            return;
        }

        // 光标在面板上时才抢占输入，面板之外照常操作游戏。
        if (HackPanel.LastFrame.Contains(GuiData.getMousePoint()))
        {
            GuiData.blockingInput = true;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(OS), "Draw")]
    private static void OnOSDraw(OS __instance)
    {
        if (!Visible(__instance))
        {
            return;
        }

        var spriteBatch = GuiData.spriteBatch;
        if (spriteBatch?.GraphicsDevice == null)
        {
            return;
        }

        var viewport = spriteBatch.GraphicsDevice.Viewport;
        var screen = new Rectangle(0, 0, viewport.Width, viewport.Height);

        var begun = false;
        var action = HackPanel.PanelAction.None;
        try
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            begun = true;
            action = HackPanel.Draw(_state, _run, __instance, screen);
        }
        catch (InvalidOperationException)
        {
            // 面板绘制期的 SpriteBatch 状态异常：跳过错帧即可，
            // finally 里的 End() 保证批次不会泄漏。
            return;
        }
        finally
        {
            if (begun)
            {
                spriteBatch.End();
            }
        }

        if (!_drawLogged)
        {
            _drawLogged = true;
            Log.LogInfo($"Overlay drawing at {screen.Width}x{screen.Height}.");
        }

        switch (action)
        {
            case HackPanel.PanelAction.Start:
                Start();
                break;

            case HackPanel.PanelAction.Close:
                _state.Open = false;
                break;
        }
    }

    private static bool Visible(OS instance)
        => _state is { Open: true } && instance != null && instance == _os && instance.IsActive;

    private static void Start()
    {
        if (_os == null)
        {
            return;
        }

        // 单写者：两条运行会同时 connect/disconnect 同一个 os.connectedComp，
        // 互相把对方的目标换掉。已在跑的运行先跑完。
        if (PendingRuns.BusyFor(_os))
        {
            _os.write("[autohack] A headless run is still in progress - wait for it to finish.");
            return;
        }

        _run = new HackRun(_os, _state.ToOptions());

        if (_run.Total == 0)
        {
            _os.write("[autohack] No eligible targets - nothing to do.");
            _run = null;
            return;
        }

        var skippedCount = _run.SkippedOwned + _run.SkippedHopeless;
        var skipped = skippedCount > 0 ? $" ({skippedCount} skipped)" : string.Empty;
        _os.write($"[autohack] Engaging {_run.Targets.Count} target(s) - {_run.Total} action(s){skipped}.");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(OS), "Update")]
    private static void OnOSUpdate(OS __instance, GameTime gameTime)
    {
        if (_run is not { Finished: false } || __instance != _os)
        {
            return;
        }

        _run.Tick(__instance, (float)gameTime.ElapsedGameTime.TotalSeconds);
    }
}

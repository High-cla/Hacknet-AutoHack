namespace AutoHack;

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
    private static HackPanelState _state;
    private static HackRun _run;
    private static OS _os;

    internal static bool IsOpen => _state is { Open: true };

    /// <summary>面板运行是否正在推进。供命令入口做互斥。</summary>
    internal static bool IsRunning => _run is { Finished: false };

    internal static void Open(OS os)
    {
        // 换过 OS（回主菜单再开档）就是新的一局：重新从 cfg 读设置，
        // 免得上一局的改动把新一局带偏。同一 OS 内重复开关面板不动状态。
        // 注意：只有 Open 会创建 _state —— 面板因此保持「开局不显示」。
        if (_state == null || !ReferenceEquals(_os, os))
        {
            var fresh = new HackPanelState();
            PanelSettings.Load(fresh);
            _state = fresh;

            // 换局即清全部跨局队列（都只有这里清；此刻 _os 还是**旧**实例，正是要忘掉的那个）：
            // · 演出队列 —— 否则上一局排的动画会漏进新一局，且旧 OS 的 exe 引用
            //   会卡住等待判据（见 NativeExes.Tick）；
            // · headless 队列 —— 上一局排的运行不该在新一局执行。
            NativeExes.Reset();
            PendingRuns.Forget(_os);

            // 追踪 HUD 不在此列：它每帧现读 OS.TrackersInProgress，没有需要清的缓存。
            // 白名单拒绝记录也不在此列：它是 ConditionalWeakTable，键就是 OS，
            // 旧实例被回收时条目自动消失（见 RefusedWhitelist 的字段注释）。
        }

        _os = os;
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
    [HarmonyPatch(typeof(OS), nameof(OS.Draw))]
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

    /// <summary>
    /// <c>OS.Draw</c> 的唯一 Postfix —— 面板与追踪 HUD 都从这里分派。
    ///
    /// <b>为什么合并</b>：合并前有三个 Postfix 挂同一方法（面板、HUD、以及一个只用来
    /// 抢输入的 Prefix），各自重取 <c>GuiData.spriteBatch</c> 做同样的空判。合并后
    /// 只剩 Prefix（抢输入）+ Postfix（绘制）各一个，且绘制次序在一个地方就看得全。
    ///
    /// <b>次序是硬约束</b>：HUD 必须画在面板<b>之后</b>，否则会被面板盖住；
    /// 而 HUD 又必须在面板<b>关着时照画</b>（它答的是「现在危不危险」，而危险恰恰发生
    /// 在玩家没开面板的时候）。两条一起决定了下面这个结构：面板走自己的早返回分支，
    /// HUD 在方法末尾无条件执行 —— 而不是共用一个 <c>if (!Visible) return;</c>。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(OS), nameof(OS.Draw))]
    private static void OnOSDraw(OS __instance)
    {
        if (Visible(__instance))
        {
            DrawPanel(__instance);
        }

        // 与面板无关：自己读 OS.TrackersInProgress，不共享上面任何状态。
        TraceHud.Draw(__instance);
    }

    /// <summary>面板的绘制与按钮动作。仅在面板可见时调用。</summary>
    private static void DrawPanel(OS __instance)
    {
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

        switch (action.Kind)
        {
            case HackPanel.PanelKind.Start:
                Start();
                break;

            case HackPanel.PanelKind.Close:
                _state.Open = false;
                break;

            case HackPanel.PanelKind.Tool:
                RunTool(action.Verb);
                break;
        }
    }

    /// <summary>
    /// 执行一个工具。工具在游戏线程上同步跑完 —— 与入侵流程共用同一个
    /// os.connectedComp，故运行中一律拒绝，避免两边互相换掉对方的目标。
    /// </summary>
    private static void RunTool(string verb)
    {
        if (_os == null)
        {
            return;
        }

        if (PendingRuns.BusyFor(_os) || IsRunning)
        {
            _os.write("[autohack] " + verb + ": a run is in progress - wait for it to finish.");
            return;
        }

        // wipe 的范围随 SCOPE 段走（「当前节点」= 当前节点所在的连线分量，其余 = 地图全表），
        // 与 dec / mem 的「whole map」复选框不是一个口径 —— 那两个问的是「工具作用在
        // 哪些机器上」，而 wipe 问的是「我的痕迹铺得多远」，后者本就该跟着 SCOPE。
        var allNodes = verb == ToolDispatch.Wipe
            ? _state.Scope != HackScope.Connected
            : _state.AllNodes;

        // 护栏在 ToolDispatch.Run 内 —— 两个入口共用一份，此处不再重复包一层。
        ToolDispatch.Run(_os, verb, allNodes);
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
        if (PendingRuns.BusyFor(_os) || IsRunning)
        {
            _os.write("[autohack] A run is already in progress - wait for it to finish.");
            return;
        }

        _run = new HackRun(_os, _state.ToOptions());

        if (_run.Total == 0)
        {
            _os.write("[autohack] No eligible targets - nothing to do.");
            _os.write(HackEngine.NoTargetHint);
            _run = null;
            return;
        }

        // 这里曾有「Engaging N target(s)」汇总行，已删（用户定：终端里只要具体的
        // 命令行输出，不要战果报告）。面板运行视图本来就有进度条与计数。
    }

    /// <summary>
    /// <c>OS.Update</c> 的唯一 Postfix —— 演出泵、headless 队列、面板运行都从这里分派。
    ///
    /// <b>为什么合并</b>：合并前它与 <see cref="PendingRuns"/> 各挂一个 Postfix，两条
    /// 补丁的先后顺序由 Harmony 的装配顺序决定（不保证），而 <c>NativeExes.Tick</c>
    /// 有「一帧只挂一个动画」的约束（见该方法的注释）—— 两个补丁都调它就会同帧挂两个。
    /// 此前靠注释互相约定（「本队列的泵由 HackOverlay 独家负责」），现在靠代码：
    /// 一个补丁，次序写死在下三行里。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(OS), nameof(OS.Update))]
    private static void OnOSUpdate(OS __instance, GameTime gameTime)
    {
        // 演出队列独立于运行推进：跑完收尾后仍要把排着的动画播完。唯一调用点。
        NativeExes.Tick(__instance);

        // headless 队列（无面板的运行）。它自己按 OS 实例过滤，不读 _os / _run。
        PendingRuns.Tick(__instance, gameTime);

        if (_run is not { Finished: false } || __instance != _os)
        {
            return;
        }

        _run.Tick(__instance, (float)gameTime.ElapsedGameTime.TotalSeconds);
    }
}

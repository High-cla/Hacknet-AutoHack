namespace AutoHack;

using AutoHack.Core;

using System.Diagnostics;

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
    /// <summary>
    /// 每个 OS 一份运行时。<see cref="System.Runtime.CompilerServices.ConditionalWeakTable{TKey,TValue}"/>
    /// 的键是 OS 实例：换局后旧条目随旧 OS 一起被回收，不需要「换局时清理」这条规则。
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<OS, AutoHackRuntime> Runtimes = new();

    private static AutoHackRuntime Runtime(OS os) => Runtimes.GetValue(os, key => new AutoHackRuntime(key));

    /// <summary>指定 OS 上是否已打开面板。</summary>
    internal static bool IsOpen(OS os)
        => os != null && Runtimes.TryGetValue(os, out var runtime) && runtime.IsOpen;

    /// <summary>面板运行是否正在推进。供命令入口做互斥。</summary>
    internal static bool IsRunning(OS os) => Runtime(os).Run is { Finished: false };

    internal static void Open(OS os)
    {
        if (os == null)
        {
            return;
        }

        var runtime = Runtime(os);

        // 本局第一次打开：从 cfg 读设置，并清掉跨局队列。
        // · 演出队列是全局的（动画挂在 OS 的 exes 上，但队列本身不分局）——重置为妙，
        //   否则上一局排的动画会漏进新一局，旧 OS 的 exe 引用还会卡住等待判据；
        // · headless 队列里其它 OS 的排队同样作废：旧局已经不可能再推进。
        if (runtime.State == null)
        {
            runtime.EnsureState();
            NativeExes.Reset();
            PendingRuns.ForgetExcept(os);
        }

        runtime.Open();

        // 追踪 HUD 不在此列：它每帧现读 OS.TrackersInProgress，没有需要清的缓存。
        // 白名单拒绝记录也不在此列：它是 ConditionalWeakTable，键就是 OS。
    }

    internal static void Toggle(OS os)
    {
        if (IsOpen(os))
        {
            Runtime(os).Close();
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
        // <b>异常边界画在整条绘制链上，而不是 spriteBatch 批次上。</b>面板与 HUD 读的是
        // 游戏与第三方数据（netMap / TrackersInProgress / 本地化表），任何一处 NRE 或越界
        // 都会穿透 Harmony Postfix 打断整个绘制帧 —— 那是「面板一开就崩」的形状。
        // 两段各自 try：HUD 坏了不该连带面板，反之亦然。
        // 帧时间统计（v1.45.0 审计 P0-1）：整条绘制链一条样本，面板另有一条。
        // 计时零分配，故无论诊断开关开不开都测 —— 关掉时只是不报告。
        var started = Stopwatch.GetTimestamp();
        var runtime = __instance == null ? null : Runtime(__instance);

        try
        {
            try
            {
                if (Visible(__instance))
                {
                    DrawPanel(__instance);
                }
            }
            catch (Exception ex)
            {
                ReportDrawFault(__instance, "panel", ex);
            }

            // 与面板无关：自己读 OS.TrackersInProgress，不共享上面任何状态。
            try
            {
                TraceHud.Draw(__instance);
            }
            catch (Exception ex)
            {
                ReportDrawFault(__instance, "hud", ex);
            }
        }
        finally
        {
            if (runtime != null)
            {
                runtime.Perf.AddDraw(Elapsed(started));
                var due = runtime.Perf.Due();
                if (due && PanelSettings.Diagnostics && __instance != null)
                {
                    __instance.write(runtime.Perf.Report() + " | " + WebRenderGuard.TakeStats());
                }
            }
        }
    }

    /// <summary>把 <see cref="Stopwatch.GetTimestamp"/> 的差值换算成毫秒。</summary>
    private static double Elapsed(long started)
        => (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;

    /// <summary>
    /// 绘制异常只上报一次/5 秒：这条路径每帧都跑，不限制就会把终端刷满 ——
    /// 而故障本身通常是「某一帧的数据坏了」，重复行没有信息量。
    /// 诊断细节始终写 <see cref="Trace"/>（编译期才生成）。
    /// </summary>
    private static void ReportDrawFault(OS os, string where, Exception error)
    {
        Trace.Write("draw fault in " + where + ": " + error);

        var runtime = os == null ? null : Runtime(os);
        var now = os?.timer ?? 0;
        if (runtime != null && now - runtime.LastDrawFaultAt < DrawFaultLogSeconds)
        {
            return;
        }

        if (runtime != null)
        {
            runtime.LastDrawFaultAt = now;
        }
        os?.write("[autohack] draw fault (" + where + ") - " + error.GetType().Name + ": " + error.Message);
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
            var runtime = Runtime(__instance);
            var panelStarted = Stopwatch.GetTimestamp();
            action = HackPanel.Draw(runtime.State, runtime.Run, __instance, screen);
            runtime.Perf.AddPanel(Elapsed(panelStarted));
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
                Start(__instance);
                break;

            case HackPanel.PanelKind.Close:
                Runtime(__instance).Close();
                break;

            case HackPanel.PanelKind.Tool:
                RunTool(__instance, action.Verb);
                break;
        }
    }

    /// <summary>
    /// 执行一个工具。工具在游戏线程上同步跑完 —— 与入侵流程共用同一个
    /// os.connectedComp，故运行中一律拒绝，避免两边互相换掉对方的目标。
    /// </summary>
    private static void RunTool(OS os, string verb)
    {
        if (os == null)
        {
            return;
        }

        if (PendingRuns.BusyFor(os) || IsRunning(os))
        {
            os.write("[autohack] " + verb + ": a run is in progress - wait for it to finish.");
            return;
        }

        // wipe 的范围随 SCOPE 段走（「当前节点」= 当前节点所在的连线分量，其余 = 地图全表），
        // 与 dec / mem 的「whole map」复选框不是一个口径 —— 那两个问的是「工具作用在
        // 哪些机器上」，而 wipe 问的是「我的痕迹铺得多远」，后者本就该跟着 SCOPE。
        var state = Runtime(os).State;
        var allNodes = verb == ToolDispatch.Wipe
            ? state.Scope != HackScope.Connected
            : state.AllNodes;

        // 护栏在 ToolDispatch.Run 内 —— 两个入口共用一份，此处不再重复包一层。
        ToolDispatch.Run(os, verb, allNodes);
    }

    /// <summary>绘制异常的最小上报间隔（秒）。见 <see cref="ReportDrawFault"/>。</summary>
    private const double DrawFaultLogSeconds = 5.0;

    private static bool Visible(OS instance)
        => instance != null
           && instance.IsActive
           && Runtimes.TryGetValue(instance, out var runtime)
           && runtime.IsOpen;

    private static void Start(OS os)
    {
        if (os == null)
        {
            return;
        }

        // 单写者：两条运行会同时 connect/disconnect 同一个 os.connectedComp，
        // 互相把对方的目标换掉。已在跑的运行先跑完。
        if (PendingRuns.BusyFor(os) || IsRunning(os))
        {
            os.write("[autohack] A run is already in progress - wait for it to finish.");
            return;
        }

        // <b>只入队，不在这里构造运行。</b>构造要做揭图（写 netMap.visibleNodes）、
        // 目标解析与规划（实测 169 台约 0.28 ms / 331 KB），而本方法跑在 OS.Draw 的
        // Postfix 里 —— 在绘制帧改地图状态是不该有的副作用，尖峰也落在最敏感的帧上。
        // 命令行入口早就是「命令线程只入队、游戏线程再构造」（PendingRuns），
        // 面板入口走同一条时序；脚本错误也在那里转成终端一行。
        if (!PendingRuns.TryEnqueue(os, Runtime(os).State.ToOptions()))
        {
            os.write("[autohack] A run is already in progress - wait for it to finish.");
        }

        // 运行由零 RAM 的隐藏可执行体承载，挂载发生在 PendingRuns.Tick（游戏线程）；
        // 面板的进度视图通过 AutoHackExecutable.Find(os) 读到它，不需要本地字段。
    }

}

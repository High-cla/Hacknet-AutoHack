namespace AutoHack;

using Pathfinder.Event;
using Pathfinder.Event.Gameplay;

/// <summary>
/// 每帧泵：订阅 Pathfinder 的 <see cref="OSUpdateEvent"/>，驱动两件与「运行」无关的队列。
///
/// <b>为什么不再自挂 <c>OS.Update</c> 补丁</b>：此前是 Harmony Postfix，两个补丁的先后由
/// Harmony 装配顺序决定（不保证），而 <see cref="NativeExes.Tick"/> 有「一帧只挂一个动画」
/// 的约束，只能靠注释互相约定。换成事件后顺序显式：一个 handler，次序写在方法体里；
/// 需要与其它插件协调时用 <c>EventHandlerOptions.Priority</c>（排序键见 EventHandler.cs:15）。
///
/// <b>事件只做驱动，不参与主流程</b>：运行本身由 <see cref="AutoHackExecutable"/> 的
/// <c>OnUpdate</c> 推进，状态机是确定性的；这里只负责把两个队列泵出去。
/// </summary>
internal static class GameHost
{
    /// <summary>在插件 Load 末尾调用一次。<c>EventHandler</c> 不去重，故只调一次。</summary>
    internal static void Subscribe()
    {
        EventManager<OSUpdateEvent>.AddHandler(OnUpdate, new EventHandlerOptions { Priority = 0 });
    }

    private static void OnUpdate(OSUpdateEvent update)
    {
        // 演出队列独立于运行推进：跑完收尾后仍要把排着的动画播完。
        NativeExes.Tick(update.OS);

        // headless 队列：命令线程只入队，真正的 HackRun 构造与 exe 挂载都在游戏线程。
        PendingRuns.Tick(update.OS);
    }
}

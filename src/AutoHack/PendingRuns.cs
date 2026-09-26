namespace AutoHack;

using Hacknet;
using HarmonyLib;
using Microsoft.Xna.Framework;

/// <summary>
/// 无面板（headless）运行队列。命令处理发生在命令线程，不宜直接改游戏状态，
/// 故入队后由 OS.Update 在游戏线程上逐帧推进。
/// </summary>
[HarmonyPatch]
internal static class PendingRuns
{
    private static readonly Queue<(OS Os, HackRun Run)> Queue = new();

    internal static void Enqueue(OS os, HackRun run) => Queue.Enqueue((os, run));

    [HarmonyPostfix]
    [HarmonyPatch(typeof(OS), "Update")]
    private static void OnOSUpdate(OS __instance, GameTime gameTime)
    {
        if (Queue.Count == 0 || Queue.Peek().Os != __instance)
        {
            return;
        }

        var (os, run) = Queue.Peek();
        run.Tick(os, (float)gameTime.ElapsedGameTime.TotalSeconds);

        if (run.Finished)
        {
            Queue.Dequeue();
        }
    }
}

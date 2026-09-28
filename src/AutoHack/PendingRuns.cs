namespace AutoHack;

using System.Collections.Concurrent;
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
    /// <summary>一条待跑的 headless 运行。构造推迟到游戏线程（见 <see cref="OnOSUpdate"/>）。</summary>
    private sealed class Entry(HackOptions options)
    {
        internal readonly HackOptions Options = options;
        internal HackRun Run;
    }

    // 键是 OS 实例。用 ConcurrentDictionary 而非 Queue：
    // 1) 一个 OS 至多一条运行 —— TryAdd 天然是原子的互斥，不必再「先查后加」；
    // 2) 按实例取用，换过 OS（回主菜单再进）后旧条目不会堵住新实例的运行。
    // 命令由 OS.execute 派生线程执行（OS.cs:1754-1767），推进在游戏线程上。
    private static readonly ConcurrentDictionary<OS, Entry> Pending = new();

    /// <summary>
    /// 入队一次运行；该 OS 已有未跑完的运行则拒绝（原子判定，无 check-then-act 窗口）。
    ///
    /// <b>只传参数，不传构造好的 HackRun</b>：其构造函数会做可达遍历，其中的
    /// <c>NetworkMap.discoverNode</c> 会改写 <c>netMap.visibleNodes</c>
    /// （NetworkMap.cs:415-431），而游戏线程每帧都在读这个 List
    /// （HubServerAlertsIcon 等）—— 在命令线程构造就是「一个线程 Add、另一个线程遍历」，
    /// 轻则抛 Collection was modified，重则索引错乱。
    /// </summary>
    internal static bool TryEnqueue(OS os, HackOptions options) => Pending.TryAdd(os, new Entry(options));

    /// <summary>该 OS 上是否已有 headless 运行在排队或推进。供面板入口做互斥。</summary>
    internal static bool BusyFor(OS os) => Pending.ContainsKey(os);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(OS), "Update")]
    private static void OnOSUpdate(OS __instance, GameTime gameTime)
    {
        // 演出队列的泵由 HackOverlay 的补丁独家负责 —— 两个补丁同挂 OS.Update，
        // 都调 NativeExes.Tick 就是同一帧挂两个动画（见 NativeExes.Tick 的一帧一个约束）。

        if (!Pending.TryGetValue(__instance, out var entry))
        {
            return;
        }

        // 首次推进时才构造：把「读游戏状态并可能回写」的动作全部留在游戏线程。
        if (entry.Run == null)
        {
            HackRun run;
            try
            {
                run = new HackRun(__instance, entry.Options);
            }
            catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
            {
                // 兜底：插件入口已前置校验过一次。这里是「别把异常抛进 Harmony
                // Postfix」的护栏 —— 抛出去会打断 OS.Update 的整条补丁链。
                __instance.write("[autohack] Script error: " + ex.Message);
                Pending.TryRemove(__instance, out _);
                return;
            }

            if (run.Total == 0)
            {
                __instance.write("[autohack] No eligible targets found.");
                __instance.write(HackEngine.NoTargetHint);
                Pending.TryRemove(__instance, out _);
                return;
            }

            entry.Run = run;
            var skipped = run.SkippedOwned > 0 ? $", {run.SkippedOwned} already owned" : string.Empty;
            __instance.write($"[autohack] Headless run: {run.Targets.Count} target(s), {run.Total} action(s){skipped}.");
        }

        entry.Run.Tick(__instance, (float)gameTime.ElapsedGameTime.TotalSeconds);

        if (entry.Run.Finished)
        {
            // 只有游戏线程会移除，且移除发生在 Finished 之后：新入队要等互斥放行，
            // 故此处不会误删刚被 TryAdd 进来的另一条运行。
            Pending.TryRemove(__instance, out _);
        }
    }
}

namespace AutoHack;

using System.Collections.Concurrent;
using AutoHack.Core;
using Hacknet;

/// <summary>
/// 无面板（headless）运行队列。命令处理发生在命令线程，不宜直接改游戏状态，
/// 故入队后由 <see cref="GameHost"/> 在游戏线程上建出运行并挂成
/// <see cref="AutoHackExecutable"/>。
///
/// <b>只传参数，不传构造好的 HackRun</b>：其构造函数会做可达遍历，其中的
/// <c>NetworkMap.discoverNode</c> 会改写 <c>netMap.visibleNodes</c>
/// （NetworkMap.cs:415-431），而游戏线程每帧都在读这个 List —— 在命令线程构造就是
/// 「一个线程 Add、另一个线程遍历」，轻则抛 Collection was modified，重则索引错乱。
/// </summary>
internal static class PendingRuns
{
    // 键是 OS 实例。用 ConcurrentDictionary 而非 Queue：
    // 1) 一个 OS 至多一条运行 —— TryAdd 天然是原子的互斥，不必再「先查后加」；
    // 2) 按实例取用，换过 OS（回主菜单再进）后旧条目不会堵住新实例的运行。
    private static readonly ConcurrentDictionary<OS, HackOptions> Pending = new();

    /// <summary>入队一次运行；该 OS 已有排队或正在跑则为假（原子判定，无 check-then-act 窗口）。</summary>
    internal static bool TryEnqueue(OS os, HackOptions options) => Pending.TryAdd(os, options);

    /// <summary>该 OS 上是否已有运行在排队、在跑或正在收尾。供面板入口做互斥。</summary>
    internal static bool BusyFor(OS os) => Pending.ContainsKey(os) || AutoHackExecutable.FindActive(os) != null;

    /// <summary>清掉某个 OS 的排队。由 <see cref="HackOverlay.Open"/> 在换局时调用。</summary>
    internal static void Forget(OS os)
    {
        if (os != null)
        {
            Pending.TryRemove(os, out _);
        }
    }

    /// <summary>
    /// 把排队的运行挂成 exe。由 <see cref="GameHost"/> 在 <c>OSUpdateEvent</c> 里每帧调用。
    /// 构造推迟到游戏线程；构造失败（脚本语法错 / 读不到）转成终端可见的一行并丢弃请求。
    /// </summary>
    internal static void Tick(OS os)
    {
        if (!Pending.TryGetValue(os, out var options))
        {
            return;
        }

        // 已有运行在跑：等它结束（BusyFor 已挡住新的入队，这里只是兜住换局等竞态）。
        if (AutoHackExecutable.FindActive(os) != null)
        {
            return;
        }

        try
        {
            var run = new HackRun(os, options);
            if (run.Total == 0)
            {
                Pending.TryRemove(os, out _);
                os.write("[autohack] No eligible targets found.");
                os.write(HackEngine.NoTargetHint);
                return;
            }

            AutoHackExecutable.Create(os, run);
            Pending.TryRemove(os, out _);
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            // 兜底：插件入口已前置校验过一次。这里是「别把异常抛进游戏线程」的护栏。
            Pending.TryRemove(os, out _);
            os.write("[autohack] Script error: " + ex.Message);
        }
    }
}

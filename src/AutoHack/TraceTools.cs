namespace AutoHack;

using System.Collections.Generic;
using Hacknet;

/// <summary>
/// 终止进行中的追踪：把 <c>OS.TrackersInProgress</c> 里的每一台都停掉，并连带擦掉那台的 /log。
///
/// 为什么必须连 /log 一起擦：追踪的<b>复发源就是日志</b>。<c>OS.handleDisconnection</c>
/// （OS.cs:944-960）在每次断开时检查刚断开那台的 /log —— 只要有一行同时含玩家 IP 与
/// <c>FileCopied</c>/<c>FileDeleted</c>/<c>FileMoved</c>，就排一个新的 <c>TrackerDetail</c>
/// （判据见 <c>TrackerCompleteSequence.CompShouldStartTrackerFromLogs</c>，
/// TrackerCompleteSequence.cs:30-47），10~20 秒后计时归零调
/// <c>TrackerCompleteSequence.TrackComplete</c> 跑游戏结束脚本。只清计时不清日志，
/// 等于下次从那台断开时它原地复活。
///
/// 计时列表的语义（OS.cs:823-839）：连着被追踪目标时该项<b>既不递减也不移除</b>，
/// 只有断开后才继续走表。故本工具是玩家唯一能「当场看见、当场掐掉」的入口 ——
/// 此前 <c>TrackersInProgress</c> 在 mod 里是零引用，玩家看不到谁在追踪自己。
///
/// 并发面：<c>TrackersInProgress</c> 由游戏线程每帧遍历（OS.cs:823），而本工具的两个
/// 入口跑在不同线程（命令行经 <c>OS.execute</c> 开独立线程，OS.cs:1754-1767；面板在
/// 游戏线程）。故先取快照、再一次性 <c>Clear()</c>，写窗口只有一次调用 ——
/// 游戏线程的 for 循环每次迭代重读 <c>Count</c>，清空后条件当场为假，不会越界。
/// </summary>
internal static class TraceTools
{
    internal static void Run(OS os)
    {
        if (os == null)
        {
            return;
        }

        var trackers = os.TrackersInProgress;
        if (trackers == null || trackers.Count == 0)
        {
            os.write("[autohack] trace: nothing is tracking you.");
            return;
        }

        var pending = new List<OS.TrackerDetail>(trackers);
        trackers.Clear();

        var wiped = 0;
        foreach (var detail in pending)
        {
            // 擦痕必须在这里做完才算「掐掉」：日志留着，下次断开就复活。
            // ClearLogs 是幂等的 —— 没有 /log 或已清空都返回空列表、不产生输出。
            if (detail.comp != null
                && HackEngine.ClearLogs(detail.comp, os.thisComputer.ip).Count > 0)
            {
                wiped++;
            }
        }

        os.write("[autohack] trace: " + pending.Count + " tracker(s) stopped, "
                 + wiped + " /log wiped.");
    }
}

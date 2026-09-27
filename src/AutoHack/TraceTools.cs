namespace AutoHack;

using System.Collections.Generic;
using Hacknet;

/// <summary>
/// 反追踪：把<b>两套</b>追踪一起止住 ——
/// <list type="number">
/// <item><c>os.traceTracker</c>：左下角那个看得见的倒计时（归零崩玩家机）；</item>
/// <item><c>os.TrackersInProgress</c>：看不见的那批脱机追踪，并连带擦掉其 /log。</item>
/// </list>
/// 两个入口：<c>autohack trace</c>（立即清）与入侵收尾（勾了「disconnect & clear
/// traces」时走这里，见 HackRun.Finish）。命令是「我现在就要清」的唯一入口。
/// 它们回答的是同一个问题：「现在有没有东西在追我，怎么让它停下」。分开成两个动作时，
/// 玩家在最紧张的那一刻还得先判断自己中的是哪一套 —— 而判断本身就需要工具先给出提示。
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

        // 第一套：看得见的倒计时。走 HackEngine.KillTrace —— 与每个目标末尾那步
        // 是同一个实现，不复制一份 stop()。
        var timerStopped = HackEngine.KillTrace(os);

        // 第二套：看不见的脱机追踪。
        var trackers = os.TrackersInProgress;
        var pending = trackers == null ? null : new List<OS.TrackerDetail>(trackers);
        trackers?.Clear();

        var wiped = 0;
        if (pending != null)
        {
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
        }

        var pendingCount = pending?.Count ?? 0;
        if (!timerStopped && pendingCount == 0)
        {
            // 两套都没有：明确说出来。这一行也是「按钮确实生效了」的自证 ——
            // 没有它，玩家无从区分「反追踪成功但本来就没被追」与「点了没反应」。
            os.write("[autohack] anti-trace: nothing is tracking you.");
            return;
        }

        var head = timerStopped ? "timer stopped" : "no active timer";
        os.write("[autohack] anti-trace: " + head + "; "
                 + pendingCount + " tracker(s) stopped, " + wiped + " /log wiped.");
    }
}

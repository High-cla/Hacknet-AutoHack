namespace AutoHack;

using Hacknet;

/// <summary>
/// autohack skip —— 把当前任务判定为完成，直接跳过。
///
/// 走的是游戏自己的两条收尾通道，不伪造任何状态：
///   · 普通任务 → <see cref="ActiveMission.finish"/>（邮件界面 Force Complete 点的那一个）。
///   · DLC 合同 → <see cref="DLCHubServer.PlayerAttemptCompleteMission"/>
///     （DHS 面板 Force Complete 点的那一个）。合同的完成状态不只写在
///     <c>os.currentMission</c> 上 —— DLCHubServer 另外持有一份
///     <see cref="DLCHubServer.ClaimableMission"/>，归档与重新序列化都归它管；
///     只调 finish() 会让合同继续挂在面板上，玩家还能再接一次。
///
/// 两条原生通道分别由邮件界面与 DHS 面板的按钮驱动，后者额外要求
/// <c>Settings.forceCompleteEnabled</c>（缺省 false，只有带 -enablefc 启动才置真，
/// 见 Program.cs:29-32）。这里临时置真再还原：调用的仍是原生方法本身，
/// 不复制它的实现，也不永久改动玩家设置。
/// </summary>
internal static class MissionTools
{
    internal static void Run(OS os)
    {
        var mission = os.currentMission;

        // 无主线任务时退到支线：finish() 内部本就会清空 branchMissions，
        // 故对任一支线调用一次即等于把支线整体收尾。
        if (mission == null)
        {
            if (os.branchMissions == null || os.branchMissions.Count == 0)
            {
                os.write("[autohack] skip: no mission is active.");
                return;
            }

            os.branchMissions[0].finish();
            os.MissionCompleteFlashTime = 3f;
            os.write("[autohack] skip: branch mission completed.");
            return;
        }

        var title = string.IsNullOrEmpty(mission.postingTitle) ? "(unnamed)" : mission.postingTitle;

        if (FindContract(os, mission, out var hub, out var contract))
        {
            var previous = Settings.forceCompleteEnabled;
            Settings.forceCompleteEnabled = true;
            bool done;
            try
            {
                done = hub.PlayerAttemptCompleteMission(contract, ForceComplete: true);
            }
            finally
            {
                Settings.forceCompleteEnabled = previous;
            }

            os.write("[autohack] skip: contract \"" + title + "\" "
                     + (done ? "completed." : "could not be completed."));
            return;
        }

        mission.finish();
        os.MissionCompleteFlashTime = 3f;
        os.write("[autohack] skip: mission \"" + title + "\" completed.");
    }

    /// <summary>
    /// 找出托管这份任务的 DLCHubServer。按对象同一性比对而非标题 ——
    /// 标题可重复，<see cref="DLCHubServer.ClaimableMission.Mission"/> 就是任务本体。
    /// </summary>
    private static bool FindContract(OS os, ActiveMission mission, out DLCHubServer hub, out DLCHubServer.ClaimableMission contract)
    {
        hub = null;
        contract = null;

        var nodes = os.netMap?.nodes;
        if (nodes == null)
        {
            return false;
        }

        foreach (var comp in nodes)
        {
            if (comp == null || comp.getDaemon(typeof(DLCHubServer)) is not DLCHubServer candidate)
            {
                continue;
            }

            foreach (var c in candidate.ActiveMissions)
            {
                if (ReferenceEquals(c?.Mission, mission))
                {
                    hub = candidate;
                    contract = c;
                    return true;
                }
            }
        }

        return false;
    }
}

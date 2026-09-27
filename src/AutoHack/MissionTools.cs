namespace AutoHack;

using Hacknet;

/// <summary>
/// autohack skip —— 把当前任务判定为完成，直接跳过。
///
/// 走的是游戏自己的三条收尾通道，不伪造任何状态：
///   · 普通任务 → <see cref="ActiveMission.finish"/>（邮件界面 Force Complete 点的那一个）。
///   · DLC 合同 → <see cref="DLCHubServer.PlayerAttemptCompleteMission"/>
///     （DHS 面板 Force Complete 点的那一个）。合同的完成状态不只写在
///     <c>os.currentMission</c> 上 —— DLCHubServer 另外持有一份
///     <see cref="DLCHubServer.ClaimableMission"/>，归档与重新序列化都归它管；
///     只调 finish() 会让合同继续挂在面板上，玩家还能再接一次。
///   · Kaguya Trials（DLC 引导）→ <see cref="DLCIntroExe.MissionWasCompleted"/>，
///     见 <see cref="TrySkipKaguyaTrial"/>。它自持任务实例，finish() 推不动它。
///
/// 三条通道分别由邮件界面、DHS 面板、引导自身的进度检查驱动。第二条额外要求
/// <c>Settings.forceCompleteEnabled</c>（缺省 false，只有带 -enablefc 启动才置真，
/// 见 Program.cs:29-32）。这里临时置真再还原：调用的仍是原生方法本身，
/// 不复制它的实现，也不永久改动玩家设置。
/// </summary>
internal static class MissionTools
{
    internal static void Run(OS os)
    {
        // Kaguya Trials（DLC 引导）必须最先查 —— 它跑的时候 os.currentMission
        // 是 null（引导自持 LoadedMission，OS 不参与，见 TrySkipKaguyaTrial 注释），
        // 放后面会被下面的「无任务」提前返回吃掉，症状正是「敲了没反应」。
        if (TrySkipKaguyaTrial(os))
        {
            os.MissionCompleteFlashTime = 3f;
            os.write("[autohack] skip: Kaguya Trial assignment completed.");
            return;
        }

        var mission = os.currentMission;

        // 无主线任务时退到支线：finish() 内部本就会清空 branchMissions，
        // 故对任一支线调用一次即等于把支线整体收尾。
        if (mission == null)
        {
            if (os.branchMissions == null || os.branchMissions.Count == 0)
            {
                // 回显里写明两处都查过了 —— 否则「没任务」与「命令没进来」在终端上
                // 长得一模一样，玩家无从分辨（本命令曾因动词大小写敏感而静默落到
                // 开关面板分支，症状正是「敲了没反应」）。
                os.write("[autohack] skip: no active mission (main and branch lists are both empty).");
                return;
            }

            var branchTitle = string.IsNullOrEmpty(os.branchMissions[0].postingTitle)
                ? "(unnamed)"
                : os.branchMissions[0].postingTitle;
            os.branchMissions[0].finish();
            os.MissionCompleteFlashTime = 3f;
            os.write("[autohack] skip: branch mission \"" + branchTitle + "\" completed.");
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
    /// 推进正在进行的 Kaguya Trials 阶段。走的是原生完成路径
    /// <see cref="DLCIntroExe.MissionWasCompleted"/>（原生 DEBUG Skip 按钮
    /// 也只是补一个 exe 后调 CompleteExecution，见 DLCIntroExe.cs:588-596）。
    /// </summary>
    private static bool TrySkipKaguyaTrial(OS os)
    {
        var exes = os.exes;
        if (exes == null)
        {
            return false;
        }

        foreach (var exe in exes)
        {
            if (exe is not DLCIntroExe intro)
            {
                continue;
            }

            // 只处理「某个阶段正在跑」的两种状态。MissionWasCompleted 内部
            // 已按 IsOnAssignment1 分派：一阶段 → AssignMission2，二阶段 → Outro。
            // 其余状态（SpinningUp / AssignMission* / Exiting …）由引导自身
            // 的计时器推进，skip 不去抢它的状态机。
            if (intro.State is not (DLCIntroExe.IntroState.OnMission1 or DLCIntroExe.IntroState.OnMission2))
            {
                return false;
            }

            // MissionWasCompleted 自己收尾：按 IsOnAssignment1 翻状态、重置
            // charsRenderedSoFar / TimeInThisState / MissionIsComplete，
            // 并翻转 IsOnAssignment1（DLCIntroExe.cs:463-479），
            // 故这里无需重复置位，直接调即可 —— 走的正是原生推进路径。
            intro.MissionWasCompleted();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 找出托管这份任务的 DLCHubServer。先比对象同一性，失效则退回比来源文件 ——
    /// 标题可重复不能用，而同一性在「读档后」必然失效，见循环内的说明。
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
                if (c?.Mission == null)
                {
                    continue;
                }

                // 快路径：本会话内刚接受合同时 os.currentMission 就是这里那个对象
                // （PlayerAcceptMission 直接赋值，DLCHubServer.cs:477）。
                if (ReferenceEquals(c.Mission, mission))
                {
                    hub = candidate;
                    contract = c;
                    return true;
                }

                // 慢路径：读档之后同一性必然失效。os.currentMission 由 OS.cs:1474
                // 的 ActiveMission.load 重建，ActiveMissions 由 DLCHubServer.cs:412
                // 的 restoreMissionFromFile 重建 —— 两条独立通道产出两个不同实例。
                // 改比来源文件；这是全库通用的任务定位键，游戏自己也这么做
                // （DLCHubServer.cs:403、MissionHubServer.cs:163、MissionListingServer.cs:275）。
                if (SameSource(c.Mission.reloadGoalsSourceFile, mission.reloadGoalsSourceFile))
                {
                    hub = candidate;
                    contract = c;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 两个来源文件路径是否指向同一份任务定义。
    ///
    /// 不能直接字符串相等 —— 两条序列化通道写出的值可能不同形：
    /// <see cref="ActiveMission.getSaveString"/>（ActiveMission.cs:87）原样写出，
    /// 而 <see cref="MissionSerializer.generateMissionFile"/>（MissionSerializer.cs:15）
    /// 写出前先编码；读回时 <see cref="ActiveMission.load"/>（ActiveMission.cs:144）
    /// 会给裸路径补 <c>Content/</c> 前缀，<see cref="LocalizedFileLoader.GetLocalizedFilepath"/>
    /// 又会把 <c>Content/</c> 换成 <c>Content/Locales/&lt;locale&gt;/</c>。
    /// 故归一化后比较，并容忍一方是另一方的后缀。
    /// </summary>
    private static bool SameSource(string a, string b)
    {
        var na = NormalizeSource(a);
        var nb = NormalizeSource(b);
        if (na.Length == 0 || nb.Length == 0)
        {
            return false;
        }

        return na == nb
               || na.EndsWith(nb, StringComparison.OrdinalIgnoreCase)
               || nb.EndsWith(na, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSource(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var text = path.Replace('\\', '/');
        var marker = "/Locales/";
        var at = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (at >= 0)
        {
            var end = text.IndexOf('/', at + marker.Length);
            if (end >= 0)
            {
                text = text.Substring(0, at + 1) + text.Substring(end + 1);
            }
        }

        return text;
    }
}

namespace AutoHack;

using Hacknet;

/// <summary>
/// 原生破解程序的「演出」：把游戏自己的 SSHCrackExe / FTPBounceExe 等挂进 RAM 面板，
/// 让入侵过程带上原版的动画与音效。由 <c>autohack run show</c> 或面板勾选开启。
///
/// <b>只是演出。</b>端口状态在调用点已由 <see cref="HackEngine.OpenPort"/> 同步写好，
/// 这里挂的 exe 到点后自己再调一次 <c>Computer.openPort</c>（SSHCrackExe.cs:229、
/// SMTPoverflowExe.cs:169、HTTPExploitExe.cs:161、FTPBounceExe.cs:153、
/// SQLExploitExe.cs:235、MedicalPortExe.cs:110、TorrentPortExe.cs:74、
/// PacificPortExe.cs:49、RTSPPortExe.cs:80）—— 那是幂等的：Pathfinder 的
/// <c>OpenPortPrefix</c> 只做 <c>PortState.Cracked = true</c> 就 <c>return false</c>
/// （ComputerExtensions.cs:182-197），故两边不冲突、不重复计数。
/// 状态不依赖 exe 跑完，exe 被中途打断也不影响战果。
///
/// 三条硬约束（决定了白名单为什么长这样）：
/// 1. <c>ExeModule</c> 构造时把 <c>targetIP</c> 定成「当前连接目标，没连接就是本机」
///    （ExeModule.cs:38）—— 没连着目标就放，动画会打在自己身上。故必须已连接。
/// 2. 有 3 个端口虽有破解程序，却在 <c>OS.launchExecutable</c> 的 switch 里**没有 case**
///    （OS.cs:2003-2154）：3724 WoWHack / 3659 confloodEOS / 9418 GitTunnel —— 传进去是静默空操作。
/// 3. 两个参数不匹配的：<c>SSLTrojan.exe</c>(443) 要 4 个参数
///    （<c>ssltrojan &lt;隧道端口&gt; &lt;-s|-f|-w|-r&gt; &lt;旁路端口&gt;</c>，SSLPortExe.cs:44 的
///    <c>args.Length &lt; 4</c>），且目标上那个旁路端口必须**已开**（:106 的 <c>if (!flag)</c>）——
///    端口开放顺序不可控，传 null 又必崩；<c>FTPSprint.exe</c>(211) 的 <c>Completed()</c> 开的
///    是 21 而不是 211（FTPFastExe.cs:60）—— 会把端口开错。两者都在白名单外。
///
/// 白名单与 <c>docs/EXTENSIONS.md</c> §4.2 的官方占位符表交叉验证一致：
/// 该表端口集与 <c>PortExploits.cracks</c> 的差集是 <c>1, 8, 3659, 3724, 9418</c> ——
/// 后三个正是约束 2 里无 case 的那三个（官方自己也没给它们占位符）。
///
/// 另需注意 <c>OS.addExe</c> 的 RAM 门禁（OS.cs:2169）：内存不够时只写一行
/// "Insufficient Memory"、不挂 exe。演出失败不影响战果，故不为此预判或扩容。
/// </summary>
internal static class NativeExes
{
    /// <summary>
    /// 可安全演出的破解程序名（取自 <c>PortExploits.cracks</c>，不硬编码端口号）。
    /// 入表条件：在 launchExecutable 里有 case、不需要参数、且 <c>Completed()</c> 开的
    /// 正是自己那个端口。排除项与理由见类注释。
    /// </summary>
    private static readonly HashSet<string> SafeExeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "SSHcrack.exe",
        "FTPBounce.exe",
        "SMTPoverflow.exe",
        "WebServerWorm.exe",
        "SQL_MemCorrupt.exe",
        "KBT_PortTest.exe",
        "TorrentStreamInjector.exe",
        "PacificPortcrusher.exe",
        "RTSPCrack.exe",
    };

    /// <summary>
    /// 为一次端口破解放原生动画。<paramref name="target"/> 必须正是当前连接目标 ——
    /// 否则动画会打在本机上（见类注释约束 1）。未连接、无对应程序、程序不在白名单时静默跳过。
    /// </summary>
    internal static void Show(OS os, Computer target, PortInfo port)
    {
        if (os == null || target == null || !ReferenceEquals(os.connectedComp, target))
        {
            return;
        }

        if (PortExploits.cracks == null || !PortExploits.cracks.TryGetValue(port.CodePort, out var exeName))
        {
            return;
        }

        if (!SafeExeNames.Contains(exeName))
        {
            return;
        }

        // 数据必须一并传：launchExecutable 靠它反查 exe 类型（OS.cs:2000
        // GetExeNameForData 逐项比对 crackExeData），只有名字是匹配不上的。
        if (PortExploits.crackExeData == null
            || !PortExploits.crackExeData.TryGetValue(port.CodePort, out var data)
            || string.IsNullOrWhiteSpace(data))
        {
            return;
        }

        os.launchExecutable(exeName, data, port.CodePort);
    }
}

namespace AutoHack;

using Hacknet;
using Microsoft.Xna.Framework;

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
/// <b>为什么必须串行（v1.32.8）。</b>此前是「破一个端口就地挂一个 exe」，于是同时挂在
/// RAM 面板上的 exe 数量只受破解节奏限制 —— 端口步只等 <c>PortDelay</c>（缺省 0.6s），
/// 而单个动画要跑 4.8~22 秒（TorrentPortExe 4.8 / PacificPortExe 6 / RTSPPortExe 6.3 /
/// SSHCrackExe 8 / SMTPoverflowExe 12 / HTTPExploitExe 14 / FTPBounceExe 15 /
/// MedicalPortExe 22），全网扫描必然叠出十几个。RAM 总量
/// <c>totalRam = 800 - (21 + 2) - 16 = 761</c>（OS.cs:74），而单个 exe 就要
/// 190~400（PacificPortExe 190 / HTTPExploitExe 208 / FTPBounceExe 210 /
/// SSHCrackExe 242 / SQLExploitExe 350 / SMTPoverflowExe 356 / TorrentPortExe 360 /
/// RTSPPortExe 360 / MedicalPortExe 400），三个就超了 ——
/// <c>OS.addExe</c>（OS.cs:2169）于是走 else 分支，刷一次
/// <c>ram.FlashMemoryWarning()</c> 并写 <c>"Insufficient Memory"</c>。
/// 现在改成队列 + 每帧泵：每帧在<b>游戏自己重算的预算</b>内挂载，
/// 故 <c>addExe</c> 的门禁恒真、溢出在结构上不可能发生。
///
/// <b>能并发就并发，且优先放「内存小、时间短」的。</b>并发上限由 RAM 决定，
/// 不是 1 —— 761mb 的预算配上 190~400mb 的单个动画，同时挂 2~3 个是常态。
/// 队列按 <see cref="Compare"/> 升序排（内存小者先、同则时间短者先、再则随机），
/// 泵只挂队首且要求装得下 —— 按代价升序贪心装箱正是<b>最大化并发个数</b>的装法，
/// 小动画先占位、也先播完释放，吞吐因此最高。
/// 超出 <see cref="MaxQueued"/> 的新请求丢弃：演出是可丢的装饰，战果早已写好。
///
/// 每帧只挂一个：<c>os.ramAvaliable</c> 由 <c>OS.Update</c> 每帧重算
/// （OS.cs:840-859），一帧内连挂多个会让后续 <c>addExe</c> 拿同一个尚未扣减的
/// 值去判断，挂出预算之外。每帧一个即 60 个/秒，远快于需求。
///
/// <b>为什么绕开 <c>OS.launchExecutable</c> 直接 <c>OS.addExe</c>。</b>
/// launchExecutable 在挂 exe 前先算位置：<c>ram.bounds.Y + contentStartOffset</c>
/// 再加一遍 <c>exes[i].bounds.Height</c>（OS.cs:1898-1903）。这条位置算法只在
/// 面板为空时正确 —— 而这里正是「已经挂着一个动画、再挂下一个」的场景，
/// 算出来的 Y 会叠在上一个 exe 身上。addExe 自己不碰位置，交给
/// <c>OS.Update</c> 的布局循环（OS.cs:840-851）统一排，那才是权威。
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
/// 另需注意 <c>OS.addExe</c> 的 RAM 门禁（OS.cs:2169）与跳板门禁（:2165）：
/// 前者经串行队列后不会触发；后者仍在，跳板激活期间排队项会以
/// "Proxy Active -- Cannot Execute" 作罢 —— 演出失败不影响战果，不为此预判。
/// </summary>
internal static class NativeExes
{
    /// <summary>队列容量。积压超此数后，新请求只有比队尾更优才挤得进来
    /// （见 <see cref="Show"/>）—— 演出是可丢的装饰，战果早已写好，
    /// 不为此把几十个动画排到几十分钟之后。</summary>
    private const int MaxQueued = 8;

    /// <summary>
    /// 一个待播动画：exe 实例 + 排序与决策用的代价。
    ///
    /// <b>存实例而非程序名，是刻意的。</b>exe 的 <c>targetIP</c> 在构造时定成
    /// 「当前连接目标」（ExeModule.cs:38），而队列里的动画会跨目标存活 ——
    /// 单个动画 8~33 秒，远长于单个目标的破解耗时，下一个 Connect 早就切走了。
    /// 若推迟到播放时才构造，<c>targetIP</c> 会指向<b>后来那台机器</b>：
    /// 动画打错对象还是轻的，<c>Completed()</c> 里的 <c>openPort</c> 会给一台
    /// 本不该被开端口的机器开端口 —— 那是污染战果。
    /// 同理，构造体里的 <c>hostileActionTaken()</c>（PacificPortExe.cs:26、
    /// TorrentPortExe.cs:36、RTSPPortExe.cs:32）必须在破端口那一刻触发：
    /// 推到播放时就成了收尾之后，而 <c>HackRun.Finish</c> 已经清过一轮追踪，
    /// 等于凭空复燃一次倒计时。
    ///
    /// <paramref name="Tiebreak"/> 是「同档内随机」的载体：代价相同的动画按它排，
    /// 入队时取一个随机数，故同档的相对次序每次都不一样。
    /// </summary>
    private readonly record struct Pending(ExeModule Exe, int RamCost, float Seconds, float Tiebreak);

    /// <summary>待播动画，按 <see cref="Compare"/> 排序 —— 代价小的在前。</summary>
    private static readonly List<Pending> Queue = new(MaxQueued);

    /// <summary>
    /// 可安全演出的破解程序名 → <b>实际存活时长</b>（秒）。这张表同时就是白名单，
    /// 键集就是可安全演出的全集 —— 白名单与时长合一而非两份，杜绝失同步。
    ///
    /// 白名单的入表条件（取自 <c>PortExploits.cracks</c>，不硬编码端口号）：
    /// 在 launchExecutable 里有 case、不需要参数、且 <c>Completed()</c> 开的
    /// 正是自己那个端口。排除项与理由见类注释。
    ///
    /// 时长是<b>从构造到 <c>needsRemoval</c> 被摘除</b>的全过程，不是动画名义时长。
    /// 三段相加：动画本体 + 收尾停顿 + 淡出
    /// （<c>fade</c> 从 1 减到 0 需要 <c>1 / FADEOUT_RATE = 2</c> 秒，ExeModule.cs:9/:76）。
    /// 出处逐条：
    /// - TorrentStreamInjector：<c>IDLE_TIME 16.5</c>（TorrentPortExe.cs:11）+ 2
    /// - PacificPortcrusher：<c>IDLE_TIME 6.2</c>（PacificPortExe.cs:10）+ 2
    /// - SSHcrack：<c>DURATION 8</c>（SSHCrackExe.cs:20）+ 2
    /// - SQL_MemCorrupt：3+3+5+1.2 = <c>12.2</c>（SQLExploitExe.cs:16-22）+ 2
    /// - SMTPoverflow：<c>DURATION 12</c>（SMTPoverflowExe.cs:10）+ <c>sucsessTimer 0.5</c>（:20）+ 2
    /// - WebServerWorm：<c>DURATION 14</c>（HTTPExploitExe.cs:10）+ <c>AFTER_COMPLETION_STALL 1</c>（:12）+ 2
    /// - FTPBounce：<c>DURATION 15</c>（FTPBounceExe.cs:8）+ 2
    /// - KBT_PortTest：<c>RUNTIME 22</c> + <c>COMPLETE_TIME 2</c>（MedicalPortExe.cs:7-9）+ 2
    /// - RTSPCrack：<c>IDLE_TIME 30.5</c>（RTSPPortExe.cs:10）+ 2
    /// </summary>
    private static readonly Dictionary<string, float> Lifetimes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TorrentStreamInjector.exe"] = 18.5f,
        ["PacificPortcrusher.exe"] = 8.2f,
        ["SSHcrack.exe"] = 10f,
        ["SQL_MemCorrupt.exe"] = 14.2f,
        ["SMTPoverflow.exe"] = 14.5f,
        ["WebServerWorm.exe"] = 17f,
        ["FTPBounce.exe"] = 17f,
        ["KBT_PortTest.exe"] = 26f,
        ["RTSPCrack.exe"] = 32.5f,
    };

    /// <summary>调度次序：先内存小、再时间短、最后按随机数 —— 前两级降序地
    /// 把「占得少、走得快」的排到前面，第三级保证同档内次序随机。</summary>
    private static int Compare(Pending a, Pending b)
    {
        if (a.RamCost != b.RamCost)
        {
            return a.RamCost.CompareTo(b.RamCost);
        }

        return a.Seconds != b.Seconds ? a.Seconds.CompareTo(b.Seconds) : a.Tiebreak.CompareTo(b.Tiebreak);
    }

    /// <summary>
    /// 为一次端口破解排一个原生动画。<paramref name="target"/> 必须正是当前连接目标 ——
    /// 否则动画会打在本机上（见类注释约束 1）。未连接、无对应程序、程序不在白名单时静默跳过；
    /// 队列满时只有比队尾更优才挤进来。
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

        if (!Lifetimes.TryGetValue(exeName, out var seconds))
        {
            return;
        }

        var exe = Create(os, exeName);
        if (exe == null)
        {
            return;
        }

        var pending = new Pending(exe, exe.ramCost, seconds, (float)Utils.random.NextDouble());

        if (Queue.Count >= MaxQueued)
        {
            // 队列满：只有比队尾（代价最大的那个）更优才挤得进来，否则丢弃新来的。
            // 这样队列始终装着「最小的 MaxQueued 个」，而不是先到先得 —— 与
            // 「优先放内存小、时间短」同一套取舍。被挤掉的实例只是不再演出：
            // 它从未 addExe，不会开端口、不影响战果（构造期的 hostileActionTaken
            // 已经触发过，与旧实现一致）。
            if (Compare(pending, Queue[Queue.Count - 1]) >= 0)
            {
                return;
            }

            Queue.RemoveAt(Queue.Count - 1);
        }

        Queue.Add(pending);
        Queue.Sort(Compare);
    }

    /// <summary>
    /// 每帧推进一步：只要预算装得下队首，就把它挂上去（队首是最小的，见
    /// <see cref="Compare"/>）。由 <see cref="HackOverlay"/> / <see cref="PendingRuns"/>
    /// 的 OS.Update 补丁调用，与运行是否结束无关 —— 收尾后的队列仍要播完。
    /// </summary>
    internal static void Tick(OS os)
    {
        if (os == null || Queue.Count == 0)
        {
            return;
        }

        // 一帧只挂一个：os.ramAvaliable 由 OS.Update 每帧重算（OS.cs:840-859），
        // 而 addExe 只扣 exes 的累计值、不回写它。同帧连挂多个会拿同一个
        // 尚未扣减的值反复判断，挂到预算之外。60 个/秒已远快于需求。
        var head = Queue[0];
        if (head.RamCost > os.ramAvaliable)
        {
            return;
        }

        Queue.RemoveAt(0);

        // 绕开 launchExecutable：它的位置算法是面板为空时的那一套（见类注释）。
        // 队列按升序排、队首是代价最小的，装不下就说明其余更装不下 ——
        // 留到预算释放后再挂，不是丢弃。
        os.addExe(head.Exe);
    }

    /// <summary>丢弃所有待播动画。换 OS（回主菜单再进档）时调用 ——
    /// 否则上一局排的动画会漏进新一局。</summary>
    internal static void Reset() => Queue.Clear();

    /// <summary>
    /// 按白名单里的名字造出对应 exe。位置交给 <c>OS.Update</c> 的布局循环定
    /// （OS.cs:840-851 每帧把 bounds.X 设为面板 X、从第二个起接在上一个下面），
    /// 故这里给一个占位矩形即可。
    /// </summary>
    private static ExeModule Create(OS os, string exeName) => exeName switch
    {
        // 无参构造：ramCost 与 IdentifierName 在构造函数里定死。
        "SSHcrack.exe" => new SSHCrackExe(Placeholder(os), os),
        "FTPBounce.exe" => new FTPBounceExe(Placeholder(os), os),
        "SMTPoverflow.exe" => new SMTPoverflowExe(Placeholder(os), os),
        "WebServerWorm.exe" => new HTTPExploitExe(Placeholder(os), os),
        "SQL_MemCorrupt.exe" => new SQLExploitExe(Placeholder(os), os),

        // 三参构造：p 是参数列表，下面四个都不读它（MedicalPortExe.cs:27-34、
        // PacificPortExe.cs:18-27、TorrentPortExe.cs:25-37、RTSPPortExe.cs:26-35
        // 的构造体里都没有出现 p），传空数组即可。
        "KBT_PortTest.exe" => new MedicalPortExe(Placeholder(os), os, Array.Empty<string>()),
        "TorrentStreamInjector.exe" => new TorrentPortExe(Placeholder(os), os, Array.Empty<string>()),
        "PacificPortcrusher.exe" => new PacificPortExe(Placeholder(os), os, Array.Empty<string>()),
        "RTSPCrack.exe" => new RTSPPortExe(Placeholder(os), os, Array.Empty<string>()),

        _ => null,
    };

    /// <summary>构造 exe 用的占位矩形。真正的 bounds 由 <c>OS.Update</c> 每帧重排，
    /// 且 <c>ExeModule.LoadContent</c> 只改写 Height（ExeModule.cs:60-63），
    /// 故 X/Y 在这里取什么都不影响呈现。</summary>
    private static Rectangle Placeholder(OS os)
        => new(os.ram.bounds.X, os.ram.bounds.Y + RamModule.contentStartOffset, RamModule.MODULE_WIDTH, 0);
}

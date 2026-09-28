namespace AutoHack;

using System;
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
/// <b>能并发就并发，且优先放「没出现过的、内存小、时间短的」。</b>并发上限由 RAM 决定，
/// 不是 1 —— 761mb 的预算配上 190~400mb 的单个动画，同时挂 2~3 个是常态。
/// 队列按 <see cref="Compare"/> 升序排：已播出次数少的在前（九种动画轮流冒头），
/// 同次数时内存小、时间短的在前，再相同则按随机数。
/// 泵只挂队首且要求装得下 —— 按代价升序贪心装箱正是<b>最大化并发个数</b>的装法，
/// 小动画先占位、也先播完释放，吞吐因此最高。
/// 超出 <see cref="MaxQueued"/> 的新请求丢弃：演出是可丢的装饰，战果早已写好。
///
/// <b>演出会点燃追踪。</b>9 个白名单 exe 里有 8 个调 <c>hostileActionTaken()</c>
/// （3 个在构造函数、5 个在 <c>LoadContent</c>），目标 <c>traceTime &gt; 0</c> 时即
/// 启动倒计时。泵每帧扑掉一次，理由与覆盖面见 <see cref="KillTrace"/>。
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
    /// TorrentPortExe.cs:36、RTSPPortExe.cs:32）在入队那一刻就点燃追踪：
    /// 推到播放时点燃会落到收尾之后（<c>HackRun.Finish</c> 已清过一轮），
    /// 等于凭空复燃一次倒计时 —— 现在泵每帧都会扑掉它（见 <see cref="KillTrace"/>），
    /// 但构造时机仍该由「破端口那一刻」决定，不该依赖事后清理。
    ///
    /// <paramref name="ExeName"/> 是查 <see cref="ShownCount"/> 的键 —— 调度以「这类动画
    /// 播出过几次」为主序，故必须把名字一起带着，不能等到播出时再反查。
    ///
    /// <paramref name="Tiebreak"/> 是「同档次内随机」的载体：键相同的动画按它排，
    /// 入队时取一个随机数，故同档的相对次序每次都不一样。
    /// </summary>
    private readonly record struct Pending(
        ExeModule Exe, string ExeName, int RamCost, float Seconds, float Tiebreak);

    /// <summary>待播动画，按 <see cref="Compare"/> 排序 —— 代价小的在前。</summary>
    private static readonly List<Pending> Queue = new(MaxQueued);

    /// <summary>
    /// <b>已经挂上面板、仍归本类管的</b> exe 实例。停播时要靠它精确找到自己挂的那些 ——
    /// 不能拿 <c>os.exes</c> 按 <c>targetIP</c> 反查：同一台目标上玩家自己也可能开着
    /// 别的 exe（ShellExe 之类），按 IP 匹配会把玩家的程序一起淡出掉。
    ///
    /// 播完的实例由游戏自己摘除（<c>needsRemoval</c>，ExeModule.cs:76 → OS.cs:852），
    /// 故每帧清一次「已不在 <c>os.exes</c> 里」的项，列表不随会话增长。
    /// </summary>
    private static readonly List<ExeModule> Live = new(MaxQueued);

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

    /// <summary>内存分档宽度（mb）。ramCost 落在 190~400，按 100 分即 4 档。</summary>
    private const int RamTierMb = 100;

    /// <summary>时长分档宽度（秒）。实际存活 8.2~32.5 秒，按 10 分即 4 档。</summary>
    private const int SecondTierSec = 10;

    /// <summary>时长档数 —— 内存档的权重。取 4 是因为 <see cref="SecondTierSec"/>
    /// 把时长分成了 4 档，相乘即「内存档优先、时长档次之」的二维编号。</summary>
    private const int SecondTierCount = 4;

    /// <summary>
    /// <see cref="Tier"/> 的取值上界，排序键用它当进制（实测档号落在 4~19，取 32 留足余量）。
    /// 必须<b>严格大于</b>档号最大值，否则次数差 1 的两项可能被档号差吃掉，
    /// 「没出现过的优先」就不再成立。
    /// </summary>
    private const int TierSpan = 32;

    /// <summary>
    /// 每个 exe 名<b>已经播出过多少次</b>。调度以它为主序（少者先），
    /// 于是各类动画的出现次数自发趋于均衡 —— 这就是「加权平均」的那一半。
    ///
    /// 只在实际挂上 RAM 面板（<see cref="Tick"/> 里的 <c>addExe</c>）时递增：
    /// 入队后被挤掉、被丢弃、或因预算不足一直没挂上的，都不算「出现过」。
    ///
    /// 排序时<b>实时查这张表</b>，而不是把次数抄进 <see cref="Pending"/> ——
    /// 这样「刚播完的那一个」立刻在队列里排到最后，积压的同类不会连着播。
    /// </summary>
    private static readonly Dictionary<string, int> ShownCount = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 调度次序：<b>先播出得少的（没出现过的优先），同次数时内存小、时间短的优先，
    /// 再相同则按随机数</b>。
    ///
    /// 主序是「已播出次数」：九种动画轮流冒头，而不是反复演同几个。
    /// 次序的权重来自 <see cref="Tier"/>（内存档 + 时长档），让内存小、跑得快的
    /// 在同次数下先上，并发吞吐与观感都更好。
    ///
    /// <b>为什么必须分档，不能逐值比较。</b>每个 exe 的 <c>ramCost</c> 与存活时长
    /// 在游戏里都是唯一的（190/208/210/242/350/356/360/360/400 与 8.2/10/14.2/14.5/
    /// 17/17/18.5/26/32.5），逐值比较时前两级<b>永远不相等</b>，第三级的随机数就
    /// 永远轮不到求值 —— 播出次序退化成表的书写顺序，每次运行一模一样。
    /// 分档把「相等」造出来，随机数才真正参与排序。
    /// </summary>
    private static int Compare(Pending a, Pending b)
    {
        var keyA = SortKey(a);
        var keyB = SortKey(b);
        return keyA != keyB ? keyA.CompareTo(keyB) : a.Tiebreak.CompareTo(b.Tiebreak);
    }

    /// <summary>
    /// 排序键 = <b>已播出次数 × <see cref="TierSpan"/> + 代价档</b>。
    ///
    /// 次数乘一个大于档号上界的进制，是为了让「没出现过」压倒一切：次数为 0 时键 ≤ 19，
    /// 而任何播出过一次的键 ≥ 32 —— 无论它的代价档多小，都排不到前面去。
    /// 代价档只在<b>同次数</b>时才起区分作用，这正是「加权」二字的位置。
    /// </summary>
    private static int SortKey(Pending p) => Count(p.ExeName) * TierSpan + Tier(p);

    private static int Count(string exeName)
        => ShownCount.TryGetValue(exeName, out var n) ? n : 0;

    /// <summary>代价档号：内存档为主、时长档为辅（见 <see cref="SortKey"/>）。</summary>
    private static int Tier(Pending p)
        => p.RamCost / RamTierMb * SecondTierCount + (int)(p.Seconds / SecondTierSec);

    /// <summary>
    /// 为一次端口破解排一个原生动画。<paramref name="target"/> 必须正是当前连接目标 ——
    /// 否则动画会打在本机上（见类注释约束 1）。未连接、无对应程序、程序不在白名单时静默跳过；
    /// 目标已提权时也跳过（见方法内）；队列满时只有排序键比队尾更小才挤进来。
    /// </summary>
    internal static void Show(OS os, Computer target, PortInfo port)
    {
        if (os == null || target == null || !ReferenceEquals(os.connectedComp, target))
        {
            return;
        }

        // 已提权的机器不排演出。它不需要破端口（权限已在手），动画因此纯粹是噪音，
        // 而且每一发都会点燃追踪（见 Tick 的清理）。判据用 adminIP —— 与
        // HackEngine.IsOwned 同一把尺子，也是「已经黑进去了」的权威定义。
        //
        // 常规路径下走不到这里：全网扫描缺省已在 ResolveTargets 剔除已控机器。
        // 起作用的是 redo / 显式点名 / 白名单回退 / 「当前节点」反推这四条路 ——
        // 玩家要碰那台机器，但碰不等于要看一遍它已经完成过的动画。
        if (HackEngine.IsOwned(target, os))
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

        var pending = new Pending(exe, exeName, exe.ramCost, seconds, (float)Utils.random.NextDouble());

        if (Queue.Count >= MaxQueued)
        {
            // 队列满：只有排序键比队尾（键最大的那个）更小才挤得进来，否则丢弃新来的。
            // 这样队列始终装着「键最小的 MaxQueued 个」，而不是先到先得 —— 与
            // 「没出现过的优先、同次数时内存小时间短优先」同一套取舍。
            // 被挤掉的实例只是不再演出：它从未 addExe，不会开端口、不影响战果
            // （构造期点火的那三个 exe 的 hostileActionTaken 已经触发过，与旧实现一致，
            // 且泵每帧都会把它扑掉 —— 见 KillTrace）。
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
    /// 每帧推进一步：只要预算装得下队首，就把它挂上去（队首的排序键最小，见
    /// <see cref="Compare"/>）。由 <see cref="HackOverlay"/> 的 OS.Update 补丁调用。
    ///
    /// 泵本身不关心入侵是否还在推进 —— 队列何时被截断由 <see cref="StopTarget"/> 与
    /// <see cref="StopAll"/> 决定，那两个入口由 <see cref="HackRun"/> 在目标边界与
    /// 整轮收尾处调用。
    ///
    /// 只在游戏线程调用（OS.Update 的 Harmony Postfix），故与 <see cref="Queue"/> /
    /// <see cref="Live"/> 的读写天然串行，不需要加锁。
    /// </summary>
    internal static void Tick(OS os)
    {
        if (os == null)
        {
            return;
        }

        // 播完的实例已由游戏摘除，这里跟着剪一遍 —— 队列空时也要剪，
        // 否则 Live 会在整个会话里只增不减。
        if (Live.Count > 0)
        {
            Live.RemoveAll(exe => exe == null || !os.exes.Contains(exe));
        }

        if (Queue.Count == 0)
        {
            return;
        }

        // 一帧只挂一个：os.ramAvaliable 由 OS.Update 每帧重算（OS.cs:840-859），
        // 而 addExe 只扣 exes 的累计值、不回写它。同帧连挂多个会拿同一个
        // 尚未扣减的值反复判断，挂到预算之外。60 个/秒已远快于需求。
        var head = Queue[0];
        if (head.RamCost <= os.ramAvaliable)
        {
            Queue.RemoveAt(0);

            // 绕开 launchExecutable：它的位置算法是面板为空时的那一套（见类注释）。
            // 队列按升序排、队首的键最小，装不下就说明其余更装不下 ——
            // 留到预算释放后再挂，不是丢弃。
            os.addExe(head.Exe);

            // 登记归属：停播时要按实例精确摘出来（见 Live）。
            Live.Add(head.Exe);

            // 挂上去了才算「出现过」—— 排过队但被挤掉、丢弃、或预算不足没挂上的都不算。
            ShownCount[head.ExeName] = Count(head.ExeName) + 1;

            // 计数变了，队列里同类的键随之变大，重排一次让「刚播完的那类」立刻让位
            // 给还没露面的。队列至多 8 项，每帧最多走这一次。
            Queue.Sort(Compare);
        }

        KillTrace(os);
    }

    /// <summary>
    /// 扑掉演出点燃的追踪。
    ///
    /// <b>演出是会点燃追踪的</b>，这是它最容易被忽略的副作用 —— 9 个白名单 exe 里有 8 个调
    /// <c>hostileActionTaken()</c>（唯一不调的是 MedicalPortExe，即 KBT_PortTest），分两处：
    /// <list type="bullet">
    /// <item><b>构造函数里</b>（入队那一刻）：TorrentPortExe.cs:36、PacificPortExe.cs:26、
    /// RTSPPortExe.cs:32。</item>
    /// <item><b>LoadContent 里</b>（<c>OS.addExe</c> 挂载那一刻，OS.cs:2171）：
    /// SSHCrackExe.cs:90、FTPBounceExe.cs:72、SMTPoverflowExe.cs:61、
    /// HTTPExploitExe.cs:59、SQLExploitExe.cs:63。</item>
    /// </list>
    /// 目标 <c>traceTime &gt; 0</c> 时它即 <c>os.traceTracker.start(traceTime)</c>
    /// （Computer.cs:294-308），倒计时归零走 <c>os.timerExpired()</c>
    /// （TraceTracker.cs:85-90）端掉玩家。
    ///
    /// <b>为什么在 Tick 里扑，而不是在入侵步骤里扑。</b>LoadContent 那一批在
    /// <c>addExe</c> 时才点火，而 addExe 发生在<b>入队之后若干帧到几十秒</b>——
    /// 那时入侵的端口步早已跑完，步骤层的任何清理都够不着。只有泵这里能覆盖。
    /// 构造期点火的那三个则在入队当帧就被扑掉。
    ///
    /// <b>成本为零</b>：未激活时 <see cref="HackEngine.KillTrace"/> 直接返回 false
    /// （<c>traceTracker</c> 非 <c>active</c>，TraceTracker.cs:116-120 只置两个字段）。
    /// 队列非空时逐帧调用，不会输出任何东西。
    /// </summary>
    private static void KillTrace(OS os)
    {
        // 不报数：这是每帧都可能发生的维护动作，写终端就是刷屏。
        HackEngine.KillTrace(os);
    }

    /// <summary>丢弃所有待播动画，并把「出现次数」清零。换 OS（回主菜单再进档）时调用 ——
    /// 否则上一局排的动画会漏进新一局，且上一局的均衡进度会带偏新一局的次序。
    ///
    /// <b>不淡出 <see cref="Live"/> 里的实例</b>：换 OS 意味着那些 exe 连同旧 OS 的
    /// <c>exes</c> 列表一起被丢弃，去碰它们没有意义。只清列表本身。</summary>
    internal static void Reset()
    {
        Queue.Clear();
        Live.Clear();
        ShownCount.Clear();
    }

    /// <summary>
    /// 一台目标的动作跑完了：丢掉它待播的动画，并让它已经在面板上播的那个淡出。
    ///
    /// <b>为什么需要这个入口。</b>动画在<b>破端口那一刻</b>入队，而提权排在全部端口之后
    /// （BuildSteps）—— 等到提权发生时，队列里还压着属于这台机器的好几个动画
    /// （队列容量 8，单个动画要活 8~33 秒，而单台端口步只花端口数 × PortDelay ≈ 几秒）。
    /// 不截断的话，玩家会在「已经黑进去了」之后继续看这台机器的破解动画。
    ///
    /// 按 <c>targetIP</c> 匹配而不是按 <see cref="Computer"/> 引用：exe 自己就是靠
    /// <c>Programs.getComputer(os, targetIP)</c> 找目标的（SSHCrackExe.cs:226 等），
    /// 那是它与目标之间唯一的联系，也是构造时定下的那个值（ExeModule.cs:38）。
    /// </summary>
    internal static void StopTarget(OS os, string ip)
    {
        if (os == null || string.IsNullOrEmpty(ip))
        {
            return;
        }

        Queue.RemoveAll(pending => SameTarget(pending.Exe, ip));
        Fade(os, exe => SameTarget(exe, ip));
    }

    /// <summary>
    /// 整轮结束：清空待播队列，并让所有仍在面板上播的动画淡出。
    ///
    /// 与 <see cref="StopTarget"/> 的差别只是范围 —— 收尾时不该再有「本轮的演出」
    /// 继续拖尾，而单台机器收尾只该截断那一台。
    /// </summary>
    internal static void StopAll(OS os)
    {
        if (os == null)
        {
            return;
        }

        Queue.Clear();
        Fade(os, _ => true);
    }

    private static bool SameTarget(ExeModule exe, string ip)
        => exe != null && string.Equals(exe.targetIP, ip, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 让匹配的实例淡出，并把它们从 <see cref="Live"/> 摘除。
    ///
    /// <b>走游戏自己的淡出，而不是从 <c>os.exes</c> 里硬删。</b>置
    /// <c>isExiting = true</c> 后，<c>ExeModule.Update</c> 会在 2 秒内把 <c>ramCost</c>
    /// 线性降到 0 并置 <c>needsRemoval</c>（ExeModule.cs:68-77），由 <c>OS.Update</c>
    /// 的布局循环摘除（OS.cs:852）—— 那是游戏自己的退场路径，RAM 预算也随淡出逐帧归还。
    /// 硬删会跳过这一切：占用瞬间释放但动画突兀消失，且绕开了游戏的移除时机。
    ///
    /// 已经在 <c>os.exes</c> 里的才置位；已播完的实例跳过（它已经不在列表里，
    /// <see cref="Tick"/> 的剪枝只是还没来得及跑）。
    /// </summary>
    private static void Fade(OS os, Func<ExeModule, bool> match)
    {
        for (var i = Live.Count - 1; i >= 0; i--)
        {
            var exe = Live[i];
            if (exe == null || !match(exe))
            {
                continue;
            }

            if (os.exes.Contains(exe))
            {
                exe.isExiting = true;
            }

            Live.RemoveAt(i);
        }
    }

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

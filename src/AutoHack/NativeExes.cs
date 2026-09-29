namespace AutoHack;

using System;
using Hacknet;
using Microsoft.Xna.Framework;
using Pathfinder.Port;

/// <summary>
/// 原生破解程序的「演出」：把游戏自己的 SSHCrackExe / FTPBounceExe 等挂进 RAM 面板，
/// 让入侵过程带上原版的动画与音效。由 <c>autohack run show</c> 或面板勾选开启。
///
/// <b>端口由这些程序自己开（v1.33.3 起）。</b>9 个可安全演出的 exe 各自在
/// <c>Completed()</c> 里调 <c>openPort(&lt;自己的原始终端口号&gt;, os.thisComputer.ip)</c>
/// （SSHCrackExe.cs:229、SMTPoverflowExe.cs:169、HTTPExploitExe.cs:161、
/// FTPBounceExe.cs:153、SQLExploitExe.cs:235、MedicalPortExe.cs:110、
/// TorrentPortExe.cs:74、PacificPortExe.cs:49、RTSPPortExe.cs:80）——
/// 开的正是自己那个端口，与 <c>PortExploits.cracks</c> 的键逐一对应。
/// 而 Pathfinder 的 <c>OpenPortPrefix</c> 直接拿<b>调用方传入的原始参数</b>匹配
/// <c>Record.OriginalPortNumber</c> 后 <c>return false</c>
/// （ComputerExtensions.cs:184-197），故它与 <c>HackEngine.OpenPort</c> 落在同一个
/// <c>PortState.Cracked</c> 上。
/// <b>于是「动画跑完端口才开」不需要造机制 —— 调用方只要不再提前写。</b>
/// <c>unbreakable</c> 随机过显示端口号也不影响（匹配的是原始终端口号）。
///
/// 代价是三条<b>补开</b>责任落在调用方（见 <see cref="Show"/> 与 <see cref="Flush"/>）：
/// 没排上演出、被丢弃、被截断的端口，动画永远不会去开它。
///
/// <b>为什么必须有队列（v1.32.8）。</b>此前是「破一个端口就地挂一个 exe」，于是同时挂在
/// RAM 面板上的 exe 数量只受破解节奏限制 —— 端口步只等 <c>PortDelay</c>（当时缺省 0.6s；
/// 该常量已在 v1.34.0 随「移除全部步进间隔」一并删除，此处保留当时的事实记录），
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
/// <b>排队次序：优先放「没出现过的、内存小、时间短的」。</b>
/// 队列按 <see cref="Compare"/> 升序排：已播出次数少的在前（九种动画轮流冒头），
/// 同次数时内存小、时间短的在前，再相同则按随机数。
/// 泵只挂队首且要求装得下 —— 按代价升序贪心装箱在<b>并发有余量时</b>是吞吐最高的装法。
///
/// <b>并发度由 RAM 决定（v1.33.3 起）。</b>端口步不再等待（见 <c>HackRun.DelayFor</c>），
/// 一帧只推进一步、步与步之间零间隔，于是单台十几个动画请求在十几帧内全部入队，
/// 由泵按 <c>os.ramAvaliable</c> 能挂几个挂几个 —— 实测峰值同屏 <b>3</b> 个
/// （贪心升序装箱 190+208+210 = 608，再加 242 就超 761）。
/// 实测 167 台 / 642 可破端口：峰值 RAM 760/761、642 端口全开、0 超时、0 丢弃。
/// 超出 <see cref="MaxQueued"/> 的新请求丢弃：演出是可丢的装饰，端口由调用方补开
/// （<see cref="Show"/> 返回 false）。
///
/// <b>演出会点燃追踪。</b>9 个白名单 exe 里有 8 个调 <c>hostileActionTaken()</c>
/// （3 个在构造函数、5 个在 <c>LoadContent</c>），目标 <c>traceTime &gt; 0</c> 时即
/// 启动倒计时。泵每帧扑掉一次，理由与覆盖面见 <see cref="KillTrace"/> ——
/// 注意判据要覆盖「已挂上但队列已空」的那一刻，见 <see cref="Tick"/>。
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
    /// <summary>
    /// 队列容量。积压超此数后，新请求只有比队尾更优才挤得进来（见 <see cref="Show"/>）。
    ///
    /// <b>v1.33.3 起从 8 提到 32。</b>端口步改成零间隔入队（每帧一个）后，单台十几个端口
    /// 会在十几帧内全部涌进来，而 RAM 只允许同时挂 3 个 —— 容量 8 会让大部分请求在
    /// 挂载之前就被挤掉，演出直接演不出来。单台最多 15 个端口，跨台重叠时再翻一倍，
    /// 32 留足余量。
    ///
    /// 被挤掉的那一条会由调用方立即补开端口（<see cref="Show"/> 返回 false）——
    /// 容量只影响观感，不影响战果。
    /// </summary>
    private const int MaxQueued = 32;

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
    /// <param name="Target">这个动画要打在谁身上。截断时按它补开端口用 ——
    /// 不能反查 <c>os.connectedComp</c>，那时早已换台（见 <see cref="Show"/>）。</param>
    /// <param name="Port">这个动画负责开的那个端口。补开与等待判据都用它。</param>
    private readonly record struct Pending(
        ExeModule Exe, string ExeName, int RamCost, Life Life, float Tiebreak,
        Computer Target, PortInfo Port);

    /// <summary>
    /// 一个<b>已经挂上面板</b>的动画。与 <see cref="Pending"/> 同构，只是少了调度字段 ——
    /// 已挂上的不再参与排序。
    /// </summary>
    private readonly record struct Running(ExeModule Exe, Computer Target, PortInfo Port);

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
    private static readonly List<Running> Live = new(MaxQueued);

    /// <summary>
    /// <b>本帧</b> <c>os.exes</c> 里那些实例的集合，供 <see cref="StillOnPanel"/> 做 O(1)
    /// 存在性查询。由 <see cref="RebuildPanel"/> 填充。
    ///
    /// <b>为什么复用而不是每帧 new。</b>本类每帧都要对 <see cref="Live"/>（上限
    /// <see cref="MaxQueued"/>）做一次剪枝，原判据是 <c>os.exes.Contains(run.Exe)</c> ——
    /// 那是 O(Live × exes) 的线性扫描（最坏 32 × 5 = 160 次引用比较）。改成先一次遍历
    /// <c>os.exes</c>（通常 1~5 项）填这个集合、之后每项 O(1)。但<b>每帧 new 一个
    /// HashSet 只是把省下的比较换成每帧一次堆分配</b>，与目标相反 —— 故用 static readonly
    /// 实例 + <c>Clear()</c> 复用，全程零分配。
    ///
    /// <b>比较语义 = 引用相等，与旧判据逐位一致。</b><c>ExeModule</c>（ExeModule.cs:7）
    /// 及其基类 <c>Module</c>（Module.cs:7）都没有重写 <c>Equals</c> / <c>GetHashCode</c>，
    /// 故默认比较器走 <c>object</c> 的引用相等 —— 与 <c>List&lt;T&gt;.Contains</c> 在
    /// 未重写 Equals 时的行为相同。（net472 没有 <c>ReferenceEqualityComparer</c>，
    /// 无法也不必显式传入。）<b>若将来 ExeModule 重写了 Equals，此处必须改回引用比较</b>，
    /// 否则「同一个实例」会变成值相等，剪枝与补开端口的结果都会变。
    ///
    /// <b>只在游戏线程使用（同 <see cref="Tick"/>），不需要加锁。</b>
    /// 这条前提是硬约束：<b>唯一的写者是 <see cref="RebuildPanel"/></b>，它只被
    /// <see cref="Tick"/> 与 <see cref="Flush"/> 调用，两者都跑在游戏线程。
    /// 其它入口（尤其是 <see cref="Reset"/> —— 它由命令行线程经 HackOverlay.Open 触发）
    /// <b>不得</b>触碰本集合，否则会与 Tick 的「重建 + 读取」形成读-清竞争。
    /// </summary>
    private static readonly HashSet<ExeModule> OnPanel = new();

    /// <summary>
    /// 一个破解程序的两个时长，单位秒。这张表同时就是白名单，
    /// 键集就是可安全演出的全集 —— 白名单与时长合一而非两份，杜绝失同步。
    ///
    /// <c>CrackSeconds</c> 是 <c>Completed()</c> 被调用的时刻，也就是端口状态写好的瞬间；
    /// <c>TotalSeconds</c> 是它从构造到 <c>needsRemoval</c> 被摘除的全过程。
    /// 两者相差一个收尾停顿加 2 秒淡出
    /// （<c>fade</c> 从 1 减到 0 需要 <c>1 / FADEOUT_RATE = 2</c> 秒，ExeModule.cs:9/:76）。
    ///
    /// <b>为什么要分开。</b>端口现在由动画的 <c>Completed()</c> 去开（见 <see cref="Show"/>），
    /// 故 <c>CrackSeconds</c> 是「多久之后端口会开」—— 它是等待与观感的依据；
    /// 而调度排序关心的是「这个动画要占多久面板」，对应总存活
    /// （<see cref="Tier"/> 用的就是它）。合成一个数会让排序低估占位。
    ///
    /// 白名单的入表条件（取自 <c>PortExploits.cracks</c>，不硬编码端口号）：
    /// 在 launchExecutable 里有 case、不需要参数、且 <c>Completed()</c> 开的
    /// 正是自己那个端口。排除项与理由见类注释。
    ///
    /// 出处逐条（完成时刻取 <c>Completed()</c> 或触发它的那个比较；总存活 = 完成时刻
    /// 之后的收尾 + 2 秒淡出，逐条核过）：
    /// - TorrentStreamInjector：4.8（TorrentPortExe.cs:49）→ 16.5 + 2 = 18.5
    /// - PacificPortcrusher：6（PacificPortExe.cs:32）→ 6.2 + 2 = 8.2
    /// - SSHcrack：8（SSHCrackExe.cs:20/:98）→ 8 + 2 = 10
    /// - SQL_MemCorrupt：3+3+5+1.2 = 12.2（SQLExploitExe.cs:81-104）→ + 2 = 14.2
    /// - SMTPoverflow：12（SMTPoverflowExe.cs:10/:68）→ 12 + 0.5 + 2 = 14.5
    /// - WebServerWorm：14（HTTPExploitExe.cs:10/:72）→ 14 + 1 + 2 = 17
    /// - FTPBounce：15（FTPBounceExe.cs:8/:88）→ 15 + 2 = 17
    /// - KBT_PortTest：22（MedicalPortExe.cs:42）→ 22 + 2 + 2 = 26
    /// - RTSPCrack：6.3（RTSPPortExe.cs:8/:40）→ 30.5 + 2 = 32.5
    /// </summary>
    internal readonly record struct Life(float CrackSeconds, float TotalSeconds);

    /// <summary>白名单 + 时长表。见 <see cref="Life"/>。</summary>
    private static readonly Dictionary<string, Life> Lifetimes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TorrentStreamInjector.exe"] = new(4.8f, 18.5f),
        ["PacificPortcrusher.exe"] = new(6f, 8.2f),
        ["SSHcrack.exe"] = new(8f, 10f),
        ["SQL_MemCorrupt.exe"] = new(12.2f, 14.2f),
        ["SMTPoverflow.exe"] = new(12f, 14.5f),
        ["WebServerWorm.exe"] = new(14f, 17f),
        ["FTPBounce.exe"] = new(15f, 17f),
        ["KBT_PortTest.exe"] = new(22f, 26f),
        ["RTSPCrack.exe"] = new(6.3f, 32.5f),
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
        => p.RamCost / RamTierMb * SecondTierCount + (int)(p.Life.TotalSeconds / SecondTierSec);

    /// <summary>
    /// 为一次端口破解排一个原生动画。<paramref name="target"/> 必须正是当前连接目标 ——
    /// 否则动画会打在本机上（见类注释约束 1）。未连接、无对应程序、程序不在白名单时静默跳过；
    /// 目标已提权时也跳过（见方法内）；队列满时只有排序键比队尾更小才挤进来。
    ///
    /// <b>回传 bool 而非时长（v1.33.3 起）。</b>端口不再由调用方提前写 —— 交给动画自己的
    /// <c>Completed()</c>（那 9 个 exe 各自 <c>openPort(&lt;自己的原始终端口号&gt;, ip)</c>，
    /// 与 <see cref="HackEngine.OpenPort"/> 落在同一个 <c>PortState.Cracked</c> 上，
    /// 见类注释）。故调用方只需要知道一件事：<b>这次有没有动画可等</b>。
    ///
    /// 返回 <c>false</c> 的每一种情形，调用方都必须立即补开端口，否则它永远不开：
    /// <list type="bullet">
    /// <item>未连接目标 —— 动画会打在本机上（类注释约束 1），不能演，但端口该开；</item>
    /// <item>目标已控 —— 不排演出，但 redo 模式下端口照样要开；</item>
    /// <item>该端口没有可安全演出的程序（实测 73/642：443 / 3659 / 3724 / 9418 / 211 / 32）；</item>
    /// <item>队列已满且新请求不比队尾更优 —— 这一条最隐蔽：实例从未 <c>addExe</c>，
    ///   <c>Completed()</c> 永不执行，端口就此丢失。</item>
    /// </list>
    /// </summary>
    internal static bool Show(OS os, Computer target, PortInfo port)
    {
        if (os == null || target == null || !ReferenceEquals(os.connectedComp, target))
        {
            Trace.Write("show " + port.Protocol + " skipped: target is not the connected node");
            return false;
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
            Trace.Write("show " + port.Protocol + " skipped: " + target.ip + " is already owned");
            return false;
        }

        if (PortExploits.cracks == null || !PortExploits.cracks.TryGetValue(port.CodePort, out var exeName))
        {
            Trace.Write("show " + port.Protocol + " (" + port.CodePort + ") skipped: no native cracker for this port");
            return false;
        }

        if (!Lifetimes.TryGetValue(exeName, out var life))
        {
            Trace.Write("show " + exeName + " skipped: not in the animation whitelist");
            return false;
        }

        var exe = Create(os, exeName);
        if (exe == null)
        {
            Trace.Write("show " + exeName + " skipped: launchExecutable has no case for it");
            return false;
        }

        var pending = new Pending(exe, exeName, exe.ramCost, life, (float)Utils.random.NextDouble(), target, port);

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
                Trace.Write("show " + exeName + " dropped: queue full (" + MaxQueued + ") and this one ranks worse");
                return false;
            }

            Queue.RemoveAt(Queue.Count - 1);
        }

        Queue.Add(pending);
        Queue.Sort(Compare);
        Trace.Write("show " + exeName + " queued for " + target.ip + ", depth " + Queue.Count);
        return true;
    }

    /// <summary>
    /// 把 <c>os.exes</c> 拍进 <see cref="OnPanel"/>，供随后的一批 <see cref="StillOnPanel"/>
    /// 查询。一次遍历换一批 O(1) 查询 —— 见 <see cref="OnPanel"/> 里为什么复用实例。
    ///
    /// <b>调用时机</b>：紧挨着每个查询循环之前（<see cref="Tick"/> 的剪枝、<see cref="Flush"/>
    /// 的面板段）。循环体内只写端口状态与改本类自己的列表，都不动 <c>os.exes</c>，
    /// 故这份快照在整个循环里有效。
    /// </summary>
    private static void RebuildPanel(OS os)
    {
        OnPanel.Clear();

        if (os?.exes == null)
        {
            return;
        }

        for (var i = 0; i < os.exes.Count; i++)
        {
            OnPanel.Add(os.exes[i]);
        }
    }

    /// <summary>
    /// 该 exe 此刻是否还在 RAM 面板上 —— <b><see cref="Tick"/> 的剪枝与 <see cref="Flush"/>
    /// 的补开用的是同一条判据</b>（原注释即强调「两处一致」，抽成一处后由代码保证）。
    ///
    /// <b>前置条件：本批查询之前必须先 <see cref="RebuildPanel"/></b>，且两次调用之间
    /// 不能有东西改动 <c>os.exes</c>。不满足时读到的是上一批的快照，结果是错的 ——
    /// 故两处调用点都紧挨着各自的循环。
    ///
    /// 刻意<b>不</b>收 <c>OS</c> 参数：本方法读的是静态 <see cref="OnPanel"/>，
    /// 收一个 OS 只会让人以为「查的是那个 OS 的面板」，而传别的 OS 会静默返回上一批快照的结果。
    /// </summary>
    private static bool StillOnPanel(ExeModule exe)
        => exe != null && OnPanel.Contains(exe);

    /// <summary>
    /// 每帧推进一步：只要预算装得下队首，就把它挂上去（队首的排序键最小，见
    /// <see cref="Compare"/>）。由 <see cref="HackOverlay"/> 的 OS.Update 补丁调用。
    ///
    /// 泵本身不关心入侵是否还在推进 —— 队列何时被清由 <see cref="StopAll"/> 决定，
    /// 那个入口由 <see cref="HackRun"/> 在整轮收尾处调用。换目标时<b>不</b>截断：
    /// 端口步零间隔入队后，本台十几个动画会跨过目标边界继续播，而换目标时端口
    /// 已经全开（提权前等过，见 <see cref="Settled"/>），截断只会白丢观感。
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
        // 动画已不在面板上 = 它再也不会 Update、也就再也不会 Completed。
        // 正常播完的那批端口早已开好（Completed 里 openPort）；剩下两种是没开成的：
        // ① 被游戏的跳板门禁拦下（OS.addExe，OS.cs:2165 写 "Proxy Active -- Cannot Execute"
        //    后直接丢弃，从未进 exes）；② 被别的东西提前摘除。
        // 这两种必须在剪枝的这一刻补开 —— 晚一步就永远补不上了（见 Flush）。
        // 先把本帧的 os.exes 拍成集合，循环里就是 O(1) 查询（见 OnPanel）。
        RebuildPanel(os);

        for (var i = Live.Count - 1; i >= 0; i--)
        {
            var run = Live[i];
            if (StillOnPanel(run.Exe))
            {
                continue;
            }

            if (!IsOpen(run.Target, run.Port))
            {
                HackEngine.OpenPort(run.Target, run.Port, os.thisComputer?.ip);
            }

            Live.RemoveAt(i);
        }

        // 一帧只挂一个：os.ramAvaliable 由 OS.Update 每帧重算（OS.cs:840-859），
        // 而 addExe 只扣 exes 的累计值、不回写它。同帧连挂多个会拿同一个
        // 尚未扣减的值反复判断，挂到预算之外。60 个/秒已远快于需求。
        if (Queue.Count > 0 && Queue[0].RamCost <= os.ramAvaliable)
        {
            var head = Queue[0];
            Queue.RemoveAt(0);

            // 绕开 launchExecutable：它的位置算法是面板为空时的那一套（见类注释）。
            // 队列按升序排、队首的键最小，装不下就说明其余更装不下 ——
            // 留到预算释放后再挂，不是丢弃。
            os.addExe(head.Exe);

            // 登记归属：停播时要按实例精确摘出来（见 Live）。
            Live.Add(new Running(head.Exe, head.Target, head.Port));

            // 挂上去了才算「出现过」—— 排过队但被挤掉、丢弃、或预算不足没挂上的都不算。
            ShownCount[head.ExeName] = Count(head.ExeName) + 1;

            // 计数变了，队列里同类的键随之变大，重排一次让「刚播完的那类」立刻让位
            // 给还没露面的。队列至多 MaxQueued 项，每帧最多走这一次。
            Queue.Sort(Compare);
        }

        // 只要「本类的动画还在队列里或还在面板上」，就逐帧扑一次追踪。
        //
        // <b>不能只判队列非空。</b>LoadContent 点火的那 5 个 exe（SSHCrackExe.cs:90 等）
        // 正是在<b>挂载那一刻</b>点火的，若此刻因为队列空而提前返回，那条倒计时就会
        // 一路跑到归零。判据必须覆盖「已经挂上去的」。
        //
        // 也不能无条件调用：那样玩家自己敲命令点燃的追踪会被面板补丁一直掐掉。
        if (Queue.Count > 0 || Live.Count > 0)
        {
            KillTrace(os);
        }
    }

    /// <summary>
    /// 等待某一台上「交给动画去开」的端口全部开完；返回是否已经全部开完。
    ///
    /// <b>判据是「那个端口是否已 Cracked」，不是 exe 内部字段。</b>九种程序的完成标志
    /// 各不相同（<c>complete</c> / <c>hasCompleted</c> / <c>isComplete</c> /
    /// <c>sucsessTimer</c> / <c>elapsedTime</c>），读它们就得写一张九分支的表；
    /// 而 <c>PortState.Cracked</c> 是<b>战果本身</b>，动画一 <c>Completed()</c> 就置真
    /// （<c>openPort</c> 的 Prefix，ComputerExtensions.cs:184-197）。用结果当判据，
    /// 既不用懂九种内部状态，也自动覆盖「动画被截断/丢弃」的情形 —— 那两种由调用方
    /// 补开（见 <see cref="Flush"/>），补完这里也就返回 true。
    ///
    /// 被截断、被丢弃、或本来就没有程序的端口，调用方已用
    /// <see cref="HackEngine.OpenPort"/> 直接开掉，故不在等待名单里。
    /// </summary>
    internal static bool Settled(Computer target)
    {
        if (target == null)
        {
            return true;
        }

        foreach (var run in Live)
        {
            if (ReferenceEquals(run.Target, target) && !IsOpen(target, run.Port))
            {
                return false;
            }
        }

        foreach (var pending in Queue)
        {
            if (ReferenceEquals(pending.Target, target) && !IsOpen(target, pending.Port))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 该端口此刻是不是已破解。<b>按协议名现查端口表</b>，不用 <see cref="PortInfo.Cracked"/> ——
    /// 那是 <c>HackEngine.Ports</c> 在构建步骤时拍的快照，之后动画把端口开了它也不会变。
    ///
    /// 查不到该协议时返回 true（当作「没有可等的」）：那种端口 <c>HackEngine.OpenPort</c>
    /// 也写不进去，等下去只会卡住。
    /// </summary>
    private static bool IsOpen(Computer target, PortInfo port)
    {
        var state = target.GetPortState(port.Protocol);
        return state == null || state.Cracked;
    }

    /// <summary>
    /// 把「交出去但动画已经不可能再跑完」的端口立刻补开，返回补开的条数。
    ///
    /// <b>为什么必须补。</b>端口不再由调用方提前写，而是等动画的 <c>Completed()</c>
    /// 去开。但有三条路径会让那个回调永不执行：
    /// <list type="number">
    /// <item><b>整轮收尾</b>（<see cref="StopAll"/>）：清队列 + 置 <c>isExiting</c>，
    ///   <c>ExeModule.Update</c> 在 <c>fade &lt;= 0</c> 时置 <c>needsRemoval</c>，
    ///   之后 <c>OS.Update</c> 摘除它、<c>Update</c> 不再被调 —— 端口永不开。</item>
    /// <item><b>队列满被丢弃</b>：那个实例从未 <c>addExe</c>，也就永远不 <c>Update</c>。</item>
    /// <item><b>换 OS</b>（<see cref="Reset"/>）：连同 <c>os.exes</c> 一起丢弃。</item>
    /// </list>
    /// 不补就是<b>丢战果</b>：终端上端口看起来破了，实际 <c>PortState.Cracked</c> 仍是假。
    /// </summary>
    /// <param name="target">只补这一台；<c>null</c> = 全部。</param>
    /// <param name="force">
    /// <c>true</c> = 连<b>还在面板上播</b>的也一并开掉并摘出 <see cref="Live"/>。
    /// 只给等待超时用（见 <c>HackRun.NativeWaitCapSeconds</c>）：那时已经不再等动画了，
    /// 留着那些项会让 <see cref="Settled"/> 永远为假，于是每帧重进超时分支、回显刷屏。
    /// </param>
    internal static int Flush(OS os, Computer target, bool force = false)
    {
        if (os == null)
        {
            return 0;
        }

        var opened = 0;
        var ip = os.thisComputer?.ip;

        for (var i = Queue.Count - 1; i >= 0; i--)
        {
            var pending = Queue[i];
            if (target != null && !ReferenceEquals(pending.Target, target))
            {
                continue;
            }

            Queue.RemoveAt(i);
            if (!IsOpen(pending.Target, pending.Port))
            {
                HackEngine.OpenPort(pending.Target, pending.Port, ip);
                opened++;
            }
        }

        // 面板上的那些：**只补「已经不可能再跑完」的**，还活着的留给它自己的 Completed ——
        // 提前补会让端口抢在动画前面开（观感上「还没跑完就开了」）。
        // 「不可能再跑完」= 已经不在 os.exes 里（被游戏摘除 / 被跳板门禁拦下从未挂上）。
        // 这一条正是 Tick 剪枝用的同一判据 —— 两处都走 StillOnPanel，一致性由代码保证。
        //
        // 快照按需重建：全仓两处 Flush 调用点都传 force:true（HackRun 的等待超时、StopAll），
        // 那条路径根本不看面板状态，先 RebuildPanel 就是白付一次 O(os.exes) 遍历。
        // 这里再拍一次而不是复用 Tick 那一次：本方法可能在一帧内被多次调用，Tick 的快照已过期。
        if (!force)
        {
            RebuildPanel(os);
        }

        for (var i = Live.Count - 1; i >= 0; i--)
        {
            var run = Live[i];
            if (target != null && !ReferenceEquals(run.Target, target))
            {
                continue;
            }

            // 不 force 时只补「已经不可能再跑完」的（不在 os.exes 里）；force 时全补。
            if (!force && StillOnPanel(run.Exe))
            {
                continue;
            }

            if (!IsOpen(run.Target, run.Port))
            {
                HackEngine.OpenPort(run.Target, run.Port, ip);
                opened++;
            }

            Live.RemoveAt(i);
        }

        return opened;
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
    /// <b>刻意不补开端口。</b>换 OS 时队列里排的是<b>旧 OS</b> 的机器，而此刻能拿到的
    /// 只有新 OS 的玩家 IP —— 拿它去开旧机器的端口是错的。而旧 OS 已经整个被丢弃
    /// （回主菜单再进档），那些端口开不开都不再有意义。
    internal static void Reset()
    {
        Queue.Clear();
        Live.Clear();
        ShownCount.Clear();

        // <b>刻意不清 OnPanel。</b>两个理由：
        // ① 没必要 —— 它的每个读点（Tick 的剪枝、Flush 的补开）之前都紧接一次
        //    RebuildPanel，读到的必是本批快照，旧 OS 的残留引用结构上不可能被读到。
        // ② 有代价 —— Reset 由 HackOverlay.Open 调用，而 Open 的触发路径
        //    （AutoHackCommand → HackOverlay.Toggle）跑在 OS.execute 派生的**命令行线程**上
        //    （OS.cs:1754-1767），Tick 在游戏线程。在此处 Clear 会与 Tick 的
        //    RebuildPanel + 读取循环形成读-清竞争，让仍在播的动画被误判为「已离开面板」。
    }

    /// <summary>
    /// 整轮结束：把还没开的端口补开，再让所有仍在面板上播的动画淡出。
    ///
    /// <b>必须先补开、后淡出</b>（v1.33.3）：端口现在由动画的 <c>Completed()</c> 去开，
    /// 而淡出会置 <c>isExiting</c> → 2 秒后 <c>needsRemoval</c> → 被 <c>OS.Update</c> 摘除，
    /// <c>Update</c> 不再被调 ⇒ <c>Completed()</c> 永不执行。先淡出就是丢战果。
    /// </summary>
    internal static void StopAll(OS os)
    {
        if (os == null)
        {
            return;
        }

        // force: true 是必需的 —— 非 force 只补「已经不在 os.exes 里」的，而此刻
        // 正在播的那些<b>还在</b> exes 里；紧接着的 Fade 把它们淡出后，
        // 它们再也不会 Completed（淡出完就被摘除），端口就此丢失。
        Flush(os, null, force: true);
        Fade(os, _ => true);
    }

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
            var exe = Live[i].Exe;
            if (exe == null || !match(exe))
            {
                continue;
            }

            // 这里**刻意**不用 StillOnPanel：本方法由 StopAll 在 Flush(force:true) 之后调用，
            // 而 force 路径不重建 OnPanel（见 Flush），拿到的会是过期快照 —— 用它会把
            // 「还在播的」判成「已离开面板」，于是不置 isExiting，动画不再淡出。
            // 直接查 os.exes 才是这条路径上的真相；它每轮只跑一次，不在每帧热路径上。
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

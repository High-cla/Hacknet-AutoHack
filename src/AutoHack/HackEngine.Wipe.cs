// HackEngine.Wipe.cs —— 日志清痕：识别、删除与全网扫描。
//
// HackEngine 的分部实现；类型声明、字段与其余职责见 HackEngine.cs。
namespace AutoHack;

using Hacknet;
using Pathfinder.Port;
using Pathfinder.Util;

internal static partial class HackEngine
{

    /// <summary>
    /// 抹掉<b>玩家自己留下的</b>痕迹 —— /log 里含玩家 IP 的那些条目。
    ///
    /// <b>为什么按 IP 过滤，而不是清空整个 /log。</b>/log 是那台机器自己的操作史
    /// （谁连过它、它读过什么文件），整目录删掉属于改写对方状态。玩家真正要的是
    /// 「别让痕迹指向我」，而指向我的判据就是条目里的那个 IP。
    ///
    /// <b>这个口径恰好与游戏的追踪判据同构。</b>
    /// <c>TrackerCompleteSequence.CompShouldStartTrackerFromLogs</c>
    /// （TrackerCompleteSequence.cs:30-47）扫的正是「<c>data.Contains(玩家IP)</c>
    /// 且含 <c>FileCopied</c>/<c>FileDeleted</c>/<c>FileMoved</c>」，命中即排一个脱机追踪。
    /// 故按 IP 过滤<b>既不误伤对方历史，又刚好让追踪判据失配</b>。
    ///
    /// <b>刻意不删其他条目。</b>断开时目标机 /log 会写入
    /// <c>"&lt;玩家IP&gt; Disconnected"</c>（Computer.cs:722-727）—— 它同样含玩家 IP，
    /// 会被一起清掉；而 <c>"admin logged in"</c> 这类不含玩家 IP 的条目一律保留，
    /// 那些是目标机自己的历史，不是我们的痕迹。
    ///
    /// 玩家机自己传 <c>os.thisComputer</c> 进来时语义相同：/log 里凡提到自己 IP 的
    /// 条目（<c>"&lt;目标IP&gt; Disconnected"</c> 等）都算自己的痕迹。
    /// </summary>
    /// <returns>被删掉的条目名快照，供回显与计数。</returns>
    internal static IReadOnlyList<string> WipeTraces(Computer comp, string ipFrom)
    {
        var root = comp?.files?.root;
        var logFolder = root?.searchForFolder(LogFolderName);
        if (root == null || logFolder == null || string.IsNullOrEmpty(ipFrom))
        {
            return Array.Empty<string>();
        }

        // 相对 files.root 的索引路径 —— 用 IndexOf 实求，不硬编码 log 的位置。
        // Folder 未重写 Equals（Folder.cs:15），IndexOf 是引用比较，而 logFolder
        // 正是从 root.folders 里取出来的，故必然命中其真实下标。
        List<int> folderPath = [root.folders.IndexOf(logFolder)];

        // 先快照待删名单，再交给 RemoveFiles 逐条点名删 —— 逐条走
        // Computer.deleteFile(ipFrom, <名>, path)，保住游戏的权限门禁与联机同步
        // （cDelete，Computer.cs:563-570）。不传 "*"：那个分支会把目标机自己的
        // 历史一起删掉，正是本版要避免的。
        var doomed = new List<string>();
        foreach (var file in logFolder.files)
        {
            if (file != null && Mentions(file.data, ipFrom))
            {
                doomed.Add(file.name);
            }
        }

        return RemoveFiles(comp, ipFrom, logFolder, folderPath, doomed);
    }

    /// <summary>
    /// 全网清痕：把地图上<b>所有</b>机器 /log 里提到玩家 IP 的条目一次清掉。
    /// 返回删掉的条目数与波及的机器数。
    ///
    /// <b>为什么需要它。</b>按目标展开的 <c>CleanLogs</c> 步只覆盖本轮的目标池 ——
    /// 玩家此前访问过、但不在池里的机器（换过分量、早前几轮打过的）一条都清不掉。
    /// 而痕迹是<b>累积</b>的：<c>Computer.log</c> 在每次连接、读文件、断开时都追加，
    /// 只要有一行同时含玩家 IP 与 <c>FileCopied</c>/<c>FileDeleted</c>/<c>FileMoved</c>，
    /// 那台机器就会在断开时自动排一个脱机追踪
    /// （<c>TrackerCompleteSequence.cs:30-47</c>）。玩家 IP 不变时，这些历史痕迹
    /// 始终有效 —— 收尾清一次是唯一的解法。
    ///
    /// <b>为什么遍历全图而不是可达闭包。</b>痕迹不看连通性：地图上任何一台机器都可能是
    /// 玩家直接敲 IP 连过的（<c>Programs.connect</c> 全程不检查 visibleNodes，
    /// Programs.cs:231-322）。范围取舍由调用方定 —— 面板「当前节点」用
    /// <see cref="SilentClosure"/> 沿连线取分量，全网扫描用全表。
    ///
    /// <b>跳过 disabled 与没有 /log 的机器</b>：前者 <c>Computer.log</c> 直接 return
    /// （Computer.cs:321-324），后者 <c>searchForFolder("log")</c> 返回 null ——
    /// <see cref="WipeTraces"/> 对两者本就是幂等的空操作，这里的守卫只为省掉无谓遍历。
    /// </summary>
    internal static (int Entries, int Machines) WipeEverything(IEnumerable<Computer> comps, string ipFrom)
    {
        if (comps == null || string.IsNullOrEmpty(ipFrom))
        {
            return (0, 0);
        }

        var entries = 0;
        var machines = 0;
        var seen = new HashSet<Computer>();

        foreach (var comp in comps)
        {
            if (comp == null || comp.disabled || !seen.Add(comp))
            {
                continue;
            }

            var wiped = WipeTraces(comp, ipFrom);
            if (wiped.Count == 0)
            {
                continue;
            }

            entries += wiped.Count;
            machines++;
        }

        return (entries, machines);
    }

    /// <summary>
    /// 全网清痕的入口：按当前 scope 取机器池，清完回显一行。
    ///
    /// <b>范围随 scope 走</b>（用户定）：
    /// <list type="bullet">
    /// <item>「当前节点」= 沿连线取当前节点所在的无向连通分量
    ///   （<see cref="SilentClosure"/>，<b>不</b>揭图 —— 清痕不是侦察）；</item>
    /// <item>全网扫描 = 地图全表（<see cref="ConnectableComputers"/>）。</item>
    /// </list>
    /// 两者都含玩家机 —— <see cref="Closure"/> 会剔除 <c>os.thisComputer</c>，
    /// 而玩家机的 /log 记着「谁连过我」，正是自己的痕迹（见 llms.txt 坑 9），
    /// 故单独补回来。
    ///
    /// <b>排在换 IP 之前</b>（调用方负责）：判据是玩家<b>当前</b>的 IP，
    /// 换完之后旧 IP 就不再指向玩家，这些痕迹反而清不掉了。
    /// </summary>
    /// <summary>
    /// 一次运行的收尾清痕：按 scope 决定范围，<c>keep</c> 时不做事。
    ///
    /// 范围（用户定）：<b>只有「当前节点」模式走窄口径</b>（沿连线取连通分量），
    /// 其余（网络扫描 / 全网 / 显式点名 / 脚本）一律地图全表 —— 玩家显式聚焦一台机器时
    /// 才不该惊动别处，其余场景「我的痕迹」本就是全网概念。
    /// </summary>
    internal static void WipeForRun(OS os, HackOptions options)
    {
        if (options == null || !options.WipeTraces)
        {
            return;
        }

        WipeNetwork(os, options.Scope != HackScope.Connected, announce: false);
    }

    /// <summary>
    /// 全网清痕的入口：按给定范围清掉含玩家 IP 的 /log 条目，清完回显一行。
    /// 面板工具与运行收尾共用这一份实现，回显措辞只写一次。
    /// </summary>
    /// <param name="allNodes">true = 地图全表；false = 当前节点所在的无向连通分量。</param>
    /// <param name="announce">
    /// 是否把统计写进终端。<c>WipeForRun</c> 传 <c>false</c> —— 收尾清痕是自动动作，
    /// 不是玩家敲的命令（用户定：终端里只要具体的命令行输出）。
    /// 显式工具 <c>autohack wipe</c> 与面板按钮走默认值，照常回显。
    /// </param>
    internal static void WipeNetwork(OS os, bool allNodes, bool announce = true)
    {
        var self = os?.thisComputer;
        if (self == null)
        {
            return;
        }

        var pool = allNodes
            ? ConnectableComputers(os)
            : SilentClosure(os, os.connectedComp ?? self);

        // 玩家机不在连通分量里（Closure 显式剔除），单独补上。
        var comps = new List<Computer>(pool.Length + 1);
        comps.AddRange(pool);
        comps.Add(self);

        var (entries, machines) = WipeEverything(comps, self.ip);
        if (announce)
        {
            os.write("[autohack] wiped " + entries + " trace entr" + (entries == 1 ? "y" : "ies")
                + " across " + machines + " node(s)"
                + (allNodes ? " (whole map)." : " (linked component)."));
        }
    }

    /// <summary>
    /// 该日志条目是否提到这个 IP。
    ///
    /// <b>比游戏自己的判据严一格。</b><c>TrackerCompleteSequence.cs:41</c> 用的是裸
    /// <c>data.Contains(targetIP)</c>，会把 <c>"156.151.1.12"</c> 认成提到
    /// <c>"156.151.1.1"</c>。游戏那样写只是<b>触发</b>一次追踪（误判代价是白紧张一场），
    /// 而这里是要<b>删</b>东西 —— 误判会删掉一条根本不属于我们的记录，正是本版要避免的
    /// 「改写对方状态」。实测存档 167 个 IP 里就存在一对这种前缀关系
    /// （<c>156.151.1.1</c> / <c>156.151.1.12</c>），不是假想。
    ///
    /// 故命中处的前后各留一个字符做边界：两侧都不是数字或点才算真命中。
    /// </summary>
    private static bool Mentions(string data, string ip)
    {
        if (string.IsNullOrEmpty(data) || string.IsNullOrEmpty(ip))
        {
            return false;
        }

        for (var at = data.IndexOf(ip, StringComparison.OrdinalIgnoreCase); at >= 0;
             at = data.IndexOf(ip, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            if (!IsIpChar(data, at - 1) && !IsIpChar(data, at + ip.Length))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>该位置是否是 IP 的组成部分（数字或点）。越界视为否。</summary>
    private static bool IsIpChar(string data, int index)
        => index >= 0 && index < data.Length && (char.IsDigit(data[index]) || data[index] == '.');

    /// <summary>
    /// 删光一个目录下的文件 —— 痕迹清理（<see cref="WipeTraces"/>，传名单）与显式删除
    /// （面板 <c>purge</c>）共用的唯一通道。
    ///
    /// 为什么要共用：两者原本各写一份，一份带兜底、一份不带，于是同一个动作在两条
    /// 入口下行为不一致。根因是 <c>Computer.deleteFile</c> 的权限门禁
    /// （Computer.cs:511-517）在拒绝时**静默返回 false** —— 不删、不报错、不写日志，
    /// 调用方只能靠复核结果发现，表现为「按了没反应」。
    ///
    /// 两段式：先走游戏原语，再无条件复核。
    /// <list type="number">
    /// <item><c>comp.deleteFile(ipFrom, "*", folderPath)</c> —— 保留游戏的权限语义、
    /// 联机同步（<c>cDelete</c> 消息，Computer.cs:563-570）与审计日志
    /// （<c>FileDeleted: ...</c>，目标名以 '@' 开头时游戏自己会跳过，Computer.cs:543）。
    /// 这就是 <c>Programs.rm</c> 真正的动作（Programs.cs:1024 只调这一句）。
    /// 该方法的 <c>"*"</c> 分支先快照文件名再逐个递归，故遍历中删除不会漏项。</item>
    /// <item>无条件清空 —— 不看返回值、也不怕它抛。<c>"*"</c> 分支是
    /// <c>flag2 &amp;= deleteFile(...)</c> 逐个递归后返回 <c>flag2</c>：若 <c>folderPath</c>
    /// 解析偏了，它会去删别的文件夹并照样返回 true；权限门禁拒绝时只是静默 false；
    /// 目标机没有 <c>log</c> 夹时它还会在 <c>log()</c> 里直接 NRE。删除是硬承诺，
    /// 不能建立在「返回值可信」之上，也不能建立在「它不会抛」之上。</item>
    /// </list>
    /// </summary>
    /// <returns>被删除的文件名快照（与 <c>"*"</c> 分支同一过滤条件），供回显与计数。</returns>
    /// <param name="only">
    /// 只删这些名字；<c>null</c> = 全删（面板 <c>purge</c> 用的就是这个口径）。
    /// 传名单时逐条点名走 <c>deleteFile</c>，保住权限门禁与联机同步；
    /// 传 <c>null</c> 时走 <c>"*"</c> 分支 —— 它先快照再逐个递归，遍历中删除不会漏项。
    /// </param>
    internal static IReadOnlyList<string> RemoveFiles(
        Computer comp, string ipFrom, Folder folder, List<int> folderPath, IReadOnlyCollection<string> only = null)
    {
        if (comp == null || folder == null || folder.files.Count == 0)
        {
            return Array.Empty<string>();
        }

        // 快照待删文件名（与 deleteFile 的 "*" 分支同一过滤条件），供回显与计数。
        var removed = SnapshotDoomed(folder, only);

        if (removed.Count == 0)
        {
            return Array.Empty<string>();
        }

        if (only == null)
        {
            DeleteAll(comp, ipFrom, folder, folderPath);
        }
        else
        {
            DeleteNamed(comp, ipFrom, folder, folderPath, removed);
        }

        return removed;
    }

    /// <summary>快照待删文件名：跳过空白名，传名单时只留名单内的名字。</summary>
    private static List<string> SnapshotDoomed(Folder folder, IReadOnlyCollection<string> only)
    {
        var removed = new List<string>(folder.files.Count);
        foreach (var file in folder.files)
        {
            if (string.IsNullOrWhiteSpace(file?.name))
            {
                continue;
            }

            if (only == null || only.Contains(file.name))
            {
                removed.Add(file.name);
            }
        }

        return removed;
    }

    /// <summary>全删路径：走 <c>"*"</c> 原语，随后无条件清空目录。</summary>
    private static void DeleteAll(
        Computer comp, string ipFrom, Folder folder, List<int> folderPath)
    {
        // 游戏原语不只「返回 false」，它还会**抛异常**：deleteFile 对每个非 '@' 开头的
        // 文件调 log()，而 log() 直接 files.root.searchForFolder("log").files.Insert(...)
        // （Computer.cs:338-354）—— 目标机没有 log 夹时 searchForFolder 返回 null，
        // 下一行就是 NRE。异常若外溢，「无条件清空」永不执行，表现仍是「按了没反应」。
        // 故兜住它：原语是「尽量走」，下沉才是硬承诺。
        try
        {
            comp.deleteFile(ipFrom, "*", folderPath);
        }
        catch (Exception ex) when (ex is NullReferenceException or ArgumentOutOfRangeException
                                       or IndexOutOfRangeException or ArgumentException)
        {
            // 原语中途失败不影响下沉：已删的已删，剩下的由下面清空，终态一致。
            // 代价是多人同步消息可能少发一次 —— 比「什么都不删」可接受。
        }

        // 无条件复核，两条路径同一终态：原语的返回值不可信（权限拒绝时静默 false；
        // folderPath 解析偏了会去删别的文件夹并照样返回 true），删除是硬承诺。
        folder.files.Clear();
    }

    /// <summary>点名路径：逐条走 <c>deleteFile</c> 原语，随后按名无条件复核剔除。</summary>
    private static void DeleteNamed(
        Computer comp, string ipFrom, Folder folder, List<int> folderPath, List<string> removed)
    {
        // 游戏原语不只「返回 false」，它还会**抛异常**：deleteFile 对每个非 '@' 开头的
        // 文件调 log()，而 log() 直接 files.root.searchForFolder("log").files.Insert(...)
        // （Computer.cs:338-354）—— 目标机没有 log 夹时 searchForFolder 返回 null，
        // 下一行就是 NRE。异常若外溢，「无条件清空」永不执行，表现仍是「按了没反应」。
        // 故兜住它：原语是「尽量走」，下沉才是硬承诺。
        try
        {
            foreach (var name in removed)
            {
                comp.deleteFile(ipFrom, name, folderPath);
            }
        }
        catch (Exception ex) when (ex is NullReferenceException or ArgumentOutOfRangeException
                                       or IndexOutOfRangeException or ArgumentException)
        {
            // 原语中途失败不影响下沉：已删的已删，剩下的由下面清空，终态一致。
            // 代价是多人同步消息可能少发一次 —— 比「什么都不删」可接受。
        }

        // 无条件复核，两条路径同一终态：原语的返回值不可信（权限拒绝时静默 false；
        // folderPath 解析偏了会去删别的文件夹并照样返回 true），删除是硬承诺。
        foreach (var name in removed)
        {
            for (var i = 0; i < folder.files.Count; i++)
            {
                if (string.Equals(folder.files[i]?.name, name, StringComparison.Ordinal))
                {
                    folder.files.RemoveAt(i);
                    i--;
                }
            }
        }
    }
}

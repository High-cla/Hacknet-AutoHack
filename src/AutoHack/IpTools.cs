namespace AutoHack;

using Hacknet;
using Pathfinder.Util;

/// <summary>
/// 重置玩家机 IP —— 游戏原生的「换 IP 保命」动作，附带肉鸡标记迁移。
///
/// <b>原生依据</b>：<c>ISPDaemon.DrawIPEntryScreen</c>（ISPDaemon.cs:122-142）是游戏里
/// 唯一给玩家换 IP 的入口（「Assign New IP」按钮）。它做三件事：
/// <list type="number">
/// <item>循环 <c>NetworkMap.generateRandomIP()</c> 直到新 IP 不与 <c>os.netMap.nodes</c>
///   里任何其它节点冲突（判据含 <c>idName != scannedComputer.idName</c>，防止撞上自己）；</item>
/// <item><c>scannedComputer.ip = 新IP</c>；</item>
/// <item>若是玩家机，调 <c>os.thisComputerIPReset()</c>（OS.cs:1791-1802）—— 它停掉
///   <c>traceTracker</c>、通知 <c>TraceDangerSequence</c> 危机已解除、并把
///   <c>thisComputer.adminIP</c> 同步成新 IP。</item>
/// </list>
/// 本类照抄这三步，不另立规则。
///
/// <b>为什么要重建查找表（换完 IP 连不上自己的根因）</b>：Pathfinder 用
/// <c>ComputerLookup</c>（<c>decompiled/pathfinder/Pathfinder.Util/ComputerLookup.cs</c>）
/// 替掉了游戏本体的节点查找 —— 它以 IL 注入接管 <c>Programs.connect</c>，改为
/// <c>netMap.nodes.IndexOf(ComputerLookup.Find(args[1], Ip | Name))</c>
/// （<c>Pathfinder.BaseGameFixes.Performance/NodeLookup.cs:281-343</c>），
/// 索引为 -1 即视为「找不到这台机器」。而该表的 <c>Add</c> 写作
/// <c>if (!ipLookup.ContainsKey(node.ip))</c>（<c>:28-42</c>）—— <b>首次写入即固定</b>，
/// 同一 IP 不再覆盖。故只改 <c>self.ip</c> 而不重建：表里「旧 IP → 玩家机」的旧映射会留下，
/// 「新 IP → 玩家机」则缺席；<c>connect</c> 自己的新 IP 查表为空 → 索引 -1 → 连不上自己。
/// Pathfinder 自己就是这么修的：<c>SAChangeIP.Trigger</c> 的 Postfix 在 IP 变化后无条件调
/// <c>ComputerLookup.RebuildLookups()</c>（<c>NodeLookup.cs:39-48</c>），ISP 界面则在绘制
/// 方法内注入重建（<c>:144-186</c>）。本类照做 —— <c>RebuildLookups</c> 是 public 静态方法
/// （<c>:14</c>），无需反射，传 null 即按 <c>OS.currentInstance.netMap.nodes</c> 重建。
///
/// <b>为什么要同步 adminIP</b>：<c>adminIP</c> 是「谁是这台机器的管理员」的标记，
/// 游戏在 <c>OS.cs:383</c> 初始化时令玩家机 <c>adminIP = ip</c>；<c>ISPDaemon</c> 换 IP
/// 后由 <c>thisComputerIPReset</c> 重新对齐。不跟着改，玩家机自己就会「不是自己的
/// 管理员」—— <c>Computer.PlayerHasAdminPermissions</c>（Computer.cs:1670）拿
/// <c>adminIP == os.thisComputer.ip</c> 判权限，两边一旦脱节，玩家在自己机器上会失去
/// admin。
///
/// <b>代价（刻意保留，不是缺陷）</b>：换 IP 会让<b>全部已控肉鸡的标记失效</b>。
/// 提权时游戏把玩家当时的 IP 写进目标机（<c>Computer.cs:740 adminIP = ipFrom</c>），
/// 判据是 <c>target.adminIP == os.thisComputer.ip</c>
/// （<c>PlayerHasAdminPermissions</c>，mod 侧同一判据见 <see cref="HackEngine.IsOwned"/>）。
/// 玩家 IP 一变，先前写入的记录全部对不上，扫描时那些机器会被当成「未拿下」。
/// 这是游戏原生设定的固有代价 —— <c>TraceDangerSequence</c> 的台词正是
/// 「换 IP 保命，但丢账户数据」（TraceDangerSequence.cs:295-302），本工具不试图绕过它，
/// 只把代价如实写进回显。
///
/// 但**归属会整体迁移**：换 IP 前，把<b>全图</b>所有 <c>adminIP == 旧 IP</c> 的机器
/// 改成新 IP（见 <see cref="MigrateOwnership"/>）。只迁移本轮目标是不够的 ——
/// 玩家上一轮跑出的那几十台肉鸡同样标记着旧 IP，不一起改就当场失效，
/// 而「自动换 IP」若每轮都清空玩家的战绩，对玩家就是净负。
///
/// 判据 <c>adminIP == 旧玩家IP</c> 的语义本就是「这台机器的管理员是玩家」，玩家换了
/// IP，自己控制的机器当然跟认新主人 —— 迁移是把这个语义落到实处，不是发明新规则。
/// 只动确实标记着旧 IP 的机器，故不会误改别人的归属。
/// </summary>
internal static class IpTools
{
    /// <summary>
    /// 给玩家机换一个新 IP，并把全部已控机器的归属迁移过去。
    /// </summary>
    /// <param name="os">游戏状态。</param>
    /// <returns>回显用的结果描述；未执行则返回 null（调用方不输出）。</returns>
    internal static string Reset(OS os)
    {
        var self = os?.thisComputer;
        if (self == null)
        {
            return null;
        }

        var previous = self.ip;
        var next = UniqueIP(os, self);

        // 迁移必须在改 IP 之前：改完之后 adminIP == previous 这个判据就再也对不上了。
        var migrated = MigrateOwnership(os, self, previous, next);

        self.ip = next;

        // 走游戏自己的收尾：停追踪 + 通知危机序列 + 把 thisComputer.adminIP 同步成新 IP。
        // 不手写这三件事 —— 它们是 OS 的职责，且 TraceDangerSequence 那段有状态机副作用。
        os.thisComputerIPReset();

        // 让 Pathfinder 的查找表跟上新 IP。必须排在 self.ip = next 之后 ——
        // RebuildLookups 按传进来的节点表的当前 ip 重建。显式传 os 自己的图，
        // 不依赖无参重载的全局 OS.currentInstance（见类文档）。
        //
        // 节点表缺席则跳过：RebuildLookups 对 null 直接抛（ComputerLookup.cs:20），
        // 而这是收尾路径，抛出去会打断整轮回显。守卫与 UniqueIP / MigrateOwnership 一致。
        if (os.netMap?.nodes is { } nodes)
        {
            ComputerLookup.RebuildLookups(nodes);
        }

        return previous + " -> " + next + " (" + migrated + " owned node(s) re-tagged)";
    }

    /// <summary>
    /// 生成一个不与地图上任何其它节点冲突的新 IP。
    /// 判据照抄 ISPDaemon.cs:127-137 —— 含 <c>idName</c> 比较，故不会撞上自己。
    /// </summary>
    private static string UniqueIP(OS os, Computer self)
    {
        string candidate;
        var attempt = 0;

        do
        {
            candidate = NetworkMap.generateRandomIP();
            attempt++;
        }
        while (attempt < 64 && Conflicts(os, self, candidate));

        return candidate;
    }

    private static bool Conflicts(OS os, Computer self, string candidate)
    {
        var nodes = os.netMap?.nodes;
        if (nodes == null)
        {
            return false;
        }

        foreach (var node in nodes)
        {
            if (node != null && node.ip == candidate && node.idName != self.idName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 把全图（<c>os.netMap.nodes</c>）里「adminIP 等于旧玩家 IP」的机器改成新 IP，
    /// 返回迁移台数；玩家机自身也在图里（OS.cs:384 令其 idName = "playerComp"），
    /// 由 <c>thisComputerIPReset</c> 另行对齐，此处跳过以免重复。
    /// </summary>
    private static int MigrateOwnership(OS os, Computer self, string previous, string next)
    {
        var nodes = os.netMap?.nodes;
        if (nodes == null)
        {
            return 0;
        }

        var migrated = 0;
        foreach (var comp in nodes)
        {
            if (comp != null && !ReferenceEquals(comp, self) && comp.adminIP == previous)
            {
                comp.adminIP = next;
                migrated++;
            }
        }

        return migrated;
    }
}

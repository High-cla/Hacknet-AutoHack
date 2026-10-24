namespace AutoHack.Core;

/// <summary>
/// 规划期需要的网络视图：能按 IP 取快照、能枚举目标池。
///
/// <b>闭包为什么不在核心算</b>：游戏那边的遍历是「走一条边就把该节点标成已发现」——
/// 读与写是同一次动作（<c>NetworkMap.discoverNode</c> 直接改 <c>visibleNodes</c>）。
/// 拆成「核心算 ids、适配器再逐个揭示」会经历一个中间态：ids 已定、地图还没亮，
/// 而目标池的构造就发生在两者之间。故闭包连同揭示一起留在适配器里，核心只要求
/// 一个已经算好的目标池。<c>tools/bench.mts</c> 的扩展性表说的就是这个遍历。
/// </summary>
internal interface INetworkView
{
    /// <summary>该 IP 的快照；机器不存在时返回 null。</summary>
    TargetView Get(string ip);

    /// <summary>
    /// 按选项解析出的目标池（顺序稳定）。
    /// <paramref name="allNodes"/> 为真取地图全表，否则沿连线取可达集合；
    /// <paramref name="origin"/> 只对单源口径有意义（当前连接节点）。
    /// </summary>
    IReadOnlyList<TargetView> Targets(HackOptions options, string originIp);

    /// <summary>玩家自己的机器（清痕与换 IP 都要它）。</summary>
    TargetView Self { get; }
}

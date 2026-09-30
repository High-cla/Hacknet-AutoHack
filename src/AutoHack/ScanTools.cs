namespace AutoHack;

using Hacknet;

/// <summary>
/// 扫描：把网络标到地图上。两个口径 —— <b>连通分量</b>（缺省）与<b>地图全表</b>
/// （<c>allnodes</c>，见 <see cref="Run"/>）。
///
/// <para>分量口径与游戏自己 <c>scan</c>（Programs.cs:1258-1299）的差别，逐条都是刻意的：</para>
///
/// <list type="number">
/// <item>走<b>无向</b>闭包。原生只遍历 <c>connectedComp.links</c>（出边，Programs.cs:1282），
///    而 <c>&lt;link&gt;</c> 与 <c>&lt;dlink&gt;</c> 都只写自己那一侧的出边
///    （ComputerLoader.cs:344-373 —— dlink 只是延迟解析，方向不变），于是
///    「别的机器指向当前节点」的入边永远扫不到。闭包口径复用
///    <see cref="HackEngine.ReachableFrom"/>（同一套 BFS，只是<b>单源</b>），
///    含 EOS 设备那批 <c>attatchedDeviceIDs</c> 反向补边，不另立第二套遍历。
///    <b>必须单源</b>：多源口径（<see cref="HackEngine.ReachableComputers"/>，供 <c>run</c>）
///    以玩家机 + 全部 visibleNodes 播种，取到的是所有已发现分量的并集，不是一张分量。</item>
/// <item>不睡。原生逐条 <c>Thread.Sleep(400)</c>（Programs.cs:1291），扫 30 台就卡死
///    12 秒，而这是在游戏线程上。发现动作本身只是内存写，不需要节流。</item>
/// <item>不设 admin 门禁。原生 <c>hasConnectionPermission(admin: true)</c>（Programs.cs:1279）
///    的本意是「没拿下目标机就别看它的邻居」，而本工具只读玩家自己存档里的图，
///    不改任何第三方状态（标「已发现」不算入侵）。</item>
/// <item>用原生 <c>NetworkMap.discoverNode</c>（NetworkMap.cs:415-423）标记，
///    与游戏自身的「已发现」同源，不做自绘的伪发现。</item>
/// </list>
///
/// <para><b>全表口径与「连线」无关。</b>它逐个标记 <c>os.netMap.nodes</c> 全表，
/// 走 <see cref="HackEngine.RevealWholeMap"/> —— 与 <c>autohack run allnodes</c> 的
/// 「先揭图后入侵」是同一份实现，不另立第二套。语义见
/// <see cref="HackEngine.ConnectableComputers"/>：<c>visibleNodes</c> 只是原版 <c>scan</c>
/// 维护的「已发现」展示标记，不是连接许可，<c>Programs.connect</c> 全程不检查它。</para>
///
/// <para><b>全表口径不需要已连接</b>，分量口径需要（没连接就没有「当前节点」）。</para>
///
/// <para>回显在这里统一：命令入口经 <c>OS.execute</c> 跑在独立线程、面板入口跑在
/// 游戏线程，两处都只写 <c>os.write</c>，格式一致。</para>
/// </summary>
internal static class ScanTools
{
    /// <param name="allNodes"><c>true</c> = 地图全表；<c>false</c> = 当前节点所在的无向连通分量。
    /// 命令行走 <c>autohack scan allnodes</c>，面板走 <c>whole map</c> 复选框。</param>
    internal static void Run(OS os, bool allNodes)
    {
        if (os?.netMap?.nodes == null)
        {
            return;
        }

        if (allNodes)
        {
            var marked = HackEngine.RevealWholeMap(os);
            os.write("[autohack] scan: whole map :: " + marked + " node(s) revealed.");
            return;
        }

        // 单源：只取「当前节点」所在的那一张连通分量。
        // 此前这里用的是多源口径（玩家机 + 全部 visibleNodes），取出的不是分量而是
        // 「所有已发现分量的并集」—— 实测同一存档多扫出 56 台与当前节点毫无连线的机器。
        // 本工具的回显一直自称 "this component"，现在代码才真的与它一致。
        var origin = os.connectedComp;
        if (origin == null)
        {
            // 没连接就没有「当前节点」。此前回落到玩家机，而玩家机的 links 常年为空
            // （实测存档里 169 台有 118 台 links 为空），于是它自己就是一张单机分量 ——
            // 扫完只标出玩家机一台，看起来像「什么都没发现」。明说原因比给个空结果好。
            // 全表口径不受这条约束，故提示里直接给出出路。
            os.write("[autohack] scan: not connected to any node - connect to one first,"
                     + " or use 'scan allnodes' to reveal the whole map.");
            return;
        }

        var before = os.netMap.visibleNodes?.Count ?? 0;
        var reachable = HackEngine.ReachableFrom(os, origin);
        var added = (os.netMap.visibleNodes?.Count ?? 0) - before;

        os.write("[autohack] scan: " + origin.name
                 + " :: " + reachable.Length + " node(s) in this component, "
                 + added + " newly revealed.");
    }
}

namespace AutoHack;

using Hacknet;

/// <summary>
/// 扫描：把「当前节点所在的那张网络连通分量」整体标到地图上。
///
/// 与游戏自己 <c>scan</c>（Programs.cs:1258-1299）的差别，逐条都是刻意的：
///
/// 1. 走**无向**闭包。原生只遍历 <c>connectedComp.links</c>（出边，Programs.cs:1282），
///    而 <c>&lt;link&gt;</c> 与 <c>&lt;dlink&gt;</c> 都只写自己那一侧的出边
///    （ComputerLoader.cs:344-373 —— dlink 只是延迟解析，方向不变），于是
///    「别的机器指向当前节点」的入边永远扫不到。闭包口径直接复用
///    <see cref="HackEngine.ReachableComputers"/>，含 EOS 设备那批
///    <c>attatchedDeviceIDs</c> 反向补边，不另立第二套遍历。
/// 2. 不睡。原生逐条 <c>Thread.Sleep(400)</c>（Programs.cs:1291），扫 30 台就卡死
///    12 秒，而这是在游戏线程上。发现动作本身只是内存写，不需要节流。
/// 3. 不设 admin 门禁。原生 <c>hasConnectionPermission(admin: true)</c>（Programs.cs:1279）
///    的本意是「没拿下目标机就别看它的邻居」，而本工具只读玩家自己存档里的图，
///    不改任何第三方状态（标「已发现」不算入侵）。
/// 4. 用原生 <c>NetworkMap.discoverNode</c>（NetworkMap.cs:415-423）标记，
///    与游戏自身的「已发现」同源，不做自绘的伪发现。
///
/// 回显在这里统一：命令入口经 <c>OS.execute</c> 跑在独立线程、面板入口跑在
/// 游戏线程，两处都只写 <c>os.write</c>，格式一致。
/// </summary>
internal static class ScanTools
{
    internal static void Run(OS os)
    {
        if (os?.netMap?.nodes == null)
        {
            return;
        }

        var origin = os.connectedComp ?? os.thisComputer;
        var before = os.netMap.visibleNodes?.Count ?? 0;

        var reachable = HackEngine.ReachableComputers(os);

        var added = (os.netMap.visibleNodes?.Count ?? 0) - before;
        os.write("[autohack] scan: " + (origin == null ? "map" : origin.name)
                 + " :: " + reachable.Length + " node(s) in this component, "
                 + added + " newly revealed.");
    }
}

// HackEngine.Graph.cs —— 网络图遍历：可达闭包、广度优先展开、揭图。
//
// HackEngine 的分部实现；类型声明、字段与其余职责见 HackEngine.cs。
namespace AutoHack;

using Hacknet;
using Pathfinder.Port;
using Pathfinder.Util;

internal static partial class HackEngine
{
    /// <summary>
    /// 广度优先：从玩家机与已发现的节点出发，沿 <c>Computer.links</c> 连线展开可达的服务器。
    /// <b>这是全网扫描的缺省口径</b>（更保守，只碰图上确有通路的机器）；
    /// 要扫地图全表用 <see cref="ConnectableComputers"/>（<c>allnodes</c> 开关）。
    ///
    /// 多源种子：玩家机 + 玩家已发现的机器。只从玩家机出发是不够的 —— 实测存档里
    /// <c>&lt;links&gt;</c> 图极稀疏（玩家机 links 仅 "0 1"、其后节点多半为空），
    /// 那样反而比 visibleNodes 看到的更少。以「已知」为起点向外展开，
    /// 既保证结果永不退化，又能越过原版 scan 的一跳极限。
    ///
    /// 展开出的新节点按原生 scan 的后效委托 <c>NetworkMap.discoverNode</c> 标为已发现
    /// （NetworkMap.cs:415），不做自绘的「伪发现」。入边展开与出边同等对待 ——
    /// 同一张连通分量里的机器，不该因为边的方向而一半被扫、一半被漏。
    /// </summary>
    internal static Computer[] ReachableComputers(OS os) => Closure(os, origin: null);

    /// <summary>
    /// **单源**口径：只从 <paramref name="origin"/> 出发取无向连通分量。
    ///
    /// `autohack scan` 用这个 —— 它的承诺是「当前节点所在的那一张分量」。
    /// 此前 scan 复用的是 <see cref="ReachableComputers"/>（多源播种：玩家机 + 全部
    /// `visibleNodes`），而多源取出的**不是任何一个分量**，是「所有已发现分量的并集」。
    /// 实测同一存档：玩家机当时未连接，它自己所在的分量只有 1 台（links 为空），
    /// 多源口径却给出 57 台 —— 多出的 56 台横跨另外 100 多个互不相连的分量
    /// （太平洋、Kaguya、CSEC 全在内），正是「扫出没有连到当前节点的服务器」。
    ///
    /// 之所以不能直接把 <see cref="ReachableComputers"/> 改成单源：它同时是
    /// `autohack run` 的缺省目标池（见 ResolveTargets），收窄它等于让「沿连线
    /// 全网入侵」退化成「只打当前这一台」。故两个口径并存，各自显式取用。
    /// </summary>
    internal static Computer[] ReachableFrom(OS os, Computer origin) => Closure(os, origin);

    /// <summary>
    /// 把地图上<b>全部</b>节点标成「已发现」，返回标记覆盖的节点数。
    ///
    /// <para>地图全表口径的<b>揭示</b>动作 —— 与 <see cref="ConnectableComputers"/> 同属
    /// allnodes 口径（那个给目标池，这个给观感）。两者刻意<b>不</b>互相调用：目标池要排除
    /// 玩家机与 disabled，而揭示要连玩家机一起点亮（它本来就是地图上的一台）。</para>
    ///
    /// <para>两个调用点：<c>autohack run allnodes</c> 的「先揭图后入侵」（见 HackRun 构造期）
    /// 与 <c>autohack scan allnodes</c> 工具（见 ScanTools）。</para>
    ///
    /// <para>标记一律走原生 <c>NetworkMap.discoverNode</c>（NetworkMap.cs:415-423），
    /// 与游戏自身的「已发现」同源。它内部是 <c>visibleNodes.Contains(nodes.IndexOf(c))</c>，
    /// 两次线性扫描，故这里是 O(V²)。实测存档 167 台无感；千级地图才需要改走哈希集
    /// （本次不做，YAGNI）。</para>
    /// </summary>
    internal static int RevealWholeMap(OS os)
    {
        var map = os?.netMap;
        if (map?.nodes == null || map.nodes.Count == 0)
        {
            return 0;
        }

        var revealed = 0;
        foreach (var comp in map.nodes)
        {
            if (comp == null || comp.disabled)
            {
                continue;
            }

            map.discoverNode(comp);
            revealed++;
        }

        return revealed;
    }

    /// <summary>
    /// 同 <see cref="ReachableFrom"/>，但<b>不</b>把走到的节点标成「已发现」。
    ///
    /// 清痕要沿连线走到玩家此前访问过的机器，但那不是侦察 ——
    /// <c>Closure</c> 默认会调 <c>NetworkMap.discoverNode</c>（NetworkMap.cs:415），
    /// 把沿途节点在地图上点亮。清痕顺手替玩家揭开地图，是没人要的副作用。
    /// </summary>
    internal static Computer[] SilentClosure(OS os, Computer origin) => Closure(os, origin, reveal: false);

    /// <summary>
    /// 一次广度优先遍历的全部共享状态：网络图、已入队下标、已发现下标与待展开队列。
    /// 打包成结构体，使遍历的每一步只携带一个参数，而不是重复传递同一组引用。
    /// </summary>
    private readonly struct Traversal
    {
        internal readonly NetworkMap map;
        internal readonly HashSet<int> seen;
        internal readonly HashSet<int> discovered;
        internal readonly Queue<int> frontier;

        internal Traversal(NetworkMap map, HashSet<int> seen, HashSet<int> discovered, Queue<int> frontier)
        {
            this.map = map;
            this.seen = seen;
            this.discovered = discovered;
            this.frontier = frontier;
        }
    }

    private static Computer[] Closure(OS os, Computer origin, bool reveal = true)
    {
        var map = os?.netMap;
        if (map?.nodes == null || map.nodes.Count == 0)
        {
            return Array.Empty<Computer>();
        }

        var seen = new HashSet<int>();
        var frontier = new Queue<int>();

        // links 是**有向**的：ComputerLoader.cs:344-356 的 <link> 与 :357-373 的 <dlink>
        // 都只往自己的 links 里加边（dlink 仅延迟到 postAllLoadedActions 解析，方向不变），
        // 而原生 scan 也只遍历 computer2.links（Programs.cs:1282）。于是「别的机器指向
        // 已知机器」这批入边在正向展开里永远走不到，整片都不会进目标池。
        // 先摊平入边，展开时双向走，按无向图取连通分量。
        var incoming = BuildIncoming(map);

        // visibleNodes 是 List<int>，逐次 Contains 会退化成 O(V·E)；
        // 先摊平成哈希集，供展开循环做 O(1) 判「已发现」。
        var discovered = map.visibleNodes == null
            ? new HashSet<int>()
            : new HashSet<int>(map.visibleNodes);
        SeedSources(map, os, origin, seen, frontier);

        if (seen.Count == 0)
        {
            return Array.Empty<Computer>();
        }

        return ExpandFrontier(new Traversal(map, seen, discovered, frontier), os, incoming, reveal);
    }

    /// <summary>种子入队：单源只取 origin，多源取玩家机加全部已发现节点。</summary>
    private static void SeedSources(
        NetworkMap map, OS os, Computer origin, HashSet<int> seen, Queue<int> frontier)
    {
        if (origin != null)
        {
            // 单源：只要这一台。它不在本图上（换过地图）时 IndexOf 返回 -1，
            // Seed 忽略越界下标，闭包为空 —— 与「找不到就什么都不扫」一致。
            Seed(map, seen, frontier, map.nodes.IndexOf(origin));
        }
        else
        {
            Seed(map, seen, frontier, os.thisComputer == null ? -1 : map.nodes.IndexOf(os.thisComputer));
            if (map.visibleNodes != null)
            {
                foreach (var index in map.visibleNodes)
                {
                    Seed(map, seen, frontier, index);
                }
            }
        }
    }

    /// <summary>主 BFS 循环：出队、跳过空/停用节点、收集结果、补 EOS 设备、双向展开邻边。</summary>
    private static Computer[] ExpandFrontier(
        Traversal traversal, OS os, List<int>[] incoming, bool reveal)
    {
        var found = new List<Computer>();
        while (traversal.frontier.Count > 0)
        {
            var index = traversal.frontier.Dequeue();
            var comp = traversal.map.nodes[index];
            if (comp == null || comp.disabled)
            {
                continue;
            }

            if (!ReferenceEquals(comp, os.thisComputer))
            {
                found.Add(comp);
            }

            // EOS 设备挂在父机的 attatchedDeviceIDs 上，links 里没有反向边，
            // 必须在 links 展开之外单独补 —— 见 RevealAttachedDevices。
            RevealAttachedDevices(traversal, os, comp, reveal);

            Expand(traversal, comp.links, reveal);
            Expand(traversal, incoming[index], reveal);
        }

        return found.ToArray();
    }

    /// <summary>广度优先的种子入队：忽略越界下标并去重。</summary>
    private static void Seed(NetworkMap map, HashSet<int> seen, Queue<int> frontier, int index)
    {
        if (index >= 0 && index < map.nodes.Count && seen.Add(index))
        {
            frontier.Enqueue(index);
        }
    }

    /// <summary>
    /// 入边邻接表：<c>incoming[i]</c> = 所有 links 指向 i 的机器下标。
    /// 一次 O(V+E) 扫描即建全 —— 正是正向展开看不见的那半张图。
    /// </summary>
    private static List<int>[] BuildIncoming(NetworkMap map)
    {
        var incoming = new List<int>[map.nodes.Count];
        for (var i = 0; i < map.nodes.Count; i++)
        {
            var links = map.nodes[i] == null ? null : map.nodes[i].links;
            if (links == null)
            {
                continue;
            }

            foreach (var target in links)
            {
                if (target < 0 || target >= map.nodes.Count)
                {
                    continue;
                }

                if (incoming[target] == null)
                {
                    incoming[target] = new List<int>();
                }

                incoming[target].Add(i);
            }
        }

        return incoming;
    }

    /// <summary>
    /// 把一批相邻下标并入 frontier，后效与原生 scan 一致：越界与已见忽略，
    /// 新节点委托 <c>NetworkMap.discoverNode</c> 标为已发现（NetworkMap.cs:415）。
    /// <paramref name="neighbors"/> 为 null 时无操作 —— 出边与入边都可能是空的。
    /// </summary>
    private static void Expand(Traversal traversal, List<int> neighbors, bool reveal = true)
    {
        if (neighbors == null)
        {
            return;
        }

        foreach (var next in neighbors)
        {
            if (next < 0 || next >= traversal.map.nodes.Count || !traversal.seen.Add(next))
            {
                continue;
            }

            var neighbor = traversal.map.nodes[next];
            if (neighbor == null || neighbor.disabled)
            {
                continue;
            }

            if (traversal.discovered.Add(next) && reveal)
            {
                traversal.map.discoverNode(neighbor);
            }

            traversal.frontier.Enqueue(next);
        }
    }

    /// <summary>
    /// 把一台机器上「已同步的 EOS 设备」补进网络图 —— 等价于原版
    /// <c>eosDeviceScan.exe</c> 的 <c>Completed()</c>（EOSDeviceScannerExe.cs:82-124）
    /// 干的事，但免跑 exe、免 8 秒计时、免 <c>hasConnectionPermission</c> 门禁
    /// （那个门禁在 <c>connectedComp.currentUser</c> 为 null 时会 NRE，OS.cs:1848）。
    ///
    /// 为什么必须补：EOS 设备的 <c>links</c> 是「设备 → 父机」单向
    /// （ContentLoader.cs:975-982），父机的 links 里根本没有它 ——
    /// 故沿 links 展开的 BFS 从父机永远走不到设备，设备进不了扫描池。
    ///
    /// 设备清单来自父机的 <c>attatchedDeviceIDs</c>（逗号分隔的 idName，
    /// 且会随存档持久化，Computer.cs:915/:1531），按 <c>Programs.getComputer</c>
    /// 三字段查找还原成 <see cref="Computer"/>（Programs.cs:1570-1580）。
    /// 发现的设备一律走原生 <c>NetworkMap.discoverNode</c>（NetworkMap.cs:415-423），
    /// 与游戏自身的「已发现」标记同源，不做自绘的伪发现。
    /// </summary>
    private static void RevealAttachedDevices(
        Traversal traversal, OS os, Computer comp, bool reveal = true)
    {
        var ids = comp?.attatchedDeviceIDs;
        if (string.IsNullOrEmpty(ids))
        {
            return;
        }

        foreach (var id in ids.Split(Utils.commaDelim, StringSplitOptions.RemoveEmptyEntries))
        {
            var device = Programs.getComputer(os, id);
            if (device == null || device.disabled)
            {
                continue;
            }

            var index = traversal.map.nodes.IndexOf(device);
            if (index < 0 || !traversal.seen.Add(index))
            {
                continue;
            }

            // 已在 visibleNodes 里的设备不重复 discoverNode（避免多余的高亮闪烁），
            // 但仍要入队 —— 它同样需要沿自己的 links 继续展开。
            if (traversal.discovered.Add(index) && reveal)
            {
                traversal.map.discoverNode(device);
            }

            traversal.frontier.Enqueue(index);
        }
    }

    /// <summary>
    /// 地图上<b>全部</b>可连接的服务器 —— <c>allnodes</c> 开关启用的口径。
    ///
    /// 判据直接对齐游戏自身的连接逻辑：<c>Programs.connect</c>（Programs.cs:231-322）
    /// 在 <c>os.netMap.nodes</c> 里按 ip/name 线性查找，**全程不检查 visibleNodes**，
    /// 也不看 <c>links</c> —— 地图上任何节点都是「敲 IP 就能连」的。
    /// <c>visibleNodes</c> 只是原版 <c>scan</c> 维护的「已发现」展示标记，不是连接许可。
    ///
    /// 实测同一存档：广度优先 7 个目标，全表 110 个 —— 差额就是「可以直接敲 IP
    /// 但不在连线上」的机器。
    ///
    /// 此处只排除玩家机；disabled、已控、永远提不了权的机器交由
    /// <see cref="ResolveTargets"/> 统一过滤。
    /// </summary>
    internal static Computer[] ConnectableComputers(OS os)
    {
        var map = os?.netMap;
        if (map?.nodes == null || map.nodes.Count == 0)
        {
            return Array.Empty<Computer>();
        }

        var found = new List<Computer>(map.nodes.Count);
        foreach (var comp in map.nodes)
        {
            if (comp != null && !ReferenceEquals(comp, os.thisComputer))
            {
                found.Add(comp);
            }
        }

        return found.ToArray();
    }
}

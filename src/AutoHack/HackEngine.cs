namespace AutoHack;

using Hacknet;
using Pathfinder.Port;
using Pathfinder.Util;

/// <summary>
/// 目标上的一个端口（统一视图，全部取自 Pathfinder 端口表）。
/// <c>CodePort</c> 是游戏内部端口号（<see cref="PortRecord.OriginalPortNumber"/>），
/// <c>DisplayPort</c> 是玩家在终端看到的端口号（<see cref="PortState.PortNumber"/>）。
/// </summary>
internal readonly record struct PortInfo(string Protocol, int CodePort, int DisplayPort, string DisplayName, bool Cracked);

/// <summary>
/// 入侵决策逻辑：目标解析、端口状态、提权判定。全部基于 Pathfinder 框架 API。
/// </summary>
internal static class HackEngine
{
    private const string LogFolderName = "log";

    /// <summary>
    /// 读取目标的端口表。
    ///
    /// 关键：Pathfinder 用 Harmony Prefix 接管了 <c>Computer.openPort</c>/<c>openPorts</c>
    /// 并返回 false 跳过原版实现，端口状态改存 <c>PortState.Cracked</c>（框架 PortTable），
    /// 原版 <c>portsOpen</c> 因此永不更新 —— 任何读 <c>portsOpen</c> 的判断都是错的。
    /// 故一律走框架端口表；仅当表尚未建立时，按原版端口号反查 <see cref="PortRecord"/>
    /// 以保留端口清单（状态按「未攻破」处理，绝不臆造已开放）。
    /// </summary>
    internal static IReadOnlyList<PortInfo> Ports(Computer comp)
    {
        if (comp == null)
        {
            return Array.Empty<PortInfo>();
        }

        var states = comp.GetAllPortStates();
        if (states != null && states.Count > 0)
        {
            var fromStates = new List<PortInfo>(states.Count);
            foreach (var state in states)
            {
                var record = state?.Record;
                if (record == null)
                {
                    continue;
                }

                fromStates.Add(new PortInfo(
                    record.Protocol,
                    record.OriginalPortNumber,
                    state.PortNumber,
                    state.DisplayName ?? record.DefaultDisplayName,
                    state.Cracked));
            }

            return fromStates;
        }

        if (comp.ports == null || comp.ports.Count == 0)
        {
            return Array.Empty<PortInfo>();
        }

        var fallback = new List<PortInfo>(comp.ports.Count);
        foreach (var code in comp.ports)
        {
            var record = PortManager.GetPortRecordFromNumber(code);
            if (record == null)
            {
                continue;
            }

            fallback.Add(new PortInfo(
                record.Protocol,
                record.OriginalPortNumber,
                record.DefaultPortNumber,
                record.DefaultDisplayName,
                Cracked: false));
        }

        return fallback;
    }

    /// <summary>该机器上「有原生破解程序」且尚未攻破的端口（破解程序表取自 PortExploits.cracks，不硬编码）。</summary>
    internal static IReadOnlyList<PortInfo> CrackablePorts(Computer comp)
    {
        var ports = Ports(comp);
        if (ports.Count == 0)
        {
            return Array.Empty<PortInfo>();
        }

        var found = new List<PortInfo>(ports.Count);
        var seen = new HashSet<int>();
        foreach (var port in ports)
        {
            if (!port.Cracked && HasCrackProgram(port.CodePort) && seen.Add(port.CodePort))
            {
                found.Add(port);
            }
        }

        return found;
    }

    /// <summary>
    /// 跳板是否处于激活状态。激活期间，所有 <c>needsProxyAccess</c> 的破解程序都会被
    /// <c>OS.addExe</c> 挡下（"Proxy Active -- Cannot Execute"），必须先过载绕过。
    /// </summary>
    internal static bool ProxyActive(Computer comp)
        => comp != null && comp.hasProxy && comp.proxyActive;

    /// <summary>已攻破端口数（框架语义：<see cref="PortState.Cracked"/>）。</summary>
    internal static int OpenPortCount(Computer comp)
        => comp == null ? 0 : comp.CountOpenPorts();

    /// <summary>
    /// 原生提权门槛 —— 与游戏 porthack 的门禁逐条对齐（OS.cs:1908-1930）：
    /// 已攻破端口数必须**超过** <c>portsNeededForCrack</c>，且防火墙已解；
    /// 缺其一会写 "Target Machine Rejecting Syndicated UDP Traffic" 并拒绝启动 PortHackExe。
    /// 此前 mod 直接调 <c>giveAdmin</c> 跳过整条门禁，等于从未用过原生的提权与防火墙机制。
    /// </summary>
    internal static bool CanEscalate(Computer comp)
        => comp != null
           && OpenPortCount(comp) > comp.portsNeededForCrack
           && (comp.firewall == null || comp.firewall.solved);

    /// <summary>
    /// 该机器理论上能否提权：端口表容量（全开后的攻破数）能否越过门槛。
    /// 门槛由游戏自身定义：<c>openPortsForSecurityLevel</c> 令
    /// <c>portsNeededForCrack = security - 1</c>（Computer.cs:200-204）。
    /// 容量 ≤ 门槛 = 永远开不满 = 永远提不了权 —— 实测存档里既有
    /// <c>portsToCrack="9999998"</c> 的剧情保护机（EnTech 系列），也有门槛 8/6
    /// 而端口表只有 4~5 个的机器。这类机器此前每次全网扫描都被连上、逐个破端口、
    /// 再提权失败，即「每一次判断都是要入侵」。
    /// </summary>
    internal static bool CanEverEscalate(Computer comp)
        => comp != null && Ports(comp).Count > comp.portsNeededForCrack;

    /// <summary>
    /// 解目标的防火墙，走游戏自身的 <c>Firewall.attemptSolve</c>（Firewall.cs:101-116）：
    /// 传入正确解即置 <c>solved = true</c> —— 与玩家敲 <c>solve &lt;序列&gt;</c> 是同一入口。
    ///
    /// 为什么不用 <c>Programs.solve</c>：它内层先跑 <c>doDots(30, 60)</c>，每点
    /// Thread.Sleep(60)（Programs.cs:18-25），合计约 1.8 秒阻塞 —— 不能在游戏线程调。
    /// <c>attemptSolve</c> 本身是纯判断，无阻塞。
    /// 解序列由游戏自己生成并公开在 <c>Firewall.solution</c>（Firewall.cs:20）；
    /// 原版 <c>analyze</c> 的逐趟收敛只是给真人看的提示，不必等它跑完。
    /// </summary>
    internal static bool SolveFirewall(Computer comp, OS os)
    {
        var firewall = comp?.firewall;
        if (firewall == null || firewall.solved || string.IsNullOrEmpty(firewall.solution))
        {
            return false;
        }

        return firewall.attemptSolve(firewall.solution, os);
    }

    /// <summary>真人会敲的破解指令，如 <c>sshcrack 22</c>。程序名取自游戏数据，端口号取显示端口。</summary>
    internal static string CrackCommand(PortInfo port)
    {
        var program = HasCrackProgram(port.CodePort)
            ? PortExploits.cracks[port.CodePort].Replace(".exe", string.Empty).ToLowerInvariant()
            : "portcrack";
        return program + " " + port.DisplayPort;
    }

    /// <summary>
    /// 打开一个端口，走 <b>游戏自身</b>的签名 <c>Computer.openPort(int portNum, string ipFrom)</c>。
    ///
    /// 为什么不是框架扩展 <c>openPort(string protocol, string ipFrom)</c>：两者最终都写
    /// <see cref="PortState.Cracked"/>，但游戏签名才是原生破解程序（<c>SSHCrackExe</c> 等）
    /// 完成时的调用形态，且 Pathfinder 用 Prefix 把该重载接到端口表
    /// （<c>Record.OriginalPortNumber == portNum</c>，ComputerExtensions.cs:236-252）。
    /// 传 <see cref="PortInfo.CodePort"/>（= OriginalPortNumber）即与原生完全同路，
    /// 玩家实际敲的 <c>sshcrack 22</c> 也是「显示端口进、内部换算成原始端口」
    /// （<c>ProgramRunner.AttemptExeProgramExecution</c> 里 <c>GetCodePortNumberFromDisplayPort</c>）。
    /// </summary>
    internal static void OpenPort(Computer comp, PortInfo port, string ipFrom)
    {
        if (comp == null || port.Protocol is null)
        {
            return;
        }

        comp.openPort(port.CodePort, ipFrom);
    }

    /// <summary>
    /// 原生 probe 的逐行输出，行格式与 Pathfinder 替换后的 <c>Programs.probe</c> 一致
    /// （<c>Port#: {PortNumber}  -  {DisplayName}</c>），但无 Thread.Sleep —— 不卡游戏线程。
    /// </summary>
    internal static IReadOnlyList<string> ProbeReport(Computer comp)
    {
        var lines = new List<string>
        {
            "Probing " + comp.ip + "...",
            "..........",
            string.Empty,
            "Probe Complete - Open ports:",
            "---------------------------------",
        };

        foreach (var port in Ports(comp))
        {
            var open = port.Cracked ? " : OPEN" : string.Empty;
            lines.Add("Port#: " + port.DisplayPort + "  -  " + port.DisplayName + open);
        }

        lines.Add("---------------------------------");
        lines.Add("Open Ports Required for Crack : " + Math.Max(comp.portsNeededForCrack + 1, 0));

        if (comp.hasProxy)
        {
            lines.Add("Proxy Detected : " + (comp.proxyActive ? "ACTIVE" : "INACTIVE"));
        }

        if (comp.firewall != null)
        {
            lines.Add("Firewall Detected : " + (comp.firewall.solved ? "SOLVED" : "ACTIVE"));
        }

        return lines;
    }

    /// <summary>
    /// 该机器是否已被玩家拿下（肉鸡）。
    /// 原生的所有权标记只有 <c>Computer.adminIP</c>（<c>giveAdmin(ipFrom)</c> 写入，
    /// 且被 <c>SaveWriter</c>/<c>SaveLoader</c> 持久化）；Pathfinder 没有更上层的封装。
    /// 注意 <c>Computer.admin</c> 是任务用的 <c>Administrator</c> 行为对象，不是所有权标记。
    /// </summary>
    internal static bool IsOwned(Computer comp, OS os)
        => comp != null && os?.thisComputer != null && comp.adminIP == os.thisComputer.ip;

    /// <summary>
    /// 解除目标的「管理员反扑」——「全网入侵失去效果」的根因。
    ///
    /// 断开连接时游戏自身会走 <c>OS.handleDisconnection()</c>（OS.cs:944-950）：
    /// <c>computer.admin?.disconnectionDetected(computer, this)</c>。而
    /// <c>BasicAdministrator.disconnectionDetected</c> / <c>FastBasicAdministrator</c> 会在
    /// 0~20 秒后关掉该机全部端口，并执行 <c>c.adminIP = c.ip</c> —— 把刚写进去的玩家
    /// IP 抹掉，肉鸡标记因此丢失，下次扫描又得重来。Pathfinder 只补了「关端口要按
    /// <c>GetAllPortStates()</c> 的协议名」（ComputerExtensions.cs:418-449），
    /// <b>没有</b>覆盖 <c>adminIP</c> 还原。
    ///
    /// <c>Computer.admin</c> 是游戏原生字段：<c>ComputerLoader</c> 用
    /// <c>type="none"</c> 把它置为 null（ComputerLoader.cs:425），存档里大量节点
    /// 本就是该状态。置 null 的语义是「这台机器没有会反扑的管理员」；玩家的所有权
    /// 由 <c>adminIP</c> 表示，不受影响。断开时的 <c>admin?.</c> 是空条件调用，
    /// 为 null 即不会注册还原回调。
    /// </summary>
    /// <returns>是否真的解除了一个管理员（供回显报数）。</returns>
    internal static bool SuppressCounterattack(Computer comp)
    {
        if (comp?.admin is null)
        {
            return false;
        }

        comp.admin = null;
        return true;
    }

    /// <summary>
    /// 立即让目标跳板失效：语义与 ShellExe 过载跑完完全一致
    /// （<c>proxyOverloadTicks = 0f; proxyActive = false;</c>，ShellExe.cs:96-99），
    /// 但不等满 <c>startingOverloadTicks</c> 秒。
    ///
    /// 为什么这必须由 mod 实现：游戏没有「立即完成过载」的 API —— 终端
    /// <c>ComShell.exe -o</c>（OS.cs:2134 → ShellOverloaderExe → ShellExe.StartOverload）
    /// 启动的就是同一个逐帧扣减的 ShellExe，跑满一次要 <c>BASE_PROXY_TICKS = 30f</c> 秒
    /// （Computer.cs:27）。全网扫一遍就是几十段 30 秒纯等待，即用户报的
    /// 「还要干等 proxy 一阵子」。故直接收敛游戏原生的 public 字段：
    /// 不碰私有状态，不挂 Harmony 补丁，也不新增依赖。
    ///
    /// 跳过等待没有副作用：全游戏 <c>AchievementsManager.Unlock</c> 与跳板无关
    /// （唯一的追踪成就在 TraceTracker.cs:70 的 "trace_close"），过载也不推进追踪。
    /// 刻意不照抄 ShellExe.cs:105 的 <c>hostileActionTaken()</c> —— 那只会点燃反追踪。
    /// </summary>
    /// <returns>是否真的绕过了跳板（供回显报数）。</returns>
    internal static bool BypassProxy(Computer comp)
    {
        if (!ProxyActive(comp))
        {
            return false;
        }

        comp.proxyOverloadTicks = 0f;
        comp.proxyActive = false;
        return true;
    }

    /// <summary>
    /// 解析目标集合，按可行性与「是否已拿下」过滤。目标串走框架查找表。
    /// 两类剔除只作用于「全网扫描」；<c>here</c> 与显式点名是刻意选择，一律尊重。
    /// <paramref name="skippedOwned"/> 与 <paramref name="skippedHopeless"/>
    /// 分别回传「已控」与「永远提不了权」的机器数，供终端报数。
    /// </summary>
    internal static IReadOnlyList<Computer> ResolveTargets(
        OS os, HackOptions options, out int skippedOwned, out int skippedHopeless)
    {
        skippedOwned = 0;
        skippedHopeless = 0;
        if (os == null)
        {
            return Array.Empty<Computer>();
        }

        var pool = options.Scope switch
        {
            HackScope.Connected => os.connectedComp is { } connected ? [connected] : [],
            HackScope.Network => options.AllNodes ? ConnectableComputers(os) : ReachableComputers(os),
            _ => options.Targets
                    .Select(id => ComputerLookup.Find(id))
                    .Where(comp => comp != null)
                    .ToArray(),
        };

        var sweep = options.Scope == HackScope.Network;
        var result = new List<Computer>(pool.Length);

        // 用哈希集去重，而不是 List.Contains：显式目标串可能重复点名，
        // 而全网遍历的池本身已去重，两者都需要 O(1) 判重。
        var seen = new HashSet<Computer>(pool.Length);

        foreach (var comp in pool)
        {
            if (comp == null || comp.disabled || ReferenceEquals(comp, os.thisComputer))
            {
                continue;
            }

            if (sweep)
            {
                if (options.SkipOwned && IsOwned(comp, os))
                {
                    skippedOwned++;
                    continue;
                }

                // 提权门槛高于端口表容量的机器永远打不通，全网扫描一律剔除
                // （显式点名时仍尊重玩家选择）。
                if (!CanEverEscalate(comp))
                {
                    skippedHopeless++;
                    continue;
                }
            }

            if (seen.Add(comp))
            {
                result.Add(comp);
            }
        }

        return result;
    }

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
    /// （NetworkMap.cs:415），不做自绘的「伪发现」。
    /// </summary>
    internal static Computer[] ReachableComputers(OS os)
    {
        var map = os?.netMap;
        if (map?.nodes == null || map.nodes.Count == 0)
        {
            return Array.Empty<Computer>();
        }

        var seen = new HashSet<int>();
        var frontier = new Queue<int>();

        // visibleNodes 是 List<int>，逐次 Contains 会退化成 O(V·E)；
        // 先摊平成哈希集，供展开循环做 O(1) 判「已发现」。
        var discovered = map.visibleNodes == null
            ? new HashSet<int>()
            : new HashSet<int>(map.visibleNodes);
        Seed(map, seen, frontier, os.thisComputer == null ? -1 : map.nodes.IndexOf(os.thisComputer));
        if (map.visibleNodes != null)
        {
            foreach (var index in map.visibleNodes)
            {
                Seed(map, seen, frontier, index);
            }
        }

        if (seen.Count == 0)
        {
            return Array.Empty<Computer>();
        }

        var found = new List<Computer>();
        while (frontier.Count > 0)
        {
            var comp = map.nodes[frontier.Dequeue()];
            if (comp == null || comp.disabled)
            {
                continue;
            }

            if (!ReferenceEquals(comp, os.thisComputer))
            {
                found.Add(comp);
            }

            if (comp.links == null)
            {
                continue;
            }

            foreach (var next in comp.links)
            {
                if (next < 0 || next >= map.nodes.Count || !seen.Add(next))
                {
                    continue;
                }

                var neighbor = map.nodes[next];
                if (neighbor == null || neighbor.disabled)
                {
                    continue;
                }

                if (discovered.Add(next))
                {
                    map.discoverNode(neighbor);
                }

                frontier.Enqueue(next);
            }
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
    /// 直接毙掉追踪 —— 不是「冻结」，是停。
    ///
    /// 走游戏自身的 <c>TraceTracker.stop()</c>（TraceTracker.cs:116-119）：
    /// <c>active = false; trackSpeedFactor = 1f;</c>。这是原生路径，
    /// <c>SecurityTraceExe.Killed()</c>（SecurityTraceExe.cs:26）在玩家关掉
    /// Security Tracer 时就是这么干的；<c>OS.thisComputerIPReset()</c>
    /// （OS.cs:1793-1796）换 IP 时也是直接置 <c>active = false</c>。
    ///
    /// 停是彻底的，不存在「暂停」形态 —— <c>TraceTracker.Update</c> 开头的
    /// <c>if (!active) return;</c>（TraceTracker.cs:53-56）让后续帧零开销，
    /// **不需要任何每帧维护**。
    ///
    /// 为什么不照抄 <c>TraceKillExe</c> 的「每帧把 <c>timeSinceFreezeRequest</c> 置 0」：
    /// 那是它作为 GUI 程序的职责 —— 玩家开着它时要看到 <c>SUPPRESSION ACTIVE</c>
    /// 的持续效果，故必须逐帧续期。mod 要的是「立即终止」这一动作，
    /// 没有那个 UI 需求，照抄只会白白常驻一个每帧补丁。
    ///
    /// 不丢成就：<c>trace_close</c> 的解锁写在 <c>TraceTracker.Update</c> 的
    /// <i>另一条</i>分支（connectedComp 为空或已换目标，TraceTracker.cs:64-71），
    /// 走 <c>stop()</c> 不经过它。真要在意那条分支的成就，得靠断开连接。
    /// </summary>
    /// <returns>是否真的终止了一条进行中的追踪（供回显报数）。</returns>
    internal static bool KillTrace(OS os)
    {
        if (os?.traceTracker is not { active: true })
        {
            return false;
        }

        os.traceTracker.stop();
        return true;
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

    /// <summary>抹除目标的 /log 目录；返回被删除的文件名，供回显 rm 指令。</summary>
    internal static IReadOnlyList<string> ClearLogs(Computer comp)
    {
        var logFolder = comp?.files?.root?.searchForFolder(LogFolderName);
        if (logFolder == null || logFolder.files.Count == 0)
        {
            return Array.Empty<string>();
        }

        var removed = new List<string>(logFolder.files.Count);
        foreach (var file in logFolder.files)
        {
            removed.Add(file.name);
        }

        logFolder.files.Clear();
        return removed;
    }

    /// <summary>原版破解程序表里是否有该端口的破解程序（含扩展注册项）。</summary>
    private static bool HasCrackProgram(int codePort)
        => PortExploits.cracks != null && PortExploits.cracks.ContainsKey(codePort);
}

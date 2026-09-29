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

    /// <summary>白名单 daemon 的文件夹名（WhitelistConnectionDaemon.cs:26/29）。</summary>
    private const string WhitelistFolderName = "Whitelist";

    /// <summary>白名单文件名（WhitelistConnectionDaemon.ListFilename，:12）。</summary>
    private const string WhitelistListFilename = "list.txt";

    /// <summary>
    /// 认证器文件名（WhitelistConnectionDaemon.SystemFilename，:10）。
    /// 它缺失即 <c>IPCanPassWhitelist</c> 无条件放行（:142-145）。
    /// </summary>
    private const string WhitelistAuthFilename = "authenticator.dll";

    /// <summary>
    /// 认证源文件名（WhitelistConnectionDaemon.SourceFilename，:14）。
    /// 其内容 = 裁决本机白名单的那台机器的 IP（loadInit，:62-65）。
    /// </summary>
    private const string WhitelistSourceFilename = "source.txt";

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

        // 以下分支在当前加载路径下**不可达**，保留作防御：
        // Pathfinder 用 ContentLoader.cs:341 的 Computer.ports executor 完全替换了游戏原生
        // 加载器，端口一律经 PortManager.LoadPortsFromStringVanilla -> comp.AddPort(record)
        // -> record.CreateState(comp) 写进 PortTable，**不回写 comp.ports**；且
        // ComputerExtensions.cs:201-204 的 OpenPortsPrefix 直接 return false 拦掉原生
        // openPorts。故 GetAllPortStates() 恒非空，上面 states.Count > 0 必先返回。
        // 唯一仍写 comp.ports 的是 DLC1SessionUpgrader.cs:57-61（只对 ispComp 加 443/6881，
        // 且不设 portsNeededForCrack）。留着是因为替换机制一旦被别的 mod 改掉，
        // 这里会静默返回空端口表 —— 那是比多几行更糟的失效方式。
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
    /// 强行提权：越过 porthack 的两道原生门禁，直接把目标机的 <c>adminIP</c>
    /// 写成玩家自己 —— 与 <c>target.giveAdmin(ip)</c>（Computer.cs:851-855）
    /// 完全等价，也就是游戏自己提权成功时走的那一步。
    ///
    /// <b>为什么必须绕过门禁。</b>porthack 要求「已攻破端口数 &gt;
    /// <c>portsNeededForCrack</c> 且防火墙已解」（<c>OS.cs:1908-1930</c>）。
    /// 门槛由 <c>openPortsForSecurityLevel</c> 定为 <c>security - 1</c>
    /// （Computer.cs:200-204），但有几类机器把它写死在数据里，端口表根本凑不出那么多：
    ///
    /// <list type="bullet">
    /// <item><b>游戏自带的防护机</b>：门槛直接写成 <c>portsToCrack="9999998"</c>。
    /// 实测存档 9 台（EnTech 中继服务器系列，配 <c>traceTime=500</c>、
    /// <c>proxyTime=30</c>）。门槛 &gt; 100 时 <c>DisplayModule</c> 走
    /// <c>INVIOLABILITY ERROR</c> 特效分支（DisplayModule.cs:429/:625/:724）——
    /// 面板上那行乱码数字就是它。</item>
    /// <item><b>玩家用 <c>unbreakable</c> 加固后的机器</b>：<see cref="HardenTools"/>
    /// 写的正是同一个字段（<c>Inviolable = 9999998</c>），故加固出来的机器与游戏防护机
    /// 在数据上无从区分。</item>
    /// <item><b>门槛只比端口表多几个的普通机器</b>：实测存档 13 台，门槛 2~8 而端口表
    /// 只有 1~4 个 —— 破满端口也差 1~6 个，判据同样恒假。</item>
    /// </list>
    ///
    /// 三类合计 22 台，<b>常规路径一台也拿不下</b>。
    ///
    /// <b>仍然先按顺序做能做的事</b>：能破的端口照破、防火墙照解。这样即便门槛
    /// 离谱到拿不下，玩家看到的战果也是真实的（<c>probe</c> 报告与端口状态一致），
    /// 而不是「什么都没发生就报成功」。
    ///
    /// <b>不做的事</b>：不碰 <c>proxyActive</c>（防护机的跳板是对方的防护，
    /// 拆它等于替对方把门打开 —— 见 <see cref="BypassProxy"/>），
    /// 不碰 <c>traceTime</c>（那是目标自己的追踪配置，改它属于篡改游戏数据，
    /// 不是「拿下这台机器」的一部分）。提权只写 <c>adminIP</c>。
    /// </summary>
    /// <returns>是否真的提权了（供回显报数）。</returns>
    internal static bool ForceEscalate(Computer comp, OS os)
    {
        if (comp == null || os?.thisComputer == null)
        {
            return false;
        }

        if (comp.adminIP == os.thisComputer.ip)
        {
            return false;   // 已经是我们的，不重复写
        }

        comp.giveAdmin(os.thisComputer.ip);
        return true;
    }

    /// <summary>
    /// 是否是 EOS 设备（<see cref="Computer.EOS"/> = 5，Computer.cs:21）。
    ///
    /// EOS 设备在端口上是个死局：<c>portsNeededForCrack = 2</c> 而端口表恰好也只有
    /// 2 个（22 + 3659，ContentLoader.cs:825/:828），而 porthack 的门禁是<b>严格大于</b>
    /// （<c>OpenPortCount &gt; portsNeededForCrack</c>，OS.cs:1896-1942）——
    /// 2 &gt; 2 恒假，破满端口也提不了权。<b>这是游戏刻意的</b>：EOS 从来不是靠破端口进的，
    /// 而是靠全系统一的固定密码（见 <see cref="TryLogin"/> 与游戏自带的
    /// Content/Post/eosScannerMail.txt:7-9）。
    /// </summary>
    internal static bool IsEosDevice(Computer comp)
        => comp != null && comp.type == Computer.EOS;

    /// <summary>
    /// 用已知账号登录目标机 —— 走游戏自身的 <c>Computer.login</c>（Computer.cs:849-865）。
    ///
    /// 这不是「绕过」，是更原生的路径：<c>login</c> 在用户名为 admin 且密码匹配时
    /// <b>内部直接调 <c>giveAdmin</c></b>（Computer.cs:851-855），与 porthack 终点等价，
    /// 但不需要破解任何端口，也<b>不会触发追踪</b>（<c>hostileActionTaken</c> 不在该路径上）。
    ///
    /// 候选顺序：admin 账号优先（其密码就是 <c>adminPass</c>，两者同源，
    /// 见 Computer.cs:122-123 与存档读回 Computer.cs:1117-1120）；
    /// 其余账号仅当 <c>known</c> 为真时才尝试 —— 那才是「玩家已知」的语义。
    ///
    /// 刻意不用 <c>Programs.login</c>：它是<b>交互式</b>的，用
    /// <c>while (commandsRun() == num) Thread.Sleep(4)</c> 轮询等玩家敲用户名与密码
    /// （Programs.cs:404-448），在游戏线程调用即卡死。
    /// </summary>
    /// <param name="credential">回传命中的账号名，供终端说明用了哪组凭据。</param>
    /// <returns>是否登录成功（成功即已提权）。</returns>
    internal static bool TryLogin(Computer comp, out string credential)
    {
        credential = null;
        if (comp?.users == null)
        {
            return false;
        }

        // ① 原生「已知」标记的账号优先 —— 这才是玩家在剧情/邮件里真知道的那组密码。
        foreach (var user in comp.users)
        {
            if (!user.known || user.name == null || user.pass == null)
            {
                continue;
            }

            if (comp.login(user.name, user.pass) == 1)
            {
                credential = user.name;
                return true;
            }
        }

        // ② 目标的 admin 账号：密码存在公开字段 adminPass，与 users[0].pass 同源
        //    （Computer.cs:122-123 构造，存档读回 Computer.cs:1117-1120）。
        //    注意 login 内部对 admin 是「用户名 admin + 密码等于 adminPass 即 giveAdmin」，
        //    不走 users 表比对。
        if (comp.adminPass != null && comp.login("admin", comp.adminPass) == 1)
        {
            credential = "admin";
            return true;
        }

        return false;
    }

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
    /// 目标解析结果。<see cref="Targets"/> 是要入侵的机器；<see cref="Skipped"/>
    /// 是被剔除、但仍会被清痕的机器 —— 它们的 /log 里留着此前侦察与入侵的痕迹，
    /// 不因「跳过入侵」而豁免清痕。
    ///
    /// 曾还有一个 <c>SkippedHopeless</c>（因端口表凑不够提权门槛而跳过）——
    /// 那道剔除已删除，字段随之作废。理由见 <see cref="ResolveTargets"/>。
    /// </summary>
    internal readonly record struct TargetPlan(
        List<Computer> Targets,
        List<Computer> Skipped,
        int SkippedOwned);

    /// <summary>
    /// 解析目标集合。目标串走框架查找表。
    ///
    /// 两道过滤：无条件跳过 <c>null</c> / <c>disabled</c> / 玩家自己；全网扫描时
    /// 按 <see cref="HackOptions.SkipOwned"/> 跳过已控机器。其余<b>全部</b>入侵 ——
    /// 包括端口表凑不够提权门槛的那些（它们由 <see cref="ForceEscalate"/> 拿下）。
    /// </summary>
    internal static TargetPlan ResolveTargets(OS os, HackOptions options)
    {
        if (os == null)
        {
            return new TargetPlan([], [], 0);
        }

        var pool = options.Scope switch
        {
            HackScope.Connected => ConnectedPool(os),
            HackScope.Network => options.AllNodes ? ConnectableComputers(os) : ReachableComputers(os),
            _ => options.Targets
                    .Select(id => ComputerLookup.Find(id))
                    .Where(comp => comp != null)
                    .ToArray(),
        };

        // 已控剔除**只对全网扫描生效**。「当前节点」与显式点名是刻意选择，一律尊重 ——
        // 玩家连上某台机器再点 START，就是明确要打它，哪怕它已经是自己的肉鸡。
        // 白名单回退那条路同理（见 ConnectedPool）。
        var sweep = options.Scope == HackScope.Network;
        var skipOwned = options.SkipOwned && sweep;

        var result = new List<Computer>(pool.Length);
        var skipped = new List<Computer>();
        var skippedOwned = 0;

        // 用哈希集去重，而不是 List.Contains：显式目标串可能重复点名，
        // 而全网遍历的池本身已去重，两者都需要 O(1) 判重。
        var seen = new HashSet<Computer>(pool.Length);

        foreach (var comp in pool)
        {
            // 无条件剔除：拿不到的对象、disabled、以及玩家自己。
            if (comp == null || comp.disabled || ReferenceEquals(comp, os.thisComputer))
            {
                continue;
            }

            // 已控（adminIP 已是玩家）的机器：全网扫描时按开关跳过。
            // 玩家此前打过谁不该决定这一轮的结果，故这是**显式开关**（面板 skip owned
            // 复选框 / 命令行 redo 的反面），而不是一条隐式的「智能」判据。
            if (skipOwned && IsOwned(comp, os))   // skipOwned 已含 sweep 条件，见上
            {
                skippedOwned++;
                skipped.Add(comp);
                continue;
            }

            // **这里曾有第二道剔除：「端口表容量 ≤ 提权门槛的不打」。v1.33.1 已删除。**
            // 它把 22 台机器整个排除在目标池外，其中 9 台是游戏自带的防护机
            // （portsToCrack=9999998，实测 EnTech 中继服务器系列），另外 13 台是门槛
            // 2~8 而端口表只有 1~4 个的普通机器 —— 两类都是 porthack 判据恒假
            // （要求已攻破端口数**严格大于**门槛，OS.cs:1916），**破满端口也差 1~6 个**。
            // 把它们排除，等于「明明有一个能拿下的办法（强行提权），却装作没有」。
            // 现在它们进目标池，Escalate 步在门禁不过时走 ForceEscalate 拿下。
            if (seen.Add(comp))
            {
                result.Add(comp);
            }
        }

        return new TargetPlan(result, skipped, skippedOwned);
    }

    /// <summary>
    /// 「当前节点」的目标池：已连接的机器；没连上时反推**刚被拒的那台**。
    ///
    /// 为什么需要反推：地图上点节点走 <c>os.runCommand("connect " + ip)</c>
    /// （NetworkMap.cs:573），而白名单会拒这个连接 —— <c>Computer.connect</c>
    /// 调 <c>DisconnectTarget()</c> 后 return false（Computer.cs:383-388），
    /// <c>Programs.connect</c> 写完 "External Computer Refused Connection" 再把
    /// <c>os.connectedComp</c> 置 null（Programs.cs:313-322）。于是目标**既没连上、
    /// 也不进任何列表** —— 只看 <c>connectedComp</c> 的话，「当前节点」就无从知道
    /// 玩家点的是哪台，表现为「敲了 autohack 什么也没发生」（池空 ⇒ Total == 0）。
    ///
    /// 反推依据见 <see cref="RefusedWhitelist.LastRefused"/>。玩家是**显式**点的那个节点，
    /// 与 <c>here</c>、点名同级，故按「刻意选择」处理：不做 SkipOwned 剔除。
    /// </summary>
    private static Computer[] ConnectedPool(OS os)
        => os.connectedComp is { } connected
            ? [connected]
            : RefusedWhitelist.LastRefused(os) is { } refused ? [refused] : [];

    /// <summary>目标池为空时的补充说明 —— 面板与命令行两个入口共用，措辞只写一次。</summary>
    internal const string NoTargetHint =
        "[autohack]   'here' follows the session; nothing is connected - "
        + "name the node, or use 'allnodes'.";

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
    /// 先把地图揭开，再开始入侵 —— 用户定的次序（「先扫描，后入侵」）。
    ///
    /// <b>为什么不改目标池。</b>Network 口径的池来自 <see cref="ReachableComputers"/>，
    /// 那是沿 <c>links</c> 的传递闭包；揭图只是把闭包里的下标写进 <c>visibleNodes</c>，
    /// 不增不减闭包本身。allnodes 口径的池来自 <see cref="ConnectableComputers"/>，
    /// 那是地图全表，与 <c>visibleNodes</c> 完全无关。故这一步是纯粹的观感前置。
    ///
    /// <b>它补的是什么。</b>Network 口径本来就在顺带揭图（<see cref="Closure"/> 的
    /// BFS 逐节点调 <c>NetworkMap.discoverNode</c>），而 allnodes 口径走的是
    /// <see cref="ConnectableComputers"/> —— 那条路只收集、不 <c>discoverNode</c>，
    /// 于是「地图全开」跑完，地图上仍是一台都没亮。这里的 <paramref name="allNodes"/>
    /// 分支正是补这个缺口。
    ///
    /// 标记一律走原生 <c>NetworkMap.discoverNode</c>（NetworkMap.cs:415-423），
    /// 与游戏自身的「已发现」同源，不做自绘的伪发现。
    /// </summary>
    /// <param name="allNodes">true = 地图全表；false = 当前节点所在的无向连通分量。</param>
    internal static void RevealMap(OS os, bool allNodes)
    {
        var map = os?.netMap;
        if (map?.nodes == null || map.nodes.Count == 0)
        {
            return;
        }

        if (!allNodes)
        {
            // 连通分量口径：ReachableFrom 的 BFS 已带 reveal，直接复用 —— 不另立第二套遍历。
            // 未连接时没有「当前节点」，此时什么都不揭（与 scan 工具的判据一致）。
            if (os.connectedComp != null)
            {
                ReachableFrom(os, os.connectedComp);
            }

            return;
        }

        // 地图全表：逐个标记。discoverNode 内部是 visibleNodes.Contains(nodes.IndexOf(c))
        // （NetworkMap.cs:417），两次线性扫描，故这里是 O(V²)。实测存档 167 台无感；
        // 千级地图才需要改走哈希集，本次不做（YAGNI）。
        foreach (var comp in map.nodes)
        {
            if (comp != null && !comp.disabled)
            {
                map.discoverNode(comp);
            }
        }
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

    /// <summary>
    /// 工具的缺省目标集合 —— 与 <c>autohack run</c> 的 <c>allnodes</c> 口径一致：
    /// 缺省只作用于当前连接的节点，未连接时退到玩家自己的机器（工具在本地也讲得通，
    /// 例如解自己的 DEC 文件）；<c>allnodes</c> 时扫地图全表。
    ///
    /// 抽出来是因为 dec 与 mem 各写了一遍同一个三元式，加工具还会再抄一遍 ——
    /// 而「工具作用在哪些机器上」是必须处处一致的口径。
    /// </summary>
    internal static Computer[] ToolTargets(OS os, bool allNodes)
        => allNodes
            ? ConnectableComputers(os)
            : [os.connectedComp ?? os.thisComputer];

    /// <summary>
    /// 抹除目标的 /log 目录，等价于原版终端 <c>rm log/*</c>；返回被删除的文件名，供回显与计数。
    ///
    /// 动作全部交给 <see cref="RemoveFiles"/> —— 痕迹清理与面板 <c>purge</c> 是同一个
    /// 「删光一个目录」的动作，只该有一份实现。
    ///
    /// 「删除动作本身不留新痕迹」的来源：/log 里的文件名形如
    /// <c>@&lt;时间&gt;_&lt;消息&gt;</c>（<c>Computer.log</c> 用
    /// <c>text.Replace(" ", "_")</c> 作 <see cref="FileEntry.name"/>，
    /// Computer.cs:338-354），以 '@' 开头 ⇒ deleteFile 跳过
    /// <c>log("FileDeleted: ...")</c> 自写（Computer.cs:543）。
    /// </summary>
    /// <summary>
    /// 该机器是否带白名单认证器（<c>WhitelistConnectionDaemon</c>）—— 连接会被它拒绝。
    ///
    /// 判定走 <c>getDaemon</c>，与游戏自己的取法一致（<c>Computer.connect</c> 内部即如此，
    /// Computer.cs:383）。判 daemon 有无而不是「这台机器会不会拒我」：后者要看
    /// <c>IPCanPassWhitelist</c>（Computer.cs:123-160）的完整语义（含
    /// <c>AuthenticatesItself</c>、<c>RemoteSourceIP</c> 递归、list.txt 逐行比对），
    /// 而真正的判据就在游戏里 —— 本方法只用来决定「要不要排一个绕过步骤」，
    /// 排了没生效也无害（见 <see cref="BypassWhitelist"/> 的返回值语义）。
    /// </summary>
    internal static bool HasWhitelist(Computer comp)
        => comp?.getDaemon(typeof(WhitelistConnectionDaemon)) is WhitelistConnectionDaemon;

    /// <summary>
    /// 把玩家 IP 追加进目标的白名单文件，让游戏下次放行连接。返回是否真的写进去了。
    ///
    /// <b>为什么不能只做一步</b>：白名单 daemon 有两种形态，放行文件不同 ——
    /// 普通型（无 `Remote`）建 `/Whitelist/list.txt`；而 `Remote=` 型改建 `source.txt`，
    /// **根本没有 list.txt**（WhitelistConnectionDaemon.cs:36-55）。官方任务
    /// PAE2_Target.xml、PA_Bookings_Mainframe.xml 都是这一型。故「追加自己的 IP」
    /// 只对前一种有效。
    ///
    /// <b>两条互补的路，都取游戏自己的判据</b>（`IPCanPassWhitelist`，
    /// WhitelistConnectionDaemon.cs:123-160）：
    /// <list type="number">
    /// <item><b>追加</b> —— `list.txt` 逐行 trim 比对玩家 IP（:151-158）。官方正路：
    ///   PAE2_Whitelist.xml 里的 `list_add_manual.txt` 明写「append list.txt &lt;你的IP&gt;」。</item>
    /// <item><b>删关键文件</b> —— 该函数有两处「文件不在就 return true」：
    ///   `authenticator.dll` 缺失（:142-145）、`list.txt` 缺失（:147-150）。
    ///   删掉任一即**无条件放行**，且这条不看 `AuthenticatesItself`、也不走
    ///   `RemoteSourceIP` 的递归委托 —— 是对付 `Remote=` 型的唯一办法。
    ///   这不是取巧：官方在 PAE2_Whitelist.xml 注释里就写着任务「is completable if
    ///   the player adds their own IP to the whitelist **or just crashes it's critical files**」。</item>
    /// </list>
    ///
    /// 两步都做（用户定）：先追加（正路、不破坏对方语义），再删两个文件把放行钉死。
    ///
    /// <b>为什么能改到它</b>：白名单只拦 `connect`（Computer.cs:383-388），文件系统
    /// 并未因此上锁 —— `deleteFile` 的门禁判 `ipFrom.Equals(adminIP)`（Computer.cs:511-517），
    /// 提权后即通过。故「先拿下这台机器，再改它的白名单」可行 —— 这正是本步骤
    /// 排在 Escalate 之后的原因。
    /// <returns>做了什么（供回显）；无可为时返回 null。</returns>
    internal static string BypassWhitelist(OS os, Computer comp, string playerIP)
    {
        var notes = new List<string>();

        // 认证源链要一起处理。判据照抄游戏的递归结构（IPCanPassWhitelist，
        // WhitelistConnectionDaemon.cs:129-141）：本机若带 source.txt，它的白名单
        // 实际由 source.txt 指向的那台裁决 —— 只改本机等于没改。
        foreach (var host in WhitelistChain(os, comp))
        {
            var note = BypassOne(host, playerIP, ReferenceEquals(host, comp));
            if (note != null)
            {
                notes.Add(note);
            }
        }

        return notes.Count == 0 ? null : string.Join("; ", notes);
    }

    /// <summary>
    /// 白名单裁决链：本机 + 它 <c>source.txt</c> 指向的认证服务器（若有）。
    ///
    /// 游戏是递归的：本机带 <c>RemoteSourceIP</c> 时把判决**委托**给那台的
    /// <c>IPCanPassWhitelist(ip, isFromRemote: true)</c>（WhitelistConnectionDaemon.cs:140）。
    /// 官方任务的典型拓扑就是这样：被保护的机器只有 <c>source.txt</c>（存认证服务器 IP），
    /// 认证服务器自己才有 <c>list.txt</c> —— 实测存档里 `dpa_bookings` 的 source.txt = 
    /// `26.217.89.33`（`dpa_whitelist` 的 IP），而 `dpa_whitelist` 的 list.txt 只列了
    /// 5 个 NPC IP。故**必须改认证服务器那台**，只改本机对它一点影响都没有。
    /// </summary>
    private static IEnumerable<Computer> WhitelistChain(OS os, Computer comp)
    {
        yield return comp;

        var source = comp?.files?.root?.searchForFolder(WhitelistFolderName)?.searchForFile(WhitelistSourceFilename);
        var remoteIP = source?.data?.Trim();
        if (string.IsNullOrEmpty(remoteIP))
        {
            yield break;
        }

        var remote = Programs.getComputer(os, remoteIP);
        if (remote != null && !ReferenceEquals(remote, comp))
        {
            yield return remote;
        }
    }

    /// <summary>
    /// 处理单台机器的白名单：追加玩家 IP，再删掉两个关键文件把放行钉死。
    /// </summary>
    /// <param name="isSelf">是否是被保护的本机（只为回显措辞区分）。</param>
    private static string BypassOne(Computer comp, string playerIP, bool isSelf)
    {
        var folder = comp?.files?.root?.searchForFolder(WhitelistFolderName);
        if (folder == null)
        {
            return null;
        }

        var notes = new List<string>();
        var tag = isSelf ? "" : "on " + comp.name + " ";

        // ① 追加（已在名单则不重复写 —— 幂等是硬要求，每轮都会跑）
        var list = folder.searchForFile(WhitelistListFilename);
        if (list != null && !AlreadyAllowed(list.data, playerIP))
        {
            list.data += "\n" + playerIP;
            notes.Add(tag + "IP appended to list.txt");
        }

        // ② 删两个关键文件，把放行钉死。
        //    直接从 List 移除，不走 Computer.deleteFile：它对每个非 '`' 文件调 log()，
        //    而 log() 是 searchForFolder("log").files.Insert(...)（Computer.cs:338-354），
        //    目标机没有 log 夹时 NRE —— 官方 Extension 的节点常常如此。
        //    这里只需要文件消失，不需要游戏的审计记录。
        var dropped = new List<string>();
        foreach (var name in new[] { WhitelistAuthFilename, WhitelistListFilename })
        {
            var file = folder.searchForFile(name);
            if (file != null)
            {
                folder.files.Remove(file);
                dropped.Add(name);
            }
        }

        if (dropped.Count > 0)
        {
            notes.Add(tag + "dropped " + string.Join(" + ", dropped));
        }

        return notes.Count == 0 ? null : string.Join("; ", notes);
    }

    /// <summary>
    /// 白名单文件里是否已有该 IP。判据照抄游戏：按换行切分、逐行 <c>Trim()</c> 后比对
    /// （Computer.cs:151-158）。
    /// </summary>
    private static bool AlreadyAllowed(string data, string playerIP)
    {
        if (string.IsNullOrEmpty(data) || string.IsNullOrEmpty(playerIP))
        {
            return false;
        }

        foreach (var line in data.Split('\n', '\r'))
        {
            if (string.Equals(line.Trim(), playerIP, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

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

    /// <summary>
    /// 静默断开当前连接。未连接时是空操作 —— 避免多余的 "Disconnected" 噪音。
    ///
    /// <c>silent</c> 是游戏自己的 public 开关（Computer.cs:57），
    /// Multiplayer.cs:125-127 就是「set true → 操作 → 还原」这个用法。
    /// 断开本身会往目标 /log 写 "&lt;ip&gt; Disconnected"（<c>Computer.disconnecting</c>，
    /// Computer.cs:722-727），而清痕必须排在断开**之前**（要连着目标的文件系统才作数），
    /// 不静音就等于清完立刻被写回一条。
    ///
    /// 多人对局不对它静音：同一个 <c>!silent</c> 门还守着
    /// <c>sendNetworkMessage("cDisconnect ...")</c>（Computer.cs:728-731），
    /// 静音会连断线同步一起吞掉，对面看到的还是「连着」。
    /// 日志保真让位于联机状态保真 —— 单机下这个分支恒真，多人才走 else。
    /// </summary>
    internal static void SilentDisconnect(OS os)
    {
        var leaving = os?.connectedComp;
        if (leaving == null)
        {
            return;
        }

        if (os.multiplayer)
        {
            Programs.disconnect(["dc"], os);
            return;
        }

        var wasSilent = leaving.silent;
        leaving.silent = true;
        try
        {
            Programs.disconnect(["dc"], os);
        }
        finally
        {
            leaving.silent = wasSilent;
        }
    }

    /// <summary>原版破解程序表里是否有该端口的破解程序（含扩展注册项）。</summary>
    private static bool HasCrackProgram(int codePort)
        => PortExploits.cracks != null && PortExploits.cracks.ContainsKey(codePort);
}

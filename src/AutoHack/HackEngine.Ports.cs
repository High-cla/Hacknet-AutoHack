// HackEngine.Ports.cs —— 端口状态、破解命令与提权判定。
//
// HackEngine 的分部实现；类型声明、字段与其余职责见 HackEngine.cs。
namespace AutoHack;

using Hacknet;
using Pathfinder.Port;
using Pathfinder.Util;

internal static partial class HackEngine
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

        // GetPortStateDict() 交出的是框架 PortTable 的内部字典本体；GetAllPortStates()
        // 则是它的 Values.ToList()，每台机器多拷一份 List。本方法每台目标调一次
        // （167 台存档即 167 次），而下面的读法两种来源完全等价 —— 枚举顺序同为
        // 插入顺序（无删除），故端口在报告里的先后不变。
        var states = comp.GetPortStateDict();
        if (states != null && states.Count > 0)
        {
            var fromStates = new List<PortInfo>(states.Count);
            foreach (var pair in states)
            {
                var state = pair.Value;
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
        // openPorts。故 PortTable 恒非空，上面 states.Count > 0 必先返回。
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

    /// <summary>
    /// 该机器上可自动攻破的端口 = 原生有破解程序的（<c>PortExploits.cracks</c>，不硬编码）
    /// ∪ 玩家在 <c>modports=</c> 里显式点名的模组协议。
    ///
    /// 后一半需要手动表：<c>cracks</c> 只含原生 36 个端口，workshop 注册的 16 个一个都不在其中，
    /// 且没有任何模组往 <c>cracks</c> 写过。理由与取舍见 <see cref="ModPortPolicy"/>。
    /// </summary>
    /// <param name="modPorts">允许开模组端口的协议名；<c>null</c>/空 = 一个都不开（缺省）。</param>
    internal static IReadOnlyList<PortInfo> CrackablePorts(Computer comp, IReadOnlyCollection<string> modPorts = null)
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
            // CodePort <= 0 一律排除：ZeroDayToolKit 注册的 "backdoor" 缺省端口就是 0
            // （ZeroDayToolKit.decompiled.cs:148），端口号 0 在游戏里无意义，开了也匹配不到任何东西。
            if (port.Cracked || port.CodePort <= 0)
            {
                continue;
            }

            if (!HasCrackProgram(port.CodePort) && !ModPortPolicy.Allows(modPorts, port.Protocol))
            {
                continue;
            }

            if (seen.Add(port.CodePort))
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

    /// <summary>
    /// 已攻破端口数（框架语义：<see cref="PortState.Cracked"/>）。
    ///
    /// <b>不用框架的 <c>CountOpenPorts()</c></b> —— 它是
    /// <c>GetAllPortStates().Count(x =&gt; x.Cracked)</c>（ComputerExtensions.cs:164-167），
    /// 而 <c>GetAllPortStates()</c> 是 <c>PortTable...Values.ToList()</c>（:135-138）：
    /// 每调一次先拷一份 List，再走一遍委托。本方法被 <see cref="PortQuotaMet"/> 在
    /// **每个端口步**上调用一次（v1.35.1 实测一轮 487 次），拷贝纯属浪费。
    /// <c>GetPortStateDict()</c>（:146-149）直接交出框架 PortTable 的内部字典本体，
    /// 零拷贝；只读遍历不改其内容。
    /// </summary>
    internal static int OpenPortCount(Computer comp)
    {
        if (comp == null)
        {
            return 0;
        }

        var open = 0;
        foreach (var pair in comp.GetPortStateDict())
        {
            if (pair.Value != null && pair.Value.Cracked)
            {
                open++;
            }
        }

        return open;
    }

    /// <summary>
    /// 已开端口数是否已达提权门槛 —— 与游戏 porthack 门禁的<b>数量那一半</b>逐字一致
    /// （<c>OS.cs:1916</c> 的 <c>num2 &gt; connectedComp.portsNeededForCrack</c>；
    /// Pathfinder 的 <c>FixPortHack</c> IL 注入把那个 <c>portsOpen</c> 求和换成了
    /// <c>CountOpenPorts()</c>，见 ComputerExtensions.cs:486-513 —— 故两边读的是同一份数据）。
    ///
    /// <b>拆出来是给 <c>HackRun</c> 用的。</b>porthack 只看端口<b>总数</b>，不看具体是哪些
    /// 端口，故端口步破到够数就该停 —— 剩下的端口对提权毫无贡献，只会白等动画、点燃追踪、
    /// 把终端刷满。实测存档 167 台：可破端口步 650 → 487（省 25.1%），79 台有节省。
    /// </summary>
    internal static bool PortQuotaMet(Computer comp)
        => comp != null && OpenPortCount(comp) > comp.portsNeededForCrack;

    /// <summary>
    /// 原生提权门槛 —— 与游戏 porthack 的门禁逐条对齐（OS.cs:1908-1930）：
    /// 已攻破端口数必须**超过** <c>portsNeededForCrack</c>，且防火墙已解；
    /// 缺其一会写 "Target Machine Rejecting Syndicated UDP Traffic" 并拒绝启动 PortHackExe。
    /// 此前 mod 直接调 <c>giveAdmin</c> 跳过整条门禁，等于从未用过原生的提权与防火墙机制。
    /// </summary>
    internal static bool CanEscalate(Computer comp)
        => PortQuotaMet(comp)
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

    /// <summary>
    /// 真人会敲的破解指令，如 <c>sshcrack 22</c>。程序名取自游戏数据，端口号取显示端口。
    ///
    /// <b>模组端口回显的是占位命令 <c>portcrack</c>。</b>那些端口的破解程序由各自的模组
    /// 注册（<c>RedisBreaker</c> / <c>SSHSwift</c> / <c>MQTTInterceptor</c> …），名字与参数
    /// 语义各不相同（有的要显示端口号、有的要 <c>-s</c> 子命令、有的开的是别人家的端口），
    /// 没有可通用推断的形式 —— 与 v1.33.5 否掉「自动联动模组程序」是同一条理由
    /// （见 ModTools 的类注释）。故这里给一个<b>一眼可辨的占位</b>，而不是编一条
    /// 看起来像真的、实际敲了会报错的命令。端口本身由 <see cref="OpenPort"/> 直接开。
    /// </summary>
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
    /// 原版破解程序表里是否有该端口的破解程序（含扩展注册项）。
    /// 供 <see cref="PortTools"/> 区分「原生端口」与「模组端口」—— 它读的是游戏数据本身，
    /// 与该端口是否被 <c>modports=</c> 放行无关。
    /// </summary>
    internal static bool HasCrackProgram(int codePort)
        => PortExploits.cracks != null && PortExploits.cracks.ContainsKey(codePort);
}

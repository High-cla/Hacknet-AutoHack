namespace AutoHack;

using Hacknet;
using Pathfinder.Port;

/// <summary>
/// 自身加固：把玩家自己这台机器改成「打不动」。
///
/// 这是**不可逆**的 —— 字段值不保留原值，且改完后玩家自己的机器在存档里就是这个
/// 状态。因此执行前后各打印一次全部关键字段，让改动可核对、可手工还原。
///
/// 端口走 Pathfinder 的 <see cref="PortState"/>，不碰原版 <c>portsOpen</c>：
/// Pathfinder 用 Harmony Prefix 把 <c>Computer.openPort/openPorts/closePort/isPortOpen</c>
/// 全部接管了（ComputerExtensions.cs:184-232），原版列表因此永不更新，
/// 写它等于没写。
///
/// 端口号一并换成随机非标准值：加固后的机器不该继续对外亮着 22/21/80 这些
/// 一看就知道对应哪个服务的端口号。
/// </summary>
internal static class HardenTools
{
    /// <summary>远超任何正常机器：DisplayModule 在 &gt;100 时走「INVIOLABILITY」特效分支，不会遍历该数值。</summary>
    private const int Inviolable = 9999998;

    /// <summary>防火墙解法长度（12 位字母数字，与参考实现一致）。</summary>
    private const int SolutionLength = 12;

    /// <summary>随机端口号下界：IANA 动态/私有段起点。高于所有原版协议端口号
    /// （OGPortToProto 里最大 9418），因此不会与协议自身撞号。</summary>
    private const int RandomPortLow = 49152;

    /// <summary>随机端口号上界（含）。</summary>
    private const int RandomPortHigh = 65535;

    /// <summary>候选端口总数（16384），线性探测时的模数。</summary>
    private const int RandomPortCount = RandomPortHigh - RandomPortLow + 1;

    internal static void Run(OS os)
    {
        var comp = os.thisComputer;
        if (comp == null)
        {
            return;
        }

        os.write("[autohack] unbreakable: BEFORE - " + Snapshot(comp) + " ports=" + comp.CountOpenPorts() + ".");

        comp.portsNeededForCrack = Inviolable;
        comp.traceTime = 1f;

        // 走游戏自身的 addProxy：它一次设定 hasProxy/proxyActive/proxyOverloadTicks/
        // startingOverloadTicks 四者（Computer.cs:243-252）。只改 hasProxy 而不改
        // overloadTicks，会让 DisplayModule 按 0/0 算进度条（DisplayModule.cs:670）。
        comp.addProxy(Inviolable);

        var firewallNote = HardenFirewall(comp);
        var remap = HardenPorts(comp);

        os.write("[autohack] unbreakable: AFTER - " + Snapshot(comp) + " ports=" + comp.CountOpenPorts() + ".");
        os.write("[autohack] unbreakable: ports moved to non-standard numbers - " + remap + ".");
        os.write("[autohack] unbreakable: firewall " + firewallNote + ". This is irreversible.");
    }

    /// <summary>有防火墙就换掉解法；没有就不新建（加固不负责凭空造一个出来）。</summary>
    private static string HardenFirewall(Computer comp)
    {
        if (comp.firewall == null)
        {
            return "absent - left as is";
        }

        var solution = new char[SolutionLength];
        for (var i = 0; i < solution.Length; i++)
        {
            solution[i] = Utils.getRandomChar();
        }

        comp.firewall.solution = new string(solution).ToUpperInvariant();
        comp.firewall.solutionLength = comp.firewall.solution.Length;
        comp.firewall.resetSolutionProgress();

        return "solution set to " + comp.firewall.solution;
    }

    /// <summary>
    /// 给原版 15 个协议端口各换一个随机非标准端口号，返回「协议=新端口号」清单。
    ///
    /// 只动 <see cref="PortState.PortNumber"/>（显示端口号）：probe 显示、porthack 反查、
    /// 存档都走这个字段（ComputerExtensions.cs:262/270 双向映射，SaveWriter.cs:184 存 Number）。
    ///
    /// 破解状态一概不碰 —— 加固不是「把端口全打开」，机器应当保持未破解。
    /// （Cracked 本身也不进存档：SaveWriter.cs:184 只写 Original/Number/Display。）
    /// </summary>
    private static string HardenPorts(Computer comp)
    {
        var used = new HashSet<int>();
        foreach (var state in comp.GetAllPortStates())
        {
            used.Add(state.PortNumber);
        }

        var remap = new List<string>();

        foreach (var record in OriginalPorts())
        {
            var state = comp.GetPortState(record.Protocol);

            // 玩家机可能还没建立端口表（存档里没有 ports 段时），先补上。
            if (state == null)
            {
                state = record.CreateState(comp);
                comp.AddPort(state);
            }

            // 只换端口号：破解状态一律不动（保持未破解）。
            var port = TakeRandomPort(used, record.DefaultPortNumber);
            state.PortNumber = port;
            remap.Add(record.Protocol + "=" + port);
        }

        return string.Join(" ", remap);
    }

    /// <summary>
    /// 取一个未被占用的随机端口号，并登记进 used。
    ///
    /// 用「随机起点 + 线性探测」而不是「随机重试」：候选 16384 个、需求 15 个，
    /// 正常第一次就命中，而线性探测保证撞了必然换到下一个，不会无界重试。
    /// 显示端口号重复会让 display→code 反查歧义（ComputerExtensions.cs:262 取 FirstOrDefault）。
    /// </summary>
    /// <param name="fallback">整段候选都被占用时的退路，正常永远用不到。</param>
    private static int TakeRandomPort(HashSet<int> used, int fallback)
    {
        var start = Utils.random.Next(RandomPortCount);

        for (var i = 0; i < RandomPortCount; i++)
        {
            var port = RandomPortLow + (start + i) % RandomPortCount;
            if (used.Add(port))
            {
                return port;
            }
        }

        return fallback;
    }

    /// <summary>
    /// 原版协议表：<c>PortExploits.portNums</c> 里凡有服务名的端口，都能经
    /// <see cref="PortManager.GetPortRecordFromNumber"/> 查到 Pathfinder 记录
    /// —— 与 Pathfinder 内部构造 <c>OGPorts</c> 的口径一致（ComputerExtensions.cs:46-51）。
    /// 不直接用 OGPorts 是因为它是 internal，跨程序集不可见。
    /// </summary>
    private static List<PortRecord> OriginalPorts()
    {
        var records = new List<PortRecord>();

        if (PortExploits.portNums == null || PortExploits.services == null)
        {
            return records;
        }

        foreach (var port in PortExploits.portNums)
        {
            if (!PortExploits.services.ContainsKey(port))
            {
                continue;
            }

            var record = PortManager.GetPortRecordFromNumber(port);
            if (record != null)
            {
                records.Add(record);
            }
        }

        return records;
    }

    private static string Snapshot(Computer comp)
        => "portsToCrack=" + comp.portsNeededForCrack
           + " traceTime=" + comp.traceTime
           + " proxy=" + comp.hasProxy + "/" + comp.proxyActive
           + "/" + comp.proxyOverloadTicks + "/" + comp.startingOverloadTicks;
}

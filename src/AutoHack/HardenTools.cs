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
/// </summary>
internal static class HardenTools
{
    /// <summary>远超任何正常机器：DisplayModule 在 &gt;100 时走「INVIOLABILITY」特效分支，不会遍历该数值。</summary>
    private const int Inviolable = 9999998;

    /// <summary>防火墙解法长度（12 位字母数字，与参考实现一致）。</summary>
    private const int SolutionLength = 12;

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
        var opened = HardenPorts(comp);

        os.write("[autohack] unbreakable: AFTER - " + Snapshot(comp) + " ports=" + comp.CountOpenPorts() + ".");
        os.write("[autohack] unbreakable: firewall " + firewallNote + ", " + opened + " port(s) opened. This is irreversible.");
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

    /// <summary>把原版 15 个协议端口全部置为已开，返回实际打开的数量。</summary>
    private static int HardenPorts(Computer comp)
    {
        var opened = 0;

        foreach (var record in OriginalPorts())
        {
            var state = comp.GetPortState(record.Protocol);

            // 玩家机可能还没建立端口表（存档里没有 ports 段时），先补上再开。
            if (state == null)
            {
                state = record.CreateState(comp, null, record.DefaultPortNumber);
                comp.AddPort(state);
            }

            if (state.Cracked)
            {
                continue;
            }

            state.SetCracked(true, comp.ip);
            opened++;
        }

        return opened;
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

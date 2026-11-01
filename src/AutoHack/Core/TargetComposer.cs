namespace AutoHack.Core;

/// <summary>
/// 把「裸事实」组装成 <see cref="TargetView"/>。
///
/// <b>为什么要有这一层</b>（v1.45.0 审计 P0-3）：素材化原先整个写在适配器
/// <c>GameWorldView.Of</c> 里，而那两条真正的判据 —— 「哪些端口可破」「已破几个」——
/// 也在其中。适配器引用 <c>Hacknet</c> 类型，裸进程连 <c>new Computer(...)</c> 都会崩，
/// 于是这两条判据永远无法离线验证，出错只能进游戏才暴露。
///
/// 现在判据在这里（纯函数、可测），适配器只负责把 <c>Computer</c> 的字段读成参数。
/// 端口清单的读取（<c>GetPortStateDict</c> → <see cref="PortView"/>）仍在适配器，
/// 那是纯搬运，没有判断。
/// </summary>
internal static class TargetComposer
{
    /// <summary>
    /// 组装一台目标的快照。
    ///
    /// <param name="allPorts">该机<b>全部</b>端口（含已破、含不可破）—— 已破计数比的是全表，
    /// 与提权门槛 <c>portsNeededForCrack</c> 同一口径；可破清单只从中筛。</param>
    /// <param name="hasCrackProgram">该端口号是否有原生破解程序（游戏数据表，由适配器提供）；
    /// 传 <c>null</c> 视为「一个都没有」。</param>
    /// <param name="modPorts">允许开模组端口的协议名；<c>null</c>/空 = 一个都不开。</param>
    /// </summary>
    internal static TargetView Compose(
        string ip,
        string name,
        bool isOwned,
        bool isEos,
        bool hasProxy,
        bool proxyActive,
        bool hasFirewall,
        bool firewallSolved,
        bool hasWhitelist,
        int portsNeeded,
        IReadOnlyList<PortView> allPorts,
        Func<int, bool> hasCrackProgram,
        IReadOnlyCollection<string> modPorts)
    {
        var ports = allPorts ?? Array.Empty<PortView>();
        return new TargetView(
            ip,
            name,
            isOwned,
            isEos,
            hasProxy,
            proxyActive,
            hasFirewall,
            firewallSolved,
            hasWhitelist,
            portsNeeded,
            CountOpen(ports),
            Crackable(ports, hasCrackProgram, modPorts));
    }

    /// <summary>
    /// 已攻破端口数 —— <b>全表口径</b>，含不可破的端口。
    ///
    /// 提权门槛（<c>portsNeededForCrack</c>）比的就是这个数：游戏自己的 porthack 也是数
    /// <c>GetAllPortStates().Count(x =&gt; x.Cracked)</c>，与「能不能破解」无关。
    /// 写成静态纯函数是为了让这条口径只有一处。
    /// </summary>
    internal static int CountOpen(IReadOnlyList<PortView> ports)
    {
        if (ports == null)
        {
            return 0;
        }

        var open = 0;
        for (var i = 0; i < ports.Count; i++)
        {
            if (ports[i].Cracked)
            {
                open++;
            }
        }

        return open;
    }

    /// <summary>
    /// 可自动攻破的端口 = 原生有破解程序的 ∪ <c>modports=</c> 显式点名的模组协议。
    ///
    /// 三条排除规则（与 v1.34.0 起的口径逐字一致）：
    /// <list type="number">
    /// <item>已破的不再排（没有意义，也会浪费动画与回显）；</item>
    /// <item><c>CodePort &lt;= 0</c> 一律排除 —— ZeroDayToolKit 注册的 <c>backdoor</c>
    ///   缺省端口就是 0，游戏里开了也匹配不到任何东西；</item>
    /// <item>同一端口号只留第一个 —— <c>modports</c> 与原生表可能指向同一个号。</item>
    /// </list>
    /// </summary>
    internal static IReadOnlyList<PortView> Crackable(
        IReadOnlyList<PortView> ports,
        Func<int, bool> hasCrackProgram,
        IReadOnlyCollection<string> modPorts)
    {
        if (ports == null || ports.Count == 0)
        {
            return Array.Empty<PortView>();
        }

        var found = new List<PortView>(ports.Count);
        var seen = new HashSet<int>();
        for (var i = 0; i < ports.Count; i++)
        {
            var port = ports[i];
            if (port.Cracked || port.CodePort <= 0)
            {
                continue;
            }

            if ((hasCrackProgram == null || !hasCrackProgram(port.CodePort))
                && !ModPortPolicy.Allows(modPorts, port.Protocol))
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
}
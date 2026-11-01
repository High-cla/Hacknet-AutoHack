namespace AutoHack;

using AutoHack.Core;
using Hacknet;

/// <summary>
/// 世界适配器：把游戏状态回答成核心要的那五个问题，并把 <c>Computer</c> 素材化成 <see cref="TargetView"/>。

/// <b>只读</b>：这里没有任何写操作 —— 所有写入都以 <see cref="GameAction"/> 的形式由
/// <see cref="HackRun"/> 落到游戏 API 上。两个方向分开，核心才能在不加载游戏的情况下被测试。
///
/// <b>为什么持有 IP → Computer 字典而不是每次查地图</b>：<c>OpenPortCount</c> /
/// <c>AnimationInFlight</c> 在每个端口步上都会被问一次（一轮可达数百次），而目标池在计划期
/// 就已经确定 —— 每次去 <c>netMap.nodes</c> 线性找一遍是纯浪费。
/// </summary>
internal sealed class GameWorldView : IWorldView
{
    private readonly OS _os;
    private readonly IReadOnlyDictionary<string, Computer> _byIp;

    internal GameWorldView(OS os, IReadOnlyDictionary<string, Computer> byIp)
    {
        _os = os;
        _byIp = byIp;
    }

    /// <summary>当前连接的目标 IP；未连接时为 null。</summary>
    public string ConnectedIp => _os?.connectedComp?.ip;

    /// <summary>全表已破端口数（与 porthack 门槛同一份数据）。</summary>
    public int OpenPortCount(string targetIp) => PortEngine.OpenPortCount(Find(targetIp));

    /// <summary>该目标是否已被玩家拿下（<c>adminIP</c> 指向玩家）。</summary>
    public bool IsOwned(string targetIp) => HackEngine.IsOwned(Find(targetIp), _os);

    /// <summary>该目标名下仍在排队或播放的原生动画条数。</summary>
    public int AnimationInFlight(string targetIp) => NativeExes.InFlight(Find(targetIp));

    /// <summary>该目标交给动画的端口是否全部落地。</summary>
    public bool AnimationSettled(string targetIp) => NativeExes.Settled(Find(targetIp));

    /// <summary>把一台机器素材化成快照。<c>Ports</c> 只含可破端口（见 <see cref="TargetView"/>）。</summary>
    internal TargetView Of(Computer comp, HackOptions options)
    {
        if (comp == null)
        {
            return null;
        }

        // 判据在 Core（<see cref="TargetComposer"/>），这里只读字段与端口清单 ——
        // 唯一无法离线测的部分被压到「纯搬运」，见该类的注释（v1.45.0 审计 P0-3）。
        return TargetComposer.Compose(
            comp.ip,
            comp.name,
            HackEngine.IsOwned(comp, _os),
            HackEngine.IsEosDevice(comp),
            comp.hasProxy,
            PortEngine.ProxyActive(comp),
            comp.firewall != null,
            comp.firewall?.solved ?? false,
            WhitelistGate.HasWhitelist(comp),
            comp.portsNeededForCrack,
            PortEngine.Ports(comp),
            HasCrackProgram,
            options.ModPorts);
    }

    /// <summary>批量素材化（保持输入顺序）。</summary>
    internal IReadOnlyList<TargetView> Of(IReadOnlyList<Computer> computers, HackOptions options)
    {
        var views = new List<TargetView>(computers.Count);
        foreach (var comp in computers)
        {
            var view = Of(comp, options);
            if (view != null)
            {
                views.Add(view);
            }
        }

        return views;
    }

    /// <summary>
    /// 原生破解程序表查询（<c>PortExploits.cracks</c>）。缓存成委托：每台目标素材化都会用到，
    /// 而方法组转换每次都会新建一个委托实例。
    /// </summary>
    private static readonly Func<int, bool> HasCrackProgram = PortEngine.HasCrackProgram;

    private Computer Find(string ip)
    {
        if (ip == null || _byIp == null)
        {
            return null;
        }

        return _byIp.TryGetValue(ip, out var comp) ? comp : null;
    }
}

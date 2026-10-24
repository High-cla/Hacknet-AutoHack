namespace AutoHack.Core;

/// <summary>
/// 一台机器的只读快照 —— 核心层做计划与判据所需的全部事实，别无其他。
///
/// <b>为什么是快照而不是接口</b>：核心若定义 <c>IComputer</c> 再转手去读游戏字段，
/// 隔离只是绕了一圈；快照让核心完全不知道 <c>Computer</c> 的存在，也就能在裸进程里被构造。
/// 代价是适配器每帧（或每次规划）做一次素材化 —— 结构体数组，量级与端口数同阶。
///
/// <see cref="Ports"/> 只含<b>可破</b>端口（有原生破解程序，或经 <c>modports</c> 放行的模组协议）——
/// 「哪些端口能破」是游戏数据表的判断，由适配器做好再交进来。
/// <see cref="OpenPortCount"/> 则是全表已破数（含不可破的端口），提权门槛比的是它。
/// </summary>
internal sealed record TargetView(
    string Ip,
    string Name,
    bool IsOwned,
    bool IsEos,
    bool HasProxy,
    bool ProxyActive,
    bool HasFirewall,
    bool FirewallSolved,
    bool HasWhitelist,
    int PortsNeeded,
    int OpenPortCount,
    IReadOnlyList<PortView> Ports);

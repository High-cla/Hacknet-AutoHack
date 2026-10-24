namespace AutoHack.Core;

/// <summary>
/// 单个端口的只读快照 —— 核心层认识的唯一端口形态。
///
/// 三个号各有来历，不能合并：<see cref="Protocol"/> 是 Pathfinder 的协议名（开端口用它，
/// 原版按端口号那条路径已被框架 Prefix 拦下）；<see cref="CodePort"/> 是游戏内部端口号
/// （决定用哪个破解程序）；<see cref="DisplayPort"/> 是玩家在终端读到的号（<c>probe</c> 与
/// <c>sshcrack 22</c> 用的都是它）。<see cref="Cracked"/> 是运行期实时状态。
/// </summary>
internal readonly record struct PortView(
    string Protocol, int CodePort, int DisplayPort, string DisplayName, bool Cracked);

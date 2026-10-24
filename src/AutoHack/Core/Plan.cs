namespace AutoHack.Core;

/// <summary>
/// 一次运行的完整计划：待执行的动作序列 + 计划期快照。
///
/// <b>为什么带快照</b>：状态机每帧要问「这台机器要破几个端口才够」（<c>PortsNeeded</c>），
/// 而目标池在运行期不再重算 —— 把它钉在计划里，判据就不依赖「此刻地图上还有谁」。
/// </summary>
internal sealed record Plan(
    IReadOnlyList<GameAction> Steps,
    IReadOnlyDictionary<string, TargetView> Targets);

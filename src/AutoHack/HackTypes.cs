namespace AutoHack;

using AutoHack.Core;
using Hacknet;

/// <summary>
/// 过渡期的单步载体（v1.42.0 迁移中）：仍持有 <c>Computer</c> 引用，供效果层直接操作。
/// 计划与判定逻辑已迁到 <c>GameAction</c> + <c>TargetView</c>，本类型在效果层改完后删除。
/// </summary>
internal readonly record struct HackStep(ActionKind Kind, Computer Target, PortView Port, string Command);

namespace AutoHack;

using AutoHack.Core;
using Hacknet;

/// <summary>
/// 目标上的一个端口（统一视图，全部取自 Pathfinder 端口表）。
/// <c>CodePort</c> 是游戏内部端口号（<see cref="PortRecord.OriginalPortNumber"/>），
/// <c>DisplayPort</c> 是玩家在终端看到的端口号（<see cref="PortState.PortNumber"/>）。
/// </summary>
internal readonly record struct PortInfo(string Protocol, int CodePort, int DisplayPort, string DisplayName, bool Cracked);

/// <summary>
/// 入侵计划中的单步（不可变）。
/// <see cref="Port"/> 仅 OpenPort 步有意义；<see cref="Command"/> 是要回显到终端的指令原文，可为 null。
/// </summary>
internal readonly record struct HackStep(HackStepKind Kind, Computer Target, PortInfo Port, string Command);

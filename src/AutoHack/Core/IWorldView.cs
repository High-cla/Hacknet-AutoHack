namespace AutoHack.Core;

/// <summary>
/// 运行期的实时事实 —— 计划期不读它，状态机每帧读。
///
/// 只有五个问题：连没连、拿没拿下、破了几个端口、多少动画在途、这台交给动画的端口落定了没。
/// 其余全部来自 <see cref="TargetView"/> 快照。<b>不提供写操作</b>：写入一律走
/// <see cref="GameAction"/>，由执行器落到游戏 API 上。
/// </summary>
internal interface IWorldView
{
    /// <summary>当前连接的目标 IP；未连接时为 null。</summary>
    string ConnectedIp { get; }

    /// <summary>该目标全表已破端口数（与 porthack 门槛同一份数据）。</summary>
    int OpenPortCount(string targetIp);

    /// <summary>该目标是否已被玩家拿下（<c>adminIP</c> 指向玩家）。</summary>
    bool IsOwned(string targetIp);

    /// <summary>该目标名下仍在排队或播放的原生动画条数（演出模式下计入端口门槛）。</summary>
    int AnimationInFlight(string targetIp);

    /// <summary>该目标交给动画的端口是否全部落地（没有在途项即为真）。</summary>
    bool AnimationSettled(string targetIp);
}

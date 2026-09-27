namespace AutoHack;

using System.Runtime.CompilerServices;
using Hacknet;
using HarmonyLib;

/// <summary>
/// 记录「哪台机器的白名单刚刚拒绝了我们的连接」。
///
/// <b>为什么不靠 <c>os.display.commandArgs</c> 反推</b>（试过，实机无效）：
/// 白名单拒绝时 <c>Computer.connect</c> 会调 <c>DisconnectTarget()</c>
/// （Computer.cs:383-388），而它会执行 <c>os.execute("disconnect")</c>
/// （WhitelistConnectionDaemon.cs:79）。<c>ProgramRunner.ExecuteProgram</c> 的
/// <c>disconnect</c> 分支**不像 <c>connect</c> 那样把 <c>flag</c> 置 false**
/// （ProgramRunner.cs:15-23），于是收尾的 <c>os.display.commandArgs = array</c>
/// （ProgramRunner.cs:616）把刚写进去的 <c>["connect", ip]</c> 覆盖成
/// <c>["disconnect"]</c> —— 等 <c>autohack</c> 在下一帧构造 <c>HackRun</c> 时，
/// 那个 ip 已经不在了。实测日志里成对的
/// <c>connect 208.91.196.94</c> / <c>disconnect</c> 正是「地图点击 + 被拒自动断开」。
///
/// 故改从 <c>DisconnectTarget</c> 取：它是游戏自己的「我拒绝了它」动作，
/// <c>Computer.connect</c>（:386）与 <c>navigatedTo</c>（:73）两条拒绝路径都走到，
/// 是唯一权威的信号源。
/// </summary>
[HarmonyPatch]
internal static class RefusedWhitelist
{
    /// <summary>写方在 connect 那条线程、读方在游戏线程，故加锁。</summary>
    private static readonly object Gate = new();

    /// <summary>
    /// 按 OS 实例存。用 <see cref="ConditionalWeakTable{TKey,TValue}"/> 而非普通字典：
    /// 玩家回主菜单再开档会换 OS 实例，弱表不拦 GC，不留长期引用。
    /// </summary>
    private static readonly ConditionalWeakTable<OS, Computer> Last = new();

    /// <summary>
    /// 刚拒过我们的机器；没有记录、或它现在已经会放行时返回 null。
    ///
    /// 「现在会不会放行」用游戏自己的 <c>IPCanPassWhitelist</c> 现算
    /// （WhitelistConnectionDaemon.cs:123-160，纯读取、无副作用）：绕过成功之后
    /// 它返回 true，这条记录自动作废 —— 不必手动清理，也不会把陈年目标
    /// 当成「当前节点」。
    /// </summary>
    internal static Computer LastRefused(OS os)
    {
        if (os == null)
        {
            return null;
        }

        Computer comp;
        lock (Gate)
        {
            if (!Last.TryGetValue(os, out comp))
            {
                return null;
            }
        }

        if (comp == null || comp.disabled || ReferenceEquals(comp, os.thisComputer))
        {
            return null;
        }

        var daemon = comp.getDaemon(typeof(WhitelistConnectionDaemon)) as WhitelistConnectionDaemon;
        if (daemon == null || daemon.IPCanPassWhitelist(os.thisComputer.ip, isFromRemote: false))
        {
            return null;
        }

        return comp;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(WhitelistConnectionDaemon), nameof(WhitelistConnectionDaemon.DisconnectTarget))]
    private static void OnDisconnectTarget(WhitelistConnectionDaemon __instance)
    {
        var comp = __instance?.comp;
        var os = __instance?.os;
        if (comp == null || os == null)
        {
            return;
        }

        lock (Gate)
        {
            Last.Remove(os);
            Last.Add(os, comp);
        }
    }
}

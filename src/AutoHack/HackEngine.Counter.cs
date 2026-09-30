// HackEngine.Counter.cs —— 追踪与反扑：反追踪、抑制管理员反扑、跳板拆除、静默断开。
//
// HackEngine 的分部实现；类型声明、字段与其余职责见 HackEngine.cs。
namespace AutoHack;

using Hacknet;
using Pathfinder.Port;
using Pathfinder.Util;

internal static partial class HackEngine
{

    /// <summary>
    /// 解除目标的「管理员反扑」——「全网入侵失去效果」的根因。
    ///
    /// 断开连接时游戏自身会走 <c>OS.handleDisconnection()</c>（OS.cs:944-950）：
    /// <c>computer.admin?.disconnectionDetected(computer, this)</c>。而
    /// <c>BasicAdministrator.disconnectionDetected</c> / <c>FastBasicAdministrator</c> 会在
    /// 0~20 秒后关掉该机全部端口，并执行 <c>c.adminIP = c.ip</c> —— 把刚写进去的玩家
    /// IP 抹掉，肉鸡标记因此丢失，下次扫描又得重来。Pathfinder 只补了「关端口要按
    /// <c>GetAllPortStates()</c> 的协议名」（ComputerExtensions.cs:418-449），
    /// <b>没有</b>覆盖 <c>adminIP</c> 还原。
    ///
    /// <c>Computer.admin</c> 是游戏原生字段：<c>ComputerLoader</c> 用
    /// <c>type="none"</c> 把它置为 null（ComputerLoader.cs:425），存档里大量节点
    /// 本就是该状态。置 null 的语义是「这台机器没有会反扑的管理员」；玩家的所有权
    /// 由 <c>adminIP</c> 表示，不受影响。断开时的 <c>admin?.</c> 是空条件调用，
    /// 为 null 即不会注册还原回调。
    /// </summary>
    /// <returns>是否真的解除了一个管理员（供回显报数）。</returns>
    internal static bool SuppressCounterattack(Computer comp)
    {
        if (comp?.admin is null)
        {
            return false;
        }

        comp.admin = null;
        return true;
    }

    /// <summary>
    /// 立即让目标跳板失效：语义与 ShellExe 过载跑完完全一致
    /// （<c>proxyOverloadTicks = 0f; proxyActive = false;</c>，ShellExe.cs:96-99），
    /// 但不等满 <c>startingOverloadTicks</c> 秒。
    ///
    /// 为什么这必须由 mod 实现：游戏没有「立即完成过载」的 API —— 终端
    /// <c>ComShell.exe -o</c>（OS.cs:2134 → ShellOverloaderExe → ShellExe.StartOverload）
    /// 启动的就是同一个逐帧扣减的 ShellExe，跑满一次要 <c>BASE_PROXY_TICKS = 30f</c> 秒
    /// （Computer.cs:27）。全网扫一遍就是几十段 30 秒纯等待，即用户报的
    /// 「还要干等 proxy 一阵子」。故直接收敛游戏原生的 public 字段：
    /// 不碰私有状态，不挂 Harmony 补丁，也不新增依赖。
    ///
    /// 跳过等待没有副作用：全游戏 <c>AchievementsManager.Unlock</c> 与跳板无关
    /// （唯一的追踪成就在 TraceTracker.cs:70 的 "trace_close"），过载也不推进追踪。
    /// 刻意不照抄 ShellExe.cs:105 的 <c>hostileActionTaken()</c> —— 那只会点燃反追踪。
    /// </summary>
    /// <returns>是否真的绕过了跳板（供回显报数）。</returns>
    internal static bool BypassProxy(Computer comp)
    {
        if (!ProxyActive(comp))
        {
            return false;
        }

        comp.proxyOverloadTicks = 0f;
        comp.proxyActive = false;
        return true;
    }

    /// <summary>
    /// 直接毙掉追踪 —— 不是「冻结」，是停。
    ///
    /// 走游戏自身的 <c>TraceTracker.stop()</c>（TraceTracker.cs:116-119）：
    /// <c>active = false; trackSpeedFactor = 1f;</c>。这是原生路径，
    /// <c>SecurityTraceExe.Killed()</c>（SecurityTraceExe.cs:26）在玩家关掉
    /// Security Tracer 时就是这么干的；<c>OS.thisComputerIPReset()</c>
    /// （OS.cs:1793-1796）换 IP 时也是直接置 <c>active = false</c>。
    ///
    /// 停是彻底的，不存在「暂停」形态 —— <c>TraceTracker.Update</c> 开头的
    /// <c>if (!active) return;</c>（TraceTracker.cs:53-56）让后续帧零开销，
    /// **不需要任何每帧维护**。
    ///
    /// 为什么不照抄 <c>TraceKillExe</c> 的「每帧把 <c>timeSinceFreezeRequest</c> 置 0」：
    /// 那是它作为 GUI 程序的职责 —— 玩家开着它时要看到 <c>SUPPRESSION ACTIVE</c>
    /// 的持续效果，故必须逐帧续期。mod 要的是「立即终止」这一动作，
    /// 没有那个 UI 需求，照抄只会白白常驻一个每帧补丁。
    ///
    /// 不丢成就：<c>trace_close</c> 的解锁写在 <c>TraceTracker.Update</c> 的
    /// <i>另一条</i>分支（connectedComp 为空或已换目标，TraceTracker.cs:64-71），
    /// 走 <c>stop()</c> 不经过它。真要在意那条分支的成就，得靠断开连接。
    /// </summary>
    /// <returns>是否真的终止了一条进行中的追踪（供回显报数）。</returns>
    internal static bool KillTrace(OS os)
    {
        if (os?.traceTracker is not { active: true })
        {
            return false;
        }

        os.traceTracker.stop();
        return true;
    }

    /// <summary>
    /// 静默断开当前连接。未连接时是空操作 —— 避免多余的 "Disconnected" 噪音。
    ///
    /// <c>silent</c> 是游戏自己的 public 开关（Computer.cs:57），
    /// Multiplayer.cs:125-127 就是「set true → 操作 → 还原」这个用法。
    /// 断开本身会往目标 /log 写 "&lt;ip&gt; Disconnected"（<c>Computer.disconnecting</c>，
    /// Computer.cs:722-727），而清痕必须排在断开**之前**（要连着目标的文件系统才作数），
    /// 不静音就等于清完立刻被写回一条。
    ///
    /// 多人对局不对它静音：同一个 <c>!silent</c> 门还守着
    /// <c>sendNetworkMessage("cDisconnect ...")</c>（Computer.cs:728-731），
    /// 静音会连断线同步一起吞掉，对面看到的还是「连着」。
    /// 日志保真让位于联机状态保真 —— 单机下这个分支恒真，多人才走 else。
    /// </summary>
    internal static void SilentDisconnect(OS os)
    {
        var leaving = os?.connectedComp;
        if (leaving == null)
        {
            return;
        }

        if (os.multiplayer)
        {
            Programs.disconnect(["dc"], os);
            return;
        }

        var wasSilent = leaving.silent;
        leaving.silent = true;
        try
        {
            Programs.disconnect(["dc"], os);
        }
        finally
        {
            leaving.silent = wasSilent;
        }
    }
}

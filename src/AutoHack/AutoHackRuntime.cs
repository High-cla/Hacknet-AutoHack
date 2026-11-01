namespace AutoHack;

using Hacknet;

/// <summary>
/// 一个 OS 一份的运行时状态：面板快照 + 绘制故障上报时刻。
///
/// <b>为什么要有这个类</b>：此前这些是 <see cref="HackOverlay"/> 里的静态字段，靠
/// 「换局时判断 <c>!ReferenceEquals(_os, os)</c>」来模拟实例边界 —— 规则散在注释里，
/// 且旧 OS 的状态只能被下一局的代码顺手覆盖。键化到 OS 之后，「哪一局的状态」由类型回答：
/// 用 <see cref="System.Runtime.CompilerServices.ConditionalWeakTable{TKey,TValue}"/> 存，
/// 旧局被回收时条目自动消失，没有需要显式清理的生命周期。
///
/// <b>运行本身不在这里</b>：它由 <see cref="AutoHackExecutable"/> 承载，本类只按需查。
/// </summary>
internal sealed class AutoHackRuntime
{
    internal AutoHackRuntime(OS os) => Os = os;

    /// <summary>这份状态属于哪个 OS 实例。</summary>
    internal OS Os { get; }

    /// <summary>面板状态；<c>null</c> = 本局还没打开过面板（面板因此保持「开局不显示」）。</summary>
    internal HackPanelState State { get; private set; }

    internal bool IsOpen => State is { Open: true };

    /// <summary>当前运行（由 exe 宿主持有）。</summary>
    internal HackRun Run => AutoHackExecutable.Find(Os);

    /// <summary>本局的帧时间统计（诊断计数开关打开时才会往终端报告）。</summary>
    internal PerfMeter Perf { get; } = new PerfMeter();

    /// <summary>上一次绘制异常的上报时刻（<c>OS.timer</c> 秒）。</summary>
    internal double LastDrawFaultAt { get; set; } = double.NegativeInfinity;

    /// <summary>首次打开时从 cfg 读设置；同一局内重复调用不改动已加载的状态。</summary>
    internal void EnsureState()
    {
        if (State != null)
        {
            return;
        }

        var fresh = new HackPanelState();
        PanelSettings.Load(fresh);
        State = fresh;
    }

    internal void Open()
    {
        EnsureState();
        State.Open = true;
    }

    internal void Close()
    {
        if (State != null)
        {
            State.Open = false;
        }
    }
}

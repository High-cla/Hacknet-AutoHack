namespace AutoHack;

using Hacknet;

/// <summary>
/// 「这一帧哪些 exe 还在 RAM 面板上」的快照。
///
/// <b>为什么单独成类</b>：它是<b>只读探测</b>——把 <c>os.exes</c> 拍成一个集合，供随后一批
/// O(1) 查询。它与队列调度、挂载生命周期没有共享状态，唯一耦合是「必须先重建再查询」
/// 这条时序约束，而这一点写在方法注释里比混在 900 行的演出类里更容易看见。
///
/// <b>为什么复用实例而不是每帧 new</b>：每帧 new 一个 <c>HashSet</c> 只是把省下的比较
/// 换成每帧一次堆分配；用静态实例 + <c>Clear()</c> 全程零分配。
///
/// <b>线程：只在游戏线程使用</b>（调用点都在 <c>OSUpdateEvent</c> / 运行收尾里）。
/// </summary>
internal static class NativeExePanelProbe
{
    /// <summary>本帧 <c>os.exes</c> 里那些实例。由 <see cref="Rebuild"/> 填充。</summary>
    private static readonly HashSet<ExeModule> OnPanel = new();

    /// <summary>
    /// 把 <c>os.exes</c> 拍进快照。紧挨着每个查询循环之前调用 ——
    /// 循环体内只写端口状态与改调用方自己的列表，都不动 <c>os.exes</c>，故快照整批有效。
    /// </summary>
    internal static void Rebuild(OS os)
    {
        OnPanel.Clear();

        if (os?.exes == null)
        {
            return;
        }

        for (var i = 0; i < os.exes.Count; i++)
        {
            OnPanel.Add(os.exes[i]);
        }
    }

    /// <summary>
    /// 该 exe 此刻是否还在快照里 —— 泵的剪枝与补开用的是同一条判据。
    /// <b>前置条件</b>：本批查询之前必须已 <see cref="Rebuild"/>。
    /// 刻意不收 <c>OS</c> 参数：读的是静态快照，传 OS 只会让人误以为查的是那个 OS。
    /// </summary>
    internal static bool StillOn(ExeModule exe)
        => exe != null && OnPanel.Contains(exe);
}

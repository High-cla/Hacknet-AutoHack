namespace AutoHack;

using Hacknet;
using Microsoft.Xna.Framework;
using Pathfinder.Executable;

/// <summary>
/// 一次运行的宿主壳：把「按帧推进」翻译成 Pathfinder 可执行体的 Update 生命周期。
///
/// <b>为什么是 exe 而不是补丁</b>：生命周期由游戏自己的 <c>OS.Update</c> 循环驱动
/// （<c>exes[i].Update(dt)</c>，OS.cs:862-864），随 OS 实例走、随 OS 注销消失；
/// 我们不再需要每帧在补丁里认领「该推进哪一个 run」。
///
/// <b>零成本挂载</b>：<c>ramCost = 0</c> 使 <c>os.ramAvaliable &gt;= 0</c> 恒成立，
/// RAM 面板不为它扣内存；<see cref="Draw"/> 空实现让它完全不可见。
/// 可被 <c>kill</c> 终结（<c>CanBeKilled = true</c>）：玩家要在中途叫停时，
/// 这就是原生出路；杀掉即连同 run 一起消失，面板随之显示空闲。
///
/// <b>壳不参与调度</b>：推进顺序、闸门、跳过判据全在 <see cref="AutoHack.Core.Session"/>，
/// 这里只把 <c>delta</c> 交给 <see cref="HackRun.Tick"/> —— 不依赖任何虚方法的调用次序。
/// </summary>
internal sealed class AutoHackExecutable : GameExecutable
{
    private readonly OS _os;

    private AutoHackExecutable(OS os, HackRun run)
    {
        _os = os;
        Run = run;

        // 零内存、不可见、可杀。三个开关都在基类的公开面上，不需要补丁。
        ramCost = 0;
        baseRamCost = 0;
        CanAddToSystem = true;
        CanBeKilled = true;
        IgnoreMemoryBehaviorPrint = true;
        IdentifierName = "autohack";
        Result = CompletionResult.Running;
    }

    /// <summary>本 exe 驱动的运行。</summary>
    internal HackRun Run { get; }

    /// <summary>挂一个运行到 OS 上。位置只影响 RAM 面板排版（高度为 0，不可见）。</summary>
    internal static AutoHackExecutable Create(OS os, HackRun run)
    {
        var exe = new AutoHackExecutable(os, run);
        var bounds = new Rectangle(
            os.ram.bounds.X,
            os.ram.bounds.Y + RamModule.contentStartOffset,
            RamModule.MODULE_WIDTH,
            (int)OS.EXE_MODULE_HEIGHT);

        os.AddGameExecutable(exe, bounds, ["autohack"]);
        return exe;
    }

    /// <summary>该 OS 上<b>尚未完成</b>的运行；已完成（正在淡出）或没有则为 null。</summary>
    internal static HackRun FindActive(OS os)
    {
        var run = Find(os);
        return run is { Finished: false } ? run : null;
    }

    /// <summary>该 OS 上挂着的那次运行（含已完成、正在淡出的）；没有则为 null。</summary>
    internal static HackRun Find(OS os)
    {
        if (os?.exes == null)
        {
            return null;
        }

        foreach (var exe in os.exes)
        {
            if (exe is AutoHackExecutable running)
            {
                return running.Run;
            }
        }

        return null;
    }

    /// <summary>每帧推进一步运行；跑完即置 Success，由基类淡出并移除。</summary>
    public override void OnUpdate(float delta)
    {
        if (!Run.Finished)
        {
            Run.Tick(_os, delta);
        }

        if (Run.Finished)
        {
            Result = CompletionResult.Success;
        }
    }

    /// <summary>
    /// 执行层抛异常时的唯一救生口：记一行、止血、吞掉异常（返回 true），
    /// 不让它穿过 <c>OS.Update</c> 打断整个游戏帧。
    ///
    /// 不在这里改 <c>Result</c>：下一帧 <see cref="OnUpdate"/> 见到 <c>Run.Finished</c> 会置
    /// <c>Success</c> 并正常淡出 —— 若改成 Failure，基类还会额外打一行
    /// <c>autohack: failed with ''</c> 的噪音。
    /// </summary>
    public override bool CatchException(Exception exception)
    {
        Run.Abort(_os, exception);
        return true;
    }

    /// <summary>不绘制任何东西 —— 零 RAM 的隐形宿主，面板另有自己的绘制层。</summary>
    public override void Draw(float t)
    {
    }
}

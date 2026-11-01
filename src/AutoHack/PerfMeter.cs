namespace AutoHack;

using System;
using System.Globalization;

/// <summary>
/// 绘制与推进的帧时间统计：给「先度量后优化」提供数据，否则一切性能结论只能靠离线模型
/// （<c>tools/bench.mts</c>）推断，见 v1.45.0 审计。
///
/// <b>零分配热路径</b>：每帧只做两次数组写入 + 一次计数，不走 LINQ、不装箱、不建串。
/// <b>只在报告时分配</b>：每 <see cref="ReportEvery"/> 帧复制一次环形缓冲并排序，频率约 5 秒一次。
///
/// <b>为什么统计的是 p50/p95/p99 而不是平均值</b>：绘制卡顿是长尾问题 —— 120 帧里 2 帧卡 8 ms
/// 会被平均值抹平（+0.13 ms），却正是玩家能感知的那种掉帧。
/// </summary>
internal sealed class PerfMeter
{
    /// <summary>环形缓冲容量：60 fps 下约 2 秒样本，足够稳定估计 p95。</summary>
    private const int Capacity = 128;

    /// <summary>多少帧报告一次（60 fps ≈ 5 秒）。太密会刷终端，太疏看不出趋势。</summary>
    internal const int ReportEvery = 300;

    private readonly double[] _draw = new double[Capacity];
    private readonly double[] _panel = new double[Capacity];
    private int _drawAt;
    private int _panelAt;
    private int _drawCount;
    private int _panelCount;
    private int _frames;

    /// <summary>记一帧整条绘制链（面板 + HUD）的耗时，单位毫秒。</summary>
    internal void AddDraw(double milliseconds)
    {
        _draw[_drawAt] = milliseconds;
        _drawAt = (_drawAt + 1) % Capacity;
        if (_drawCount < Capacity)
        {
            _drawCount++;
        }
    }

    /// <summary>记一帧面板自身的耗时（不含 HUD），单位毫秒。</summary>
    internal void AddPanel(double milliseconds)
    {
        _panel[_panelAt] = milliseconds;
        _panelAt = (_panelAt + 1) % Capacity;
        if (_panelCount < Capacity)
        {
            _panelCount++;
        }
    }

    /// <summary>推进帧计数；到达报告周期时返回真（并复位计数，调用方随后应取 <see cref="Report"/>）。</summary>
    internal bool Due()
    {
        if (++_frames < ReportEvery)
        {
            return false;
        }

        _frames = 0;
        return true;
    }

    /// <summary>
    /// 生成一行报告并复位样本窗口。调用前应先确认 <see cref="Due"/> —— 否则样本不足，
    /// 分位数没有意义。
    /// </summary>
    internal string Report()
    {
        var text = "[autohack] perf " + Describe("draw", _draw, _drawCount, _drawAt)
                   + " | " + Describe("panel", _panel, _panelCount, _panelAt);
        _drawCount = 0;
        _panelCount = 0;
        return text;
    }

    /// <summary>把环形缓冲拍成升序数组后取分位；样本为空时返回 <c>n/a</c>。</summary>
    private static string Describe(string name, double[] ring, int count, int next)
    {
        if (count == 0)
        {
            return name + " n/a";
        }

        var sorted = new double[count];
        for (var i = 0; i < count; i++)
        {
            // next 指向下一个写入位，故最新样本在它前面一个；环形展开从最旧的开始写。
            sorted[i] = ring[(next - count + i + Capacity * 2) % Capacity];
        }

        Array.Sort(sorted);
        return name
               + " p50=" + Ms(sorted, 0.50)
               + " p95=" + Ms(sorted, 0.95)
               + " p99=" + Ms(sorted, 0.99)
               + " max=" + Fmt(sorted[count - 1]);
    }

    private static string Ms(double[] sorted, double fraction)
    {
        var index = (int)Math.Round(fraction * (sorted.Length - 1), MidpointRounding.AwayFromZero);
        return Fmt(sorted[index]);
    }

    /// <summary>固定不变文化与小位数：日志要能跨机器逐字比对。</summary>
    private static string Fmt(double milliseconds)
        => milliseconds.ToString("0.00", CultureInfo.InvariantCulture) + "ms";
}
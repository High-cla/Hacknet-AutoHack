// ExeCatalog.cs —— 可播原生程序的目录：白名单（有表才算可播）与时长，以及代价档号。
//
// 由 NativeExes 拆出（v1.45.0 审计 P1-1）：这是一张纯数据表加一个纯函数，
// 与队列调度、线程模型、游戏 API 都无关，放在调度器里只会让「能不能播」与「怎么播」
// 混在一起读。
namespace AutoHack;

using System;
using System.Collections.Generic;

internal static class ExeCatalog
{
    /// <summary>白名单 + 时长表。见 <see cref="Life"/>。</summary>
    private static readonly Dictionary<string, NativeExes.Life> Lifetimes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TorrentStreamInjector.exe"] = new NativeExes.Life(4.8f, 18.5f),
        ["PacificPortcrusher.exe"] = new(6f, 8.2f),
        ["SSHcrack.exe"] = new(8f, 10f),
        ["SQL_MemCorrupt.exe"] = new(12.2f, 14.2f),
        ["SMTPoverflow.exe"] = new(12f, 14.5f),
        ["WebServerWorm.exe"] = new(14f, 17f),
        ["FTPBounce.exe"] = new(15f, 17f),
        ["KBT_PortTest.exe"] = new(22f, 26f),
        ["RTSPCrack.exe"] = new(6.3f, 32.5f),
    };

    /// <summary>内存分档宽度（mb）。ramCost 落在 190~400，按 100 分即 4 档。</summary>
    private const int RamTierMb = 100;

    /// <summary>时长分档宽度（秒）。实际存活 8.2~32.5 秒，按 10 分即 4 档。</summary>
    private const int SecondTierSec = 10;

    /// <summary>时长档数 —— 内存档的权重。取 4 是因为 <see cref="SecondTierSec"/>
    /// 把时长分成了 4 档，相乘即「内存档优先、时长档次之」的二维编号。</summary>
    private const int SecondTierCount = 4;

    /// <summary>
    /// <see cref="Tier"/> 的取值上界，排序键用它当进制（实测档号落在 4~19，取 32 留足余量）。
    /// 必须<b>严格大于</b>档号最大值，否则次数差 1 的两项可能被档号差吃掉，
    /// 「没出现过的优先」就不再成立。
    /// </summary>
    internal const int TierSpan = 32;

    /// <summary>查一个 exe 名是否在表内（= 有原生时长数据 = 可播），并给出它的时长。</summary>
    internal static bool TryGetLife(string exeName, out NativeExes.Life life)
        => Lifetimes.TryGetValue(exeName, out life);

    /// <summary>代价档号：内存档为主、时长档为辅（见 NativeExes.SortKey）。</summary>
    internal static int Tier(int ramCost, float totalSeconds)
        => ramCost / RamTierMb * SecondTierCount + (int)(totalSeconds / SecondTierSec);
}
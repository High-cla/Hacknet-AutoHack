namespace AutoHack;

using System;
using System.Runtime.CompilerServices;
using Hacknet;
using HarmonyLib;

/// <summary>
/// 给游戏原生的 <c>replace</c> 补一条「失败要说出来」的回显。
///
/// <b>为什么需要</b>：<c>Programs.replace2</c>（Programs.cs:795）用
/// <c>fileEntry.data.Replace(old, new)</c> 改文件，而 <c>string.Replace</c> **大小写敏感**；
/// 找不到目标串时是**静默空操作** —— 不报错、不回显、文件一字未动。玩家看到的是
/// 「命令敲了、也有回显，但什么都没变」，与仓库里反复出现的「按了没反应」同族。
///
/// 实测踩点（2026-09）：lelzSec 入门任务的判据是
/// <c>&lt;goal type="FileChange" target="nortronWebServer" file="index.html"
/// path="web" keyword="dicks"/&gt;</c>（lelzSec/IntroTestMission.xml:4），
/// 而该页原文写的是 <c>Nortron Security Services</c> —— 大写 <c>Security</c>。
/// 玩家敲的是 <c>replace index.html security dicks</c>，首字母小写 ⇒ 恒不命中，
/// 任务因此永不判完成；日志里同一条命令重复了 4 次（LogOutput.log:176/178/179）。
///
/// <b>判据取「调用前后 data 是否真的变了」</b>，不自己重算替换规则 —— 那样等于把
/// <c>replace2</c> 的匹配语义抄第二份，游戏一改就漂移。此处只读结果。
///
/// 只加回显，**不改替换语义**：大小写敏感是游戏原生行为，改成不敏感会波及其他任务。
/// </summary>
[HarmonyPatch]
internal static class ReplaceFeedback
{
    /// <summary>
    /// 命令走 <c>OS.execute</c> 的独立线程（OS.cs:1754-1767），同一条命令的
    /// Prefix/Postfix 在同一线程，但同一 OS 上可能并发多条 —— 弱表本身的单次操作是
    /// 线程安全的，<c>Remove</c> + <c>Add</c> 这一对不是，故自己加锁。
    /// 用弱表而非字典：玩家回主菜单换 OS 后不留长期引用（与 RefusedWhitelist 同一取舍）。
    /// </summary>
    private static readonly object Gate = new();

    private static readonly ConditionalWeakTable<OS, string> Before = new();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Programs), nameof(Programs.replace2))]
    private static void OnReplacePrefix(string[] args, OS os)
    {
        if (os == null)
        {
            return;
        }

        var data = TargetFile(args, os)?.data;
        if (data == null)
        {
            return;
        }

        lock (Gate)
        {
            Before.Remove(os);
            Before.Add(os, data);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Programs), nameof(Programs.replace2))]
    private static void OnReplacePostfix(string[] args, OS os)
    {
        if (os == null)
        {
            return;
        }

        string before;
        lock (Gate)
        {
            if (!Before.TryGetValue(os, out before))
            {
                return;
            }
        }

        // 文件没定位到（原生自己已经报过「not found」），或内容确实变了（替换成功，
        // 原生照常回显）—— 两种都不该由我们再插一句。
        var file = TargetFile(args, os);
        if (file == null || file.data != before)
        {
            return;
        }

        // 走到这里：文件在、命令也执行了，但内容一字未动。
        var needle = Arg(args, 2);
        if (string.IsNullOrEmpty(needle))
        {
            return;
        }

        var actual = CaseInsensitiveHit(before, needle);
        if (actual == null)
        {
            os.write("[autohack] replace: no match for \"" + needle + "\" in " + Arg(args, 1) + ".");
        }
        else if (!string.Equals(actual, needle, StringComparison.Ordinal))
        {
            // 命中的是这一条：串确实在，只是大小写不同 —— 游戏按 Ordinal 比对，故不替换。
            os.write("[autohack] replace: the search is case-sensitive - \"" + needle
                     + "\" did not match, but \"" + actual + "\" is there.");
        }
        else
        {
            os.write("[autohack] replace: " + Arg(args, 1) + " unchanged (old and new text are the same).");
        }
    }

    /// <summary>不区分大小写地在正文里找一次，返回**原文里的真实拼写**；没有则 null。</summary>
    private static string CaseInsensitiveHit(string haystack, string needle)
    {
        var at = haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        return at < 0 ? null : haystack.Substring(at, needle.Length);
    }

    /// <summary>
    /// 复刻 <c>replace2</c> 的文件定位：目标文件名取 <c>args[1]</c>，在当前目录里查。
    /// 只用于读；找不到返回 null，调用方随即放弃插话。
    /// </summary>
    private static FileEntry TargetFile(string[] args, OS os)
    {
        var name = Arg(args, 1);
        if (name == null)
        {
            return null;
        }

        var folder = Programs.getCurrentFolder(os);
        return folder?.searchForFile(name);
    }

    /// <summary>取第 <paramref name="index"/> 个参数，越界返回 null。</summary>
    private static string Arg(string[] args, int index)
        => args != null && index >= 0 && index < args.Length ? args[index] : null;
}

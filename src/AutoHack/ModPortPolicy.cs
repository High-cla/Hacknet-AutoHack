namespace AutoHack;

using System;
using System.Collections.Generic;
using Hacknet;
using Pathfinder.Port;

/// <summary>
/// 「模组端口」白名单：允许自动入侵去开的、由其它插件注册的自定义端口协议。
///
/// <b>为什么需要手动表。</b><see cref="HackEngine.CrackablePorts"/> 的判据是
/// <c>PortExploits.cracks.ContainsKey(codePort)</c>，而 <c>cracks</c> 只含游戏原生的
/// 36 个端口（<c>PortExploits.cs:51-214</c>）。workshop 模组注册的 16 个端口
/// （LunarOS 3 / SRPortToolkit 8 / ZeroDayToolKit 5）一个都不在其中，且<b>没有任何模组
/// 往 <c>cracks</c> 里写过</b>（全仓只有 ZeroDayToolKit.decompiled.cs:945/949 两处只读引用）
/// —— 故模组端口在自动入侵里恒不可见。
///
/// <b>为什么不用反射枚举。</b><c>PortManager</c> 的公开面只有「按名字查」
/// （<c>IsPortRegistered</c> / <c>GetPortRecordFromProtocol</c> / <c>GetPortRecordFromNumber</c>，
/// PortManager.cs:47-68），注册表是私有静态 <c>AssemblyAssociatedList&lt;PortRecord&gt; CustomPorts</c>
/// （:14）。反射能读（<see cref="ModTools.ScanPorts"/> 就是这么做的），但那是<b>诊断</b>用途：
/// 端口清单不能自动变成「允许开」—— 这些端口是各模组的剧情拼图，提前开等于替玩家跳过解谜。
///
/// <b>手动表只需要回答「允许开哪些协议」</b>，不需要回答「哪台机器有哪个端口」——
/// 后者由机器的端口表直接给出（见 <see cref="HackEngine.Ports"/>）。
///
/// <b>默认空 = 完全 opt-in。</b>命令行 <c>modports=mqtt,ntp</c> 逐协议累加；不写则一个都不开，
/// 行为与本特性不存在时逐位相同。
///
/// <b>两个「允许」全部」的入口</b>：命令行 <c>modports=*</c>，或面板上的
/// <c>auto mod ports</c> 复选框（它写出的就是 <c>*</c>）。两者等价 —— 面板只是把它
/// 变成了一个勾选框，省得玩家去终端里敲。**这一条不是「自动填写」**：白名单内容仍是
/// 常量 <c>*</c>，由策略在运行时对所有已注册协议求值；它不枚举、不写死任何具体协议名，
/// 故不会因为「某台机器的端口表里恰好有什么」而漂移。
///
/// <b>类名为什么是 Policy 而不是 Ports。</b><see cref="HackOptions"/> 上那个属性就叫
/// <c>ModPorts</c>，同名静态类会被实例成员遮蔽 —— 在 <c>HackOptions</c> 的方法里写
/// <c>ModPorts.Matches(...)</c> 会编译成「对实例属性的成员访问」并报 CS0120。改名一次，
/// 换掉以后每写一行都要加限定的坑。
///
/// <b>为什么不做成静态累积表。</b>静态状态会跨轮残留（上一轮 <c>modports=mqtt</c> 漏到下一轮），
/// 而「这轮开哪些模组端口」是纯粹的运行参数。故本类只提供<b>纯函数</b>：
/// 解析 token、判定命中、挑出查不到的名字；集合本身由 <see cref="HackOptions.ModPorts"/> 持有。
///
/// <b>已知的取舍：协议名匹配不区分大小写，但「是否注册」的检查区分。</b>
/// 各模组注册的名字大小写不一（<c>Redis</c> / <c>IMP</c> / <c>DNS</c> / <c>mqtt</c> / <c>ntp</c>），
/// 玩家不该被迫记住。故 <see cref="Allows"/> 走 <see cref="StringComparer.OrdinalIgnoreCase"/>，
/// 而 <see cref="Unknown"/> 只能用 <c>PortManager.IsPortRegistered</c>（内部按 Ordinal 比）。
/// 结果是「写了 <c>redis</c> 而注册名是 <c>Redis</c>」时<b>功能正常但会多报一行未注册提示</b>——
/// 报错措辞已注明大小写，比静默好。
/// </summary>
internal static class ModPortPolicy
{
    /// <summary>命令行前缀，与 <c>script=</c> 同为「带载荷的 token」形态。</summary>
    internal const string Prefix = "modports=";

    /// <summary>
    /// 通配符：<c>modports=*</c> = 允许**全部**已注册的模组端口，不必逐个点名。
    /// 面板上那个复选框用的就是它（见 <see cref="HackPanelState.AutoModPorts"/>）。
    /// </summary>
    internal const string Wildcard = "*";

    /// <summary>载荷分隔符：逗号为主，空格与分号一并收下（玩家手写习惯不一）。</summary>
    private static readonly char[] Separators = [',', ' ', ';', '\t'];

    /// <summary>token 是否是 <c>modports=</c> 形态（调用方传小写化后的 token）。</summary>
    internal static bool Matches(string lowerToken)
        => lowerToken != null && lowerToken.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// 解析 <c>modports=a,b</c> 的载荷。空载荷返回空表（不报错 —— <c>modports=</c>
    /// 单独出现等于「这轮不开模组端口」，与不写同义）。
    /// </summary>
    internal static IReadOnlyList<string> Parse(string token)
    {
        var payload = token.Substring(Prefix.Length);
        var names = new List<string>();
        foreach (var raw in payload.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var name = raw.Trim();
            if (name.Length > 0)
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>
    /// 该协议是否在白名单里。<paramref name="allowed"/> 为 <c>null</c> 或空 =
    /// 一个都不允许（缺省行为）。
    /// </summary>
    internal static bool Allows(IReadOnlyCollection<string> allowed, string protocol)
    {
        if (allowed == null || allowed.Count == 0 || string.IsNullOrEmpty(protocol))
        {
            return false;
        }

        foreach (var name in allowed)
        {
            // 通配符先判：`modports=*` 与 `modports=*,mqtt` 同义，
            // 不要求玩家在混写时还要遵守某种次序。
            if (name == Wildcard || string.Equals(name, protocol, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 这组白名单是否是通配（等价于「全部已注册的模组端口都允许」）。
    /// 供 <see cref="Unknown"/> 跳过逐名检查 —— <c>*</c> 本身当然不是注册名，
    /// 不特判就会误报一行「没有任何插件注册过名为 '*' 的端口」。
    /// </summary>
    internal static bool IsWildcard(IReadOnlyCollection<string> allowed)
    {
        if (allowed == null)
        {
            return false;
        }

        foreach (var name in allowed)
        {
            if (name == Wildcard)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 白名单里<b>没有任何插件注册过</b>的协议名，供调用方在开跑前提示一次。
    ///
    /// 为什么必须报：模组未加载（或名字拼错）时端口表里根本没有那个协议，
    /// 整轮会安静地少开几个端口，玩家只会以为「这个开关没用」。
    ///
    /// 判据用 <c>PortManager.IsPortRegistered</c>（公开 API，同时覆盖模组注册表与原生
    /// <c>OGPorts</c>）。它是 Ordinal 比较，故大小写不符会误报 —— 见类注释的取舍。
    /// </summary>
    internal static IReadOnlyList<string> Unknown(IReadOnlyCollection<string> allowed)
    {
        var missing = new List<string>();
        if (allowed == null || IsWildcard(allowed))
        {
            return missing;
        }

        foreach (var name in allowed)
        {
            bool known;
            try
            {
                known = PortManager.IsPortRegistered(name);
            }
            catch (Exception ex) when (ex is TypeLoadException or MemberAccessException
                                           or InvalidOperationException or ArgumentException)
            {
                // 查不动就别误报：让功能继续，玩家不该因为诊断失败而看到一个假警报。
                continue;
            }

            if (!known)
            {
                missing.Add(name);
            }
        }

        return missing;
    }
}

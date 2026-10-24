namespace AutoHack;

using AutoHack.Core;
using Pathfinder.Port;

/// <summary>
/// 模组端口白名单的<b>可观测性</b>部分：哪些协议名其实没有任何插件注册过。
///
/// 与 <see cref="ModPortPolicy"/> 分开是因为判据来源不同：那里全是字符串策略（离线可测），
/// 这里要查 Pathfinder 的 <c>PortManager</c> 注册表，属适配层。
///
/// <b>为什么必须报</b>：模组未加载（或名字拼错）时端口表里根本没有那个协议，整轮会安静地
/// 少开几个端口，玩家只会以为「这个开关没用」。判据用 <c>PortManager.IsPortRegistered</c>
/// （公开 API，同时覆盖模组注册表与原生 <c>OGPorts</c>），它是 Ordinal 比较，
/// 故大小写不符会误报 —— 见 <see cref="ModPortPolicy"/> 类注释的取舍。
/// </summary>
internal static class ModPortProbe
{
    /// <summary>白名单里没有任何插件注册过的协议名；空集合或通配表示不检查。</summary>
    internal static IReadOnlyList<string> Unknown(IReadOnlyCollection<string> allowed)
    {
        var missing = new List<string>();
        if (allowed == null || ModPortPolicy.IsWildcard(allowed))
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

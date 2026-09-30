namespace AutoHack;

using System.Collections;
using System.Reflection;
using Hacknet;
using Pathfinder.Executable;
using Pathfinder.Port;

/// <summary>
/// 扫描其它插件（workshop mod）注册的自定义 exe 与自定义端口，并把它们的 exe 补进玩家 /bin。
///
/// <b>为什么必须现扫、不能缓存。</b>workshop 的 DLL 不由 BepInEx 在启动时装 ——
/// BepInEx 只加载 6 个插件（AutoUpdater / PathfinderAPI / AutoHack / Hacknet Save Fix /
/// KernelFix / HacknetHotReplace），而 LunarOSPathfinder.dll / ZeroDayToolKit.dll /
/// SRPortToolkit.dll 等是 Pathfinder 在<b>扩展被激活时</b>才装进来的。它们的
/// <c>ExecutableManager.RegisterExecutable</c> / <c>PortManager.RegisterPort</c> 都写在各自
/// 的 Load() 里，故一定晚于本插件的 Load() —— 在 Load() 里扫只能扫到空表。
/// 同理不做跨调用缓存：切扩展时 <c>ExecutableManager.OnPluginUnload</c> 会按程序集摘掉旧条目
/// （ExecutableManager.cs:73-76），缓存会发霉。表只有二十来项，每次现扫的代价可忽略。
///
/// <b>exe 名单走公开 API，不反射。</b><c>ExecutableManager.AllCustomExes</c> 就是权威注册表，
/// 每项带 <c>XmlId</c> / <c>ExeData</c> / <c>ExeType</c>，正是补 /bin 所需的三样。
/// <b>端口名单只能反射</b>：<c>PortManager</c> 的公开面只有「按名字查」（IsPortRegistered /
/// GetPortRecordFromProtocol…），没有任何枚举入口，注册表是私有的
/// <c>private static readonly AssemblyAssociatedList&lt;PortRecord&gt; CustomPorts</c>
/// （PortManager.cs:17）。故端口那一份是<b>诊断信息</b>，拿不到就如实报空、绝不抛。
///
/// <b>为什么补 /bin 是安全的。</b>自定义 exe 的执行链与原生完全同构：玩家敲名字 →
/// <c>ProgramRunner.GetFileIndexOfExeProgram</c> 按文件名匹配 /bin（ProgramRunner.cs:664）→
/// 取 <c>file.data</c> → IL hook 抛 <c>ExecutableExecuteEvent</c>
/// （ExecutableExecuteEvent.cs:118-133）→ <c>ExecutableManager.OnExeExecute</c> 拿
/// <c>file.data</c> 去比对 <c>CustomExeInfo.ExeData</c>（ExecutableManager.cs:47-71）→ 命中即
/// <c>Activator.CreateInstance</c> 并挂载。故<b>把 ExeData 原样写进 /bin 就够了</b>，
/// 不需要任何额外接线。这正是 mod 自己的节点 XML 在做的事
/// （<c>&lt;file path="bin" name="RedisBreaker.exe"&gt;#REDIS_EXE#&lt;/file&gt;</c>，
/// 占位符由 <c>TextReplaceEvent</c> 换成 ExeData）。
///
/// <b>不联动自动入侵。</b>「端口 → exe → 参数」三元组在这些 mod 里不可通用推断：
/// 实测 21 个自定义 exe 中 11 个有 <c>Args.Length &lt; 2</c> 就立即退出的硬门禁，
/// 参数语义各异（显示端口号 / <c>-s</c> <c>-f</c> 子命令 / 文件路径 / 协议名），
/// 且多个 exe 开的是<b>别人</b>的端口（LunarEclipse 开 moonshine 的 3653、
/// EOSRootKitExe 抢原生 3659、NetSpoofExe 抢原生 211），
/// 个别甚至给 <c>val2.ip</c>（第三方机器）开端口。强行自动化会开错端口、刷错误、卡住动画。
/// 故本工具只<b>提供文件</b>，是否使用由玩家自己决定。
/// </summary>
internal static class ModTools
{
    /// <summary>
    /// 一个自定义端口。<paramref name="CodePort"/> 是 <c>PortRecord.OriginalPortNumber</c>
    /// ——游戏内部的协议身份，与玩家看到的显示端口号不同（显示号可被 unbreakable 随机化）。
    /// </summary>
    internal readonly record struct ModPort(string Protocol, int CodePort, string DisplayName);

    /// <summary>
    /// 报告扫描结果并补全 /bin。两个入口（命令行 <c>autohack mods</c> 与面板按钮）都走这里。
    /// </summary>
    internal static void Run(OS os)
    {
        var exes = ScanExes(out var exeError);
        var ports = ScanPorts(out var portError);

        os.write("[autohack] mods: " + exes.Count + " exe(s), " + ports.Count + " port(s) registered by other plugins.");

        if (exeError != null)
        {
            os.write("[autohack] mods: exe scan failed - " + exeError);
        }

        if (portError != null)
        {
            os.write("[autohack] mods: port scan failed - " + portError);
        }

        foreach (var exe in exes)
        {
            os.write("[autohack] mods: exe " + exe.FileName + " (" + exe.ClassName + " from " + exe.Owner + ")");
        }

        if (ports.Count > 0)
        {
            // 端口是诊断信息（它们不在 PortExploits.cracks 里，AutoHack 不会去破解它们），
            // 故压成一行 —— 逐行打印会把终端刷满，而这里没有可操作的下一步。
            var names = new string[ports.Count];
            for (var i = 0; i < ports.Count; i++)
            {
                names[i] = ports[i].Protocol + "=" + ports[i].CodePort;
            }

            os.write("[autohack] mods: ports " + string.Join(", ", names)
                + "  (pass 'modports=<protocol,...>' to autohack run to crack them).");
        }

        if (exes.Count == 0)
        {
            os.write("[autohack] mods: nothing to add - no other plugin has registered an exe yet.");
            return;
        }

        var (added, skipped) = ExeTools.FillCustom(os, exes);
        os.write("[autohack] mods: added " + added + ", skipped " + skipped + " into /bin.");
    }

    /// <summary>
    /// 枚举 <c>ExecutableManager.AllCustomExes</c>。同名（按 <see cref="FileNameFor"/> 归一后）
    /// 只留第一个 —— /bin 里同名文件会让 <c>GetFileIndexOfExeProgram</c> 只认第一个，
    /// 写两份只是噪音。
    /// </summary>
    /// <param name="error">失败原因；成功为 <c>null</c>。不抛异常：mod 缺失或注册表被改都是常态，
    /// 让工具静默降级成空表，比把异常抛进游戏线程好。</param>
    private static List<ModExe> ScanExes(out string error)
    {
        error = null;
        var found = new List<ModExe>();
        try
        {
            var all = ExecutableManager.AllCustomExes;
            if (all == null || all.Count == 0)
            {
                return found;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var info in all)
            {
                var type = info.ExeType;
                if (type == null || string.IsNullOrEmpty(info.ExeData))
                {
                    continue;
                }

                var name = FileNameFor(type);
                if (!seen.Add(name))
                {
                    continue;
                }

                found.Add(new ModExe(name, type.Name, info.XmlId, info.ExeData, OwnerOf(type)));
            }
        }
        catch (Exception ex) when (ex is TypeLoadException or MemberAccessException
                                       or InvalidOperationException or ArgumentException
                                       or TargetInvocationException or NullReferenceException)
        {
            error = ex.GetType().Name + " - " + ex.Message;
        }

        return found;
    }

    /// <summary>
    /// 反射读 <c>PortManager.CustomPorts</c>（私有静态）的 <c>AllItems</c>。
    /// Pathfinder 若改了字段名，这里拿到 null，工具报 0 端口并继续 —— 不影响补 exe。
    /// </summary>
    private static List<ModPort> ScanPorts(out string error)
    {
        error = null;
        var found = new List<ModPort>();
        try
        {
            var field = typeof(PortManager).GetField("CustomPorts", BindingFlags.NonPublic | BindingFlags.Static);
            var registry = field?.GetValue(null);
            if (registry == null)
            {
                return found;
            }

            var allItems = registry.GetType()
                .GetProperty("AllItems", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(registry);

            if (allItems is not IEnumerable items)
            {
                return found;
            }

            foreach (var item in items)
            {
                if (item is PortRecord record)
                {
                    found.Add(new ModPort(record.Protocol, record.OriginalPortNumber, record.DefaultDisplayName));
                }
            }
        }
        catch (Exception ex) when (ex is TypeLoadException or MemberAccessException
                                       or InvalidOperationException or ArgumentException
                                       or TargetInvocationException or NullReferenceException)
        {
            error = ex.GetType().Name + " - " + ex.Message;
        }

        return found;
    }

    /// <summary>
    /// 类名 → /bin 里的文件名。
    ///
    /// 去掉结尾的 <c>Exe</c>/<c>EXE</c> 再补 <c>.exe</c>，是为了对上各 mod 自己节点 XML 里
    /// 写定的文件名 —— 实测 4/4 吻合：<c>RedisBreakerExe</c> → <c>RedisBreaker.exe</c>、
    /// <c>AutoCrackFirewallExe</c> → <c>AutoCrackFirewall.exe</c>、
    /// <c>SSHSwiftEXE</c> → <c>SSHSwift.exe</c>、<c>GitTunnelEXE</c> → <c>GitTunnel.exe</c>
    /// （出处在 3562955558/Nodes/Base/fh.xml:38/41/50/51）。
    /// 这是<b>启发式</b>，不是契约：没有公开 API 能拿到 mod 声明的文件名，
    /// 而 <c>XmlId</c>（<c>#SSH_SWIFT#</c>）与文件名也不同源。
    /// 猜错也不致命 —— 玩家敲类名同样能执行，<c>GetFileIndexOfExeProgram</c> 只比对文件名主干。
    /// </summary>
    private static string FileNameFor(Type type)
    {
        var name = type.Name ?? string.Empty;
        if (name.Length > 3 && name.EndsWith("Exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(0, name.Length - 3);
        }

        return name + ".exe";
    }

    /// <summary>注册它的程序集短名；拿不到就返回 <c>?</c>（只用于回显，不值得为它失败）。</summary>
    private static string OwnerOf(Type type)
    {
        try
        {
            return type.Assembly?.GetName()?.Name ?? "?";
        }
        catch (Exception ex) when (ex is MemberAccessException or InvalidOperationException or TypeLoadException)
        {
            return "?";
        }
    }
}

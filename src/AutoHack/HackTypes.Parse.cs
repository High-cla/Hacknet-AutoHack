// HackTypes.Parse.cs —— 命令行解析：已删 token 拦截、别名查表与 token 归约。
//
// HackOptions 的分部实现；类型声明、字段与其余职责见 HackTypes.cs。
namespace AutoHack;

internal sealed partial record HackOptions
{

    /// <summary>
    /// 脚本模式：用一份动作表取代内置次序。<c>script=&lt;文件名&gt;</c>，
    /// 文件按游戏的加载前缀解析（扩展目录或 Content/），详见 <see cref="HackScript"/>。
    /// </summary>
    private const string ScriptPrefix = "script=";

    /// <summary>
    /// v1.34.0 随「移除全部步进间隔」删掉的命令行 token。
    ///
    /// <b>为什么必须点名拦下。</b>它们已不是别名，也不再被 <see cref="Parse"/> 特判，
    /// 于是会静默掉进末尾的「显式目标」分支（<c>ids.Add(token)</c>）——
    /// 玩家敲 <c>autohack run instant</c> 得到的是「No eligible targets」，
    /// 只会以为目标名写错，而真实原因是那个开关没了。报出来比默默收下一个
    /// 永远匹配不到的目标好。
    ///
    /// 只收「玩家可能真敲过」的那些；别名表里的其余词（<c>here</c> / <c>dc</c> …）
    /// 仍在 <see cref="AliasMap"/> 里，走不到这里。
    /// </summary>
    private static readonly HashSet<string> RetiredTokens = new(StringComparer.Ordinal)
    {
        "slow", "normal", "fast", "quick", "instant", "turbo", "sameframe",
    };

    /// <summary>
    /// 找出参数里第一个已被删除的 token；没有则返回 <c>null</c>。
    /// 判据与 <see cref="Parse"/> 同一套（小写化 + 前缀），避免两处口径漂移。
    /// </summary>
    internal static string RetiredTokenIn(IReadOnlyList<string> args)
    {
        foreach (var raw in args ?? Array.Empty<string>())
        {
            var token = raw?.Trim();
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            var lower = token.ToLowerInvariant();
            if (RetiredTokens.Contains(lower) || lower.StartsWith("delay=", StringComparison.Ordinal))
            {
                return token;
            }
        }

        return null;
    }

    internal static HackOptions Parse(IReadOnlyList<string> args)
    {
        var ids = new List<string>();
        var scope = HackScope.Network;
        // 缺省**开**（v1.33.2 起，此前为关）。
        //
        // 口径变了：不再是「清空对方的 /log」（那会改写目标机的操作史），
        // 而是只删 /log 里含玩家 IP 的条目 —— 抹的是自己的痕迹，不是对方的历史。
        // 口径一变，缺省跟着翻：旧实现默认关是对的（清空整目录属越权），
        // 新实现默认关则是错的 —— 留下的 FileCopied/FileDeleted 条目会让
        // 带 tracker="true" 的机器在断开时自动排一个脱机追踪
        // （TrackerCompleteSequence.cs:30-47），那是「留痕 = 自杀」。
        var wipeTraces = true;
        var uploadMarker = false;
        var connectFirst = true;

        // 缺省关（v1.16.0 起）：保持连接是更中性的默认 —— 断开会终止会话、
        // 清空 navigationPath。它只管断开；收尾清追踪已改为恒定动作，不再需要显式开启。
        var disconnect = false;
        var skipOwned = true;
        var allNodes = false;
        // 缺省关（v1.15.0 起）：adminPass 是公开字段，开启后能登入全部机器，
        // 端口破解 / 防火墙 / 跳板三套机制实际都不会再被走到，等于架空玩法。
        // 要便利性再显式 creds。
        var useCredentials = false;

        // 缺省开：把原生破解程序挂进 RAM 面板当演出。
        //
        // v1.33.3 起端口改由这些程序自己在 Completed() 里开（见 NativeExes.Show 的文档
        // 注释）—— 看到动画跑完就等于那个端口真的开了。没有对应动画的端口
        // （实测 73/642）由调用方立即开，故关掉演出不影响战果，只是没有动画可看。
        // 缺省开是为了让脚本跑起来有可看的演出；要安静跑用 noshow。
        var showExes = true;

        // 缺省**关**（v1.32.6 起，此前为开）。换 IP 是游戏原生的「保命」动作
        // （ISP 服务器的 Assign New IP），但它会**打断任何要求 IP 不变的任务链**：
        // lelzSec 那条明写「Your IP's been whitelisted (so dont go changing it for now)」
        // （lelzSec/MessageBoardIntro.xml），白名单记的是当时的 IP，换掉即失效。
        // 缺省开时这类任务会莫名其妙进不去，而玩家很难把两件事联系起来。
        // 要换用 newip 显式开启，或直接用面板的 NEW IP 按钮换一次。
        var resetIP = false;

        // 模组端口白名单：缺省空（完全 opt-in）。命令行可给多次 modports=，逐协议累加。
        // 不做成静态累积表 —— 那会让上一轮的协议漏到下一轮，见 ModPortPolicy 的类注释。
        var modPorts = new List<string>();

        string script = null;

        foreach (var raw in args ?? Array.Empty<string>())
        {
            var token = raw?.Trim();
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            var lower = token.ToLowerInvariant();

            // 一次查找定动作；别名全部不中才轮到 script= / ids（判定顺序与旧实现一致）。
            if (AliasMap.TryGetValue(lower, out var opt))
            {
                switch (opt)
                {
                    case Opt.Connected: scope = HackScope.Connected; break;
                    case Opt.Network: scope = HackScope.Network; break;
                    case Opt.KeepTrace: wipeTraces = false; break;
                    case Opt.WipeTrace: wipeTraces = true; break;
                    case Opt.Marker: uploadMarker = true; break;
                    case Opt.NoMarker: uploadMarker = false; break;
                    case Opt.Redo: skipOwned = false; break;
                    case Opt.AllNodes: allNodes = true; break;
                    case Opt.Direct: connectFirst = false; break;
                    case Opt.Stay: disconnect = false; break;
                    case Opt.Leave: disconnect = true; break;
                    case Opt.Credentials: useCredentials = true; break;
                    case Opt.NoCredentials: useCredentials = false; break;
                    case Opt.ShowExes: showExes = true; break;
                    case Opt.NoShowExes: showExes = false; break;
                    case Opt.ResetIP: resetIP = true; break;
                    case Opt.NoResetIP: resetIP = false; break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(opt), opt, "别名动作未在 Parse 中分派");
                }

                continue;
            }

            if (ModPortPolicy.Matches(lower))
            {
                modPorts.AddRange(ModPortPolicy.Parse(token));
                continue;
            }

            if (lower.StartsWith(ScriptPrefix, StringComparison.Ordinal))
            {
                var name = token.Substring(ScriptPrefix.Length).Trim();
                if (name.Length > 0)
                {
                    script = name;
                }

                continue;
            }

            ids.Add(token);
        }

        if (ids.Count > 0)
        {
            scope = HackScope.Explicit;
        }

        return new HackOptions(
            scope, ids, wipeTraces, uploadMarker, connectFirst, disconnect, skipOwned,
            allNodes, useCredentials, showExes, resetIP, modPorts, script);
    }
}

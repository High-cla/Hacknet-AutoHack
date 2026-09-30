// HackRun.Steps.cs —— 单步实现：把 HackStep 落到游戏 API 上，并回显对应终端指令。
//
// HackRun 的分部实现；类型声明、字段与其余职责见 HackRun.cs。
namespace AutoHack;

using Hacknet;
using Pathfinder.Port;

internal sealed partial class HackRun
{

    /// <summary>单帧最多执行多少步，防止 Instant 档在极端规模下一帧卡死。</summary>
    private const int MaxStepsPerFrame = 512;

    /// <summary>
    /// 该步骤是否依赖「已连接」这一状态。
    ///
    /// 只有两个：Connect 自己与 Disconnect（未连接时是空操作）。其余步骤操作的是
    /// <c>target</c> 对象本身，不经连接 —— 端口表的 <c>Cracked</c> 状态、
    /// <c>giveAdmin</c> 写入的 <c>adminIP</c>、以及清痕走的
    /// <c>Computer.deleteFile(ipFrom, 名, folderPath)</c> 都直接落在目标机上。
    ///
    /// 清痕曾在这个名单里 —— 那时它回显并依赖 <c>rm log/*</c>，而 <c>rm</c> 的目标机
    /// 取自 <c>os.connectedComp</c>（Programs.cs:956）。改为按 IP 逐条点名删之后
    /// （见 <see cref="HackEngine.WipeTraces"/>），它连回显都不需要了，自然也不再依赖连接。
    /// </summary>
    private static bool DependsOnConnection(HackStepKind kind) => kind switch
    {
        HackStepKind.Connect => true,
        HackStepKind.Disconnect => true,
        _ => false,
    };

    /// <summary>
    /// 连接目标：回显 connect、执行连接，并核对是否被目标的白名单拒绝；
    /// 被拒时登记 <see cref="_refused"/> 并报一行状态，后续步骤照常执行。
    /// </summary>
    private void ApplyConnect(OS os, HackStep step)
    {
        var target = step.Target;
        Phase = "CONNECTING TO " + Upper(target.name);
        Echo(os, step.Command);
        Programs.connect(["connect", target.ip], os);

        // 连接可以被目标主动拒绝。带 WhitelistConnectionDaemon 的机器在
        // Computer.connect 里检查白名单，不通过就调 DisconnectTarget() 并
        // return false（Computer.cs:383-388）；Programs.connect 只写一行
        // "External Computer Refused Connection" 再把 os.connectedComp 置 null
        // （Programs.cs:313-322）。两者都不返回值，故只能核对结果。
        //
        // 不核对就是假战果：OpenPort / Escalate / CleanLogs 全部直接对 target
        // 对象操作，压根不经过连接，于是玩家看到 Refused 却收到「入侵成功」。
        if (!ReferenceEquals(os.connectedComp, target))
        {
            _refused.Add(target);
            return;
        }
    }

    /// <summary>解除目标的延迟反扑，仅在实际解除时由 Neutralize 报一行。</summary>
    private void ApplyNeutralize(OS os, Computer target)
    {
        Phase = "DISABLING COUNTERATTACK ON " + Upper(target.name);
        Neutralize(os, target);
    }

    /// <summary>解除目标的跳板，仅在实际解除时由 BypassProxy 报一行。</summary>
    private void ApplyBypassProxy(OS os, Computer target)
    {
        Phase = "BYPASSING PROXY ON " + Upper(target.name);
        BypassProxy(os, target);
    }

    /// <summary>
    /// 用已知凭据登录目标：成功即回显 login、提权，并登记 <see cref="_loggedIn"/>
    /// （后续破端口类步骤整体跳过）；失败则什么都不做，照常走破端口那条路。
    /// </summary>
    private void ApplyLogin(OS os, Computer target)
    {
        Phase = "LOGGING IN";

        // login 成功即 giveAdmin（Computer.cs:851-855），与 porthack 终点等价，
        // 但不破端口、不触发追踪。凭据来自目标自身的公开字段与 known 标记。
        if (HackEngine.TryLogin(target, out var credential))
        {
            Phase = "LOGGED IN";

            // 只回显 login 本身：原版 login 是交互式的（先问用户名再问密码，
            // Programs.cs:404-448），没有 "login <user> <pass>" 这种写法，
            // 拼上参数会是条游戏里不存在的命令。
            Echo(os, "login");

            // 本目标已提权，后续破端口/解防火墙/porthack 都不必跑。
            // 只登记「本次运行中确实靠 login 拿下的」机器 —— 不用
            // 「adminIP 已是我们」这个更宽的判据，否则 redo 模式
            // （重打已控节点）会连端口都不破，改变其语义。
            _loggedIn.Add(target);
        }
    }

    /// <summary>侦察目标：回显 probe 并输出完整端口报告，两者都无条件。</summary>
    private void ApplyProbe(OS os, HackStep step)
    {
        var target = step.Target;
        Phase = "PROBING " + Upper(target.name);

        // <b>无条件回显</b>（v1.34.0，用户定：终端里要具体的命令行输出）。
        //
        // 此前只在连着目标时回显，理由是「未连接时 Programs.probe 的目标是
        // os.connectedComp ?? os.thisComputer（Programs.cs:1387），回显是假命令」。
        // 那个顾虑对**游戏原语**成立，对本步不成立：端口报告取自目标对象本身
        // （HackEngine.ProbeReport），不看连接 —— 侦察确实发生了，就该看得见。
        // 缺了它，玩家只看到端口被破，看不到在探什么。
        Echo(os, step.Command ?? "probe");
        // 端口报告**无条件**打（v1.34.0 修正）。
        //
        // 曾短暂地在 noshow 下压掉它，理由是「167 台上千行太吵」。那是把
        // 「不要动画」误读成「不要输出」—— 演出开关管的是原生破解程序要不要
        // 挂进 RAM 面板，而 probe 报告是**侦察结果**（目标开了哪些端口、
        // 要破几个才够提权门槛），属于战果本身，不是过程演出。
        // 没有它，noshow 下玩家只能看到一串 sshcrack，看不到在打什么。
        foreach (var line in HackEngine.ProbeReport(target))
        {
            os.write(line);
        }
    }

    /// <summary>
    /// 破解单个端口：端口优先交给原生动画去开，<b>命令回显也跟着动画走</b> ——
    /// 动画真正挂上 RAM 面板那一帧才回显该行（见 <see cref="NativeExes.Tick"/>）。
    /// 排不上演出时立即回显并直接补开，保证命令与端口都不会丢。
    /// </summary>
    private void ApplyOpenPort(OS os, HackStep step)
    {
        var target = step.Target;
        Phase = "CRACKING PORT " + step.Port.DisplayPort;

        // 端口交给动画去开（v1.33.3）：那 9 个 exe 各自在 Completed() 里调
        // openPort(<自己的原始终端口号>, ip)，与这里调 HackEngine.OpenPort 落在
        // 同一个 PortState.Cracked 上（ComputerExtensions.cs:184-197 的 Prefix
        // 直接拿调用方传入的原始端口号匹配 Record.OriginalPortNumber）。
        // 故「动画跑完端口才开」不需要造机制 —— 只要不再提前写。
        //
        // 回显同样交给动画代理（用户定）：本行命令由 NativeExes 在实例真正 addExe
        // 那一刻写出，故「终端上出现 sshcrack 22」与「画面上开始跑那个动画」是同一件事。
        // 此前是无条件提前回显，于是被丢弃的动画其命令照样已经打出（观感与画面脱钩）。
        //
        // Show 返回 false 的每一种情形（未连接 / 已控 / 该端口没有可安全演出的程序，
        // 实测 73/642 / 队列满被丢弃）都走下面这一支：立即回显 + 补开端口 ——
        // 端口不补则永远不开，命令不补则终端与战果对不上。
        if (Options.ShowExes && NativeExes.Show(os, target, step.Port, () => Echo(os, step.Command)))
        {
            return;
        }

        Echo(os, step.Command);
        HackEngine.OpenPort(target, step.Port, os.thisComputer.ip);
    }

    /// <summary>解目标防火墙：回显 solve 后无条件解除。</summary>
    private void ApplySolveFirewall(OS os, Computer target)
    {
        Phase = "BYPASSING FIREWALL ON " + Upper(target.name);

        // 同 OpenPort：无条件回显（v1.34.0）。下面 SolveFirewall 直接解目标机
        // 的 firewall，不经连接，故回显与效果始终对应。
        Echo(os, "solve " + (target.firewall?.solution ?? string.Empty));

        SolveFirewall(os, target);
    }

    /// <summary>
    /// 提权：回显 porthack（无条件），先试原生门禁，过不了就直接写 adminIP。
    /// </summary>
    private void ApplyEscalate(OS os, HackStep step)
    {
        var target = step.Target;
        Phase = "ESCALATING";

        // 无条件回显（v1.34.0）。下面走 giveAdmin 直接写目标机 adminIP，
        // 不经连接 —— 提权确实发生了，回显与效果对应。
        Echo(os, step.Command ?? "porthack");

        // 只用 giveAdmin，不用 os.takeAdmin(ip)：后者内部还会 runCommand("connect " + ip)
        // （OS.cs:1871-1879），而 connect 的第一件事就是无条件断开旧连接
        // （Programs.connect，Programs.cs:235-236）—— 那会立刻触发 handleDisconnection
        // 与管理员反扑，等于自找麻烦。此处已连着目标，写所有权标记即可。
        // 两道门禁：先试原生 porthack 语义（端口数越不过门槛且防火墙已解），
        // 过不了就直接写 adminIP —— 与原生提权成功的终态完全一致
        // （<c>giveAdmin</c> 就是游戏自己提权时走的那一步，Computer.cs:851-855）。
        //
        // 这曾是可选开关（v1.33.1 的 ForceEscalate，缺省开），v1.33.2 起常驻：
        // porthack 的门禁对实测存档里 22 台机器恒假（9 台防护机门槛 9999998、
        // 13 台普通机器破满端口也差 1~6 个），留一个「默认开、关了就打不下」
        // 的开关，等于给唯一出路配了个自毁按钮。
        //
        if (HackEngine.CanEscalate(target))
        {
            target.giveAdmin(os.thisComputer.ip);
        }
        else
        {
            HackEngine.ForceEscalate(target, os);
        }

        // 补上游戏自己的收尾文案（用户定，v1.40.0）：PortHackExe.Completed() 写完
        // adminIP 后就写这一行（PortHackExe.cs:123-124）。mod 不走那个 exe（理由见上），
        // 但玩家该看到同一个终点 —— 否则「提权到手了」在终端上没有落点。
        // 判后置条件而非「调用了 giveAdmin」：adminIP 已是玩家时 ForceEscalate 返回 false，
        // 那种情况下写这行等于虚报。
        if (HackEngine.IsOwned(target, os))
        {
            os.write("--Porthack Complete--");
        }
    }

    /// <summary>
    /// 绕过目标白名单：只对确实连不上的目标动手，放行后重连并解除
    /// <see cref="_refused"/> 登记；连着时是空操作。
    /// </summary>
    private void ApplyBypassWhitelist(OS os, Computer target)
    {
        // 只对确实连不上的目标动手 —— 连接正常的机器没必要（也不该）改它的白名单。
        //
        // 判据不能写成「本轮被拒过」（_refused）：只有 Connect 步失败才会登记，
        // 而「当前节点」模式压根不排 Connect 步（BuildSteps 里 alreadyConnected
        // 为真时跳过，见 :679-683），于是这一步在所有直连路径上都静默空转 ——
        // 正是「当前节点模式下带白名单的机器没反应」的最后一道门。
        // 改为核对连接结果：连着就别碰，连不上才绕。
        if (ReferenceEquals(os.connectedComp, target))
        {
            return;
        }

        Phase = "BYPASSING WHITELIST ON " + Upper(target.name);

        // 这一步不是终端指令（未连接状态下 append 的「当前目录」是玩家自己的
        // 文件系统），故不发 Echo —— 它自己写 /Whitelist/list.txt 并重连。
        if (HackEngine.BypassWhitelist(os, target, os.thisComputer.ip) == null)
        {
            return;
        }

        // 白名单已放行，重连应当成功；成了就恢复正常流程
        // （清痕与断开都依赖连接，之前被 DependsOnConnection 挡掉了）。
        Echo(os, "connect " + target.ip);
        Programs.connect(["connect", target.ip], os);
        if (ReferenceEquals(os.connectedComp, target))
        {
            _refused.Remove(target);
        }
    }

    /// <summary>投放标记文件：只在目标确已拿下（含靠 login 提权）时写 autohack.txt。</summary>
    private void ApplyUploadMarker(OS os, Computer target)
    {
        Phase = "UPLOADING PAYLOAD";
        UploadMarker(os, target);
    }

    /// <summary>
    /// 清痕：按 IP 逐条点名删目标 /log 中提及玩家 IP 的条目。
    /// 不回显 rm、不报统计，也不依赖连接。
    /// </summary>
    private void ApplyCleanLogs(OS os, Computer target)
    {
        Phase = "WIPING TRACES";

        // 不回显 rm，也不报统计（v1.34.0，用户定：终端里只要具体的命令行输出）。
        //
        // 不回显 rm：这一步已不是「敲一条终端命令」，而是按 IP 逐条点名删日志条目
        // （见 HackEngine.WipeTraces）。回显 rm log/* 反而误导 —— 玩家会以为整个
        // /log 被清空了，实际只删了提到自己 IP 的那些。
        //
        // 不需要连接：目标机与目录都由参数给定，不读 os.connectedComp。
        HackEngine.WipeTraces(target, os.thisComputer.ip);
    }

    /// <summary>断开连接：由 Leave 解除反扑、回显并静默断开；未连接时是空操作。</summary>
    private void ApplyDisconnect(OS os, HackStep step)
    {
        Leave(os, step.Target, step.Command ?? "dc");
    }

    /// <summary>终止进行中的追踪（静默 —— 收尾动作不写终端）。</summary>
    private void ApplyKillTrace(OS os)
    {
        Phase = "KILLING TRACE";
        KillTrace(os);
    }

    /// <summary>
    /// 离开节点：先解除管理员反扑，再回显并断开。未连接时是空操作（避免多余的 "Disconnected" 噪音）。
    ///
    /// 断开这一步同时干两件事，缺一不可：
    /// <list type="number">
    /// <item>解除延迟反扑 —— 否则 0~20 秒后全部端口被关、<c>adminIP</c> 被还原成机器自己，
    /// 刚打下的肉鸡标记当场丢失（「全网扫描失去效果」的根因）。</item>
    /// <item>中止追踪 —— <c>TraceTracker.Update</c> 在 connectedComp 为空或换目标时置
    /// <c>active = false</c>，断开是确定性的止血动作。</item>
    /// </list>
    /// </summary>
    private static void Leave(OS os, Computer target, string command)
    {
        // 未连接时连回显都不做：Leave 是「收尾」，没连接就没有可收的尾。
        if (os.connectedComp == null)
        {
            return;
        }

        Neutralize(os, target);
        Echo(os, command);

        // 静默断开的全部理由（为什么静音、为什么多人不静音）见
        // <see cref="HackEngine.SilentDisconnect"/> —— 面板的 drop 工具走同一条。
        HackEngine.SilentDisconnect(os);
    }

    /// <summary>
    /// 终止进行中的追踪。排在断开之后：断开已让 TraceTracker 自己失效
    /// （connectedComp 为空），此处只是把「确实停掉了」这件事写进终端；
    /// 若断开没生效（如 stay 模式），这里就是唯一的止血点。
    /// </summary>
    private static void KillTrace(OS os)
    {
        HackEngine.KillTrace(os);
    }

    /// <summary>解除目标的延迟反扑（静默 —— 这不是终端指令，也不报状态）。</summary>
    private static void Neutralize(OS os, Computer target)
    {
        HackEngine.SuppressCounterattack(target);
    }

    /// <summary>解目标防火墙（静默 —— 指令本身已由 ApplySolveFirewall 回显）。</summary>
    private static void SolveFirewall(OS os, Computer target)
    {
        HackEngine.SolveFirewall(target, os);
    }

    /// <summary>解除跳板（静默 —— 这不是终端指令）。</summary>
    private static void BypassProxy(OS os, Computer target)
    {
        HackEngine.BypassProxy(target);
    }

    /// <summary>
    /// 回显一条指令：格式与 <c>OS.runCommand</c> 完全一致（换行 + 当前提示符 + 原文）。
    ///
    /// 命令以「当前连接」为作用域时才回显 —— 未连接时玩家敲同一条命令得到的是
    /// 另一个效果（<c>probe</c> 会探测自己、<c>porthack</c> 无从下手），
    /// 回显它就成了一条假命令。<c>Apply</c> 已在每处按需决定是否回显，
    /// 这里只兜住「拿不准就别写」这一条。
    /// </summary>
    private static void Echo(OS os, string command)
    {
        if (string.IsNullOrEmpty(command) || os.terminal == null)
        {
            return;
        }

        os.write("\n" + os.terminal.prompt + command);
    }

    private static void UploadMarker(OS os, Computer target)
    {
        // 按「是否已拿下」判定，而不是 CanEscalate —— 后者要求端口已破，
        // 而靠 login 提权的目标一个端口都没破，用它会漏掉投放。
        if (!HackEngine.IsOwned(target, os))
        {
            return;
        }

        var home = target.getFolderPath("home", true);
        if (home != null)
        {
            target.makeFile(os.thisComputer.ip, "autohack.txt", "== ACCESS CONTROL ==" + Environment.NewLine + "released by autohack" + Environment.NewLine, home);
        }
    }
}

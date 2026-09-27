namespace AutoHack;

using Hacknet;
using Pathfinder.Port;

/// <summary>单个目标的战果（GUI 展示用）。</summary>
internal sealed record TargetOutcome(string Name, int Opened, int Total, bool Escalated);

/// <summary>
/// 一次入侵运行的执行状态与推进逻辑。
/// 由 OS.Update / OSUpdateEvent 每帧驱动；与绘制解耦，便于单独测试。
/// 每个动作先把对应指令回显到终端（格式同 OS.runCommand），再执行。
/// 端口一律按 Pathfinder 的协议名操作（<c>openPort(protocol, ipFrom)</c>），
/// 而非原版按端口号 —— 原版那条路径已被框架 Prefix 拦下。
///
/// 三个必须尊重的游戏机制：
/// 1. 管理员反扑：断开连接时 OS.handleDisconnection() 会调
///    admin?.disconnectionDetected()，BasicAdministrator 在 0~20 秒后关掉全部端口并把
///    adminIP 还原成机器自己 —— 肉鸡标记当场丢失，这就是「全网扫描失去效果」的根因。
///    必须在断开前把 computer.admin 置 null（<see cref="HackEngine.SuppressCounterattack"/>）。
/// 2. 追踪：TraceTracker 只在「连着被追踪目标」时推进（Update 见 connectedComp 为空
///    即刻置 active = false），故跑完一个目标就断开连接即等于反追踪；
///    否则计时归零会走 OS.timerExpired() 端掉玩家。
/// 3. 清痕必须在断开**之前**：rm 这类命令的目标机取自 os.connectedComp
///    （Programs.rm，Programs.cs:956），且 Programs.disconnect 会清空
///    os.navigationPath —— 断开之后再清，回显的 rm 就是条假命令，正如玩家
///    手敲时那样「没有效果」。断开本身会写 "&lt;玩家IP&gt; Disconnected"
///    （Computer.cs:722-727），故 Leave 断开时把 silent 置真，避免刚清的痕迹被写回。
/// 4. 跳板：proxyActive 的机器上，需要跳板访问权的破解程序被 OS.addExe 门禁拦下
///    （OS.cs:2165）。解除即把 proxyOverloadTicks 收敛到 0、proxyActive 置 false ——
///    与 ShellExe 过载跑完的终态逐字节相同，只是不等那 30 秒。
///    刻意不照抄 ShellExe.cs:105 的 hostileActionTaken() —— 那只会点燃追踪。
/// </summary>
internal sealed class HackRun
{
    /// <summary>Normal 档的非端口步间隔（真人节奏）。</summary>
    private const float NormalStepDelay = 0.35f;

    private readonly List<Computer> _targets;
    private readonly List<HackStep> _steps;

    private int _index;
    private float _timer;

    /// <summary>本次运行中靠已知凭据登入（即已提权）的机器，其破端口类步骤整体跳过。</summary>
    private readonly HashSet<Computer> _loggedIn = new();

    /// <summary>
    /// 连接被目标主动拒绝（白名单）的机器。
    ///
    /// 它**只跳过依赖连接的步骤**（<see cref="HackStepKind.Connect"/>、
    /// <see cref="HackStepKind.CleanLogs"/>、<see cref="HackStepKind.Disconnect"/>），
    /// 其余（破端口 / 解跳板 / 解防火墙 / login / 提权 / 投放）全部照常执行 ——
    /// 那些步骤直接操作 <c>target</c> 对象，本就不经过连接。
    ///
    /// v1.23.0 曾整段跳过这类目标，理由是「连接没成而后续照样成功 = 假战果」。
    /// 那个顾虑只对<b>依赖连接语义</b>的步骤成立（rm 的目标机取自
    /// <c>os.connectedComp</c>，断开后 rm 会打到自己的文件系统上）；
    /// 对操作对象本身的步骤并不成立 —— 端口表的 <c>Cracked</c> 状态、
    /// <c>giveAdmin</c> 写入的 <c>adminIP</c> 都落在目标机上，是真的战果。
    /// 整段跳过等于把一台本来能拿下的机器直接放弃，这才是真损失。
    /// </summary>
    private readonly HashSet<Computer> _refused = new();

    /// <summary>本次运行的入侵脚本；null = 用内置次序。构造期已解析完成。</summary>
    private readonly HackScript _script;

    internal HackRun(OS os, HackOptions options)
    {
        Options = options;

        // 脚本在构造期解析一次：语法错/文件缺失在这里抛出，由调用方转成终端可见
        // 的错误行。放进 Tick 会让错误每帧重复，放进 BuildSteps 会让「目标为空」
        // 与「脚本坏了」两种情况混在一起。
        _script = string.IsNullOrEmpty(options.Script) ? null : HackScript.Load(options.Script);

        var plan = HackEngine.ResolveTargets(os, options);
        _targets = plan.Targets;
        SkippedOwned = plan.SkippedOwned;
        SkippedHopeless = plan.SkippedHopeless;

        // clearLogs 已开时全体都清，不存在「额外强制」的机器，故那种情况计 0。
        ForcedLogWipe = options.ClearLogs ? 0 : _targets.Count(t => t.HasTracker);

        _steps = BuildSteps(_targets, plan.Skipped, options, os, _script);
        Current = _targets.Count > 0 ? _targets[0].name : "-";
        Phase = "ENGAGING";
    }

    internal HackOptions Options { get; }

    internal List<TargetOutcome> Outcomes { get; } = new();

    internal int Total => _steps.Count;

    internal int Done => Math.Min(_index, _steps.Count);

    internal bool Finished { get; private set; }

    internal string Phase { get; private set; }

    internal string Current { get; private set; }

    internal IReadOnlyList<Computer> Targets => _targets;

    /// <summary>因已控（肉鸡）而跳过的机器数。</summary>
    internal int SkippedOwned { get; }

    /// <summary>因提权门槛高于端口表容量（永远打不通）而跳过的机器数。</summary>
    internal int SkippedHopeless { get; }

    /// <summary>
    /// 因带 <c>tracker="true"</c> 而被强制清痕的机器数（不受 <c>clearLogs</c> 开关约束）。
    /// 理由见 <see cref="BuildSteps"/> 的清痕分支。
    /// </summary>
    internal int ForcedLogWipe { get; }

    /// <summary>
    /// 面板标题一律大写。在每个 Phase 赋值处转换一次，
    /// 而不是在绘制期每帧对同一字符串重复转换。
    /// </summary>
    private static string Upper(string value)
        => string.IsNullOrEmpty(value) ? string.Empty : value.ToUpperInvariant();

    /// <summary>推进一帧。只允许游戏线程调用 —— 会改动游戏状态。</summary>
    internal void Tick(OS os, float deltaSeconds)
    {
        if (Finished)
        {
            return;
        }

        _timer += deltaSeconds;

        // Instant 档：非端口步不等时间，一帧内连跑到底，直到撞上一个端口步
        // 或本帧的预算耗尽。端口步始终按 Options.PortDelay 等 —— 那是回显
        // 逐条浮现的节奏来源，也是唯一有意义的等待。
        var budget = MaxStepsPerFrame;
        while (!Finished && budget-- > 0)
        {
            if (_index >= _steps.Count)
            {
                Finish(os);
                return;
            }

            var step = _steps[_index];

            // 该目标的连接被拒（白名单）：只跳过**依赖连接**的步骤，其余照常执行。
            // 依据见 _refused 的字段注释 —— 破端口/提权/投放操作的是 target 对象本身，
            // 不经连接，跳过它们等于白白放弃一台能拿下的机器。
            if (_refused.Contains(step.Target) && DependsOnConnection(step.Kind))
            {
                _index++;
                continue;
            }

            // 该目标已靠 login 提权：破端口/解防火墙/porthack 都是无用功，
            // 直接跳过（不回显、不耗时）。停机也计入 Done，进度条才走得准。
            if (IsRedundantAfterLogin(step))
            {
                _index++;
                continue;
            }

            var delay = DelayFor(step.Kind);
            if (_timer < delay)
            {
                return;
            }

            _timer = 0f;
            Apply(os, step);
            _index++;
        }
    }

    /// <summary>单帧最多执行多少步，防止 Instant 档在极端规模下一帧卡死。</summary>
    private const int MaxStepsPerFrame = 512;

    /// <summary>
    /// 该步骤是否依赖「已连接」这一状态。
    ///
    /// 只有三个：Connect 自己；清痕（<c>rm</c> 的目标机取自 <c>os.connectedComp</c>，
    /// Programs.cs:956）；断开（未连接时是空操作）。其余步骤操作的是 <c>target</c>
    /// 对象本身，不经连接 —— 端口表的 <c>Cracked</c> 状态与 <c>giveAdmin</c> 写入的
    /// <c>adminIP</c> 都落在目标机上，连不连得上都成立。
    /// </summary>
    private static bool DependsOnConnection(HackStepKind kind) => kind switch
    {
        HackStepKind.Connect => true,
        HackStepKind.CleanLogs => true,
        HackStepKind.Disconnect => true,
        _ => false,
    };

    /// <summary>
    /// 该步骤是否已被 login 提权化为无用功。只对<b>本次运行中确实靠 login 拿下</b>
    /// 的目标成立（<see cref="_loggedIn"/>），不影响 redo 模式下重打已控节点的语义。
    /// </summary>
    private bool IsRedundantAfterLogin(HackStep step)
    {
        if (_loggedIn.Count == 0 || !_loggedIn.Contains(step.Target))
        {
            return false;
        }

        return step.Kind is HackStepKind.BypassProxy
            or HackStepKind.OpenPort
            or HackStepKind.SolveFirewall
            or HackStepKind.Escalate;
    }

    /// <summary>该步骤需等待的秒数。Instant 档把非端口步压到 0。</summary>
    private float DelayFor(HackStepKind kind)
    {
        if (kind == HackStepKind.OpenPort)
        {
            return Options.PortDelay;
        }

        // 清痕是纯内存操作（ClearLogs 只做 List 清空 + deleteFile 遍历，
        // 无磁盘 IO、无 Thread.Sleep），且每台至多回显一行摘要 ——
        // 没有需要人眼跟上的逐条节奏。故不吃节流：一整屏机器同帧抹完。
        // 单帧步数仍受 MaxStepsPerFrame 约束，不会失控。
        if (kind == HackStepKind.CleanLogs)
        {
            return 0f;
        }

        // 脚本自带 delay 行时以它为准（游戏 HackerScript 的 config 第 4 参同义），
        // 否则回到 speed 档位 —— 两者正交：档位管「多快」，脚本管「什么次序」。
        if (_script?.StepDelay is { } scripted)
        {
            return scripted;
        }

        return Options.Speed switch
        {
            HackSpeed.Instant => 0f,
            HackSpeed.Fast => HackOptions.FastStepDelay,
            _ => NormalStepDelay,
        };
    }

    private void Apply(OS os, HackStep step)
    {
        var target = step.Target;
        Current = target.name + " @ " + target.ip;

        switch (step.Kind)
        {
            case HackStepKind.Connect:
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
                    os.write("[autohack] " + target.name
                        + " :: connection refused (whitelist) - continuing without a session");
                    break;
                }

                break;

            case HackStepKind.Neutralize:
                Phase = "DISABLING COUNTERATTACK ON " + Upper(target.name);
                Neutralize(os, target);
                break;

            case HackStepKind.BypassProxy:
                Phase = "BYPASSING PROXY ON " + Upper(target.name);
                BypassProxy(os, target);
                break;

            case HackStepKind.Login:
                Phase = "LOGGING IN";

                // login 成功即 giveAdmin（Computer.cs:851-855），与 porthack 终点等价，
                // 但不破端口、不触发追踪。凭据来自目标自身的公开字段与 known 标记。
                if (HackEngine.TryLogin(target, out var credential))
                {
                    Phase = "LOGGED IN";

                    // 只回显 login 本身：原版 login 是交互式的（先问用户名再问密码，
                    // Programs.cs:404-448），没有 "login <user> <pass>" 这种写法，
                    // 拼上参数会是条游戏里不存在的命令。用了哪组凭据由下面的状态行交代。
                    Echo(os, "login");

                    // 本目标已提权，后续破端口/解防火墙/porthack 都不必跑。
                    // 只登记「本次运行中确实靠 login 拿下的」机器 —— 不用
                    // 「adminIP 已是我们」这个更宽的判据，否则 redo 模式
                    // （重打已控节点）会连端口都不破，改变其语义。
                    _loggedIn.Add(target);

                    os.write("[autohack] " + target.name + " :: admin via login (" + credential + ") - skipping port cracks");
                }
                else
                {
                    // 失败必须可见：静默会把「为什么没跳过」这条最有价值的信息藏起来。
                    os.write("[autohack] " + target.name + " :: login unavailable - cracking ports");
                }

                break;

            case HackStepKind.Probe:
                Phase = "PROBING " + Upper(target.name);

                // 只有连着目标时 probe 才是在探它 —— 未连接时 Programs.probe 的目标是
                // os.connectedComp ?? os.thisComputer（Programs.cs:1387），回显会是假命令。
                // 端口报告本身取自目标对象（HackEngine.ProbeReport），不看连接，故照常输出。
                if (os.connectedComp == target)
                {
                    Echo(os, step.Command ?? "probe");
                }
                foreach (var line in HackEngine.ProbeReport(target))
                {
                    os.write(line);
                }

                break;

            case HackStepKind.OpenPort:
                Phase = "CRACKING PORT " + step.Port.DisplayPort;

                // 破解程序的作用域是「当前连接」；未连接时它无从打到目标上，
                // 而下面的 HackEngine.OpenPort 是直接写目标机端口表，照样生效。
                if (os.connectedComp == target)
                {
                    Echo(os, step.Command);
                }

                HackEngine.OpenPort(target, step.Port, os.thisComputer.ip);

                // 状态已写好，这里只是把原版动画挂上 RAM 面板（缺省关）。
                if (Options.ShowExes)
                {
                    NativeExes.Show(os, target, step.Port);
                }

                break;

            case HackStepKind.SolveFirewall:
                Phase = "BYPASSING FIREWALL ON " + Upper(target.name);

                // 同 OpenPort：solve 命令的作用域是当前连接，未连接时是假命令。
                if (os.connectedComp == target)
                {
                    Echo(os, "solve " + (target.firewall?.solution ?? string.Empty));
                }

                SolveFirewall(os, target);
                break;

            case HackStepKind.Escalate:
                Phase = "ESCALATING";

                // 未连接时 porthack 在终端里无从下手（它读 os.connectedComp），
                // 而下面的 giveAdmin 是直接写目标机的 adminIP，照样生效。
                if (os.connectedComp == target)
                {
                    Echo(os, step.Command ?? "porthack");
                }

                // 只用 giveAdmin，不用 os.takeAdmin(ip)：后者内部还会 runCommand("connect " + ip)
                // （OS.cs:1871-1879），而 connect 的第一件事就是无条件断开旧连接
                // （Programs.connect，Programs.cs:235-236）—— 那会立刻触发 handleDisconnection
                // 与管理员反扑，等于自找麻烦。此处已连着目标，写所有权标记即可。
                if (HackEngine.CanEscalate(target))
                {
                    target.giveAdmin(os.thisComputer.ip);
                }

                break;

            case HackStepKind.BypassWhitelist:
                // 只对确实连不上的目标动手 —— 连接正常的机器没必要（也不该）改它的白名单。
                //
                // 判据不能写成「本轮被拒过」（_refused）：只有 Connect 步失败才会登记，
                // 而「当前节点」模式压根不排 Connect 步（BuildSteps 里 alreadyConnected
                // 为真时跳过，见 :679-683），于是这一步在所有直连路径上都静默空转 ——
                // 正是「当前节点模式下带白名单的机器没反应」的最后一道门。
                // 改为核对连接结果：连着就别碰，连不上才绕。
                if (ReferenceEquals(os.connectedComp, target))
                {
                    break;
                }

                Phase = "BYPASSING WHITELIST ON " + Upper(target.name);

                // 这一步不是终端指令（未连接状态下 <c>append</c> 的「当前目录」是玩家
                // 自己的文件系统，回显出来会是一条假命令），故不发 Echo，只报状态。
                var bypassNote = HackEngine.BypassWhitelist(os, target, os.thisComputer.ip);
                if (bypassNote == null)
                {
                    os.write("[autohack] " + target.name
                        + " :: no /Whitelist folder to touch - staying session-less");
                    break;
                }

                os.write("[autohack] " + target.name + " :: " + bypassNote);

                // 白名单已放行，重连应当成功；成了就恢复正常流程
                // （清痕与断开都依赖连接，之前被 DependsOnConnection 挡掉了）。
                Programs.connect(["connect", target.ip], os);
                if (ReferenceEquals(os.connectedComp, target))
                {
                    _refused.Remove(target);
                    os.write("[autohack] " + target.name + " :: reconnected - whitelist bypassed");
                }

                break;

            case HackStepKind.UploadMarker:
                Phase = "UPLOADING PAYLOAD";
                UploadMarker(os, target);
                break;

            case HackStepKind.CleanLogs:
                Phase = "WIPING LOGS";

                // 正常目标：清痕排在它自己的 Disconnect 之前，此刻 os.connectedComp
                // 就是 target，回显的 rm 是一条真能跑的命令（正是玩家手敲的那条）。
                // 先回显后执行，与其余步骤同一约定。
                //
                // 命令的两个作用域都成立，缺一不可：
                // ① 目标机取自连接 —— Programs.rm（Programs.cs:956）用的是
                //    os.connectedComp，断开后就变成玩家自己的文件系统；
                // ② 目录取自当前目录 —— rm 的参数 "log/*" 会经 getFolderAtPath
                //    （Programs.cs:1582）在【当前目录】下找 log 子文件夹。
                //    本插件从不发 cd，而 connect 会 Clear() 导航路径
                //    （Programs.cs:235），故此刻当前目录恒为目标根。
                //
                // 路径不写前导斜杠：Hacknet 没有绝对路径，getFolderAtPath 按 '/'
                // 切分后把空段整个跳过（Programs.cs:1590），"/log" 与 "log" 解析结果
                // 相同 —— 但前者会让人以为它从根出发。玩家在 cd log 之后敲
                // "rm log/*" 之所以失败，正是因为在【当前目录】里再找 log 找不到。
                var onTarget = os.connectedComp == target;
                if (onTarget)
                {
                    Echo(os, step.Command ?? "rm log/*");
                }

                var wiped = HackEngine.ClearLogs(target, os.thisComputer.ip);

                // 战果必须可见。原版 rm 逐文件打印 "Deleting <名>." + "Done"
                // （Programs.cs:1018-1031），全自动跑 100+ 台会刷屏，压成一行摘要，
                // 措辞沿用游戏自己的两个词。删 0 条时不吭声 —— 无痕迹的机器是多数。
                if (wiped.Count > 0)
                {
                    os.write(onTarget
                        ? "Deleting " + wiped.Count + " file(s)... Done"
                        : "[autohack] " + target.name + " :: rm log/* -> "
                            + wiped.Count + " log file(s) wiped");
                }

                break;

            case HackStepKind.Disconnect:
                Leave(os, target, step.Command ?? "dc");
                break;

            case HackStepKind.KillTrace:
                Phase = "KILLING TRACE";
                KillTrace(os);
                break;
        }
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
        if (HackEngine.KillTrace(os))
        {
            os.write("[autohack] trace killed - timer stopped.");
        }
    }

    /// <summary>解除目标的延迟反扑，仅在实际解除时回显一行（这不是终端指令，故不走 Echo）。</summary>
    private static void Neutralize(OS os, Computer target)
    {
        if (HackEngine.SuppressCounterattack(target))
        {
            os.write("[autohack] " + target.name + " :: admin counterattack disabled");
        }
    }

    /// <summary>解目标防火墙，仅在实际解开时回显一行（非终端指令）。</summary>
    private static void SolveFirewall(OS os, Computer target)
    {
        if (HackEngine.SolveFirewall(target, os))
        {
            os.write("[autohack] " + target.name + " :: firewall solved");
        }
    }

    /// <summary>解除跳板，仅在实际解除时回显一行（同上，非终端指令）。</summary>
    private static void BypassProxy(OS os, Computer target)
    {
        if (HackEngine.BypassProxy(target))
        {
            os.write("[autohack] " + target.name + " :: proxy bypassed");
        }
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

    private void Finish(OS os)
    {
        Finished = true;
        Phase = "COMPLETE";

        // 收尾反追踪 —— 无条件执行，不是选项：止住倒计时 + 清空脱机追踪列表。
        // **不擦追踪者的 /log**（用户定）：那是目标机的操作史，改由 wipe target logs 单独决定。
        // 已知代价 —— 追踪的复发源正是那些日志：OS.handleDisconnection（OS.cs:944-960）
        // 在每次断开时检查刚断开那台的 /log，只要有一行同时含玩家 IP 与
        // FileCopied/FileDeleted/FileMoved，就自动排入一条新的 TrackerDetail
        // （判据见 TrackerCompleteSequence.CompShouldStartTrackerFromLogs，:30-47），
        // 10~20 秒后计时归零端掉玩家。故同一台机器上的追踪可能复发 —— 要断源就开 wipe target logs。
        //
        // 排在换 IP 之前：语义上「先收拾追踪、再换身份」；且换 IP 会改掉
        // os.thisComputer.ip，任何按旧 IP 匹配的判断都必须在它之前做完。
        TraceTools.Run(os);

        // 换 IP：游戏原生的「保命」动作（ISPDaemon 的 "Assign New IP"），
        // 并把全图已控机器的归属迁移到新 IP。理由与代价见 IpTools 的文档注释。
        //
        // 排在清追踪之后：TraceTools 判「旧 IP 是否在日志里」只在擦日志时才有意义 ——
        // 现在不擦日志了，但顺序仍然保持，因为语义上「先收拾追踪、再换身份」更清楚。
        if (Options.ResetIP)
        {
            var ipNote = IpTools.Reset(os);
            if (ipNote != null)
            {
                os.write("[autohack] new local IP: " + ipNote + ".");
            }
        }

        foreach (var target in _targets)
        {
            // 两个数取自同一次快照：分两次查会各分配一份端口表副本
            // （GetAllPortStates 是 ...Values.ToList()），且理论上可能显示
            // opened > total 的不一致比例。PortInfo 自带 Cracked，无需第二次遍历。
            var ports = HackEngine.Ports(target);
            var opened = ports.Count(p => p.Cracked);

            // 所有权看 adminIP（肉鸡标记的真身），不看 CanEscalate ——
            // 后者要求端口已破，靠 login 拿下的目标会因为 0 端口而被误报 admin=no。
            var owned = HackEngine.IsOwned(target, os);
            Outcomes.Add(new TargetOutcome(target.name, opened, ports.Count, owned));
            os.write("[autohack] " + target.name + " :: " + opened + "/" + ports.Count
                + " ports, admin=" + (owned ? "yes" : "no"));
        }

        if (_targets.Count == 0)
        {
            // 目标为 0 是最容易被误读的状态：终端仍会逐台打清痕行，
            // 看着像「每台都重跑了一遍」，实际每台只抹了 log。
            // 必须把原因和出路直接写出来。
            os.write("[autohack] No targets: all " + (SkippedOwned + SkippedHopeless)
                + " reachable node(s) were filtered out ("
                + SkippedOwned + " already owned, " + SkippedHopeless + " cannot escalate).");
            os.write("[autohack]   'redo' re-hacks owned nodes; 'allnodes' sweeps the whole map.");
        }

        if (SkippedOwned > 0)
        {
            os.write("[autohack] skipped " + SkippedOwned + " node(s) already owned - 'redo' to include them.");
        }

        if (SkippedHopeless > 0)
        {
            os.write("[autohack] skipped " + SkippedHopeless
                + " node(s) whose port table cannot reach the escalation threshold.");
        }

        if (_refused.Count > 0)
        {
            os.write("[autohack] " + _refused.Count
                + " node(s) refused the session (whitelist) - cracked without one.");
        }

        if (ForcedLogWipe > 0)
        {
            os.write("[autohack] wiped " + ForcedLogWipe
                + " node(s) carrying tracker=\"true\" - their /log would auto-start a trace on disconnect.");
        }

        Current = "done - " + _targets.Count + " target(s)";
    }

    /// <summary>
    /// 展开动作序列：连接 → 侦察 → 解跳板 → 逐端口攻破 → 提权 → 投放 → 清痕 → 断开。
    /// 清痕必须最后（提权与投放都会向 /log 追加记录），断开更在其后。
    /// 已连接的节点不再重复 connect（那会先断开再重连，徒增噪音）。
    /// <paramref name="skipped"/> 是被剔除的机器，只在末尾追加清痕。
    /// </summary>
    private static List<HackStep> BuildSteps(
        List<Computer> targets, IReadOnlyList<Computer> skipped, HackOptions options, OS os,
        HackScript script)
    {
        var steps = new List<HackStep>((targets.Count + skipped.Count) * 10);

        foreach (var target in targets)
        {
            if (script != null)
            {
                AppendScripted(steps, target, script, options, os);
                continue;
            }

            var alreadyConnected = ReferenceEquals(target, os.connectedComp);
            if (options.ConnectFirst && !alreadyConnected)
            {
                steps.Add(new HackStep(HackStepKind.Connect, target, default, "connect " + target.ip));
            }

            // 独立成步而非挂在 Connect 上：direct/已连接路径没有 Connect 步，同样需要解除反扑。
            steps.Add(new HackStep(HackStepKind.Neutralize, target, default, null));

            steps.Add(new HackStep(HackStepKind.Probe, target, default, "probe"));

            // 已知凭据登录排在最前：成功即提权，后面整段破端口动作都不必跑。
            // 放在 probe 之后是为了让终端先有原生端口报告，再看到 login。
            //
            // EOS 设备无条件排这一步：它的端口容量等于提权门槛，破端口是死路，
            // 固定密码 "alpine" 才是游戏设计的正路（见 HackEngine.IsEosDevice）。
            if (options.UseCredentials || HackEngine.IsEosDevice(target))
            {
                steps.Add(new HackStep(HackStepKind.Login, target, default, null));
            }

            var ports = HackEngine.CrackablePorts(target);
            if (ports.Count > 0 && HackEngine.ProxyActive(target))
            {
                steps.Add(new HackStep(HackStepKind.BypassProxy, target, default, null));
            }

            foreach (var port in ports)
            {
                steps.Add(new HackStep(HackStepKind.OpenPort, target, port, HackEngine.CrackCommand(port)));
            }

            // 防火墙必须在 porthack 之前解 —— 游戏自己的门禁要求
            // firewall.solved（OS.cs:1918-1930），未解则 porthack 直接被拒。
            if (target.firewall is { solved: false })
            {
                steps.Add(new HackStep(HackStepKind.SolveFirewall, target, default, null));
            }

            steps.Add(new HackStep(HackStepKind.Escalate, target, default, "porthack"));

            // 白名单机器无条件排一步绕过（与开关解耦）：它的 connect 会被拒，
            // 而破端口/提权不经连接、照样能拿下 —— 拿下之后再把自己的 IP 追加进它的
            // /Whitelist/list.txt，重连即恢复会话，后续清痕与断开才走得通。
            // 这是游戏设计的正路（官方任务 PAE2_Whitelist.xml 的 list_add_manual.txt
            // 明写「append list.txt <你的IP>」），详见 HackEngine.BypassWhitelist。
            if (HackEngine.HasWhitelist(target))
            {
                steps.Add(new HackStep(HackStepKind.BypassWhitelist, target, default, null));
            }

            if (options.UploadMarker)
            {
                steps.Add(new HackStep(HackStepKind.UploadMarker, target, default, null));
            }

            // 清痕必须排在断开**之前**：rm 的语义是「操作当前连接的文件系统」
            // —— Programs.rm 的作用域来自 getCurrentFolder(os)，而它读的是
            // os.connectedComp 与 os.navigationPath（Programs.cs:1531-1534 →
            // getFolderAtDepth :1536-1560），Programs.disconnect 又会把
            // navigationPath 清空。断开之后再清，回显的 rm 就是条假命令。
            // 带 tracker="true" 的机器**无条件**清痕，不受 clearLogs 开关约束。
            // OS.handleDisconnection（OS.cs:944-960）在断开时检查目标 /log：只要有一行
            // 同时含玩家 IP 与 FileCopied/FileDeleted/FileMoved，就自动排入追踪
            // （TrackerCompleteSequence.cs:30-47），10~20 秒后计时归零端掉玩家。
            // 而 deleteFile 每次都会写 "FileDeleted: by <玩家IP>"（Computer.cs:543）。
            // 这类机器上「留痕」不是疏忽而是自杀。
            if (options.ClearLogs || target.HasTracker)
            {
                steps.Add(new HackStep(HackStepKind.CleanLogs, target, default, "rm log/*"));
            }

            if (options.Disconnect)
            {
                steps.Add(new HackStep(HackStepKind.Disconnect, target, default, "dc"));
            }

            // 断开已让 TraceTracker 自己失效；这一步是确定性的兜底 ——
            // stay 模式（不断开）下它是唯一的止血点。走 stop()，零每帧开销。
            steps.Add(new HackStep(HackStepKind.KillTrace, target, default, null));
        }

        // 被剔除的机器照样清痕：它们此前进过、破过、侦察过，/log 里留着痕迹，
        // 「跳过入侵」不等于「放过证据」。排在全部正常步骤之后 —— 正常流程不会再碰
        // 这些机器，此刻清是终点动作，不会有新记录再追加进来。
        // 对没有痕迹的机器是幂等的：ClearLogs 返回空列表，不产生任何输出。
        if (options.ClearLogs)
        {
            foreach (var comp in skipped)
            {
                steps.Add(new HackStep(HackStepKind.CleanLogs, comp, default, null));
            }
        }

        // 玩家自己的机器单独一条开关（ClearOwnLogs），缺省关。
        //
        // 它与上面的目标清痕是两回事：上面抹的是「我入侵别人留下的证据」，
        // 这条抹的是「玩家自己的操作史」—— 玩家的 /log 记的是谁连过他、他读过什么文件。
        // 后者是玩家自己的数据，不该被「入侵时顺手」清掉，故必须显式开。
        //
        // 机器不在 targets 里（ResolveTargets 显式跳过 os.thisComputer，
        // HackEngine.cs:445），也不在 skipped 里 —— 上面两处都够不着，必须单独追加。
        //
        // 排在全部步骤之后是刻意的：玩家的 /log 记的是「谁连过我」，入侵过程中
        // 每连一台都会往自己机器上写一条，提前清会被后续步骤重新写回来。
        //
        // 不需要等断开：ClearLogs → RemoveFiles 把 folderPath 直接传给
        // Computer.deleteFile（HackEngine.cs:848），而
        // Programs.getFolderFromNavigationPath（Programs.cs:1749-1770）只读 path
        // 与 startFolder，不看 os.connectedComp / navigationPath ——
        // 与 rm 命令的作用域规则不同。
        if (options.ClearOwnLogs)
        {
            steps.Add(new HackStep(HackStepKind.CleanLogs, os.thisComputer, default, null));
        }

        return steps;
    }

    /// <summary>
    /// 按脚本给一个目标展开动作。脚本只描述「怎么打」，目标与源机由 scope 解析
    /// —— 游戏 HackerScript 的 <c>config</c> 行同时指定目标与源机，但那是给 NPC
    /// 用的（它的 connect 走 cConnection，目标机视角），对玩家终端无意义。
    ///
    /// 前置始终补三步（脚本不必写、写了也会去重）：<c>connect</c>（未连接时）、
    /// <c>neutralize</c>（解除管理员反扑，否则断开后 0~20 秒肉鸡标记丢失）、
    /// 末尾的 <c>killtrace</c>（零开销兜底）。这三步是正确性要求而非风格偏好，
    /// 交给玩家手写只会漏。
    /// </summary>
    private static void AppendScripted(
        List<HackStep> steps, Computer target, HackScript script, HackOptions options, OS os)
    {
        var emitted = new HashSet<HackStepKind>();
        var connected = ReferenceEquals(target, os.connectedComp);

        // 连接与解除反扑是前置条件，不是脚本可选项 —— 见方法注释。
        // 但 direct 模式（ConnectFirst=false）下玩家可能就是要靠脚本自己连，
        // 故那种情况不登记 connect，把决定权留给脚本。
        var connectRequired = options.ConnectFirst;
        if (connectRequired && !connected)
        {
            steps.Add(new HackStep(HackStepKind.Connect, target, default, "connect " + target.ip));
            emitted.Add(HackStepKind.Connect);
        }

        steps.Add(new HackStep(HackStepKind.Neutralize, target, default, null));
        emitted.Add(HackStepKind.Neutralize);

        foreach (var action in script.Actions)
        {
            // connect 已被前置步骤登记过时（ConnectFirst 模式），脚本里再写就跳过。
            if (action.Kind == HackStepKind.Connect)
            {
                if (connected || !emitted.Add(HackStepKind.Connect))
                {
                    continue;
                }

                steps.Add(new HackStep(HackStepKind.Connect, target, default, "connect " + target.ip));
                continue;
            }

            // OpenPort 可重复（逐端口展开）。CleanLogs 也可重复 —— 它对空 /log
            // 是幂等的（ClearLogs 返回空列表、不输出），而「证据必须消失」是硬承诺，
            // 让玩家写两次就多清一次比静默吞掉第二个更符合预期。
            // 其余动作改的是游戏状态，重复出现只取首次。
            if (action.Kind is not (HackStepKind.OpenPort or HackStepKind.CleanLogs) &&
                !emitted.Add(action.Kind))
            {
                continue;
            }

            if (action.Kind == HackStepKind.OpenPort)
            {
                foreach (var step in ExpandPorts(target, action.Port))
                {
                    steps.Add(step);
                }

                continue;
            }

            steps.Add(new HackStep(action.Kind, target, default, CommandFor(action.Kind)));
        }

        // 带 tracker="true" 的机器：脚本没写清痕也要补上（理由见 BuildSteps）。
        // 排在 KillTrace 之前。即便脚本已 dc，ClearLogs 仍按 folderPath 直取目标
        // /log（HackEngine.cs:741），不依赖连接 —— 只是不再回显那条 rm。
        if (target.HasTracker && !emitted.Contains(HackStepKind.CleanLogs))
        {
            steps.Add(new HackStep(HackStepKind.CleanLogs, target, default, "rm log/*"));
        }

        // 兜底反追踪：与内置次序同理，脚本没写也要有 —— 断开已让它失效，
        // 这一步覆盖「脚本以 stay 结尾」与「最后一步之后才被点燃」的窗口。
        if (emitted.Add(HackStepKind.KillTrace))
        {
            steps.Add(new HackStep(HackStepKind.KillTrace, target, default, null));
        }
    }

    /// <summary>
    /// 展开 <c>openPort</c> 动作。<paramref name="portNumber"/> 为 0 = 该目标上全部
    /// 有原生破解程序的端口；否则只挑匹配的那一个（按显示端口号比对，与玩家在终端里
    /// 敲的 <c>sshcrack 22</c> 同一个数）。指定的端口不存在或不含破解程序时展开为空 ——
    /// 不臆造步骤。
    /// </summary>
    private static IEnumerable<HackStep> ExpandPorts(Computer target, int portNumber)
    {
        foreach (var port in HackEngine.CrackablePorts(target))
        {
            if (portNumber != 0 && port.DisplayPort != portNumber && port.CodePort != portNumber)
            {
                continue;
            }

            yield return new HackStep(HackStepKind.OpenPort, target, port, HackEngine.CrackCommand(port));
        }
    }

    /// <summary>
    /// 脚本动作对应的终端回显原文；返回 null 表示由 Apply 的分支自行处理
    /// （<c>Connect</c> 拼 ip、<c>OpenPort</c> 用破解程序名、其余非终端指令一律不回显）。
    /// </summary>
    private static string CommandFor(HackStepKind kind) => kind switch
    {
        HackStepKind.Disconnect => "dc",
        HackStepKind.Escalate => "porthack",
        HackStepKind.Probe => "probe",
        HackStepKind.CleanLogs => "rm log/*",
        _ => null,
    };
}

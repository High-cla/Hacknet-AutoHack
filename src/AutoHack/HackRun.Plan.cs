// HackRun.Plan.cs —— 计划构建：把目标与选项展开成 HackStep 序列（纯函数，不读运行状态）。
//
// HackRun 的分部实现；类型声明、字段与其余职责见 HackRun.cs。
namespace AutoHack;

using Hacknet;
using Pathfinder.Port;

internal sealed partial class HackRun
{

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

            AppendTargetSteps(steps, target, options, os);
        }

        AppendSkippedSteps(steps, skipped, options);
        AppendSelfStep(steps, os, options);

        return steps;
    }

    /// <summary>
    /// 为一个目标展开内置次序：连接 → 侦察 → 解跳板 → 逐端口攻破 → 提权 → 投放 → 清痕 → 断开。
    /// 本方法只负责把四段串起来，各段的判据与注释都在对应的小方法里。
    /// </summary>
    private static void AppendTargetSteps(
        List<HackStep> steps, Computer target, HackOptions options, OS os)
    {
        AppendEngageSteps(steps, target, options, os);
        AppendPortSteps(steps, target, options);
        AppendEscalateSteps(steps, target);
        AppendCleanupSteps(steps, target, options);
    }

    /// <summary>入场的四步：连接（可选）→ 解除反扑 → 侦察 → 登录（可选）→ 解跳板（可选）。</summary>
    private static void AppendEngageSteps(
        List<HackStep> steps, Computer target, HackOptions options, OS os)
    {
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

        var ports = HackEngine.CrackablePorts(target, options.ModPorts);
        if (ports.Count > 0 && HackEngine.ProxyActive(target))
        {
            steps.Add(new HackStep(HackStepKind.BypassProxy, target, default, null));
        }
    }

    /// <summary>逐端口攻破；每个端口后跟一步反追踪（理由见循环内注释）。</summary>
    private static void AppendPortSteps(List<HackStep> steps, Computer target, HackOptions options)
    {
        foreach (var port in HackEngine.CrackablePorts(target, options.ModPorts))
        {
            steps.Add(new HackStep(HackStepKind.OpenPort, target, port, HackEngine.CrackCommand(port)));

            // 每个端口后立刻清一次追踪（用户定的行为；此前只在每台末尾清一次）。
            //
            // 计时上两者都安全，不是缺陷修复：traceTime = max(10 - security, 3) * 15，
            // 最小 45 秒（Computer.cs:224），而单台的端口步总耗时是毫秒量级
            // （v1.34.0 起已无步进间隔）—— 每台清一次就已经比倒计时快好几个数量级。
            // 每端口一次的实际差别是「追踪状态在端口之间也归零」：演出 exe 的构造体
            // 会调 hostileActionTaken()（PacificPortExe.cs:26、SSHCrackExe.cs:90、
            // SMTPoverflowExe.cs:61 等），traceTime > 0 时即
            // os.traceTracker.start → os.warningFlash()（Computer.cs:300、
            // TraceTracker.cs:110）—— 每端口清一次，跑的过程中就不会出现常亮的
            // 追踪条与闪屏。
            //
            // 成本为零：KillTrace 不吃节流（见 DelayFor），且未激活时
            // HackEngine.KillTrace 直接返回 false、连一行输出都没有。
            //
            // 脚本模式不插这一步 —— 那份次序由玩家显式书写，killtrace 是脚本可写的
            // 动作（HackScript.cs:81），末尾也已有兜底，不该由插件改写脚本语义。
            steps.Add(new HackStep(HackStepKind.KillTrace, target, default, null));
        }
    }

    /// <summary>解防火墙 → 提权 → 白名单绕过 → 投放标记。</summary>
    private static void AppendEscalateSteps(List<HackStep> steps, Computer target)
    {
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
    }

    /// <summary>收尾：投放（可选）→ 清痕（可选）→ 断开（可选）→ 反追踪兜底。</summary>
    private static void AppendCleanupSteps(List<HackStep> steps, Computer target, HackOptions options)
    {
        if (options.UploadMarker)
        {
            steps.Add(new HackStep(HackStepKind.UploadMarker, target, default, null));
        }

        // 清痕排在断开**之前**是硬要求：断开本身会往目标 /log 写一条
        // "<玩家IP> Disconnected"（Computer.disconnecting，Computer.cs:722-727），
        // 那条也含玩家 IP，排在断开后就得再清一遍。
        //
        // 不需要回显也不需要连接：清痕走
        // Computer.deleteFile(ipFrom, 名, folderPath)（HackEngine.WipeTraces），
        // 目标机与目录都由参数给定，不读 os.connectedComp / navigationPath ——
        // 这正是它从 DependsOnConnection 名单里退出来的原因。
        //
        // 这一步**不设开关**：留痕的代价是带 tracker="true" 的机器在断开时
        // 自动排一个脱机追踪（OS.handleDisconnection，OS.cs:944-960 →
        // TrackerCompleteSequence.cs:30-47），10~20 秒后计时归零端掉玩家。
        // 而 wipe 删的只是含玩家 IP 的条目，目标机自己的历史原样保留 ——
        // 没有需要玩家权衡的取舍。要留痕传 'keep'。
        if (options.WipeTraces)
        {
            steps.Add(new HackStep(HackStepKind.CleanLogs, target, default, null));
        }

        if (options.Disconnect)
        {
            steps.Add(new HackStep(HackStepKind.Disconnect, target, default, "dc"));
        }

        // 断开已让 TraceTracker 自己失效；这一步是确定性的兜底 ——
        // stay 模式（不断开）下它是唯一的止血点。走 stop()，零每帧开销。
        steps.Add(new HackStep(HackStepKind.KillTrace, target, default, null));
    }

    /// <summary>为被剔除的机器追加清痕步骤（<c>WipeTraces</c> 为真时）。</summary>
    private static void AppendSkippedSteps(
        List<HackStep> steps, IReadOnlyList<Computer> skipped, HackOptions options)
    {
        // 被剔除的机器照样清痕：它们此前进过、破过、侦察过，/log 里留着痕迹，
        // 「跳过入侵」不等于「放过证据」。排在全部正常步骤之后 —— 正常流程不会再碰
        // 这些机器，此刻清是终点动作，不会有新记录再追加进来。
        // 对没有痕迹的机器是幂等的：WipeTraces 返回空列表，不产生任何输出。
        if (options.WipeTraces)
        {
            foreach (var comp in skipped)
            {
                steps.Add(new HackStep(HackStepKind.CleanLogs, comp, default, null));
            }
        }
    }

    /// <summary>为玩家自己的机器追加清痕步骤（<c>WipeTraces</c> 为真时），必须排在全部步骤之后。</summary>
    private static void AppendSelfStep(List<HackStep> steps, OS os, HackOptions options)
    {
        // 玩家自己的机器也在同一口径内 —— 它的 /log 里同样有指向自己的条目
        // （"<目标IP> Disconnected" 之类），那就是「我的痕迹」。
        //
        // 机器不在 targets 里（ResolveTargets 显式跳过 os.thisComputer，
        // HackEngine.cs:445），也不在 skipped 里 —— 上面两处都够不着，必须单独追加。
        //
        // 排在全部步骤之后是刻意的：玩家的 /log 记的是「谁连过我」，入侵过程中
        // 每连一台都会往自己机器上写一条，提前清会被后续步骤重新写回来。
        if (options.WipeTraces)
        {
            steps.Add(new HackStep(HackStepKind.CleanLogs, os.thisComputer, default, null));
        }
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
            AppendScriptedAction(steps, target, action, emitted, connected, options);
        }

        // 清痕排在 KillTrace 之前，且**不设开关**（与内置次序同一口径，见 BuildSteps）。
        // 排在最后是刻意的：提权与投放都会往目标 /log 追加条目，早清等于白清。
        //
        // 即便脚本已 dc 也不受影响：WipeTraces 按 folderPath 直取目标 /log
        // （HackEngine.WipeTraces），不读 os.connectedComp，也不再回显那条 rm。
        //
        // 脚本没写 rm 时补上（开关为真才补）—— 留痕的代价是目标机在断开时自动排一个
        // 脱机追踪（TrackerCompleteSequence.cs:30-47）。脚本里写了 rm 就不补：
        // 那是玩家显式指定的位置，多清一次虽幂等，但会把清理点挪到玩家没写的地方。
        if (options.WipeTraces && !emitted.Contains(HackStepKind.CleanLogs))
        {
            steps.Add(new HackStep(HackStepKind.CleanLogs, target, default, null));
        }

        // 兜底反追踪：与内置次序同理，脚本没写也要有 —— 断开已让它失效，
        // 这一步覆盖「脚本以 stay 结尾」与「最后一步之后才被点燃」的窗口。
        if (emitted.Add(HackStepKind.KillTrace))
        {
            steps.Add(new HackStep(HackStepKind.KillTrace, target, default, null));
        }
    }

    /// <summary>
    /// 追加脚本里的单个动作：<c>connect</c> 已登记则跳过、<c>openPort</c> 逐端口展开，
    /// 其余动作重复出现只取首次（<c>cleanlogs</c> 例外，它幂等且可重复）。
    /// </summary>
    private static void AppendScriptedAction(
        List<HackStep> steps, Computer target, HackScript.Action action,
        HashSet<HackStepKind> emitted, bool connected, HackOptions options)
    {
        // connect 已被前置步骤登记过时（ConnectFirst 模式），脚本里再写就跳过。
        if (action.Kind == HackStepKind.Connect)
        {
            if (connected || !emitted.Add(HackStepKind.Connect))
            {
                return;
            }

            steps.Add(new HackStep(HackStepKind.Connect, target, default, "connect " + target.ip));
            return;
        }

        // OpenPort 可重复（逐端口展开）。CleanLogs 也可重复 —— 它对空 /log
        // 是幂等的（WipeTraces 返回空列表、不输出），而「证据必须消失」是硬承诺，
        // 让玩家写两次就多清一次比静默吞掉第二个更符合预期。
        // 其余动作改的是游戏状态，重复出现只取首次。
        if (action.Kind is not (HackStepKind.OpenPort or HackStepKind.CleanLogs) &&
            !emitted.Add(action.Kind))
        {
            return;
        }

        if (action.Kind == HackStepKind.OpenPort)
        {
            foreach (var step in ExpandPorts(target, action.Port, options))
            {
                steps.Add(step);
            }

            return;
        }

        steps.Add(new HackStep(action.Kind, target, default, CommandFor(action.Kind)));
    }

    /// <summary>
    /// 展开 <c>openPort</c> 动作。<paramref name="portNumber"/> 为 0 = 该目标上全部
    /// 有原生破解程序的端口；否则只挑匹配的那一个（按显示端口号比对，与玩家在终端里
    /// 敲的 <c>sshcrack 22</c> 同一个数）。指定的端口不存在或不含破解程序时展开为空 ——
    /// 不臆造步骤。
    /// </summary>
    private static IEnumerable<HackStep> ExpandPorts(Computer target, int portNumber, HackOptions options)
    {
        foreach (var port in HackEngine.CrackablePorts(target, options.ModPorts))
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

        _ => null,
    };
}

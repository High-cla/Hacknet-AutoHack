namespace AutoHack;

using Hacknet;
using Pathfinder.Port;
using Pathfinder.Util;
/// <summary>
/// 入侵决策逻辑：目标解析、端口状态、提权判定。全部基于 Pathfinder 框架 API。
/// </summary>
internal static partial class HackEngine
{

    /// <summary>
    /// 是否是 EOS 设备（<see cref="Computer.EOS"/> = 5，Computer.cs:21）。
    ///
    /// EOS 设备在端口上是个死局：<c>portsNeededForCrack = 2</c> 而端口表恰好也只有
    /// 2 个（22 + 3659，ContentLoader.cs:825/:828），而 porthack 的门禁是<b>严格大于</b>
    /// （<c>OpenPortCount &gt; portsNeededForCrack</c>，OS.cs:1896-1942）——
    /// 2 &gt; 2 恒假，破满端口也提不了权。<b>这是游戏刻意的</b>：EOS 从来不是靠破端口进的，
    /// 而是靠全系统一的固定密码（见 <see cref="TryLogin"/> 与游戏自带的
    /// Content/Post/eosScannerMail.txt:7-9）。
    /// </summary>
    internal static bool IsEosDevice(Computer comp)
        => comp != null && comp.type == Computer.EOS;

    /// <summary>
    /// 用已知账号登录目标机 —— 走游戏自身的 <c>Computer.login</c>（Computer.cs:849-865）。
    ///
    /// 这不是「绕过」，是更原生的路径：<c>login</c> 在用户名为 admin 且密码匹配时
    /// <b>内部直接调 <c>giveAdmin</c></b>（Computer.cs:851-855），与 porthack 终点等价，
    /// 但不需要破解任何端口，也<b>不会触发追踪</b>（<c>hostileActionTaken</c> 不在该路径上）。
    ///
    /// 候选顺序：admin 账号优先（其密码就是 <c>adminPass</c>，两者同源，
    /// 见 Computer.cs:122-123 与存档读回 Computer.cs:1117-1120）；
    /// 其余账号仅当 <c>known</c> 为真时才尝试 —— 那才是「玩家已知」的语义。
    ///
    /// 刻意不用 <c>Programs.login</c>：它是<b>交互式</b>的，用
    /// <c>while (commandsRun() == num) Thread.Sleep(4)</c> 轮询等玩家敲用户名与密码
    /// （Programs.cs:404-448），在游戏线程调用即卡死。
    /// </summary>
    /// <param name="credential">回传命中的账号名，供终端说明用了哪组凭据。</param>
    /// <returns>是否登录成功（成功即已提权）。</returns>
    internal static bool TryLogin(Computer comp, out string credential)
    {
        credential = null;
        if (comp?.users == null)
        {
            return false;
        }

        // ① 原生「已知」标记的账号优先 —— 这才是玩家在剧情/邮件里真知道的那组密码。
        foreach (var user in comp.users)
        {
            if (!user.known || user.name == null || user.pass == null)
            {
                continue;
            }

            if (comp.login(user.name, user.pass) == 1)
            {
                credential = user.name;
                return true;
            }
        }

        // ② 目标的 admin 账号：密码存在公开字段 adminPass，与 users[0].pass 同源
        //    （Computer.cs:122-123 构造，存档读回 Computer.cs:1117-1120）。
        //    注意 login 内部对 admin 是「用户名 admin + 密码等于 adminPass 即 giveAdmin」，
        //    不走 users 表比对。
        if (comp.adminPass != null && comp.login("admin", comp.adminPass) == 1)
        {
            credential = "admin";
            return true;
        }

        return false;
    }

    /// <summary>
    /// 该机器是否已被玩家拿下（肉鸡）。
    /// 原生的所有权标记只有 <c>Computer.adminIP</c>（<c>giveAdmin(ipFrom)</c> 写入，
    /// 且被 <c>SaveWriter</c>/<c>SaveLoader</c> 持久化）；Pathfinder 没有更上层的封装。
    /// 注意 <c>Computer.admin</c> 是任务用的 <c>Administrator</c> 行为对象，不是所有权标记。
    /// </summary>
    internal static bool IsOwned(Computer comp, OS os)
        => comp != null && os?.thisComputer != null && comp.adminIP == os.thisComputer.ip;

    /// <summary>
    /// 目标解析结果。<see cref="Targets"/> 是要入侵的机器；<see cref="Skipped"/>
    /// 是被剔除、但仍会被清痕的机器 —— 它们的 /log 里留着此前侦察与入侵的痕迹，
    /// 不因「跳过入侵」而豁免清痕。
    ///
    /// 曾还有一个 <c>SkippedHopeless</c>（因端口表凑不够提权门槛而跳过）——
    /// 那道剔除已删除，字段随之作废。理由见 <see cref="ResolveTargets"/>。
    /// </summary>
    internal readonly record struct TargetPlan(
        List<Computer> Targets,
        List<Computer> Skipped,
        int SkippedOwned);

    /// <summary>
    /// 解析目标集合。目标串走框架查找表。
    ///
    /// 两道过滤：无条件跳过 <c>null</c> / <c>disabled</c> / 玩家自己；全网扫描时
    /// 按 <see cref="HackOptions.SkipOwned"/> 跳过已控机器。其余<b>全部</b>入侵 ——
    /// 包括端口表凑不够提权门槛的那些（它们由 <see cref="ForceEscalate"/> 拿下）。
    /// </summary>
    internal static TargetPlan ResolveTargets(OS os, HackOptions options)
    {
        if (os == null)
        {
            return new TargetPlan([], [], 0);
        }

        var pool = options.Scope switch
        {
            HackScope.Connected => ConnectedPool(os),
            HackScope.Network => options.AllNodes ? ConnectableComputers(os) : ReachableComputers(os),
            _ => options.Targets
                    .Select(id => ComputerLookup.Find(id))
                    .Where(comp => comp != null)
                    .ToArray(),
        };

        // 已控剔除**只对全网扫描生效**。「当前节点」与显式点名是刻意选择，一律尊重 ——
        // 玩家连上某台机器再点 START，就是明确要打它，哪怕它已经是自己的肉鸡。
        // 白名单回退那条路同理（见 ConnectedPool）。
        var sweep = options.Scope == HackScope.Network;
        var skipOwned = options.SkipOwned && sweep;

        var result = new List<Computer>(pool.Length);
        var skipped = new List<Computer>();
        var skippedOwned = 0;

        // 用哈希集去重，而不是 List.Contains：显式目标串可能重复点名，
        // 而全网遍历的池本身已去重，两者都需要 O(1) 判重。
        var seen = new HashSet<Computer>(pool.Length);

        foreach (var comp in pool)
        {
            // 无条件剔除：拿不到的对象、disabled、以及玩家自己。
            if (comp == null || comp.disabled || ReferenceEquals(comp, os.thisComputer))
            {
                continue;
            }

            // 已控（adminIP 已是玩家）的机器：全网扫描时按开关跳过。
            // 玩家此前打过谁不该决定这一轮的结果，故这是**显式开关**（面板 skip owned
            // 复选框 / 命令行 redo 的反面），而不是一条隐式的「智能」判据。
            if (skipOwned && IsOwned(comp, os))   // skipOwned 已含 sweep 条件，见上
            {
                skippedOwned++;
                skipped.Add(comp);
                continue;
            }

            // **这里曾有第二道剔除：「端口表容量 ≤ 提权门槛的不打」。v1.33.1 已删除。**
            // 它把 22 台机器整个排除在目标池外，其中 9 台是游戏自带的防护机
            // （portsToCrack=9999998，实测 EnTech 中继服务器系列），另外 13 台是门槛
            // 2~8 而端口表只有 1~4 个的普通机器 —— 两类都是 porthack 判据恒假
            // （要求已攻破端口数**严格大于**门槛，OS.cs:1916），**破满端口也差 1~6 个**。
            // 把它们排除，等于「明明有一个能拿下的办法（强行提权），却装作没有」。
            // 现在它们进目标池，Escalate 步在门禁不过时走 ForceEscalate 拿下。
            if (seen.Add(comp))
            {
                result.Add(comp);
            }
        }

        return new TargetPlan(result, skipped, skippedOwned);
    }

    /// <summary>
    /// 「当前节点」的目标池：已连接的机器；没连上时反推**刚被拒的那台**。
    ///
    /// 为什么需要反推：地图上点节点走 <c>os.runCommand("connect " + ip)</c>
    /// （NetworkMap.cs:573），而白名单会拒这个连接 —— <c>Computer.connect</c>
    /// 调 <c>DisconnectTarget()</c> 后 return false（Computer.cs:383-388），
    /// <c>Programs.connect</c> 写完 "External Computer Refused Connection" 再把
    /// <c>os.connectedComp</c> 置 null（Programs.cs:313-322）。于是目标**既没连上、
    /// 也不进任何列表** —— 只看 <c>connectedComp</c> 的话，「当前节点」就无从知道
    /// 玩家点的是哪台，表现为「敲了 autohack 什么也没发生」（池空 ⇒ Total == 0）。
    ///
    /// 反推依据见 <see cref="RefusedWhitelist.LastRefused"/>。玩家是**显式**点的那个节点，
    /// 与 <c>here</c>、点名同级，故按「刻意选择」处理：不做 SkipOwned 剔除。
    /// </summary>
    private static Computer[] ConnectedPool(OS os)
        => os.connectedComp is { } connected
            ? [connected]
            : RefusedWhitelist.LastRefused(os) is { } refused ? [refused] : [];

    /// <summary>目标池为空时的补充说明 —— 面板与命令行两个入口共用，措辞只写一次。</summary>
    internal const string NoTargetHint =
        "[autohack]   'here' follows the session; nothing is connected - "
        + "name the node, or use 'allnodes'.";


    /// <summary>
    /// 工具的缺省目标集合 —— 与 <c>autohack run</c> 的 <c>allnodes</c> 口径一致：
    /// 缺省只作用于当前连接的节点，未连接时退到玩家自己的机器（工具在本地也讲得通，
    /// 例如解自己的 DEC 文件）；<c>allnodes</c> 时扫地图全表。
    ///
    /// 抽出来是因为 dec 与 mem 各写了一遍同一个三元式，加工具还会再抄一遍 ——
    /// 而「工具作用在哪些机器上」是必须处处一致的口径。
    /// </summary>
    internal static Computer[] ToolTargets(OS os, bool allNodes)
        => allNodes
            ? ConnectableComputers(os)
            : [os.connectedComp ?? os.thisComputer];
}

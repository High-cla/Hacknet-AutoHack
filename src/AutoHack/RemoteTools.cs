namespace AutoHack;

using Hacknet;

/// <summary>
/// 远程操作工具：对「当前目录」与「当前连接」直接动手 —— 整目录下载、整目录删除、
/// 把当前节点从网络图上摘掉。
///
/// 为什么不直接调游戏自身的 <c>Programs.scp</c> / <c>Programs.rm</c>：
/// 两者内部都有 <c>Thread.Sleep</c>（scp 每文件 250ms、进度点再 200ms×最多 20 次，
/// Programs.cs:629/697；rm 每文件 200ms×3~26，Programs.cs:1017），
/// 在游戏线程上同步跑会把整帧卡住 —— 与「不在游戏线程调 probe/login」同一条约束。
/// 这里只取它们的**语义**，动作一律走游戏自身的原语
/// （<c>Computer.canCopyFile</c> / <c>Computer.deleteFile</c>），不睡、不阻塞。
/// </summary>
internal static class RemoteTools
{
    /// <summary>
    /// 当前目录的三元组：宿主机器、目录本身、相对 <c>files.root</c> 的索引路径。
    ///
    /// 目录由 <c>Programs.getFolderFromNavigationPath</c>（Programs.cs:1749）解出，
    /// 与 <c>Computer.deleteFile</c> 内部用的是同一个函数（Computer.cs:523/549）
    /// —— 两者按构造一致，不会出现「报告的是 A 目录、删的是 B 目录」。
    ///
    /// 路径必须先**快照**：<c>Programs.disconnect</c> 会清空 <c>os.navigationPath</c>
    /// （Programs.cs:328），断开后再读就是空路径（= 根目录）。
    /// </summary>
    private static (Computer Comp, Folder Dir, List<int> Path) Current(OS os)
    {
        var comp = os.connectedComp ?? os.thisComputer;
        var path = new List<int>(os.navigationPath);
        var dir = Programs.getFolderFromNavigationPath(path, comp.files.root, os);
        return (comp, dir, path);
    }

    /// <summary>人类可读的当前目录（根目录显示为 "/"）。</summary>
    private static string Where(Computer comp, Folder dir)
        => ReferenceEquals(dir, comp.files.root) ? "/" : "/" + dir.name;

    /// <summary>
    /// 落点路由，逐条照抄 <c>Programs.scp</c>（Programs.cs:632-655）：
    /// .exe → /bin（落下即可 exe 运行）、.sys → /sys、'@' 开头（日志）→ /home/dl_logs、
    /// 其余 → /home。这是游戏自己的规则，改掉会让「下载的破解程序不能直接跑」。
    /// </summary>
    private static string Destination(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.EndsWith(".exe"))
        {
            return "bin";
        }

        if (lower.EndsWith(".sys"))
        {
            return "sys";
        }

        return name.StartsWith("@") ? "home/dl_logs" : "home";
    }

    /// <summary>
    /// 把当前目录下的全部文件下载到玩家自己的机器。等价于终端 <c>scp *</c>，
    /// 只是不睡：游戏版每文件间隔 250ms，几十个文件就是十几秒的卡帧。
    /// </summary>
    internal static void Pull(OS os)
    {
        if (os.connectedComp == null)
        {
            os.write("[autohack] pull: not connected - nothing remote to pull from.");
            return;
        }

        var (comp, dir, _) = Current(os);
        var where = Where(comp, dir);

        if (dir.files.Count == 0)
        {
            os.write("[autohack] pull: " + comp.name + " :: " + where + " is empty.");
            return;
        }

        // 快照来源列表：落点在玩家自己的文件系统（与来源不是同一棵树），
        // 但「遍历中不改动被遍历的 List」是本仓库的既有约束，不靠巧合成立。
        var sources = new List<FileEntry>(dir.files);

        var copied = 0;
        var denied = 0;

        foreach (var file in sources)
        {
            if (file == null || string.IsNullOrWhiteSpace(file.name))
            {
                continue;
            }

            // 权限门禁交回游戏（Computer.cs:496-506）：它同时写审计日志
            // "FileCopied: by <ip> - file:<name>" 并发联机同步消息，与 scp 一致。
            if (!comp.canCopyFile(os.thisComputer.ip, file.name))
            {
                denied++;
                continue;
            }

            // 走游戏自身的路径解析（Computer.cs:1628），不手写建夹；
            // 重名规则复用 ToolFiles.Write（stem 即完整文件名、扩展名留空）。
            var dest = os.thisComputer.getFolderFromPath(Destination(file.name), createFoldersThatDontExist: true);
            ToolFiles.Write(dest, file.name, string.Empty, file.data);
            copied++;
        }

        var tail = denied > 0 ? ", " + denied + " denied (needs admin access)" : string.Empty;
        os.write("[autohack] pull: " + copied + " file(s) from " + comp.name + " :: " + where + " -> local home" + tail + ".");
    }

    /// <summary>
    /// 删除当前目录下的全部文件。等价于终端 <c>rm *</c>。
    /// 未连接时作用于玩家自己的机器（与 <c>rm</c> 的作用域规则一致）。
    /// </summary>
    internal static void Purge(OS os)
    {
        var (comp, dir, path) = Current(os);
        var where = Where(comp, dir);

        var before = dir.files.Count;
        if (before == 0)
        {
            os.write("[autohack] purge: " + comp.name + " :: " + where + " is already empty.");
            return;
        }

        // 走游戏自身的删除原语（Computer.cs:508）。"*" 分支先快照文件名再逐个递归，
        // 故遍历中删除不会漏项；权限门禁与联机同步都交回游戏。
        comp.deleteFile(os.thisComputer.ip, "*", path);

        // 如实复核，不看返回值 —— "*" 分支是 flag2 &= deleteFile(...) 逐个递归后返回
        // flag2，路径解析偏了它会去删别的文件夹并照样返回 true，权限被拒时只是静默
        // false。**不**像 ClearLogs 那样强行清 List：那是「证据必须消失」的硬承诺，
        // 而这里是用户显式发起的删除，权限门禁是游戏自己的访问控制，不该被绕过。
        var after = dir.files.Count;
        var removed = before - after;

        var tail = after > 0 ? " - " + after + " denied (needs admin access)" : string.Empty;
        os.write("[autohack] purge: " + removed + " of " + before + " file(s) removed from " + comp.name + " :: " + where + tail + ".");
    }

    /// <summary>
    /// 断开当前连接，并把该节点从网络图上摘掉。
    ///
    /// 「摘节点」没有专门的 API，游戏自己的做法就是摘 <c>visibleNodes</c> 里的下标。
    /// 权威实现是官方 Action <c>&lt;HideNode&gt;</c> → <c>SAHideNode.Trigger</c>：
    /// <code>
    /// do { oS.netMap.visibleNodes.Remove(oS.netMap.nodes.IndexOf(computer)); }
    /// while (oS.netMap.visibleNodes.Contains(oS.netMap.nodes.IndexOf(computer)));
    /// </code>
    /// 循环而非单次 Remove —— <c>visibleNodes</c> 里可能有重复下标
    /// （<c>discoverNode</c> 自带判重，但存档载入与 <c>DLC1SessionUpgrader</c>
    /// 等路径会直接 Add），单次删只去掉第一个。此处照抄该循环。
    /// 官方另有三处同样写法，且都在断开**之后**做：
    /// <c>AircraftDaemon.cs:229-234</c>、<c>EndingSequenceModule.cs:244-245</c>、
    /// <c>ExtensionSequencerExe.cs:208-210</c>。
    ///
    /// 边界（如实记录，不假装更强）：节点仍在 <c>map.nodes</c> 里，只是不在
    /// <c>visibleNodes</c>。默认口径 <c>ReachableComputers</c> 以 visibleNodes 为种子
    /// 沿 links 展开（HackEngine.cs:490-498），故摘掉后**默认扫描再也够不着它**；
    /// 但显式 <c>allnodes</c> 走 <c>ConnectableComputers</c>，那是刻意的全表口径
    /// （HackEngine.cs:600-602 记录了这个设计），仍会看到它。
    ///
    /// 不设 <c>comp.disabled</c>：<c>NetworkMap.Update</c> 每帧对 disabled 节点调
    /// <c>bootupTick</c>（NetworkMap.cs:126-131），而 <c>bootTimer</c> 缺省 0，
    /// 于是下一帧就 <c>disabled = false</c>（Computer.cs:311-317）——
    /// 那字段是给 crash/reboot 用的临时态，拿来当「已删除」是假的。
    /// </summary>
    internal static void Drop(OS os)
    {
        var comp = os.connectedComp;
        if (comp == null)
        {
            os.write("[autohack] drop: not connected to any node.");
            return;
        }

        var map = os.netMap;
        if (map?.nodes == null || map.visibleNodes == null)
        {
            return;
        }

        var index = map.nodes.IndexOf(comp);
        var name = comp.name;
        var ip = comp.ip;

        // 先断开：连着的时候摘节点，游戏自己的状态机（追踪、延迟反扑）还在跑。
        HackEngine.SilentDisconnect(os);

        if (index < 0)
        {
            os.write("[autohack] drop: " + name + " is not on the network map.");
            return;
        }

        while (map.visibleNodes.Remove(index))
        {
        }

        os.write("[autohack] drop: " + name + " (" + ip + ") removed from the map.");
    }
}

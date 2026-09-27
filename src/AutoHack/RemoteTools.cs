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
    /// 「当前目录」以游戏自己的 <see cref="Programs.getCurrentFolder"/> 为**唯一权威**
    /// （Programs.cs:1531-1534）—— <c>ls</c>、终端提示符、<c>rm</c> 的默认作用域都用它。
    ///
    /// 早先这里自己用 <c>getFolderFromNavigationPath(os.navigationPath, ...)</c> 又走了
    /// 一遍路径，于是游戏里存在**两套下钻逻辑**（<c>getFolderAtDepth</c> 与
    /// <c>getFolderFromNavigationPath</c>）。两者对正常路径等价，但对越界路径的处理不同：
    /// 前者静默跳过该层，后者写 "Invalid Path" 并停在上一层。于是可能出现
    /// 「<c>ls</c> 显示 A 目录、本工具清 B 目录」，且**两边都不报错** —— 玩家无从察觉。
    ///
    /// 现在路径改为**从目录对象反推**（<see cref="PathTo"/>），不再二次解析。
    /// 这样 <c>Computer.deleteFile</c> 内部拿这个路径再解一次，必然回到同一个对象
    /// —— 「报告的是 A、删的是 B」在构造上不可能发生，也不需要再依赖
    /// <c>navigationPath</c> 的快照语义（<c>Programs.disconnect</c> 会清空它，
    /// Programs.cs:328）。
    /// </summary>
    private static (Computer Comp, Folder Dir, List<int> Path) Current(OS os)
    {
        var comp = os.connectedComp ?? os.thisComputer;
        var dir = Programs.getCurrentFolder(os);
        return (comp, dir, PathTo(comp.files.root, dir));
    }

    /// <summary>从根出发的索引链；<paramref name="target"/> 不在该树下时返回空（= 根）。</summary>
    private static List<int> PathTo(Folder root, Folder target)
    {
        var path = new List<int>();
        return ReferenceEquals(root, target) || Walk(root, target, path) ? path : new List<int>();
    }

    /// <summary>深度优先找 <paramref name="target"/>，边走边记下标链。文件夹树很浅，无需迭代化。</summary>
    private static bool Walk(Folder folder, Folder target, List<int> path)
    {
        for (var i = 0; i < folder.folders.Count; i++)
        {
            var child = folder.folders[i];
            path.Add(i);

            if (ReferenceEquals(child, target) || Walk(child, target, path))
            {
                return true;
            }

            path.RemoveAt(path.Count - 1);
        }

        return false;
    }

    /// <summary>
    /// 人类可读的当前目录**全路径**（根目录显示为 "/"）。
    ///
    /// 显示全路径而非末层名（原为 <c>"/" + dir.name</c>）：末层名分不清
    /// <c>/home/dl_logs</c> 与 <c>/stash/dl_logs</c>，一旦解析偏了，
    /// 回显看着正常而删的是别处 —— 玩家没有任何办法察觉。
    /// </summary>
    private static string Where(Computer comp, List<int> path)
    {
        if (path.Count == 0)
        {
            return "/";
        }

        var parts = new List<string>(path.Count);
        var folder = comp.files.root;

        foreach (var index in path)
        {
            if (index < 0 || index >= folder.folders.Count)
            {
                parts.Add("?");
                continue;
            }

            folder = folder.folders[index];
            parts.Add(folder.name);
        }

        return "/" + string.Join("/", parts);
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

        var (comp, dir, path) = Current(os);
        var where = Where(comp, path);

        if (dir.files.Count == 0)
        {
            os.write("[autohack] pull: " + comp.name + " :: " + where + " is empty.");
            return;
        }

        // 快照来源列表：落点在玩家自己的文件系统（与来源不是同一棵树），
        // 但「遍历中不改动被遍历的 List」是本仓库的既有约束，不靠巧合成立。
        var sources = new List<FileEntry>(dir.files);

        var misc = ToolFiles.Misc(os);
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

            // 落点固定 home/misc，不按扩展名分流。重名规则复用 ToolFiles.Write
            // （stem 即完整文件名、扩展名留空）。
            ToolFiles.Write(misc, file.name, string.Empty, file.data);
            copied++;
        }

        var tail = denied > 0 ? ", " + denied + " denied (needs admin access)" : string.Empty;
        os.write("[autohack] pull: " + copied + " file(s) from " + comp.name + " :: " + where
                 + " -> local /home/misc" + tail + ".");
    }

    /// <summary>
    /// 删除当前目录下的全部文件。等价于终端 <c>rm *</c>，但不睡
    /// （游戏版每文件 200ms×3~26，Programs.cs:1017）。
    /// 未连接时作用于玩家自己的机器（与 <c>rm</c> 的作用域规则一致）。
    ///
    /// 动作与清痕共用 <see cref="HackEngine.RemoveFiles"/>：同一个「删光一个目录」
    /// 的动作只该有一份实现。本方法此前自带一份、且没有兜底，于是
    /// <c>deleteFile</c> 权限门禁一拒就静默什么都不删，表现为「按了没反应」。
    /// </summary>
    /// <summary>未连接时把「作用于本机」写进回显，避免玩家以为删的是目标。</summary>
    private static string SelfTag(bool onSelf)
        => onSelf ? " (local machine - not connected)" : string.Empty;

    internal static void Purge(OS os)
    {
        var (comp, dir, path) = Current(os);
        var where = Where(comp, path);

        // 未连接时 Current 会退到玩家自己的机器 —— 这与终端 rm 的作用域规则一致
        // （rm 也取 os.connectedComp ?? os.thisComputer），但玩家很容易以为它是
        // 冲着目标去的。不改变行为，只把它写进回显，不留给玩家猜。
        var onSelf = os.connectedComp == null;

        // 子夹数一并报出：本工具按约定**不删文件夹**，所以「夹子还在」是正常结果，
        // 不是没生效。把它说在前面，免得玩家对着一个空夹反复试。
        var folders = dir.folders.Count;

        if (dir.files.Count == 0)
        {
            os.write("[autohack] purge: " + comp.name + SelfTag(onSelf) + " :: " + where
                     + " has no file (" + folders + " folder(s) left - folders are never removed).");
            return;
        }

        var before = dir.files.Count;
        var removed = HackEngine.RemoveFiles(comp, os.thisComputer.ip, dir, path);
        var left = dir.files.Count;

        // 如实复核，不看返回值：RemoveFiles 的兜底保证「文件必被清空」，
        // 真剩下了就是它没做到 —— 不该被一行乐观的回显盖过去。
        var tail = left > 0 ? " - " + left + " still there (unexpected)" : string.Empty;
        os.write("[autohack] purge: " + removed.Count + " of " + before + " file(s) removed from "
                 + comp.name + SelfTag(onSelf) + " :: " + where + tail
                 + (folders > 0 ? " (" + folders + " folder(s) left)" : string.Empty) + ".");
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

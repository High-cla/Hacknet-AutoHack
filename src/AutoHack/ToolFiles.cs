namespace AutoHack;

using Hacknet;

/// <summary>
/// 工具的落盘公共部分：统一落点与重名处理。
/// 抽出来是因为 DEC 批量落盘与内存转储走的是同一套规则，
/// 分散在两处迟早会漂移。
///
/// 两处落点，按产物性质分：
/// <list type="bullet">
/// <item><c>/home/MemDumps</c> —— 工具**生成**的可读文件（DEC 解密产物、内存转储）。</item>
/// <item><c>/home/misc</c> —— 从目标机**拉取**回来的文件（原样保存，见 <c>RemoteTools.Pull</c>）。</item>
/// </list>
/// 都是玩家自己的机器，且都在 <c>home</c> 下，故不会出现「两处找东西」。
/// 拉取单独一个夹是为了让「下载回来的东西」一眼可辨 —— 它们与工具产物性质不同。
/// </summary>
internal static class ToolFiles
{
    /// <summary>
    /// 玩家机 /home/MemDumps，缺失则建（与 MemoryDumpDownloader.cs:92-98 同一落点）。
    /// 内存转储导出与 DEC 解密产物共用此落点 —— 两者都是「工具产出的可读文件」。
    /// </summary>
    internal static Folder MemDumps(OS os)
        => os.thisComputer.getFolderFromPath("home/MemDumps", createFoldersThatDontExist: true);

    /// <summary>
    /// 玩家机 /home/misc —— <c>pull</c> 拉取回来的文件的唯一落点。
    ///
    /// 这个夹**游戏自己就会建**：<c>OS.LoadContent</c> 给玩家机建 home 时一并加了
    /// <c>stash</c> 与 <c>misc</c>（OS.cs:386-388），存档里也持久化
    /// （官方测试存档 <c>Content/Tests/DLCTests/save_preDLC.xml:18-22</c> 里
    /// <c>home</c> 下正是 <c>stash</c> + <c>misc</c>）。故 <c>createFoldersThatDontExist</c>
    /// 只是兜底：老存档或非常规路径下缺了就补一个，与 <see cref="MemDumps"/> 同一写法。
    ///
    /// 拉取不再按扩展名分流到 /bin、/sys、/home/dl_logs（那是 <c>Programs.scp</c> 的规则，
    /// Programs.cs:637-657），因为分流会让一次下载散落在三四个夹里，
    /// 而 <c>dl_logs</c> 还得现建 —— 游戏没有任何删除文件夹的入口，
    /// mod 建出来的夹玩家永远清不掉（见 <c>docs/RESEARCH.md</c> §28）。
    /// </summary>
    internal static Folder Misc(OS os)
        => os.thisComputer.getFolderFromPath("home/misc", createFoldersThatDontExist: true);

    /// <summary>
    /// 递归收集符合条件的文件。DEC 批量与内存扫描是同一套遍历，
    /// 差异只有「认哪些文件」与「跳哪些目录」，故抽成一处。
    /// </summary>
    internal static void Collect(Folder folder, List<FileEntry> into, Func<string, bool> match, string[] skipFolders)
    {
        if (folder == null)
        {
            return;
        }

        foreach (var file in folder.files)
        {
            if (match(file.data))
            {
                into.Add(file);
            }
        }

        foreach (var child in folder.folders)
        {
            if (skipFolders != null && Array.IndexOf(skipFolders, child.name) >= 0)
            {
                continue;
            }

            Collect(child, into, match, skipFolders);
        }
    }

    /// <summary>写进目录，返回实际文件名（重名自动加序号）。</summary>
    internal static string Write(Folder folder, string stem, string extension, string content)
    {
        var name = Utils.GetNonRepeatingFilename(stem, extension, folder);
        folder.files.Add(new FileEntry(content, name));
        return name;
    }

    /// <summary>去掉扩展名得到文件名主干；后缀不符则原样返回。</summary>
    internal static string Stem(string name, string extension)
        => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? name.Substring(0, name.Length - extension.Length)
            : name;

    /// <summary>把任意标签洗成可当文件名用的串（走游戏自身的渲染清洗，再抹掉路径分隔符）。</summary>
    internal static string SafeStem(string label)
        => Utils.CleanStringToRenderable(label).Replace('/', '_').Replace('\\', '_');
}

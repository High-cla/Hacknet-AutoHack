namespace AutoHack;

using Hacknet;

/// <summary>
/// 工具的落盘公共部分：统一落点与重名处理。
/// 抽出来是因为 DEC 批量落盘与内存转储走的是同一套规则，
/// 分散在两处迟早会漂移。
///
/// 落点只有一处：玩家 /home/MemDumps —— 全部工具的产物都是「可读文件」，
/// 分开落点会让玩家在两处找东西。
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

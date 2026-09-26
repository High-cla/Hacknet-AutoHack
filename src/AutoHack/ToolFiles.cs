namespace AutoHack;

using Hacknet;

/// <summary>
/// 工具的落盘公共部分：统一落点与重名处理。
/// 抽出来是因为 DEC 批量落盘与内存转储导出走的是同一套规则，
/// 分散在两处迟早会漂移。
/// </summary>
internal static class ToolFiles
{
    /// <summary>玩家机 /home，缺失则建。</summary>
    internal static Folder Home(OS os)
    {
        var root = os.thisComputer.files.root;
        var home = root.searchForFolder("home");
        if (home == null)
        {
            home = new Folder("home");
            root.folders.Add(home);
        }

        return home;
    }

    /// <summary>玩家机 /home/MemDumps，缺失则建（与 MemoryDumpDownloader.cs:92-98 同一落点）。</summary>
    internal static Folder MemDumps(OS os)
    {
        var home = Home(os);
        var dumps = home.searchForFolder("MemDumps");
        if (dumps == null)
        {
            dumps = new Folder("MemDumps");
            home.folders.Add(dumps);
        }

        return dumps;
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

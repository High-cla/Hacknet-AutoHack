namespace AutoHack;

using Hacknet;

/// <summary>
/// 内存转储工具：查看 / 导出本机 MemoryContents，并识别节点上的 .mem 转储。
///
/// 全部走游戏自身的 <see cref="MemoryContents"/> —— 紧凑格式、编码、还原都是游戏
/// 已有的往返对（<c>GetCompactSaveString</c> / <c>ReExpandSaveString</c> /
/// <c>GetEncodedFileString</c> / <c>GetMemoryFromEncodedFileString</c>），
/// 本工具只负责把它们接到终端与玩家 /home/MemDumps。
///
/// 已知游戏缺陷：<c>MemoryContents.GetSaveString()</c> 的 FileFragments 分支
/// 遍历的是 <c>CommandsRun.Count</c>（MemoryContents.cs:48），当 FileFragments
/// 比 CommandsRun 长时会 IndexOutOfRangeException。查看/导出都要兜住它，
/// 否则一个坏存档会把面板绘制线程带崩。
/// </summary>
internal static class MemTools
{
    private const string Extension = ".mem";

    /// <summary>查看时最多打印的行数；超出部分只报数量，完整内容走导出。</summary>
    private const int MaxViewLines = 60;

    internal static void Run(OS os, bool allNodes)
    {
        View(os);
        Export(os);
        Scan(os, allNodes);
    }

    /// <summary>查看：打印本机内存的紧凑格式。</summary>
    private static void View(OS os)
    {
        var memory = os.thisComputer.Memory;
        if (memory == null)
        {
            os.write("[autohack] mem: this machine has no memory dump (<Memory />).");
            return;
        }

        string compact;
        try
        {
            compact = memory.GetCompactSaveString();
        }
        catch (IndexOutOfRangeException)
        {
            os.write("[autohack] mem: memory is malformed (game bug: FileFragments vs CommandsRun) - cannot render.");
            return;
        }

        os.write("[autohack] mem: " + compact.Length + " char(s) compact - "
                 + memory.DataBlocks.Count + " block(s), "
                 + memory.CommandsRun.Count + " command(s), "
                 + memory.FileFragments.Count + " fragment(s), "
                 + memory.Images.Count + " image(s).");

        // 紧凑串动辄上万字符，全灌进终端既刷屏又拖慢绘制。截断并如实报出省略量，
        // 完整内容仍可经导出（home/MemDumps）拿到。
        var lines = compact.Split(Utils.robustNewlineDelim, StringSplitOptions.None);
        var shown = Math.Min(lines.Length, MaxViewLines);
        for (var i = 0; i < shown; i++)
        {
            os.write(lines[i]);
        }

        if (lines.Length > shown)
        {
            os.write("[autohack] mem: ... " + (lines.Length - shown) + " more line(s) - use the export below for the full dump.");
        }
    }

    /// <summary>导出：编码后写进 /home/MemDumps，并当场做一次往返比对。</summary>
    private static void Export(OS os)
    {
        var memory = os.thisComputer.Memory;
        if (memory == null)
        {
            return;
        }

        string encoded;
        string before;
        try
        {
            before = memory.GetCompactSaveString();
            encoded = memory.GetEncodedFileString();
        }
        catch (IndexOutOfRangeException)
        {
            os.write("[autohack] mem: export skipped - memory is malformed (game bug).");
            return;
        }

        var folder = ToolFiles.MemDumps(os);
        var stem = ToolFiles.SafeStem(os.thisComputer.name) + "_dump";
        var name = ToolFiles.Write(folder, stem, Extension, encoded);

        var verdict = "round-trip UNVERIFIED";
        try
        {
            var restored = MemoryContents.GetMemoryFromEncodedFileString(encoded);
            verdict = restored.GetCompactSaveString() == before ? "round-trip OK" : "round-trip MISMATCH";
        }
        catch (Exception ex) when (ex is FormatException or NullReferenceException or IndexOutOfRangeException or ArgumentException)
        {
            verdict = "round-trip FAILED (" + ex.GetType().Name + ")";
        }

        os.write("[autohack] mem: exported home/MemDumps/" + name + " (" + encoded.Length + " chars, " + verdict + ").");
    }

    /// <summary>扫描：按游戏自身的 FileHeader 识别 .mem，解出内嵌内容，含 DEC 则继续解。</summary>
    private static void Scan(OS os, bool allNodes)
    {
        var targets = allNodes
            ? HackEngine.ConnectableComputers(os)
            : new[] { os.connectedComp ?? os.thisComputer };

        var candidates = new List<FileEntry>();
        foreach (var target in targets)
        {
            Collect(target.files.root, candidates);
        }

        if (candidates.Count == 0)
        {
            os.write("[autohack] mem: no memory dump file in " + targets.Length + " node(s).");
            return;
        }

        var home = ToolFiles.Home(os);
        var decoded = 0;
        var withDec = 0;

        foreach (var file in candidates)
        {
            MemoryContents memory;
            try
            {
                memory = MemoryContents.GetMemoryFromEncodedFileString(file.data);
            }
            catch (Exception ex) when (ex is FormatException or NullReferenceException or ArgumentException)
            {
                os.write("[autohack] mem: " + file.name + " - not a readable dump (" + ex.GetType().Name + ").");
                continue;
            }

            string compact;
            try
            {
                compact = memory.GetCompactSaveString();
            }
            catch (IndexOutOfRangeException)
            {
                os.write("[autohack] mem: " + file.name + " - decoded but malformed (game bug).");
                continue;
            }

            decoded++;
            var stem = ToolFiles.SafeStem(file.name);
            var written = ToolFiles.Write(home, stem, ".txt", compact);
            os.write("[autohack] mem: " + file.name + " -> home/" + written + " (" + compact.Length + " chars).");

            // 转储正文里若嵌着 DEC 层，复用 DEC 的多层递归，不另写一套。
            // net472 的 string.Contains 没有 StringComparison 重载，用 IndexOf。
            if (compact.IndexOf(DecTools.Marker, StringComparison.Ordinal) < 0)
            {
                continue;
            }

            var result = DecTools.DecryptAll(compact);
            if (!result.Ok)
            {
                os.write("[autohack] mem: " + file.name + " - inner DEC not resolvable.");
                continue;
            }

            withDec++;
            var inner = ToolFiles.Write(home, stem + "_dec", ".txt", result.Content);
            os.write("[autohack] mem: " + file.name + " -> home/" + inner
                     + " (inner DEC, " + result.Passcodes.Count + " layer(s)).");
        }

        os.write("[autohack] mem: " + decoded + "/" + candidates.Count + " dump(s) decoded, "
                 + withDec + " carried an inner DEC layer.");
    }

    /// <summary>收集按游戏 FileHeader 识别出的内存转储（含 .mem 之外的扩展名）。</summary>
    private static void Collect(Folder folder, List<FileEntry> into)
    {
        if (folder == null)
        {
            return;
        }

        foreach (var file in folder.files)
        {
            if (file.data != null && file.data.StartsWith(MemoryContents.FileHeader, StringComparison.Ordinal))
            {
                into.Add(file);
            }
        }

        foreach (var child in folder.folders)
        {
            Collect(child, into);
        }
    }
}

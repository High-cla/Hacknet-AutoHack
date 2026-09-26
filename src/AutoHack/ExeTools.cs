namespace AutoHack;

using Hacknet;

/// <summary>
/// 程序补全：把游戏已经生成好的破解程序补进玩家 /bin。
///
/// exe 二进制不是我们造的 —— PortExploits.populate()（PortExploits.cs:38）在启动时
/// 就用固定的 MSRandom(17021990) 把 crackExeData[port] 全部算好了（:47-56）。
/// 本工具只做两件事：从 cracks 表取名字、用 searchForFile 判重后落盘。
/// 不引入任何自写随机数生成器 —— 那只会和游戏的表产生不一致。
/// </summary>
internal static class ExeTools
{
    internal static void Run(OS os)
    {
        // populate() 是幂等的（整表重建），但没必要每次调用都扰动 Utils.random；
        // 表为空说明游戏还没初始化过，这时才补一次。
        if (PortExploits.cracks == null)
        {
            PortExploits.populate();
        }

        var total = PortExploits.cracks.Count;

        // 数据源核对：先证明表本身可用，再谈补全。缺数据的 port 如实报出来。
        //
        // 判据只能是「非空」—— 不要拿长度做门槛。PortExploits.EXE_FILE_LENGTH(500)
        // 是 generateBinaryString 的**请求**长度，不是产物长度：generateBinaryString
        // 开 byte[length/8] 即 62 字节（Computer.cs:1580），而 Convert.ToString(b,2)
        // 不补前导零（:1585），每字节出 1~8 位，实测产物约 445 字符。
        // 拿 500 当门槛会把 37 个程序全判成「无数据」—— 曾经的 added 0 就是这么来的。
        var usable = 0;
        foreach (var port in PortExploits.cracks.Keys)
        {
            if (PortExploits.crackExeData.TryGetValue(port, out var probe)
                && !string.IsNullOrWhiteSpace(probe))
            {
                usable++;
            }
        }

        os.write("[autohack] exes: table check - " + usable + "/" + total + " entr(ies) carry exe data.");

        // 走游戏自身的路径解析（Computer.cs:1628），不手写建夹。
        var bin = os.thisComputer.getFolderFromPath("bin", createFoldersThatDontExist: true);

        var added = 0;
        var skipped = 0;

        foreach (var pair in PortExploits.cracks)
        {
            var name = pair.Value;

            if (bin.searchForFile(name) != null)
            {
                skipped++;
                continue;
            }

            if (!PortExploits.crackExeData.TryGetValue(pair.Key, out var data)
                || string.IsNullOrWhiteSpace(data))
            {
                skipped++;
                os.write("[autohack] exes: port " + pair.Key + " (" + name + ") has no usable exe data - skipped.");
                continue;
            }

            bin.files.Add(new FileEntry(data, name));
            added++;
        }

        os.write("[autohack] exes: added " + added + ", skipped " + skipped + ".");
    }
}

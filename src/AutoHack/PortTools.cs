namespace AutoHack;

using Hacknet;

/// <summary>
/// 端口清单：把当前节点（未连接时是本机）的端口表逐条打出来 ——
/// 协议名、显示端口、显示名、**原始端口号**，以及破解状态与「是不是模组端口」。
///
/// <b>补的是哪一个缺口</b>：终端里唯一能看到端口的地方是 <c>probe</c>，而它只打
/// <c>Port#: {PortNumber} - {DisplayName}</c>（见 <see cref="HackEngine.ProbeReport"/>
/// 与 Pathfinder 的 <c>Programs.probe</c> 替换），**没有协议名、没有原始端口号、没有状态**。
/// 而这三样恰恰是玩家判断「还剩几个端口没破」「`modports=` 该写哪个协议名」的全部依据 ——
/// 后者按**协议名**匹配（见 <see cref="ModPortPolicy"/>），此前只能靠反编译模组才知道。
///
/// <b>为什么显示端口与原始端口要并排打</b>：<c>unbreakable</c> 会把机器的端口号随机化，
/// 此后 <c>PortNumber</c>（显示端口）与 <c>Record.OriginalPortNumber</c> 不再相等，
/// 而 <c>Computer.openPort(int)</c> 走的是**原始端口**
/// （<c>ComputerExtensions.cs:184-197</c> 按 <c>OriginalPortNumber</c> 匹配）。
/// 只打一列会让人以为两者总是同一个数 —— 那正是模组端口曾经静默失效的原因。
///
/// <b>只读</b>：不改端口状态、不开端口、不破解。与 <c>scan</c> 同一取舍 ——
/// 查看存档里的既有状态不算入侵。
/// </summary>
internal static class PortTools
{
    internal static void Run(OS os)
    {
        // 与其它工具一致：作用于「当前连接的节点」，没连接就作用于本机
        // （游戏自己的 probe / rm 取目标时也是这两条规则）。
        var target = os.connectedComp ?? os.thisComputer;
        if (target == null)
        {
            return;
        }

        var ports = HackEngine.Ports(target);
        if (ports.Count == 0)
        {
            os.write("[autohack] ports: " + target.ip + " has no port table.");
            return;
        }

        os.write("[autohack] ports @ " + target.ip + " (" + target.name + "):");

        var cracked = 0;
        var native = 0;

        foreach (var port in ports)
        {
            if (port.Cracked)
            {
                cracked++;
            }

            // 「原生还是模组」是游戏数据的事实（PortExploits.cracks 只含原生 36 个端口，
            // 模组注册的一个都不在其中），与白名单无关，故这里如实分开报。
            var isNative = HackEngine.HasCrackProgram(port.CodePort);
            if (isNative)
            {
                native++;
            }

            os.write("  " + Pad(port.DisplayPort, 7)
                     + Pad(port.Protocol, 12)
                     + Pad(port.DisplayName, 24)
                     + "orig=" + Pad(port.CodePort, 7)
                     + (isNative ? "native" : "mod")
                     + (port.Cracked ? "  CRACKED" : string.Empty));
        }

        var mods = new List<string>();
        foreach (var port in ports)
        {
            // 判据与 HackEngine.CrackablePorts 逐条对齐，否则会给出**点了没用的**建议：
            // · 有原生破解程序的端口本来就参与自动入侵，写进 modports= 是多余的
            //   （白名单只对「原生表里没有」的协议起作用）；
            // · CodePort <= 0 的端口（ZeroDayToolKit 的 backdoor 缺省就是 0）
            //   在 CrackablePorts 里被显式排除 —— 端口号 0 开了也匹配不到任何东西。
            // 去重按协议名：同一协议在一台机器上只出现一次，但防御性挡一下。
            if (port.CodePort > 0
                && !HackEngine.HasCrackProgram(port.CodePort)
                && !mods.Contains(port.Protocol))
            {
                mods.Add(port.Protocol);
            }
        }

        os.write("[autohack] ports: " + ports.Count + " total, " + cracked + " cracked, "
                 + native + " with a crack program"
                 + (mods.Count > 0 ? ", " + mods.Count + " mod port(s)" : string.Empty));

        if (mods.Count > 0)
        {
            // 两行，都只是提示文本 —— 本工具不写任何白名单（那是每轮的运行参数，
            // 不是可持久化的状态；见 ModPortPolicy 的类注释）。
            // 第一行推荐通配：它是**完整**答案，不会漏掉这台机器上没出现的协议。
            os.write("[autohack] ports: " + mods.Count + " mod port(s) here -> " + string.Join(",", mods));
            os.write("[autohack] ports: to auto-crack them all -> modports=*   "
                     + "(panel: tick 'auto mod ports'; the list above is only what THIS machine has)");
        }
    }

    /// <summary>左对齐补白；超宽不截断 —— 宁可排版跑偏，也不吞掉名字。</summary>
    private static string Pad(int value, int width) => Pad(value.ToString(), width);

    private static string Pad(string text, int width)
    {
        var s = text ?? string.Empty;
        return s.Length >= width ? s + " " : s.PadRight(width);
    }
}

namespace AutoHack;

using System.Globalization;
using Hacknet;

/// <summary>
/// DEC 解密工具：解开 #DEC_ENC 加密文件，支持多层递归与批量落盘。
///
/// 密码不是暴力破解出来的 —— 游戏自身的加密是逐字符仿射：
///     Encrypt: num = data[i] * 1822 + 32767 + passcode     (FileEncrypter.cs:40)
/// 头部第 4 段固定加密字符串 "ENCODED"，其首字符 'E' 的密文恒为
///     'E' * 1822 + 32767 + passcode = 158485 + passcode
/// 于是 passcode = 首个密文数字 - 158485，再交给游戏自身的
/// <see cref="FileEncrypter.TestingDecryptString"/> 反验：解出的第 6 段必须等于
/// "ENCODED"，否则说明反推失败，当场放弃而不是继续猜。
/// </summary>
internal static class DecTools
{
    /// <summary>DEC 文件头标记（FileEncrypter.EncryptString 固定写入，FileEncrypter.cs:19）。</summary>
    internal const string Marker = "#DEC_ENC::";

    /// <summary>'E' * 1822 + 32767 —— 头部 "ENCODED" 首字符密文的常量偏移。</summary>
    private const int Magic = 'E' * 1822 + 32767;

    /// <summary>递归层数上限：防自引用/病态嵌套把游戏线程挂死。</summary>
    private const int MaxLayers = 16;

    /// <summary>批量收集时跳过的目录（log/sys 无用户数据，且 log 会被入侵流程清掉）。</summary>
    private static readonly string[] Skipped = { "log", "sys" };

    /// <summary>一次解密的结果：Ok 表示已解到无标记的明文，Passcodes 是逐层反推出的密码。</summary>
    internal readonly record struct Result(bool Ok, string Content, IReadOnlyList<ushort> Passcodes);

    internal static bool IsEncrypted(string data)
        => !string.IsNullOrEmpty(data) && data.StartsWith(Marker, StringComparison.Ordinal);

    /// <summary>从头部第 4 段反推 passcode；结构不符返回 false（不猜、不抛）。</summary>
    internal static bool TryDerivePasscode(string data, out ushort passcode)
    {
        passcode = 0;

        var lines = data.Split(Utils.robustNewlineDelim, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            return false;
        }

        var parts = lines[0].Split(FileEncrypter.HeaderSplitDelimiters, StringSplitOptions.None);
        if (parts.Length < 4)
        {
            return false;
        }

        var nums = parts[3].Split(Utils.spaceDelim, StringSplitOptions.RemoveEmptyEntries);
        if (nums.Length == 0)
        {
            return false;
        }

        if (!int.TryParse(nums[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var first))
        {
            return false;
        }

        passcode = (ushort)(first - Magic);
        return true;
    }

    /// <summary>解一层：反推 passcode 后用游戏自身实现反验，验不过即失败。</summary>
    internal static bool TryDecryptLayer(string data, out string body, out ushort passcode)
    {
        body = null;

        if (!TryDerivePasscode(data, out passcode))
        {
            return false;
        }

        string[] result;
        try
        {
            result = FileEncrypter.TestingDecryptString(data, passcode);
        }
        catch (Exception ex) when (ex is FormatException or NullReferenceException)
        {
            return false;
        }

        // result[5] 是头部第 4 段的解密结果：只有 passcode 正确才会是 "ENCODED"。
        if (result[5] != "ENCODED")
        {
            return false;
        }

        body = result[2];
        return true;
    }

    /// <summary>逐层解到正文不再带 #DEC_ENC 标记为止，记录每层反推出的 passcode。</summary>
    internal static Result DecryptAll(string data)
    {
        var codes = new List<ushort>();
        var current = data;

        for (var i = 0; i < MaxLayers; i++)
        {
            if (!IsEncrypted(current))
            {
                return new Result(true, current, codes);
            }

            if (!TryDecryptLayer(current, out var body, out var code))
            {
                return new Result(false, current, codes);
            }

            codes.Add(code);

            // 自引用：解出来的还是自己，再循环下去没有意义，停在这里并如实报出层数。
            if (body == null || body == current)
            {
                break;
            }

            current = body;
        }

        return new Result(false, current, codes);
    }

    /// <summary>批量：扫目标节点上的 DEC 文件，逐层解开后写进玩家 /home。</summary>
    internal static void Run(OS os, bool allNodes)
    {
        var targets = allNodes
            ? HackEngine.ConnectableComputers(os)
            : new[] { os.connectedComp ?? os.thisComputer };

        var found = new List<FileEntry>();
        foreach (var target in targets)
        {
            ToolFiles.Collect(target.files.root, found, IsEncrypted, Skipped);
        }

        if (found.Count == 0)
        {
            os.write("[autohack] dec: no #DEC_ENC file in " + targets.Length + " node(s).");
            return;
        }

        var home = ToolFiles.Home(os);
        var written = 0;
        var failed = 0;

        foreach (var file in found)
        {
            var result = DecryptAll(file.data);
            if (!result.Ok)
            {
                failed++;
                os.write("[autohack] dec: " + file.name + " - FAILED after " + result.Passcodes.Count + " layer(s).");
                continue;
            }

            var name = ToolFiles.Write(home, ToolFiles.Stem(file.name, ".dec"), ".txt", result.Content);
            written++;
            os.write("[autohack] dec: " + file.name + " -> home/" + name
                     + " (" + result.Passcodes.Count + " layer(s), " + result.Content.Length + " chars).");
        }

        os.write("[autohack] dec: " + written + " written, " + failed + " failed, "
                 + found.Count + " candidate(s) in " + targets.Length + " node(s).");
    }
}

namespace AutoHack.Core;

/// <summary>
/// 终端指令原文 —— 唯一真源。
///
/// 这些字符串必须与游戏自己的命令名逐字一致（玩家看到的、游戏能执行的是同一个词），
/// 散在各处时改一处漏一处就会出现「回显的命令执行不了」。计划期回显与执行期兜底都从这里取。
/// </summary>
internal static class HackCommands
{
    internal const string Probe = "probe";

    internal const string Escalate = "porthack";

    internal const string Disconnect = "dc";

    internal const string Login = "login";

    /// <summary>连接命令：<c>connect &lt;ip&gt;</c>。</summary>
    internal static string Connect(string ip) => "connect " + ip;
}

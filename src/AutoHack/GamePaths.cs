namespace AutoHack;

/// <summary>
/// 游戏内容目录里的固定名字：目录名与文件名。
///
/// <b>为什么单独一个类型</b>（v1.45.0 审计 P1-1）：这些名字被端口、清痕、白名单三块共用，
/// 而三块原先同属一个 partial 类，故曾是 <c>private const</c>。拆分后若留在任一侧，
/// 另外两侧就得复制字符串 —— 复制出来的那一份迟早与游戏真名漂移。
/// </summary>
internal static class GamePaths
{
    /// <summary>每台机器 /log 的目录名（<c>Computer</c> 文件系统里的一层）。</summary>
    internal const string LogFolder = "log";

    /// <summary>Pathfinder 白名单守护的数据目录名。</summary>
    internal const string WhitelistFolder = "Whitelist";

    internal const string WhitelistList = "list.txt";

    internal const string WhitelistAuth = "authenticator.dll";

    internal const string WhitelistSource = "source.txt";
}
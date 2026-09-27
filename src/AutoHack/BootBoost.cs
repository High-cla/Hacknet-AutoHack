namespace AutoHack;

using Hacknet;

/// <summary>
/// 开机自检文字加速：把 <c>CrashModule.BOOT_TIME</c> 从 14.5 秒压到 1.2 秒。默认开启，
/// 无开关、无命令（用户定）。
///
/// 为什么是「直接写字段」而不是给面板加个复选框：<c>CrashModule.BOOT_TIME</c>
/// （CrashModule.cs:15）是 <b>static 字段</b>，值在类静态初始化器里一次固化：
/// <code>
/// public static float BOOT_TIME = (Settings.isConventionDemo ? 5f : (Settings.FastBootText ? 1.2f : 14.5f));
/// </code>
/// 之后再改 <c>Settings.FastBootText</c> 不会有任何效果 —— 派生值早已算完，而
/// <c>SettingsLoader</c> 里根本没有 <c>FastBootText</c> 的写入点（它不落 Settings.txt）。
/// 唯一能生效的做法就是覆盖这个字段。
///
/// 时序：本方法在插件 <c>Load()</c> 里调用，早于 <c>OS</c> 构造
/// （<c>OS.cs:500-501</c> 的 <c>new CrashModule(...)</c> 才首次触碰该类）。
/// 首次触碰触发静态初始化，随后本行覆盖之。<c>bootTextDelay</c> 是实例字段、
/// 只在 <c>CrashModule.LoadContent()</c>（CrashModule.cs:78）算一次，那一刻读到的
/// 已是新值，逐行节奏自动跟着变 —— 故<b>不必也不该</b>手动重算它：<c>reset()</c>
/// （CrashModule.cs:319-331）不重算，手动补的那份在重启后会失效，反而更脆。
///
/// 覆盖面：<c>BOOT_TIME</c> 全项目唯一用点是 <c>CrashModule.Update</c> 的状态机
/// （CrashModule.cs:120 的 <c>elapsedTime &lt; BLUESCREEN_TIME + BLACK_TIME + BOOT_TIME + ...</c>），
/// 其余三个同族常量（<c>BLUESCREEN_TIME</c>/<c>BLACK_TIME</c>/<c>BOOT_FAIL_CRASH_TIME</c>）
/// 与文字逐行无关，不动。故改这一个字段即完整。
///
/// 副作用：会一并覆盖 Convention demo 的 5 秒档（<c>Settings.isConventionDemo</c>），
/// 那是展台专用模式，正常游玩不涉及。
/// </summary>
internal static class BootBoost
{
    /// <summary>目标时长（秒）。与游戏 <c>Settings.FastBootText</c> 为真时同值。</summary>
    private const float BootTime = 1.2f;

    internal static void Apply()
    {
        CrashModule.BOOT_TIME = BootTime;
    }
}

namespace AutoHack;

using System.Runtime.CompilerServices;
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

    /// <summary>
    /// 模块初始化器：本程序集被首次触碰时由编译器生成的 <c>.cctor</c> 自动调用，
    /// <b>早于</b> BepInEx 调用插件的 <c>Load()</c>。
    ///
    /// <b>为什么要从 Load() 挪到这里。</b>原先是 <c>Load()</c> 的第一行，靠一句注释
    /// 「必须在 PatchAll 之前、且在 OS 构造之前」约束后人 —— 那是<b>人工约定</b>：
    /// 谁往 <c>Load()</c> 前面插一行就可能打破它，而打破之后的表现是「开机文字又变慢 14.5 秒」，
    /// 与那次代码改动毫无表面关联，极难回查。<c>[ModuleInitializer]</c> 把这条约束变成
    /// <b>运行时保证</b> —— 模块初始化器一定在本程序集里任何其它代码之前跑完。
    ///
    /// 时序余量反而更大了：模块首次被访问（BepInEx 反射扫描插件类型那一刻）即执行，
    /// 比 <c>Load()</c> 还早；而 <c>CrashModule</c> 的静态初始化要到
    /// <c>OS.cs:500-501</c> 的 <c>new CrashModule(...)</c> 才发生，覆盖必然赶在前面。
    ///
    /// <c>[ModuleInitializer]</c> 需要 <c>System.Runtime.CompilerServices.ModuleInitializerAttribute</c>，
    /// net472 没有 —— 由 <c>IsExternalInit.cs</c> 一并声明。
    /// </summary>
    [ModuleInitializer]
    internal static void Init() => Apply();
}

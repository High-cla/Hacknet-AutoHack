namespace AutoHack;

using System.Runtime.CompilerServices;
using Hacknet;

/// <summary>
/// 开机加速：把<b>整条开机链</b>从约 25.5 秒压到约 2.4 秒。默认开启，无开关、无命令（用户定）。
///
/// <b>为什么一个常量不够（v1.41.1 修的 bug）。</b>此前只压 <c>CrashModule.BOOT_TIME</c>
/// （自检文字的逐行节奏），实测出现「文字飞快、然后黑屏干等十几秒才进系统」。
/// 因为进系统<b>不由</b> <c>CrashModule</c> 决定 —— <c>OS.Update</c> 的判据是
/// <c>if (!thisComputer.disabled)</c>（OS.cs:753-771），而 <c>disabled</c> 由
/// <c>Computer.bootupTick</c> 逐帧递减 <c>bootTimer</c> 到 0 才置假（Computer.cs:310-317），
/// <c>bootTimer</c> 的初值是 <c>Computer.BASE_BOOT_TIME = 25.5f</c>（Computer.cs:23，
/// 由 <c>Computer.crash</c> 写入，Computer.cs:437）。于是实际时间线是：
/// <code>
/// 0 ──8s── 蓝屏(BLUESCREEN_TIME) ──2s── 黑屏(BLACK_TIME) ──1.2s── 自检文字(BOOT_TIME)
///                                                   └────── 余下 14.3 秒纯黑等待 ← bug
/// 25.5s  bootTimer 归零 ──▶ disabled=false ──▶ 进系统
/// </code>
/// 现在四个常量一起压，闸门与动画对齐：<b>文字跑完那一刻就进系统</b>。
///
/// <b>为什么是「直接写字段」而不是给面板加复选框。</b>这些全是 <b>static 字段</b>，值在
/// 各类型的静态初始化器里一次固化（<c>CrashModule.cs:13-23</c>、<c>Computer.cs:23</c>）；
/// 之后改 <c>Settings.FastBootText</c> 毫无效果 —— 派生值早已算完，而 <c>SettingsLoader</c>
/// 里根本没有 <c>FastBootText</c> 的写入点（它不落 Settings.txt）。唯一能生效的做法就是覆盖字段。
///
/// <b>时序。</b>覆盖发生在 <c>[ModuleInitializer]</c>（见 <see cref="Init"/>），早于插件的
/// <c>Load()</c>，更早于 <c>OS</c> 构造（<c>OS.cs:500-501</c> 的 <c>new CrashModule(...)</c> 与
/// 任何 <c>Computer.crash</c>）。读字段本身就会触发对应类型的静态初始化，故覆盖必然落在原值算完之后。
///
/// <b>为什么不必重算 <c>bootTextDelay</c>。</b>它是实例字段，只在
/// <c>CrashModule.LoadContent()</c>（CrashModule.cs:78）按 <c>BOOT_TIME</c> 算一次 ——
/// 那一刻读到的已是新值。手动重算反而更脆：<c>reset()</c>（:319-331）不重算，
/// 补的那份在重启后会失效。
///
/// <b>代价（刻意接受）。</b><c>BLUESCREEN_TIME</c> / <c>BLACK_TIME</c> 是全局静态量，
/// 玩家机器被打崩时走的是同一条状态机 —— 崩溃时的假蓝屏也跟着变短（1.0s 而不是 8s）。
/// 用同一组常量管两条路径是刻意的：想区分就得按触发源写补丁并把原值搬进跨线程状态，
/// 复杂度明显高于收益。
///
/// 另：<c>CrashModule.POST_BLACK_TIME</c> 在游戏里<b>从未被使用</b>（只有声明，
/// 全项目零读点），故不覆盖。
/// </summary>
internal static class BootBoost
{
    /// <summary>自检文字的逐行节奏（秒）。实测 94 行在 1.2 秒内走完。</summary>
    private const float BootTime = 1.2f;

    /// <summary>假蓝屏停留（原 8 秒）。</summary>
    private const float BlueScreenTime = 1.0f;

    /// <summary>蓝屏与文字之间的黑屏（原 2 秒）。</summary>
    private const float BlackTime = 0.2f;

    /// <summary>
    /// 进系统的闸门时长 = 蓝屏 + 黑屏 + 文字 + 0.1 秒余量。
    ///
    /// 余量不是保险丝而是一帧的量化差：<c>bootTimer</c> 与 <c>CrashModule.elapsedTime</c>
    /// 由不同调用点各自累加（OS.cs:756 与 :757 同一帧、先后顺序固定），两者严格对齐时
    /// 最后一帧可能谁都不够 —— 留 0.1 秒让文字必然先走完。
    /// </summary>
    private const float PowerOnTime = BlueScreenTime + BlackTime + BootTime + 0.1f;

    internal static void Apply()
    {
        // 开机动画（CrashModule 的状态机，CrashModule.cs:108-172）
        CrashModule.BLUESCREEN_TIME = BlueScreenTime;
        CrashModule.BLACK_TIME = BlackTime;
        CrashModule.BOOT_TIME = BootTime;

        // 进系统的闸门（OS.cs:753 的 !thisComputer.disabled ← Computer.cs:310-317 的 bootTimer）
        Computer.BASE_BOOT_TIME = PowerOnTime;

        // 远程机重启用 BASE_REBOOT_TIME，而其 bootTimer 从不被 tick（只有玩家机在
        // OS.Update 里调 bootupTick），故对本机时长无影响；一并对齐只为「所有开机路径同一口径」。
        Computer.BASE_REBOOT_TIME = PowerOnTime;
    }

    /// <summary>
    /// 模块初始化器：本程序集被首次触碰时由编译器生成的 <c>.cctor</c> 自动调用，
    /// <b>早于</b> BepInEx 调用插件的 <c>Load()</c>。
    ///
    /// <b>为什么要从 Load() 挪到这里。</b>原先是 <c>Load()</c> 的第一行，靠一句注释
    /// 「必须在 PatchAll 之前、且在 OS 构造之前」约束后人 —— 那是<b>人工约定</b>：
    /// 谁往 <c>Load()</c> 前面插一行就可能打破它，而打破之后的表现是「开机又变慢」，
    /// 与那次代码改动毫无表面关联，极难回查。<c>[ModuleInitializer]</c> 把这条约束变成
    /// <b>运行时保证</b> —— 模块初始化器一定在本程序集里任何其它代码之前跑完。
    ///
    /// <c>[ModuleInitializer]</c> 需要 <c>System.Runtime.CompilerServices.ModuleInitializerAttribute</c>，
    /// net472 没有 —— 由 <c>IsExternalInit.cs</c> 一并声明。
    /// </summary>
    [ModuleInitializer]
    internal static void Init() => Apply();
}
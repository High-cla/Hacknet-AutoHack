namespace AutoHack;

using System.Diagnostics;
using BepInEx.Logging;

/// <summary>
/// 诊断追踪通道：<b>调用点在编译期被整条删除</b>，除非定义了 <c>AUTOHACK_TRACE</c>。
///
/// <c>[Conditional]</c> 与 <c>#if</c> 的区别就在这一点：<c>#if</c> 需要把调用点也包进
/// 预处理块（读起来支离破碎，且忘了包就漏编译），而 <c>[Conditional]</c> 只标在方法上 ——
/// <b>编译器自己把每个调用点连同实参求值一起抹掉</b>。于是 Release 构建里
/// 这些调用**一个字节都不存在**：没有 <c>if</c>、没有字符串拼接、没有实参装箱。
///
/// 打开方式（默认关闭，不影响发布产物）：
/// <code>
/// dotnet build src/AutoHack/AutoHack.csproj -c Release -p:AutoHackTrace=true
/// </code>
/// 详见 AutoHack.csproj 里的 <c>AutoHackTrace</c> 属性 —— 它<b>追加</b>而非覆盖
/// <c>DefineConstants</c>，故不会碰掉 SDK 自带的 TRACE/RELEASE 等符号。
///
/// 输出走 BepInEx 日志（<c>BepInEx/LogOutput.log</c>），不写游戏终端 ——
/// 追踪是给开发者看的，不该污染玩家的终端回显。
///
/// <b>为什么值得存在。</b>本插件有两类「静默失效」：动画没排上
/// （<see cref="NativeExes.Show"/> 有四种返回 false 的理由）与提权等待超时。
/// 两者在终端都只表现为「什么都没发生」，没有追踪就只能靠读代码猜。这几处埋点是为此准备的。
/// </summary>
internal static class Trace
{
    private static ManualLogSource _log;

    /// <summary>接上 BepInEx 日志源。由 <see cref="AutoHackPlugin.Load"/> 调用（本方法不受条件编译影响）。</summary>
    internal static void Bind(ManualLogSource log) => _log = log;

    /// <summary>记一条追踪。未定义 <c>AUTOHACK_TRACE</c> 时，所有调用点不存在。</summary>
    [Conditional("AUTOHACK_TRACE")]
    internal static void Write(string message) => _log?.LogInfo("[trace] " + message);
}

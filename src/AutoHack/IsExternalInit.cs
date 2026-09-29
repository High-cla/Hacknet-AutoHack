namespace System.Runtime.CompilerServices;

/// <summary>
/// net472 缺失的编译器契约类型，集中在此声明。
///
/// 它们不是「功能」，而是 Roslyn 遇到对应语法时**必须引用**的标记类型 ——
/// .NET Framework 4.7.2 的 mscorlib 里没有，缺了就编译不过
/// （<c>init</c>/<c>record</c> 报 CS0518、<c>required</c> 报 CS0656、其余报 CS0234）。
/// 每个都只是空标记类型，自己声明即可，**不需要引入任何 NuGet 包**。
///
/// 实测（探针工程，net472 + LangVersion 13，零包引用）：补上声明后
/// <c>init</c> / <c>record</c> / <c>record struct</c> / <c>required</c> /
/// <c>CallerArgumentExpression</c> / <c>[ModuleInitializer]</c> 全部可用。
///
/// <b>不要删</b>：删掉后全仓的 <c>record struct</c>（13 处）与 <c>init</c> 访问器会一起编译失败。
/// </summary>
internal static class IsExternalInit { }

/// <summary>
/// <c>[ModuleInitializer]</c> 需要它：被标记的静态无参 <c>void</c> 方法
/// 会在**模块首次被访问时**自动执行（由编译器生成的 <c>.cctor</c> 调用）。
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class ModuleInitializerAttribute : Attribute { }

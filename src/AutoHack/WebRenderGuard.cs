namespace AutoHack;

using System;
using System.IO;
using System.Text;
using Hacknet;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// 原生网页渲染器（<c>XNAWebRenderer.dll</c> + CEF）的稳定性补丁。
///
/// <b>为什么需要它。</b>访问带 <see cref="WebServerDaemon"/> 的机器时有概率闪退。
/// 取证：<c>BepInEx/LogOutput.log</c> 尾部停在 <c>WebRenderer.navigateTo</c> 里的
/// <c>Console.WriteLine("Launching Web Thread")</c>，即进程死在网页渲染路径上。
///
/// 三处缺陷各自独立，都在本文件里堵掉：
///
/// <b>① <see cref="WebRenderer.setSize"/> 尺寸没变也重建一切（主因）。</b>
/// 它无条件 <c>texture.Dispose()</c> → <c>new Texture2D</c> → <c>texBuffer = new byte[…]</c>
/// → <c>XNAWR_SetViewport</c>（<c>WebRenderer.cs:31-50</c>）。而原生侧是 CEF，
/// 页面加载完成时跨原生边界回调 <see cref="WebRenderer.TextureUpdated"/>，
/// 那里按<b>当前</b> <c>texBuffer.Length</c> 盲拷：
/// <code>
/// Marshal.Copy(buffer, texBuffer, 0, texBuffer.Length);
/// texture.SetData(texBuffer);
/// </code>
/// 于是「换缓冲」与「回调」之间是竞态：新缓冲比原生缓冲大就是<b>越界读</b>。
/// <b>而 <c>AccessViolationException</c> 在 .NET 4.x 属损坏状态异常、默认不可捕获</b>
/// （本机无 <c>Hacknet.exe.config</c>，没有开 <c>legacyCorruptedStateExceptionsPolicy</c>），
/// 游戏自己那个 <c>catch (AccessViolationException)</c> 兜不住 ⇒ 进程直接终止，
/// 表现为「闪退」而不是报错。
///
/// <b>本插件为什么把它放大成「有概率」。</b><c>ThemeManager.Update</c>
/// （<c>ThemeManager.cs:60-86</c>）的判据是「连着的机器有没有 WebServerDaemon」，
/// 而 <c>framesTillWebUpdate</c> 在<b>每次断开时被复位为 0</b>（<c>:82</c>）——
/// 于是每连上一台网页服务器都会再调一次 <c>setSize</c>。
/// AutoHack 每台目标都 <c>connect</c>（还可能 <c>disconnect</c>），
/// 于是这个竞态窗口被打开上百次：<b>访问的网页服务器越多，撞上的概率越高</b>。
///
/// <b>② <see cref="WebRenderer.TextureUpdated"/> 对不可用对象不设防。</b>
/// <c>texBuffer</c> 为 null、<c>texture</c> 已 <c>Dispose</c> 时仍照拷照写，
/// 抛出的 <c>ObjectDisposedException</c> 不在那个 <c>catch (AccessViolationException)</c>
/// 的覆盖范围内，会从原生回调里冒出去。
///
/// <b>③ 网页缓存文件是所有服务器共用的一个路径、且非原子写。</b>
/// <c>WebServerDaemon.ShowPage</c>（<c>WebServerDaemon.cs:118-132</c>）固定写
/// <c>&lt;cwd&gt;/Content/Web/Cache/HN_OS_WebCache.html</c>，用的是
/// <see cref="Utils.writeToFile"/>（<c>StreamWriter</c> 直写，<c>Utils.cs:275-281</c>），
/// 紧接着 <c>WebRenderer.navigateTo(text)</c> 让 CEF 去读它。
/// AutoHack 连着 A 写完立刻切到 B 再写同一文件时，CEF 可能正读到半截。
///
/// <b>改动面刻意压到最小：三处都是 Harmony Prefix，不改游戏 DLL、不加开关。</b>
/// 每一条都在「原实现会是空操作」时才跳过，故不改变任何正常路径的行为。
/// </summary>
[HarmonyPatch]
internal static class WebRenderGuard
{
    /// <summary>
    /// <b>①</b> 尺寸与当前一致时跳过整个重建。
    ///
    /// 跳过条件（全部满足才算「原实现是空操作」）：
    /// <list type="bullet">
    /// <item><c>Enabled</c> —— 关掉网页渲染时原方法只写 <c>width</c>/<c>height</c> 就返回，
    ///   跳过会让那两个字段不更新，故不跳。</item>
    /// <item><c>graphics != null</c> —— 否则原方法也不重建（只置 <c>loadingPage</c>）。</item>
    /// <item><c>texture</c> / <c>texBuffer</c> 非空且 <c>texture</c> 未释放 ——
    ///   否则必须重建，跳过会把坏状态留着。</item>
    /// <item><b>新尺寸与 <c>WebRenderer.width</c>/<c>height</c> 相等</b> —— 核心判据。
    ///   这两个字段是 <c>public static int</c>，全仓<b>只在 <c>setSize</c> 内被赋值</b>
    ///   （<c>WebRenderer.cs:31-50</c>），无外部写入者，故它们就是「当前缓冲的尺寸」的权威来源。</item>
    /// </list>
    ///
    /// <b>不跳过 <c>loadingPage</c>。</b>想过「页面加载中就先不换缓冲」，但那会漏掉真正的
    /// 尺寸变化：<c>ThemeManager</c> 是<b>一次性</b>调用（<c>framesTillWebUpdate</c> 置 -1 后
    /// 永不再进那个分支），跳过一次就再也不会重试，窗口尺寸变更会永久不生效。
    /// 只跳过「尺寸相同」这种<b>确定无事可做</b>的情形，才既安全又不漏。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(WebRenderer), nameof(WebRenderer.setSize))]
    private static bool BeforeSetSize(int frameWidth, int frameHeight)
    {
        if (!WebRenderer.Enabled)
        {
            return true;
        }

        if (WebRenderer.graphics == null)
        {
            return true;
        }

        var texture = WebRenderer.texture;
        var buffer = WebRenderer.texBuffer;
        if (texture == null || texture.IsDisposed || buffer == null || buffer.Length == 0)
        {
            return true;
        }

        // 尺寸一致 = 重建后状态与现在逐字相同，唯一差别是 texture 换了实例 ——
        // 而那正是竞态的来源。跳过它就是本补丁的全部目的。
        return frameWidth != WebRenderer.width || frameHeight != WebRenderer.height;
    }

    /// <summary>
    /// <b>②</b> 原生回调到达时，缓冲或纹理不可用就整帧丢弃。
    ///
    /// 丢掉的只是一帧网页画面（下一帧会重画），换来的是不让 <c>Marshal.Copy</c> 从
    /// 空指针读、不让 <c>SetData</c> 打在已释放的纹理上 ——
    /// 这两种抛出的异常都不在游戏自己那个 <c>catch (AccessViolationException)</c>
    /// 的覆盖范围内，会从原生回调里冒出去。
    ///
    /// <b>不检查原生缓冲的大小</b>：那个尺寸原生侧不告诉我们。真正让两者一致的是
    /// <see cref="BeforeSetSize"/> —— 它保证 <c>texBuffer</c> 在页面加载期间不再被换掉。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(WebRenderer), nameof(WebRenderer.TextureUpdated))]
    private static bool BeforeTextureUpdated(IntPtr buffer)
    {
        if (buffer == IntPtr.Zero)
        {
            return false;
        }

        var texture = WebRenderer.texture;
        var target = WebRenderer.texBuffer;
        return texture != null && !texture.IsDisposed && target != null && target.Length > 0;
    }

    /// <summary>网页缓存文件名（<c>WebServerDaemon.TEMP_WEBPAGE_CACHE_FILENAME</c> 的末段）。</summary>
    private const string WebCacheName = "HN_OS_WebCache.html";

    /// <summary>原子写的临时文件后缀。与 <c>Utils.SafeWriteToFile</c> 用的 <c>.tmp</c> 区分开。</summary>
    private const string TempSuffix = ".autohack.tmp";

    /// <summary>
    /// <b>③</b> 网页缓存改为原子写。
    ///
    /// 只拦这一个路径 —— <see cref="Utils.writeToFile"/> 是通用原语，
    /// 其余调用点（主题、存档、脚本等）一律放行给原实现。
    ///
    /// 判据用「文件名末段相等」而不是全路径比较：<c>ShowPage</c> 拼的是
    /// <c>Directory.GetCurrentDirectory() + "/Content/Web/Cache/…"</c>，
    /// 而扩展模式下会换成扩展目录（<c>WebServerDaemon.cs:120-130</c>），
    /// 两种情形末段都一样。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Utils), nameof(Utils.writeToFile))]
    private static bool BeforeWriteToFile(string data, string filename)
    {
        if (string.IsNullOrEmpty(filename) || !EndsWithWebCacheName(filename))
        {
            return true;
        }

        try
        {
            WriteAtomic(data, filename);
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // 原子写失败就退回原实现 —— 它至少还能写出内容，
            // 而这里失败的原因（占位冲突、只读、路径过长）原实现同样会失败，
            // 交给它去抛，异常语义与打补丁前一致。
            return true;
        }
    }

    /// <summary>写临时文件再整体替换，读者要么看到旧文件、要么看到新的完整文件。</summary>
    private static void WriteAtomic(string data, string filename)
    {
        // 同一个路径可能被多个线程写（游戏线程的 navigateTo 与命令行线程的 AutoHack 工具），
        // 锁住的是「临时文件同名」这件事 —— 没有它两个线程会写同一个 .tmp。
        lock (WriteLock)
        {
            var temp = filename + TempSuffix;
            File.WriteAllText(temp, data, Utf8NoBom);

            if (File.Exists(filename))
            {
                // ReplaceFile：先写新内容、再原子换名，中途没有「文件不存在」的窗口
                // （Utils.SafeWriteToFile 是 Delete + Move，中间那一瞬 CEF 会读到 file-not-found）。
                File.Replace(temp, filename, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temp, filename);
            }
        }
    }

    /// <summary>末段比对，避免为一处判据引入 <c>Path.GetFileName</c> 的分配与异常面。</summary>
    private static bool EndsWithWebCacheName(string path)
    {
        if (path.Length < WebCacheName.Length)
        {
            return false;
        }

        var start = path.Length - WebCacheName.Length;
        return string.CompareOrdinal(path, start, WebCacheName, 0, WebCacheName.Length) == 0
               && (start == 0 || path[start - 1] == '/' || path[start - 1] == '\\');
    }

    /// <summary>原子写的串行闸门。见 <see cref="WriteAtomic"/>。</summary>
    private static readonly object WriteLock = new();

    /// <summary>
    /// 与原实现（<c>new StreamWriter(path)</c>）一致的编码：UTF-8 且不带 BOM。
    /// 带 BOM 会让 CEF 把 BOM 当正文渲染，网页顶部多出三个乱码字符。
    /// </summary>
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
}

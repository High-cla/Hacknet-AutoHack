namespace AutoHack;

using System.IO;
using BepInEx.Configuration;
using BepInEx.Logging;

/// <summary>
/// 面板设置的落盘层。
///
/// <b>面板是唯一设置界面</b>（用户定）：本类不提供任何 UI，只回答两件事 ——
/// 「上次存了什么」（<see cref="Load"/>）与「把现在的值存回去」（<see cref="Persist"/>）。
/// 设置项永远只有面板这一个真相来源，不会出现两处界面互相漂移。
///
/// <b>为什么不用 <c>Pathfinder.Options</c></b>：框架那套只负责绘制
/// （<c>Pathfinder.Options.Option</c> 的子类各自 <c>Draw</c> 一个控件、值存内存字段），
/// 持久化是**调用方**自己用 BepInEx <see cref="ConfigFile"/> 做的 —— 框架自身的做法见
/// <c>Pathfinder.Options/PathfinderOptions.cs</c>：<c>Bind</c> 读、<c>Config.Save()</c> 写。
/// 既然框架不含落盘，引入它就只剩一套与自绘面板风格冲突的原生控件
/// （原生 <c>CheckBox</c> 只在 hover 时画文字，见 llms.txt 坑 8），故直接用 ConfigFile。
///
/// <b>什么时候写盘</b>：<c>SaveOnConfigSet</c> 关掉，赋值只改内存；只在**指针抬起**
/// 后落一次盘（<see cref="HackOverlay"/> 按<see cref="HackPanelState.Fingerprint"/>
/// 的变化触发）。若沿用 BepInEx 的缺省（<c>SaveOnConfigSet = true</c>），拖动滑条
/// 就是每帧一次磁盘写入。
/// </summary>
internal static class PanelSettings
{
    /// <summary>cfg 里的小节名。键名用 snake_case，便于手工编辑。</summary>
    private const string Section = "panel";

    private static ConfigFile _config;
    private static ManualLogSource _log;

    /// <summary>
    /// 注入插件自己的 cfg（<c>HacknetPlugin.Config</c> → <c>BepInEx/config/&lt;GUID&gt;.cfg</c>）
    /// 与日志。必须在面板首次读取设置之前调用（见 <c>AutoHackPlugin.Load</c>）。
    /// </summary>
    internal static void Bind(ConfigFile config, ManualLogSource log)
    {
        _log = log;
        if (config == null)
        {
            return;
        }

        // 赋值不落盘 —— 落盘时机由 Persist 独家决定。
        config.SaveOnConfigSet = false;
        _config = config;

        // 绘制链要在面板打开之前就能读到它，故这里先取一次。
        Diagnostics = Read("diagnostics", false);
    }

    /// <summary>
    /// 把上次存下的值读进面板状态。没存过时 <c>Bind</c> 会以字段当前值作为缺省
    /// 建条目，故首次运行等于「保持各字段自己的缺省」。
    /// </summary>
    internal static void Load(HackPanelState s)
    {
        if (_config == null || s == null)
        {
            return;
        }

        s.Scope = Read("scope", s.Scope);

        // 键名从 clear_logs / clear_own_logs 改为 wipe_traces：口径变了（只删含玩家 IP
        // 的条目），旧键存的是「整目录清空」的意图，不能悄悄套用到新语义上 ——
        // 旧的 true 在新口径下过宽、旧的 false 又会把新缺省（开）压成关。
        // 故换键、不迁移：玩家沿用新缺省即可。
        s.WipeTraces = Read("wipe_traces", s.WipeTraces);
        s.UploadMarker = Read("upload_marker", s.UploadMarker);
        s.ConnectFirst = Read("connect_first", s.ConnectFirst);
        s.Disconnect = Read("disconnect_when_done", s.Disconnect);
        s.SkipOwned = Read("skip_owned", s.SkipOwned);
        s.AutoModPorts = Read("auto_mod_ports", s.AutoModPorts);
        s.AllNodes = Read("all_nodes", s.AllNodes);
        s.UseCredentials = Read("use_credentials", s.UseCredentials);
        s.ShowExes = Read("native_exes", s.ShowExes);
        s.ResetIP = Read("new_ip", s.ResetIP);

        s.Diagnostics = Read("diagnostics", s.Diagnostics);

        s.X = Read("panel_x", s.X);
        s.Y = Read("panel_y", s.Y);
        s.Collapsed = Read("collapsed", s.Collapsed);
    }

    /// <summary>
    /// 读一项：**非法值不静默**。手改 cfg 写成不存在的枚举名、越界的数字时，BepInEx 的
    /// 转换会抛 —— 抛在 <see cref="Load"/> 里就是「面板打不开」，而它通常在命令线程被调用。
    /// 这里逐项兜住：记一行警告、回退该项缺省，其余项照常读。
    /// </summary>
    private static T Read<T>(string key, T fallback)
    {
        try
        {
            return Entry(key, fallback).Value;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidCastException
                                       or OverflowException or NotSupportedException)
        {
            Warn(key, ex);
            return fallback;
        }
    }

    /// <summary>非法配置只报一次/键，避免每次开面板刷屏。</summary>
    private static readonly HashSet<string> WarnedKeys = new(StringComparer.Ordinal);

    private static void Warn(string key, Exception error)
    {
        if (!WarnedKeys.Add(key))
        {
            return;
        }

        _log?.LogWarning("[autohack] config '" + Section + ":" + key + "' is invalid ("
                         + error.GetType().Name + "); using the default. " + error.Message);
    }

    /// <summary>
    /// 性能计数开关的当前值（cfg 键 <c>diagnostics</c>）。
    ///
    /// 单独暴露一份<b>只读缓存</b>：绘制链每 300 帧要判一次，而那时面板状态可能还没建立
    /// （<see cref="AutoHackRuntime.State"/> 为 null 表示本局没开过面板），不能为了读一个
    /// 布尔值去 <c>EnsureState</c> —— 那会顺带把面板变成可见。故由本类在
    /// <see cref="Bind"/> / <see cref="Load"/> / <see cref="Persist"/> 三处同步。
    /// </summary>
    internal static bool Diagnostics { get; private set; }

    /// <summary>把当前状态写回 cfg 并落盘。写失败只记日志：设置存不下不该影响游戏。</summary>
    internal static void Persist(HackPanelState s)
    {
        if (_config == null || s == null)
        {
            return;
        }

        Entry("scope", s.Scope).Value = s.Scope;
        Entry("wipe_traces", s.WipeTraces).Value = s.WipeTraces;
        Entry("upload_marker", s.UploadMarker).Value = s.UploadMarker;
        Entry("connect_first", s.ConnectFirst).Value = s.ConnectFirst;
        Entry("disconnect_when_done", s.Disconnect).Value = s.Disconnect;
        Entry("skip_owned", s.SkipOwned).Value = s.SkipOwned;
        Entry("auto_mod_ports", s.AutoModPorts).Value = s.AutoModPorts;
        Entry("all_nodes", s.AllNodes).Value = s.AllNodes;
        Entry("use_credentials", s.UseCredentials).Value = s.UseCredentials;
        Entry("native_exes", s.ShowExes).Value = s.ShowExes;
        Entry("new_ip", s.ResetIP).Value = s.ResetIP;
        Entry("diagnostics", s.Diagnostics).Value = s.Diagnostics;
        Diagnostics = s.Diagnostics;
        Entry("panel_x", s.X).Value = s.X;
        Entry("panel_y", s.Y).Value = s.Y;
        Entry("collapsed", s.Collapsed).Value = s.Collapsed;

        try
        {
            _config.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 目录只读 / cfg 被别的进程占用。值仍在内存里生效，只丢持久化。
            _log?.LogWarning("AutoHack: could not save panel settings - " + ex.Message);
        }
    }

    /// <summary>绑定一个条目并取回它。键已存在时返回既有的那个，缺省值被忽略。</summary>
    private static ConfigEntry<T> Entry<T>(string key, T value) => _config.Bind(Section, key, value);
}

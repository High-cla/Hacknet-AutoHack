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

        s.Scope = Entry("scope", s.Scope).Value;

        // 键名从 clear_logs / clear_own_logs 改为 wipe_traces：口径变了（只删含玩家 IP
        // 的条目），旧键存的是「整目录清空」的意图，不能悄悄套用到新语义上 ——
        // 旧的 true 在新口径下过宽、旧的 false 又会把新缺省（开）压成关。
        // 故换键、不迁移：玩家沿用新缺省即可。
        s.WipeTraces = Entry("wipe_traces", s.WipeTraces).Value;
        s.UploadMarker = Entry("upload_marker", s.UploadMarker).Value;
        s.ConnectFirst = Entry("connect_first", s.ConnectFirst).Value;
        s.Disconnect = Entry("disconnect_when_done", s.Disconnect).Value;
        s.SkipOwned = Entry("skip_owned", s.SkipOwned).Value;
        s.AllNodes = Entry("all_nodes", s.AllNodes).Value;
        s.UseCredentials = Entry("use_credentials", s.UseCredentials).Value;
        s.ShowExes = Entry("native_exes", s.ShowExes).Value;
        s.ResetIP = Entry("new_ip", s.ResetIP).Value;

        s.X = Entry("panel_x", s.X).Value;
        s.Y = Entry("panel_y", s.Y).Value;
        s.Collapsed = Entry("collapsed", s.Collapsed).Value;
    }

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
        Entry("all_nodes", s.AllNodes).Value = s.AllNodes;
        Entry("use_credentials", s.UseCredentials).Value = s.UseCredentials;
        Entry("native_exes", s.ShowExes).Value = s.ShowExes;
        Entry("new_ip", s.ResetIP).Value = s.ResetIP;
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

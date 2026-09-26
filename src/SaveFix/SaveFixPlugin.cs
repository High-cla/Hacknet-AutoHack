namespace SaveFix;

using BepInEx;
using Hacknet;
using Hacknet.PlatformAPI.Storage;
using HarmonyLib;

/// <summary>
/// 修游戏自身的存档崩溃：<c>os.SaveUserAccountName</c> 为 null 时保存必抛
/// NullReferenceException。
///
/// <para>根因链（全部在游戏本体，与本修复无关）：</para>
/// <list type="number">
/// <item><c>OS.cs:148</c> —— <c>public string SaveUserAccountName = null;</c>，默认就是 null。</item>
/// <item>该字段**只在 <c>MainMenu</c> 构造 OS 时被赋值**（<c>MainMenu.cs:103/150/235/315</c>）。
/// 任何绕过主菜单进入 OS 的入口（例如用 HacknetHotReplace 之类的工具直接连进设备、
/// 或经扩展直接起 OS）都会让它停在 null。</item>
/// <item><c>OS.cs:1522</c> —— <c>writeSaveGame(SaveUserAccountName)</c> 把它当文件名传下去。</item>
/// <item><c>SaveFileManager.cs:238</c> —— <c>GetSaveFileNameForUsername(playerID)</c>。</item>
/// <item><c>SaveFileManager.cs:223</c> ——
/// <c>"save_" + FileSanitiser.purifyStringForDisplay(username).Replace("_","-").Trim() + ".xml"</c>。
/// 而 <c>FileSanitiser.cs:9-12</c> 对 null 输入**返回 null**，紧接着的 <c>.Replace</c>
/// 就打在 null 上 —— 栈里的 <c>IL&lt;0x0001&gt;</c> 正是这一句。</item>
/// </list>
///
/// <para><c>WriteSaveData</c> 把这个异常吞成一行错误日志（<c>SaveFileManager.cs:240-243</c>），
/// 所以游戏不崩，但**存档静默失败** —— 这才是危险的地方。</para>
///
/// <para>修法：在崩溃点之前把 null 的 <c>username</c> 换成游戏自己在
/// <c>OS.cs:372</c> 用的同一套回落
/// （<c>isConventionDemo ? ConventionLoginName : Environment.UserName</c>）。
/// 由于 <c>SaveUserAccountName</c> 为 null 时 <c>os.username</c> 正是由同一表达式算出的
/// （<c>OS.cs:372-373</c>），落盘文件名与 <c>os.username</c> 保持一致。</para>
/// </summary>
[BepInPlugin(Guid, "Hacknet Save Fix", "1.0.0")]
public sealed class SaveFixPlugin : BepInEx.Hacknet.HacknetPlugin
{
    internal const string Guid = "com.highcla.hacknetsavefix";

    public override bool Load()
    {
        HarmonyInstance.PatchAll(typeof(SaveFixPlugin).Assembly);
        Log.LogInfo("Hacknet Save Fix loaded.");
        return true;
    }

    public override bool Unload() => true;
}

/// <summary>把 null / 空白的用户名回落到游戏自己的默认账号名，使存档文件名可构造。</summary>
[HarmonyPatch(typeof(SaveFileManager), nameof(SaveFileManager.GetSaveFileNameForUsername))]
internal static class SaveFileNamePatch
{
    private static readonly BepInEx.Logging.ManualLogSource Log =
        BepInEx.Logging.Logger.CreateLogSource("Hacknet Save Fix");

    [HarmonyPrefix]
    private static void Prefix(ref string username)
    {
        if (!string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        // 与 OS.cs:372 逐字一致 —— 不另立一套规则。
        username = Settings.isConventionDemo ? Settings.ConventionLoginName : Environment.UserName;

        // 只在真的兜底时出声：这是异常路径，静默会把「有入口没设账号名」这件事藏起来。
        Log.LogWarning(
            "SaveUserAccountName was null - saving as '" + username + "'. "
            + "This means the OS was entered without going through the main menu.");
    }
}

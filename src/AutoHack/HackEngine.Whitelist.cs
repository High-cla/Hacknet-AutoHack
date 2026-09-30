// HackEngine.Whitelist.cs —— 白名单旁路（WhitelistConnectionDaemon）。
//
// HackEngine 的分部实现；类型声明、字段与其余职责见 HackEngine.cs。
namespace AutoHack;

using Hacknet;
using Pathfinder.Port;
using Pathfinder.Util;

internal static partial class HackEngine
{

    /// <summary>
    /// 抹除目标的 /log 目录，等价于原版终端 <c>rm log/*</c>；返回被删除的文件名，供回显与计数。
    ///
    /// 动作全部交给 <see cref="RemoveFiles"/> —— 痕迹清理与面板 <c>purge</c> 是同一个
    /// 「删光一个目录」的动作，只该有一份实现。
    ///
    /// 「删除动作本身不留新痕迹」的来源：/log 里的文件名形如
    /// <c>@&lt;时间&gt;_&lt;消息&gt;</c>（<c>Computer.log</c> 用
    /// <c>text.Replace(" ", "_")</c> 作 <see cref="FileEntry.name"/>，
    /// Computer.cs:338-354），以 '@' 开头 ⇒ deleteFile 跳过
    /// <c>log("FileDeleted: ...")</c> 自写（Computer.cs:543）。
    /// </summary>
    /// <summary>
    /// 该机器是否带白名单认证器（<c>WhitelistConnectionDaemon</c>）—— 连接会被它拒绝。
    ///
    /// 判定走 <c>getDaemon</c>，与游戏自己的取法一致（<c>Computer.connect</c> 内部即如此，
    /// Computer.cs:383）。判 daemon 有无而不是「这台机器会不会拒我」：后者要看
    /// <c>IPCanPassWhitelist</c>（Computer.cs:123-160）的完整语义（含
    /// <c>AuthenticatesItself</c>、<c>RemoteSourceIP</c> 递归、list.txt 逐行比对），
    /// 而真正的判据就在游戏里 —— 本方法只用来决定「要不要排一个绕过步骤」，
    /// 排了没生效也无害（见 <see cref="BypassWhitelist"/> 的返回值语义）。
    /// </summary>
    internal static bool HasWhitelist(Computer comp)
        => comp?.getDaemon(typeof(WhitelistConnectionDaemon)) is WhitelistConnectionDaemon;

    /// <summary>
    /// 把玩家 IP 追加进目标的白名单文件，让游戏下次放行连接。返回是否真的写进去了。
    ///
    /// <b>为什么不能只做一步</b>：白名单 daemon 有两种形态，放行文件不同 ——
    /// 普通型（无 `Remote`）建 `/Whitelist/list.txt`；而 `Remote=` 型改建 `source.txt`，
    /// **根本没有 list.txt**（WhitelistConnectionDaemon.cs:36-55）。官方任务
    /// PAE2_Target.xml、PA_Bookings_Mainframe.xml 都是这一型。故「追加自己的 IP」
    /// 只对前一种有效。
    ///
    /// <b>两条互补的路，都取游戏自己的判据</b>（`IPCanPassWhitelist`，
    /// WhitelistConnectionDaemon.cs:123-160）：
    /// <list type="number">
    /// <item><b>追加</b> —— `list.txt` 逐行 trim 比对玩家 IP（:151-158）。官方正路：
    ///   PAE2_Whitelist.xml 里的 `list_add_manual.txt` 明写「append list.txt &lt;你的IP&gt;」。</item>
    /// <item><b>删关键文件</b> —— 该函数有两处「文件不在就 return true」：
    ///   `authenticator.dll` 缺失（:142-145）、`list.txt` 缺失（:147-150）。
    ///   删掉任一即**无条件放行**，且这条不看 `AuthenticatesItself`、也不走
    ///   `RemoteSourceIP` 的递归委托 —— 是对付 `Remote=` 型的唯一办法。
    ///   这不是取巧：官方在 PAE2_Whitelist.xml 注释里就写着任务「is completable if
    ///   the player adds their own IP to the whitelist **or just crashes it's critical files**」。</item>
    /// </list>
    ///
    /// 两步都做（用户定）：先追加（正路、不破坏对方语义），再删两个文件把放行钉死。
    ///
    /// <b>为什么能改到它</b>：白名单只拦 `connect`（Computer.cs:383-388），文件系统
    /// 并未因此上锁 —— `deleteFile` 的门禁判 `ipFrom.Equals(adminIP)`（Computer.cs:511-517），
    /// 提权后即通过。故「先拿下这台机器，再改它的白名单」可行 —— 这正是本步骤
    /// 排在 Escalate 之后的原因。
    /// <returns>做了什么（供回显）；无可为时返回 null。</returns>
    internal static string BypassWhitelist(OS os, Computer comp, string playerIP)
    {
        var notes = new List<string>();

        // 认证源链要一起处理。判据照抄游戏的递归结构（IPCanPassWhitelist，
        // WhitelistConnectionDaemon.cs:129-141）：本机若带 source.txt，它的白名单
        // 实际由 source.txt 指向的那台裁决 —— 只改本机等于没改。
        foreach (var host in WhitelistChain(os, comp))
        {
            var note = BypassOne(host, playerIP, ReferenceEquals(host, comp));
            if (note != null)
            {
                notes.Add(note);
            }
        }

        return notes.Count == 0 ? null : string.Join("; ", notes);
    }

    /// <summary>
    /// 白名单裁决链：本机 + 它 <c>source.txt</c> 指向的认证服务器（若有）。
    ///
    /// 游戏是递归的：本机带 <c>RemoteSourceIP</c> 时把判决**委托**给那台的
    /// <c>IPCanPassWhitelist(ip, isFromRemote: true)</c>（WhitelistConnectionDaemon.cs:140）。
    /// 官方任务的典型拓扑就是这样：被保护的机器只有 <c>source.txt</c>（存认证服务器 IP），
    /// 认证服务器自己才有 <c>list.txt</c> —— 实测存档里 `dpa_bookings` 的 source.txt = 
    /// `26.217.89.33`（`dpa_whitelist` 的 IP），而 `dpa_whitelist` 的 list.txt 只列了
    /// 5 个 NPC IP。故**必须改认证服务器那台**，只改本机对它一点影响都没有。
    /// </summary>
    private static IEnumerable<Computer> WhitelistChain(OS os, Computer comp)
    {
        yield return comp;

        var source = comp?.files?.root?.searchForFolder(WhitelistFolderName)?.searchForFile(WhitelistSourceFilename);
        var remoteIP = source?.data?.Trim();
        if (string.IsNullOrEmpty(remoteIP))
        {
            yield break;
        }

        var remote = Programs.getComputer(os, remoteIP);
        if (remote != null && !ReferenceEquals(remote, comp))
        {
            yield return remote;
        }
    }

    /// <summary>
    /// 处理单台机器的白名单：追加玩家 IP，再删掉两个关键文件把放行钉死。
    /// </summary>
    /// <param name="isSelf">是否是被保护的本机（只为回显措辞区分）。</param>
    private static string BypassOne(Computer comp, string playerIP, bool isSelf)
    {
        var folder = comp?.files?.root?.searchForFolder(WhitelistFolderName);
        if (folder == null)
        {
            return null;
        }

        var notes = new List<string>();
        var tag = isSelf ? "" : "on " + comp.name + " ";

        // ① 追加（已在名单则不重复写 —— 幂等是硬要求，每轮都会跑）
        var list = folder.searchForFile(WhitelistListFilename);
        if (list != null && !AlreadyAllowed(list.data, playerIP))
        {
            list.data += "\n" + playerIP;
            notes.Add(tag + "IP appended to list.txt");
        }

        // ② 删两个关键文件，把放行钉死。
        //    直接从 List 移除，不走 Computer.deleteFile：它对每个非 '`' 文件调 log()，
        //    而 log() 是 searchForFolder("log").files.Insert(...)（Computer.cs:338-354），
        //    目标机没有 log 夹时 NRE —— 官方 Extension 的节点常常如此。
        //    这里只需要文件消失，不需要游戏的审计记录。
        var dropped = new List<string>();
        foreach (var name in new[] { WhitelistAuthFilename, WhitelistListFilename })
        {
            var file = folder.searchForFile(name);
            if (file != null)
            {
                folder.files.Remove(file);
                dropped.Add(name);
            }
        }

        if (dropped.Count > 0)
        {
            notes.Add(tag + "dropped " + string.Join(" + ", dropped));
        }

        return notes.Count == 0 ? null : string.Join("; ", notes);
    }

    /// <summary>
    /// 白名单文件里是否已有该 IP。判据照抄游戏：按换行切分、逐行 <c>Trim()</c> 后比对
    /// （Computer.cs:151-158）。
    /// </summary>
    private static bool AlreadyAllowed(string data, string playerIP)
    {
        if (string.IsNullOrEmpty(data) || string.IsNullOrEmpty(playerIP))
        {
            return false;
        }

        foreach (var line in data.Split('\n', '\r'))
        {
            if (string.Equals(line.Trim(), playerIP, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

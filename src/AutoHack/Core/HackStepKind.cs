// 核心层：入侵计划的动作种类。此处不得出现 Hacknet / Pathfinder / BepInEx / Harmony
// 的 using —— 该文件同时被插件与离线单测（tools/core-tests）编译。
namespace AutoHack.Core;

internal enum HackStepKind
{
    /// <summary>connect &lt;ip&gt;：建立会话，后续指令才落在目标上。</summary>
    Connect,

    /// <summary>解除目标的延迟反扑（<c>Computer.admin = null</c>）。非终端指令，故不回显。</summary>
    Neutralize,

    /// <summary>probe：读取目标端口表并回显原生格式报告。</summary>
    Probe,

    /// <summary>login：用已知账号密码登入（<c>Computer.login</c>），成功后直接提权，跳过全部破端口步骤。</summary>
    Login,

    /// <summary>绕过跳板：等价于过载跑完，但不等待那 30 秒。</summary>
    BypassProxy,

    /// <summary>&lt;破解程序&gt; &lt;端口&gt;：攻破一个端口。</summary>
    OpenPort,

    /// <summary>analyze + solve &lt;解&gt;：解目标防火墙。porthack 门禁要求防火墙已解（OS.cs:1918-1930）。</summary>
    SolveFirewall,

    /// <summary>porthack：提权。</summary>
    Escalate,

    /// <summary>投放标记文件（非终端指令，故不回显）。</summary>
    UploadMarker,

    /// <summary>rm /log/&lt;文件&gt;：抹除入侵痕迹。</summary>
    CleanLogs,

    /// <summary>dc：断开连接。追踪只在连着目标时推进，断开即让它失效。</summary>
    Disconnect,

    /// <summary>TraceTracker.stop()：直接毙掉进行中的追踪，零每帧开销。</summary>
    KillTrace,

    /// <summary>
    /// 白名单服务器被拒后的绕过：把玩家 IP 追加进目标 <c>/Whitelist/list.txt</c> 再重连。
    /// 这是游戏设计的正路（官方任务 PAE2_Whitelist.xml 的 list_add_manual.txt 明写
    /// 「append list.txt &lt;你的IP&gt;」），见 <see cref="HackEngine.BypassWhitelist"/>。
    /// </summary>
    BypassWhitelist,
}

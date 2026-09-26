# Hacknet 全自动入侵 Mod — 调研报告

调研日期：2026-09-26
目标游戏：`D:\steam\steamapps\common\Hacknet`（Pathfinder 5.3.4 已安装）
工作区：`D:\git\HacknetMod`

---

## 1. 结论摘要

**游戏原生已有「自动入侵」机制：`HackerScript`**（`Extensions/<Ext>/HackerScripts/*.txt`，由 XML `<LaunchHackScript>` 触发）。
本 Mod 采取**混合路径**：C# 命令做智能决策，底层调用**原生 API**（`openPort` / `giveAdmin` / `PortExploits`）与**原生 HackerScript 语义**（延迟节奏、日志清理）。

---

## 2. 可用扩展机制（按侵入度递增）

### 2.1 纯 XML（零代码）
- `Extensions/<Ext>/Actions/*.xml` — `ConditionalActions`，`<OnConnect target="..." needsMissionComplete="...">`
- `<LaunchHackScript Filepath TargetComp SourceComp DelayHost Delay RequireLogsOnSource RequireSourceIntact/>`
- 目录约定：`Actions/` `Missions/` `Nodes/` `Factions/` `Themes/` `HackerScripts/` `Docs/` `ExtensionInfo.xml` `Logo.png`

### 2.2 HackerScript 命令集（游戏原生自动入侵 DSL）

| 命令 | 作用 |
|---|---|
| `config [TARGET] [SOURCE] [delay]` | 设置目标/源与基线延迟 |
| `connect` / `disconnect` | 连接/断开 |
| `openPort N` | 开放端口 |
| `setAdminPass X` | 设置管理员密码 |
| `makeFile <folder> <name> <content>` | 创建文件 |
| `delete <folder> <file>` | 删除文件 |
| `forkbomb` | 崩溃目标（非玩家瞬间） |
| `instanttrace` / `trackseq` | 追踪 |
| `flash` / `hide*` / `show*` / `clearTerminal` | UI 效果（针对玩家） |
| `startMusic` / `stopMusic` / `openCDTray` / `closeCDTray` | 彩蛋 |
| `write` / `writel` / `write_silent` / `writel_silent` | 终端输出 |
| `delay N` | 延迟 N 秒 |

行内注释分隔符：`$#%#$`

### 2.3 Pathfinder C# API（已装 5.3.4）

**核心机制：属性自动扫描。**
`Pathfinder.Meta.Load.AttributeManager` 用 Harmony IL hook 挂在 `HacknetChainloader.LoadPlugin` 中对 `HacknetPlugin.Load()` 的调用点，注入 `ReadAttributesFor(plugin)`，自动扫描**整个程序集**（`type.Assembly.GetTypes()`）上的 `BaseAttribute` 派生特性并调用 `CallOn`。
→ 只需在插件类里标注属性，**无需**手动调用注册 API。

| 特性 | 目标 | 效果 |
|---|---|---|
| `[Command("name", addAutocomplete, caseSensitive)]` | `static void M(OS, string[])` | 注册终端命令 |
| `[Executable("#XML_NAME#")]` | class : BaseExecutable | 注册可执行程序 |
| `[Port]` | PortRecord 字段/属性 | 注册端口协议 |
| `[Daemon]` / `[Goal]` / `[Condition]` / `[Action]` | class | 注册对应扩展点 |
| `[Option]` / `[OptionsTab]` | 字段/类 | 插件选项 UI |

非属性式等价 API：
```csharp
ExecutableManager.RegisterExecutable<T>("#XML_NAME#");
PortManager.RegisterPort(protocol, displayName, defaultPort);
CommandManager.RegisterCommand(commandName, Action<OS,string[]>, addAutocomplete, caseSensitive);
DaemonManager.RegisterDaemon<T>();
AdministratorManager.RegisterAdministrator<T>();
GoalManager.RegisterGoal<T>("x");
ConditionManager.RegisterCondition<T>("x");
ActionManager.RegisterAction<T>("x");
```

### 2.4 Harmony
直接 patch（`base.HarmonyInstance.PatchAll(typeof(PatchClass))`）。

---

## 3. 游戏原生入侵 API（反编译实测签名）

反编译来源：`decompiled/game-proj/`（ilspycmd `-p`，358 文件）。
Pathfinder 5.3.4：`decompiled/pathfinder/`（134 文件）。

### 3.1 OS（`Hacknet/OS.cs`）
```csharp
public NetworkMap netMap;                    // :98
public Computer thisComputer = null;         // :128
public Computer connectedComp = null;        // :130
public void write(string text);              // :1726
public void writeSingle(string text);        // :1739
public void takeAdmin();                     // :1862  → connectedComp.giveAdmin(thisComputer.ip) + runCommand("connect ...")
public void takeAdmin(string ip);            // :1871  → Programs.getComputer(this, ip).giveAdmin(thisComputer.ip)
```

### 3.2 Computer（`Hacknet/Computer.cs`）
```csharp
public int securityLevel;                    // :41
public int portsNeededForCrack = 0;          // :45
public string adminIP;                       // :47
public List<int> links;                      // :51
public List<int> ports;                      // :53
public bool disabled = false;                // :59
public List<int> portsOpen;                  // (openPort 内使用)

public bool connect(string ipFrom);                                        // :377
public void log(string message);                                           // :319
public bool isPortOpen(int portNum);                                       // :801
public int GetDisplayPortNumberFromCodePort(int codePort);                 // :867
public int GetCodePortNumberFromDisplayPort(int displayPort);              // :876
public Folder getFolderFromPath(string path, bool createFoldersThatDontExist = false);  // :1628
public bool makeFile(string ipFrom, string name, string data, List<int> folderPath, bool isUpload = false);  // :653
public bool deleteFile(string ipFrom, string name, List<int> folderPath);  // :508
public bool PlayerHasAdminPermissions();                                   // :1668

public void giveAdmin(string ipFrom);        // :33077  → adminIP = ipFrom; log(ipFrom + " Became Admin")
public void openPort(int portNum, string ipFrom);   // :33090 → GetCodePortNumberFromDisplayPort(num) 后写入
public void setAdminPassword(string newPass);       // :33231
public void closePort(...);
```

**关键**：`log()` 实现（:319-360）把日志写入 `files.root.searchForFolder("log")` 的 `FileEntry` 列表，消息前缀 `@<elapsedTime>`，文件名 = 消息（空格→`_`，重名追加 `_N`）。
→ **清理日志** = 清空该 folder 的 `files`。

### 3.3 PortExploits（`Hacknet/PortExploits.cs`）
```csharp
public static List<int> portNums;             // :12
public static List<int> exeNums;              // :14
public static Dictionary<int,string> services;  // :16  端口→服务名
public static Dictionary<int,string> cracks;    // :18  端口→破解程序名
public static Dictionary<int,string> crackExeData;
public static Dictionary<int,string> crackExeDataLocalRNG;
public static Dictionary<int,bool> needsPort;   // :24
public static List<string> passwords;           // :36
public static void populate();                  // :38
```

端口↔破解程序映射（实测）：
| 端口 | 服务 | 破解程序 |
|---|---|---|
| 22 | SSH | SSHcrack.exe |
| 21 | FTP Server | FTPBounce.exe |
| 25 | SMTP MailServer | SMTPoverflow.exe |
| 80 | HTTP WebServer | WebServerWorm.exe |
| 3724 | Blizzard Updater | WoWHack.exe |
| 1433 | SQL Server | SQL_MemCorrupt.exe |
| 104 | Medical Services | KBT_PortTest.exe |
| 3659 | eOS Connection Manager | confloodEOS.exe |
| 443 | HTTPS (SSL) | — |
| 211 | Transfer | — |
| 32 | SignalScramble | — |
| 9418 | Version Control | — |
| 192 | Pacific Dedicated | — |
| 6881 | BitTorrent | — |

### 3.4 PortHackExe（原生图形化破解程序，`Hacknet/PortHackExe.cs`）
```csharp
public static float CRACK_TIME = 6f;
public static float TIME_BETWEEN_TEXT_SWITCH = 0.06f;
public static float TIME_ALIVE_AFTER_SUCSESS = 5f;
public static float COMPLETE_LIGHT_FLASH_TIME = 2f;
public override void Completed();   // → os.takeAdmin(targetIP); os.write("--Porthack Complete--")
```
→ 原生「延迟破解」节奏参考值。

### 3.5 Programs（`Hacknet/Programs.cs`）
```csharp
public static Computer getComputer(OS os, string ip_Or_ID_or_Name);   // :53794
```

### 3.6 ExeModule（可执行程序基类，`Hacknet/ExeModule.cs`）
```csharp
public static float FADEOUT_RATE = 0.5f;
public static float MOVE_UP_RATE = 350f;
public static int DEFAULT_RAM_COST = 246;
public int PID = 0;
public float fade = 1f;
public bool isExiting = false;
public bool needsRemoval = false;
public float moveUpBy = 0f;
public int ramCost = DEFAULT_RAM_COST;
public int baseRamCost = 0;
public string targetIP = "";
public bool needsProxyAccess = false;
public string IdentifierName = "UNKNOWN";
public ExeModule(Rectangle location, OS operatingSystem);
public override void LoadContent();
public override void Update(float t);
public override void Draw(float t);
public virtual void Completed();
public virtual void Killed();
public virtual void drawOutline();
public virtual void drawTarget(string typeName = "app:");
public Rectangle GetContentAreaDest();
```
（C# 12 主构造函数语法已用于反编译输出，实际 IL 为普通构造函数。）

### 3.7 NetworkMap（`Hacknet/NetworkMap.cs`）
```csharp
public List<Computer> nodes;                 // :47291
```

### 3.8 Folder（`Hacknet/Folder.cs`）
```csharp
public List<FileEntry> files = new List<FileEntry>();   // :24
public List<Folder> folders = new List<Folder>();       // :26
public string name;                                     // :28
```

### 3.9 FileEntry（`Hacknet/FileEntry.cs`）
```csharp
public string name;    // :14
public string data;    // :16
```

### 3.10 Pathfinder 5.3.4 便捷扩展（`Pathfinder.Port.ComputerExtensions`）
```csharp
public static void AddPort(this Computer comp, string protocol, int portNum, string displayName);  // :56
public static PortState GetPortState(this Computer comp, string protocol);   // :123
public static int CountOpenPorts(this Computer comp);                        // :164
public static void openPort(this Computer comp, string protocol, string ipFrom);  // :169
public static bool isPortOpen(this Computer comp, string protocol);          // :236
```

### 3.11 Pathfinder 5.3.4 其他
```csharp
// Pathfinder.Executable.BaseExecutable
public abstract class BaseExecutable : ExeModule {
    [Obsolete("To be removed in 6.0.0")] public virtual string GetIdentifier() => null;
    public string[] Args;
    public BaseExecutable(Rectangle location, OS operatingSystem, string[] args);
}

// Pathfinder.Command.CommandManager
public static void RegisterCommand(string commandName, Action<OS,string[]> handler,
                                   bool addAutocomplete = true, bool caseSensitive = false);  // :94

// Pathfinder.Executable.ExecutableManager
public static void RegisterExecutable<T>(string xmlName);
```

---

## 4. 构建环境

| 项 | 值 |
|---|---|
| dotnet SDK | 10.0.301（`C:\Program Files\dotnet`） |
| 目标框架 | `net472` |
| LangVersion | 10（项目模板）/ 可提高 |
| ImplicitUsings | enable（`Configurations.props:35`） |
| 游戏目录 | `D:\steam\steamapps\common\Hacknet` |
| 插件安装目录 | `Hacknet\BepInEx\plugins\` |
| 已装 PathfinderAPI | **本仓库框架**（`VERSION` 常量 5.3.4；develop 与 master 的 VERSION 均为 5.3.4，未随提交 bump） |
| 反编译器 | `ilspycmd` 10.1.1.8388 |
| 引用程序集 | `refs/assemblies/net472-pkg`（nuget `Microsoft.NETFramework.ReferenceAssemblies.net472` 1.0.3） |

**注意**：本机 dotnet build 会报 `MSB3644`（缺 .NET Framework 4.7.2 目标包）。两条解法：
1. `-p:TargetFrameworkRootPath=<绝对路径>\` 指向 `refs/assemblies/netfx-all`（**需 Windows 路径**，MSYS `/tmp` 不被 MSBuild 认可）。
2. 直接 `csc` 编译（上轮配方见 `tools/*.rsp`）。

**游戏程序集引用**（用于编译期绑定）：
- `Hacknet.exe`（游戏本体，含 `Hacknet` 命名空间）
- `FNA.dll`
- `BepInEx/core/0Harmony.dll`、`BepInEx.Core.dll`
- `BepInEx/plugins/PathfinderAPI.dll`、`BepInEx/core/BepInEx.Hacknet.dll`
- `libs/Mono.Cecil.dll` 等（按需）

---

## 5. 陷阱与约束

1. **版本号不可用于判断分支** — 本仓库 develop 与 master 的 `VERSION` 常量都是 `5.3.4`（develop 领先 16 提交但未 bump）。游戏目录装的即本仓库框架。编译以 `decompiled/pathfinder/`（游戏内 DLL 反编译）为准，与仓库源码一致。
2. `BaseExecutable.GetIdentifier()` 已 `[Obsolete]`，实现时避免覆写，或加 `#pragma warning disable CS0618`。
3. `PathfinderAPI/Replacements/FileEncrypterReplacement.cs:195` 用 `MemoryMarshal.AsBytes`，其 `System.Memory` 引用靠 `BepInEx.Hacknet.csproj:22` 传递，`PathfinderAPI.csproj` 未声明（既有脆弱点）。
4. **ImplicitUsings 需自建 `GlobalUsings.cs`**（项目模板里没有自动生成时）：
   ```csharp
   global using System;
   global using System.Collections.Generic;
   global using System.IO;
   global using System.Linq;
   global using System.Net.Http;
   global using System.Threading;
   global using System.Threading.Tasks;
   ```
   **不要加 `System.Numerics`** — 与 `Microsoft.Xna.Framework.Vector2` 冲突（CS0104）。
5. 本机 `Hacknet.exe` 已注入 BepInEx（1,469,952 B），`HacknetOld.exe` 为原版备份（1,477,120 B）。`PathfinderPatcher.exe` 安装后自删属正常。
6. `grep -ac 'BepInEx.Hacknet.Entrypoint' Hacknet.exe` 返回 0 是**误判信号** — `.cctor` 里是拼接/编码形式，非明文。

7. **属性扫描有加载顺序依赖 —— 必须声明 `[BepInDependency("com.Pathfinder.API")]`。**
   §2.3 那个 IL hook 是在 `PathfinderAPIPlugin.Load()` 里 `PatchAll` 时才安装的
   （`decompiled/pathfinder/Pathfinder/PathfinderAPIPlugin.cs:45` = `HarmonyInstance.PatchAll(typeof(PathfinderAPIPlugin).Assembly)`；
   hook 点见 `Pathfinder.Meta.Load/AttributeManager.cs:27`）。若本插件先加载，
   属性扫描不会覆盖到它 —— `[Command]` 注册的终端命令会**静默失效**：不报错、不打日志，
   终端里就是没有 `autohack` 这个命令。
   实测初始顺序 `AutoHack → AutoUpdater → PathfinderAPI`（错误），加依赖后变为
   `AutoUpdater → PathfinderAPI → AutoHack`（正确）。
   **这不是可选依赖，是正确性前提**；同理，任何依赖 Pathfinder 属性注册的插件都要声明它。

---

## 6. 待办

初版五项目标已在 v1.0 全部完成，改为状态记录：

| # | 目标 | 状态 |
|---|---|---|
| 1 | 确认最终设计（范围/附加能力） | ✅ v1.0（范围＝全网扫描＋当前节点；附加＝可控时序／自动清理／面板） |
| 2 | 搭建项目骨架（csproj + 源文件） | ✅ v1.0（net472 / LangVersion 13 / 直接输出到游戏目录） |
| 3 | 实现自动入侵核心 | ✅ v1.0，v1.2 迁移到 Pathfinder 框架 API，v1.3 补跳板与追踪 |
| 4 | 编译输出到游戏目录 `BepInEx/plugins/` | ✅ 每次 `dotnet build` 自动落地 |
| 5 | 游戏内验证 | ✅ 加载顺序／命令注册已由日志证实；**面板渲染、鼠标交互与终端回显的肉眼观感待真人确认**（无法自动化）。四工具同理 —— 反推内核、内存往返、程序补全判据均已用存档数据离线验通，按钮点击与观感仍需进游戏确认 |


---

## 追加调研（v1.1）：Pathfinder 接管端口状态 + 终端指令契约

来源：`decompiled/pathfinder/Pathfinder.Port/ComputerExtensions.cs`、`decompiled/game-proj/Hacknet/{OS,Programs,ProgramRunner,Computer,PortExploits}.cs`。

### 1. 关键陷阱：Pathfinder 用 Prefix 跳过了原版 openPort

```csharp
[HarmonyPrefix]
[HarmonyPatch(typeof(Computer), "openPort")]
private static bool OpenPortPrefix(Computer __instance, int portNum, string ipFrom)
{
    PortState portState = __instance.GetAllPortStates()
        .FirstOrDefault(x => x.Record.OriginalPortNumber == portNum);
    if (portState != null) portState.Cracked = true;
    __instance.log($"{ipFrom} Opened Port#{portNum}");
    if (!__instance.silent) __instance.sendNetworkMessage($"cPortOpen {__instance.ip} {ipFrom} {portNum}");
    return false;   // ← 原版实现被跳过
}
```

同理 `OpenPortsPrefix` 对 `Computer.openPorts()` 直接 `return false`。

**后果**：端口状态存在 Pathfinder 的 `ConditionalWeakTable<Computer, Dictionary<string,PortState>> PortTable` 里，
原版 `Computer.portsOpen`（`List<byte>`）**永不更新**。

**教训**：任何读端口开关的代码，若用 `comp.portsOpen[i] > 0`，在装了 Pathfinder 的游戏里**恒为 0**，
提权门槛 `已开端口数 > portsNeededForCrack` 永不成立。必须改用框架 API：

| 需求 | 正确 API | 位置 |
|---|---|---|
| 端口集合 | `comp.GetAllPortStates()` → `List<PortState>` | ComputerExtensions.cs:135 |
| 是否已开 | `state.Cracked` | PortState.cs:40 |
| code 端口 | `state.Record.OriginalPortNumber` | PortRecord.cs:11 |
| 显示名 | `state.DisplayName ?? state.Record.DefaultDisplayName` | PortState.cs:16 |
| 已开数量 | `comp.CountOpenPorts()`（=`GetAllPortStates().Count(x => x.Cracked)`） | ComputerExtensions.cs:164 |
| 显示端口 | `state.PortNumber`（玩家在终端看到的号） | PortState.cs:16 |
| 打开端口 | `comp.openPort(state.Record.Protocol, ipFrom)`（**协议名重载，框架新 API**） | ComputerExtensions.cs:222 |
| 关闭端口 | `comp.closePort(protocol, ipFrom)` | ComputerExtensions.cs:260 |
| 端口是否开 | `comp.isPortOpen(protocol)` | ComputerExtensions.cs:291 |
| 按协议取 | `comp.GetPortState(protocol)` | ComputerExtensions.cs:179 |
| 按号查记录 | `PortManager.GetPortRecordFromNumber(codePort)` | PortManager.cs:74 |
| 目标查找 | `ComputerLookup.Find(id)` / `FindById` / `FindByIp` / `FindByName` | Util/ComputerLookup.cs:50 |

原版机器（vanilla，来自地图 XML）的端口表由 `PortManager.LoadPortsFromStringVanilla` 初始化，
调用点：`decompiled/pathfinder/Pathfinder.Replacements/ContentLoader.cs:341`（EOS 另见 `:828`）、`SaveLoader.cs:356` → 原版机器**也有** PortState。

### 2. 终端指令契约（回显格式）

`OS`（decompiled/game-proj/Hacknet/OS.cs）：

| 成员 | 行号 | 说明 |
|---|---|---|
| `public Terminal terminal` | 96 | 终端实例（public 字段） |
| `public void write(string)` | 1726 | **纯输出，不执行** |
| `public void writeSingle(string)` | 1739 | 同上，不换行 |
| `public void runCommand(string)` | 1744 | `write("\n" + terminal.prompt + text)` + `terminal.lastRunCommand = text` + `execute(text)` |
| `public void execute(string)` | 1754 | `Split(' ')` 后起**后台线程** `threadExecute` → `ProgramRunner.ExecuteProgram` |

`Terminal.prompt` 是 `public string`（Terminal.cs:24）。

**回显配方（与 runCommand 逐字一致，但不触发执行）**：

```csharp
os.write("\n" + os.terminal.prompt + command);
```

### 3. 原生指令分派表（ProgramRunner.ExecuteProgram, ProgramRunner.cs:10+）

`connect` → `Programs.connect`；`disconnect|dc`；`ls|dir`；`cd`；`cd..`；`cat|more|less`；
`exe` → `Programs.execute`；`probe|nmap` → `Programs.probe`；`scp`；`scan` → `Programs.scan`；
`rm|del`；`mv`；`ps`；`kill|pkill`；`reboot`；`opencdtray`；`closecdtray`；`replace`；
`analyze`；`solve`；`clear`；`upload|up` → `Programs.upload`；`login`；`addnote`；`exe` 等。

### 4. 各指令的输出格式与前置条件（本插件按此对齐）

**`Programs.connect`（Programs.cs:231，全同步、可安全在游戏线程调用）**
先 `navigationPath.Clear()`；已连接则 `connectedComp.disconnecting(...)` + `write("Disconnected \n")`；
`terminal.prompt = "> "` → `write("Scanning For " + args[1])` → 在 `netMap.nodes` 里按 ip 或 name 找；
命中且 `node.connect(thisComputer.ip)` 为真 → `connectedComp=node` + `write("Connection Established ::")`
+ `write("Connected To name@ip")` + **`terminal.prompt = ip + "@> "`** + `visibleNodes.Add(i)` + 触达 boot daemon 的 `navigatedTo()`；
被拒 → `write("External Computer Refused Connection")`；找不到 → `write("Failed to Connect:\nCould Not Find Computer at ...")` + `connectedComp=null`。

**`Programs.scan`（Programs.cs:1258）** 带参按 ip/name 找到则 `discoverNode` + `write("Found Terminal : name@ip")`；
无参需 `hasConnectionPermission(admin:true)`，否则 `write("Scanning Requires Admin Access\n")`；
否则逐 link：`visibleNodes.Add` + `write("Found Terminal : ...")` + **`Thread.Sleep(400)`** + `write("Scan Complete\n")`。

**`Programs.probe`（Programs.cs:1384）** 作用对象是 `os.connectedComp ?? os.thisComputer`（**无目标参数**）：
`write("Probing ip...\n")` → 10 次（`Thread.Sleep(80)` + `writeSingle(".")`）→ `write("\nProbe Complete - Open ports:\n")`
→ `"---------------------------------"` → 逐端口 `write("Port#: "+GetDisplayPortNumberFromCodePort(ports[i])+"  -  "+services[ports[i]]+(portsOpen[i]>0?" : OPEN":""))` + `Sleep(120)`
→ `"---------------------------------"` → `write("Open Ports Required for Crack : " + Math.Max(portsNeededForCrack+1,0))`
→ `hasProxy` 时 `write("Proxy Detected : ACTIVE|INACTIVE")` → `firewall != null` 时 `write("Firewall Detected : SOLVED|ACTIVE")`。

> **注意**：probe 内部用 `Thread.Sleep`（合计约 2.8s）。**不能在游戏主线程直接调用**（会卡帧）。
> 本插件的 `ProbeReport` 复刻同一格式但**剔除 Sleep**，由 `OS.Update` 逐帧输出。

**`Programs.upload`（Programs.cs:738）/ `scp`（:580）/ `rm`（:948）** 都要求 `connectedComp != null`
（upload 还要求 `!= thisComputer` 且 `hasConnectionPermission(admin:false)`），否则 `write("Must be Connected to a Non-Local Host\n")`。
→ 这是「先 connect 再 probe / upload」设计的依据。

### 5. 破解指令名与端口取自游戏数据

`PortExploits.populate()`（PortExploits.cs:38）填充 `cracks[codePort] = "SSHcrack.exe"` 等：

| code 端口 | 服务 | 破解程序 |
|---|---|---|
| 22 | SSH | SSHcrack.exe |
| 21 | FTP Server | FTPBounce.exe |
| 25 | SMTP MailServer | SMTPoverflow.exe |
| 80 | HTTP WebServer | WebServerWorm.exe |
| 3724 | Blizzard Updater | WoWHack.exe |
| 1433 | SQL Server | SQL_MemCorrupt.exe |
| 104 | Medical Services | KBT_PortTest.exe |
| 3659 | eOS Connection Manager | confloodEOS.exe |
| 443 / 211 / 32 / 9418 / 192 / 6881 | SSL / Transfer / SignalScramble / Version Control / Pacific Dedicated / BitTorrent | — |

**端口语义**：`Computer.ports` 存 **code** 端口；展示用 `GetDisplayPortNumberFromCodePort(code)`，
输入用 `GetCodePortNumberFromDisplayPort(display)`（`PortRemapping` 为 null 时二者恒等）。
原生破解程序是**两参数**调用：`<crackername> <displayPort>`（如 `SSHCrack 22`），
由 AttackExe 自身在动画结束后调 `computer.openPort(code, os.thisComputer.ip)`。
→ 本插件把 exe 名去掉 `.exe` 转小写、拼上显示端口，作为回显指令（`sshcrack 22`）。

### 5. v1.2 迁移：原版 API → Pathfinder API

框架的 `[Obsolete]` 标注即官方迁移路线。本插件已全部对齐：

| 原版/旧 API | 新 API | 说明 |
|---|---|---|
| `comp.GetDisplayPortNumberFromCodePort(code)` | `state.PortNumber` | 框架给该方法是 Prefix 重写，直接读状态字段更直接 |
| `comp.GetCodePortNumberFromDisplayPort(d)` | `state.Record.OriginalPortNumber` | 同上 |
| `comp.openPort(codePort, ipFrom)` | `comp.openPort(protocol, ipFrom)` | 按协议名，前缀已接管原版签名 |
| 手写 `ports.Count(x => x.Cracked)` | `comp.CountOpenPorts()` | 框架封装 |
| `Programs.getComputer(os, id)` | `ComputerLookup.Find(id)` | `NodeLookup` Prefix 只挂在 `Programs.getComputer`；直连查找表免去一层 patch |
| `comp.portsOpen[i] > 0` | 删除 | 装框架后恒为 0，属**错误数据源**，不是「旧 API」 |
| `GetAllPorts()` / `GetPort()` / `GetPortDict()` / `RegisterPort(PortData)` | `GetAllPortStates()` / `GetPortState()` / `GetPortStateDict()` / `RegisterPort(PortRecord)` | `PortData` 全族 `[Obsolete("Avoid PortData")]`，本插件零引用 |

**5.3.4 vs 5.4.1 实证**：两个版本的 `Pathfinder.Port` 与 `Pathfinder.Util` 公开 API **逐字节一致**
（反编译成员清单 diff 为空），故本次迁移对两个安装版本均适用；`upstream/Hacknet-Pathfinder`
的 `HacknetChainloader.VERSION` 已是 5.4.1，游戏目录内仍是 5.3.4 安装版。

### 6. v1.3 游戏机制：跳板与追踪

两者都是「状态只在正确交互序列下才失效」的机制，不是可跳过的标志位。

#### 跳板 Proxy

字段（`Computer.cs`）：`:83 hasProxy`、`:85 proxyOverloadTicks`、`:87 startingOverloadTicks`、`:89 proxyActive`。
`addProxy(float time)`（`:243-251`）：`time > 0` 时 `hasProxy = proxyActive = true`，`proxyOverloadTicks = startingOverloadTicks = time`。

门禁（`OS.cs:2165`，`addExe` 内）：`if (exe.needsProxyAccess && computer.proxyActive) write("Proxy Active -- Cannot Execute")`。
标 `needsProxyAccess = true` 的 exe：`FTPBounceExe`、`FTPFastExe`、`HTTPExploitExe`、`MedicalPortExe`、`PacificPortExe`、`SMTPoverflowExe`、`SSHCrackExe`（`ExeModule.cs:31` 为默认 `false`）。**即绝大多数破解程序在跳板激活时都会被挡下。**

解除（`ShellExe.cs:94-105`，Overload 状态）：每帧 `destComp.proxyOverloadTicks -= t`；
归零时 `proxyOverloadTicks = 0f; proxyActive = false; completedAction(1)`；
未归零时每帧调 `destComp.hostileActionTaken()` —— **绕过跳板本身是一次持续的敌对动作，会触发追踪**。

其它引用：`DisplayModule.cs:606/661-674` 画 Proxy Detected/Bypassed 条（进度条 = `1 - proxyOverloadTicks / startingOverloadTicks`）；
`FastBasicAdministrator.cs:19-22` 与 `FastProgressOnlyAdministrator.cs:25-28` 在重载/读档时把 `hasProxy` 的机器重新置 `proxyActive = true; proxyOverloadTicks = startingOverloadTicks`（**跳板会复活，需重新过载**）；
`ComputerLoader.cs:295-296` 与 `Computer.cs:1089-1090` 清零；`Computer.cs:921/1021` 序列化属性 `proxyTime`。

#### 追踪 TraceTracker

字段（`TraceTracker.cs`）：`os` / `timer` / `startingTimer` / `active` / `timeSinceFreezeRequest` / `trackSpeedFactor` / `target`。
`start(float t)`（`:92`）：仅在 `!active` 时生效，`startingTimer = timer = t`，`target = os.connectedComp ?? os.thisComputer`，`active = true`。
`Update(float t)`（`:40`）：**若 `os.connectedComp == null` 或 `os.connectedComp.ip != target.ip`，立刻 `active = false`**（`timer < 0.5f` 时解锁成就 `trace_close`）；
否则每帧 `timer -= t * trackSpeedFactor * (Settings.AllTraceTimeSlowed ? 0.055 : 1)`，归零则 `active = false; os.timerExpired()`。
`stop()` 置 `active = false` 并复位 `trackSpeedFactor = 1`。`Draw` 在左下角画百分比。

触发链：`Computer.hostileActionTaken()`（`Computer.cs:294`）—— 仅当 `os.connectedComp != null && os.connectedComp.ip == ip` 时，
若 `traceTime > 0` 则 `os.traceTracker.start(traceTime)`；同时每 0.35s 闪一次圈。
调用方（= 会被追踪的敌对动作）：`PortHackExe.cs:88`、`SSHCrackExe.cs:90`、`FTPBounceExe.cs:72`、`FTPFastExe.cs:28`、`SMTPoverflowExe.cs:61`、`HTTPExploitExe.cs:59`、`SQLExploitExe.cs:63`、`ShellExe.cs:105`、`Programs.cs:1192`（analyze）、`DLCTraceSlower.cs:34`、`PacificPortExe.cs:26`、`RTSPPortExe.cs:32`、`SSLPortExe.cs:38`、`TorrentPortExe.cs:36`。

`traceTime` 来源：`Computer.cs:118` 构造 `-1f`；`:224` `Math.Max(10 - security, 3) * BASE_TRACE_TIME`（`BASE_TRACE_TIME = 15f`，`:29`）；`ComputerLoader.cs:381` 从 XML 属性读；`MissionFunctions.cs:210-212` 设 `BASE_TRACE_TIME * 7.5f`；`MissionGenerator.cs:101-103` 若 `<= 0` 则设 `10f * BASE_TRACE_TIME`。

归零后果：`OS.timerExpired()`（`OS.cs:1433`）—— 有 `traceCompleteOverrideAction` 则走它，否则 `connectedComp.admin.traceEjectionDetected`；`Flags` 含 `CSEC_Member` 走 `TraceDangerSequence.BeginTraceDangerSequence()`，否则 `thisComputer.crash()`。

反追踪程序：`TraceKillExe`（端口 12，`PortExploits.cracks[12] = "TraceKill.exe"`，`OS.cs:2041` 分发）——读 `os.traceTracker.timeSinceFreezeRequest`（`:89,93` 每 0.25s 冻结一次计时），`Draw` 打 "SUPPRESSION ACTIVE"，**只能减速不能中止**；
`DLCTraceSlower.cs:75` 降低 `trackSpeedFactor`。
**终端没有 `proxy` / `untrace` 这类命令**（`ProgramRunner.ExecuteProgram` 指令表全文见 §3），跳板靠 ShellExe 图形化 exe、反追踪靠 TraceKill.exe —— 二者都不是命令行。

#### 本插件的处理

| 机制 | 处理 |
|---|---|
| 跳板激活 + 有可破解端口 | 在破解前插入 `BypassProxy` 步：逐帧 `proxyOverloadTicks -= dt`，未归零则调 `hostileActionTaken()` 并打进度行；归零置 `proxyActive = false`（同 `ShellExe.cs:99-100`） |
| 追踪在跑 | 每个目标末尾 `Disconnect` 步（回显并执行 `dc`）。`TraceTracker.Update` 见 `connectedComp == null` 立即 `active = false` —— 断开是确定性中止手段 |
| 收尾时追踪仍 active | `AbortTrace`：补一次断开并把 `[autohack] trace was active - disconnected to abort it.` 写进终端 |

| 不想断开 | `stay` 参数（CLI）或面板 `disconnect after` 复选框关掉 |

### 7. v1.4 肉鸡（已控节点）判定与跳过

所有权标记只有 `Computer.adminIP`（`Computer.cs:47 public string adminIP`）：`giveAdmin(string ipFrom)`（`Computer.cs:738`）写入 `adminIP = ipFrom`；
`Computer.cs:921` 序列化为 XML 属性 `adminIP`，`Computer.cs:1012` 侧读回，Pathfinder 的 `Replacements/SaveWriter.cs:520` 与 `Replacements/SaveLoader.cs:228` 亦读写该属性 —— 故肉鸡状态**跨存档持久**。

**不是**所有权标记的：`Computer.admin`（`Computer.cs:103 public Administrator admin = null`）是任务的 `Administrator` 行为对象（`Administrator.cs`：`ResetsPassword` / `IsSuper` / `disconnectionDetected` / `traceEjectionDetected`），与玩家是否拿下无关。

游戏与 Pathfinder 都没有更上层的「已控」封装：`adminIP` 全树 144 处命中，除存档读写外全是 `adminIP == os.thisComputer.ip` 的即时比较，没有可复用的判定函数。故本插件直接用 `comp.adminIP == os.thisComputer.ip` 判定。

跳过策略：**只对全网扫描生效**。`here` 与显式点名的目标是刻意选择，一律尊重 —— 且重打已控节点本身是合法用法（重放／重置）。`redo` / `force` / `all-nodes` 三个别名或面板复选框可关闭过滤。



### 8. v1.5 面板 UI：为什么必须自绘

**三条实测理由（全部反编译核对过）：**

1. `CheckBox.doCheckBox(id, x, y, isChecked, color, text)`（`Hacknet.Gui/CheckBox.cs:53-60`）的完整实现是
   `if (GuiData.hot == myID) TextItem.doSmallLabel(new Vector2(x + 20, y - 20), text, null);` —— **标签只在悬停时才画**，
   而且画在复选框**上方 20px**（不在右侧）。这是旧面板「丑」的最大来源：一屏复选框全是无字方块。
2. `Button.drawButton`（`Hacknet.Gui/Button.cs:96-110`）用 `GuiData.tinyfont`（Font10）并用
   `Math.Min(scale.X, scale.Y)` 把文字缩放塞进按钮；字号无阶梯，长文案会被压扁。
   `drawModernButton` 还在 `width > 65` 时额外画一条 13px 宽的颜色标签条。
3. 隐藏地雷：`GuiData.UISmallfont` / `GuiData.UITinyfont` 两个字段**从未被赋值** ——
   `GuiData.InitFontOptions`（`Hacknet/GuiData.cs:107+`）只装入 `smallFont` / `tinyFont` / `bigFont`，
   `ActivateFontConfig`（`:181-190`）只赋 `smallfont` / `tinyfont` / `font` / `detailfont`。
   任何按字段名猜测去用 `UISmallfont` 的代码都会在 `MeasureString` 处 NRE，而该异常会被
   `OS.Draw` 的内层 catch（`OS.cs:1244-1251`）吞掉并写入 error 文件 —— 表现为「面板时有时无」。
   可用的只有 `GuiData.smallfont`（Font12）。

**原生控件的输入状态机**（自绘必须自己维护，否则两套控件抢同一次点击）：
`GuiData.hot` / `GuiData.active` / `GuiData.enganged`（`GuiData.cs:70-76`），
配合 `getMousePoint()`（`:245`，= `mouse - scrollOffset`）、`isMouseLeftDown()`（`:257`）、
`mouseLeftUp()`（`:262`，需要 `lastMouse` 按下且 `mouse` 抬起）、`mouseWasPressed()`（`:271`）。
`Button.doButton` 的完整模式见 `Button.cs:32-70`：命中则置 `hot`，按下时若 `active == -1` 则置 `active = myID`，
在 `active == myID` 上抬起才算点击并复位 `active = -1`。自绘控件照抄这套语义。

**主题取值**：`OS.cs:266-342` 的 `highlightColor`（默认 `(0,139,199)`）/ `terminalTextColor`（默认 `(213,245,255)`）
/ `subtleTextColor` / `outlineColor` / `lockedColor` / `unlockedColor` 是运行时可变字段（`CustomTheme` 与设置可改），
面板取前两个作为强调色与正文色，因此跟随主题。

**模态输入**：`GuiData.blockingInput` 是「本帧禁止下层控件响应」的开关，在 `GuiData.doInput()`（`:202-215`）里每帧先置 false。
游戏自身的模块用 `CoreModule.PreDrawStep`（`CoreModule.cs:22-30`）在绘制前置真来锁定输入。
本插件用同一手法，但放在 `OS.Draw` 的 **Prefix**：因为正文里的原生控件在 `Draw` 期间就消费 `GuiData.hot/active`，
Postfix 才置真已经太晚。条件限定为光标落在面板矩形内，面板之外不干扰游戏。

**命中测试必须与鼠标坐标同源**：`GuiData.getMousePoint()`（`:245`）= `mouse - scrollOffset`，
而 `DraggableRectangle`（`Hacknet.Gui/DraggableRectangle.cs:70-75`）拖动时用的是 `GuiData.mouse.X/Y` 原始坐标。
自绘控件一律走 `getMousePoint()`，避免与游戏控件对同一次点击产生分歧。

**字体阶梯**：`GuiData.tinyfont` = Font10（配置 "default"）/ Font12（"medium"）/ Font14（"large"）；
`GuiData.smallfont` = Font12 / Font14 / Font16。面板统一用 `smallfont` 加 0.9 / 1.0 / 1.1 / 1.3 缩放系数，
得到确定性的四级阶梯，不依赖玩家选的字号配置。

**布局常量**（`HackPanel.cs`）：宽 396、标题栏高 34、内边距 12、内容宽 372、两列各 182（含 8px 列距）；
行高 22（复选）/ 26（分段）/ 18（滑条）/ 16（战果行）；块间距 14；进度条分 10 段便于读刻度。

## 9. v1.6 反追踪重构：两个被忽略的原生机制

### 9.1 根因一：断开连接会触发管理员反扑（"全网扫描失去效果"）

```
Programs.disconnect(...)                     // 玩家主动断开
  -> os.connectedComp = null
OS.Update()                                  // 下一帧
  -> if (connectedComp == null && connectedIPLastFrame != null) handleDisconnection()   // OS.cs:813-817
  -> else if (connectedIPLastFrame != connectedComp.ip)          handleDisconnection()  // OS.cs:819-821
OS.handleDisconnection()                     // OS.cs:944-950
  -> computer = Programs.getComputer(this, connectedIPLastFrame)
  -> computer.admin?.disconnectionDetected(computer, this)    // ← 唯一触发点（全文件仅此一处）
```

`BasicAdministrator.disconnectionDetected`（`BasicAdministrator.cs:5-25`）：

```csharp
double time = 20.0 * Utils.random.NextDouble();          // 0~20 秒随机延迟
os.delayer.Post(ActionDelayer.Wait(time), delegate {
    if (os.connectedComp == null || os.connectedComp.ip != c.ip) {
        for (int i = 0; i < c.ports.Count; i++) c.closePort(c.ports[i], "LOCAL_ADMIN");
        if (ResetsPassword) c.setAdminPassword(PortExploits.getRandomPassword());
        c.adminIP = c.ip;                                 // ← 肉鸡标记被还原成机器自己
        if (c.firewall != null) { c.firewall.solved = false; c.firewall.resetSolutionProgress(); }
    }
});
```

**后果**：`giveAdmin(玩家IP)` 刚写进 `adminIP`，断开后 0~20 秒随机时刻被抹掉。全网扫描逐个 `dc` 等于逐个白跑 —— 表象就是「入侵完成了，过一会儿全没了」。

**Pathfinder 只补了一半**：`ComputerExtensions.cs:418-449` 对三个 Administrator 子类各挂了一个 `[HarmonyPostfix]`，把「围绕 `c.ports` 循环」换成「围绕 `GetAllPortStates()` 里 `Cracked` 的协议名循环」。它**没有**覆盖 `c.adminIP = c.ip` 这一行 —— 还原行为原样保留。

**关键时序**：那个延迟回调是在`断开的那一刻`才注册进 `os.delayer` 的，事后无法撤销。唯一可控的时点是**断开之前** —— 把 `Computer.admin` 置 null。

**为什么置 null 是安全的**：`Computer.admin` 是游戏原生字段（`Computer.cs:103`），`ComputerLoader.cs:416-425` 按 XML `type` 属性赋值，`type="none"` 就是 `null`；存档实测 148 个节点里 86 个是 `type="none"`。断开处的写法是 `computer.admin?.disconnectionDetected(...)`，空条件调用 —— null 即不注册回调。`admin` 只有两处使用：`disconnectionDetected`（OS.cs:949）与 `traceEjectionDetected`（OS.cs:1440-1442，追踪超时喂给管理员），后者在 `BasicAdministrator` 里是空的 `virtual`。玩家的所有权标记是 `adminIP`，与 `admin` 无关。

### 9.2 根因二：过载跳板时自点追踪

`Computer.hostileActionTaken()`（`Computer.cs:294-308`）全部作用只有两件事：

```csharp
public void hostileActionTaken() {
    if (os.connectedComp != null && os.connectedComp.ip.Equals(ip)) {   // 只在「连着本机」时生效
        if (traceTime > 0f) os.traceTracker.start(traceTime);           // ← 点燃追踪
        if (os.timer - timeLastPinged > 0.35f) { SFX.addCircle(...); timeLastPinged = os.timer; }  // 只是特效
    }
}
```

它**不参与** `proxyOverloadTicks` 的扣减（那是 `ShellExe` 在 `Update` 里自己做的，`ShellExe.cs:96-100`）。原生 `ShellExe.cs:105` 调它，只是因为「过载」在叙事上算敌对动作；对本插件而言，那句调用唯一的效果就是无谓地点燃 `TraceTracker`。故**移除**该调用。

> v1.7 起过载本身也改成一次性收敛（见 §10.1），这个逐帧循环整个消失了。

`TraceTracker.start(t)` 的关键在于 `target` 取的是**当前连接对象**：

```csharp
public void start(float t) { ...; target = os.connectedComp == null ? os.thisComputer : os.connectedComp; ... }
```

而 `Update` 在 `os.connectedComp == null || connectedComp.ip != target.ip` 时立刻 `active = false` 并解锁成就 `trace_close`。两条结论：追踪**不会**跨目标延续，且**只有断开（或换目标）能中止它** —— 这就是 `Leave()` 先解除反扑、再 `dc` 的原因。

### 9.3 重构落点

| 位置 | 改动 |
| --- | --- |
| `HackEngine.SuppressCounterattack(Computer)` | 新增。`comp.admin = null`，返回是否真的解除了一个（供回显） |
| `HackEngine.OpenPort(Computer, PortInfo, string)` | 新增。走**游戏自身签名** `comp.openPort(port.CodePort, ipFrom)`，替代框架的协议名重载 |
| `HackStepKind.Neutralize` | 新增步骤种类。独立成步而非挂在 `Connect` 上 —— `direct`／已连接的路径没有 `Connect` 步 |
| `HackRun.BuildSteps` | `Neutralize` 插在**侦察之前**，任何 scope 都覆盖 |
| `HackRun.OverloadProxy` | 删除 `target.hostileActionTaken()`。过载只扣 `proxyOverloadTicks` |
| `HackRun.Disconnect` → `Leave` | 先 `Neutralize` 再 `Programs.disconnect`，两件事合一 |
| `HackRun.AbortTrace` | 收尾补断开也改走 `Leave` |

**为什么 `Neutralize` 不放在 `Connect` 步里**：`HackRun` 会跳过「已连接的节点」的 `connect`（`alreadyConnected` 判断），`direct` 参数也会整段跳过连接 —— 这两条路径同样会被管理员反扑，必须独立成步。这也是「单一职责」的直接体现：解除反扑是目标级的准备动作，与「如何连上」无关。

**语言特性**：步骤表用 `readonly record struct`（不可变值语义，无堆分配）；`HackStepKind` 用 `enum` 而非字符串标记（编译期穷尽）；集合表达式 `["connect", target.ip]` 直接构造 `string[]`；`admin` 的空值检查用 `is null` 模式而非 `== null`（与 `CompilationRelaxations` 下的重载解析一致，也避免自定义 `==` 运算符介入）。


## 10. v1.7：目标集合改「可达遍历」，跳板去掉干等

> **注：本节的目标集合方案（多源 BFS over `links`）已在 v1.9 被推翻并删除** ——
> 它画错了图，「可连接」的判据应是 `netMap.nodes` 全表。见 §12.2。

用户要求：**优先游戏 API，其次框架 API，最后 mod 实现**；并解决两件事 —— 跳板要干等 30 秒、"扫描所有可连接的服务器" 要覆盖原版 scan 看不到的节点。

### 10.1 跳板：从逐帧过载到一次性收敛

**取证结论：游戏没有「立即完成过载」的 API。**

| 路径 | 结果 |
|---|---|
| 终端 `ComShell.exe -o` | `OS.cs:2134` → `ShellOverloaderExe` → `ShellExe.StartOverload()`（`ShellExe.cs:148-158`）。启动的还是同一个逐帧扣减的 `ShellExe` |
| `ShellExe.Update` case 1 | `ShellExe.cs:93-107`：`destComp.proxyOverloadTicks -= t` 直到 `<= 0f` 才 `proxyActive = false` |
| 耗时 | `addProxy(BASE_PROXY_TICKS * time)`，`BASE_PROXY_TICKS = 30f`（`Computer.cs:27`）→ 单次 30 秒 |
| Settings 开关 | 全文只有 `AllTraceTimeSlowed`（`Settings.cs:29`），无跳板加速项 |
| 框架封装 | Pathfinder 只在 `Executable/ExecutableManager.cs:120-122` 复刻了 "Proxy Active -- Cannot Execute" 提示 |

即三条路（游戏 API / 框架 API）都不提供快路径，落回 **mod 实现**：直接收敛游戏原生的 public 字段。

```csharp
// HackEngine.BypassProxy —— 与 ShellExe 过载跑完的终态逐字节相同
comp.proxyOverloadTicks = 0f;
comp.proxyActive = false;
```

对比 `ShellExe.cs:96-99` 的终态：`destComp.proxyOverloadTicks = 0f; destComp.proxyActive = false;` —— 完全一致。

**副作用审计（跳过那 30 秒安全吗）**：全游戏 `AchievementsManager.Unlock` 调用点共 12 处（`AdvancedTutorial` / `ClockExe` / `EndingSequenceModule` / `MissionFunctions` ×5 / `PointClickerDaemon` ×2 / `ThemeChangerExe` / `TraceTracker`），**没有一处与跳板相关**；唯一的追踪成就是 `TraceTracker.cs:70` 的 `trace_close`，条件是 `timer < 0.5f` 时断开。过载本身不推进追踪（`proxyOverloadTicks` 与 `traceTracker` 无任何数据流交集）。故跳过等待为零副作用。

> v1.5 曾判断「直接置 `proxyActive = false` 会跳过成就」—— 该判断是把追踪的成就误记到跳板名下，v1.7 已推翻并订正。

配套删除：`HackRun.OverloadProxy`（逐帧扣减 + 进度行）、`Tick` 里的跨帧分支。跳板步现在与其它步同构，吃一次 `NonPortDelay`（0.35s）即完成。

### 10.2 目标集合：从 visibleNodes 到可达遍历

**原版 scan 的语义**（`Programs.cs:1258-1299`）：无参时 `computer2 = connectedComp ?? thisComputer`，然后**只遍历 `computer2.links` 一跳邻接**，每节点 `Thread.Sleep(400)`。带参时在 `netMap.nodes` 里按 ip/name 找一个节点 `discoverNode`。

即原生 scan 一次只发现当前节点的一跳邻居，图上更远的机器看不到。

**"可连接" 的真实语义**：`Programs.connect`（`Programs.cs:231-322`）遍历 `netMap.nodes` 按 ip/name 匹配 + `nodes[i].connect(os.thisComputer.ip)` 做白名单检查 —— **完全不检查 `links`**。所以「可连接」在技术上等于「节点表里存在且未 disabled」，`links` 只决定**可见性**与叙事上的网络拓扑。

**实测存档的图有多稀疏**（`save_1.xml`，148 节点）：

| 指标 | 值 |
|---|---|
| `<links>` 非空的节点 | 44 / 147 |
| 玩家机（`nodes[0]`，id=`playerComp`）的 links | `0 1` |
| `nodes[1]` 的 links | 空 |
| `<visible>` | 7 个：`0 1 7 2 99 112 113` |
| 可见性覆盖 | 7 / 148 = 4.7% |

**踩坑记录**：第一版实现是**单源 BFS（只从玩家机出发）**。用真实存档回放后发现它会退化成 **2 个节点**（玩家机 links 只有 `0 1`，而 `nodes[1].links` 为空即止），比原来的 `visibleNodes`(7) **更少** —— 典型的"改完更差"。修正为**多源种子**（玩家机 + 全部 `visibleNodes`），继续向外 BFS：

| 算法 | 目标数 |
|---|---|
| 单源（仅玩家机） | 2 ❌ |
| 旧实现（仅 visibleNodes） | 6 |
| **多源 BFS（v1.7）** | **7**（多发现 node 108） |
| 理论上限（全部未禁用节点） | 147 |

多源保证**永不退化**（种子集已含全部已知节点），同时能越过原版 scan 的一跳极限。

**发现动作用游戏 API**：新展开的节点全部经 `NetworkMap.discoverNode(Computer)`（`NetworkMap.cs:415`）标记 —— 与真人敲 scan 的后效逐字一致（进 `visibleNodes` + `highlightFlashTime = 1f` + `lastAddedNode`），不做自绘的"伪发现"。

### 10.3 API 优先级复核（游戏 → 框架 → mod）

| 用途 | 采用 | 层级 |
|---|---|---|
| 端口状态读取 | `Computer.GetAllPortStates()` | 框架 |
| 端口攻破 | `Computer.openPort(int portNum, string ipFrom)` | **游戏签名**（框架 Prefix 已接到端口表） |
| 已开放端口数 | `comp.CountOpenPorts()` | 框架 |
| 连接 | `Programs.connect` | **游戏** |
| 断开 | `Programs.disconnect` | **游戏** |
| 目标查找 | `ComputerLookup.Find` | 框架（且框架已 Prefix 接管 `Programs.getComputer`，语义等同） |
| 节点发现 | `NetworkMap.discoverNode` | **游戏** |
| 提权 | `Computer.giveAdmin` | **游戏**（刻意不用 `OS.takeAdmin`，它内部会 `runCommand("connect")` 触发断开反扑） |
| 反扑解除 | `comp.admin = null` | **游戏字段**（无任何上层 API，见 §9.1） |
| 跳板解除 | `proxyOverloadTicks` / `proxyActive` | **游戏字段**（无快路径 API，见 §10.1） |

### 10.4 语言特性

- 可达遍历用 `HashSet<int>` + `Queue<int>` 标准库结构，手写 BFS 而非递归（深图不爆栈）。
- 种子入队抽成 `Seed(NetworkMap, HashSet<int>, Queue<int>, int)`，消除「玩家机」与「visibleNodes 循环」两处的边界判断重复（DRY，且该判断原先写了两遍）。
- `found.ToArray()` 而非返回 `List`：与同文件 `DiscoveredComputers` 的返回类型统一，`ResolveTargets` 里 `pool.Length` 才能编译（`IReadOnlyList` 无 `Length`）。
- 跳板收敛是两行直接赋值，不做`事件/回调`式的"通知跳板已绕过"抽象 —— 没有第二个消费者（YAGNI）。

### 10.5 验证

反编译产物 `decompiled/autohack-v7/AutoHack.decompiled.cs`（1792 行）：

| 检查项 | 结果 |
|---|---|
| `OverloadProxy` | 0（已删） |
| `hostileActionTaken` | 0（从不调用） |
| `ReachableComputers` | 2（定义 + 调用）—— **v1.9 已删除，见 §12.2** |
| `discoverNode` | 1 |
| `BypassProxy` | 7 |
| `SuppressCounterattack` | 2 |
| `Seed` | 3 |
| `proxyOverloadTicks = 0f` | :317 |
| `proxyActive = false` | :318 |
| 版本 | `1.7.0`（v1.8 复核见 §11） |

构建：`0 个警告 0 个错误`；产物 `AutoHack.dll` 44032 B（v1.6 为 43520 B）。

## 11. v1.8：坏味道审计与并发修正

用户要求「更新索引。查找反模式死代码代码坏味道性能问题，优先使用语言特性异步多线程并发协程等优化手段」。
审计对象是插件自身 1712 行源码，逐条给证据与修法。**不追求条目数量，只改有实据的问题**。

### 11.1 并发：三处真实缺陷（本轮的实质发现）

这一节是本次审计最有价值的部分 —— 三个都是会真实出问题的缺陷，不是风格问题。

#### (a) 跨线程队列

证据链：`AutoHackCommand` 由 `OS.execute` 执行，而后者是
`new Thread(threadExecute)` 派生线程（OS.cs:1754-1767，日志里的
`Spawning thread for command autohack` 就是它）。命令线程写入，
游戏线程在 `OS.Update` Postfix 里读取 —— 原实现用 `Queue<T>`，
不是线程安全的。

修法不是加 `lock`（会引入新的阻塞点），而是换 BCL 并发集合。

#### (b) `visibleNodes` 的跨线程改写（比 (a) 更隐蔽）

`HackRun` 的**构造函数**里就有一次可达遍历，而遍历会调
`NetworkMap.discoverNode` 把新节点写进 `netMap.visibleNodes`
（NetworkMap.cs:415-431：`visibleNodes.Add(...)`）。

原实现里 `HackRun` 是在**命令线程**构造的（`AutoHackCommand` 直接
`new HackRun(os, options)`），于是命令线程对 `visibleNodes` 做 `Add`，
而游戏线程每帧都在遍历同一个 `List<int>`（`HubServerAlertsIcon.cs:123`
读它、`DLCIntroExe` 读它、`EndingSequenceModule` 改它）——
典型的「一个线程 Add、另一个线程 foreach」，轻则
`InvalidOperationException: Collection was modified`，重则索引错位。

**修法**：入队只传参数，把 `HackRun` 的构造推迟到游戏线程首帧
（`PendingRuns.OnOSUpdate`）。`Enqueue(OS, HackRun)` 因此改成
`TryEnqueue(OS, HackOptions)`。

#### (c) 两条运行同时改连接状态

面板的 RUN 按钮与 `autohack run` 命令**互相不知道对方存在**，
二者可以同时推进：两条 `HackRun` 各自 `Programs.connect` /
`Programs.disconnect` 同一个 `os.connectedComp`，把对方正在攻打的
目标换掉 —— 结果是两端都「失去效果」。

**修法**：单写者仲裁。面板入口查 `PendingRuns.BusyFor(os)`，
命令入口查 `HackOverlay.IsRunning` 并让 `TryEnqueue` 原子地拒绝重复入队。

#### 为什么最终用 `ConcurrentDictionary<OS, Entry>` 而非 `ConcurrentQueue`

中间版本用过 `ConcurrentQueue`，但暴露了两个问题，故换掉：

| 问题 | `ConcurrentQueue` | `ConcurrentDictionary` |
|---|---|---|
| 跨 OS 实例堵塞 | `TryPeek` 只看队首；换过 OS（回主菜单再进）后旧条目卡在队首，新实例永远轮不到 | 按实例键控，各走各的 |
| 重复入队判定 | `Count`+`Peek` 再 `Enqueue` 是 check-then-act，中间有窗口 | `TryAdd` 天然原子 |
| 互斥查询 | 需遍历整个队列 | `ContainsKey` O(1) |

`Entry.Run` 初值为 null 作为「尚未在游戏线程构造」的标记，
构造完成后才赋值 —— 这个可空字段是刻意的状态机，不是疏忽。

#### 为什么不用 `async`/`await` 与协程

用户要求「优先使用语言特性异步多线程并发协程等优化手段」。本项目的判断是：
**并发该用，异步不该用** —— 理由是可核查的，不是偏好。

`async`/`await` 在 C# 里的语义是「在 `await` 处把后续代码交给线程池**或**某个
`SynchronizationContext` 继续执行」。而本插件要做的每一件事 ——
`target.openPort()`、`Programs.connect()`、`computer.files` 的增删 ——
都只能碰游戏对象状态，必须在**游戏主线程**上执行。用 `async` 的结果是：
`await` 之后的代码跑在**线程池线程**上，与游戏线程同时触摸 `visibleNodes`、
`connectedComp` 等非线程安全字段 —— 正是 11.1(b) 描述的那类缺陷。

游戏本身的架构也印证这一点：它只有**一个**位置把工作丢给派生线程
（`OS.execute`，OS.cs:1754），且那里的 `threadExecute` 只做终端的文本处理。
真正的状态变更全部由 `OS.Update` 在主线程逐帧推进 —— 本插件的
`HackRun.Tick` 正是同一个模式。

至于「协程」：Hacknet 基于 FNA/MonoGame，其更新循环是
`Update(GameTime)` 每帧回调一次，没有 Unity 那样的 `IEnumerator` 协程原语。
等价物就是本插件已有的做法 —— 把长动作拆成 `HackStep` 序列，
每帧在 `OS.Update` Postfix 里推进一个（`_timer` 累积到 `delay` 才 `Apply`
一步）。这就是协程语义，且天然在主线程上。

**结论**：状态变更同步在主线程（逐帧状态机），跨线程只传**不可变参数**
（`HackOptions` 是 `record`），用 BCL 并发集合兜住共享容器。
`async` 在这里只会把「主线程顺序执行」拆成「不确定的线程池调度」，纯属负收益。

### 11.2 死代码

| 符号 | 判定依据 | 处置 |
|---|---|---|
| `HackRun.Elapsed` | 全仓仅两处：声明 +`+= deltaSeconds`；无任何读取者 | **删除**（连累加一起删） |
| `HackPanel.AtMost` | 仅包装 `source.Take(count)`，两处调用点改为 `for` 后无引用 | **删除** |
| `HackRun.Tick` 返回值 | 调用方（`HackOverlay`/`PendingRuns`）均丢弃；文档注释自称「供日志节流」但从未节流 | 改 `void` |
| `HackPanel.Palette.Bad` | 有使用者（收起/展开态关闭按钮），**保留** | — |

`Tick` 的返回值是 YAGNI 的典型：为不存在的需求（日志节流）预留了接口。

### 11.3 性能：每帧执行路径上的三处浪费

面板绘制每帧跑 60 次，是唯一的持续开销来源。

| 问题 | 原实现 | 修法 |
|---|---|---|
| 字符串每帧重复大写 | `run.Phase?.ToUpperInvariant()` 在 `DrawRunning` 里，每帧对同一字符串转换一次 | Phase 在**赋值处**一次性大写（12 个赋值点），绘制期直接画 |
| 超长文本截断 | `Ellipsize` 逐字符 `Substring` + 每轮重测 `MeasureString`，O(长度) 次测量 | 首字符宽度比例估算 + 常数步向两侧微调；结果与逐字符法一致 |
| 每帧 LINQ 分配 | `AtMost` 用 `Take`，每次遍历分配枚举器 | 改 `for` 循环索引，并引入 `MaxOutcomeRows` 常量替代裸 `5` |

`Segment`/`Check`/`Outcome` 的标签测量无法省（文本长度动态），保持原样。

### 11.4 算法：两处超线性查找

| 位置 | 原复杂度 | 修法 |
|---|---|---|
| `ResolveTargets` 去重 | `List.Contains` → O(n²) | `HashSet<Computer>` → O(n) |
| `ReachableComputers` 展开循环 | `map.visibleNodes.Contains(next)`，`visibleNodes` 是 `List<int>` → O(V·E) | 先摊平成 `HashSet<int>` 供 O(1) 判「已发现」 |

显式目标串可能重复点名同一台机器（用户手抖写两遍），全网池则可能因
多源种子重叠而重复，两者都走同一条去重路径。

### 11.5 一处布局 bug（自查）

`MaxY` 原按 `CollapsedHeight`（30px）算纵向上限，但展开态面板高数百像素 ——
拖到屏幕底部时面板会坠出可视区。改为按**当前帧实际高度**约束，`Drag` 与
`ResolveOrigin` 都传入 `LastFrame.Height` / 当次高度。

### 11.6 注释漂移

| 位置 | 漂移 |
|---|---|
| `HackOverlay` catch 注释 | 自称「内层 catch 可能吞异常并留下未关闭批次」，但 `finally` 里 `begun` 时确会 `End()`，批次不会泄漏 |
| `HackScope.Network` 注释 | 仍写「全部已发现节点」，而 v1.7 已改为「从玩家机与已发现节点出发的可达集合」 |
| CLI help 的 `here` 说明 | 写 `default: whole network`，实际已是可达集合口径 |

三处均已订正。注释漂移比无注释更坏 —— 它主动误导后续维护者。

### 11.7 codebase-memory 索引退化（工具链修正）

**现象**：索引从 25470 节点塌成 360 节点，`not_indexed` 指向 `decompiled/`。

**根因**：`.gitignore` 为发布需要排除了 `decompiled/`（游戏反编译源码，版权物），
而 codebase-memory **把 `.gitignore`、`.git/info/exclude` 与 `.cbmignore` 一并
当忽略源读取**。`.cbmignore` 里写的「decompiled/ 与 upstream/ 必须保留」因此无效。

**验证过程**（逐步排除，非猜测）：
1. `.cbmignore` 追加 `!decompiled/` 取反行 → 无效（不认取反语法）。已回滚。
2. 把两项从 `.gitignore` 挪到 `.git/info/exclude` → 仍无效（同样被读）。
3. 临时移除 `.git/info/exclude` 中两行 → **索引立刻恢复到可索引状态**
   （`excluded` 列表里 decompiled 消失，出现 42 个 `ignored-suffix` 的 PNG）。
   决定性证据，确认三个文件都参与忽略判定。
4. 全部回滚 —— 版权物必须留在 git 之外，不能为一时的索引便利牺牲发布正确性。

**结论**：这是无法两全的约束，不是可修的缺陷。当前 360 节点的索引正好覆盖
插件源码本身（89 个符号，`search_graph`/`trace_path` 可用）。需要查游戏或框架
API 时直接用 grep 搜 `decompiled/` 与 `upstream/`。已在 `.cbmignore` 里写明
这一限制与验证过程，避免下次重踩。

### 11.8 验证

- 构建：`dotnet build src/AutoHack/AutoHack.csproj -c Release` → 0 警告 0 错误。
- 反编译核对（`decompiled/autohack-v8`，1852 行）：

| 类别 | 项 | 结果 |
|---|---|---|
| 应消失 | `AtMost` / `float Elapsed` / `private static readonly Queue` | 0 / 0 / 0 |
| 修复就位 | `ConcurrentDictionary<OS` / `TryEnqueue` / `BusyFor` / `IsRunning` / `TryAdd` | 1 / 2 / 2 / 2 / 1 |
| 修复就位 | `internal void Tick` / `MaxOutcomeRows` / `MaxY(Rectangle, int)` / `Upper(string)` | 1 / 1 / 1 / 1 |
| 内核无改动 | `ReachableComputers` / `discoverNode` / `BypassProxy` / `SuppressCounterattack` | 2 / 1 / 7 / 2 |
| 内核无改动 | `OnOSDrawPrefix` / `proxyOverloadTicks = 0f` / `Programs.disconnect` | 1 / 1 / 1 |
| 必须为 0 | 原生控件 + `UISmallfont`/`UITinyfont` / `hostileActionTaken` / `Thread.Sleep` | 0 / 0 / 0 |

- 版本：`AutoHack", "1.8.0"`。

未变的是内核语义：目标集合、跳板绕过、反追踪、管理员解除四条链路本轮**零改动** ——
它们已在 v1.7 验证过，本次只动了外围。


---

## §12 v1.9：原生命令与机制补齐，目标集合改为「全部可连接」

用户报四条：①「还有原生命令和机制没有完整利用」②「可以直接敲 IP 的服务器可以连接没有找到」
③「已连接的服务器依旧会删 log」④「还有一些服务器压根没做入侵成功后的处理，每一次判断都是要入侵」。
四条根因全部来自源码与真实存档，互不重叠。

### 12.1 根因一：porthack 门禁被整体绕过（原生命令未利用）

游戏自己的 porthack 有硬门禁 —— `OS.cs:1908-1930`：

```
num2 = Σ connectedComp.portsOpen[i]          // 已攻破端口数
if (num2 > portsNeededForCrack) flag = true
if (connectedComp.firewall != null && !firewall.solved) {
    if (flag) flag2 = true;                   // → "Target Machine Rejecting Syndicated UDP Traffic"
    flag = false;                             // → 拒绝启动 PortHackExe
}
```

即「端口数过关」**且**「防火墙已解」才放行 `new PortHackExe(...)`。
Pathfinder 用 ILManipulator（`ComputerExtensions.cs:481-500`）只把前半段换成
`connectedComp.CountOpenPorts()`，**防火墙条件原样保留**。

而 v1.8 及之前，mod 在 `HackRun.Apply` 里直接调 `target.giveAdmin(ip)` —— 整条门禁跳过，
原生的 `PortHackExe`、`analyze`、`solve` 三条机制一次都没走到。存档实证：
147 个节点里 **39 个带 `<firewall>`**，这些机器等于从未被正确处理。

处置（v1.9）：

| 环节 | 实现 | 依据 |
|---|---|---|
| 提权门槛 | `CanEscalate` = 端口数 > 门槛 **且**（无防火墙 或 已解） | `OS.cs:1908-1930` 逐条对齐 |
| 解防火墙 | `Firewall.attemptSolve(firewall.solution, os)` | `Firewall.cs:101-116`，同玩家敲 `solve` |
| 解序列来源 | `Firewall.solution`（游戏自己生成，public 字段） | `Firewall.cs:20`；不必等 `analyze` 逐趟收敛 |
| 新步骤 | `HackStepKind.SolveFirewall`，排在 `Escalate` 之前 | 门禁要求 |

**为什么不用 `Programs.solve`**（那才是玩家敲 `solve` 的入口）：
它内层先跑 `doDots(30, 60)`，每次 `Thread.Sleep(Utils.DebugGoFast() ? 1 : 60)`
（`Programs.cs:18-25`），30 点合计约 1.8 秒**阻塞游戏线程**。
`attemptSolve` 本身是纯判断（`attempt.ToLower() == solution.ToLower()`），无阻塞。
故取同一个判断入口，绕开阻塞的装饰层 —— 与 probe 的处理同构
（v1.0 起就是「自实现报告、不调 `Programs.probe`」，理由同样是 `Thread.Sleep`）。

**`analyze` 刻意不回显**：它只产生给人看的逐趟提示（`generateOutputPass` 是个
协程，`analysisPasses` 每跑一次才多揭示 3 个字符），对 `solved` 没有任何影响 ——
唯一置 `solved = true` 的地方是 `attemptSolve`。回显一条不执行的命令是欺骗，
故只回显真跑的 `solve <解>`。

### 12.2 根因二：可连接集合画错了图

`Programs.connect`（`Programs.cs:231-322`）的实际逻辑：

```
for (int i = 0; i < os.netMap.nodes.Count; i++) {
    if (nodes[i].ip != args[1] && nodes[i].name != args[1]) continue;
    if (nodes[i].connect(os.thisComputer.ip)) { ...成功... }
}
```

**全程不检查 `visibleNodes`** —— 地图上任何节点都是「敲 IP 就能连」。
`visibleNodes` 只是原版 `scan` 维护的「已发现」展示标记（`Programs.cs:1282-1292`，
且每次发现都 `Thread.Sleep(400)`），不是连接许可。`Computer.connect`
（`Computer.cs:377-397`）只拒两类：`disabled`，以及 WhitelistConnectDaemon 不放行。

v1.7/v1.8 的 `ReachableComputers` 按 `visibleNodes` + `Computer.links` 做多源 BFS，
方向错了。实测存档 save_1.xml（147 节点）：

| 口径 | 结果 |
|---|---|
| 玩家机 `<links>` | 仅 `0 1` |
| 其后节点 `<links>` | 103 个为空、31 个只有 1 条 |
| BFS 多源结果 | 7 个目标 |
| 游戏实际情况 | 随手敲任意 IP 都能连上 |

处置：删掉整个 BFS（含 `Seed` 辅助与 `DiscoveredComputers` 兜底），
换成 `ConnectableComputers` —— 遍历 `netMap.nodes` 全表，只排除玩家机
（`disabled`、已控、不可提权交由 `ResolveTargets` 统一过滤）。
实测同一存档得 **110 个可提权目标**（见 12.4）。

顺带消除一个隐患：旧 BFS 会调 `NetworkMap.discoverNode` 改写 `visibleNodes`，
而游戏线程每帧都在遍历那个 `List<int>`（`HubServerAlertsIcon.cs:123` 等）——
v1.8 为此专门把 `HackRun` 构造推迟到游戏线程。新实现是纯读，不再触碰该表。

### 12.3 根因三：清痕与断开的先后 —— 本节结论已被 §14.12 推翻

> ⚠️ **本节的历史结论是错的，保留在此仅作记录。** 当时把 `CleanLogs` 挪到
> `Disconnect` **之后**，理由是「断开自身会往 /log 写一条 Disconnected」。
> 这只对了一半：`rm` 这类命令的**目标机取自连接**（`Programs.cs:956` 取
> `os.connectedComp`），断开之后再清，回显的 `rm` 就是条假命令 ——
> 与玩家手敲「没有效果」是同一个坑。正确解法是清痕排在断开**之前**，
> 再把断开本身静音（`Computer.silent`）。详见 §14.12。

`Computer.disconnecting`（`Computer.cs:722-727`）：

```csharp
public void disconnecting(string ipFrom, bool externalDisconnectToo = true) {
    if (!silent) log(ipFrom + " Disconnected");     // ← 又写一条 /log
    ...
}
```

而 `Programs.disconnect`（`Programs.cs:326-360`）第一件事就是调它。
v1.8 的步骤顺序是 `CleanLogs → Disconnect` —— 清完立刻又追加一条
`"124.205.173.252 Disconnected"`，痕迹原样留存。这就是「已连接的服务器依旧会删 log」
（看起来删了，其实目标上还有记录）。

~~处置：把 `CleanLogs` 挪到 `Disconnect` 之后，成为本目标的**最后一步**。
新顺序：`… → UploadMarker → Disconnect → CleanLogs`。~~

~~连带修正回显的真实性：清痕时已不在目标上，再回显 `rm /log/xxx` 就是假命令。
故按 `os.connectedComp == null` 分支 —— 连着才回显真 `rm`（`here`／已连接路径），
否则写一行状态 `[autohack] <名> :: wiped N log file(s)`。~~

**以上处置在 v1.11.3 被推翻**：真正的顺序是 `CleanLogs → Disconnect`，
断开改为静默。详见 §14.12。

### 12.4 根因四：无提权可能的机器每次都被打

门槛由游戏自身定义 —— `openPortsForSecurityLevel`（`Computer.cs:200-204`）：

```csharp
portsNeededForCrack = security - 1;
if (security >= 5) portsNeededForCrack--;      // 安全级 ≥5 再减 1
```

提权需 `已攻破端口数 > portsNeededForCrack`，**而端口数是这张表的容量上限**。
故「端口表容量 ≤ 门槛」= 永远开不满 = 永远提不了权。
`CanEverEscalate` 即该判据，全网扫描一律剔除。

存档实证（save_1.xml，147 节点）：

| 剔除理由 | 数量 | 判据 |
|---|---|---|
| 玩家机 | 1 | 自身 |
| `disabled` | 0 | `Computer.disabled` |
| Whitelist 认证器 | 4 | `WhitelistConnectionDaemon` 不放行则连不上 |
| 已控（肉鸡） | 5 | `adminIP == 玩家IP` |
| **永远提不了权** | **27** | 端口表容量 ≤ `portsToCrack` |
| **可打** | **110** | — |

被剔除的 27 个里，`portsToCrack` 取值为 `8, 6, 4, 3, 2, 9999998`。
其中 **`9999998` 出现 9 次**（EnTech 剧情保护机：`EnTechOutsiderRepo` / `EnTechMainframe` /
`EnTechPrometheus` 等，全部 `security=5` / `traceTime=500` / 带 firewall）——
这类机器此前每次全网扫描都被连上、逐个破端口、再提权失败，
正是用户说的「每一次判断都是要入侵」。

注：`portsToCrack="9999998"` 不是游戏源码里的常量，内容 XML 里也搜不到 ——
是任务脚本通过 `HACKNET` 类动作在运行时写入的哨兵值。判据不必特判它，
`端口容量 ≤ 门槛` 天然覆盖。

### 12.5 取舍记录

| 决策 | 选择 | 理由 |
|---|---|---|
| 提权 | 对齐原生门禁（含防火墙） | 用户明确「原生命令和机制没有完整利用」；直调 `giveAdmin` 等于架空 `PortHackExe` |
| 仍不调 `os.takeAdmin` | 保持 `giveAdmin` | `takeAdmin` 内部 `runCommand("connect " + ip)`，而 `connect` 第一件事是无条件断开（`Programs.cs:235-236`）→ 立刻触发 `handleDisconnection` 与管理员反扑 |
| 解防火墙 | `attemptSolve` 而非 `Programs.solve` | 后者 `doDots(30,60)` 阻塞 1.8 秒；前者是同一判断入口且无阻塞 |
| 回显 | 只回显真执行的命令 | 回显 `analyze` 却跑 `attemptSolve` 属欺骗；`dc` 后的 `rm` 同理 |
| 目标集合 | `netMap.nodes` 全表 | 对齐 `Programs.connect` 的实际判据（不查 visibleNodes） |
| 不可提权机 | 只从全网扫描剔除 | 与 `SkipOwned` 同口径 —— `here`／显式点名是玩家的刻意选择，一律尊重 |
| CLR 层 | 不新增依赖、不挂新补丁 | 全部走游戏已有的 public 成员 |

### 12.6 验证（v1.9）

反编译核对（配方同前，产物 `decompiled/autohack-v9/AutoHack.decompiled.cs`，1859 行）：

| 项 | 关键词 | 计数 |
|---|---|---|
| 应存在 | `ConnectableComputers` / `CanEverEscalate` / `SolveFirewall` / `SkippedHopeless` | 2 / 2 / 7 / 6 |
| 应存在 | `attemptSolve` / `HackStepKind.SolveFirewall` | 1 / 2 |
| 应消失 | `ReachableComputers` / `DiscoveredComputers` / `private static void Seed` | 0 / 0 / 0 |
| 顺序 | `HackStepKind.Disconnect`（:1693）先于 `HackStepKind.CleanLogs`（:1697） | ✓ |
| 必须为 0 | 原生控件 / `UISmallfont` / `hostileActionTaken` | 0 / 0 / 0 |

- 构建：`dotnet build src/AutoHack/AutoHack.csproj -c Release` → 0 警告 0 错误；
  产物 `AutoHack.dll` 45056 B。
- 版本：`AutoHack", "1.9.0"`。

待真人验证：面板在真实存档上的观感；`solve` 步骤在有防火墙的机器上的实际表现。

---

## §13 v1.10：追踪直接毙掉，全网扫描加开关，标记改默认关

用户三条指令：①「以后只推送发布，由我来决定」（流程约束，已记入 §13.5）；
②「shell 命令类，默认不上传标记文件；全域扫描默认用上一版，做开关」；
③追问「每帧调用是不是太吃性能了，不能直接毙掉或者绕过吗？」——这一问推翻了 §12 里
我准备照抄 `TraceKillExe` 的每帧冻结方案。

### 13.1 关键修正：追踪可以直接毙掉，不必每帧维护

`TraceTracker` 有原生的停止入口：

```csharp
public void stop() {          // TraceTracker.cs:116-119
    active = false;
    trackSpeedFactor = 1f;
}
```

而 `Update` 开头就是防空转早返回（`TraceTracker.cs:53-56`）：

```csharp
if (!active) { return; }
```

**停是彻底的，不存在「暂停」形态** —— 置一次 `active = false`，后续所有帧零开销。

这是游戏自己的做法：

| 调用点 | 场景 |
|---|---|
| `SecurityTraceExe.Killed()`（SecurityTraceExe.cs:26） | 玩家关掉 Security Tracer 程序 |
| `OS.thisComputerIPReset()`（OS.cs:1793-1796） | 玩家换 IP 时 `if (traceTracker.active) traceTracker.active = false;` |

**为什么原计划是错的**：我原本准备照抄 `TraceKillExe` 的
`os.traceTracker.timeSinceFreezeRequest = 0f`（TraceKillExe.cs:88-94）。
那是**它作为 GUI 程序的职责** —— 玩家开着 TraceKill 时要看到
`SUPPRESSION ACTIVE` 的持续效果（TraceKillExe.cs:181），故必须逐帧续期。
`timeSinceFreezeRequest` 的作用是让 `TraceTracker.Update` 里的
`flag`（`timeSinceFreezeRequest < 0.2f`，:48-51）成立，从而**跳过倒计时扣减**——
即「冻结」。冻结是给「开着程序持续压制」这个交互形态用的；
mod 要的是「立即终止」这一**动作**，没有那个 UI 需求，
照抄只会白白常驻一个每帧补丁。

用户这一问正中要害：为一个一次性动作常驻每帧补丁是纯粹的浪费。

**不丢成就**：`trace_close` 的解锁写在 `TraceTracker.Update` 的**另一条**分支
（`connectedComp == null || ip != target.ip` 时解锁，TraceTracker.cs:64-71），
走 `stop()` 不经过它。所以两种终止方式在成就上是互补的：
- 断开连接 → 走那条分支 → 解锁 `trace_close`；
- `stop()` → 不走 → 不解锁，但也不会漏掉任何已有成就。

mod 两者都做了：`dc` 仍是每目标的断开动作（顺带解锁成就与警告闪烁），
`stop()` 作为随后的确定性兜底。

### 13.2 shell / Trap 为什么不做

用户原话「2 但是解决计时」，我取证后确认 **shell 的 Trap 解决不了计时**，
且用户最终选择「不做 shell/trap，聚焦于 stop() 绕过计时」。

`shell` 命令（OS.cs:1966-1990 → `new ShellExe`）在 RAM 面板里有两个按钮，
Trap 的动作是（ShellExe.cs:118-126）：

```csharp
destComp.forkBombClients(targetIP);
compThisShellIsRunningOn.log("#SHELL_TrapActivate_:_ConnectionsFlooded");
```

而 `forkBombClients`（Computer.cs:831-847）：

```csharp
for (int i = 0; i < os.ActiveHackers.Count; i++) {
    if (os.ActiveHackers[i].Value == ip) {
        Computer computer = Programs.getComputer(os, os.ActiveHackers[i].Key);
        computer.crash(ip);
    }
}
```

`os.ActiveHackers`（OS.cs:220）**只由 `HackerScriptExecuter` 在剧情脚本里填充**
（HackerScriptExecuter.cs:111 加、:417 移除）。普通存档里这个列表基本为空 ——
它反制的是任务脚本指定的对手，**不是** `TraceTracker`。追踪计时与它毫无关系。

故结论：shell/Trap 对「解决计时」零帮助，不做。

### 13.3 全网扫描两套口径

`Programs.connect`（Programs.cs:231-322）遍历 `os.netMap.nodes` 全表，
按 ip/name 匹配，**不检查 visibleNodes，也不看 links**。
`visibleNodes` 只是原版 `scan` 的「已发现」展示标记。

两套口径（同一存档 147 节点实测）：

| 口径 | 实现 | 目标数 | 开关 |
|---|---|---|---|
| 沿连线广度优先 | `ReachableComputers`：多源种子（玩家机 + visibleNodes）沿 `Computer.links` 展开 | **7** | 缺省 |
| 地图全表 | `ConnectableComputers`：遍历 `netMap.nodes`，只排除玩家机 | **110** | `allnodes` / 面板 `whole map` |

用户指令是「全域扫描默认用上一版」，即恢复 v1.8 的 BFS 为缺省，v1.9 的全表降为可选。

注意这条通路判据比游戏实际的连接能力**更窄**：差额那 103 台正是
「可以直接敲 IP 连上」却被连线图漏掉的机器。两者都只排除玩家机，
`disabled`／已控／永远提不了权的机器统一由 `ResolveTargets` 过滤。

### 13.4 标记文件默认关

`HackOptions.Parse` 的 `uploadMarker` 初值 `true` → `false`；
新增 `mark` / `upload` / `marker` 别名可显式开启（`nomark` 保留，现在与缺省等价）。
面板 `HackPanelState.UploadMarker` 默认同步改为 `false`。

理由：投放 `~/autohack.txt` 会在目标机留下文件，而该文件被复制/删除都会触发
`TrackerCompleteSequence.CompShouldStartTrackerFromLogs`（见 §12.3）；
不做标记的入侵更干净。要留所有权凭证时可以显式开。

### 13.5 流程约束

**「以后只推送发布，由我来决定」** —— 提交与推送照常（版本控制需要），
但**发布（`gh release create`）不再自动执行**，等用户明确指示。
此前 v1.8.0 的 Release 与本次均遵循此约定。

### 13.6 验证（v1.10）

反编译核对（产物 `decompiled/autohack-v10/AutoHack.decompiled.cs`，1963 行）：

| 项 | 关键词 | 计数 |
|---|---|---|
| 应存在 | `KillTrace` / `traceTracker.stop` | 8 / 1 |
| 应存在 | `ReachableComputers` / `ConnectableComputers` / `AllNodes` | 2 / 2 / 7 |
| 应存在 | `allnodes` / `whole map` 别名与面板标签 | 2 / 2 |
| 已撤 | `ShellTrap` / `forkBombClients` | 0 / 0 |
| 必须为 0 | 原生控件 / `UISmallfont` / `hostileActionTaken` | 0 / 0 / 0 |

- 构建：0 警告 0 错误；产物 `AutoHack.dll` 47104 B（v1.9 为 45056 B）。
- 版本：`AutoHack", "1.10.0"`。
- 面板选项块行数不变（3 行 6 个复选），`OptionsBlockHeight` 无需改。

**待真人验证**：`whole map` 开启后是否连上明显更多机器（应约 110 台）；
`[autohack] trace killed - timer stopped.` 是否在追踪出现时打印。

---

## §14 v1.11：已知账密登入、节奏档位与同帧连跑

用户三条指令：①「login（用已知账密登入而非破解）」；
②「delay 最低档位改成 0.02」；③「再加『非端口步并行到同一帧』或 speed 档位」——
③ 经追问后选定「两者都做」；①的凭据来源用户回「1+2」（两级候选都实现）。

### 14.1 login 为什么能替代破解（机制取证）

`Computer.login`（Computer.cs:849-865）全文：

```csharp
public virtual int login(string username, string password, byte type = 1)
{
    if (username.ToLower().Equals("admin") && password.Equals(adminPass))
    {
        giveAdmin(os.thisComputer.ip);   // ← 直接提权，与 porthack 终点等价
        return 1;
    }
    for (int i = 0; i < users.Count; i++)
    {
        if (users[i].name.Equals(username) && users[i].pass.Equals(password)
            && (users[i].type == type || type == 1))
        {
            currentUser = users[i];      // ← 返回值 2：只设 currentUser，不写 adminIP
            return 2;
        }
    }
    return 0;
}
```

三个关键事实：

1. **admin 分支内部直接 `giveAdmin`** —— 写 `adminIP`、标记 `users[0].known`，
   与 porthack 完全同效，但**不破任何端口**。
2. **返回值 2 不等于提权** —— 那条路径只设 `currentUser`，不写 `adminIP`，
   机器仍不算肉鸡。故只有 `== 1` 才算成功（首版写成 `!= 0`，已修正）。
3. **该路径上没有 `hostileActionTaken`** —— `login` 与 `giveAdmin` 都不调它，
   故登录**不点燃追踪**。对比 `openPort` 的各破解程序都会调（§12.1 表）。

安全前提已核实：

| 检查 | 结果 |
|---|---|
| `Computer.os` 是否已赋值（`login` 内用 `os.thisComputer.ip`） | 构造函数赋值（Computer.cs:117），安全 |
| Pathfinder 是否补丁 `login` | 否（`upstream/` 全库搜索为空） |
| 是否有子类 `override int login` | 否（`EOSComp` 不继承 `Computer`） |

**刻意不用 `Programs.login`**：它是交互式的 ——

```csharp
os.terminal.prompt = "Username :";
int num = os.terminal.commandsRun();
while (os.terminal.commandsRun() == num) { Thread.Sleep(4); }   // Programs.cs:414-417
```

轮询等玩家输入，在游戏线程调用即卡死。所以只调纯函数 `Computer.login`。

### 14.2 凭据两级候选与其量级（用户选「1+2」）

| 级 | 来源 | 语义 | 存档实测覆盖 |
|---|---|---|---|
| ① | `UserDetail.known == true` 的账号 | 游戏原生的「玩家已知这组账密」标记。写入点：`giveAdmin`（Computer.cs:747）、`MissionFunctions.cs:439/469`、`SAGivePlayerUserAccount.cs:31`、`DLCIntroExe.cs:242` | 146 台非玩家机里 **5 台** |
| ② | 目标 `adminPass` 公开字段 | 与 `users[0].pass` 同源（构造 Computer.cs:122-123，存档读回 :1117-1120） | **146/146（100%）** |

**②的量级必须写明**：`login` 能拿下全部 146 台非玩家机，于是
**端口破解、防火墙、跳板过载三套机制实际都不会再被走到**。
这是「`creds` 缺省开」的直接后果。要保留原玩法须 `nocreds`。

保留 ① 优先的理由：它才是「玩家真的知道密码」的原生语义；② 属便利性放行。
两级都实现是用户的明确选择（回「1+2」）。

### 14.3 排程：Login 步插在 Probe 之后、破端口之前

`BuildSteps` 顺序变为：

```
Connect → Neutralize → Probe → Login → BypassProxy → OpenPort×N
        → SolveFirewall → Escalate → UploadMarker → Disconnect → KillTrace → CleanLogs
```

Login 插在 Probe 之后，是为了让终端先有原生端口报告、再看到登录 —— 观感更像真人先侦察。

**运行时跳过**：`Tick` 用 `IsRedundantAfterLogin(step)` 判定，命中则 `_index++` 不回显不耗时。
跳过集合 = `{BypassProxy, OpenPort, SolveFirewall, Escalate}`，条件是
`_loggedIn.Contains(step.Target)`。

**为什么用 `_loggedIn` 集合而不是「`adminIP` 已是我们」**：后者太宽 ——
`redo` 模式（重打已控节点）下所有目标都满足，会连端口都不破，篡改该模式的语义。
`_loggedIn` 只登记**本次运行中确实靠 login 拿下**的机器。

被跳过的步骤也计入 `Done`（`Done => Math.Min(_index, _steps.Count)` 天然满足），进度条才准。

### 14.4 连带修正：所有权判定从 `CanEscalate` 改为 `IsOwned`

`UploadMarker` 与 `Finish` 原本用 `CanEscalate`（要求端口已破）判定 ——
靠 login 提权的目标**一个端口都没破**，会被误判成未拿下，导致：
- 开了 `upload marker` 也投不出标记文件；
- 战果行误报 `admin=no`。

改为 `IsOwned(target, os)`（`comp.adminIP == os.thisComputer.ip`）—— 肉鸡标记的真身。

### 14.5 「永远提不了权」过滤须与凭据联动

§12.4 的过滤剔除「端口表容量 ≤ 门槛」的机器（`CanEverEscalate`）。
但 login 那条路**不看端口数**，故这些机器并非真的没救：

```csharp
if (!CanEverEscalate(comp) && !(options.UseCredentials && HasAnyCredential(comp)))
{
    skippedHopeless++;
    continue;
}
```

与 `options.UseCredentials` 联动是必需的：否则 `nocreds` 模式下过滤器失效
（`HasAnyCredential` 与模式无关，仍会返回 true），把「永远打不通」的机器放进来。

### 14.6 节奏档位与同帧连跑

| 档 | 非端口步间隔 | 行为 |
|---|---|---|
| `NORMAL`（缺省） | 0.35s | 与旧版一致，终端逐行浮现 |
| `FAST` | 0.05s | 仍分帧，只压缩间隔 |
| `INSTANT` | 0 | 一帧内连跑到底，只在端口步停下 |

端口步**始终**按 `Options.PortDelay` 等 —— 那是回显逐条浮现的节奏来源。

`Tick` 从「每帧至多一步」改为循环：

```csharp
var budget = MaxStepsPerFrame;
while (!Finished && budget-- > 0)
{
    if (_index >= _steps.Count) { Finish(os); return; }
    var step = _steps[_index];
    if (IsRedundantAfterLogin(step)) { _index++; continue; }
    if (_timer < DelayFor(step.Kind)) { return; }
    _timer = 0f;
    Apply(os, step);
    _index++;
}
```

`MaxStepsPerFrame = 512` 兜住极端规模：110 目标 × ~10 步 ≈ 1100 步，
分 3 帧跑完，不会一帧卡死。

**为什么缺省不是 INSTANT**：改缺省会静默改变所有既有用户的行为，
而档位是显式选项 —— 缺省保持 `NORMAL`（= 旧版），要快就自己选。

### 14.7 delay 下限 0.05 → 0.02

`MinPortDelay = 0.02f`（50 端口/秒）。仍逐条回显，只是间隔极短。
`HackOptions.Parse` 的钳制与面板滑条的 `min` 同步改（面板滑条传的就是
`HackOptions.MinPortDelay`，单点修改）。

新增常量 `FastStepDelay = 0.05f` 供 FAST 档用。
首版误命名为 `FastPortDelay`（它管的是非端口步，名不副实），已更正为 `FastStepDelay`。

### 14.8 面板改动

新增 `SPEED` 段（三档 segment：NORMAL/FAST/INSTANT）+ 复选行重排为 4 行：

```
SCOPE         [NETWORK SWEEP] [CURRENT NODE]
PORT INTERVAL <值>  [========|=====]
SPEED         <档位说明>  [NORMAL][FAST][INSTANT]
[use known creds] [whole map]
[skip owned]      [wipe logs]
[connect first]   [anti-trace dc]
[upload marker]
RUN
```

`OptionsBlockHeight` 由 192 增至 274，公式同步加一项
`SectionHeight + SegmentHeight + Gap` 并把复选改 `* 4`。
用脚本按 `DrawOptions` 逐行累加核对：实际推进 274 = 声明 274 ✓。

控件 ID 顺延至 `IdBase + 22`。

### 14.9 验证（v1.11）

反编译核对（`decompiled/autohack-v11/AutoHack.decompiled.cs`，2156 行）：

| 项 | 关键词 | 计数 |
|---|---|---|
| 应存在 | `TryLogin` / `HasAnyCredential` | 2 / 2 |
| 应存在 | `HackStepKind.Login` / `IsRedundantAfterLogin` / `_loggedIn` | 2 / 2 / 3 |
| 应存在 | `HackSpeed` / `MaxStepsPerFrame` / `FastStepDelay` | 17 / 1 / 1 |
| 常量 | `MinPortDelay = 0.02f` / `NormalStepDelay = 0.35f` / `FastStepDelay = 0.05f` | ✓ |
| 已弃 | `_skipPortsFor`（被 `_loggedIn` 取代） | 0 |
| 必须为 0 | 原生控件 / `UISmallfont` / `hostileActionTaken` / `Thread.Sleep` | 0 / 0 / 0 / 0 |

- 构建：0 警告 0 错误；产物 `AutoHack.dll` 50688 B（v1.10 为 47104 B）。
- 版本 `AutoHack", "1.11.0"`。

**待真人验证**：面板 `SPEED` 三档与 `use known creds` 的点击手感；
INSTANT 档下终端刷屏观感；`login` 在真实目标上是否如期打印
`admin via login (admin)` 并跳过破端口。

### 14.10 清痕不受跳过影响（v1.11.1 修正）

**用户指令**：「清理log改成不受跳过影响」。

**根因**：`ResolveTargets` 的两处剔除直接 `continue`，被剔除的机器
既不进 `result`，也就**不生成任何步骤** —— 包括 `CleanLogs`：

```csharp
if (options.SkipOwned && IsOwned(comp, os))
{
    skippedOwned++;
    continue;          // ← 连清痕也一并免了
}
```

注意 `IsRedundantAfterLogin` **不含** `CleanLogs`（只跳过
`BypassProxy` / `OpenPort` / `SolveFirewall` / `Escalate`），
所以 login 那条路径的清痕本来就是好的；本次问题**只在目标层剔除**这一处。

**问题性质**：「跳过入侵」与「放过证据」是两回事。这些机器此前进过、破过、侦察过，
`/log` 里躺着痕迹，恰恰是**最该清**的一批 —— 已控机器上的玩家痕迹最多。

**改法**：剔除时登记，末尾补步。

```csharp
skipped.Add(comp);     // 两处剔除分支各一行
// ...
return new TargetPlan(result, skipped, skippedOwned, skippedHopeless);
```

`BuildSteps` 在**全部正常步骤之后**补：

```csharp
if (options.ClearLogs)
{
    foreach (var comp in skipped)
    {
        steps.Add(new HackStep(HackStepKind.CleanLogs, comp, default, null));
    }
}
```

**为什么排在最后**：正常流程不会再碰这些机器，此刻清是终点动作，
不会有新记录再追加进来（正常目标的清痕排在 `dc` 之前，见 §14.12；
同样是为了「清完不再有人写」这个不变量）。

**边界**：`disabled` 机器与玩家自己**不进** `Skipped` —— 既没打过，也不该碰。
对无痕迹的机器幂等：`ClearLogs` 返回空列表，不产生任何输出。

**顺带的结构收敛**：`ResolveTargets` 原本返回 `IReadOnlyList<Computer>` 并带两个
`out int` 参数，现在改为返回 `TargetPlan` 记录结构。三个返回值本就是同一份
解析的产出，装进一个结构才符合单一职责，调用侧也少两个 `out`。

反编译核对（`decompiled/autohack-v111/AutoHack.decompiled.cs`，2169 行）：
`list2.Add` 在 :452 与 :458 各一次（两处剔除）、`in skipped` 1（补步循环）、
`HackStepKind.CleanLogs, item2` 1（补的正是清痕步）、`out int skippedOwned` 0（已随签名删除）。

### 14.11 清痕改用游戏删除原语（v1.11.2 修正）

**用户指令**：「就是攻破的机器同样删除log,是不是写错了？」「命令系统。rm log/*」
—— 要求改走游戏命令系统，而不是 mod 直接操作内存。

**先做的取证**：拿参考存档全量统计，检验「攻破的机器没被删」这一假设。

| 判据 | 数值 |
|---|---|
| 玩家机 | `1 PC @ 51.87.123.209`（`spec="player"`） |
| 总机数 | 169 |
| 已被控制（`<security adminIP>` == 玩家 IP） | **9** 台 |
| 其中 `/log` 已清空 | **0** 台 |
| 其中 `/log` 有残留 | **9** 台 |
| 全档 `Became_Admin` 记录 | **0** 条 |

**结论**：清痕**早已生效**。`Computer.giveAdmin` 必写 `"<ip> Became Admin"`
（`Computer.cs:741`），9 台已控却一条不剩 —— 只有"被清过"能解释。
残留内容全是 `Connection:_from`（25 条）、`Disconnected`（62 条）、
`FileRead`，其中 `@616_Connection` 与 `@616_Disconnected` **时间戳同秒**，
而 mod 一条目标的 Connect→Disconnect 间隔至少 2.5 秒（0.35s/步 × ~7 步）——
这正是**玩家清完之后自己 `connect` 去检查**时游戏新写的
（`Computer.connect` → `log("Connection: from " + ipFrom)`，`Computer.cs:389`）。

**写 log 的全部 15 个调用点**（`grep '\.log(' decompiled/game-proj/Hacknet/*.cs`）：

| 行 | 内容 | 触发者 |
|---|---|---|
| `Computer.cs:389` | `Connection: from <ip>` | 被连方，每次 connect |
| `Computer.cs:409` | `User Account Added` | `addUser` |
| `Computer.cs:434` | `CRASH REPORT` | 崩溃 |
| `Computer.cs:455` | `Rebooting system` | 重启 |
| `Computer.cs:490` | `FileRead: by <ip>` | `cat` 等读取 |
| `Computer.cs:502` | `FileCopied` | 拷贝 |
| `Computer.cs:543` | `FileDeleted` | `deleteFile` 且**文件名不以 `@` 开头** |
| `Computer.cs:649` | `FileMoved` | 移动 |
| `Computer.cs:661` | `FileCreated` | 新建 |
| `Computer.cs:699` | `FolderCreated` | 建目录 |
| `Computer.cs:726` | `<ip> Disconnected` | 断开 |
| `Computer.cs:741` | `<ip> Became Admin` | 提权 |
| `Computer.cs:767` | `<ip> Opened Port#` | 开端口（仅 vanilla `portsOpen` 路，Pathfinder 下不触发） |
| `Computer.cs:793` | `<ip> Closed Port#` | 关端口（同上，恒不触发） |
| `ShellExe.cs:52/142/155/193/228` | `#SHELL_*` | 本插件不使用 Shell |

**改法**：`ClearLogs(Computer)` → `ClearLogs(Computer, string ipFrom)`。

```csharp
var root = comp?.files?.root;
var logFolder = root?.searchForFolder(LogFolderName);
if (root == null || logFolder == null || logFolder.files.Count == 0)
{
    return Array.Empty<string>();
}

var removed = new List<string>(logFolder.files.Count);
foreach (var file in logFolder.files)
{
    if (!string.IsNullOrWhiteSpace(file?.name)) removed.Add(file.name);
}

var folderPath = new List<int> { root.folders.IndexOf(logFolder) };
if (!comp.deleteFile(ipFrom, "*", folderPath))
{
    logFolder.files.Clear();      // 兜底：门禁拒绝时不让清痕静默失败
}
return removed;
```

**为什么 `deleteFile` 不会自我污染** —— 这是本次最关键的发现：

```csharp
// Computer.deleteFile(ipFrom, name, folderPath)，Computer.cs:541-545
if (name[0] != '@')
{
    log("FileDeleted: by " + ipFrom + " - file:" + name);
}
```

而 `/log` 里的文件名**恒以 `@` 开头**：

```csharp
// Computer.log(string message)，Computer.cs:337-355
message = "@" + (int)OS.currentElapsedTime + " " + message;
string text2 = text.Replace(" ", "_");            // ← 这就是 FileEntry.name
files.root.searchForFolder("log").files.Insert(0, new FileEntry(message, text2));
```

⇒ 删 log 文件时 `name[0] == '@'` 成立，`FileDeleted` 这条 log **被 `deleteFile` 自己豁免**。
存档实证佐证：14 台有痕迹机器的文件名**全部**以 `@` 开头
（如 `@616_Connection:_from_51.87.123.209`）。所以"用游戏原语删除"与"不留下删除痕迹"
这两个目标不冲突 —— 原先担心的 `rm log/*` 会残留 N 条 `FileDeleted` 并不成立。

**权限门禁**（`Computer.cs:511-517`）：

```csharp
if (currentUser.type == 1 || currentUser.type == 0) flag = true;
if (!flag && !silent && !ipFrom.Equals(adminIP) && !ipFrom.Equals(ip)) return false;
```

`currentUser` 是 `UserDetail`（结构体，`UserDetail.cs:5`），`type` 字段默认 `0` ⇒ 门禁恒开。
真正的保障是上面那行返回 `false` 时的 `files.Clear()` 回退。

**`"*"` 分支的安全性**（`Computer.cs:519-537`）：先快照 `folder.files` 的**名字**到局部
`List<string>`，再逐个递归调用 —— 遍历期间删元素不会漏项。这是能安全用 `"*"` 的前提。

**回显收敛**：原先逐文件 `Echo(os, "rm /log/" + name)`，N 个文件刷 N 行命令。
改为一条 `rm log/*`（stay 模式下是真实可跑的命令）；默认已断开时写状态行
`[autohack] <名> :: rm log/* -> N log file(s) wiped`。

**为什么不用 `Programs.rm`**：它逐文件 `for j in 0..min(max(size/1000,3),26): Thread.Sleep(200)`
（`Programs.cs:1013-1017`），每个文件最多 5.2 秒动画，且必须跑在非游戏线程
（`OS.execute` 派生线程，`OS.cs:1754-1767`）。挪进 mod 等于强行插入秒级等待，
而它的实质只是转调 `computer.deleteFile(os.thisComputer.ip, list[i].name, list2)`
（`Programs.cs:1019`）—— 直接调这一句即可，语义完全相同而无动画。

**验证**（`decompiled/autohack-v112/AutoHack.decompiled.cs`，2156 行）：
`deleteFile` 1、`ClearLogs(Computer comp, string ipFrom)` 1、`rm log/*` 2、
`FileDeleted` 0、`"1.11.2"` 1、`"1.11.1"` 0；禁项
`UISmallfont`/`doCheckBox`/`hostileActionTaken`/`Thread.Sleep` 全 0。


### 14.12 清痕必须排在断开之前，断开改为静默（v1.11.3 修正）

**用户指令（决定性）**：「我给的命令是要连接上对方的文件系统才能有效果的。还是没有效果」

这一句点破了 v1.11.2 的根因。此前把清痕排在 `dc` 之后，理由是不让
`disconnecting` 写的那条 `Disconnected` 残留 —— 但那个理由只对了一半。

#### 14.12.1 命令的作用域由连接决定

`Programs.rm`（`Programs.cs:948-1034`）第一件事：

```csharp
Computer computer = ((os.connectedComp != null) ? os.connectedComp : os.thisComputer);
```

**第 956 行 —— 目标机就是"当前连接"。** `navigationPath` 只负责在目标上选文件夹：

```csharp
public static Folder getCurrentFolder(OS os)
    => getFolderAtDepth(os, os.navigationPath.Count);          // Programs.cs:1531

public static Folder getFolderAtDepth(OS os, int depth)
{
    Folder folder = ((os.connectedComp != null)
        ? os.connectedComp.files.root : os.thisComputer.files.root);  // :1538
    ...
}
```

而 `Programs.disconnect` 会 `os.navigationPath.Clear()`。⇒ **断开之后再 `rm`，
操作的是玩家自己的文件系统**。清痕排在 `dc` 之后，语义上就是一条假命令 ——
正是玩家手敲时踩的同一个坑。

#### 14.12.2 玩家手敲的那条命令为什么也失败

插件日志（`BepInEx/LogOutput.log`）末尾：

```
Spawning thread for command cd log
Spawning thread for command ls
Spawning thread for command rm log/*
Spawning thread for command cd log
```

玩家在 `cd log` 之后敲 `rm log/*`。`Programs.rm` 把参数拆成 path=`log`、name=`*`：

```csharp
int num = args[1].LastIndexOf('/');                    // Programs.cs:957
if (num > 0 && num < args[1].Length - 1) {
    text = args[1].Substring(num + 1);                 // "*"
    text2 = args[1].Substring(0, num);                 // "log"
}
folder = getFolderAtPath(text2, os, folder, returnsNullOnNoFind: true);  // :968
if (folder == null) { os.write("Folder " + text2 + " Not found!"); return; }
```

`getFolderAtPath` 从**当前目录**（已在 `/log`）往下找名为 `log` 的子文件夹
（`Programs.cs:1582+`，`folder.folders[j].name == array[i]`；`Folder.searchForFolder`
同理，`Folder.cs:76-86`）⇒ 不存在 ⇒ `Folder log Not found!` 直接返回。
正确敲法是 `cd log` 后 `rm *`，或根目录下 `rm log/*`。

#### 14.12.2b Hacknet 没有绝对路径（反直觉，值得单列）

`rm /log/*` 与 `rm log/*` 在 Hacknet 里**解析结果相同**：

```csharp
// Programs.cs:1582-1593  getFolderAtPath
char[] separator = new char[2] { '/', '\\' };
string[] array = path.Split(separator);      // "/log" → ["", "log"]
for (int i = 0; i < array.Length; i++) {
    if (array[i] == "" || array[i] == " ") { continue; }   // ← 空段整个跳过
    ...
}
```

前导 `/` 只产出一个空段，随即被跳过 —— 于是 `/log` 与 `log` 一律按
**「当前目录下的 log 子文件夹」**解析，不回到根。这不是实现疏漏：整个
`Programs` 里没有一处把路径当绝对路径处理，`getCurrentFolder`（`:1531`）
永远是解析起点。

**故本插件的回显定格为 `rm log/*`（无前导斜杠）**：两者行为一致，但带斜杠会
让人以为它从根出发 —— 而玩家若照抄进 `cd log` 之后的当前目录，照样会撞
`Folder log Not found!`。写不带斜杠的形式，与「真正决定行为的是当前目录」
这件事相符。

清痕时当前目录**恒为目标根**，两个条件同时成立：`connect` 会
`os.navigationPath.Clear()`（`Programs.cs:235`），而本插件从不发 `cd`
（全仓 `grep '"cd ` 无命中），`getFolderAtDepth`（`:1536`）在
`navigationPath.Count == 0` 时直接返回 `files.root`。

#### 14.12.3 改法

**① 顺序反转**（`HackRun.BuildSteps`）：`CleanLogs` 提到 `Disconnect` 之前，
命令从 `null` 改为字面量 `"rm log/*"`。新顺序：

```
… → Escalate → UploadMarker → CleanLogs → Disconnect → KillTrace
```

**② 断开静音**（`HackRun.Leave`）：

```csharp
var wasSilent = leaving.silent;
leaving.silent = true;
try { Programs.disconnect(["dc"], os); }
finally { leaving.silent = wasSilent; }
```

`silent` 是游戏自己的 public 字段（`Computer.cs:57` `public bool silent = false;`），
`Multiplayer.cs:125-127` 就是「set true → 操作 → 还原」这个用法。
`disconnecting` 写日志的门正是它（`Computer.cs:723` `if (!silent)`）。
只影响这一台、只影响这一次调用，`finally` 保证还原。

**边界（多人）**：同一个 `!silent` 门还守着下一行
`sendNetworkMessage("cDisconnect " + ip + " " + ipFrom)`（`Computer.cs:728-731`）——
静音会连断线同步一起吞掉，对局对面看到的还是「连着」。
故 `os.multiplayer` 为真时**不静音**，退回普通断开：日志保真让位于联机状态保真。
单机下该分支恒不触发（`OS.multiplayer` 默认 false，`OS.cs:156`；存档无 multiplayer 标记）。

**③ 兜底改为无条件校验**（`HackEngine.ClearLogs`）：

```csharp
comp.deleteFile(ipFrom, "*", folderPath);
if (logFolder.files.Count > 0) { logFolder.files.Clear(); }
```

原实现只在 `deleteFile` 返回 `false` 时回退。但 `"*"` 分支是
`flag2 &= deleteFile(...)` 逐个递归后返回 `flag2`（`Computer.cs:526-539`）——
若 `folderPath` 解析偏了，它会去删**别的文件夹**并照样返回 true。
清痕是「证据必须消失」的硬承诺，不能建立在「返回值可信」之上。

**④ 战果可见**：原版 `rm` 逐文件打印 `"Deleting <名>." + "Done"`
（`Programs.cs:1018-1031`），全自动跑 100+ 台会刷屏，压成一行
`Deleting N file(s)... Done`（沿用游戏自己的两个词）；删 0 条时不吭声。
被剔除的机器没有连接，走状态行 `[autohack] <名> :: rm log/* -> N log file(s) wiped`。

> **本地化核对**：原版走 `LocaleTerms.Loc("Deleting")` / `Loc("Done")`
> （`LocaleTerms.cs:53-64`：非 en-us 时查 `ActiveTerms`，查不到就返回原文）。
> 实测 `Content/Locales/zh-cn/Hacknet_UI_Terms.txt`（UTF-16LE，529 行）
> **没有** `Deleting` 与 `Done` 词条 ⇒ 游戏在中文下本身就打印英文
> `Deleting <名>.` / `Done`。故这里硬编码英文与游戏实际输出**逐字一致**，
> 不引入自造译文。

#### 14.12.4 权限门禁的再核对

```csharp
bool flag = false;
if (currentUser.type == 1 || currentUser.type == 0) { flag = true; }        // Computer.cs:511
if (!flag && !silent && !ipFrom.Equals(adminIP) && !ipFrom.Equals(ip)) { return false; }
```

`currentUser` 是 `UserDetail` 结构体字段（`Computer.cs:65`），`type` 默认 0
⇒ 门禁恒开。但 `Computer.login` 命中 `users[i]` 时直接 `currentUser = users[i]`
（`Computer.cs:860`），若是 type 2 账号则落到 `:515` 的 `ipFrom.Equals(adminIP)`
判定 —— 未提权的目标会被拒。这正是「无条件校验」不可省的原因。

存档实证：参考存档 169 台机器中仅 **14 台** `/log` 非空（共 94 条记录），
清痕覆盖面远大于此，绝大多数删除都是空操作（`ClearLogs` 早退，无输出）。

#### 14.12.5 反编译核对

`decompiled/autohack-v113/AutoHack.decompiled.cs`（2166 行）：

| 项 | 计数 | 说明 |
|---|---|---|
| `"1.11.3"` / `"1.11.2"` / `"1.11.1"` | 1 / 0 / 0 | 版本唯一 |
| `CleanLogs @1937 → Disconnect @1941 → KillTrace @1943` | — | 顺序反转实证 |
| `"rm log/*"` | 2 | 一条回显 + 一条状态行 |
| `deleteFile(ipFrom` | 1 | 走游戏删除原语 |
| `.files.Clear()` | 1 | 无条件兜底 |
| `connectedComp.silent = true` | 1 | 静默断开 |
| `Deleting ` | 1 | 战果摘要行 |
| `UISmallfont` / `doCheckBox` / `hostileActionTaken` / `Thread.Sleep` | 0/0/0/0 | 禁项清白 |

---

## 15. v1.12：脚本驱动入侵（script=）

### 15.1 起点：游戏自己的 HackerScript 能不能用

用户给了官方参考页 `https://hacknet.wiki/reference/HackerScripts`。逐条核实后结论是
**语法可借、执行器不可借**。

HackerScript 是游戏内置的**对手黑客引擎**，由 `<LaunchHackScript>` 标签
（`SALaunchHackScript.cs:70`）触发，读取 `Content/HackerScripts/*.txt`，在名为
`OpposingHackerThread` 的后台线程上跑（`HackerScriptExecuter.cs:46`）。格式：

```
config [目标电脑ID] [源电脑ID] [每行延迟] $#%#$
[行为类型] [...参数] $#%#$
```

`$#%#$` 是必须的分隔符（`splitDelimiter = " $#%#$\r\n"`，`HackerScriptExecuter.cs:13`）；
官方**没有注释语法** —— wiki 原话是「没有对应命令的行就不会执行」。

### 15.2 三条否决理由（逐条查源码）

**① 没有提权动作。** `HackerScriptExecuter.executeThreadedScript` 的全部 case
（`:81`–`:356`，共 28 个）里没有 `porthack`、没有 `giveAdmin`。唯一的「接管」是
`systakeover`（`:161`）→ `HostileHackerBreakinSequence.Execute`，而该类会往
**真实磁盘**写 `Documents/My Games/Hacknet/Libs/Injected/VMBootloaderTrap.dll` +
`OpenCMD.bat` + `VM_Recovery_Guide.txt`（`HostileHackerBreakinSequence.cs:13-21`），
是剧情级破坏序列 —— 绝不可用于自动入侵。

**② `connect` 不驱动玩家终端。** `case "connect"`（`:120-127`）走
`Multiplayer.parseInputMessage(getBasicNetworkCommand("cConnection", computer, computer2), os)`
→ `Multiplayer.cs:53-64` 的 `comp.connect(array[2])`。这是**目标机视角**：
目标机记录「有人从源 IP 连进来」，**完全不设 `os.connectedComp`**。
对照游戏自带脚本 `Content/HackerScripts/TrackSequence.txt`：`config playerComp [SOURCE_COMP] 0.2`
—— 目标就是玩家机，源才是攻击者。它是 NPC 引擎，驱动不了玩家。

**③ `os.ActiveHackers` 的唯一写入点由此确认。** `config` 分支里
`os.ActiveHackers.Add(...)`（`HackerScriptExecuter.cs:111`，在 `:417` 移除）。
这补上了 v1.10 的一个悬案：`Computer.forkBombClients` 只遍历 `os.ActiveHackers`，
而它只由 `HackerScriptExecuter` 填充 ⇒ ShellExe 的 Trap 对普通追踪无效 —— 当初
「不实现 shell/Trap」的决策得到二次确认。

### 15.3 但它给了两条有用的东西

**（a）行式语法 + `config` 的延迟语义**。`config` 第 4 个参数是每行间隔
（`timeout = TimeSpan.FromSeconds(Convert.ToDouble(array[3]))`，`:108`），
每个动作末尾 `Thread.Sleep(timeout)`（`:412`）。AutoHack 的脚本沿用它，这样
手写的动作表与游戏脚本的节奏直觉一致。

**（b）静默变更的官方写法 —— 印证 v1.11.3。** `HackerScriptExecuter` 的所有状态变更
都经 `Multiplayer.parseInputMessage`，而 `Multiplayer.cs` 里 **16 处** 都是同一模式：

```csharp
comp.silent = true;
comp.<操作>(...);
comp.silent = false;
```

涉及 `cConnection`(:61-63) `cDisconnect`(:69-71) `cAdmin`(:77-79) `cPortOpen`(:84-86)
`cPortClose`(:91-93) `cFile`(:98-100) `cDelete`(:125-127) `cMake`(:141-143) 等。
v1.11.3 给 `Leave` 加的静默断开是**游戏自己的标准做法**，逐字吻合。

顺带读清 `silent` 的真实作用域：它**只**抑制 `sendNetworkMessage`
（`Computer.cs:276`：`if (os.multiplayer && !silent)`，头部自带 `os.multiplayer` 门禁），
**不**抑制 `log()`。故它在单机对清痕零影响，加它的意义只在多人对局不吞掉
`cDisconnect` 同步。

### 15.4 动作集：自己的，不是游戏的

脚本动作映射到 `HackStepKind`，与内置次序共用同一套执行器：

| 脚本动作 | 别名 | HackStepKind | 终端回显 |
|---|---|---|---|
| `connect` | `c` | `Connect` | `connect <ip>` |
| `neutralize` | `anticounter` `noadmin` | `Neutralize` | 无（改游戏状态） |
| `probe` | `p` | `Probe` | `probe` |
| `login` | `creds` | `Login` | `login` |
| `proxy` | `bypass` `overload` | `BypassProxy` | 无 |
| `openPort [n]` | `crack` `port` | `OpenPort` | `<破解程序> <端口>` |
| `solve` | `firewall` `analyze` | `SolveFirewall` | `solve <解>` |
| `porthack` | `escalate` `admin` | `Escalate` | `porthack` |
| `mark` | `upload` `marker` | `UploadMarker` | 无 |
| `rm` | `clean` `wipe` `logs` | `CleanLogs` | `rm log/*` |
| `dc` | `disconnect` | `Disconnect` | `dc` |
| `killtrace` | `stoptrace` `trace` | `KillTrace` | 无 |

游戏有、AutoHack 没有的 26 个动作单列在 `HackScript.UnsupportedVerbs`，错误信息据此
区分「这是游戏 NPC 动作，此处没有对应物」与「拼错了」—— 否则玩家拿自己那份
在游戏里明明有效的脚本过来，只会看到一句莫名的「未知动作」。

### 15.5 三个恒定补上的动作

脚本不必写、写了也会去重：

1. **`connect`**（仅 `ConnectFirst && !alreadyConnected` 时）—— `direct` 模式下不补，
   把决定权留给脚本。
2. **`neutralize`** —— 不解除反扑，断开后 0~20 秒
   `BasicAdministrator.disconnectionDetected` 会把 `adminIP` 还原成机器自己，
   肉鸡标记当场丢失。
3. **`killtrace`** —— 兜底反追踪，覆盖「脚本以 `stay` 结尾」与「最后一步之后才被点燃」
   的窗口。

这三条是**正确性要求而非风格偏好**：交给玩家手写，漏一个是必然的。

### 15.6 `rm` 早于 `dc` 的不变量

违反即拒绝整份脚本（`HackScript.Validate`）。判据是「每个 `rm` 之前是否存在
**未被 `connect` 抵消**的 `dc`」：

```csharp
var connected = true;
foreach (var action in actions)
{
    switch (action.Kind)
    {
        case HackStepKind.Connect:    connected = true;  break;
        case HackStepKind.Disconnect: connected = false; break;
        case HackStepKind.CleanLogs when !connected:
            throw new FormatException(...);
    }
}
```

不用「最后一个 `dc`」这种粗判 —— 那会把合法的 `dc / connect / rm`（rm 作用于新建立的
连接）误拒。这是 §14.12 结论的可执行化：既然 `rm` 的作用域是当前连接，那么
「断开之后清痕」就不该是能静默失败的东西，而该在解析期就拦住。

### 15.7 解析细节

- **`config` 行**取第 4 个参数作间隔，其余（目标 ID、源机 ID）整个忽略 ——
  目标由 `scope` 解析（`here`/`network`/`allnodes`/显式），与脚本正交。
  脚本只描述「怎么打」，不描述「打谁」。
- **`openPort` 不带端口号** = 走 `HackEngine.CrackablePorts`，即该目标上全部
  「有原生破解程序且未攻破」的端口。与内置次序同一数据源，不在脚本里硬编码端口。
  带号时按**显示端口**与**原始端口**双匹配（与玩家在终端敲的 `sshcrack 22` 同一个数）。
- **`CleanLogs` 允许重复**：它对空 `/log` 幂等（`ClearLogs` 返回空列表、不输出），
  而「证据必须消失」是硬承诺，玩家写两次多清一次比静默吞掉第二个更符合预期。
- **文件解析顺序**：原样路径 → `<加载前缀>/HackerScripts/<名>`（本地化版优先）
  → 加 `.txt` 再试一遍 → 直接 `<加载前缀>/<名>`。`Utils.GetFileLoadPrefix()` 在
  扩展模式下返回扩展目录、否则 `Content/`，故放在 `Content/HackerScripts/` 的脚本
  能直接按名字引用。

### 15.8 错误处理：边界前置 + 兜底

脚本在**入队前**解析一遍（`AutoHackPlugin`），语法错当场报出行号与原因，
报错即 `return`，不入队。`PendingRuns.OnOSUpdate` 另有一层 `catch` 兜底 ——
那里是 Harmony Postfix，异常抛出去会打断 `OS.Update` 的整条补丁链。

```csharp
catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
{
    __instance.write("[autohack] Script error: " + ex.Message);
    Pending.TryRemove(__instance, out _);
    return;
}
```

过滤器只捕这三类（预期内的用户输入错误），其余照常抛出 —— 不吞未知异常。

### 15.9 逐帧推进仍然复用

脚本不引入新的执行模型：`BuildSteps` 产出同一种 `List<HackStep>`，
`Tick`/`Apply`/`DelayFor` 原样工作。唯一的接触点是 `DelayFor` ——
脚本自带 `delay` 行时以它为准，否则回到 `speed` 档位。两者正交：
档位管「多快」，脚本管「什么次序」。

### 15.10 验证

反编译 `decompiled/autohack-v120/AutoHack.decompiled.cs`（2501 行）逐项核对：
`"1.12.0"` 1、`"1.11."` 0、`class HackScript` 1、`Vocabulary` 3、
`UnsupportedVerbs` 2、`AppendScripted`/`ExpandPorts`/`CommandFor` 各 2、
`_script` 4、`StepDelay` 5、`script=` 5、`Script { get` 1、`Script error` 2、
rm 不变量字符串 1（`:2193`）、`config` 取 `array2[3]`（`:2150-2152`）。
禁项 `UISmallfont`/`doCheckBox`/`hostileActionTaken`/`Thread.Sleep` 全 0，
`HostileHackerBreakin` 0（`systakeover` 仅出现在 `UnsupportedVerbs` 字面量里，`:2076`）。

解析语义另用 Node 复刻同一算法做了边界表测试：
自家三份脚本、游戏自带两份（`TrackSequence.txt` → `flash` 被识别为 NPC 动作、
`ThemeHack.txt` → `delete`）、`dc/connect/rm` 合法、`dc→rm` 拒绝、
重复 `rm` 合法、拼错报行号、`config` 第 4 参生效、空脚本报 `no actions`。

**未验证**：真机加载脚本、终端回显观感需真人进游戏确认。


---

## 16. 验证流程变更：只看产物 MD5，不再反编译核对

**用户指令（v1.12.1 起生效）**：「以后不要反编译核对,只看MD五就好编译出来的文件在哪里？」

### 16.1 产物路径

```
D:\steam\steamapps\common\Hacknet\BepInEx\plugins\AutoHack.dll
```

由 `src/AutoHack/AutoHack.csproj:14-15` 写死，构建后**自动直投**，无需手动拷贝：

```xml
<HacknetDir Condition="'$(HacknetDir)' == ''">D:\steam\steamapps\common\Hacknet\</HacknetDir>
<OutputPath>$(HacknetDir)BepInEx\plugins\</OutputPath>
<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
```

同目录另有 `PathfinderAPI.dll`、`PathfinderUpdater.dll`、`HacknetHotReplace.dll`、
`KernelFix.dll`（后两个非本插件产物）。

### 16.2 新的核对流程

1. `rm -rf src/AutoHack/obj src/AutoHack/bin` —— **不可省**。历史教训：增量缓存会
   产出与上一版**同字节数**的假通过（`d897d5b` 那次就是这样被骗过的）。
2. `dotnet build src/AutoHack/AutoHack.csproj -c Release -v q --nologo`，须 **0 警告 0 错误**。
3. 记 `md5sum` + 字节数 + mtime，与上一版比对。

**为什么这已足够**：构建成功本身即证明源码被编入 —— 清过缓存的完整重编，任何
语法错、缺引用、类型不匹配都会直接失败。MD5 的作用收窄为「确认部署确实是新产物」
（同一个 md5 出现两次就说明构建没真正生效，或源码没改到该改的地方）。

### 16.3 废止的做法

`ilspycmd -o decompiled/autohack-vNNN <dll>` 的逐项字符串计数核对**不再执行**。
这些存档目录本身也已于 v1.15.0 **全部删除** —— 本节及 §11–§17 里出现的 `decompiled/autohack-vNN/…`
路径是当时核对的历史记录，**文件已不存在**，行数/计数留作文物指纹。
README 里 v1.7–v1.12.0 的九块反编译核对记录**已删除**（不再保留为历史存档）；各版的字节数与 MD5 指纹留在 README 的「版本沿革」表，逐版核对原文只在 git 历史里（`git log --follow README.md`）。
它们的残留价值：那些行数与计数是当时产物的指纹，回溯「某版本编进去了什么」时可查。

**注意**：`decompiled/game-proj/` 与 `decompiled/pathfinder/` 是**另一回事** ——
那是**游戏与框架本体**的反编译，是本插件全部决策的依据来源（RESEARCH 里大量
`文件:行号` 引用都指向它），**必须保留**，与「不再核对 mod 产物」无关。

## 17. v1.12.3：清痕不吃节流；targets=0 必须说明原因

### 17.1 清痕的耗时构成（用户问「能否异步多线程提速」）

结论先行：**慢的不是清痕，是节流；多线程既无收益也有害。**

`ClearLogs`（`src/AutoHack/HackEngine.cs`）实际动作只有两件：

```csharp
var folderPath = new List<int> { root.folders.IndexOf(logFolder) };
comp.deleteFile(ipFrom, "*", folderPath);   // 纯内存：遍历 List + Remove
if (logFolder.files.Count > 0) { logFolder.files.Clear(); }   // 兜底
```

- **无磁盘 IO**：`Computer.files` 是内存里的 `FileSystem` 对象，存档写盘由游戏自己的
  `SaveGame` 负责，与此无关。
- **无 `Thread.Sleep`**：`Programs.rm`（`Programs.cs:948-1034`）每文件有
  `for j in 0..min(max(size/1000,3),26) { Thread.Sleep(200); }` 的动画，本插件
  **刻意不走它**，只调它内部真正的那一句 `deleteFile`（`Programs.cs:1024`）。
- **无网络**：`deleteFile` 里 `sendNetworkMessage` 受 `os.multiplayer` 门禁，单机不发。
- **连自写日志都没有**：`deleteFile` 唯一写 log 处是
  `if (name[0] != '@') { log("FileDeleted: by " + ipFrom + " - file:" + name); }`
  （`Computer.cs:543`），而 `/log` 文件名恒以 `@` 开头
  （`Computer.log` 用 `"@" + (int)OS.currentElapsedTime + " " + message` 再空格换下划线，
  `Computer.cs:337-355`）—— 条件不成立，被豁免。

所以真正的耗时是 `HackRun.Tick` 里的：

```csharp
var delay = DelayFor(step.Kind);
if (_timer < delay) { return; }      // Normal 档 = 0.35 s/步
```

Normal 档下：11 台跳过机 3.85 s；`allnodes` ~158 台 55 s；正常入侵 110 台 38.5 s。
**全部是等出来的。**

### 17.2 为什么不用多线程

1. **没有可并行的东西** —— 耗时是人为等待而非计算，开线程后每个线程仍要等同样的时长。
2. **会踩游戏主线程状态** —— `Computer.files` / `deleteFile` 被游戏每帧读（存档、GUI 遍历），
   跨线程写即数据竞争。v1.8.0 已因此产生真实缺陷（`Collection was modified`），
   并由此确立「不引入 async/await、不跨线程，靠每帧步进（协程等价物）」的规矩（§11.1）。
3. **收益为零、风险为存档损坏** —— 不成比例。

### 17.3 改法

`DelayFor` 中对 `CleanLogs` 返回 `0f`：

```csharp
if (kind == HackStepKind.CleanLogs) { return 0f; }
```

理由：清痕没有需要人眼跟上的逐条回显（每台至多一行摘要），不该吃节流。
单帧步数仍由 `MaxStepsPerFrame = 512` 兜底，不会因一次涌入几百步而卡帧。
`OpenPort` 不受影响，仍按 `Options.PortDelay` 走 —— 那是逐条端口回显的节奏来源，
唯一有意义的等待。

`INSTANT` 档本也能达成同样效果，但它会连带把 `probe`/`login`/`porthack` 的节奏
一起打掉；本次只解锁清痕，两者正交可叠加。

### 17.4 targets=0 的误读（真实发生的 UX 缺陷）

实测 `LogOutput.log`：

```
plan: targets=1 skipped=10 steps=21 creds=True loginSteps=1 speed=Normal
网络教育档案馆 :: admin via login (admin) - skipping port cracks | users=1 known=1 adminPass=set seclevel=6 ports=1
plan: targets=0 skipped=11 steps=11 creds=True  loginSteps=0 speed=Fast
plan: targets=0 skipped=11 steps=11 creds=True  loginSteps=0 speed=Instant
plan: targets=0 skipped=11 steps=11 creds=False loginSteps=0 speed=Instant   ...（共 10 次）
```

第一行证明 **login 成功且端口被跳过**；`creds=True` 与 `creds=False` 的结果完全相同，
说明差异不在凭据上。

真因：用户可达的 11 台机器**全部已归玩家**（`adminIP` 指向自己），
`skip owned`（默认开）把它们过滤干净 → `targets=0` → 没有登录步骤可建
（`loginSteps=0`）。那 `steps=11` 是 v1.11.1 的「被跳过的机器照样清痕」，
**每台只抹了 log**，却因为逐台在终端滚出机器名，被读成「每台都重跑了一遍流程」。

修法：`HackRun.Finish` 在 `_targets.Count == 0` 时直接写明原因与出路 ——

```
[autohack] No targets: all N reachable node(s) were filtered out (X already owned, Y cannot escalate).
[autohack]   'redo' re-hacks owned nodes; 'allnodes' sweeps the whole map.
```

### 17.5 失败静默也是缺陷

登录分支原本是：

```csharp
if (HackEngine.TryLogin(target, out var credential)) { /* 打印成功 */ }
break;      // 失败分支什么都不打印
```

失败静默把「为什么没跳过」这条最有价值的信息藏了起来，直接导致本次误判。
现改为打印 `login unavailable (<诊断>)`，并经 `Diag.LogInfo` 落进 BepInEx 日志：

```
[autohack] <机器名> :: login unavailable (users=5 known=1 adminPass=set seclevel=5 ports=4)
```

`HackEngine.CredentialReport` 摊开全部前提：`users` 条数、其中 `known` 条数、
`adminPass` 是否为空、`securityLevel`、可破端口数。

同时运行计划记一行，便于事后从日志还原：
`plan: targets=… skipped=… owned=… hopeless=… steps=… creds=… loginSteps=… speed=…`

### 17.6 交付

- 产物：`D:\steam\steamapps\common\Hacknet\BepInEx\plugins\AutoHack.dll`
- 67072 字节，MD5 `51cfa993824f6d5978ddcb6886a06d90`
- 构建：`rm -rf src/AutoHack/obj src/AutoHack/bin && dotnet build src/AutoHack/AutoHack.csproj -c Release`
  → 0 警告 0 错误
- 版本：`AutoHackPlugin.cs` 的 `[BepInPlugin]` → `1.12.3`

## 18. v1.13.0：摘除全部日志探针

v1.12.2/v1.12.3 为定位「login 没跳过」曾临时加入三处诊断输出。问题查清后按用户
要求（「bug 全部解决，清除所有日志探针」）全部摘除：

| 探针 | 位置（v1.12.3） | 处置 |
|---|---|---|
| `Diag` 静态日志源 | `HackRun.cs` 字段 | 删除 |
| `plan: targets=… creds=… loginSteps=… speed=…` | `HackRun` 构造函数 | 删除 |
| `HackEngine.CredentialReport` | `HackEngine.cs` | 删除 |
| 成功分支的 `Diag.LogInfo(okLine + " \| " + report)` | `Apply` Login | 删除 |
| 失败分支的 `login unavailable (<诊断>)` + `Diag.LogInfo` | `Apply` Login | 降级为一行 `login unavailable - cracking ports` |

**保留的两条不是探针，是 Fail Fast，不随摘除移除：**

- `[autohack] <名> :: login unavailable - cracking ports` —— 失败可见，
  避免重新陷入「静默失败」这个已确认的缺陷（§17.5）。
- `[autohack] No targets: all N reachable node(s) were filtered out
  (X already owned, Y cannot escalate).` + `'redo' … 'allnodes' …` ——
  这是 §17.4 那个真实 UX 缺陷的修复，不是诊断。

摘除后本插件在 BepInEx 日志里只剩加载期两条（`AutoHack loaded (GUI).` 与
`self-check OK …`），运行期零输出。

**交付**：`AutoHack.dll` 66048 字节，MD5 `1f1630b978614e8020f72089fe7b115b`，
版本 `1.13.0`，构建 0 警告 0 错误。

## 19. HacknetSaveFix：修游戏本体的存档 NullReferenceException

### 19.1 症状

```
[Error  :   Hacknet] Error writing save data for user :
System.NullReferenceException
   at Hacknet.PlatformAPI.Storage.SaveFileManager.GetSaveFileNameForUsername(String username) IL<0x0001>
   at Hacknet.PlatformAPI.Storage.SaveFileManager.WriteSaveData(String saveData, String playerID) IL<0x0000>
```

注意 `for user :` 冒号后为空 —— 说明 `playerID` 是 null。

### 19.2 根因链（全在游戏本体）

```
OS.cs:148        public string SaveUserAccountName = null;      ← 默认就是 null
MainMenu.cs:103/150/235/315   ← 该字段只在这里被赋值
OS.cs:1522       writeSaveGame(SaveUserAccountName)           ← 当文件名传下去
OS.cs:1529       public void writeSaveGame(string filename)
OS.cs:1548       SaveFileManager.WriteSaveData(text, filename)
SaveFileManager.cs:238   GetSaveFileNameForUsername(playerID)
SaveFileManager.cs:223   "save_" + FileSanitiser.purifyStringForDisplay(username).Replace("_","-").Trim() + ".xml"
FileSanitiser.cs:9-12    if (data == null) { return null; }   ← 返回 null
                         ↑ 紧接着的 .Replace 打在 null 上 → NRE
```

栈里的 `IL<0x0001>` 正是 `purifyStringForDisplay` 返回 null 后**第一句**取成员的位置。

**触发条件**：任何**绕过主菜单**进入 OS 的入口。实测来源是用
HacknetHotReplace 的默认配置直接连进扩展设备 —— 那条路径不经过 `MainMenu`，
字段停在 null。

### 19.3 为什么危险

`WriteSaveData`（`SaveFileManager.cs:240-243`）把异常 catch 成一行错误日志：

```csharp
catch (Exception ex)
{
    Utils.AppendToErrorFile("Error writing save data for user : " + playerID + "\r\n" + Utils.GenerateReportFromException(ex));
}
```

**游戏不崩，但存档静默失败** —— 玩家以为存了，实际没写盘。这是本次最值得修的点。

顺带记一条游戏侧疏漏：`FileSanitiser.purifyStringForDisplay` 自己做了 null 防护
（返回 null），但调用处直接链式取成员 `.Replace(...)`，防护形同虚设。

### 19.4 修法（独立插件，不进 AutoHack）

`src/SaveFix/`，产物 `HacknetSaveFix.dll`，与 AutoHack 零耦合，可单独装卸。

```csharp
[HarmonyPatch(typeof(SaveFileManager), nameof(SaveFileManager.GetSaveFileNameForUsername))]
internal static class SaveFileNamePatch
{
    [HarmonyPrefix]
    private static void Prefix(ref string username)
    {
        if (!string.IsNullOrWhiteSpace(username)) { return; }
        username = Settings.isConventionDemo ? Settings.ConventionLoginName : Environment.UserName;
    }
}
```

**为什么用这套回落**：它与 `OS.cs:372` 逐字一致 ——

```csharp
username = ((SaveUserAccountName != null) ? SaveUserAccountName
          : (Settings.isConventionDemo ? Settings.ConventionLoginName : Environment.UserName));
username = FileSanitiser.purifyStringForDisplay(username);
```

即 `SaveUserAccountName` 为 null 时，`os.username` 正是由同一表达式算出的，
所以落盘文件名与 `os.username` 保持一致 —— 不另立规则。

两个字段的默认值不同，容易看错：
- `OS.cs:146` `public string SaveGameUserName = "";`（**读**路径，`OS.cs:1457`）
- `OS.cs:148` `public string SaveUserAccountName = null;`（**写**路径）

只在真的兜底时打一条 `LogWarning`：这是异常路径，静默会把
「有入口没设账号名」这件事藏起来。

### 19.5 交付

- 源码：`src/SaveFix/SaveFix.csproj` + `src/SaveFix/SaveFixPlugin.cs`
- 产物：`<Hacknet>/BepInEx/plugins/HacknetSaveFix.dll`，5120 字节
- MD5：`505df298c541768b0cc39a3d4d610806`
- 依赖：仅 BepInEx + 0Harmony（**不需要** PathfinderAPI）
- 构建：`rm -rf src/SaveFix/obj src/SaveFix/bin && dotnet build src/SaveFix/SaveFix.csproj -c Release` → 0 警告 0 错误
- 反编译核对（本插件自身产物）：`[HarmonyPatch(typeof(SaveFileManager), "GetSaveFileNameForUsername")]`
  + `Prefix(ref string username)` + 回落表达式一致

## 20. v1.14.0：四个独立工具（DEC 解密 / 内存转储 / 程序补全 / 自身加固）

四个工具都不进 `autohack run` 的自动入侵流程，只在显式调用时执行；命令与面板
TOOLS 区按钮走同一份实现（`ToolDispatch`）。本节记录全部游戏侧依据，每条都带
`文件:行号`，可 grep 复核。

### 20.1 DEC 解密：密码是反推出来的，不是暴力

`Hacknet.FileEncrypter`（`FileEncrypter.cs`，`public static class`）：

- `Encrypt(data, passcode)`（`:35-44`）逐字符：`num = data[i] * 1822 + 32767 + passcode`（`:40`），
  空格拼接后 `Trim()`。
- `Decrypt(data, passcode)`（`:46-59`）：`num3 = num - 32767 - passcode; num3 /= 1822;`（`:54-55`）。
- `EncryptString(data, header, ipLink, pass="", fileExtension=null)`（`:10-28`）产出的头部为
  `#DEC_ENC::<enc header>::<enc ipLink>::<enc "ENCODED" w/ real pass>::[<enc ext>]`（`:19`），
  `\r\n` 后接加密正文（`:25-26`）。**`"ENCODED"` 是固定明文**，这是反推的支点。
- `DecryptString(data, pass="")`（`:61-105`）返回 `string[6]`：`[0]`header、`[1]`ipLink、
  `[2]`正文（仅当 `[5]=="ENCODED"` 时非 null，`:90-93`）、`[3]`扩展名、`[4]`"1"/"0"、
  `[5]` = 头部第 4 段的解密结果。
- `TestingDecryptString(data, ushort pass)`（`:107-151`）同上，但直接收 `ushort` 密码
  —— 反推结果不必绕回字符串哈希，这是本工具使用的入口。
- `GetPassCodeFromString(code) => (ushort)code.GetHashCode()`（`:30-33`）。

**反推公式**：头部第 4 段是 `Encrypt("ENCODED", passcode)`，首字符 `'E'` 的密文恒为
`'E' * 1822 + 32767 + passcode = 158485 + passcode`。故

```
passcode = 首个密文数字 - 158485
```

反推后**必须用 `TestingDecryptString` 反验**（第 6 段是否等于 `"ENCODED"`），验不过即
放弃 —— 这是 Fail Fast，不是可选步骤。

> 实测（玩家存档 `Accounts/save_1.xml`）：39 个唯一 DEC 文件全部反推 + 反验通过，
> 共 43 层，其中 4 个是两层嵌套。层数上限取 16 防自引用。

### 20.2 内存转储：全部走游戏自身的往返对

`Hacknet.MemoryContents`（`MemoryContents.cs`）：

- 常量 `EncryptionPass = "19474-217316293"`（`:11`）、
  `FileHeader = "MEMORY_DUMP : FORMAT v1.22 ----------\n\n"`（`:13`，**长度 39**）。
- 四个列表：`DataBlocks`（`:15`）、`CommandsRun`（`:17`）、
  `FileFragments`（`:19`，`List<KeyValuePair<string,string>>`）、`Images`（`:21`）。
- `GetCompactSaveString()`（`:134-144`）把 `Commands>`→`CM>`、`Command>`→`c>` 等九处缩写；
  `ReExpandSaveString(save)`（`:146-158`）逆向展开。
- `GetEncodedFileString()`（`:160-165`）= `FileHeader` + `generateBinaryString(512).Substring(0,400)`
  + `"\n\n"` + `FileEncrypter.EncryptString(compact, "MEMORY DUMP", "------", EncryptionPass)`。
- `GetMemoryFromEncodedFileString(data)`（`:167-175`）反向切分：
  `data.Substring(FileHeader.Length + 400 + 2)` = 偏移 **441**。

> **游戏自身缺陷**：`GetSaveString()`（`:23-65`）的 `FileFragments` 分支遍历的是
> `CommandsRun.Count` 而不是 `FileFragments.Count`（`:48`，同缺陷另见 `:39`），且闭合标签写成 `</Command>`。
> 当 `FileFragments.Count > CommandsRun.Count` 时抛 `IndexOutOfRangeException`。
> 查看与导出都必须兜住它 —— 否则一个坏存档就能把面板绘制线程带崩。

导出落点与游戏一致：`MemoryDumpDownloader`（`MemoryDumpDownloader.cs:92-99`）在玩家机
`home` 下建 `MemDumps` 子夹，文件名走 `Utils.GetNonRepeatingFilename(stem, ".mem", folder)`。

### 20.3 程序补全：exe 数据游戏早已算好

`Hacknet.PortExploits.populate()`（`PortExploits.cs:38-287`）在启动时用
`Random random = new Random(17021990)`（`:47`）与 `MSRandom rng = new MSRandom(17021990)`（`:48`）
把全部 `crackExeData[port] = Computer.generateBinaryString(500, rng)` 预计算好
（`:56` 起，逐端口），期间临时替换 `Utils.random` 再还原（`:49-50`、`:286`）。

表：`portNums`（`:12`）、`exeNums`（`:14`）、`services`（`:16`）、`cracks`（`:18`，port→exe 名）、
`crackExeData`（`:20`）、`crackExeDataLocalRNG`（`:22`）、`needsPort`（`:24`）；
`EXE_FILE_LENGTH = 500`（`:10`）。全部 `public static`，跨程序集可用。

**故本工具不重新生成任何二进制** —— 参考实现里的 `SubtractiveRNG`/`gen_bin` 只是游戏
`MSRandom` 的 Python 复刻，游戏里已经有现成的表；自写 RNG 只会与游戏产生不一致。

判重与落点照游戏既有写法：`Folder.searchForFile(name)`（`Folder.cs:88-97`）为 null 才
`new FileEntry(data, name)` 加入 `/bin`（同 `MissionFunctions.cs:284/293`、
`DLCIntroExe.cs:593-594`）。

### 20.4 自身加固：端口必须走 Pathfinder，字段必须同步

`Hacknet.Computer` 的安全字段：`securityLevel`（`:41`）、`traceTime`（`:43`，构造置 `-1f`，`:118`）、
`portsNeededForCrack`（`:45`）、`hasProxy`（`:83`）、`proxyOverloadTicks`（`:85`）、
`startingOverloadTicks`（`:87`）、`proxyActive`（`:89`）、`firewall`（`:97`）。

`addProxy(float time)`（`:243-252`）的语义是**一次设定四者**：`hasProxy = true`、
`proxyActive = true`、`proxyOverloadTicks = time`、`startingOverloadTicks = proxyOverloadTicks`。
只改 `hasProxy` 而不改 overload 计数，会让 `DisplayModule.cs:670` 按
`proxyOverloadTicks / startingOverloadTicks` = `0/0` 算进度条。

`hostileActionTaken()`（`:294-308`）只在 `os.connectedComp.ip == ip` 且 `traceTime > 0f` 时
启动追踪（`:296-301`）。玩家不会连自己，故给玩家机设 `traceTime = 1f` 不会让玩家被追踪。

**端口**：Pathfinder 用 Harmony Prefix 接管了 `Computer.openPort`（`ComputerExtensions.cs:184-199`）、
`openPorts`（`:201-204`，直接 `return false`）、`closePort`（`:221-235`）、
`isPortOpen`（`:243-252`）。原版 `portsOpen` 列表因此**永不更新**，状态改存
`PortState.Cracked`（`PortState.cs:40`）。正确写法是 `state.SetCracked(true, ipFrom)`
（`PortState.cs:42-53`，内部调 `Computer.openPort`/`closePort`）。

原版 15 个协议端口由 `ComputerExtensions` 静态构造用
`PortExploits.portNums ∩ PortExploits.services` 构建 `OGPorts`（`:48-52`）。`OGPorts` 是
`internal`，跨程序集不可见 —— 本工具改用公开的
`PortManager.GetPortRecordFromNumber(port)`（`PortManager.cs:90-99`）取得同一批记录，
口径与 Pathfinder 内部一致。

`DisplayModule.cs:429` 与 `:623-660` 在 `portsNeededForCrack > 100` 时走「INVIOLABILITY
DETECTED」特效分支，只按数字位数生成随机字符串，**不遍历该数值** —— 故取
`9999998` 是安全的，不会造成卡顿。

### 20.5 合规：参考实现是 GPL-3.0，本仓库是 MIT

功能清单的来源 [TesterNaN/Hacknet_Save_Editor](https://github.com/TesterNaN/Hacknet_Save_Editor)
是 **GPL-3.0** 授权的 Python 3.7 + Tkinter 程序（单文件 `main.py`）。本仓库是 **MIT**。

**只参考其功能机制与存档字段含义，未复制任何代码。** 具体地：它的
`brute_force_passcode`/`decrypt_layer`/`decrypt_all_layers` 是 Python 实现，
本仓库的反推内核是依据游戏 `FileEncrypter` 源码重新推导的（`MAGIC = 158485` 是从
`FileEncrypter.cs:40` 直接算出的，不是从参考实现抄来的）；它的 `SubtractiveRNG`/`gen_bin`
完全没有采用（游戏 `PortExploits` 已有现成表）；`makeMyComputerUnbreakable` 的字段
取值经游戏源码逐条核对后重写。

### 20.6 交付

- 源码：`src/AutoHack/{DecTools,MemTools,ExeTools,HardenTools,ToolFiles,ToolDispatch}.cs`
- 入口：`AutoHackPlugin.cs`（子命令分派 + help）、`HackPanel.cs`（TOOLS 区，
  `PanelAction` 改为 `(Kind, Verb)` 记录结构）、`HackOverlay.cs`（工具执行路径）
- 产物：`<Hacknet>/BepInEx/plugins/AutoHack.dll`，81408 字节
- MD5：`0220d6ad083d792753be1ce8bd1a725d`（已被 v1.14.1 取代）
- 构建：`rm -rf src/AutoHack/obj src/AutoHack/bin && dotnet build src/AutoHack/AutoHack.csproj -c Release` → 0 警告 0 错误
- 验收读数：DEC 反推 39/39 通过（43 层）；内存切分偏移 441 与往返逐字节一致

## 21. v1.14.1：两处工具缺陷修复

用户实测报回两条：「清理 log 没包括玩家的机器」「全部程序显示给予但实际无效果」。
两条都是真缺陷，根因都已定位到具体代码行。

### 21.1 全部程序无效果 —— 自造的长度守卫恒真

`ExeTools` 原本用 `data.Length == PortExploits.EXE_FILE_LENGTH` 判「这份数据可用」。
该判据是错的，两层原因：

1. **`EXE_FILE_LENGTH = 500` 是"请求长度"，不是产物长度。** 它在游戏里从未被任何代码
   使用（全仓 grep 只有 `PortExploits.cs:10` 一处声明，无读者）。`Computer.generateBinaryString(500)`
   （`Computer.cs:1578-1588`）先开 `byte[length / 8]` —— 即 `byte[62]` —— 再逐字节
   `text += Convert.ToString(array[i], 2)`（`:1585`）。**`Convert.ToString(byte, 2)` 不补前导零**，
   每字节产出 1~8 位，62 字节合计约 445 字符。
2. 存档实证：玩家 `/bin` 里 `SSHcrack.exe` 的 data 实测 **len = 445**。

于是 `data.Length == 500` 恒假 → 表核对打印 `0/37`，循环里 37 个程序全部判为
"has no usable exe data" 而 skip，最终 `added 0, skipped 37`。这就是「显示给予但无效果」
的全部原因 —— **数据源本身是对的**（`PortExploits.crackExeData[port]`，与游戏
`ComputerLoader.filter` 的占位符实现 `#SSH_CRACK#` 等取的是同一张表，`ComputerLoader.cs:2001-2029`）。

**修法**：判据改成「非空/非空白」。不引入任何长度门槛 —— 长度是游戏 RNG 的产物，
不是契约。修复后同一份存档的预期读数：`added 28, skipped 9`（9 个已存在；
`SSHcrack.exe(1)` 是重复副本，`cracks` 表里没有这个名字，不计入）。

游戏侧消费链已逐环核实自洽，无需改动：`ProgramRunner.AttemptExeProgramExecution`
（`ProgramRunner.cs:686`）→ `GetFileIndexOfExeProgram`（`:658-684`，查询名先
`.Replace(".exe","").ToLower()`；内置程序 `porthack/forkbomb/shell/tutorial/notes`
返回 `int.MaxValue` 不走 /bin；其余在 `folder.files` 里按「原名 / 去 .exe / 去 .exe 且小写」
三种比较）→ `:689` 只从 `os.thisComputer.files.root.searchForFolder("bin")` 取 →
`:700-710` 按**内容**匹配 `crackExeData[n]` 或 `crackExeDataLocalRNG[n]` →
`:768-800` `needsPort` 类程序要求目标机开着对应端口（否则 "Target Port is Closed"）→
`:796` 兼容性闸门 → `:802` `os.launchExecutable`。

### 21.2 清痕不含玩家机器 —— 玩家机被两处显式排除

- `HackEngine.ResolveTargets`（`HackEngine.cs:423`）：
  `if (comp == null || comp.disabled || ReferenceEquals(comp, os.thisComputer)) { continue; }`
  —— 玩家机既不入 `Targets` 也不入 `Skipped`。
- `HackRun.BuildSteps` 只对 `targets`（`:580-583`）与 `skipped`（`:599-605`）追加 `CleanLogs`，
  玩家机两处都不在。

**结果**：玩家 `/log` 从来没被清过。存档实证 —— 玩家机 `/log` 有 21 条痕迹
（`@0_Connection:_from_…`、`@298_FileDeleted:…` 等），全部来自玩家自己的连接动作。

**修法**：`options.ClearLogs` 时在**全部步骤之后**追加一条
`new HackStep(HackStepKind.CleanLogs, os.thisComputer, default, null)`。

排序是刻意的，不是随手放末尾：玩家的 `/log` 记的是「谁连过我」，入侵过程中每连一台
都会往玩家自己机器上写一条，提前清会被后续步骤重新写回来。放末尾才是终点动作。

**不需要等断开** —— 这是与普通目标的关键差别。普通目标的 `CleanLogs` 必须排在
`Disconnect` 之前，因为回显的 `rm` 走 `Programs.rm`，作用域取自
`os.connectedComp`（`Programs.cs:956`）。但玩家机的这条 `CleanLogs` 的 command 传
`null`（不产生 `rm` 回显），实际删除走 `HackEngine.ClearLogs` →
`Computer.deleteFile(ipFrom, "*", folderPath)`，而
`Programs.getFolderFromNavigationPath`（`Programs.cs:1749-1770`）**只读 `path` 与
`startFolder`，不看 `os.connectedComp` / `os.navigationPath`**。故任何时刻调用结果一致。

**权限门禁**：`Computer.deleteFile`（`Computer.cs:508-517`）对玩家机放行 ——
`ipFrom == ip == os.thisComputer.ip` 命中 `!ipFrom.Equals(ip)` 的反面。
`Folder.searchForFolder`（`Folder.cs:76-86`）**不递归**，只比直接子夹，而玩家
`files.root` 下 `home/log/bin/sys` 平级，故 `root.searchForFolder("log")` 直接命中。

### 21.3 交付

- 改动：`ExeTools.cs`（两处判据）、`HackRun.cs`（BuildSteps 末尾追加玩家机清痕）
- 产物：`<Hacknet>/BepInEx/plugins/AutoHack.dll`，81408 字节
- MD5：`2c9ab2c456ce36a4e038803415e371ad`
- 构建：0 警告 0 错误
- 验收读数（离线推算，基于 `save_1.xml`）：exes `added 28 / skipped 9`（修复前 0/37）；
  cleanlogs 新增覆盖玩家机 `/log` 的 21 条痕迹
## 22. v1.14.2：全仓审计后的六项修复

用 codebase-memory 重建索引（**734 nodes / 1767 edges**，17 个 C# 文件，排除
`.git`/`buildlogs`/`decompiled`/`refs`/`release`）后，按「反模式 / 死代码 / 坏味道 / 性能 /
游戏原生 API 未对齐 / 语言特性」六类做了一轮审计，落地六项修复。

### 22.1 游戏原生 API 未对齐（3 处）

**A1 — 三处手写目录解析。** `ToolFiles.Home`、`ToolFiles.MemDumps`、`ExeTools` 都各自
`searchForFolder` + 手工 `new Folder(...)` + `folders.Add(...)`。游戏本身就有
`Computer.getFolderFromPath(string path, bool createFoldersThatDontExist = false)`
（`Computer.cs:1628-1636`）→ `getFolderPath`（`:1638-1665`）按 `/` 与 `\\` 切分、逐段
查找、缺失且允许时 `folders.Add(new Folder(array[i]))`，再 `getFolderAtDepth`（`:1602-1616`）
定位。三处改为一行调用，`MemDumps` 还顺带把两步并成 `"home/MemDumps"` 一次解析。

**注意 `getFolderPath` 的静默缩短行为**：找不到且 `createFoldersThatDontExist == false` 时
不 `list.Add`，返回的路径会缺段 —— 因此只有传 `true` 才等价于原手写逻辑。

**A2 — proxy 四字段。** `HardenTools` 原本逐字段赋值 `hasProxy` / `proxyActive` /
`proxyOverloadTicks` / `startingOverloadTicks`，而注释里自己就写着「addProxy 的语义就是
一次设定四者」。改用 `comp.addProxy(Inviolable)`（`Computer.cs:243-252`，`time > 0f` 时
四者同步置位），语义完全等价。`Inviolable` 是 `int` 常量，`addProxy` 收 `float`，靠隐式转换。

### 22.2 性能

**B1 — 内存查看 60 次 `os.write` 合并为 1 次。** `MemTools.View` 原本 `for (var i = 0; i < shown; i++)
os.write(lines[i]);`，`shown` 上限 `MaxViewLines = 60`。`OS.write`（`OS.cs:1726-1737`）内部走
`DisplayModule.cleanSplitForWidth`（逐词字符串拼接），而 `Terminal.writeLine`
（`Terminal.cs:356-364`）本就把正文按 `'\n'` 拆行入 history —— 逐行调用等于把这段逐词
开销乘以行数。游戏自身也传多行串（`DLCIntroExe.cs:213`）。改为 `os.write(string.Join("\n", lines, 0, shown))`。

### 22.3 死代码与坏味道

**C1 — `HackEngine.Ports` 的兜底分支不可达。** 证据链：Pathfinder 用 `decompiled/pathfinder/Pathfinder.Replacements/ContentLoader.cs:341`
的 `Computer.ports` executor 完全替换了游戏原生加载器 → 走 `PortManager.LoadPortsFromStringVanilla`
（`PortManager.cs:238-262`）→ `comp.AddPort(record)` → `ComputerExtensions.cs:75-85` →
`record.CreateState(comp)` → `PortRecord.cs:33-36` `new PortState(...)` → `AddPort(PortState)`
**只写 `PortTable`，不回写 `comp.ports`**。而 `GetAllPortStates()`（`ComputerExtensions.cs:135-138`）
= `PortTable.GetOrCreateValue(comp).Values.ToList()`，`ComputerExtensions.cs:201-204` 的
`OpenPortsPrefix` 又直接 `return false` 拦掉原生 `openPorts`。故 `states.Count > 0` 必先返回。
唯一仍写 `comp.ports` 的是 `DLC1SessionUpgrader.cs:57-61`（只对 `ispComp` 加 443/6881，且不设
`portsNeededForCrack`）。

**决定保留并注释**，而非删除：替换机制一旦被别的 mod 改掉，这里会静默返回空端口表 ——
那是比多几行代码更糟的失效方式。

**C2 — v1.13.0 漏掉的绘制探针。** `HackOverlay` 里 `_drawLogged` + `Log.LogInfo($"Overlay drawing
at {screen.Width}x{screen.Height}.")` 是「摘除全部日志探针」时漏下的一条。摘掉后 `Log` 字段
失去唯一用途，连 `using BepInEx.Logging;` 一并移除。

**C4 — 两处同构递归。** `DecTools.Collect` 与 `MemTools.Collect` 结构完全一致，差异只有
谓词（`IsEncrypted` vs `StartsWith(FileHeader)`）与是否跳 `log`/`sys`。抽成
`ToolFiles.Collect(Folder, List<FileEntry>, Func<string, bool> match, string[] skipFolders)`，
`skipFolders` 传 `null` 表示不跳。

### 22.4 语言特性 / 异步 / 并发：已评估并否决

| 手段 | 判定 | 依据 |
|---|---|---|
| `Span<T>` / `ArrayPool` / `stackalloc` | **不可用** | net472 + `LangVersion 13` + 无任何 `PackageReference`（`AutoHack.csproj` 只引 0Harmony / BepInEx.Core / BepInEx.Hacknet / PathfinderAPI / Hacknet / FNA），`refs/assemblies/netfx-all/` 无 `System.Memory` |
| `async/await` | **禁用** | 见 §11.1：全状态主线程独占，逐帧步进机即协程等价物；v1.8.0 已用 `Collection was modified` 证明过 |
| 多线程 / `Parallel` | **禁用（工具路径）** | `ExeTools` 37 个 port、`DecTools` N 个文件虽是纯 CPU，但都要写 `folder.files` 与 `os.write`。现状（`ConcurrentDictionary` 仅用于命令线程→游戏线程入队，`PendingRuns.cs:26`）已是正确的最小用法 |

### 22.5 审计确认**无问题**的项（勿重复排查）

- **无真死代码**：全文件声明名引用计数扫描，6 个「零引用」全是 BepInEx/Harmony 反射入口
  （`AutoHackCommand` / `OnOSDraw` / `OnOSDrawPrefix` / `PostLoad` / `Unload`）+ 编译器合成 `IsExternalInit`。
- **异常边界已对齐**：`HackOverlay.RunTool` try/catch ✓、`PendingRuns.OnOSUpdate` try/catch ✓、
  `HackRun.Tick` 无 catch（正确，不应吞）✓。
- **面板布局数学一致**：实测 `DrawOptions` 推进 = 60+52+60+88+14 = **274 = `OptionsBlockHeight`** ✓；
  `ToolsBlockHeight` 按按钮数参数化 ✓；`BodyHeight` 三态均含 `ToolsBlockHeight` ✓。

### 22.6 交付

- 改动：`ToolFiles.cs`（A1 + C4）、`ExeTools.cs`（A1）、`HardenTools.cs`（A2）、
  `MemTools.cs`（B1 + C4 + `IsDump`）、`DecTools.cs`（C4）、`HackEngine.cs`（C1 注释）、
  `HackOverlay.cs`（C2）、`AutoHackPlugin.cs`（版本 1.14.2）
- 产物：`<Hacknet>/BepInEx/plugins/AutoHack.dll`，81408 字节
- MD5：`7ee5d4bb15490985599557e1cc06a904`（v1.14.1 为 `2c9ab2c456ce36a4e038803415e371ad`，已变 → 改动确已进 IL）
- 构建：0 警告 0 错误

## 23. v1.15.0：两条缺省翻转 + 玩家机日志开关

### 23.1 玩家自己的 /log 独立成开关，缺省关

v1.14.1 把玩家机清痕塞进了 `if (options.ClearLogs)`（目标清痕开关）。这混淆了两件语义
完全不同的事：

| 开关 | 抹的是什么 | 该谁决定 |
|---|---|---|
| `ClearLogs` | 「我入侵别人留下的证据」 | 入侵者的战术选择，缺省开合理 |
| `ClearOwnLogs` | 「玩家自己的操作史」—— 谁连过他、他读过什么文件 | **玩家自己的数据**，缺省必须关 |

抹掉后者不属于「顺手」的范畴，故拆成独立选项 `ClearOwnLogs`，缺省 `false`，
面板文案 `wipe my logs`。`HackRun.BuildSteps` 里它被移出 `ClearLogs` 块，单独判定。

同时把 `ClearLogs` 的面板标签由 `wipe logs` 改为 **`wipe target logs`** ——
有了 own 开关后，原标签是歧义源。

**面板零高度改动**：`DrawOptions` 第 4 行（`CheckRowHeight` 第四次）原本只有
`upload marker` 独占左列，**右列是空的**，故新增勾选框不触碰任何高度常量
（`OptionsBlockHeight = 274`、`ToolsBlockHeight`、`BodyHeight` 三态全不变）。

### 23.2 `use known creds` 缺省翻转为关

原缺省 `true`。问题不在实现而在**它架空了玩法**：`adminPass` 是目标机的公开字段，
已知账密登入成功即 `giveAdmin`（`Computer.login` 内部直接调，`Computer.cs:851-855`），
此后该目标的**破端口 / 解防火墙 / porthack 三套机制整体跳过** —— 缺省开启等于
默认绕过游戏的核心玩法。改为缺省 `false`，要便利性再显式 `creds`。

命令行解析的 `var useCredentials = false;`（`HackTypes.cs` `Parse`）与面板
`HackPanelState.UseCredentials` 缺省同步。`CredentialAliases` / `NoCredentialAliases`
两组合法值保留不变，故旧脚本写 `creds` 仍照常生效。

### 23.3 交付

- 版本 1.14.2 → **1.15.0**（`AutoHackPlugin.cs:12`）。
- 产物 81920 B / MD5 `6823f1de25a98881dd6577cc944fd9df`（v1.14.2 为 81408 B /
  `7ee5d4bb15490985599557e1cc06a904`，已变 → 改动确已进 IL）；构建 0 警告 0 错误。
- 改动文件：`HackTypes.cs`（record 加第 5 个参数 + 别名 + 两个缺省）、
  `HackRun.cs`（玩家机清痕移出 `ClearLogs` 块）、`HackPanel.cs`（state 字段 +
  `ToOptions` + 新勾选框 `IdBase+23` + 标签改名）。
- **未标定分级计划树**：面板显示的 0/12 是「从未调用 `mark_task`」，
  与实际完成度无关（四工具 v1.14.0/1/2 已交付并发布）。

## 24. v1.16.0：两条缺省翻转 + 三个远程工具

本轮改动的依据是 `docs/EXTENSIONS.md` §5.2「Action 全集」—— 那张表列的是游戏
自己往节点塞文件、删文件、摘节点时用的官方动作。三个新工具不是「另起一套」，
而是把官方动作的语义搬到玩家侧，并保留游戏自身的权限门禁。

### 24.1 两条缺省翻转为「关」

| 项 | 原缺省 | 新缺省 | 理由 |
|---|---|---|---|
| `ClearLogs`（`wipe target logs`） | 开 | **关** | 清痕会**改写对方机器状态**（抹掉它的操作史），属「玩家明确要求才做」的动作。默认开会让只想打下来看看的玩家在不知情时抹掉对方 `/log` |
| `Disconnect`（`anti-trace dc`） | 开 | **关** | 保持连接是更中性的默认；断开是「反追踪」这一**特定目的**的手段（追踪只在连着目标时推进），且它同时终止会话、清空 `navigationPath` |

`ClearOwnLogs` 在 v1.15.0 已是缺省关（§23.1），本轮不变。
三处同步：`HackTypes.Parse` 的局部缺省、`HackPanelState` 的属性缺省、
`AutoHackPlugin` 的 help 文本。

**反向别名（必补）**：缺省翻转后，原有的 `nologs` / `stay` 变成空操作 ——
它们表达的是「新缺省」，玩家无从命令行**打开**这两项。故新增：

- `WipeLogsAliases = ["logs", "wipelogs", "wipe-logs"]` → `clearLogs = true`
- `LeaveAliases = ["dc", "leave", "disconnect"]` → `disconnect = true`

这与 v1.15.0 翻转 `useCredentials` 时的处理一致（`creds` / `nocreds` 两个方向
都保留）。**翻转缺省必须同时保证两个方向都可表达**，否则等于删掉功能。

### 24.2 三个远程工具（`RemoteTools.cs`）

依据 `EXTENSIONS.md` §5.2 的三个官方 Action：

| 官方 Action | 实现类 | 官方做法 | 本 mod 取法 |
|---|---|---|---|
| `<CopyAsset>` | `SACopyAsset` | `folderFromPath.files.Add(new FileEntry(fileEntry.data, DestFileName))` —— 无权限门禁、无重名处理 | `Computer.canCopyFile` + `ToolFiles.Write`（保留权限语义与联机同步，重名走游戏 `GetNonRepeatingFilename`） |
| `<DeleteFile>` | `SADeleteFile` | `folderAtPath.files.Remove(fileEntry)` —— 无权限门禁 | `Computer.deleteFile(ip, "*", path)`（保留权限门禁） |
| `<HideNode>` | `SAHideNode` | `do { visibleNodes.Remove(IndexOf(c)) } while (Contains(IndexOf(c)))` | **逐字照抄该循环**（见下） |

**为什么不直接调 `Programs.scp` / `Programs.rm`**：两者内部都有 `Thread.Sleep`
（`scp` 每文件 250ms + 进度点 200ms×最多 20 次，`Programs.cs:629/697`；
`rm` 每文件 200ms×3~26，`Programs.cs:1017`），在游戏线程同步跑会整帧卡住 ——
与「不在游戏线程调 `probe`/`login`」同一条约束。故只取它们的**语义**，
动作走不睡的原语。

#### 24.2.1 `pull`：下载当前目录全部文件

落点路由**逐条照抄** `Programs.scp`（`Programs.cs:632-655`）：
`.exe` → `/bin`（落下即可运行）、`.sys` → `/sys`、`@` 开头 → `/home/dl_logs`、
其余 → `/home`。改掉会让「下载的破解程序不能直接跑」。

「当前目录」由 `Programs.getFolderFromNavigationPath(os.navigationPath, comp.files.root, os)`
解出（`Programs.cs:1749`）—— 与 `Computer.deleteFile` 内部用的是**同一个函数**
（`Computer.cs:523/549`），故报告与动作按构造一致，不会「报 A 删 B」。
**路径必须先快照**：`Programs.disconnect` 会 `os.navigationPath.Clear()`
（`Programs.cs:328`），断开后再读就是根目录。

#### 24.2.2 `purge`：删除当前目录全部文件

走 `Computer.deleteFile(ip, "*", path)`。其 `"*"` 分支**先快照文件名再逐个递归**，
故遍历中删除不会漏项。

**与 `ClearLogs` 的关键差别（刻意不统一）**：`HackEngine.ClearLogs` 在
`deleteFile` 之后**无条件** `logFolder.files.Clear()` —— 那是「证据必须消失」的
硬承诺，玩家要求的是结果而非过程。`purge` 则**如实复核**（`before - after`）并
回显被拒数量：这是玩家显式发起的删除，权限门禁是**游戏自己的访问控制**，
不该被绕过。同一套原语，两种承诺，故两种写法。

#### 24.2.3 `drop`：断开并摘掉当前节点

**摘节点的权威实现**是 `SAHideNode.Trigger`：

```csharp
do { oS.netMap.visibleNodes.Remove(oS.netMap.nodes.IndexOf(computer)); }
while (oS.netMap.visibleNodes.Contains(oS.netMap.nodes.IndexOf(computer)));
```

**循环而非单次 `Remove`** —— `visibleNodes` 里可能有重复下标：
`NetworkMap.discoverNode` 自带判重（`NetworkMap.cs:415-423`），但存档载入与
`DLC1SessionUpgrader` 等路径会直接 `Add`。单次删只去掉第一个，是静默的半成品。

**必须先断开**：官方三处摘节点都在断开之后 —— `AircraftDaemon.cs:229-234`、
`EndingSequenceModule.cs:244-245`、`ExtensionSequencerExe.cs:208-210`。
连着的时候摘，追踪与延迟反扑状态机还在跑一个已不在图上的节点。

**不设 `comp.disabled`**（反直觉，已取证）：`NetworkMap.Update` 每帧对 disabled
节点调 `bootupTick`（`NetworkMap.cs:126-131`），而 `bootTimer` 缺省 `0f`
（`Computer.cs:61`），`bootupTick` 立刻把它置回 `false`（`Computer.cs:311-317`）。
`disabled` 只在 `crash()`（:436）与 `reboot()`（:457）里被设成有意义的时长，
是 crash/reboot 的**临时态**，拿来当「已删除」是假的。

**边界如实记录**：节点仍在 `map.nodes` 里，只是不在 `visibleNodes`。默认口径
`ReachableComputers` 以 visibleNodes 为种子沿 links 展开（`HackEngine.cs:490-498`），
故摘掉后**默认扫描再也够不着它**；但显式 `allnodes` 走 `ConnectableComputers`
（`HackEngine.cs:600-602` 记录了这个全表口径的设计），仍会看到它。
逆操作是 `netMap.discoverNode(comp)`（`SAShowNode` 的写法）。

### 24.3 `HackEngine.SilentDisconnect` 抽取

`HackRun.Leave` 里的静默断开内联代码（约 30 行）抽成 `HackEngine.SilentDisconnect(os)`，
`Leave` 与 `RemoteTools.Drop` 共用一条。理由（为什么静音、为什么多人不静音）
随方法迁移，调用点只留一句 `<see cref>` 指向 —— 同一知识只写一次。

### 24.4 面板：三个按钮零高度改动

`HackPanel.Tools` 数组加三项（`PULL FILES` / `PURGE FILES` / `DROP NODE`，
后两个 `Danger: true` 用告警色）。行数由 `ToolRows` 自动算
（`(Tools.Length + ToolColumns - 1) / ToolColumns`，2 列 → 4 行），
`BodyHeight` 三个分支读的都是 `ToolsBlockHeight`，`DrawTools` 的推进量也读同一个
属性 —— **故加按钮不必改任何高度常量**（7 个按钮仍是 4 行，恰好未越界）。

### 24.5 交付

- 版本 1.15.0 → **1.16.0**（`AutoHackPlugin.cs:12`）。
- 产物 85504 B / MD5 `b062b6428d8c7d0c743639a20c87d654`（v1.15.0 为 81920 B /
  `6823f1de25a98881dd6577cc944fd9df`，已变 → 改动确已进 IL）；构建 0 警告 0 错误。
- 改动文件：新增 `RemoteTools.cs`；`ToolDispatch.cs`（三个动词 + help + 分派）、
  `HackEngine.cs`（`SilentDisconnect`）、`HackRun.cs`（`Leave` 改为委托）、
  `HackTypes.cs`（两个缺省 + 两组反向别名）、`HackPanel.cs`（两个属性缺省 +
  三个按钮）、`AutoHackPlugin.cs`（版本 + help）。

## 25. v1.17.0：面板文案中英双语

### 25.1 为什么不能硬编码中文

面板自绘，走 `GuiData.smallfont`（`HackPanel.cs:733`）。这个字段指向哪个字体取决于 locale：

| locale | 字体 | 体积 |
| --- | --- | --- |
| en-us（缺省） | 内置 `Content/Font16.xnb` | 6,150 B |
| zh-cn | `Content/Locales/zh-cn/Fonts/zh-cn_Font16.xnb` | 701,546 B |

百倍差距来自汉字字形表。en-us 下字体无汉字，`SpriteFont.DrawString` 遇到缺字形画 default character，硬编码中文只会得到一片方块。

切换发生在启动时：`Game1.cs:319` `LocaleActivator.ActivateLocale(code, Content)` → `LocaleFontLoader.LoadFontConfigForLocale` → 末尾赋 `GuiData.UITinyfont`/`UISmallfont`，并经 `GuiData.ActivateFontConfig(FontCongifOption)`（`GuiData.cs:181-191`）设 `smallfont`/`tinyfont`/`font`/`detailfont` 四个。**注意这四个与「`UISmallfont`/`UITinyfont` 从不赋值」是两码事** —— 后者是面板注释里那条老坑，指的是面板若去读 `UISmallfont` 会 NRE；而 `smallfont` 是会被本地化整体替换的。

游戏自己会为 CJK 调布局：`tinyFontCharHeight` 按 `ActiveLocaleIsCJK()` 分档（15/17/19 vs 10/14/16），并给 `smallFont.LineSpacing += 2`（`LocaleFontLoader.cs:31-34`）；`Button.cs:146` 另有 `num2 = CJK ? 4f : 0f` 的行高余量。

### 25.2 判定条件：认 `zh`，不认 CJK

`LocaleActivator.ActiveLocaleIsCJK()`（`LocaleActivator.cs:86`）回答的是「当前字体里有没有汉字」，`zh`/`ja`/`ko` 都算。但这里要问的是「该给玩家看哪份文案」—— 日文玩家拿到中文比拿到英文更糟。故 `Loc.cs` 用 `Settings.ActiveLocale.StartsWith("zh")`。

### 25.3 查表次序

1. `LocaleTerms.Loc(english)`（`LocaleTerms.cs:53-64`）—— 游戏自己的词表，未命中时原样返回，故比较返回值是否变化即知有无命中；
2. `Loc.Chinese` 本表；
3. 英文原文。

把游戏词表放第一位是为了将来官方补译时自动跟随。实测 zh-cn 的 `Hacknet_UI_Terms.txt`（528 条，UTF-16 + TAB 分隔）只覆盖 UI 通用词（`Disconnect`→断开、`Delete`→删除、`Back`→返回、`Exit`→退出、`Complete`→完成、`Failed`→失败），面板专有词（`RUN`/`SCOPE`/`SPEED`/`NORMAL`/`FAST`/`INSTANT`/`TOOLS`/`NETWORK`）**全部未收录**，故本表是主力。

### 25.4 语序：带数字的文案不拼接

中英语序不同（`3 NODE(S)` vs `3 个节点`），故 `Loc.Running`/`Done`/`LastRun`/`More`/`Ports` 每种语言各留一份完整格式串，而不是拼接 `T(...)` 的碎片 —— 后者一旦语序不同就拼不成句。`AdminTag()` 同理（`  admin` vs `  管理员`）。

### 25.5 实现

新增 `src/AutoHack/Loc.cs`（约 120 行）。`HackPanel.cs` 的 30 处字面量改为 `Loc.T(...)`；`Tools` 数组保留英文原文、**绘制时**才查表 —— 该数组是 `static readonly`，静态初始化可能早于 locale 生效。

标题跟随语言（`AUTOHACK` → 自动入侵）。终端 help 文本保持英文：那是命令行输出、走 `os.write` 的等宽字体，不在面板内，本轮不扩散范围。

布局零改动：`Width = 396`、`ContentWidth = 372`，汉字 16px/字 → 每行 23 字；`ColumnWidth = 182` → 每行 10 字。最长标签「清除目标日志」6 字 = 96px，余量充足。

### 25.6 交付

87552 B / `6b54d30232caf8f5817cc2c99ffb60b1`。构建 0 警告 0 错误。产物内 29 条中文串全部命中（UTF-16LE），英文原文同时保留 —— 双语共存，非硬替换。

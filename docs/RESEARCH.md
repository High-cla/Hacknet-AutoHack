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

---

## 6. 待办

初版五项目标已在 v1.0 全部完成，改为状态记录：

| # | 目标 | 状态 |
|---|---|---|
| 1 | 确认最终设计（范围/附加能力） | ✅ v1.0（范围＝全网扫描＋当前节点；附加＝可控时序／自动清理／面板） |
| 2 | 搭建项目骨架（csproj + 源文件） | ✅ v1.0（net472 / LangVersion 13 / 直接输出到游戏目录） |
| 3 | 实现自动入侵核心 | ✅ v1.0，v1.2 迁移到 Pathfinder 框架 API，v1.3 补跳板与追踪 |
| 4 | 编译输出到游戏目录 `BepInEx/plugins/` | ✅ 每次 `dotnet build` 自动落地 |
| 5 | 游戏内验证 | ✅ 加载顺序／命令注册已由日志证实；面板肉眼观感待真人确认（见 README「未验证」） |


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
调用点：`Pathfinder.Replacements.ContentLoader.cs:341,828`、`SaveLoader.cs:356` → 原版机器**也有** PortState。

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

Pathfinder 没有提供更上层的「已控」封装（`grep adminIP|IsOwned|owned` 全树仅命中 `ReloadExtensionNodes.cs:42` 的属性拷贝与存档读写两处），故本插件直接用 `comp.adminIP == os.thisComputer.ip` 判定。

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
| `ReachableComputers` | 2（定义 + 调用） |
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


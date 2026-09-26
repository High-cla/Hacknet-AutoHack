# Hacknet Extension 格式参考

游戏自带两个官方示例扩展，是本文的**第一手依据**：

```
D:\steam\steamapps\common\Hacknet\Extensions\
├── BlankExtension\   骨架：最小可运行扩展
└── IntroExtension\   教程：官方逐字段注释，覆盖全部标签
```

`IntroExtension/` 的每个文件都带开发者本人的注释，**比任何第三方 wiki 权威**。凡本文与 wiki
冲突处，一律以游戏实现（`decompiled/game-proj/Hacknet/`）与官方样本为准，并在文中标出差异。

本文档的用途：本仓库的 AutoHack 是 C# mod，不写 Extension；但 Extension 格式是**游戏自身
声明「怎么往节点塞文件、怎么加密文件、怎么造内存转储」的地方**，故它是游戏 API 的权威说明书。
§4（占位符）、§3.10（`<encryptedFile>`）、§3.11（`<memoryDumpFile>`）直接支撑了本仓库四个工具的实现依据。

---

## 1. 目录结构

官方两个样本的文件布局（`IntroExtension` 为全集）：

```
IntroExtension\
├── ExtensionInfo.xml          入口清单（必需）
├── Intro.txt                  任意文本资源
├── Logo.png                   游戏内 logo
├── WorkshopLogo.png           Steam 创意工坊预览图
├── Actions\                   条件行为集
│   ├── StartingActions.xml    ExtensionInfo 指定的启动行为
│   ├── HackerScriptActions.xml
│   ├── ExampleConditionalActionSet.xml
│   ├── LoopingOnConnectAction.xml
│   ├── ThemeSwapActions.xml
│   └── CreditsRunActions.xml
├── Factions\                  阵营
│   ├── IntroFaction.xml
│   └── ExampleFaction.xml
├── HackerScripts\             NPC 入侵脚本（游戏原生 DSL，见 §8）
│   ├── ExampleHack.txt        逐动词教学
│   ├── AllyHack.txt
│   ├── TrapHack.txt
│   └── HostileHackerHarrasment.txt
├── Missions\                  任务
│   ├── ExampleMission.xml     逐字段教学
│   ├── Intro\                 IntroMission1-6.xml + FactionWelcomeMessage
│   ├── IntroFac\              13 个教学任务（Daemons / Websites / People / …）
│   ├── BranchExample\         分支任务 7 个
│   └── Misc\
├── Nodes\                     计算机定义
│   ├── ExampleComputer.xml    **最重要的单文件**，覆盖全部节点标签
│   ├── PlayerComp.xml         玩家机定义（id 必须是 playerComp）
│   ├── MailServer.xml
│   ├── Faction\
│   └── Intro\
├── People\ExamplePerson.xml   人物（学术库/医疗库数据源）
├── Themes\                    自定义主题 + Backgrounds\
├── Docs\                      留言板 / 新闻稿的纯文本
├── Web\                       HTML 网页 + css
└── Music\                     .ogg
```

**目录名是约定不是强制**，只有 `ExtensionInfo.xml` 的位置固定。所有路径引用都相对扩展根目录。

---

## 2. ExtensionInfo.xml

`IntroExtension/ExtensionInfo.xml` 是官方全字段注释版。`<HacknetExtension>` 下：

| 标签 | 含义 |
|---|---|
| `<Language>` | `en-us` / `de-de` / `fr-be` / `ru-ru` / `es-ar` / `ko-kr` / `ja-jp` / **`zh-cn`** |
| `<Name>` | 扩展名，≤128 字符 |
| `<AllowSaves>` | 是否允许存档 |
| `<StartingVisibleNodes>` | 逗号分隔，开局即可见的节点 id |
| `<StartingMission>` | 开局加载的任务文件 |
| `<StartingActions>` | 新建会话时加载的条件行为集（做初始化：发程序、给阵营）。**不需要就写 `NONE` 或删标签** |
| `<Description>` | 游戏内显示，可多行 |
| `<Faction>` | 可写多个，每个一行 |
| `<StartsWithTutorial>` | 是否带教程 |
| `<HasIntroStartup>` | 是否走标准重启开机序列 |
| `<StartingTheme>` | 主题名或路径。基础主题名：`TerminalOnlyBlack` / `HacknetBlue` / `HacknetTeal` / `HacknetYellow` / `HackerGreen` / `HacknetWhite` / `HacknetPurple` / `HacknetMint` |
| `<IntroStartupSong>` | 游戏原曲写文件名（自动搜 `Content/Music` 与 `Content/DLC/Music`），自己的歌写扩展内路径 |
| `<SequencerTargetID>` | 序列器启动后玩家连上的节点 id |
| `<SequencerSpinUpTime>` | 序列器起转秒数 |
| `<SequencerFlagRequiredForStart>` | 启动所需的 flag |
| `<ActionsToRunOnSequencerStart>` | 按下按钮**那一刻**执行（不是起转完成时），故要自己 delay |

Steam 创意工坊组：

| 标签 | 约束 |
|---|---|
| `<WorkshopDescription>` | ≤8000 字符，可含换行 |
| `<WorkshopLanguage>` | |
| `<WorkshopVisibility>` | `0` 公开 / `1` 仅好友 / `2` 私有 |
| `<WorkshopTags>` | 目前只有 `Extension` 一个分类，原样保留 |
| `<WorkshopPreviewImagePath>` | 扩展内路径，≤1MB，`.png`/`.jpg`/`.gif`。**两个官方样本自相矛盾**：`IntroExtension` 注释写「1x1 正方形」，`BlankExtension` 写「16x9」—— 按 16:9 理解 |
| `<WorkshopPublishID>` | 首次发布后 Steam 自动回填。**务必留存此 ID** —— 丢了会作为新条目发布并丢失全部订阅者。只有写 `NONE` 才生成新 ID |

---

## 3. 节点 XML（`Nodes/*.xml`）

`Nodes/ExampleComputer.xml` 覆盖了全部标签，是本节的主依据。游戏侧加载器为
`ComputerLoader.cs`（共 2067 行）。

### 3.1 根标签

```xml
<Computer id="advExamplePC" name="Extension Example PC" ip="167.194.132.7"
          security="2" allowsDefaultBootModule="false" icon="chip" type="1">
```

| 属性 | 说明 |
|---|---|
| `id` | 节点唯一 id，扩展内引用用（`TargetComp` 等都填它） |
| `security` | 0–5。1–4 时自动 `portsNeededForCrack = security - 1`；**4 以上会自动追加其它安全设施**，官方建议显式覆盖具体字段 |
| `allowsDefaultBootModule` | 默认 true。true 时连上去会自动启动**文件里最后一个 daemon** 并投到 Display |
| `icon` | 省略则按 security 取默认。可选：`laptop` / `chip` / `kellis` / `tablet` / `ePhone` / `ePhone2`；DLC：`Psylance` / `PacificAir` / `Alchemist` / `DLCLaptop` / `DLCPC1` / `DLCPC2` / `DLCServer` |
| `type` | `1` 企业 / `2` 家用 / `3` 服务器 / `4` 空，或直接写 `empty`（不生成垃圾文件与 IRC） |

### 3.2 账号与安全字段

```xml
<adminPass pass="password" />
<account username="Matt" password="testpass" type="ALL" />
```

`account` 的 `type` 可用数字或名字：`ADMIN=0` / `ALL=1`（可删文件）/ `MAIL=2` / `MISSIONLIST=3`。
加载位置 `ComputerLoader.cs:462`（另一处 `:1909`）。

```xml
<ports>21, 22, 25, 80, 1433, 104, 6881, 443, 192, 554</ports>   <!-- :238 -->
<proxy time="2" />                                              <!-- :282 -->
<portsForCrack val="0" />                                       <!-- :299 -->
<firewall level="6" solution="Scypio" additionalTime="1.0"/>    <!-- :311 -->
<trace time="5678" />                                           <!-- :374 -->
<admin type="fast" resetPassword="false" isSuper="false"/>      <!-- :396 -->
<portRemap>web=1234,22=2</portRemap>                            <!-- :434 -->
<tracker />                                                     <!-- :504 -->
```

| 标签 | 语义 |
|---|---|
| `ports` | 该机开着的端口。**注意 Pathfinder 接管后端口状态存 `PortState.Cracked`，不读原版 `portsOpen`** |
| `proxy` | `time` 是 `BASE_PROXY_TICKS`（30 秒）的倍数，`1` 正常、`2` 长。**`-1` 表示移除** |
| `portsForCrack` | 允许 `porthack` 运行所需已开端口数 |
| `firewall` | `level` 设 `-1` 移除。`solution` 是玩家要破的答案，**`level` 应等于 `solution` 长度**（可更长，不可更短）。`additionalTime` 是每步额外评估耗时 |
| `trace` | 追踪秒数，`-1` 移除 |
| `admin` | 自动管理员。`basic` 约 15 秒后重置；`progress` 重置端口/防火墙/代理进度（玩家已是 admin 则不动）；`fast` 立即。超级管理员在 fast 模式立即重置密码。**security > 4 的机器用 `<admin type="none" />` 移除自动追加的管理员** |
| `portRemap` | 把默认端口映射成别的。左边可写端口号或别名：`ssh` `ftp` `web` `torrent` `medical` `smtp` `sql` |
| `tracker` | **被动追踪**：玩家断开时若在该机留下下载/删除日志，会被自动生成的 AI 黑客攻击（重置玩家，或让玩家走 ETAS —— 若玩家有 `CSEC_Member` flag） |

### 3.3 网络与位置

```xml
<dlink target="advExamplePC2" />                                    <!-- :357 -->
<positionNear target="advExamplePC2" position="1" total="3"
              extraDistance="0.1" force="false"/>                   <!-- :244 -->
```

`dlink` 是**单向**连接（从该机扫描可发现）。`positionNear` 把节点摆在目标周围：
`position` 是径向序号、`total` 是环绕节点总数、`extraDistance` 建议在 `-0.6` ~ `0.3`
（`0.1` 较好，过大会显得很远并可能出问题）、`force` 强制精确落位（跳过防重叠检查）。

### 3.4 文件

```xml
<file path="home" name="Test_File.txt">内容</file>
<file path="home/NewDirectory" name="Test_File">…</file>
<file path="bin" name="SSHCrack.exe">#SSH_CRACK#</file>
```

`path` 可多层，**目录不存在会自动创建**（`Computer.getFolderFromPath(path, createFoldersThatDontExist: true)`，
`Computer.cs:1628-1636`；底层 `getFolderPath` 在 `:1638-1665`，按 `/` 与 `\` 切分后逐段
`folders.Add(new Folder(name))`）。空内容会填 500 请求长度的随机二进制（`ComputerLoader.cs:163-166`）。

**官方落程序的位置就是 `path="bin"` 配 `#XXX_EXE#` 占位符** —— 与本仓库 ExeTools 的落点一致。

### 3.5 加密文件 —— DEC 的官方生成方式

```xml
<encryptedFile path="home" name="encrypted_File.dec" extension=".txt"
               ip="192.168.1.1" header="This is the header" pass="decryptionPassword">
  正文
</encryptedFile>

<encryptedFile path="home" name="easy_encrypted_File.dec"
               ip="192.168.1.1" header="This is the header">
  不提供 pass 则无需密码即可解密
</encryptedFile>
```

实现 `ComputerLoader.cs:147-181`：

```
path 默认 "home" / name 默认 "Data" / header 默认 "ERROR" / ip 默认 "ERROR" / pass 默认 ""
extension 默认 null / double 默认 false
正文为空 → generateBinaryString(500)
正文与 name 都过 filter()（占位符替换）
double=true → 先 EncryptString(正文, header, ip, pass, extension)
再（无论如何）EncryptString(上一层结果, header, ip, pass, double ? "_LAYER2.dec" : extension)
落盘：已有同名文件则改 data，否则 files.Add(new FileEntry(text, name))
```

**`double="true"` 就是官方产出的双层嵌套 DEC** —— 这解释了为什么多层递归解密是必需的
（本仓库存档实测 39 个 `.dec` 中有 4 个两层）。

底层密码学在 `FileEncrypter.cs`：`Encrypt` 为 `data[i] * 1822 + 32767 + passcode`（`:40`），
`Decrypt` 为其逆（`:54-55`）。本仓库 `DecTools.cs` 的 `MAGIC = 'E' * 1822 + 32767 = 158485`
反推公式即由此推出。

### 3.6 内存转储 —— `<memoryDumpFile>` 与 `<Memory>`

```xml
<memoryDumpFile name="testDump.md" path="home">
  <Memory>
    <Data><Block>test string one</Block><Block>test string two</Block></Data>
    <Commands><Command>connect 123.123.123.123</Command></Commands>
  </Memory>
</memoryDumpFile>
```

实现 `ComputerLoader.cs:183-207`：`path` 默认 `home`、`name` 默认 `Data` →
`MemoryContents.Deserialize(xmlReader)` → `memoryContents.GetEncodedFileString()` 落盘。
**注意 `name` 默认值是 `Data` 而非 `.mem` 后缀**，官方样本自己写 `testDump.md`。

节点级 `<Memory>`（`:1194-1198`）直接赋给 `c.Memory`：

```xml
<Memory>
  <Commands><Command>cd /log</Command><Command>rm *.log</Command></Commands>
  <Data><Block>This appears in the "files" section</Block></Data>
  <Images><Image>DLC/Sprites/Misc/DraculaTank</Image></Images>
</Memory>
```

`<Data>` / `<Commands>` / `<FileFragments>` / `<Images>` 四段与 `MemoryContents` 的四个列表一一对应。
本仓库 MemTools 的查看/导出/扫描三条路径即基于此格式。

### 3.7 其它

```xml
<customthemefile path="sys" name="Custom_x-server.sys" themePath="Themes/SecondaryTheme.xml"/>  <!-- :208 -->
<eosDevice name="Deliliah's ePhone 4S" id="eosIntroPhone" icon="ePhone2"
           empty="true" passOverride="notAlpine">                                               <!-- :1190 -->
  <note>TestNote
More text</note>
  <mail username="test@jmail.com" pass="thisIstheaccountpass" />
  <file path="eos/test" name="crackedFile.txt">…</file>
</eosDevice>
```

`eosDevice` 会在本机旁**额外生成一台 EOS 设备节点**并自动补齐应用与存档文件。
`note` 的文件名由**第一行内容**生成（空格替换成下划线）。

DLC 内容可用注释包裹，只在装了 Labyrinths 时生效：

```xml
<!--START_LABYRINTHS_ONLY_CONTENT-->
  …
<!--END_LABYRINTHS_ONLY_CONTENT-->
```

### 3.8 Daemons

官方样本里 daemon 是**节点上的「程序」**，最后定义的那个会被 `allowsDefaultBootModule` 自动启动。
全集（`ExampleComputer.xml`）：

| 类别 | 标签 |
|---|---|
| 邮件/文件 | `<mailServer name= color= generateJunk=>` + `<email recipient= sender= subject=>`；`<uploadServerDaemon name= folder= needsAuth= color=>` |
| Web | `<addWebServer name= url=>`；`<addOnlineWebServer name= url=>`（官方注明很 janky，可能不生效，每机只能一个） |
| 数据库 | `<deathRowDatabase />`、`<academicDatabase />`、`<MedicalDatabase />`、`<PointClicker />`（均从 `People.All` 取数）；`<ispSystem />` |
| 留言板 | `<messageBoard name=>` + `<thread>路径</thread>` |
| 医疗 | `<HeartMonitor patient="J_Stalvern"/>`（`patient` 是人物 id；死亡会加 `PatientID:DEAD` flag） |
| 列表/合约 | `<variableMissionListingServer name= iconPath= articleFolderPath= color= assigner= public= title=>`；`<missionHubServer groupName= serviceName= missionFolderPath= themeColor= lineColor= backgroundColor= allowAbandon=>` |
| 音乐/主题 | `<SongChangerDaemon />`、`<CustomConnectDisplayDaemon />`、`<LogoDaemon Name= ShowsTitle= TextColor= LogoImagePath=>`（标签体是 logo 下方多行文字）、`<LogoCustomConnectDisplayDaemon logo= title= overdrawLogo= buttonAlignment=left|middle|right />` |
| 其它 | `<CreditsDaemon Title= ButtonText= ConditionalActionSetToRunOnButtonPressPath=>`、`<FastActionHost />`（纯 delay 宿主，>50 个并发 action 时更省）、`<MarkovTextDaemon Name= SourceFilesContentFolder=>`、`<IRCDaemon themeColor= name= needsLogin=>` + `<user name= color=>` + `<post user=>` |
| 白名单 | `<WhitelistAuthenticatorDaemon SelfAuthenticating="true|false" Remote="节点id" />` |
| DLC | `<DHSDaemon groupName= addsFactionPointOnMissionComplete= autoClearMissionsOnPlayerComplete= themeColor= allowContractAbbandon=>` + `<agent name= pass= color=>`；`<DatabaseDaemon DataType= Permissions= Foldername= Color= AdminEmailAccount= AdminEmailHostID= Name=>` |

`DatabaseDaemon` 的 `DataType` 支持全部 .NET 基础类型、Hacknet 代码库内类型，以及官方模板：
`TextRecord`（`<Title>`/`<Data>`）、`OnlineAccount`（`ID`/`Username`/`BanStatus`/`Notes`）、
`CAROData`、`Account`、`SurveillanceProfile`、`AgentDetails`、`GitCommitEntry`（`<EntryNumber>`/
`<ChangedFiles><String>`/`<Message>`/`<UserName>`/`<SourceIP>`）。

---

## 4. 自替换占位符

结构：`#<标识符>#`。实现在 `ComputerLoader.filter(string s)`（**`ComputerLoader.cs:1995-2048`**），
一串 `.Replace()`。

> **wiki 的 `/reference/Placeholder` 不完整**：它漏了 `PLAYER_ACCOUNT_PASSWORD`、
> `PLAYERLOCATION`、`EXTENSION_FOLDER_PATH`、`GIBSON_IP`、`KAGUYA_EXE`、
> `PACEMAKER_FW_WORKING`/`PACEMAKER_FW_DANGER`；且把 `BINARYSMALL` 写成 1000 字符
> （实现是 `generateBinaryString(800)`，`:1997`）。下表以**实现**为准。

### 4.1 通用

| 占位符 | 展开为 | 行 |
|---|---|---|
| `#BINARY#` | `Computer.generateBinaryString(2000)` | `:1997` |
| `#BINARYSMALL#` | `generateBinaryString(800)`（**wiki 误写 1000**） | `:1997` |
| `#PLAYERNAME#` | `os.defaultUser.name` | `:1997` |
| `#PLAYER_IP#` | `os.thisComputer.ip` | `:1998` |
| `#PLAYER_ACCOUNT_PASSWORD#` | `SaveFileManager.LastLoggedInUser.Password` | `:1999` |
| `#RANDOM_IP#` | `NetworkMap.generateRandomIP()` | `:2000` |
| `#PLAYERLOCATION#` | 字面量 `"UNKNOWN"` | `:2031` |
| `#EXTENSION_FOLDER_PATH#` | `ExtensionLoader.ActiveExtensionInfo.GetFullFolderPath()` | `:2030` |
| `#GIBSON_IP#` | `os.GibsonIP`（DLC 段） | `:2045` |
| 制表符 | 四个空格 | `:2032` |

### 4.2 程序

**全部取 `PortExploits.crackExeData[port]`** —— 这是本仓库 ExeTools 数据源正确的最终佐证。

| 占位符 | 端口 | 行 | | 占位符 | 端口 | 行 |
|---|---|---|---|---|---|---|
| `#SSH_CRACK#` | 22 | `:2001` | | `#EOS_SCANNER_EXE#` | 13 | `:2015` |
| `#FTP_CRACK#` | 21 | `:2002` | | `#TRACEKILL_EXE#` | 12 | `:2016` |
| `#WEB_CRACK#` | 80 | `:2003` | | `#RTSP_EXE#` | 554 | `:2026` |
| `#DECYPHER_PROGRAM#` | 9 | `:2004` | | `#EXT_SEQUENCER_EXE#` | 40 | `:2027` |
| `#DECHEAD_PROGRAM#` | 10 | `:2005` | | `#SHELL_OPENER_EXE#` | 41 | `:2028` |
| `#CLOCK_PROGRAM#` | 11 | `:2006` | | `#FTP_FAST_EXE#` | 211 | `:2029` |
| `#MEDICAL_PROGRAM#` | 104 | `:2007` | | `#TORRENT_EXE#` | 6881 | `:2035` |
| `#SMTP_CRACK#` | 25 | `:2008` | | `#SSL_EXE#` | 443 | `:2035` |
| `#SQL_CRACK#` | 1433 | `:2009` | | `#KAGUYA_EXE#` | 31 | `:2035` |
| `#SECURITYTRACER_PROGRAM#` | 4 | `:2010` | | `#SIGNAL_SCRAMBLER_EXE#` | 32 | `:2036` |
| `#HACKNET_EXE#` | 15 | `:2011` | | `#MEM_FORENSICS_EXE#` | 33 | `:2037` |
| `#HEXCLOCK_EXE#` | 16 | `:2012` | | `#MEM_DUMP_GENERATOR#` | 34 | `:2038` |
| `#SEQUENCER_EXE#` | 17 | `:2013` | | `#PACIFIC_EXE#` | 192 | `:2039` |
| `#THEMECHANGER_EXE#` | 14 | `:2014` | | `#NETMAP_ORGANIZER_EXE#` | 35 | `:2040` |
| | | | | `#SHELL_CONTROLLER_EXE#` | 36 | `:2041` |
| | | | | `#NOTES_DUMPER_EXE#` | 37 | `:2042` |
| | | | | `#CLOCK_V2_EXE#` | 38 | `:2043` |
| | | | | `#DLC_MUSIC_EXE#` | 39 | `:2044` |

**DLC 那批（`:2033-2046`）整段包在 `if (DLC1SessionUpgrader.HasDLC1Installed)` 里** ——
没有 Labyrinths 时这些占位符**不会被替换**，会原样落进文件。

官方注明：`RTSPCrack` 专破 554；`ESequencer` 由 `ExtensionInfo.xml` 的序列器字段控制；
`OpShell` 支持 `-s` / `-o` 保存与重开 shell 布局。

### 4.3 主题与起搏器

`#GREEN_THEME#` / `#WHITE_THEME#` / `#YELLOW_THEME#` / `#TEAL_THEME#` / `#BASE_THEME#` /
`#PURPLE_THEME#` / `#MINT_THEME#` → `ThemeManager.getThemeDataString(OSTheme.X)`（`:2017-2023`）。

`#PACEMAKER_FW_WORKING#` → `PortExploits.ValidPacemakerFirmware`；
`#PACEMAKER_FW_DANGER#` → `PortExploits.DangerousPacemakerFirmware`（`:2024-2025`）。
官方在 `HeartMonitor` 注释里明确表示**不公开如何生成有效起搏器文件**（那是 Hacknet 独有的剧情时刻）。

---

## 5. ConditionalActions（行为系统）

文件根标签 `<ConditionalActions>`，内含若干**条件组**；每组内的 action **按文件顺序执行**。

### 5.1 条件组

| 标签 | 触发时机 |
|---|---|
| `<Instantly>` | 加载完成即刻。可加 `needsMissionComplete` |
| `<OnConnect target="节点id" needsMissionComplete="true" requiredFlags="a,b">` | 玩家连上目标机 |
| `<OnDisconnect target="节点id">` | 玩家从目标机断开（或连上自己的机器）。**去掉 target 或写 `NONE` = 任意断开都触发** |
| `<OnAdminGained target="节点id">` | 玩家取得该机 admin |
| `<HasFlags requiredFlags="a,b">` | 玩家**拥有全部**列出的 flag |
| `<DoesNotHaveFlags Flags="a,b">` | 玩家**一个都没有** |

### 5.2 Action 全集

官方样本 `ExampleFaction.xml` / `Actions/*.xml` 出现的全部 action（属性名原样）：

| Action | 属性 |
|---|---|
| `<AddAsset FileName FileContents TargetComp TargetFolderpath>` | 往目标机目录塞文件。**`TargetFolderpath` 指向的文件夹必须已存在**，否则抛 `NullReferenceException`（`SAAddAsset.cs:26-30`）。`FileContents` 支持占位符，但官方注明**不支持换行与超长内容**，建议只用来塞程序 |
| `<CopyAsset DestFilePath DestComp SourceComp SourceFileName SourceFilePath>` | 跨机复制文件。源不存在则什么都不做。**玩法提示**：可以造一台找不到的隐藏机放素材，之后再复制出来 |
| `<DeleteFile TargetComp FilePath FileName DelayHost Delay>` | |
| `<AppendToFile TargetComp TargetFolderpath TargetFilename DelayHost Delay>` | |
| `<AddIRCMessage Author TargetComp Delay>` | 标签体是消息。`@channel` 会通知所有人；`@#PLAYERNAME#` 可插值 |
| `<AddThreadToMissionBoard ThreadFilepath TargetComp>` | 往 `/el` 留言板加帖（UTF-8 纯文本） |
| `<AddMissionToHubServer MissionFilepath TargetComp AssignmentTag>` | 加任务到 hub / listing / DHS 服务器。`AssignmentTag` 存在时玩家**无法领取**（已指派给 NPC）；Entropy 型服务器可填 `top` 置顶 |
| `<RemoveMissionFromHubServer MissionFilepath TargetComp>` | |
| `<LoadMission MissionName>` | 立即加载任务，替换当前并发出邮件 |
| `<AddConditionalActions Filepath DelayHost Delay>` | 加载另一个行为集。**循环 action 的机制**：在 `OnDisconnect` 里重新加载自己 |
| `<LaunchHackScript Filepath DelayHost Delay SourceComp TargetComp RequireLogsOnSource RequireSourceIntact>` | 执行 HackerScript（§8）。`RequireLogsOnSource` 要求源机上有玩家留下的日志；`RequireSourceIntact` 要求目标机 `/sys` 网络文件还在（缺了就不攻击，即「残废的机器无法反击」） |
| `<SwitchToTheme ThemePathOrName FlickerInDuration DelayHost Delay>` | 可填基础主题名或路径 |
| `<StartScreenBleedEffect AlertTitle CompleteAction TotalDurationSeconds DelayHost Delay>` | 标签体是多行正文 |
| `<CancelScreenBleedEffect DelayHost Delay>` | |
| `<KillExe DelayHost Delay ExeName>` | |
| `<CrashComputer TargetComp CrashSource DelayHost Delay>` | `playerComp` 会让玩家机立刻蓝屏；其它机器移出网络图 15 秒 |
| `<ChangeAlertIcon Target Type DelayHost Delay>` | |
| `<ChangeIP TargetComp NewIP DelayHost Delay>` | |
| `<ChangeNetmapSortMethod Method DelayHost Delay>` | |
| `<HideNode TargetComp DelayHost Delay>` / `<ShowNode Target DelayHost Delay>` / `<HideAllNodes DelayHost Delay>` | |
| `<GivePlayerUserAccount TargetComp Username DelayHost Delay>` | |
| `<SaveGame DelayHost Delay>` | |
| `<RunFunction FunctionName FunctionValue>` | 执行任务函数（见 §7） |

**注意**：带 `DelayHost` 的 action 由指定节点托管延时；`<FastActionHost />` 是专用高效宿主。

---

## 6. Faction

```xml
<CustomFaction name="Example Faction" id="examplefaction" playerVal="0">
  <Action ValueRequired="1"> … </Action>
  <Action Flags="decypher"> … </Action>
  <Action ValueRequired="5" Flags="decypher"> … </Action>
</CustomFaction>
```

阵营 = 名字 + 玩家在其中的数值 + 一组响应式 action。
`<Action ValueRequired="N">` 在玩家数值**从低于 N 跨到 ≥ N** 时触发；`Flags` 是附加条件。
action 内容见 §5.2。

---

## 7. Mission

官方明确警告：**这个格式「拼凑得很粗糙」，对元素顺序与大小写非常严格，改的时候要小心并测试。**

```xml
<mission id="testMission0" activeCheck="true" shouldIgnoreSenderVerification="false">
  <goals> … </goals>
  <missionStart> … </missionStart>
  <missionEnd> … </missionEnd>
  <branchMissions> … </branchMissions>
</mission>
```

- `activeCheck="true"`：任务激活期间**每帧**检查是否完成，而不是只在玩家回信时检查。用来做延时、或从任务链外部接触玩家。
- `shouldIgnoreSenderVerification`：跳过「玩家回复的寄件人是否正确」的校验。

### 7.1 goal 全集（`ExampleMission.xml` 实测）

| type | 属性 | 含义 |
|---|---|---|
| `filedeletion` | `target` `file` `path` | 删除指定文件 |
| `clearfolder` | `target` `path` | 清空整个目录 |
| `filedownload` | `target` `file` `path` | 玩家下载（scp）该文件 |
| `filechange` | `target` `file` `path` `keyword` `removal` `caseSensitive` | 向文件加入 `keyword`；`removal="true"` 改为移除。两者组合可要求「替换」 |
| `fileupload` | `target` `file` `path` `destTarget` `destPath` `decrypt` `decryptPass` | 上传到目标机目录。`decrypt="true"` 配 `decryptPass` 要求**上传的是解密后的内容** |
| `getadmin` | `target` | 取得 admin。**只有玩家回复邮件时仍是 admin 才算通过** —— 有自动重置密码 admin 的机器会让玩家来不及 |
| `getadminpasswordstring` | `target` | |
| `getstring` | `target` | 要求玩家在「Additional Info」里回填指定字符串 |
| `delay` | `time` | **首次尝试后 N 秒**才算完成。配合 `activeCheck` + 静音任务可做延时缓冲，模拟真人响应时间 |
| `hasflag` | `target` | 要求已设置该 flag |
| `AddDegree` | `owner` `degree` `uni` `gpa` | 给人物加学位（学术库可见） |
| `wipedegrees` | `owner` | 清空人物学位 |
| `sendemail` | `mailServer` `recipient` `subject` | |

全部 goal 必须完成，任务才会完成。

### 7.2 其它元素

`<email>` + `<sender>` / `<subject>` / `<body>` / `<attachments>` / `<note>` / `<posting>`；
`<nextMission>` / `<branchMissions>` + `<branch>` + `<link>`；`<missionEnd>` 里放
`<RunFunction FunctionName= FunctionValue=>`。官方提示：调试时可用一个早期分支任务配
`getString` goal 直接跳到后期内容。

---

## 8. HackerScript（NPC 入侵脚本）

**这是游戏原生的自动入侵 DSL**，由行为标签 `<LaunchHackScript>` 执行。
本仓库 AutoHack 的 `script=` 模式（`HackScript.cs`）**故意不复用它**，理由见 RESEARCH §15.2；
但格式本身值得记录。

### 8.1 结构

```
# 注释是不规范写法 —— 官方没有提供注释语法，只因没有对应命令而「不执行」
config playerComp advExamplePC 0.2 $#%#$

connect $#%#$
delay 2 $#%#$
openPort 21 $#%#$
disconnect $#%#$
```

- 第一行必须是 `config [目标机id] [源机id] [每行延迟秒] $#%#$`
- 之后每行 `[动词] [参数…] $#%#$`
- `$#%#$` 是分隔符，必需
- 用标签选择目标时，id 原样写 `[TARGET_COMP]` / `[SOURCE_COMP]`，在 `<LaunchHackScript>` 里声明
- 一个脚本可指定多个目标，但**不是同时**攻击（重复写 `config` 段）
- **执行是实时的** —— 退出游戏会中断正在执行的脚本（与行为标签的 `delay` 原理不同）
- 执行痕迹与玩家相似，**会留下日志**

### 8.2 动词全集

`IntroExtension/HackerScripts/ExampleHack.txt` 是官方逐动词教学脚本，wiki 表格与它一致。

| 动词 | 参数 | 说明 |
|---|---|---|
| `connect` | — | 源机向目标机建连 |
| `disconnect` | — | 断开 |
| `delay` | 秒 | |
| `openPort` | 端口号 | **收的是原始端口号**（`AllyHack.txt` 实证 `openPort 3724`） |
| `clearTerminal` | — | 仅当目标是玩家时生效 |
| `hideNetMap` / `hideRam` / `hideDisplay` / `hideTerminal` | — | **官方警告「BE VERY CAREFUL —— 玩家自己拿不回来」** |
| `showNetMap` / `showRam` / `showDisplay` / `showTerminal` | — | 与上面配对 |
| `trackseq` | — | 改变玩家被 forkbomb 的结果：有 CSEC flag 且未防住则进入紧急恢复（不蓝屏） |
| `instanttrace` | — | 「brutal command」，立刻让目标进入紧急恢复模式 |
| `forkbomb` | — | |
| `flash` | — | UI 闪一下 |
| `delete` | 路径 + 文件名 | **两个参数**，如 `delete /sys x-server.sys` |
| `setAdminPass` | 新密码 | |
| `makeFile` | 目录 + 文件名 + 内容 | 官方注明**不能建子目录**；`makeFile bin SSHCrack.exe #SSH_CRACK#` 是标准用法 |
| `openCDTray` / `closeCDTray` | — | |
| `write` | 字符串 | 输出不换行 |
| `writel` | 字符串 | 输出并换行 |
| `writel_silent` | 字符串 | 换行输出但**不触发 UI 变红闪烁** |
| `write_silent` | 字符串 | 同上，不换行 |

**空 `writel` 推一个空行**；`writel  `（带尾随空格）与 `writel`（空）都合法。

### 8.3 官方样本

| 文件 | 内容 |
|---|---|
| `ExampleHack.txt` | 逐动词教学，覆盖全部动词 |
| `AllyHack.txt` | 纯 `connect` / `delay` / `openPort` / `disconnect` 的实战型（盟友脚本） |
| `TrapHack.txt` | `config playerComp [SOURCE_COMP] 0` + `forkbomb` —— 目标是玩家机 |
| `HostileHackerHarrasment.txt` | 大量 `writel` / `delay` / `stopMusic` / `clearTerminal` / `startMusic` 的骚扰脚本 |

### 8.4 执行侧

`Actions/HackerScriptActions.xml` 演示了两种用法：

```xml
<OnConnect target="hackerTarget" needsMissionComplete="false">
  <LaunchHackScript Filepath="HackerScripts/AllyHack.txt" DelayHost="advExamplePC" Delay="2.5"
                    SourceComp="allyHackerSource" TargetComp="hackerTarget" RequireLogsOnSource="false"/>
</OnConnect>

<OnConnect target="introFactionHomeNode" needsMissionComplete="true">
  <LaunchHackScript Filepath="HackerScripts/HostileHackerHarrasment.txt"
                    SourceComp="hackerTarget" TargetComp="playerComp"
                    RequireLogsOnSource="true" RequireSourceIntact="true"/>
</OnConnect>
```

`LoopingOnConnectAction.xml` 演示了**循环行为集**：在 `OnDisconnect` 里
`<AddConditionalActions Filepath="Actions/LoopingOnConnectAction.xml" />` 把自己重新加载回来，
从而每次连接都能再次触发。

---

## 9. Person 与 Theme

### 9.1 Person（`People/*.xml`）

```xml
<Person id="personID" handle="tester" firstName="Joe" lastName="Citizen"
        isMale="true" forceHasNeopals="true">
  <Birthplace name="Califonia" />
  <Degrees>
    <Degree uni="University of California" gpa="3.2">Bachelor of Software Engineering</Degree>
  </Degrees>
  <Medical>
    <Blood>AA</Blood>
    <Height>178</Height>                        <!-- cm -->
    <Allergies>Commas,Separate,Allergies</Allergies>
    <Perscription>…</Perscription>              <!-- 可多条 -->
    <Notes>…</Notes>
  </Medical>
  <DOB>17/02/1990 09:00:00</DOB>                <!-- DD/MM/YYYY HH:MM:SS -->
  <Notes>…</Notes>                              <!-- 游戏内不用，仅供自己记录 -->
</Person>
```

`id` 目前未使用；`handle` 是 PointClicker / Neopals 用的账号名。
`forceHasNeopals="true"` 保证生成 Neopals 账号（否则随机）。
数据源被 `<academicDatabase />` / `<MedicalDatabase />` / `<PointClicker />` 消费。

### 9.2 Theme（`Themes/*.xml`）

```xml
<CustomTheme>
  <themeLayoutName>mint</themeLayoutName>
  <backgroundImagePath>Themes/Backgrounds/RiptideGreen.png</backgroundImagePath>
  <UseAspectPreserveBackgroundScaling>false</UseAspectPreserveBackgroundScaling>
  <BackgroundImageFillColor>0,0,0</BackgroundImageFillColor>
  …
</CustomTheme>
```

`themeLayoutName` 必须是基础主题布局之一：`blue` / `green` / `white` / `mint` /
`greencompact` / `riptide` / `colamaeleon` / `riptide2`。
背景图建议 1920x1080 的 `.jpg`/`.png`；省略则自动生成动态背景。

颜色字段分三组：
- **主色**：`defaultHighlightColor`（网络图节点色，许多派生色由它算）、`defaultTopBarColor`、
  `moduleColorSolidDefault`（模块窗口描边）、`moduleColorStrong`、`moduleColorBacking`
- **exe 模块**：`exeModuleTopBar`、`exeModuleTitleText`
- **额外**：`warningColor` `subtleTextColor` `darkBackgroundColor` `indentBackgroundColor`
  `outlineColor` `lockedColor` `brightLockedColor` `brightUnlockedColor` `unlockedColor`
  `lightGray` `shellColor` `shellButtonColor` `semiTransText` `terminalTextColor` `topBarTextColor`
  `superLightWhite` `connectedNodeHighlight` `netmapToolTipColor` `netmapToolTipBackground`
  `topBarIconsColor` `thisComputerNode` `scanlinesColor`
- **AlienFX**（外星人硬件灯）：`AFX_KeyboardMiddle` `AFX_KeyboardOuter` `AFX_WordLogo` `AFX_Other`

颜色格式 `R,G,B` 或 `R,G,B,A`。

---

## 10. 与本仓库的关系

本文档不是要写 Extension，而是**用官方样本反查游戏 API**。三条直接落地：

| 本文档 | 支撑的实现 | 结论 |
|---|---|---|
| §4.2 占位符全走 `PortExploits.crackExeData[port]` | `ExeTools.cs` | 数据源正确；v1.14.1 的缺陷在**自造的长度守卫**（`EXE_FILE_LENGTH = 500` 是请求长度不是产物长度），不在数据源 |
| §3.5 `<encryptedFile>` + `FileEncrypter` | `DecTools.cs` | `double="true"` 就是官方双层嵌套；反推公式由 `Encrypt` 的 `*1822 + 32767` 推出 |
| §3.6 `<memoryDumpFile>` / `<Memory>` | `MemTools.cs` | 四个列表与四段 XML 一一对应；`GetEncodedFileString` 是唯一出口 |

另外两条给后续用的现成 API（**实现前应优先于手写遍历**）：

- `Computer.getFolderFromPath(string path, bool createFoldersThatDontExist)`（`Computer.cs:1628`）——
  按 `/` 切分并**自动建目录**，比手写 `folders` 遍历干净。
- `Programs.getFolderAtPath(path, os, root, returnsNullOnNoFind)` 与
  `SAAddAsset.Trigger`（`SAAddAsset.cs:16-32`）—— 官方「往节点目录塞文件」的标准两步。

### 合规

`Extensions/` 下两个样本是**游戏自带内容**，随游戏分发，版权归原作者；本文只做格式记录与引用，
未复制其代码到本仓库。wiki 两页（`/reference/Placeholder`、`/reference/HackerScripts`）为社区文档，
本文以游戏实现与官方样本为准并已标出 wiki 的错漏处。

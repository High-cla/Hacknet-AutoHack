# Hacknet 内嵌脚本与占位符参考

本文覆盖游戏内两套「写进数据的迷你语言」：

1. **自替换占位符** —— 形如 `#BINARY#`，在 XML 载入时被替换成程序内容或运行时数据
2. **HackerScript** —— 游戏原生的 NPC 入侵 DSL，由行为标签 `<LaunchHackScript>` 执行

社区文档 [hacknet.wiki/reference/Placeholder](https://hacknet.wiki/reference/Placeholder) 与
[HackerScripts](https://hacknet.wiki/reference/HackerScripts) 记录了这两者，但**都有错漏**。
本文以游戏实现与官方样本为准，凡与 wiki 冲突处均标出。

依据（全部一手）：

```
decompiled/game-proj/Hacknet/ComputerLoader.cs:1995-2048   占位符替换实现
decompiled/game-proj/Hacknet/FileEncrypter.cs              DEC 密码学
D:\steam\steamapps\common\Hacknet\Extensions\IntroExtension\
├── Nodes\ExampleComputer.xml          占位符与节点标签的官方用法
├── Actions\StartingActions.xml        <AddAsset> 用占位符塞程序
├── Actions\HackerScriptActions.xml    <LaunchHackScript> 用法
├── Actions\LoopingOnConnectAction.xml 循环行为集
└── HackerScripts\{ExampleHack,AllyHack,TrapHack,HostileHackerHarrasment}.txt
```

---

## 1. 自替换占位符

### 1.1 结构

`#<标识符>#`，标识符**不含尖括号**。实现在 `ComputerLoader.filter(string s)`
（`ComputerLoader.cs:1995-2048`）—— 一串 `.Replace()`，全部在 XML 载入时执行。

替换在 `LocalizedFileLoader.FilterStringForLocalization` 与 `MissionGenerationParser.parse`
之后进行（`:1997`），故占位符可以出现在节点文件、`<AddAsset FileContents>`、
`<encryptedFile>` 正文、`makeFile` 内容等任何过 `filter()` 的文本里。

### 1.2 通用占位符

| 占位符 | 展开为 | 行 |
|---|---|---|
| `#BINARY#` | `Computer.generateBinaryString(2000)` | `:1997` |
| `#BINARYSMALL#` | `generateBinaryString(800)` | `:1997` |
| `#PLAYERNAME#` | `os.defaultUser.name` | `:1997` |
| `#PLAYER_IP#` | `os.thisComputer.ip` | `:1998` |
| `#PLAYER_ACCOUNT_PASSWORD#` | `SaveFileManager.LastLoggedInUser.Password` | `:1999` |
| `#RANDOM_IP#` | `NetworkMap.generateRandomIP()` | `:2000` |
| `#PLAYERLOCATION#` | 字面量 `"UNKNOWN"`（占位但未实现） | `:2031` |
| `#EXTENSION_FOLDER_PATH#` | `ExtensionLoader.ActiveExtensionInfo.GetFullFolderPath()`，并把 `/Content` 换成 `/ Content`；无活动扩展时为 `"ERROR GETTING PATH"` | `:2030` |
| `#GIBSON_IP#` | `os.GibsonIP`（**DLC 段内**） | `:2045` |
| 制表符 `\t` | 四个空格 | `:2032` |

> **wiki 漏了** `PLAYER_ACCOUNT_PASSWORD`、`PLAYERLOCATION`、`EXTENSION_FOLDER_PATH`、`GIBSON_IP`。

`generateBinaryString` 的产物长度**不等于请求长度**：`byte[length/8]` 再逐字节
`Convert.ToString(b, 2)`，而该方法**不补前导零**（`Computer.cs:1580-1585`），
故请求 500 实际约 445 字符。**任何拿 `EXE_FILE_LENGTH = 500` 当长度门槛的校验都是错的**
（本仓库 v1.14.1 修的就是这个缺陷，见 llms.txt 坑 10）。

### 1.3 程序占位符

**全部取 `PortExploits.crackExeData[port]`** —— 这是游戏声明「程序内容从哪来」的唯一答案。

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

**DLC 那批（`:2033-2046`）整段包在 `if (DLC1SessionUpgrader.HasDLC1Installed)` 里。**
没装 Labyrinths 时这些占位符**不会被替换**，会原样落进文件 —— 写扩展时必须把 DLC 内容
一起放进 `<!--START_LABYRINTHS_ONLY_CONTENT-->` 块（见 EXTENSIONS.md §3.7）。

> **wiki 漏了** `KAGUYA_EXE`、`PACEMAKER_FW_*`、`GIBSON_IP`；把 `BINARYSMALL` 写成 1000 字符（实为 800）。

官方对几个特殊程序的说明：`RTSPCrack` 专破 554；`ESequencer` 由 `ExtensionInfo.xml`
的序列器字段控制；`OpShell` 支持 `-s` / `-o` 保存与快速重开 shell 布局。

### 1.4 主题与起搏器

```
#GREEN_THEME#  #WHITE_THEME#  #YELLOW_THEME#  #TEAL_THEME#
#BASE_THEME#   #PURPLE_THEME# #MINT_THEME#          → ThemeManager.getThemeDataString(OSTheme.X)   :2017-2023
#PACEMAKER_FW_WORKING# → PortExploits.ValidPacemakerFirmware                                        :2024
#PACEMAKER_FW_DANGER#  → PortExploits.DangerousPacemakerFirmware                                    :2025
```

官方在 `HeartMonitor` 的注释里明确表示**不公开如何生成有效起搏器文件** ——
「That moment was special for Hacknet alone.」

### 1.5 官方用法

`Nodes/ExampleComputer.xml`：

```xml
<file path="bin" name="Binary_File">#BINARY#</file>

<file path="home/NewDirectory" name="Test_File">
  #BINARY#        生成 2000 字符二进制
  #BINARYSMALL#   生成 800 字符二进制（注释里写的 1000 与实现不符）
  #PLAYER_IP#     玩家机 IP
  #PLAYERNAME#    玩家名
  #RANDOM_IP#     新随机 IP
</file>

<file path="bin" name="SSHCrack.exe">#SSH_CRACK#</file>
<file path="bin" name="FTPBounce.exe">#FTP_CRACK#</file>
<!-- …共 20 条基础 + 12 条 DLC，全部 path="bin" -->
```

`Actions/StartingActions.xml`：

```xml
<ConditionalActions>
  <Instantly>
    <AddAsset FileName="RTSPCrack.exe" FileContents="#RTSP_EXE#"
              TargetComp="playerComp" TargetFolderpath="bin" />
  </Instantly>
</ConditionalActions>
```

`SAAddAsset.Trigger`（`SAAddAsset.cs:16-32`）对 `FileContents` 调 `ComputerLoader.filter`，
再 `new FileEntry(内容, FileName)` 塞进 `TargetFolderpath`。**目标文件夹必须已存在**，
否则抛 `NullReferenceException`。

**官方往节点 `/bin` 塞程序的两种写法**：节点文件里 `<file path="bin" …>`（声明期），
或 `<AddAsset TargetFolderpath="bin">`（运行期）。本仓库 `ExeTools.cs` 走的是运行期路径，
落点与数据源均与官方一致。

---

## 2. HackerScript

游戏原生的自动入侵 DSL。由行为标签 `<LaunchHackScript>` 执行。

> 本仓库 AutoHack 的 `script=` 模式**故意不复用它**，三条否决理由见
> [RESEARCH.md §10.5](RESEARCH.md)。本节只记录格式本身。

### 2.1 结构

```
config playerComp advExamplePC 0.2 $#%#$

connect $#%#$
delay 2 $#%#$
openPort 21 $#%#$
disconnect $#%#$
```

- **第一行必须是** `config [目标机 id] [源机 id] [每行延迟秒] $#%#$`
- 之后每行 `[动词] [参数…] $#%#$`
- `$#%#$` 是分隔符，**必需**
- **没有注释语法** —— 官方原话：不规范写法，只因没有对应命令而不执行
- 用标签选目标时 id 原样写 `[TARGET_COMP]` / `[SOURCE_COMP]`，在 `<LaunchHackScript>` 里声明
- 一个脚本可指定多个目标，但**不是同时**攻击（重复写 `config` 段）
- **执行是实时的** —— 退出游戏会中断正在执行的脚本（与行为标签的 `delay` 原理不同）
- 执行痕迹与玩家相似，**会留下日志**

### 2.2 动词全集

`IntroExtension/HackerScripts/ExampleHack.txt` 是官方逐动词教学脚本。

| 动词 | 参数 | 说明 |
|---|---|---|
| `connect` | — | 源机向目标机建连 |
| `disconnect` | — | 断开 |
| `delay` | 秒 | |
| `openPort` | 端口号 | **收的是原始端口号**（`AllyHack.txt` 实证 `openPort 3724`） |
| `clearTerminal` | — | 仅当目标是玩家时生效 |
| `hideNetMap` / `hideRam` / `hideDisplay` / `hideTerminal` | — | **官方警告「BE VERY CAREFUL —— 玩家自己拿不回来」** |
| `showNetMap` / `showRam` / `showDisplay` / `showTerminal` | — | 与上面配对 |
| `trackseq` | — | 改变玩家被 forkbomb 的结果：有 CSEC flag 且未防住则进入紧急恢复模式（不蓝屏） |
| `instanttrace` | — | 「brutal command」，立刻让目标进入紧急恢复模式 |
| `forkbomb` | — | |
| `flash` | — | UI 闪一下 |
| `delete` | **路径 + 文件名** | 两个参数，如 `delete /sys x-server.sys` |
| `setAdminPass` | 新密码 | |
| `makeFile` | **目录 + 文件名 + 内容** | 官方注明**不能建子目录**。`makeFile bin SSHCrack.exe #SSH_CRACK#` 是标准用法（内容可含占位符） |
| `openCDTray` / `closeCDTray` | — | |
| `write` | 字符串 | 输出不换行 |
| `writel` | 字符串 | 输出并换行 |
| `writel_silent` | 字符串 | 换行输出但**不触发 UI 变红闪烁** |
| `write_silent` | 字符串 | 同上，不换行 |

**空 `writel` 推一个空行**；`writel  `（带尾随空格）与 `writel`（空）都合法 ——
故分词必须 `RemoveEmptyEntries`（本仓库 `HackScript.cs` 同此）。

### 2.3 官方样本

| 文件 | 内容 |
|---|---|
| `ExampleHack.txt` | 逐动词教学，覆盖全部动词；含 `stopMusic` / `startMusic` / `clearTerminal` 的玩家专属说明 |
| `AllyHack.txt` | 纯 `connect` / `delay` / `openPort` / `disconnect` 的实战型盟友脚本 |
| `TrapHack.txt` | `config playerComp [SOURCE_COMP] 0` + `forkbomb` —— 目标是玩家机 |
| `HostileHackerHarrasment.txt` | 大量 `writel` / `delay` / `stopMusic` / `clearTerminal` / `startMusic` 的骚扰脚本 |

### 2.4 执行侧

`Actions/HackerScriptActions.xml`：

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

| 属性 | 含义 |
|---|---|
| `Filepath` | 脚本路径（相对扩展根） |
| `SourceComp` / `TargetComp` | 源机与目标机 id，**可填 id 或 IP** |
| `DelayHost` / `Delay` | 由指定节点托管延时 |
| `RequireLogsOnSource` | 仅当源机上有玩家留下的日志（删除/移动/复制过文件）才执行 |
| `RequireSourceIntact` | 仅当目标机 `/sys` 网络文件还在才执行 —— **残废的机器无法反击** |

---

## 3. 与本仓库的关系

| 本文档 | 支撑 | 结论 |
|---|---|---|
| §1.3 程序占位符全走 `PortExploits.crackExeData[port]` | `ExeTools.cs` | 数据源正确。v1.14.1 修的缺陷在**自造的长度守卫**（拿 `EXE_FILE_LENGTH` 当产物长度），不在数据源 |
| §1.2 `generateBinaryString` 不补前导零 | `ExeTools.cs` | 请求 500 → 实产约 445 字符。**判据用「非空」** |
| §1.3 `#DECYPHER_PROGRAM#` / `#DECHEAD_PROGRAM#` | `DecTools.cs` | DEC 系程序同样出自 `crackExeData`；密码学在 `FileEncrypter.cs` |
| §2.2 动词表 | `HackScript.cs` | 本仓库**未复用**游戏 DSL（RESEARCH §10.5）；`makeFile` / `delete` / `setAdminPass` / `write*` 属「游戏有而我们没有」，已列入 `UnsupportedVerbs` |
| §2.2 `openPort` 收原始端口号 | `HackScript.cs` | 别名映射到 `HackStepKind` 时保留原始端口语义 |

### 合规

`Extensions/` 下样本与游戏实现均为**游戏自带内容**，版权归原作者；本文只做格式记录与引用，
未复制其代码到本仓库。wiki 两页为社区文档，本文以游戏实现与官方样本为准并已标出 wiki 的错漏处。

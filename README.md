# AutoHack — Hacknet 全自动入侵 Mod

仓库：<https://github.com/High-cla/Hacknet-AutoHack> · 许可：[MIT](LICENSE)

基于 Hacknet + Pathfinder 的自动入侵插件，带**交互式控制面板**。**优先使用游戏原生机制**：每个动作都先把对应指令回显进终端，再走原生 API（`Programs.connect` / `Computer.openPort` / `Computer.giveAdmin` / `makeFile`）执行，行为与真人敲 `connect` / `probe` / `sshcrack 22` / `porthack` 一致。四个游戏机制被显式处理：**管理员反扑**（断开时 `disconnectionDetected` 会关端口并把 `adminIP` 还原成机器自己，故离开前先解除反扑）、**跳板**（`proxyActive` 会拦下破解程序，先过载绕过）、**追踪**（`TraceTracker` 只在连着被追踪目标时推进，跑完即 `dc` 中止）、以及**肉鸡复用**（全网扫描默认跳过 `adminIP` 已是玩家的节点）。

## 安装

### 方式一：下载现成产物

从 [Releases](https://github.com/High-cla/Hacknet-AutoHack/releases/latest) 取 `AutoHack.dll`，放进游戏的 `BepInEx/plugins/` 目录：

```
<Hacknet>/BepInEx/plugins/AutoHack.dll
```

### 方式二：从源码构建

本仓库的构建配置会把产物**直接输出到游戏目录**，无需手工拷贝：

```
D:\steam\steamapps\common\Hacknet\BepInEx\plugins\AutoHack.dll
```

```bash
dotnet build src/AutoHack/AutoHack.csproj -c Release
```

## 使用

游戏终端里输入 `autohack` 打开**控制面板**（再输一次关闭）：

```
autohack                                        # 开关控制面板
autohack run [here] [delay=秒] [stay] [redo] [nologs] [nomark] [direct]   # 不开面板，直接执行
autohack run script=stealth                     # 用脚本决定入侵次序（见下）
autohack dec|mem|exes|unbreakable [allnodes]    # 独立工具（见「工具」）
autohack pull|purge|drop                        # 对当前连接节点动手
autohack -h                                     # 帮助
```

### 面板

面板浮在游戏画面右下角，**全部自绘**（不用游戏原生 `Button`/`CheckBox`/`SliderBar`），鼠标直接操作。拖动标题栏可移到任意位置，`-` 收起为一条状态栏，`x` 关闭。

| 控件 | 作用 |
|---|---|
| `NETWORK SWEEP` / `CURRENT NODE` | 目标范围：沿网络连线可达的服务器 / 仅当前连接节点 |
| `whole map` | 全网扫描口径：勾选 = 地图全表（含不在连线上的机器），缺省不勾 = 沿连线广度优先 |
| `PORT INTERVAL` 滑条 | 每个端口的破解间隔，`0.05`–`5` 秒，默认 `0.6`；支持滚轮微调，`≤0.15` 时数值转为警示色 |
| `wipe target logs` | 是否在执行后清空**目标**日志（缺省**关**，v1.16.0 起）—— 清痕会改写对方状态，要清须显式勾 |
| `wipe my logs` | 是否连**自己机器**的 `/log` 一起清（缺省**关**）—— 那是你自己的操作史（谁连过你、你读过什么），要清须显式勾 |
| `connect first` | 每个目标先 `connect` 再动手（缺省开） |
| `upload marker` | 是否上传 `~/autohack.txt` 标记（缺省**关**） |
| `use known creds` | 用已知账密登入目标（缺省**关**，v1.15.0 起）—— 成功即提权，跳过全部破端口 |
| `NORMAL` / `FAST` / `INSTANT` | 推进节奏：非端口步保留真人间隔（0.35s）/ 压到 0.05s / 合并到**同一帧** |
| `anti-trace dc` | 每个目标跑完 `dc`：追踪只在连着目标时推进，断开即中止（缺省**关**，v1.16.0 起）—— 断开是反追踪的手段，不是中性默认 |
| `skip owned nodes` | 全网扫描时跳过已拿下的肉鸡，不重复入侵（缺省开） |
| — | 永远提不了权的机器（端口表容量 ≤ `portsToCrack`）在全网扫描时一律跳过，见下 |
| `RUN` | 按当前设置执行 |
| TOOLS 区 `DEC DECRYPT` / `MEMORY DUMP` / `ALL PROGRAMS` | 见「工具」，单击**立即执行**，无二次确认 |
| TOOLS 区 `PULL FILES` / `PURGE FILES` / `DROP NODE` | 对**当前连接的节点**动手：下载 / 删除当前目录下全部文件、把节点从网络图摘掉（后两个用告警色） |
| TOOLS 区 `UNBREAKABLE` | 加固本机，**不可逆**，用告警色标注 |

执行期间面板切换为进度视图：阶段 + 百分比、分段进度条、当前目标与动作计数，下方滚动显示逐目标战果。完成后显示 `LAST RUN` 与 `RUN AGAIN`。

**为什么自绘而不是用原生控件**（三条都实测过）：

1. `CheckBox.doCheckBox(id, x, y, on, color, text)` 只在 `GuiData.hot == id` 时才画文字（`Hacknet.Gui/CheckBox.cs:55-59`）——**标签平时不可见**，这是旧面板显脏的主因。
2. 原生 `Button` 用 `tinyfont`（Font10）并自动缩放塞进按钮（`Button.cs:96-110`），字号与间距不可控；自绘统一用 `smallfont`（Font12）加显式缩放系数，得到 0.9 / 1.0 / 1.1 / 1.3 四级字号阶梯。
3. 原生 `Button` 在宽度 > 65 时会额外画一条 13px 颜色标签条，与紧凑面板风格冲突。

配色（`highlightColor` / `terminalTextColor`）**取自 `OS` 当前主题**，换主题时面板跟随，不会与游戏自身 UI 撞色。

输入是模态的：`OS.Draw` 的 **Prefix** 在正文绘制前检查光标是否落在 `HackPanel.LastFrame` 内，是则置 `GuiData.blockingInput = true`。必须用 Prefix —— 正文里的游戏控件在 `Draw` 期间就消费输入，Postfix 已经太晚；只在光标位于面板上时抢占，面板之外照常操作游戏。

### 命令行参数（`autohack run` 时）

| 参数 | 说明 |
|---|---|
| `here` | 仅当前已连接的节点（缺省 = 沿网络连线可达的服务器） |
| `delay=秒` | 每个端口的破解间隔，默认 `0.6`，范围 **`0.02`**–`5` |
| `nologs` | 不清除目标日志（**已是缺省**） |
| `logs` | 清除目标日志（v1.16.0 起缺省改为不清，要清须显式传） |
| `ownlogs` | **连自己机器的 `/log` 一起清**（缺省**不清**）—— 玩家自己的操作史，默认保留 |
| `noownlogs` | 明确不清自己机器的日志（**已是缺省**） |
| `nomark` | 不上传标记文件（**已是缺省**） |
| `mark` | 上传标记文件 |
| `allnodes` | 全网扫描改扫地图全表，不再只沿连线展开 |
| `creds` / `nocreds` | 用 / 不用已知账密登入（**缺省不用**，v1.15.0 起；`creds` 显式开启，`nocreds` 已是缺省） |
| `instant` / `fast` / `slow` | 节奏档位：非端口步同帧连跑 / 0.05s / 0.35s（**缺省 slow**） |
| `direct` | 跳过 connect / probe，直接就地破解（不再回显这两条指令） |
| `stay` | 跑完**不**断开连接（**已是缺省**，v1.16.0 起） |
| `dc` | 每个目标跑完断开（反追踪：追踪只在连着目标时推进） |
| `redo` | 全网扫描时**连已控节点一起重打**（缺省跳过肉鸡及永远提不了权的机器） |
| `script=文件` | 用一份**动作表**取代内置次序（见「脚本模式」） |

> `allnodes` 与缺省口径的差额实测（同一存档 147 节点）：沿连线广度优先 **7** 个目标，地图全表 **110** 个。
> 差额是「可以直接敲 IP 连上、但不在连线上」的机器 —— `Programs.connect` 遍历的是
> `netMap.nodes` 全表，本来就不检查 `links`（见「关键设计决策」#4c）。

### 工具（v1.14.0 起；v1.14.1 / v1.14.2 修缺陷；v1.16.0 加三个远程动作；v1.18.0 删除通道归一）

工具与入侵流程**完全独立** —— 不进 `autohack run` 的自动流程，命令与面板 TOOLS 区按钮走**同一份实现**。面板按钮**单击立即执行**，不弹二次确认（`UNBREAKABLE` / `PURGE FILES` / `DROP NODE` 用告警色 + 回显里的 `irreversible` 代替）。

| 命令 | 作用 |
|---|---|
| `autohack dec [allnodes]` | 解开目标上的 `#DEC_ENC` 加密文件，逐层解到明文，写入玩家 `/home/MemDumps` |
| `autohack mem [allnodes]` | 查看本机内存转储（紧凑格式，截断显示）、导出到 `/home/MemDumps`、扫描节点上的 `.mem` 并解其内嵌 DEC |
| `autohack exes` | 把游戏能生成的破解程序全部补进玩家 `/bin`（幂等） |
| `autohack unbreakable` | 加固玩家自己这台机器（**不可逆**） |
| `autohack pull` | 把**当前目录**下全部文件下载到本机（按扩展名分流，同游戏 `scp`；**绝不新建文件夹**） |
| `autohack purge` | 删除**当前目录**下全部文件（同游戏 `rm`；与清痕**同一实现**；**只删文件，不删文件夹**） |
| `autohack drop` | 断开并把当前连接的节点从网络图上摘掉 |

`allnodes` 只对 `dec` / `mem` 有意义（缺省只作用于当前连接节点，与 `run` 口径一致）；`exes` 与 `unbreakable` 天然只针对玩家自己。

**两个远程动作都不碰文件夹**：`pull` 只往已存在的目录里放（`/bin`、`/sys`、`/home`），落不下就退回 `/home`，**不会新建**；`purge` 只清文件，**不删文件夹**。原因是游戏自身**没有任何删除文件夹的入口**（`Programs` 里没有 rmdir，官方 Action 也只有 `<DeleteFile>`），mod 一旦建出文件夹，玩家就永远清不掉 —— 所以干脆不建，也不假装能删。`purge` 的回显会把「还剩几个子文件夹」一并报出，免得对着一个空夹反复试。

#### DEC 解密：反推而非暴力

游戏的 `FileEncrypter.Encrypt` 是逐字符仿射（`FileEncrypter.cs:40`）：

```
num = data[i] * 1822 + 32767 + passcode
```

头部第 4 段恒为加密字符串 `"ENCODED"`，其首字符 `'E'` 的密文因此恒等于
`'E' * 1822 + 32767 + passcode = 158485 + passcode`。于是

```
passcode = 头部第 4 段首个密文数字 - 158485
```

得到后**交给游戏自身的 `FileEncrypter.TestingDecryptString` 反验**：只有 `passcode`
正确，第 4 段才会解出 `"ENCODED"`；验不过即判定失败并如实报出，不做任何猜测。
多层嵌套递归解到正文不含 `#DEC_ENC` 标记为止，层数上限 16 防自引用挂死。

> 实测：玩家存档 `save_1.xml` 中 **39 个**唯一 DEC 文件（含 4 个两层嵌套，共 43 层），
> 全部反推 + 反验通过。

产物落点与内存转储一致，都在 `/home/MemDumps` —— 工具产出都是「可读文件」，分开落点只会让玩家两处找东西。

#### 内存转储

查看与导出都走游戏自身的往返对（`GetCompactSaveString` / `GetEncodedFileString` /
`GetMemoryFromEncodedFileString`）。导出落点 `home/MemDumps`，与游戏
`MemoryDumpDownloader` 一致（`MemoryDumpDownloader.cs:92-99`），并当场做一次往返比对。

> 游戏自身缺陷：`MemoryContents.GetSaveString()` 的 `FileFragments` 分支遍历的是
> `CommandsRun.Count`（`MemoryContents.cs:48`），当 `FileFragments` 比 `CommandsRun`
> 长时会 `IndexOutOfRangeException`。查看/导出都兜住它，坏存档不会把游戏线程带崩。

#### 自身加固（不可逆）

对玩家自己的机器置：`portsNeededForCrack = 9999998`、`traceTime = 1`、
`hasProxy/proxyActive/proxyOverloadTicks/startingOverloadTicks` **四字段同步**置
`9999998`（`addProxy` 的语义就是一次设定四者，`Computer.cs:243-252` —— 只改
`hasProxy` 会让 `DisplayModule` 按 `0/0` 算进度条）、`firewall.solution` 换 12 位随机串。
执行前后各打印一次全部字段，便于核对与手工还原。

端口走 Pathfinder 的 `PortState.SetCracked`（15 个原版协议），**不写原版 `portsOpen`** ——
Pathfinder 已用 Harmony Prefix 接管 `openPort`/`openPorts`（`ComputerExtensions.cs:184-204`），原版列表永不更新。

### 脚本模式

脚本文件放在 `Content/HackerScripts/`，扩展名可写可不写，
`#` 开头是注释（游戏本身没有注释语法，这里补一个，是唯一比游戏宽松的地方）：

```
# samples/stealth.txt —— 不碰端口，只靠已知账密登入
probe $#%#$
login $#%#$
rm    $#%#$
dc    $#%#$
```

| 动作 | 含义 |
|---|---|
| `probe` | 读取并回显目标端口表 |
| `login` | 用已知账密登入（成功即提权） |
| `proxy` | 解除跳板（等价于过载跑完，但不等 30 秒） |
| `openPort [端口]` | 攻破端口；**不带号 = 该目标上全部可破端口** |
| `solve` | 解目标防火墙（`porthack` 的前置） |
| `porthack` | 提权 |
| `mark` | 投放 `~/autohack.txt` 标记 |
| `rm` | 清除 `/log` |
| `dc` | 断开连接 |
| `delay 秒` / `config … 秒` | 设定每步间隔（`config` 按游戏原格式取第 4 个参数） |

**三个动作不用写**，AutoHack 恒定补上 —— 它们是正确性要求而非风格偏好：
`connect`（未连接时）、`neutralize`（解除管理员反扑，否则断开后 0~20 秒肉鸡标记丢失）、
`killtrace`（收尾反追踪）。

**`rm` 必须排在 `dc` 之前**，否则整份脚本被拒绝并说明原因。`rm` 的作用域是「当前连接」
（`Programs.rm` 读 `os.connectedComp`），断开之后再执行，删的是**玩家自己**的文件系统 ——
这正是玩家手敲时「命令敲对了却没有效果」的根因。

> **不能直接跑游戏自带的脚本。** 游戏那 28 个动作里**没有提权**，唯一像「接管」的
> `systakeover` 会往真实磁盘写 `VMBootloaderTrap.dll` 与 `OpenCMD.bat`
> （`HostileHackerBreakinSequence.cs:15-21`），是剧情级破坏序列。它的 `connect` 也只是
> `parseInputMessage("cConnection …")` —— 目标机视角记「有人连进来」，
> **根本不设 `os.connectedComp`**（`HackerScriptExecuter.cs:121`），驱动不了玩家终端。
> 喂错动作时插件会明确区分「这是游戏 NPC 动作，此处没有对应物」与「拼错了」，
> 而不是笼统报「未知动作」。

## 架构

```
src/AutoHack/
├── AutoHackPlugin.cs   插件入口：命令注册（属性扫描）+ PostLoad 自检 + 装配 Harmony
├── HackOverlay.cs      叠加层：patch OS.Draw 画面板（Prefix 抢输入）/ patch OS.Update 推进执行
├── HackPanel.cs        面板绘制：自绘控件 + 进度/战果视图（含主题配色 Palette）
├── HackRun.cs          执行模型：动作序列、逐帧推进、指令回显（与绘制解耦）
├── HackEngine.cs       决策逻辑：可连接目标遍历、端口表读取、提权门槛（含端口容量）、防火墙破解、反扑解除、跳板绕过、日志清理
├── HackTypes.cs        不可变数据：HackOptions（参数解析）/ HackStep（含回显指令）
├── HackScript.cs       入侵脚本：行式动作表的解析与校验（script= 模式）
├── PendingRuns.cs      无面板运行的调度：命令线程只传参数，游戏线程构造并推进（按 OS 键控的 ConcurrentDictionary）
├── IsExternalInit.cs   net472 兼容垫片（record/init 需要）
└── GlobalUsings.cs     全局 using
```

## 验证

### 验证方式

**当前规矩（用户定）：只看产物 MD5，不做反编译核对。**

| 项 | 值 |
|---|---|
| 产物路径 | `D:\steam\steamapps\common\Hacknet\BepInEx\plugins\AutoHack.dll` |
| 当前版本 | v1.22.0 |
| 字节数 | 89600 |
| MD5 | `0c3d23467926cdb469c088b8e4ee1ae3` |

核对流程：清理 `obj`/`bin` → 构建（须 0 警告 0 错误）→ 记 `md5sum` 与字节数，
与上一版比对。构建成功即证明源码已编入（增量缓存已清，漏编会报错）；
MD5 只用于确认部署确实是新的那个产物。
**验证只看构建输出目录里已编译的那份**，不下载 Release 附件回来比对（用户定，v1.22.0 起）。

### 版本沿革

| 版本 | 字节数 | MD5 | 要点 |
|---|---|---|---|
| v1.22.0 | 89600 | `0c3d23467926cdb469c088b8e4ee1ae3` | EOS 设备：端口容量天生等于门槛（2 = 2）故永不提权 —— 那是游戏刻意的，正路是全系统一的固定密码 `alpine`。新增 `RevealAttachedDevices` 免跑 exe 补发现（原版 `eosDeviceScan.exe` 等价物），EOS 放行 login 路径（§30） |
| v1.21.0 | 89088 | `7deb395a07a4b0a924220ba8682ea75e` | 修面板控件 ID 冲突：`DragId` 硬编码 7099 撞上 PURGE 按钮（`IdBase+30+5`），点击被拖动逻辑吃掉；`DragId` 改为从 `IdBase` 派生（§29） |
| v1.20.0 | 89088 | `3a5f848d9b108d743d199112ef644fa0` | `pull` 绝不新建文件夹；`purge` 只删文件并如实报出子夹数；目录回显改全路径；`RemoveFiles` 补兜异常（§28） |
| v1.19.0 | 88064 | `291e23446587d0e4682266f5db5e565e` | 异常护栏下沉到 `ToolDispatch`（命令入口此前裸奔、失败静默）；工具目标口径抽 `ToolTargets`；端口表一次快照（§27） |
| v1.18.0 | 87552 | `42b2604b866531ff6a0afc35191fa9d8` | 删除通道归一（清痕与 `purge` 共用 `RemoveFiles`）；DEC 落点并入 `/home/MemDumps`（§26） |
| v1.17.0 | 87552 | `6b54d30232caf8f5817cc2c99ffb60b1` | 面板文案中英双语，跟随游戏 locale 自动切换（#4l、§25） |
| v1.16.0 | 85504 | `b062b6428d8c7d0c743639a20c87d654` | 清痕与断连两条缺省翻转为**关**（新增 `logs`/`dc` 反向别名）；三个远程工具 `pull`/`purge`/`drop`（#4k、§24） |
| v1.15.0 | 81920 | `6823f1de25a98881dd6577cc944fd9df` | 凭据登入缺省翻转为**关**；玩家自机清痕独立成开关（缺省关）；删除全部 `decompiled/autohack-vNN`（#4i2、§23） |
| v1.14.2 | 81408 | `7ee5d4bb15490985599557e1cc06a904` | 审计后六项修复（#4j、`docs/RESEARCH.md` §22） |
| v1.14.1 | 81408 | `2c9ab2c456ce36a4e038803415e371ad` | 清痕补上玩家机；程序补全判据改为「非空」 |
| v1.14.0 | — | — | 四个独立工具（DEC / 内存 / 程序 / 加固），未单独发版 |
| v1.13.0 | 66048 | `1f1630b978614e8020f72089fe7b115b` | 摘除全部日志探针；`CleanLogs` 不吃节流；`targets=0` 说明原因 |
| v1.12.3 | 67072 | `51cfa993824f6d5978ddcb6886a06d90` | 同上两条的引入版（#4i） |
| v1.12.1 | — | — | 验证流程改为只看 MD5（放弃反编译核对） |
| v1.12.0 | — | — | 脚本模式（#4h） |
| v1.11.x | — | — | 凭据登入（#4d）、节奏档位（#4e）、剔除机补清痕（#4f）、清痕改走 `deleteFile` 并前移（#4g） |
| v1.10 | — | — | `allnodes` 口径（#4c）、追踪终止 |
| v1.9 | — | — | 提权走原生门禁（#4b） |
| v1.8 | — | — | 命令线程 / 游戏线程分工（#5b） |
| v1.7 | — | — | 自绘面板；跳板收敛、追踪直毙（#5） |

> v1.7–v1.12.0 的**逐版反编译核对记录**（每版的符号计数与行号指纹）已从本文件移出，需要回溯时查 git 历史（`git log --follow README.md`）。本表保留各版的 MD5 指纹与改动要点，足以回答「某版本究竟编进去了什么」。

## 附：HacknetSaveFix（独立插件）

仓库里还有第二个**独立**插件 `src/SaveFix/`，产物 `HacknetSaveFix.dll`，
修的是**游戏本体**的一个存档崩溃，与 AutoHack 无耦合，可单独安装/卸载。

### 症状

```
[Error  :   Hacknet] Error writing save data for user :
System.NullReferenceException
   at Hacknet.PlatformAPI.Storage.SaveFileManager.GetSaveFileNameForUsername(String username) IL<0x0001>
   at Hacknet.PlatformAPI.Storage.SaveFileManager.WriteSaveData(String saveData, String playerID) IL<0x0000>
```

### 根因（全在游戏本体）

| # | 位置 | 事实 |
|---|---|---|
| 1 | `OS.cs:148` | `public string SaveUserAccountName = null;` —— 默认就是 null |
| 2 | `MainMenu.cs:103/150/235/315` | 该字段**只在 MainMenu 构造 OS 时赋值** |
| 3 | `OS.cs:1522` | `writeSaveGame(SaveUserAccountName)` 把它当文件名传下去 |
| 4 | `SaveFileManager.cs:238` | `GetSaveFileNameForUsername(playerID)` |
| 5 | `SaveFileManager.cs:223` | `purifyStringForDisplay(username).Replace("_","-").Trim()` |
| 6 | `FileSanitiser.cs:9-12` | 对 null 输入**返回 null** → 紧接着的 `.Replace` 打在 null 上 |

栈里的 `IL<0x0001>` 正是第 5、6 步那一句。错误信息 `for user :`（冒号后为空）
也印证 `playerID` 是 null。

**任何绕过主菜单进入 OS 的入口都会触发** —— 例如用 HacknetHotReplace 之类的工具
直接连进设备、或经扩展直接起 OS。此时字段停在 null，保存必炸。

`WriteSaveData` 把异常吞成一行错误日志（`SaveFileManager.cs:240-243`），
**游戏不崩，但存档静默失败** —— 这是真正危险的地方。

### 修法

给崩溃点打一个 Harmony 前缀，把 null/空白用户名换成**游戏自己在 `OS.cs:372`
用的同一套回落**，不另立规则：

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

因为 `SaveUserAccountName` 为 null 时 `os.username` 正是由同一表达式算出
（`OS.cs:372-373`，且已 purify），落盘文件名与 `os.username` 保持一致。

只在真的兜底时打一条 `LogWarning` —— 这是异常路径，静默会把
「有入口没设账号名」这件事藏起来。

### 构建与产物

```bash
rm -rf src/SaveFix/obj src/SaveFix/bin
dotnet build src/SaveFix/SaveFix.csproj -c Release
```

产物落点同 AutoHack：`<Hacknet>/BepInEx/plugins/HacknetSaveFix.dll`。
5120 字节，MD5 `505df298c541768b0cc39a3d4d610806`。

不依赖 PathfinderAPI，只需 BepInEx + 0Harmony。

## 调研资料

- `docs/RESEARCH.md` — 完整调研：原生机制、API 精确签名、陷阱（§1–§30，每条结论带 `文件:行号`）
- `docs/EXTENSIONS.md` — 游戏自带 `Extensions/` 官方样本的格式参考：节点 XML、占位符、行为系统、任务、阵营、主题
- `docs/HACKERSCRIPTS.md` — 自替换占位符全表 + HackerScript 动词表，以游戏实现与官方样本为准，已标出 wiki 的错漏处

以下为**本地研究树**，未入库（体积大，且含第三方版权物与 binary；`.gitignore` 已排除，可按下列配方随时重建）：

- `upstream/Hacknet-Pathfinder/` — Pathfinder 源码（[Arkhist/Hacknet-Pathfinder](https://github.com/Arkhist/Hacknet-Pathfinder) 的 clone）
- `decompiled/game-proj/` — 游戏反编译（358 文件，ilspycmd `-p`）
- `decompiled/pathfinder/` — PathfinderAPI 反编译（134 文件）
- `refs/prs/` — 上游 10 个 PR 的 diff

## 许可证

[MIT](LICENSE) © 2026 High-cla

本仓库只包含插件源码。Hacknet 及其反编译产物、PathfinderAPI 二进制均为各自权利人的版权物，不在本仓库内。

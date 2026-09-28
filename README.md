# AutoHack — Hacknet 全自动入侵 Mod

仓库：<https://github.com/High-cla/Hacknet-AutoHack> · 许可：[MIT](LICENSE)

基于 Hacknet + Pathfinder 的自动入侵插件，带**交互式控制面板**。**优先使用游戏原生机制**：每个动作都先把对应指令回显进终端，再走原生 API（`Programs.connect` / `Computer.openPort` / `Computer.giveAdmin` / `makeFile`）执行，行为与真人敲 `connect` / `probe` / `sshcrack 22` / `porthack` 一致。四个游戏机制被显式处理：**管理员反扑**（断开时 `disconnectionDetected` 会关端口并把 `adminIP` 还原成机器自己，故离开前先解除反扑）、**跳板**（`proxyActive` 会拦下破解程序，先过载绕过）、**追踪**（`TraceTracker` 只在连着被追踪目标时推进，跑完即 `dc` 中止）、以及**肉鸡复用**（全网扫描默认跳过 `adminIP` 已是玩家的节点）。

## 安装

### 下载现成产物

从 [Releases](https://github.com/High-cla/Hacknet-AutoHack/releases/latest) 取 `AutoHack.dll`，放进游戏的 `BepInEx/plugins/` 目录：

```
<Hacknet>/BepInEx/plugins/AutoHack.dll
```

> 另有独立插件 **HacknetSaveFix**（修游戏本体存档 NRE，见「架构」），随同一个 Release 发布，放进同一个 `plugins/` 目录即可。

要从源码构建，见「构建」。

## 使用

游戏终端里输入 `autohack` 打开**控制面板**（再输一次关闭）：

```
autohack                                        # 开关控制面板
autohack run [here] [delay=秒] [stay] [redo] [nologs] [nomark] [direct]   # 不开面板，直接执行
autohack run script=stealth                     # 用脚本决定入侵次序（见下）
autohack scan|dec|mem|exes|unbreakable [allnodes]  # 独立工具（见「工具」）
autohack pull|purge|drop|trace|ip               # 对当前连接节点动手 / 掐追踪 / 换本机 IP
autohack skip                                   # 完成当前任务并接下一个（含 DLC 合同与 Kaguya Trials）
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
| `native exes` | 把原生破解程序挂进 RAM 面板当演出（缺省**开**）—— 纯演出，端口状态早已写好，exe 再调一次是幂等的 |
| `new IP after run` | 跑完把本机换成新 IP（缺省**开**）—— 游戏原生的「换 IP 保命」，并把全图已控机器的归属迁到新 IP |
| `NORMAL` / `FAST` / `INSTANT` | 推进节奏：非端口步保留真人间隔（0.35s）/ 压到 0.05s / 合并到**同一帧** |
| `disconnect when done` | 每个目标跑完是否 `dc`（缺省**关**）—— 断开会终止会话、清空 `navigationPath`，属行为选择。**只管断开**：清追踪已是每轮收尾的恒定动作，不再由开关控制 |
| `skip owned nodes` | 全网扫描时跳过已拿下的肉鸡，不重复入侵（缺省开） |
| — | 永远提不了权的机器（端口表容量 ≤ `portsToCrack`）在全网扫描时一律跳过，见下 |
| `RUN` | 按当前设置执行 |
| TOOLS 区 `SCAN NETWORK` / `DEC DECRYPT` / `MEMORY DUMP` / `ALL PROGRAMS` | 见「工具」，单击**立即执行**，无二次确认 |
| TOOLS 区 `PULL FILES` / `PURGE FILES` / `DROP NODE` | 对**当前连接的节点**动手：下载 / 删除当前目录下全部文件、把节点从网络图摘掉（后两个用告警色） |
| TOOLS 区 `UNBREAKABLE` | 加固本机，**不可逆**，用告警色标注 |
| TOOLS 区 | **反追踪不进 TOOLS 区**：清追踪已是每轮收尾的恒定动作（见「追踪」）。命令行即时清除仍可用 `autohack trace` |

执行期间面板切换为进度视图：阶段 + 百分比、分段进度条、当前目标与动作计数，下方滚动显示逐目标战果。完成后显示 `LAST RUN` 与 `RUN AGAIN`。

### 设置持久化

面板里的每一项设置（含面板位置与收起状态）都写进 `BepInEx/config/com.highcla.autohack.cfg`，重开游戏后原样恢复。

- 面板是**唯一**的设置界面 —— 没有第二套 UI，也就不存在两处设置互相漂移。cfg 只作落盘载体，手改它同样生效；非法值由 BepInEx 忽略并退回该项缺省，不会让游戏出错。
- 落盘时机是**指针抬起**而非每次改动：拖动面板或拉滑条期间每帧都在改值，逐帧写盘等于把磁盘打满。点击类控件在点击那一帧即落盘。
- `端口间隔` 读回时按 `0.02`–`5` 夹取，与命令行 `delay=` 同一套上下限，cfg 不会绕过校验。

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
| `instant` / `fast` / `slow` | 节奏档位：非端口步同帧连跑 / 0.05s / 0.35s（**缺省 slow**）。v1.27.0 删掉了面板上的三档 UI，**档位只从这里进** |
| `direct` | 跳过 connect，直接就地破解（probe 照跑并报告端口，只是不再回显 `probe` 这条指令） |
| `stay` | 跑完**不**断开连接（**已是缺省**，v1.16.0 起） |
| `dc` | 每个目标跑完断开（反追踪：追踪只在连着目标时推进） |
| `redo` | 全网扫描时**连已控节点一起重打**（缺省跳过肉鸡及永远提不了权的机器） |
| `script=文件` | 用一份**动作表**取代内置次序（见「脚本模式」） |

> `allnodes` 与缺省口径的差额实测（同一存档 147 节点）：沿连线广度优先 **7** 个目标，地图全表 **110** 个。
> 差额是「可以直接敲 IP 连上、但不在连线上」的机器 —— `Programs.connect` 遍历的是
> `netMap.nodes` 全表，本来就不检查 `links`。

### 自动换 IP

每轮入侵收尾时，玩家机自动换一个新 IP，并把**全图所有已控机器**的归属迁移过去。这是游戏原生的「换 IP 保命」动作（ISP 服务器上的 `Assign New IP`，`ISPDaemon.cs:122-142`），本插件把它自动化。

- **为什么**：追踪者判定依据是日志里出现过的玩家 IP —— 换掉 IP 等于让已有记录失去指向。这是游戏设计给玩家的最后手段（CSEC 任务链的追踪危机里，就要求玩家去 ISP 服务器手动改）。
- **归属迁移**：提权时游戏把玩家当时的 IP 写进目标机（`adminIP`），判据是 `adminIP == 玩家IP`。换 IP 会让该判据全部落空，故收尾时把全图里仍标记着旧 IP 的机器一并改成新 IP，已有战绩不丢。
- **回显**：终端写明 `new local IP: <旧> -> <新> (N owned node(s) re-tagged)`；`N` 为 0 表示当前没有被控机器。
- **两种用法**：面板 TOOLS 区的 `NEW IP` 按钮（单击立即换一次，与命令行 `autohack ip` 同一实现）；或 `new IP after run` 复选框（每轮收尾自动换）。
- **缺省关**（v1.32.6 起，此前为开）：换 IP 会**打断要求 IP 不变的任务链** —— lelzSec 那条明写「Your IP's been whitelisted (so dont go changing it for now)」，白名单记的是当时的 IP。要每轮自动换就勾上复选框或命令行传 `newip`；只换这一次用按钮/`autohack ip`。

| 命令 | 作用 |
|---|---|
| `autohack scan` | 把**当前节点所在的整张连通分量**标到地图上（无向闭包，含 EOS 设备）。**只揭示当前节点所在的那一张连通分量**（未连接时不扫，先 `connect`）。不睡、不设 admin 门禁，与面板 SCAN 按钮同一实现 |
| `autohack dec [allnodes]` | 解开目标上的 `#DEC_ENC` 加密文件，逐层解到明文，写入玩家 `/home/MemDumps` |
| `autohack mem [allnodes]` | 查看本机内存转储（紧凑格式，截断显示）、导出到 `/home/MemDumps`、扫描节点上的 `.mem` 并解其内嵌 DEC |
| `autohack exes` | 把游戏能生成的破解程序全部补进玩家 `/bin`（幂等） |
| `autohack unbreakable` | 加固玩家自己这台机器（**不可逆**） |
| `autohack pull` | 把**当前目录**下全部文件下载到本机 `/home/stash`（**一个夹**，不分流）。**注意**：`FileDownload` 类任务的判定不递归子目录（`Folder.containsFileWithData` 只查一级），故 `pull` 拉回的文件**不能**用于过这类任务 —— 要过请手敲 `scp <file>`（落 `/home`） |
| `autohack purge` | 删除**当前目录**下全部文件（同游戏 `rm`；与清痕**同一实现**；**只删文件，不删文件夹**）。`clearfolder` 类任务要求目标目录一个文件不剩，**先 `cd` 对再敲** —— 站错目录会删掉任务不需要的东西而目标目录仍非空 |
| `autohack drop` | 断开并把当前连接的节点从网络图上摘掉 |
| `autohack ip` | 给本机换一个新 IP（原生 `Assign New IP` 三步 + 全图已控机器归属迁移 + 重建 Pathfinder 查找表），与面板 `NEW IP` 按钮同一实现 |
| `autohack trace` | **反追踪**：同时止住两套追踪（倒计时 + 脱机追踪），**不碰对方 `/log`**（见「追踪」） |
| `autohack skip` | **跳过当前任务**：完成它并接下一个（走游戏自己的三条原生收尾通道，含 DLC 合同与 Kaguya Trials） |

注意 `pull` 拉回来的 `.exe` **不能直接跑**：游戏只在 `/bin` 里解析可执行程序（`ProgramRunner.cs:689` 写死 `searchForFolder("bin")`），要用得先 `mv` 到 `/bin`。

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

## 构建

构建配置会把产物**直接输出到游戏目录**，无需手工拷贝：

```
D:\steam\steamapps\common\Hacknet\BepInEx\plugins\AutoHack.dll
```

```bash
rm -rf src/AutoHack/obj src/AutoHack/bin
dotnet build src/AutoHack/AutoHack.csproj -c Release
```

第二个插件 `src/SaveFix/` 同法（`SaveFix.csproj`）：

```bash
rm -rf src/SaveFix/obj src/SaveFix/bin
dotnet build src/SaveFix/SaveFix.csproj -c Release
```

两个 csproj 都是 net472 + LangVersion 13，`HacknetDir` 缺省 `D:\steam\steamapps\common\Hacknet\`，可用 `-p:HacknetDir=<路径>` 覆盖；引用程序集从 `$(HacknetDir)` 就地取，`Private=false` 不复制。

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

### 第二个插件：HacknetSaveFix

`src/SaveFix/` 与 AutoHack **零耦合**，可单独安装/卸载，修的是**游戏本体**的存档缺陷：绕过主菜单进入 OS 的入口（HacknetHotReplace 直连、经扩展直接起 OS 等）会让 `OS.SaveUserAccountName` 停在 null（`OS.cs:148` 缺省即 null，只在 `MainMenu.cs:103/150/235/315` 被赋值），保存时把它当文件名传下去（`OS.cs:1522`），`SaveFileManager.GetSaveFileNameForUsername` 拿到 null（`:238`），`FileSanitiser.purifyStringForDisplay` 对 null 返回 null（`FileSanitiser.cs:9-12`），紧接着的 `.Replace` 打在 null 上 → NRE；而 `WriteSaveData` 把异常吞成一行日志（`SaveFileManager.cs:240-243`），**游戏不崩但存档静默失败**。修法是给崩溃点打一个 Harmony 前缀，用游戏自己在 `OS.cs:372` 用的同一套回落，不另立规则，只在真兜底时打一条 `LogWarning`。

## 相关开发资料

- `docs/API.md` — **Pathfinder 框架 + Hacknet 游戏本体的 public API 签名索引**，每条带 `文件:行号` 指向 `decompiled/`。由 `node tools/api-index.ts` 生成，勿手改
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

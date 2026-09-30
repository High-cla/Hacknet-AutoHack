# AutoHack — Hacknet 全自动入侵 Mod

仓库：<https://github.com/High-cla/Hacknet-AutoHack> · 许可：[MIT](LICENSE)

基于 Hacknet + Pathfinder 的自动入侵插件，带**交互式控制面板**。**优先使用游戏原生机制**：每个动作都走原生 API（`Programs.connect` / `Computer.openPort` / `Computer.giveAdmin` / `makeFile`）执行，并把对应指令回显进终端，行为与真人敲 `connect` / `probe` / `sshcrack 22` / `porthack` 一致 —— **终端看到什么，游戏状态就变什么**。端口步是这条规则的强化版：命令与端口都跟着原生动画走，动画**挂上面板那一刻**才回显、**跑完那一刻**才开端口（见下「面板」的 `native exes` 行）。四个游戏机制被显式处理：**管理员反扑**（断开时 `disconnectionDetected` 会关端口并把 `adminIP` 还原成机器自己，故离开前先解除反扑）、**跳板**（`proxyActive` 会拦下破解程序，先过载绕过）、**追踪**（`TraceTracker` 只在连着被追踪目标时推进，跑完即 `dc` 中止）、以及**强行提权**（porthack 门禁过不了时直接写 `adminIP`，见下）。

## 安装

从 [Releases](https://github.com/High-cla/Hacknet-AutoHack/releases/latest) 取 `AutoHack.dll`，放进游戏的 `BepInEx/plugins/` 目录：

```
<Hacknet>/BepInEx/plugins/AutoHack.dll
```

> 另有独立插件 **HacknetSaveFix**（修游戏本体存档 NRE，见「架构」）—— 它**单独发版**（[HacknetSaveFix 1.0.0](https://github.com/High-cla/Hacknet-AutoHack/releases/tag/v1.0.0)），取 `HacknetSaveFix.dll` 放进同一个 `plugins/` 目录即可。两者互不依赖，可各自单独安装或卸载。

## 使用

游戏终端里输入 `autohack` 打开**控制面板**（再输一次关闭）：

```
autohack                                        # 开关控制面板
autohack run [here] [stay] [redo] [nologs] [nomark] [direct]   # 不开面板，直接执行
autohack run script=stealth                     # 用脚本决定入侵次序（见下）
autohack scan|dec|mem [allnodes]                # 独立工具（见下表）
autohack exes|unbreakable|mods|ports            # 补程序 / 加固本机 / 扫模组（与网络范围无关）
autohack pull|purge|drop|trace|ip               # 对当前连接节点动手 / 掐追踪 / 换本机 IP
autohack wipe [here]                            # 清掉我的痕迹（缺省全网，here 收窄到当前分量）
autohack skip                                   # 完成当前任务并接下一个（含 DLC 合同与 Kaguya Trials）
autohack -h                                     # 帮助
```

### 面板

面板浮在游戏画面右下角，**全部自绘**（不用游戏原生 `Button`/`CheckBox`/`SliderBar`），鼠标直接操作。拖动标题栏可移到任意位置，`-` 收起为一条状态栏，`x` 关闭。

| 控件 | 作用 |
|---|---|
| `NETWORK SWEEP` / `CURRENT NODE` | 目标范围：沿网络连线可达的服务器 / 仅当前连接节点 |
| `whole map` | 全网扫描口径：勾选 = 地图全表（含不在连线上的机器），缺省不勾 = 沿连线广度优先。**揭图只在这里发生**（v1.39.0 起）—— 全网扫描 + 勾选时先把全图标成「已发现」再动手；`CURRENT NODE` 与命令行点名（`ids=…`）都不动地图：目标池取自当前连接 / 点名表，与「已发现」无关，先前会顺手点亮整片连通分量（勾了 `whole map` 更是点亮全图） |
| `wipe my traces` | 抹掉**你自己留下的**痕迹（缺省**开**，v1.33.2 起）—— 只删 `/log` 里**含你 IP 的条目**，目标机自己的操作史原样保留。**每轮收尾全网兜一次**（v1.33.3 起）：按目标展开的清痕只覆盖本轮打的机器，此前访问过的清不掉，而痕迹是累积的。范围随 SCOPE —— 「当前节点」只清当前节点所在的连通分量，其余（网络扫描 / 全网）清地图全表。留着痕迹会让带 `tracker="true"` 的机器在断开时自动排一次追踪，故缺省开；要留痕取消勾选 |
| `connect first` | 每个目标先 `connect` 再动手（缺省开） |
| `upload marker` | 是否上传 `~/autohack.txt` 标记（缺省**关**） |
| `use known creds` | 用已知账密登入目标（缺省**关**，v1.15.0 起）—— 成功即提权，跳过全部破端口 |
| `native exes` | 把原生破解程序挂进 RAM 面板当演出（缺省**开**）。**端口与回显都跟着动画走**：终端那行 `sshcrack 22` 在动画**真正挂上面板那一刻**才打出，排不上演出（无对应程序 / 队列满被丢弃）时立即打出并直接开端口 —— 故「终端出现某行命令」与「画面上开始跑那个动画」严格一一对应（v1.33.3 起端口由动画自己开）：那 9 个原生破解程序各自在跑完时调 `openPort`，所以看到动画跑完 = 那个端口真的开了 —— 不再是「状态早写好、动画只是重演」。内存够时多个动画并发（实测峰值 3 个同屏），提权前会等本台动画跑完（上限 180 秒）。没有对应动画的端口（443/3659/3724/9418/211/32）立即开。想跳过演出用 `noshow`（端口立即开，无动画；每台的 `probe` 端口报告照打 —— 那是侦察结果，不是演出） |
| `new IP after run` | 跑完把本机换成新 IP（缺省**关**）—— 游戏原生的「换 IP 保命」，并把全图已控机器的归属迁到新 IP |
| `disconnect when done` | 每个目标跑完是否 `dc`（缺省**关**）—— 断开会终止会话、清空 `navigationPath`，属行为选择。**只管断开**：清追踪已是每轮收尾的恒定动作，不再由开关控制 |
| `skip owned` | 全网扫描时跳过已拿下的肉鸡（缺省**开**）。**只对全网扫描生效** —— 「当前节点」是刻意选择，连上再点 START 就是要打它 |
| `auto mod ports` | 允许自动入侵去开**全部已注册的模组端口**（缺省**关**），等价于命令行 `modports=*`。勾上后不必再逐个点名协议 —— 见「模组端口」 |
| — | **强行提权已常驻**（v1.33.2 起，不再是开关）：porthack 门禁过不了时直接给目标写 `adminIP`。防护机（`portsToCrack=9999998`）与端口表凑不够门槛的机器只有这条路拿得下。提权落定后补一行原生收尾文案 `--Porthack Complete--`（v1.40.0，与 `PortHackExe.Completed()` 同串） |
| `RUN` | 按当前设置执行 |
| TOOLS 区 `SCAN NETWORK` / `DEC DECRYPT` / `MEMORY DUMP` / `ALL PROGRAMS` / `MOD PROGRAMS` / `PORT LIST` | 单击**立即执行**，无二次确认（见下表） |
| TOOLS 区 `PULL FILES` / `PURGE FILES` / `DROP NODE` | 对**当前连接的节点**动手：下载 / 删除当前目录下全部文件、把节点从网络图摘掉（后两个用告警色） |
| TOOLS 区 `UNBREAKABLE` | 加固本机，**不可逆**，用告警色标注 |
| TOOLS 区 `WIPE TRACES` | 清掉我的痕迹，用告警色标注。范围**随 SCOPE 段走**：「当前节点」只清当前节点所在的连通分量，其余清地图全表 |
| TOOLS 区 | **反追踪不进 TOOLS 区**：清追踪已是每轮收尾的恒定动作。命令行即时清除仍可用 `autohack trace` |

执行期间面板切换为进度视图：阶段 + 百分比、分段进度条、当前目标与动作计数，下方滚动显示逐目标战果。完成后显示 `LAST RUN` 与 `RUN AGAIN`。

**推进没有任何等待间隔**，整轮只受三道闸门约束：单帧步数预算 512、提权前等本台动画跑完（**仅勾着 `native exes`**，上限 180 秒，超时直接补开端口）、以及**端口步每帧只推进一步**。最后这道两种模式都启用 —— 它同时管「动画入队速率」与「终端回显速率」，去掉它，一帧 512 步会把整轮几百个端口挤进同一帧，终端一次涌出几百行，看不清破了什么。代价是端口步恒为 **1 帧/个 = 60 个/秒**：642 个端口约 **10.7 秒**。这是刻意取舍 —— 战果要看得见，而 60/秒已远快于任何人工节奏。

### 命令行

#### `autohack run` 的参数

| 参数 | 说明 |
|---|---|
| `here` | 仅当前已连接的节点（缺省 = 沿网络连线可达的服务器） |
| `keep` | **留下**我的痕迹（缺省是抹掉）。旧别名 `nologs` / `keep-logs` / `noownlogs` / `keep-own` / `keep-my-logs` 仍可用 |
| `logs` | 抹掉我的痕迹（**已是缺省**，写出来只为显式）。旧别名 `ownlogs` / `my-logs` / `selflogs` / `wipe-logs` 仍可用 —— v1.33.2 起「清目标」与「清自己」合并为同一口径：只删 `/log` 里含你 IP 的条目 |
| `nomark` | 不上传标记文件（**已是缺省**） |
| `mark` | 上传标记文件 |
| `allnodes` | 全网扫描改扫地图全表，不再只沿连线展开；`autohack scan allnodes` 同样改扫全表 |
| `creds` / `nocreds` | 用 / 不用已知账密登入（**缺省不用**，v1.15.0 起；`creds` 显式开启，`nocreds` 已是缺省） |
| `direct` | 跳过 connect，直接就地破解（probe 照跑并报告端口，只是不再回显 `probe` 这条指令） |
| `stay` | 跑完**不**断开连接（**已是缺省**，v1.16.0 起） |
| `dc` | 每个目标跑完断开（反追踪：追踪只在连着目标时推进） |
| `redo` | 全网扫描时**连已控节点一起重打**（缺省跳过肉鸡） |
| `script=文件` | 用一份**动作表**取代内置次序（见「脚本模式」） |
| `modports=协议,...` | 额外破解**其它插件注册的模组端口**（缺省**一个都不开**）。名字用各模组注册的协议名，如 `modports=mqtt,ntp,Redis`，**大小写不敏感**；可写多次累加。**`modports=*` = 全部已注册的模组端口**（与面板 `auto mod ports` 复选框等价）—— 不想逐个点名时用它。这些端口**直接开**，不跑模组自己的破解程序 —— 那 21 个 exe 的参数语义各异（实测 11 个「参数不足即退出」、多个开的是别人家的端口），没有可通用推断的形式（见下「模组端口」） |

> `allnodes` 与缺省口径的差额实测（同一存档 147 节点）：沿连线广度优先 **7** 个目标，地图全表 **110** 个。
> 差额是「可以直接敲 IP 连上、但不在连线上」的机器 —— `Programs.connect` 遍历的是
> `netMap.nodes` 全表，本来就不检查 `links`。

#### 独立工具

| 命令 | 作用 |
|---|---|
| `autohack scan [allnodes]` | 把网络标到地图上。缺省揭示**当前节点所在的整张连通分量**（无向闭包，含 EOS 设备；未连接时不扫，先 `connect`）；传 `allnodes` 改标**地图全表**（不看连线，未连接也照扫）。不睡、不设 admin 门禁，与面板 SCAN 按钮同一实现（口径随 `whole map` 复选框） |
| `autohack dec [allnodes]` | 解开目标上的 `#DEC_ENC` 加密文件，逐层解到明文，写入玩家 `/home/MemDumps` |
| `autohack mem [allnodes]` | 查看本机内存转储（紧凑格式，截断显示）、导出到 `/home/MemDumps`、扫描节点上的 `.mem` 并解其内嵌 DEC |
| `autohack exes` | 把游戏能生成的破解程序全部补进玩家 `/bin`（幂等） |
| `autohack mods` | 扫描其它插件（workshop mod）注册的自定义 exe 与自定义端口，并把它们的 exe 补进玩家 `/bin`（幂等）。**只提供文件，不参与自动入侵** —— 这些 exe 的参数语义各异（实测 21 个里 11 个有「参数不足即退出」的硬门禁，多个开的是别人的端口），强行自动化会开错端口、刷错误、卡住动画。它列出的端口可以喂给 `autohack run modports=...`（见「模组端口」） |
| `autohack ports` | 打出**当前节点**（未连接时本机）的端口清单：显示端口、协议名、显示名、**原始端口号**、是否有原生破解程序、是否已破。补的是 `probe` 的缺口 —— 它只打 `端口号 - 显示名`，而 `modports=` 要的是**协议名**（此前只能靠反编译模组才知道）。显示端口与原始端口并排打，是因为 `unbreakable` 随机化过端口号后两者不再相等，而 `openPort` 走的是原始端口。**只读**，不改任何端口状态 |
| `autohack unbreakable` | 加固玩家自己这台机器（**不可逆**） |
| `autohack pull` | 把**当前目录**下全部文件下载到本机 `/home/stash`（**一个夹**，不分流）。**注意**：`FileDownload` 类任务的判定不递归子目录（`Folder.containsFileWithData` 只查一级），故 `pull` 拉回的文件**不能**用于过这类任务 —— 要过请手敲 `scp <file>`（落 `/home`） |
| `autohack purge` | 删除**当前目录**下全部文件（同游戏 `rm`；与清痕**同一实现**；**只删文件，不删文件夹**）。`clearfolder` 类任务要求目标目录一个文件不剩，**先 `cd` 对再敲** —— 站错目录会删掉任务不需要的东西而目标目录仍非空 |
| `autohack drop` | 断开并把当前连接的节点从网络图上摘掉 |
| `autohack ip` | 给本机换一个新 IP（原生 `Assign New IP` 三步 + 全图已控机器归属迁移 + 重建 Pathfinder 查找表），与面板 `NEW IP` 按钮同一实现 |
| `autohack wipe [here]` | **清掉我的痕迹**：删掉 `/log` 里含你 IP 的条目。缺省**地图全表**；传 `here` 收窄到当前节点所在的连通分量。与面板 `WIPE TRACES` 按钮同一实现，也与每轮收尾那次兜底同一份代码 |
| `autohack trace` | **反追踪**：同时止住两套追踪（倒计时 + 脱机追踪），**不碰对方 `/log`** |
| `autohack skip` | **跳过当前任务**：完成它并接下一个（走游戏自己的三条原生收尾通道，含 DLC 合同与 Kaguya Trials） |

注意 `pull` 拉回来的 `.exe` **不能直接跑**：游戏只在 `/bin` 里解析可执行程序（`ProgramRunner.cs:689` 写死 `searchForFolder("bin")`），要用得先 `mv` 到 `/bin`。

### 脚本模式

脚本文件放在 `Content/HackerScripts/`，扩展名可写可不写，`#` 开头是注释（游戏本身没有注释语法，这里补一个，是唯一比游戏宽松的地方）：

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

### 设置持久化

面板里的每一项设置（含面板位置与收起状态）都写进 `BepInEx/config/com.highcla.autohack.cfg`，重开游戏后原样恢复。

- 面板是**唯一**的设置界面 —— 没有第二套 UI，也就不存在两处设置互相漂移。cfg 只作落盘载体，手改它同样生效；非法值由 BepInEx 忽略并退回该项缺省，不会让游戏出错。
- 落盘时机是**指针抬起**而非每次改动：拖动面板或拉滑条期间每帧都在改值，逐帧写盘等于把磁盘打满。点击类控件在点击那一帧即落盘。

## 构建

构建配置会把产物**直接输出到游戏目录**，无需手工拷贝：

```
D:\steam\steamapps\common\Hacknet\BepInEx\plugins\AutoHack.dll
```

```bash
rm -rf src/AutoHack/obj src/AutoHack/bin
dotnet build src/AutoHack/AutoHack.csproj -c Release
```

> 构建前**必须清掉 `obj`/`bin`** —— 否则会命中增量缓存，产出与源码不符的旧产物。

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
├── HackOverlay.cs      叠加层：唯一的 OS.Draw / OS.Update 补丁出口 —— 画面板 + 追踪 HUD、推进入侵 + headless 队列
├── ToolDispatch.cs     工具分派：全部 autohack 子命令的唯一入口（命令行与面板共用）
├── HackPanel.cs        面板绘制：布局、绘制入口、状态与常量
├── HackPanel.Draw.cs   　└ 自绘控件与绘制原语（Segment/Track/Measure 等）
├── HackRun.cs          执行模型：逐帧推进、动作分派、收尾（与绘制解耦）
├── HackRun.Plan.cs     　└ 计划构建：目标与选项 → HackStep 序列（纯函数）
├── HackRun.Steps.cs    　└ 单步实现：把 HackStep 落到游戏 API 上
├── NativeExes.cs       原生演出：破解程序的排队、挂载、回显与补开端口
├── HackEngine.cs       决策逻辑入口：目标解析与选择（含 ToolTargets）
├── HackEngine.Graph.cs 　└ 网络图遍历：可达闭包、广度优先展开、揭图
├── HackEngine.Ports.cs 　└ 端口表读取、破解命令、提权门槛（含端口容量）、防火墙
├── HackEngine.Wipe.cs  　└ 日志清痕：识别、删除与全网扫描
├── HackEngine.Whitelist.cs └ 白名单绕过
├── HackEngine.Counter.cs　└ 反追踪、抑制管理员反扑、跳板拆除、静默断开
├── HackTypes.cs        不可变数据：HackOptions / HackStep / PortInfo
├── HackTypes.Parse.cs  　└ 命令行解析：别名表与 token 归约
├── HackScript.cs       入侵脚本：行式动作表的解析与校验（script= 模式）
├── ModPortPolicy.cs    模组端口白名单：modports= 的解析、命中判定、未注册提示
├── PortTools.cs        端口清单：显示端口 / 协议名 / 原始端口号 / 破解状态（只读）
├── PendingRuns.cs      无面板运行的调度：命令线程只传参数，游戏线程构造并推进（按 OS 键控的 ConcurrentDictionary）
├── IsExternalInit.cs   net472 兼容垫片（record/init 需要）
└── GlobalUsings.cs     全局 using
```

设计上的三条硬边界：**`HackOverlay.cs` 是唯一的 `OS.Draw` / `OS.Update` 补丁出口**；**`ToolDispatch.cs` 是全部工具命令的唯一入口**（命令行与面板按钮共用一份实现，不存在两套行为）；**执行层与绘制解耦** —— `HackRun` 只由每帧 delta 驱动，可脱离 GUI 单独推进与测试。

### 第二个插件：HacknetSaveFix

`src/SaveFix/` 与 AutoHack **零耦合**，可单独安装/卸载，修的是**游戏本体**的存档缺陷：绕过主菜单进入 OS 的入口（HacknetHotReplace 直连、经扩展直接起 OS 等）会让 `OS.SaveUserAccountName` 停在 null（`OS.cs:148` 缺省即 null，只在 `MainMenu.cs:103/150/235/315` 被赋值），保存时把它当文件名传下去（`OS.cs:1522`），`SaveFileManager.GetSaveFileNameForUsername` 拿到 null（`:238`），`FileSanitiser.purifyStringForDisplay` 对 null 返回 null（`FileSanitiser.cs:9-12`），紧接着的 `.Replace` 打在 null 上 → NRE；而 `WriteSaveData` 把异常吞成一行日志（`SaveFileManager.cs:240-243`），**游戏不崩但存档静默失败**。修法是给崩溃点打一个 Harmony 前缀，用游戏自己在 `OS.cs:372` 用的同一套回落，不另立规则，只在真兜底时打一条 `LogWarning`。

## 相关开发资料

- `llms.txt` — **编码 agent 的主知识库**：三条硬约束、五十余条「最容易踩的坑 / 最该知道的机制」（每条带 `文件:行号` 指向 `decompiled/`）、以及本仓库的文件索引。改代码前先读它
- `docs/API.md` — **Pathfinder 框架 + Hacknet 游戏本体的 public API 签名索引**，每条带 `文件:行号` 指向 `decompiled/`。由 `node tools/api-index.ts` 生成，勿手改
- `docs/EXTENSIONS.md` — 游戏自带 `Extensions/` 官方样本的格式参考：节点 XML、占位符、行为系统、任务、阵营、主题
- `docs/HACKERSCRIPTS.md` — 自替换占位符全表 + HackerScript 动词表，以游戏实现与官方样本为准，已标出 wiki 的错漏处
- `docs/TOOLCHAIN.md` — 本地工具链分工与实测坑（动手写脚本前必看）

以下为**本地研究树**，未入库（体积大，且含第三方版权物与 binary；`.gitignore` 已排除，可按下列配方随时重建）：

- `upstream/Hacknet-Pathfinder/` — Pathfinder 源码（[Arkhist/Hacknet-Pathfinder](https://github.com/Arkhist/Hacknet-Pathfinder) 的 clone）
- `decompiled/game-proj/` — 游戏反编译（358 文件，ilspycmd `-p`）
- `decompiled/pathfinder/` — PathfinderAPI 反编译（134 文件）
- `refs/prs/` — 上游 10 个 PR 的 diff

## 许可证

[MIT](LICENSE) © 2026 High-cla

本仓库只包含插件源码。Hacknet 及其反编译产物、PathfinderAPI 二进制均为各自权利人的版权物，不在本仓库内。

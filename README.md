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
autohack -h                                     # 帮助
```

### 面板

面板浮在游戏画面右下角，**全部自绘**（不用游戏原生 `Button`/`CheckBox`/`SliderBar`），鼠标直接操作。拖动标题栏可移到任意位置，`-` 收起为一条状态栏，`x` 关闭。

| 控件 | 作用 |
|---|---|
| `NETWORK SWEEP` / `CURRENT NODE` | 目标范围：沿网络连线可达的服务器 / 仅当前连接节点 |
| `whole map` | 全网扫描口径：勾选 = 地图全表（含不在连线上的机器），缺省不勾 = 沿连线广度优先 |
| `PORT INTERVAL` 滑条 | 每个端口的破解间隔，`0.05`–`5` 秒，默认 `0.6`；支持滚轮微调，`≤0.15` 时数值转为警示色 |
| `wipe logs` | 是否在执行后清空目标日志 |
| `connect first` | 每个目标先 `connect` 再动手（缺省开） |
| `upload marker` | 是否上传 `~/autohack.txt` 标记（缺省**关**） |
| `anti-trace dc` | 每个目标跑完 `dc`：追踪只在连着目标时推进，断开即中止（缺省开） |
| `skip owned nodes` | 全网扫描时跳过已拿下的肉鸡，不重复入侵（缺省开） |
| — | 永远提不了权的机器（端口表容量 ≤ `portsToCrack`）在全网扫描时一律跳过，见下 |
| `RUN` | 按当前设置执行 |

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
| `delay=秒` | 每个端口的破解间隔，默认 `0.6`，范围 `0.05`–`5` |
| `nologs` | 不清除目标日志 |
| `nomark` | 不上传标记文件（**已是缺省**） |
| `mark` | 上传标记文件 |
| `allnodes` | 全网扫描改扫地图全表，不再只沿连线展开 |
| `direct` | 跳过 connect / probe，直接就地破解（不再回显这两条指令） |
| `stay` | 跑完**不**断开连接（缺省断开 = 回显并执行 `dc`，可中止追踪） |
| `redo` | 全网扫描时**连已控节点一起重打**（缺省跳过肉鸡及永远提不了权的机器） |

> `allnodes` 与缺省口径的差额实测（同一存档 147 节点）：沿连线广度优先 **7** 个目标，地图全表 **110** 个。
> 差额是「可以直接敲 IP 连上、但不在连线上」的机器 —— `Programs.connect` 遍历的是
> `netMap.nodes` 全表，本来就不检查 `links`（见「关键设计决策」#4c）。

## 行为：终端里看到什么

对每个目标，按真人入侵的次序执行，**每一步都先把指令回显到终端**（格式与 `OS.runCommand` 一致：换行 + 当前提示符 + 指令原文），再执行：

| 终端回显 | 实际动作 |
|---|---|
| `> connect 10.0.0.5` | `Programs.connect`（原生，自带 `Scanning For` / `Connection Established ::` 输出，并把提示符切成 `<ip>@> `） |
| `10.0.0.5@> probe` | 读取目标端口表，按 `Programs.probe` 的原生格式逐行输出报告 |
| `10.0.0.5@> sshcrack 22` | `Computer.openPort(22, 玩家IP)`（**游戏自身签名**，框架 Prefix 已把它接到端口表） |
| `10.0.0.5@> solve ABCDEF` | `Firewall.attemptSolve`（**游戏自身**入口，同玩家敲 `solve`）。解序列由游戏生成并公开在 `Firewall.solution` |
| `10.0.0.5@> porthack` | 达门槛时 `Computer.giveAdmin(玩家IP)` |
| `10.0.0.5@> dc` | 断开前先 `Computer.admin = null`，再 `Programs.disconnect`（原生）。**同时**解除延迟反扑与中止追踪：前者见下，后者因 `TraceTracker.Update` 见 `connectedComp == null` 立刻置 `active = false` |
| `[autohack] trace killed - timer stopped.` | `TraceTracker.stop()` —— 直接毙掉追踪（非指令，故无回显）。断开已让它失效，这步是确定性的兜底 |
| `10.0.0.5@> rm /log/Connection:_from_10.0.0.1` | 逐条抹除该目标的 `/log` 文件。因断开自身会追加一条记录，清痕排在 `dc` **之后**，此时已不在目标上，故改为一行状态：`[autohack] <名> :: wiped 3 log file(s)` |
| `[autohack] <名> :: proxy bypassed` | 跳板解除（非指令，故无回显） |
| `[autohack] <名> :: 3/5 ports, admin=yes` | 收尾战果行（非指令） |

> **顺序是刻意的**：提权、上传、**以及断开连接**都会向 `/log` 追加记录（`Computer.disconnecting` 写 `"<玩家IP> Disconnected"`，`Computer.cs:722-727`），所以清痕必须排在本目标的**最后**（`dc` 之后），否则痕迹残留。
> 破解指令名取自游戏数据（`PortExploits.cracks`），显示端口取自框架端口表（`PortState.PortNumber`），均不硬编码。
>
> **跳板**：目标 `proxyActive` 时，`OS.addExe` 会拦下所有 `needsProxyAccess` 的破解程序（`Proxy Active -- Cannot Execute`），破解必然失败。解除方式是把 `proxyOverloadTicks` 收敛到 0、`proxyActive` 置 false —— 与原生 `ShellExe` 过载跑完的终态逐字节相同（`ShellExe.cs:96-99`），但**不等**那 30 秒。
>
> 游戏本身没有「立即完成过载」的 API：终端 `ComShell.exe -o`（`OS.cs:2134` → `ShellOverloaderExe` → `ShellExe.StartOverload`）启动的就是同一个逐帧扣减的 `ShellExe`，跑满一次要 `BASE_PROXY_TICKS = 30f` 秒（`Computer.cs:27`）——全网扫一遍就是几十段纯等待。跳过等待**没有副作用**：全游戏 `AchievementsManager.Unlock` 与跳板无关（唯一的追踪成就在 `TraceTracker.cs:70` 的 `trace_close`），过载也不推进追踪。**刻意不照抄原生 `ShellExe.cs:105` 的 `hostileActionTaken()`**：过载本身已经通过扣减 `proxyOverloadTicks` 生效，而 `hostileActionTaken()` 的唯一作用是在连着目标时 `traceTracker.start()`——调它只是白白点燃反追踪（这正是旧版「还会触发反追踪」的元凶，它每帧调一次）。
>
> **追踪**：`TraceTracker` 只在 `connectedComp` 就是被追踪目标时推进计时（断开或换目标立即 `active = false`），归零则走 `OS.timerExpired()` 端掉玩家。故每个目标跑完都 `dc`；收尾时若 `traceTracker.active` 仍为真，再补一次断开并把结果写进终端。
>
> **管理员反扑**：断开连接会让游戏执行 `computer.admin?.disconnectionDetected()`（`OS.handleDisconnection`，`OS.cs:944-950`），而 `BasicAdministrator` 会在 **0~20 秒随机延迟**后关掉该机全部端口、并按 `ResetsPassword` 重置密码，最后执行 `c.adminIP = c.ip` —— **把刚写入的玩家 IP 抹掉**。后果就是：断开的瞬间看起来成功了，十几秒后这台机器又变回「未拿下」，全网扫描逐个断开等于逐个白跑（这就是「失去效果」的根因）。Pathfinder 只补了「关端口要按 `GetAllPortStates()` 的协议名」（`ComputerExtensions.cs:418-449`），**没有**覆盖 `adminIP` 还原。本插件在离开每个目标前把 `Computer.admin` 置 null（`type="none"` 节点的原生状态，`ComputerLoader.cs:425`），从源头取消这次反扑。

## 肉鸡（已控节点）

入侵成功后目标即成为肉鸡 —— 游戏里的所有权标记是 `Computer.adminIP`（`giveAdmin(ipFrom)` 写入，并被 `SaveWriter`/`SaveLoader` 持久化到存档）。但`Computer.admin` 是任务用的 `Administrator` 行为对象，**不是**所有权标记，别拿它判断。

全网扫描（`all`／`Whole network`）**默认跳过 `adminIP == 玩家IP` 的已控节点**，避免重复敲一遍已经没有意义的机器：

```
[autohack] Headless run: 7 target(s), 60 action(s), 5 owned node(s) skipped.
[autohack] skipped 5 node(s) already owned - 'redo' to include them.
```

- `here` 与显式点名的目标**不过滤** —— 那是刻意的选择，且重打已控节点本身是合法用法（重放、重置状态）。
- 想连肉鸡一起重扫：CLI 加 `redo`，或关掉面板的 `skip owned nodes`。

## 架构

```
src/AutoHack/
├── AutoHackPlugin.cs   插件入口：命令注册（属性扫描）+ PostLoad 自检 + 装配 Harmony
├── HackOverlay.cs      叠加层：patch OS.Draw 画面板（Prefix 抢输入）/ patch OS.Update 推进执行
├── HackPanel.cs        面板绘制：自绘控件 + 进度/战果视图（含主题配色 Palette）
├── HackRun.cs          执行模型：动作序列、逐帧推进、指令回显（与绘制解耦）
├── HackEngine.cs       决策逻辑：可连接目标遍历、端口表读取、提权门槛（含端口容量）、防火墙破解、反扑解除、跳板绕过、日志清理
├── HackTypes.cs        不可变数据：HackOptions（参数解析）/ HackStep（含回显指令）
├── PendingRuns.cs      无面板运行的调度：命令线程只传参数，游戏线程构造并推进（按 OS 键控的 ConcurrentDictionary）
├── IsExternalInit.cs   net472 兼容垫片（record/init 需要）
└── GlobalUsings.cs     全局 using
```

### 关键设计决策

**1. 为什么用属性注册而非手动调用。**
`Pathfinder.Meta.Load.AttributeManager` 用 Harmony IL hook 挂在 `HacknetChainloader.LoadPlugin` 对 `HacknetPlugin.Load()` 的调用点，自动扫描整个程序集上的 `BaseAttribute` 派生特性。因此 `[Command("autohack")]` 无需任何手动注册即可生效。

**2. 为什么必须声明 `[BepInDependency("com.Pathfinder.API")]`。**
该 IL hook 是在 `PathfinderAPIPlugin.Load()` 里 `PatchAll` 才安装的。若本插件先加载，属性扫描不会覆盖到它 —— 命令会**静默失效**。实测初始顺序为 `AutoHack → AutoUpdater → PathfinderAPI`（错误），加依赖后变为 `AutoUpdater → PathfinderAPI → AutoHack`（正确）。

**3. 为什么面板挂在 `OS.Draw` 上，而不是做成 ExeModule。**
- ExeModule 被限制在 RAM 面板内且占用内存；叠加层可自由定位、不占 RAM、可随时开关。
- 能直接拿到正在绘制的 OS 实例 —— `OS.currentInstance` **不可靠**：它只在构造函数里赋值且从不置空，主菜单里的 `new OS()` 也会改写它。
- 用 `GameScreen.IsActive` 判断画面是否真的可见，主菜单/弹窗覆盖时面板自动隐藏。
- 此处游戏自身的 `spriteBatch` 已 End（`Draw` 内的 `startDraw`/`endDraw` 成对），且 `PostProcessor.end()` 已把渲染目标交还后台缓冲，故自行 `Begin`/`End` 即可安全叠加。
- **Prefix 与 Postfix 分工**：Prefix 抢输入（见上文「面板」节的模态说明），Postfix 自绘面板。二者都用同一份 `Visible()` 判定，避免出现「Prefix 抢了输入但面板没画」的错位帧。

**3b. 为什么面板控件自绘，以及为什么只能用 `GuiData` 里已初始化的控件。**
自绘的三条理由见上文「面板」节。另有两个硬约束卡住了「照抄原生控件走一遍」这条路：
- `GuiData.UISmallfont` / `UITinyfont` **从未被赋值**（`GuiData.InitFontOptions` 只装 `smallFont`/`tinyFont`/`bigFont`），只有 `smallfont`/`tinyfont`/`font` 可用 —— 用 `UISmallfont` 会在 `MeasureString` 处直接 NRE，且该异常被 `OS.Draw` 的内层 catch 吞掉，表现为「面板时好时坏」。
- 自绘控件必须自己维护 `GuiData.hot`/`active`，否则游戏原生控件与面板控件会同时响应同一次点击。

**4. 为什么端口状态必须读 Pathfinder 的端口表，而不是原版 `portsOpen`。**
Pathfinder 用 `[HarmonyPrefix]` 接管了 `Computer.openPort(int, string)` 并 **return false 跳过原版实现**，端口状态改存自己的 `PortState.Cracked`（`ConditionalWeakTable` 里的 `PortTable`）。因此原版 `portsOpen` 数组永不更新 —— 直接读它会**恒为 0**，提权判断将永不成立。**读**状态一律走框架 API：`GetAllPortStates()` / `state.Cracked` / `state.PortNumber` / `Record.OriginalPortNumber` / `CountOpenPorts()`，**没有任何一条**读原版 `portsOpen` 的路径（该字段在装框架的游戏里恒为 0，读它就是 bug）。**写**状态则走游戏自身的签名 `Computer.openPort(int portNum, string ipFrom)`（传 `Record.OriginalPortNumber`）—— 它正是原生破解程序完成时的调用形态，也是 Pathfinder Prefix 接管的那个重载；只要不在它之后去读 `portsOpen`，两者完全同路。详见 `docs/RESEARCH.md`。

**4c. 全网扫描两套口径，「沿连线」为缺省（v1.10）。**
`Programs.connect`（`Programs.cs:231-322`）在 `os.netMap.nodes` 里按 ip/name 线性查找，
**全程不检查 `visibleNodes`，也不看 `links`** —— 地图上任何节点都是「敲 IP 就能连」的。
`visibleNodes` 只是原版 `scan` 维护的「已发现」展示标记，不是连接许可。

据此有两套口径，用 `whole map` 开关／`allnodes` 参数切换：

| 口径 | 实现 | 实测（存档 147 节点） |
|---|---|---|
| **沿连线广度优先（缺省）** | `ReachableComputers`：多源种子（玩家机 + 已发现节点）沿 `Computer.links` 展开 | **7** 个目标 |
| 地图全表 | `ConnectableComputers`：遍历 `netMap.nodes`，只排除玩家机 | **110** 个目标 |

缺省取保守口径，是因为它只碰图上确有通路的机器；但要注意这条通路判据比游戏实际的
连接能力**更窄** —— 差额那 103 台正是「可以直接敲 IP 连上」却被连线图漏掉的机器。
两者都只排除玩家机，`disabled`／已控／永远提不了权的机器统一由 `ResolveTargets` 过滤。

广度优先还会把新展开的节点按原生 `scan` 的后效委托 `NetworkMap.discoverNode`
（`NetworkMap.cs:415`）标为已发现，不做自绘的「伪发现」。

**4b. 提权走原生门禁，不绕过（v1.9 修正）。**
游戏自己的 porthack 有硬门禁（`OS.cs:1908-1930`）：已攻破端口数必须**超过** `portsNeededForCrack`，**且** `firewall == null || firewall.solved` —— 缺其一就写 `Target Machine Rejecting Syndicated UDP Traffic` 并拒绝启动 `PortHackExe`。旧版直接调 `Computer.giveAdmin` 把整条门禁跳过去了，等于从未用过原生的提权与防火墙机制。现在：
- `CanEscalate` 逐条对齐该门禁（含防火墙）；
- 防火墙走游戏自身的 `Firewall.attemptSolve(solution)`（`Firewall.cs:101-116`），同玩家敲 `solve`；**不用 `Programs.solve`** —— 它内层先跑 `doDots(30, 60)`，每点 `Thread.Sleep(60)`，合计约 1.8 秒阻塞游戏线程。解序列由游戏生成并公开在 `Firewall.solution`，不必等 `analyze` 的逐趟收敛跑完；
- 追加 `CanEverEscalate`：**端口表容量 ≤ 门槛**的机器永远开不满、永远提不了权（门槛由 `openPortsForSecurityLevel` 定义为 `security - 1`，`Computer.cs:200-204`）。实测存档里既有 `portsToCrack="9999998"` 的剧情保护机，也有门槛 8/6 而端口表只有 4~5 个的机器；这类机器此前每次全网扫描都被连上、逐个破端口、再提权失败 —— 即「每一次判断都是要入侵」。全网扫描现在直接剔除（显式点名仍尊重玩家）。

**5. 跳板收敛到终态，追踪直接毙掉。**
两者性质不同，处置也必须不同。
- **跳板（`proxyActive`）**：把 `proxyOverloadTicks` 置 0、`proxyActive` 置 false —— 与 ShellExe 过载跑满的终态（`ShellExe.cs:96-99`）逐字节相同。游戏**没有**更快的路径：终端 `ComShell.exe -o` 启动的就是同一个逐帧扣减的 ShellExe，跑满要 `BASE_PROXY_TICKS = 30f` 秒（`Computer.cs:27`）。跳过等待无副作用 —— 全游戏 12 处 `AchievementsManager.Unlock` 里唯一与追踪相关的是 `TraceTracker.cs:70` 的 `trace_close`，与跳板无关（v1.5 曾误判「跳过会丢成就」，v1.7 已订正）。**唯一刻意不重演**的是过载循环里那句 `hostileActionTaken()`（`ShellExe.cs:105`）：它不参与跳板失效，只负责点燃追踪。
- **追踪（`TraceTracker`）**：走游戏自身的 `TraceTracker.stop()`（`TraceTracker.cs:116-119`，即 `active = false; trackSpeedFactor = 1f;`）——**直接毙掉，零每帧开销**。`TraceTracker.Update` 开头就是 `if (!active) return;`（:53-56），停是彻底的，没有「暂停」形态。这也是游戏自己的做法：`SecurityTraceExe.Killed()`（`SecurityTraceExe.cs:26`）关程序时这么干，`OS.thisComputerIPReset()`（`OS.cs:1793-1796`）换 IP 时也是直接置 `active = false`。
  - **不照抄 `TraceKillExe` 的「每帧把 `timeSinceFreezeRequest` 置 0」**：那是它作为 GUI 程序的职责 —— 玩家开着它时要看到 `SUPPRESSION ACTIVE` 的持续效果，故必须逐帧续期。mod 要的是「立即终止」这一动作，照抄只会白白常驻一个每帧补丁。
  - 断开连接仍是每一步的收尾（`dc`），它让 `TraceTracker` 自己失效（`connectedComp` 为空，`TraceTracker.cs:60-66`）并顺带走完成就与警告闪烁；`stop()` 是随后确定性的兜底，覆盖 `stay` 模式与「断开后才被点燃」的窗口（如目标机带 tracker，经 `TrackerCompleteSequence` 延迟 10~20 秒启动，`OS.cs:950-958`）。

**5b. 命令线程与游戏线程的分工（v1.8 修正）。**
`autohack run` 由 `OS.execute` 在**派生线程**上执行（OS.cs:1754-1767，日志里的 `Spawning thread for command autohack` 就是它），而它触碰的每一样东西 —— 连接状态、`netMap.visibleNodes`、`Computer.files` —— 都属于游戏线程。故命令入口**只解析参数并入队**，真正的 `HackRun` 构造推迟到首帧的 `OS.Update`。构造里要遍历 `netMap.nodes` 解析目标集合，而游戏线程每帧都在动那张表。调度容器用按 OS 键控的 `ConcurrentDictionary`：`TryAdd` 同时充当「同一终端只允许一条运行」的原子互斥。

**6. 管理员反扑只能从源头解除。**
`Computer.admin` `disconnectionDetected` 的延迟回调是在`断开那一刻`注册进 `os.delayer` 的（`BasicAdministrator` `20.0 * Utils.random.NextDouble()`），事后无法撤销；唯一可控的时点是**断开之前**。所以本插件把「解除反扑」做成独立步骤（`HackStepKind.Neutralize`），排在侦察之前 —— 而不是挂在 `connect` 上，因为 `direct`／已连接的路径根本没有 `connect` 步。置 null 而非替换成自定义 `Administrator`：`type="none"` 本就是游戏的原生状态，`admin?.` 是空条件调用，语义完全对齐，不需要新类型。

## 验证

### 加载顺序与注册（实测日志）

```
[Info : BepInEx] Loading [AutoUpdater 5.3.4]
[Info : BepInEx] Loading [PathfinderAPI 5.3.4]   ← Pathfinder 先，安装属性扫描 hook
[Info : BepInEx] Loading [AutoHack 1.10.0]        ← 本插件后，能被扫描到
[Info : AutoHack] AutoHack loaded (GUI).
[Info : AutoHack] self-check OK: 'autohack' is registered and autocompletes.
```

自检读的是游戏**自己的** `ProgramList.programs`（自动补全注册表，由 `CommandManager` 在注册自定义命令时填充）——命中即证明扫描链路完整，而非仅凭本插件自述。

### 静态验证（反编译产物）

```
[BepInPlugin("com.highcla.autohack", "AutoHack", "1.10.0")]
[Command("autohack", true, false)]

Echo:   os.write("\n" + os.terminal.prompt + command);
Connect: Programs.connect(new[] { "connect", target.ip }, os);
Ports:   comp.GetAllPortStates() -> PortState.Cracked / PortNumber / Record.OriginalPortNumber
Open:    comp.CountOpenPorts()
Targets: ComputerLookup.Find(id)            // 取代 Programs.getComputer
Probe:   "Port#: " + PortNumber + "  -  " + DisplayName + " : OPEN"
Crack:   PortExploits.cracks[code] -> "sshcrack" + " " + PortInfo.DisplayPort
OpenP:   target.openPort(port.CodePort, os.thisComputer.ip)  // 游戏自身签名（框架 Prefix 接端口表）
Firewall: target.firewall.attemptSolve(target.firewall.solution, os)                // 同玩家敲 solve，无阻塞
Escalate: HackEngine.CanEscalate(target) && target.giveAdmin(os.thisComputer.ip)   // 对齐 porthack 门禁
Owned:   comp.adminIP == os.thisComputer.ip                                  // 肉鸡判定
Hopeless: Ports(comp).Count <= comp.portsNeededForCrack                      // 永远开不满 -> 跳过
Clean:   root.searchForFolder("log").files.Clear()  -> 回显 "rm /log/<名>"（连着时）
Proxy:   HackEngine.BypassProxy(target) -> proxyOverloadTicks = 0f; proxyActive = false  // 同 ShellExe 过载终态
Neutral: HackEngine.SuppressCounterattack(target) -> comp.admin = null             // 断开前解除反扑
Trace:   os.traceTracker.stop() -> active = false; trackSpeedFactor = 1f           // 直接毙掉，零每帧开销
Targets: ReachableComputers(os) -> BFS over links (缺省) / ConnectableComputers -> 全表 (allnodes)
```

面板侧（v1.5.0 自绘 UI）：

```
Chrome:  Fill(frame, Panel) + doRectangleOutline(Edge,1) + 3px 高亮色左条
Palette: os.highlightColor / os.terminalTextColor 取值 -> 主题跟随
Fonts:   GuiData.smallfont 统一 + 0.9/1.0/1.1/1.3 四级缩放
Widgets: Segment / Glyph / Check / Slider / PrimaryButton / Progress 全部自绘
Track:   GuiData.hot / active / mouseWasPressed / mouseLeftUp 自维护
Modal:   HackPanel.LastFrame.Contains(getMousePoint()) -> blockingInput = true  // OS.Draw Prefix
原生控件调用数：0（Button.doButton / CheckBox.doCheckBox / SliderBar.doSliderBar / TextItem.do* 反编译后零命中）
```

> 注意：.NET 字符串在 PE 里是 **UTF-16LE** 存储，`grep` 明文查 DLL 恒返回 0；校验二进制必须反编译（`ilspycmd`）。

v1.7 反编译产物逐项核对（`decompiled/autohack-v7/AutoHack.decompiled.cs`，1792 行）：

```
OverloadProxy        0    已删（逐帧过载循环）
hostileActionTaken   0    从不调用（不点燃追踪）
ReachableComputers   2    可达遍历 + 调用点
discoverNode         1    委托游戏原生发现
BypassProxy          7    跳板一次收敛
Seed                 3    BFS 种子（含边界判断去重）
proxyOverloadTicks = 0f @317 / proxyActive = false @318   同 ShellExe 终态
```

v1.8 反编译产物逐项核对（`decompiled/autohack-v8/AutoHack.decompiled.cs`，1807 行）：

```
Elapsed              2    仅剩 gameTime.ElapsedGameTime（死属性已删）
AtMost               0    已删（LINQ Take 换成 for 索引循环）
ConcurrentQueue      1    跨线程队列换并发集合
TryPeek/TryDequeue   2    游戏线程出队，命令线程入队
internal void Tick   1    Tick 不再返回无用的动作计数
MaxOutcomeRows       1    取代裸魔法值 5
MaxY(Rectangle,int)  1    纵向上限按当前高度，不再用收起高度
Upper(string)        1    Phase 在赋值处大写，绘制期零转换
原生控件 / UISmallfont / UITinyfont   0
```

该轮只动外围（死代码、并发、每帧开销、注释），四条内核链路当时零改动。
**其中「目标集合」「提权门槛」已在 v1.9 被替换**，见下。


v1.9 反编译产物逐项核对（`decompiled/autohack-v9/AutoHack.decompiled.cs`，1859 行）：

```
ConnectableComputers   2    netMap.nodes 全表 —— 取代 BFS（对齐 Programs.connect 判据）
ReachableComputers     0    已删
DiscoveredComputers    0    已删（BFS 兜底）
private static void Seed 0  已删（BFS 种子）
CanEverEscalate        2    端口表容量 > portsNeededForCrack，否则永远开不满
SolveFirewall          7    步类型 + 决策 + 执行 + 回显
attemptSolve           1    走游戏自身入口（不用 Programs.solve 的 doDots 阻塞）
SkippedHopeless        6    跳过计数（决策/属性/报告/面板/终端）
HackStepKind.SolveFirewall 2  新步骤
Disconnect @1693 -> CleanLogs @1697   清痕排在本目标最后（断开自身会写 /log）
原生控件 / UISmallfont / hostileActionTaken   0 / 0 / 0
```

v1.10 反编译产物逐项核对（`decompiled/autohack-v10/AutoHack.decompiled.cs`，1963 行）：

```
KillTrace               8    新步骤：决策 + 枚举 + 执行 + 回显 + 收尾兜底
traceTracker.stop       1    走游戏自身的 stop()，不照抄 TraceKill 的每帧续期
ReachableComputers      2    缺省口径（沿 links 广度优先）恢复
ConnectableComputers    2    allnodes 口径（netMap.nodes 全表）
AllNodes                7    开关（选项/面板/解析/决策 + allnodes 别名）
ShellTrap / forkBombClients  0 / 0   已撤（ActiveHackers 仅剧情脚本填充，反制不了普通追踪）
hostileActionTaken / UISmallfont / doButton   0 / 0 / 0
```

### 未验证

**面板的实际渲染、鼠标交互与终端回显的肉眼观感需要真人进游戏确认**（无法自动化）。已加一次性诊断日志：渲染路径首次执行时会记录 `Overlay drawing at <宽>x<高>`。

## 环境

| 项 | 值 |
|---|---|
| 游戏 | `D:\steam\steamapps\common\Hacknet`（v5.069 + Labyrinths） |
| PathfinderAPI | 本仓库框架（`upstream/Hacknet-Pathfinder` `HacknetChainloader.VERSION` = `5.4.1`；游戏目录内为 `5.3.4` 安装版） |
| 目标框架 | `net472` |
| LangVersion | 13 |
| dotnet SDK | 10.0.301 |

本机缺 .NET Framework 4.7.2 目标包（`MSB3644`），csproj 已指向本地参考程序集：

```xml
<TargetFrameworkRootPath>$(MSBuildThisFileDirectory)refs\assemblies\netfx-all\</TargetFrameworkRootPath>
```

> `refs/` 未入库。新克隆若同样缺目标包，安装 .NET Framework 4.7.2 Developer Pack 后删掉该属性即可；或自行准备同结构的 `refs/assemblies/netfx-all/`。

## 调研资料

- `docs/RESEARCH.md` — 完整调研：原生机制、API 精确签名、陷阱

以下为**本地研究树**，未入库（体积大，且含第三方版权物与 binary；`.gitignore` 已排除，可按下列配方随时重建）：

- `upstream/Hacknet-Pathfinder/` — Pathfinder 源码（[Arkhist/Hacknet-Pathfinder](https://github.com/Arkhist/Hacknet-Pathfinder) 的 clone）
- `decompiled/game-proj/` — 游戏反编译（358 文件，ilspycmd `-p`）
- `decompiled/pathfinder/` — PathfinderAPI 反编译（134 文件）
- `refs/prs/` — 上游 10 个 PR 的 diff

## 许可证

[MIT](LICENSE) © 2026 High-cla

本仓库只包含插件源码。Hacknet 及其反编译产物、PathfinderAPI 二进制均为各自权利人的版权物，不在本仓库内。

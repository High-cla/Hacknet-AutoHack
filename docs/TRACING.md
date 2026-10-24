# Hacknet 追踪与反追踪机制详解

> 依据：`decompiled/game-proj/`（游戏反编译，**行号引用的唯一依据**）+ `Content/HackerScripts/`（游戏自带脚本）。
> 本文只描述**游戏本体**的机制；AutoHack 的对应实现在每节末尾单列，代码位置见 `src/AutoHack/`。
> 版本基准：游戏本体（Hacknet + Pathfinder），AutoHack v1.40.0。

---

## 0. 先分清：三套机制，不是一个东西

名字相近、常被混为一谈的三套机制，触发源、判据、后果**完全独立**：

| # | 机制 | 触发源 | 是否可见 | 归零/触发后果 |
|---|---|---|---|---|
| 1 | `TraceTracker`（倒计时） | 目标机 `traceTime > 0`，且**你连着它**时做了敌对动作 | **可见**：左下角 `TRACE :` 百分比 + 蜂鸣 | 崩玩家机 / DLC 撤离流程 |
| 2 | `OS.TrackersInProgress`（脱机追踪） | 你**断开**连接那一刻，目标 `/log` 里有含你 IP 的复制/删除/移动记录 | **不可见**（原生零 UI） | 10~20 秒后目标反打：连你、开端口、forkbomb 崩机 |
| 3 | `BasicAdministrator.disconnectionDetected`（管理员反扑） | 你断开连接 | 不可见 | 关该机全部端口、`adminIP` 还原 → **肉鸡标记丢失** |

第 3 套严格说不是「追踪」，但它与第 2 套由**同一个函数** `OS.handleDisconnection()`（`OS.cs:944-960`）派生，且后果都是「断开之后才发作」，所以必须一起理解。

**一句话概括**：第 1 套**连着才推进**，第 2、3 套**断开才发作** —— 方向相反，这是所有取舍的根据。

---

## 1. 追踪从哪来：`traceTime` 与 `hostileActionTaken()`

### 1.1 `traceTime` 是每台机器的字段

`Computer.cs:43` 声明，`:118` 缺省 `-1f`（= 永不追踪）。

自动赋值只在 `Computer.openPortsForSecurityLevel`（`Computer.cs:200-231`）：

```csharp
// Computer.cs:222-225
if (security >= 4)
{
    traceTime = (float)Math.Max(10 - security, 3) * BASE_TRACE_TIME;
}
```

`BASE_TRACE_TIME = 15f`（`Computer.cs:29`）：

| security | 4 | 5 | 6 | 7 | ≥ 8 |
|---|---|---|---|---|---|
| `traceTime`（秒） | **90** | **75** | **60** | **45** | **45** |

- **security ≤ 3 的机器天赋不追踪**（`traceTime = -1`，`hostileActionTaken` 里 `> 0f` 判据不成立）。
- 任务与地图生成器可覆盖：`MissionFunctions.cs:210-212` 写 `7.5 * BASE_TRACE_TIME` = **112.5 秒**；`MissionGenerator.cs:103` 写 `10 * BASE_TRACE_TIME` = **150 秒**。
- 存档以 `<security ... traceTime="..."/>` 存回（`Computer.cs:921`），读档直接取（`ComputerLoader.cs:1526`）—— 所以**手改存档能改追踪时长**。

### 1.2 唯一的点火口：`hostileActionTaken()`

```csharp
// Computer.cs:294-308（节选）
public void hostileActionTaken()
{
    if (os.connectedComp != null && os.connectedComp.ip.Equals(ip))   // 条件 A：必须是当前连接目标
    {
        if (traceTime > 0f)                                          // 条件 B：这台机自己会追踪
        {
            os.traceTracker.start(traceTime);
        }
        if (os.timer - timeLastPinged > 0.35f) { /* ping 音效 */ }
    }
}
```

两个条件缺一不可。**关键**：条件是「这台机器是不是当前连接目标」，不是「你有没有权限」。

### 1.3 什么动作会点燃（`hostileActionTaken` 的全部调用点）

| 调用点 | 时机 |
|---|---|
| `SSHCrackExe.cs:90` / `SMTPoverflowExe.cs:61` / `HTTPExploitExe.cs:59` / `FTPBounceExe.cs:72` / `SQLExploitExe.cs:63` | `LoadContent()` —— **动画挂上 RAM 面板那一刻** |
| `PacificPortExe.cs:26` / `TorrentPortExe.cs:36` / `RTSPPortExe.cs:32` / `SSLPortExe.cs:38` / `FTPFastExe.cs:28` | **构造函数** —— 实例被创建那一刻 |
| `PortHackExe.cs:88` | `Update()` 内，进度未满时**每帧** |
| `Programs.cs:1192` | `analyze` 命令（目标有防火墙时） |
| `ShellExe.cs:105` | 跳板过载 |
| `DLCTraceSlower.cs:34` | SignalScramble 生成时 |

**不点火的动作**：`probe`、`login`、`connect`、`solve`、`rm`、`scp`、`ls`、`cat`。

> 实战含义：`probe` 看一眼是安全的，`analyze` 不是。
> **演出动画本身就是追踪源** —— 上表前两行（5 个在 `LoadContent`、5 个在构造函数）都是「只有创建了那个 exe 才会点火」。因此 `noshow` 模式**根本不会**被它们点燃：那些 exe 从不创建，`Show` 也不被调用。`noshow` 更安静不只是「没动画」。详见 §7.1。

---

## 2. 第 1 套：可见倒计时 `TraceTracker`

文件：`decompiled/game-proj/Hacknet/TraceTracker.cs`（135 行）。

### 2.1 `start()`：不叠加、不续期

```csharp
// TraceTracker.cs:102-114
public void start(float t)
{
    if (!active)                       // ← 已激活时直接忽略
    {
        trackSpeedFactor = 1f;
        startingTimer = t;
        timer = t;
        active = true;
        os.warningFlash();
        target = ((os.connectedComp == null) ? os.thisComputer : os.connectedComp);
    }
}
```

第二次点火**不会**把 90 秒续成 180 秒，也不会刷新余量。`target` 在点火那一刻锁定 —— 之后换连接不改变它，而下面这条校验会立刻终止追踪。

### 2.2 `Update()`：三层判断

```csharp
// TraceTracker.cs:61-91（结构重排，语义逐字）
if (!active) return;                                              // ① 未激活零开销

if (os.connectedComp == null || !os.connectedComp.ip.Equals(target.ip))
{
    active = false;                                               // ② 断了 / 换台 → 立即中止
    if (timer < 0.5f) AchievementsManager.Unlock("trace_close");  // 差一点被追到 → 成就
}
else if (!flag)                                                   // ③ 未被冻结才扣时
{
    float num = (Settings.AllTraceTimeSlowed ? 0.055f : 1f);
    timer -= t * trackSpeedFactor * num;
    if (timer <= 0f) { timer = 0f; active = false; os.timerExpired(); }
}
```

**这是反追踪的第一原理**：追踪只在「你还连着那台机器」时推进。`dc` 即中止，不需要任何道具、任何权限。

`trace_close` 成就只在分支 ② 解锁（余量 < 0.5 秒时断开）—— 走 `stop()` 不算。

### 2.3 冻结：`TraceKill.exe`

```csharp
// TraceTracker.cs:52-60
timeSinceFreezeRequest += t;
if (timeSinceFreezeRequest < 0f) timeSinceFreezeRequest = 1f;
if (timeSinceFreezeRequest < 0.2f) flag = true;   // flag=true → 跳过扣时
```

```csharp
// TraceKillExe.cs:86-94
if (!isExiting && !flag)                          // flag = 正在连 dGibson（剧情特例）
{
    if (os.traceTracker.timeSinceFreezeRequest > 0.25f) traceKillSound.Play();
    os.traceTracker.timeSinceFreezeRequest = 0f;  // ← 每帧续期
}
```

`TraceKill.exe`（RAM **600**，`TraceKillExe.cs:74`；玩家 `/bin` 自带，是原版 12 号破解程序的数据 `PortExploits.cs:138`）每帧把冻结计时器清零 → `flag` 恒真 → 倒计时**停走**。

它是 GUI 程序，必须逐帧续期才能维持 `SUPPRESSION ACTIVE`；**一关就恢复走表**（余量原样保留）。

### 2.4 降速：`SignalScramble` / `DLCTraceSlower`（DLC）

```csharp
// DLCTraceSlower.cs:69-77
Computer computer = ((os.connectedComp == null) ? os.thisComputer : os.connectedComp);
bool flag = !isExiting && computer.PlayerHasAdminPermissions();
float point = ((float)ramCost - 50f) / 550f;
if (flag)
{
    float val = 1f - Utils.QuadraticOutCurve(point);
    os.traceTracker.trackSpeedFactor = Math.Min(os.traceTracker.trackSpeedFactor, val);
}
```

- `ramCost` 从 50 涨到 600（约 2.75 秒，`RAM_CHANGE_PS = 200f`），速度因子从 ~1 降到 ~0。
- 额外要求：**当前连接机对你有 admin 权限**（`PlayerHasAdminPermissions()`），否则显示 `Administrator Access Required`。
- `Killed()` / `Completed()` / 点 EXIT 都会把 `trackSpeedFactor` 复位成 1（`:79-89`、`:160`）。

### 2.5 归零之后：`os.timerExpired()`

```csharp
// OS.cs:1433-1452
public void timerExpired()
{
    if (traceCompleteOverrideAction != null) { traceCompleteOverrideAction(); return; }   // DLC 剧情接管
    if (connectedComp != null && connectedComp.admin != null)
        connectedComp.admin.traceEjectionDetected(connectedComp, this);
    if (Flags.HasFlag("CSEC_Member")) TraceDangerSequence.BeginTraceDangerSequence();
    else thisComputer.crash(thisComputer.ip);
}
```

| 分支 | 后果 |
|---|---|
| `traceCompleteOverrideAction` 非空 | DLC 剧情接管（Kaguya Trials、`DLCIntroExe.cs:143-148` / `:518`） |
| `CSEC_Member` 标志（主线） | `TraceDangerSequence`：强制 `dc`、锁终端、播 `Music/Traced`；13.9 秒警告屏 → **130 秒倒计时**要求去 ISP 改 IP；`thisComputerIPReset()` 成功则播 `DISCONNECTED / IP Address successfully reset / Foreign trace averted`，10 秒后重启；**超时 `Game1.Exit()`**（`TraceDangerSequence.cs:29`、`:149-157`） |
| 其它（支线） | `thisComputer.crash(thisComputer.ip)`，正常崩机流程 |

主线那条流程是**一次性、不可延后**的（警告屏原文：*THIS IS NOT REPEATABLE AND CANNOT BE DELAYED*），且必须在**当前 ISP 的路由服务器**上操作。

### 2.6 显示

`Draw`（`:122-133`）：左下角画 `TRACE :`（缩到 0.3 倍）+ `timer / startingTimer * 100` 两位百分比。蜂鸣分档（`:92-99`）：余量 `< 15%` 每 1 单位响一次、`< 45%` 每 5、其余每 10，并伴随 `os.warningFlash()`。

### 2.7 主动点燃自己：`SecurityTracer.exe`

```csharp
// SecurityTraceExe.cs:17
os.traceTracker.start(30f);   // 构造即点燃 30 秒
```

玩家 `/bin` 自带（`NetworkMap.cs:351` 写入 `SecurityTracer.exe`），`launchExecutable` 有 case（`OS.cs:2023-2025`）。`Killed()` 时 `stop()`，但连着 `dGibson` 时不 stop（剧情特例，`:24`）。用途：主动给自己上 30 秒追踪。

---

## 3. 第 2 套：看不见的脱机追踪 `TrackersInProgress`

这才是「跑完断开后被反打」的元凶。

### 3.1 生成：只在断线那一帧

```csharp
// OS.cs:812-822（OS.Update 内）
if (connectedComp == null)
{
    if (connectedIPLastFrame != null) handleDisconnection();
}
else if (connectedIPLastFrame != connectedComp.ip) handleDisconnection();
```

```csharp
// OS.cs:944-960
public void handleDisconnection()
{
    Computer computer = Programs.getComputer(this, connectedIPLastFrame);
    if (computer != null)
    {
        computer.admin?.disconnectionDetected(computer, this);                    // ← 第 3 套：反扑
        if (computer.HasTracker && TrackerCompleteSequence.CompShouldStartTrackerFromLogs(this, computer))
        {
            float timeLeft = TrackerCompleteSequence.MinTrackTime
                           + Utils.randm(TrackerCompleteSequence.MaxTrackTime - TrackerCompleteSequence.MinTrackTime);
            TrackersInProgress.Add(new TrackerDetail { comp = computer, timeLeft = timeLeft });
        }
    }
}
```

两个条件：

1. **`computer.HasTracker`** —— 存档 XML 的 `tracker="true"` 属性（`ComputerLoader.cs:506`；写回见 `Computer.cs:916`）。地图生成器不给普通机器这个标志，它是**手工标定的「会反追踪」机器**。
2. **`CompShouldStartTrackerFromLogs`** —— 目标 `/log` 里**存在任一行同时含你的 IP 与 `FileCopied` / `FileDeleted` / `FileMoved`**：

```csharp
// TrackerCompleteSequence.cs:30-47
for (int i = 0; i < folder.files.Count; i++)
{
    string data = folder.files[i].data;
    if (data.Contains(targetIP)
        && (data.Contains("FileCopied") || data.Contains("FileDeleted") || data.Contains("FileMoved")))
        return true;
}
```

时间窗：`MinTrackTime = 10f`、`MaxTrackTime = 20f`（`TrackerCompleteSequence.cs:5-7`），随机取。

> **只有这三种动词算数。** `openPort` 写的 `"<ip> Opened Port#22"`（`Computer.cs:767`）、`"<ip> Disconnected"`（`:726`）、`"<ip> Became Admin"`（`:741`）**都不触发**。
> 所以「只连上去看一眼」通常没事，「拷走了文件」必然出事。

### 3.2 走表：连着它就作废

```csharp
// OS.cs:823-839
for (int i = 0; i < TrackersInProgress.Count; i++)
{
    if (connectedComp != null && connectedComp == TrackersInProgress[i].comp)
    {
        TrackersInProgress.RemoveAt(i);        // ← 直接移除，不是暂停
        i--;
        continue;
    }
    TrackerDetail value = TrackersInProgress[i];
    value.timeLeft -= num;
    if (value.timeLeft <= 0f) { TrackerCompleteSequence.TrackComplete(this, value.comp); break; }
    TrackersInProgress[i] = value;
}
```

**重连被追踪的那台机器 = 取消它尚未到期的脱机追踪。** 这是游戏自带的逃生通道，原生**零提示、零教程**。断开后它重新走表（若 /log 里那几行还在，下次断开又会排一条）。

`TrackerDetail` 是 `OS` 的内嵌结构体（`OS.cs:27-32`）：`{ Computer comp; float timeLeft; }`。

### 3.3 归零：`TrackComplete` 干了三件事

```csharp
// TrackerCompleteSequence.cs:13-28
public static void TrackComplete(object osobj, Computer source)
{
    OS oS = (OS)osobj;
    oS.TrackersInProgress.Clear();                                       // ① 清空全部待执行追踪
    Folder folder = source.files.root.searchForFolder("log");
    for (int i = 0; i < folder.files.Count; i++)                         // ② 删掉证据行
    {
        string data = folder.files[i].data;
        if (data.Contains(oS.thisComputer.ip)
            && (data.Contains("FileCopied") || data.Contains("FileDeleted") || data.Contains("FileMoved")))
        { folder.files.RemoveAt(i); i--; }
    }
    HackerScriptExecuter.runScript("HackerScripts/TrackSequence.txt", oS, source.ip);   // ③ 反打
}
```

第 ③ 步跑的脚本就在游戏 Content 里，全文如下（`Content/HackerScripts/TrackSequence.txt`，逐字）：

```
config playerComp [SOURCE_COMP] 0.2 $#%#$
connect $#%#$
delay 2 $#%#$
openPort 21 $#%#$
delay 0.9 $#%#$
openPort 22 $#%#$
delay 1.1 $#%#$
openPort 25 $#%#$
delay 0.8 $#%#$
openPort 80 $#%#$
flash $#%#$
delay 0.1 $#%#$
flash $#%#$
flash $#%#$
flash $#%#$
flash $#%#$
flash $#%#$
delay 0.5 $#%#$
trackseq $#%#$
forkbomb $#%#$
delay 0.5 $#%#$
disconnect $#%#$
```

逐句含义（`HackerScriptExecuter.cs`）：

| 行 | 做了什么 |
|---|---|
| `connect` | 从**玩家机视角**连到源机（`[SOURCE_COMP]` 被替换成源机 IP） |
| `openPort 21/22/25/80` | **反向破解你的四个端口**，日志写到你机器上 |
| `flash` × 6 | 屏幕红闪（`case "flash"`，`:335-340`） |
| `trackseq` | `TrackerCompleteSequence.FlagNextForkbombCompletionToTrace(源机IP)`（`:316-329`） |
| `forkbomb` | `Multiplayer.parseInputMessage("eForkBomb", …)`（`:155-157`）→ `launchExecutable("forkbomb")`，门禁 `hasConnectionPermission(admin: true)`（`OS.cs:1943-1945`）。源机此刻是你的 admin，**门禁通过** → `ForkBombExe` 崩你机器 |
| `disconnect` | `computer.disconnecting(...)`（`:158-160`） |

即：**被反打 = 对方连上你、反向开端口、闪屏、扔 forkbomb 崩机**，全程 5~6 秒，终端里能看到那几行 `Opened Port#`。

### 3.4 相关辅助接口

| 接口 | 作用 |
|---|---|
| `TrackerCompleteSequence.TriggerETAS(os)`（`:49-53`） | 直接 `os.timerExpired()`（脚本动词 `etas`） |
| `TrackerCompleteSequence.FlagNextForkbombCompletionToTrace(ip)`（`:55-59`） | 给下一次 forkbomb 打标记（脚本动词 `trackseq`） |
| `OS.traceCompleteOverrideAction`（`OS.cs:264`） | 让 DLC 剧情接管倒计时归零的后果 |

---

## 4. 第 3 套：管理员反扑（不是追踪，但同源）

```csharp
// BasicAdministrator.cs:5-29
public override void disconnectionDetected(Computer c, OS os)
{
    double time = 20.0 * Utils.random.NextDouble();
    os.delayer.Post(ActionDelayer.Wait(time), delegate
    {
        if (os.connectedComp == null || os.connectedComp.ip != c.ip)   // 这 20 秒内没重连回来
        {
            for (int i = 0; i < c.ports.Count; i++) c.closePort(c.ports[i], "LOCAL_ADMIN");
            if (ResetsPassword) c.setAdminPassword(PortExploits.getRandomPassword());
            c.adminIP = c.ip;                                          // ← 肉鸡标记当场作废
            if (c.firewall != null) { c.firewall.solved = false; c.firewall.resetSolutionProgress(); }
        }
    });
}
```

- 延迟 **0~20 秒随机**（不是固定值）。
- 只在这 20 秒内**没重新连上**该机时才执行；重连即取消（与第 2 套同一形状）。
- 防火墙被 `solved = false` 且 `analysisPasses = 0`（`Firewall.cs:96-99`）—— 分析进度清零。

| 变体 | 行为 |
|---|---|
| `BasicAdministrator` | 延迟 0~20 秒 |
| `FastBasicAdministrator` | **立即**关端口 + 重置防火墙 |
| `FastProgressOnlyAdministrator` | 立即；且 `traceEjectionDetected` 里再调一次 `disconnectionDetected`（被追踪翻车时双杀） |

**唯一的对抗手段**：断开前把 `computer.admin = null` —— 之后 `handleDisconnection` 里 `computer.admin?.disconnectionDetected` 整个短路。

---

## 5. 反追踪手段全表（按代价排序）

| 手段 | 作用于 | 机制 | 代价 |
|---|---|---|---|
| **断开连接**（`dc`） | 第 1 套 | `Update` 的 IP 校验 → `active = false`（`TraceTracker.cs:65-80`） | 写一条 `Disconnected` 到目标 `/log`；触发第 3 套反扑 |
| **不断开** | 第 2 套 | 脱机追踪**只在断线时生成**，不断就不会被排 | 无（但连接会一直被追踪目标锁着） |
| **换 IP**（ISP `Assign New IP`） | 第 1、2 套 | 判据全是「含玩家 IP 的行」→ 换掉即全部落空；`thisComputerIPReset()` 还直接 `traceTracker.active = false`（`OS.cs:1791-1802`） | 需跑到 ISP 服务器；打断「IP 不变」类任务链 |
| **删 `/log` 里含自己 IP 的副本/删除/移动行** | 第 2 套 | 让 `CompShouldStartTrackerFromLogs` 恒假 | 需连着目标（`rm` 作用域是 `connectedComp`） |
| **重连被追踪的那台** | 第 2 套 | `OS.Update` 直接 `RemoveAt` 该条目（`OS.cs:825-830`） | 逆直觉、原生零提示 |
| **`TraceKill.exe`** | 第 1 套 | 每帧 `timeSinceFreezeRequest = 0` → 冻结 | RAM 600；一关就恢复 |
| **`SignalScramble`**（DLC） | 第 1 套 | `trackSpeedFactor` 降到 ~0 | RAM 50→600；需当前机对你有 admin |
| **CSEC 主线撤离流程** | 已经归零 | 130 秒内到 ISP 改 IP | 一次性、不可延后 |

`Settings.AllTraceTimeSlowed`（`Settings.cs:29`）是全局 1/18 减速开关，缺省 `false`，只在调试/特定模式下打开。

---

## 6. 一轮完整时序（把三套串起来）

```
你连上 sec=5 的目标（traceTime = 75s）
   │
   ├─ 敲 sshcrack 22 → SSHCrackExe.LoadContent → hostileActionTaken()
   │                      └→ traceTracker.start(75)        ← 左下角开始跳数
   │
   ├─ 拷贝文件 → 目标 /log 写入 "FileCopied: by <你的IP>"
   │
   ├─ 敲 dc ──────┬→ handleDisconnection()
   │              │    ├→ admin.disconnectionDetected()    ← 0~20s 后关端口 / 作废 adminIP
   │              │    └→ HasTracker && 有含 IP 的 Copied 行
   │              │         → TrackersInProgress += {comp, 10~20s}
   │              └→ TraceTracker 下一帧发现 target ≠ connectedComp → active = false  ← 倒计时中止
   │
   └─ 10~20 秒后 TrackersInProgress 归零 → TrackComplete()
         └→ 跑 TrackSequence.txt：反向连你、开 21/22/25/80、闪屏 ×6、forkbomb 崩机
```

**倒计时与脱机追踪方向相反**：连着才推进 / 断开才生成。所以「尽量别断开」躲第 2 套，「尽快断开」躲第 1 套 —— 二者不可兼得。

---

## 7. AutoHack 的对应实现

| 游戏机制 | mod 实现 | 位置 |
|---|---|---|
| `TraceTracker` | `HackEngine.KillTrace(os)` → `traceTracker.stop()` | `HackEngine.Counter.cs:94-103` |
| `TrackersInProgress` | `TraceTools.Run` 里 `trackers.Clear()` | `TraceTools.cs:58-60` |
| 两套一起清 | 面板 `ANTI-TRACE` 按钮 / `autohack trace`（立即）/ `HackRun.Finish`（每轮无条件） | `TraceTools.cs`、`HackRun.cs:408` |
| 追踪链送来的 forkbomb | `HackEngine.KillForkBombs(os)`：`Killed()` + `needsRemoval`，由游戏线程当帧移除 | `HackEngine.Counter.cs` |
| 脱机追踪的复发源 | `HackStepKind.CleanLogs` → `HackEngine.WipeTraces`，只删含玩家 IP 的行 | `HackRun.Plan.cs:161`、`HackEngine.Wipe.cs` |
| 管理员反扑 | `SuppressCounterattack`：`computer.admin = null` | `HackEngine.Counter.cs` |
| 换 IP | `IpTools.Reset`：原生三步 + 归属迁移 + 重建 Pathfinder 查找表 | `IpTools.cs` |
| 演出动画点燃追踪 | `NativeExes.Tick` 每帧 `KillTrace` | `NativeExes.cs:539-542` |

`KillTrace` 刻意**不照抄** `TraceKillExe` 的逐帧续期：`stop()` 是彻底终止，`TraceTracker.Update` 开头的 `if (!active) return;` 让后续帧零开销（`HackEngine.Counter.cs:80-92` 已说明）。代价是不走 `trace_close` 成就分支。

### 7.1 「演出动画」与追踪的关系（补 §1.3）

`noshow` 模式下**不创建**那 9 个原生破解 exe（`NativeExes.Show` 根本不被调用），因此 `hostileActionTaken` 也不会被它们的构造/`LoadContent` 触发 —— **`noshow` 比 `show` 更安静**，不只是「没动画」。

`show` 模式下，动画在**入队那一刻**（构造函数）或**挂载那一刻**（`LoadContent`）点燃追踪，而 `NativeExes.Tick` 每帧扑一次；判据必须覆盖「已挂上但队列已空」的那一刻（见 `NativeExes.cs:532-542`）。

---

## 8. 常见误解

| 误解 | 事实 |
|---|---|
| 「清掉倒计时就没事了」 | 第 2 套完全不受 `traceTracker` 影响；复发源是目标 `/log` |
| 「重连会冻结脱机追踪」 | 不是冻结，是**移除**（`OS.cs:825-830`）；之后若日志还在，下次断开重排 |
| 「断得越快越安全」 | 断得越快越躲第 1 套，但**第 2、3 套正是断开才发作** |
| 「`probe` 会点燃追踪」 | 不会。`analyze` 会 |
| 「`TraceKill.exe` 能永久免疫」 | 它是逐帧续期的冻结，关掉即恢复；RAM 600 |
| 「换 IP 就清空了所有痕迹」 | 只让**已有日志**失去指向；你机上自己的 `/log` 条目仍在，仍会被判据读到（如果之后又用旧 IP） |
| 「`adminIP` 只是标记」 | 它是第 3 套的靶子：0~20 秒后无条件改回机器自己 |

---

## 9. 源码索引

| 文件 | 行 | 内容 |
|---|---|---|
| `Hacknet/TraceTracker.cs` | 49-100 | `Update`：IP 校验、冻结、扣时、蜂鸣分档 |
| | 102-120 | `start` / `stop` |
| | 122-133 | 左下角绘制 |
| `Hacknet/Computer.cs` | 43 / 118 / 222-225 | `traceTime` 字段、缺省 -1、按 security 赋值 |
| | 294-308 | `hostileActionTaken()` |
| | 722-727 | `disconnecting` 写 log |
| | 751-772 / 774+ | `openPort` / `closePort` 写 log |
| | 916 | 存档写 `tracker="true"` |
| `Hacknet/OS.cs` | 27-32 | `TrackerDetail` 结构体 |
| | 256 | `TrackersInProgress` 字段 |
| | 812-822 | `handleDisconnection` 的触发条件 |
| | 823-839 | 脱机追踪走表（连上即移除） |
| | 944-960 | `handleDisconnection` 本体 |
| | 1433-1452 | `timerExpired()` |
| | 1791-1802 | `thisComputerIPReset()` |
| `Hacknet/TrackerCompleteSequence.cs` | 5-7 | Min/MaxTrackTime = 10/20 |
| | 13-28 | `TrackComplete` |
| | 30-47 | `CompShouldStartTrackerFromLogs` |
| | 49-59 | `TriggerETAS` / `FlagNextForkbombCompletionToTrace` |
| `Hacknet/TraceKillExe.cs` | 70-107 | 构造（RAM 600）与冻结续期 |
| `Hacknet/DLCTraceSlower.cs` | 69-89 | 降速与复位 |
| `Hacknet/BasicAdministrator.cs` | 5-29 | 管理员反扑 |
| `Hacknet/ISPDaemon.cs` | 122-142 | `Assign New IP` |
| `Hacknet/SecurityTraceExe.cs` | 13-28 | 主动点燃 30 秒 |
| `Hacknet/HackerScriptExecuter.cs` | 155-160 / 316-340 | `forkbomb` / `disconnect` / `trackseq` / `etas` / `flash` |
| `Content/HackerScripts/TrackSequence.txt` | 全部 | 反打脚本原文 |

---

## 10. 复现要点

想亲眼验证这套机制，最快路径（不需要改存档）：

1. 找一台 `security >= 4`、带 `tracker="true"` 的机器（`autohack ports` 看端口表，`probe` 看 `TRACE`）。
2. 连上、`analyze`（不用破端口）→ 左下角出现 `TRACE :` 倒计时。
3. 立刻 `dc` → 倒计时消失（第 1 套中止）；`autohack trace` 回显 `no active timer` —— 证明它确实已经停了。
4. 重新连上、`scp` 拷一个文件下来（写入 `FileCopied`）、再 `dc` → 10~20 秒内屏幕红闪、终端涌出 `Opened Port#21/22/25/80`、随后 forkbomb 崩机（第 2 套）。
5. 复现前先 `autohack trace`：它会把两套一起清，并回显 `N tracker(s) stopped`。

> 想量化验证而不进游戏：读存档 XML（`Accounts/save_*.xml`），统计 `tracker="true"` 的机器数与各自的 `traceTime`。
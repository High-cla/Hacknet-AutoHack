# AutoHack 工具集设计（DEC / 内存转储 / 程序补全 / 自身加固）

日期：2026-09-26 · 状态：待用户确认 · 验收模式：correct

## 一、目的

为【已通关 Hacknet、想继续折腾存档与游戏内数据的玩家】在【不想退出游戏改存档、也不想手输 DEC 密码】的处境下，
达成【在游戏内一键完成 DEC 多层解密、内存转储查看导出、/bin 程序补全、自身机器加固】。

可观测结果：终端出现对应回显、玩家 `/home` 出现产物文件、面板出现对应按钮。

## 二、需求（8 条）

1. **DEC 解密**：递归多层（正文含 `#DEC_ENC` 就再解一层）+ 免密码反推 passcode + 批量；默认开启破解
2. **内存转储**：查看当前节点 Memory 紧凑格式；导出 `.mem` 到玩家 `/home/MemDumps`；解析 `.mem` 并识别内嵌 DEC
3. **一键全程序获取**：遍历 `PortExploits.cracks` 补全玩家 `/bin` 缺失的 `.exe`（含 `WoWHack.exe` / `confloodEOS.exe` / `GitTunnel.exe` 等废案）
4. **无懈可击**：加固玩家自己的机器（`traceTime=1`、`portsNeededForCrack` 极大、proxy 永不失效、防火墙随机 12 位、端口全开）
5. **入口**：命令 `autohack dec|mem|exes|unbreakable` + 面板按钮，双入口单实现；面板按钮单击立即执行、无二次确认
6. **作用域**：`dec`/`mem` 默认当前连接节点，加 `allnodes` 切全网；`exes`/`unbreakable` 天然只针对玩家自己
7. **绝不混进 `autohack run` 的自动入侵流程**
8. **全部走游戏 API**（`FileEncrypter` / `MemoryContents` / `PortExploits.crackExeData` / `PortState`），不重写密码学

## 三、非目标（5 条）

1. 不做存档文件（`save_*.xml`）的直接读写 —— 那是 `Hacknet_Save_Editor` 的领域，本插件只在游戏运行时操作内存对象
2. 不重写游戏已有的密码学（`FileEncrypter`）或 exe 数据生成（`PortExploits.crackExeData` 已在游戏启动时算好）
3. **不抄 `Hacknet_Save_Editor` 的代码** —— 该仓库是 GPL-3.0，本仓库是 MIT，只可参考机制不可复制代码
4. 不给 `dec`/`exes`/`unbreakable` 加二次确认弹窗（用户明确要求「点击就启动」）
5. 不改 `autohack run` 既有行为

## 四、假设（4 条）

1. 「点击就启动」= 面板按钮单击即执行，不加确认弹窗；`unbreakable` 仅在按钮文案上标注不可逆
2. 「默认打开」= 四个功能默认可用且 DEC 破解默认开启，不藏在参数后面
3. 面板现有 396px 宽、`OptionsBlockHeight = 274`，需新增 TOOLS 区并同步改 `BodyHeight` 与 `DrawOptions` 推进量
   （v1.5.0 曾因 `BodyHeight` 漏算导致面板高度错）
4. `dec` 解密结果写入玩家 `/home`（参考实现是「保存至玩家 `/home` 或导出本地」，本插件只做前者 —— 导出本地属存档编辑器领域）

## 五、游戏侧依据（已逐条核实）

### 5.1 DEC：`Hacknet.FileEncrypter`（`decompiled/game-proj/Hacknet/FileEncrypter.cs`）

加密原语（`:35-44`）：

```csharp
public static string Encrypt(string data, ushort passcode)
{
    int num = data[i] * 1822 + 32767 + passcode;   // :40
}
public static string Decrypt(string data, ushort passcode)
{
    int num3 = num - 32767 - passcode;             // :54
    num3 /= 1822;                                  // :55
}
```

文件结构（`EncryptString` `:10-28`）：

```
#DEC_ENC::<加密的 header>::<加密的 ipLink>::<用真实 passcode 加密的 "ENCODED">[::<加密的扩展名>]
<用真实 passcode 加密的正文>
```

`DecryptString(data, pass)` 返回 `string[6]`（`:98-104`）：`[0]=header`、`[1]=ip`、`[2]=正文`、`[3]=扩展名`、`[4]="1"/"0"` 是否成功、`[5]` 解密出的标记。

**免密码反推**：头部第 3 段加密的固定串是 `"ENCODED"`，其首字符 `'E'`：

```
MAGIC    = 'E' * 1822 + 32767 = 69 * 1822 + 32767 = 158485
passcode = (首个密文数字 - MAGIC) & 0xFFFF
```

O(1) 一次命中，再用整段验证解出 `"ENCODED"` 即可确认。**不是暴力遍历** —— 是密文结构给出的确定性反推。

多层递归：正文里含 `#DEC_ENC` 就再解一层（`:19` 的固定头）。

### 5.2 内存转储：`Hacknet.MemoryContents`

- 字段：`Computer.Memory`（`Computer.cs:107`），序列化 `Memory.GetSaveString()`（`:951-953`），读档 `MemoryContents.Deserialize(reader)`（`:1131-1132`）
- 常量：`EncryptionPass = "19474-217316293"`、`FileHeader = "MEMORY_DUMP : FORMAT v1.22 ----------\n\n"`
- API：`GetCompactSaveString()`、`GetEncodedFileString()`、`MemoryContents.GetMemoryFromEncodedFileString(data)`
- 数据结构：`DataBlocks` / `CommandsRun` / `FileFragments` / `Images` 四个列表

### 5.3 exe 数据：`Hacknet.PortExploits`

游戏启动 `populate()`（`PortExploits.cs:38`）时就用固定种子算好了每个 exe 的 500 字符内容：

```csharp
Random random = new Random(17021990);      // :47
MSRandom rng  = new MSRandom(17021990);    // :48
crackExeData[22] = Computer.generateBinaryString(500, rng);   // :56
```

`cracks`（port → 文件名）与 `crackExeData`（port → 内容）两张表即为唯一数据源。
废案程序：`WoWHack.exe`（`:82`）、`confloodEOS.exe`（`:103`）、`GitTunnel.exe`（`:214`）。

> 参考实现的 `SubtractiveRNG` / `gen_bin` 是这套生成的 Python 复刻 —— 游戏里本来就有，直接取用即可。

### 5.4 无懈可击：存档字段 → 游戏字段映射

| 存档属性（`Computer.cs:921`） | 游戏字段 | 目标值 | 行号 |
|---|---|---|---|
| `traceTime` | `traceTime` | `1f` | `:43` |
| `portsToCrack` | `portsNeededForCrack` | `9999998` | `:45` |
| `proxyTime` | `hasProxy` + `startingOverloadTicks` + `proxyActive` | `true` / `9999998f` / `true` | `:83`/`:87`/`:89` |
| `firewall.solution` | `Firewall.solution`（+ `complexity`、`additionalDelay` 置 0） | 随机 12 位字母数字 | `Firewall.cs:20`/`:24`/`:28` |
| `portsOpen` | `PortState.Cracked`（Pathfinder 下 vanilla `portsOpen` 恒 0） | 全开 | — |

**proxy 必须同时置三个字段**：`startingOverloadTicks > 0f` 才写进存档（`:921`），
而 `hasProxy` 决定写的是 `startingOverloadTicks` 还是 `-1`。

## 六、架构

新增一个工具内核类，与 `HackEngine` / `HackRun` 平级，四个功能各自一个方法组；
命令分发在 `AutoHackPlugin` 现有的 `args[0] == "run"` 分支旁新增；面板在 RUN 按钮下方新增 TOOLS 区。

- 内核不依赖 `HackRun` 的步骤机 —— 这些是一次性动作，不是入侵流程
- 命令与面板走同一实现（双入口单实现，不写两遍）
- 面板 TOOLS 区高度按按钮数参数化，避免每次加按钮都手改三处布局数学

## 七、风险

| 风险 | 处置 |
|---|---|
| 反推公式错 | 反推结果必须经游戏 `FileEncrypter.Decrypt(data, passcode)` 反向验证；对照空密码与长密码两种样本 |
| 面板布局数学漏算 | `OptionsBlockHeight` / `BodyHeight` / `DrawOptions` 推进量三处同步；对照 v1.5.0 的旧缺陷 |
| `unbreakable` 不可逆 | 按钮文案标注；执行前后各打印一行关键字段值供对照 |
| 合规 | 只参考 GPL 仓库的机制，代码全部自写；在 RESEARCH 中写明 |

## 八、验证方式

沿用现行规矩：**只看产物 MD5，不做反编译核对**。

1. 构建须 0 警告 0 错误
2. 记 `md5sum` + 字节数，与上一版比对
3. 四项功能各自在游戏里跑一次，看终端输出与 `/home` 产物
4. DEC 用存档里现成的 `.dec` 文件验证层数与结果

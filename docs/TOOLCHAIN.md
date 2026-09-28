# 工具链

本仓库日常开发用到的本地工具链：**怎么选**、**怎么用**、**哪些坑踩过**。
每条坑都带实测命令，可自行复跑验证。

## 分工

| 需求 | 工具 | 入口 |
|---|---|---|
| 命令执行 / 文件读写搜索 | fastctx | `mcp__fastctx__run` / `inspect_local_file` / `grep` / `glob` / `replace` |
| 符号定位、调用链、影响面 | codebase-memory | `mcp__codebase_memory__*` |
| 找形状、批量结构审计 | ast-grep | `sg` CLI |
| 纯文本检索 | ripgrep | `rg` |
| 多步组合 | PTC | `run_code` 里写 TypeScript 一次跑完 |

三条原则：

1. **语义 → codebase-memory，形状 → ast-grep，文本 → rg。** 三者不可互替：
   rg 找字面量最快；ast-grep 能表达「带某修饰符的类声明」而 rg 不能；
   codebase-memory 能回答「谁调用它」而前两者都不能。
2. **多步无依赖 → 一个 `run_code` 程序一次跑完**，不要一步一问。
3. **结论必须复跑验证**：`||` 兜底文案、`grep -c` 计数都会伪造「结论」，
   见下文坑 1 与坑 4。

## ast-grep（`sg`）

### 语言名

`-l csharp` 与 `-l cs` **都合法且等价**（多 pattern 交叉实测，输出逐字节相同）。
非法名会明确报错，不会静默：

```bash
sg run -p 'class $N' -l boguslang src/AutoHack/RefusedWhitelist.cs
# error: invalid value 'boguslang' for '--lang <LANG>': boguslang is not supported!
```

### pattern 匹配的是完整 AST 节点，不是文本

这是本仓库最容易踩错的地方。**pattern 必须写出目标节点的全部修饰符与前置特性**，
少写一个就落空：

```bash
# RefusedWhitelist.cs 第 25 行是 [HarmonyPatch]，第 26 行是 internal static class RefusedWhitelist
sg run -p 'class $N'                                 -l csharp src/AutoHack/RefusedWhitelist.cs  # 1（class 关键字节点）
sg run -p 'static class $N'                          -l csharp src/AutoHack/RefusedWhitelist.cs  # 0
sg run -p 'internal static class $N'                 -l csharp src/AutoHack/RefusedWhitelist.cs  # 0 ← 有特性，落空
sg run -p '[HarmonyPatch] internal static class $N'  -l csharp src/AutoHack/RefusedWhitelist.cs  # 1 ← 命中
```

对照无特性的 `BootBoost.cs`：`internal static class $N` 命中，`class $N` 只命中关键字。

用 `--debug-query=pattern` 看 pattern 被解析成什么节点，是排错第一步：

```bash
sg run -p 'class $N'        -l csharp --debug-query=pattern src/AutoHack/BootBoost.cs  # -> class            ← 只是关键字
sg run -p 'static class $N' -l csharp --debug-query=pattern src/AutoHack/BootBoost.cs  # -> class_declaration
```

### 用规则式（`kind`）绕开修饰符问题

要「所有类 / 所有方法 / 所有特性」时别拼 pattern，直接按节点类型：

```bash
sg scan --inline-rules 'id: c
language: csharp
rule:
  kind: class_declaration' src/AutoHack --json | jq length      # 29

sg scan --inline-rules 'id: m
language: csharp
rule:
  kind: method_declaration' src/AutoHack --json | jq length     # 184

sg scan --inline-rules 'id: a
language: csharp
rule:
  kind: attribute
  regex: ^Harmony' src/AutoHack --json | jq -r '[.[].file]|unique|length'  # 6
```

**特性节点用 `kind: attribute`，不要用 `pattern '[HarmonyPostfix]'`** —— 后者恒 0 命中。

### 计数：`--json | jq length` 而非 `grep -c`

默认输出是**多行上下文**（一个匹配展开成整节点），`grep -c` 数的是行不是匹配：

```bash
sg run -p 'internal static class $N' -l csharp src/AutoHack/BootBoost.cs | grep -c .          # 10（行）
sg run -p 'internal static class $N' -l csharp src/AutoHack/BootBoost.cs --json | jq length   # 1（匹配）
```

### 路径与忽略规则

`--rule` 的文件路径**只接受相对路径或 Windows 形态**，MSYS 形态会失败：

```bash
sg scan --rule .tmp/r.yml src/AutoHack/RefusedWhitelist.cs                      # OK
sg scan --rule D:/git/HacknetMod/.tmp/r.yml src/AutoHack/RefusedWhitelist.cs    # OK
sg scan --rule /d/git/HacknetMod/.tmp/r.yml src/AutoHack/RefusedWhitelist.cs    # Error: Cannot read rule
sg scan --rule /tmp/r.yml src/AutoHack/RefusedWhitelist.cs                      # Error: Cannot read rule
```

根因是 MSYS 路径不会被转成 Windows 形态（`cygpath -w /tmp/r.yml` → `C:\Users\11\AppData\Local\Temp\r.yml`）。
**在 `run_code` 里靠 shell 变量或 `$(cygpath ...)` 传路径同样失败**（不会展开）——用相对路径最稳。

忽略规则：`sg` **默认尊重 `.gitignore`**，目录扫描跳过被忽略的文件。沙盒实测
（`.gitignore` 含 `ignored_sub/`，其中 `b.cs` 定义 `class Hidden`）：

```bash
sg run -p 'class $N' -l csharp .                   # 只出 Visible
sg run -p 'class $N' -l csharp ignored_sub         # 出 Hidden ← 显式指名即绕过忽略
sg run -p 'class $N' -l csharp --no-ignore vcs .   # 出 Hidden + Visible ← flag 有效
```

⚠ **别用 `git check-ignore` 推断 `sg` 的行为。** 本仓库 `decompiled/` 被 `.gitignore:14` 排除，
但 `sg` 扫目录仍能命中 462 个文件 —— 它不在 `sg` 实际读取的忽略源里，
`--no-ignore vcs` 加与不加结果相同。要扫 `decompiled/` 直接扫，不行再显式给文件。

### 稳定性

串行 500 次 + 并行 8×20 次目录扫描，退出码全部为 0。
但曾观测到**一次**偶发崩溃（`fatal runtime error: I/O error: operation failed to complete synchronously`，
退出码 127、输出截断，约 1/10 批次的早期小样本中出现过一次，之后 660 次未复现）。
**批量脚本仍应检查退出码**，避免把崩溃当成「无匹配」。

## codebase-memory

索引只覆盖**本插件源码**（`.cbmignore` 与 `.gitignore` 共同决定），
`decompiled/`、`upstream/`、`docs/` 不进索引 —— 这是有意为之，理由见 `.cbmignore` 内的长注释。
查游戏/框架 API 符号请用 `rg` 直接搜 `decompiled/`。

### 重建

```js
mcp__codebase_memory__index_repository({ repo_path: "D:/git/HacknetMod", mode: "moderate", persistence: true })
```

- `mode="moderate"`（缺省）：保留相似度与语义边，日常用这个
- `mode="fast"`：冒烟用，**没有相似度/语义边**
- `mode="full"`：仅当 moderate 漏掉相关文件时才用，代价高
- `persistence=true` 写出 `.codebase-memory/graph.db.zst`（`.gitignore:23` 忽略，不入库）

重建后核对节点/边数：

```bash
cat .codebase-memory/artifact.json
# {"nodes": 814, "edges": 2252, "commit": "120519d...", "reconcile_basis": "git-clean-head", ...}
```

### 判新鲜度

比对 `artifact.json` 的 `commit` 与当前 HEAD 即可。
**不要用「图里有没有某符号」判断** —— 未入索引的文件在重建前本就不在图里，会误判成过期。
行号是最可靠的交叉验证：

```bash
rg -n -F 'ConnectedPool(OS os)' src/AutoHack/     # HackEngine.cs:518
# 图内对应 qn 的 lines 字段也应是 518-521
```

### 常用查询

```js
search_graph({ project, query: "resolve target pool" })              // 自然语言
search_graph({ project, name_pattern: "^HackScope$" })               // 精确名
trace_path({ project, function_name: "ConnectedPool", direction: "both", depth: 2 })
get_architecture({ project, aspects: ["overview", "hotspots"] })
```

## fastctx

命令执行、文件读写搜索的统一入口。**参数集与内置 bash 不同**：

| | fastctx `run` | 内置 bash |
|---|---|---|
| `command` | ✅ | ✅ |
| `cwd` | ✅ | ✅ |
| `timeout_ms` | ✅ | ✅ |
| `login_shell` | ✅ | ❌ |
| `encoding` | ✅ | ❌ |
| `description` | ❌ | ✅ |
| 后台运行 | 独立工具 `run_background` | 参数 `run_in_background` |

传错参数会**硬报错**（不是忽略）：

```
failed to deserialize parameters: unknown field `description`,
expected one of `command`, `cwd`, `timeout_ms`, `login_shell`, `encoding`
```

## 本仓库的实测坑

| # | 坑 | 事实 |
|---|---|---|
| 1 | 兜底文案伪造结论 | `sg ... || echo 'failed'` 会把**正常的无匹配**（rc=1）报成「工具失败」。判断工具是否报错要看 **stderr 与退出码**，不要看自己写的兜底串 |
| 2 | pattern 缺修饰符/特性 | `internal static class $N` 对**带特性**的类落空；`class $N` 只匹配关键字节点 |
| 3 | 特性用 pattern 匹配 | `[HarmonyPostfix]` 恒 0 命中，须 `kind: attribute` |
| 4 | `grep -c` 当匹配数 | 默认输出多行，须 `--json | jq length` |
| 5 | MSYS 路径传 `--rule` | `/d/...` 与 `/tmp/...` 均报 Cannot read rule；用相对路径或 `D:/...` |
| 6 | 用 `git check-ignore` 推 `sg` 忽略行为 | 两者忽略源不同：`decompiled/` 被 git 忽略却被 `sg` 照常扫（462 文件），`--no-ignore vcs` 无差别。要确认就**直接试扫** |
| 7 | 未验证的「偶发崩溃」写进文档 | 单次 `fatal runtime error` 曾被写成「约 1/10」，复测 660 次 0 复现。**小样本异常不可当规律** |
| 8 | fastctx 传 `description` | 硬报错，见上表 |
| 9 | 用「符号是否存在」判索引新鲜度 | 新文件在重建前不在图里，会误判成过期；用 `artifact.json` 的 `commit` 与 HEAD 比对 |
| 10 | `replace` 的 replacement 含 `$N` | 会被当成捕获组引用而报 `undefined capture group`；整文件重写更省事 |

> 坑 1、6、7 都是**曾经写错的结论**，记录于此以免重犯。早期版本的本文档曾把
> 「`-l cs` 静默失败」「`--no-ignore vcs` 必需」写成坑，实测证明均不成立 ——
> 前者是坑 1 的产物（`cs` 与 `csharp` 等价），后者加与不加结果相同。

## 环境

| 工具 | 版本 |
|---|---|
| node | v24.15.0 |
| dotnet | 10.0.301 |
| ast-grep | 0.42.3 |
| ripgrep | 15.1.0 |
| jq | 1.8.1 |
| gh | 2.96.0 |

- 工作目录 `D:\git\HacknetMod`；**Git Bash 下所有命令都要显式 `cd`**（会话默认目录是 `C:/Users/11`）
- `git.exe` 是原生 Windows 版：**必须 `cd` 后调用，不能用 `git -C /d/...`**（报 cannot change to）
- 本机无 `bc`，计数用 `wc -l` / `jq length`
- `ilspycmd` 需 Windows 形态路径（`D:/steam/...`，非 `/d/steam/...`）
- `rg` 没有 `--include`（那是 `grep` 的参数），用 `-g`

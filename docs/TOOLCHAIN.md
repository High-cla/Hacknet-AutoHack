# 工具链

本仓库日常开发用到的本地工具链：**怎么选**、**怎么用**、**哪些坑踩过**。
每条坑都带实测命令，可自行复跑验证。全部读数于 2026-09-29 在本机复测。

---

## 路径铁律

> **一句话**：`rg` `fd` `sg` `yq` `bat` `tokei` `difft` `delta` `duckdb` `7z` 已被 wrapper 接管，
> **可以直接写 `/d/...`**；`sd`、`ast-grep`（全名）等未被接管的原生二进制**必须写 `D:/...`** 或用相对路径。

### 现状：wrapper 已就位

`~/.bashrc:72` 会 source `~/.dsh/winpath.sh`，后者（`winpath.sh:71`）为下列工具定义了同名 bash 函数：

```
rg  fd  sg  yq  bat  tokei  difft  delta  duckdb  7z
```

实测（`type -t`）：以上 10 个全部是 `function`；`sd` 与 `ast-grep` 是 `file`（**未包装**）。

### 判据：什么样的参数会被转换

`_dsh_is_msys_path`（`winpath.sh:39-53`）要求**三条同时满足**：

| # | 判据 | 实测 |
|---|---|---|
| ① | 形状为 `/<字母>/...` 或 `/<字母>` | `/d/x` ✅、`/tmp/x` ❌、`/zz/x` ❌、`/dd/x` ❌ |
| ② | `/<字母>` 是真实存在的盘符根 | `/d` 存在 ✅ |
| ③ | **该参数在文件系统中真实存在**（`[ -e ]`） | `/d/nonexist` ❌ → 不转换 |

判据 ③ 是刻意的设计：宁可让不存在的路径**原样报错**（可见失败），
也不要静默改写正则（隐蔽错误）。

```bash
rg -c HarmonyPatch /d/git/HacknetMod/src     # ✅ 7 个文件（wrapper 转换）
rg -c HarmonyPatch /d/nope                   # ❌ os error 2（③ 不满足，原样透传）
rg -c -F '/AutoHack/' llms.txt               # ✅ 24（正则形态不满足 ①，原样透传）
```

### 代价一：通配符不满足判据 ③

```bash
rg -c HarmonyPatch '/d/git/HacknetMod/src/**/*.cs'   # ❌ os error 3
rg -c HarmonyPatch 'D:/git/HacknetMod/src/**/*.cs'   # ❌ os error 123
```

通配符不是真实存在的路径 → wrapper 不转换。**要 glob 用 `-g`，别把通配符塞进路径**：

```bash
rg -c -g '*.cs' HarmonyPatch /d/git/HacknetMod/src   # ✅
```

### 代价二：未被包装的工具是另一套规则

| 工具 | 包装 | `/d/...` | `D:/...` | 相对路径 |
|---|---|---|---|---|
| `rg` `fd` `sg` `yq` `bat` `tokei` `difft` `delta` `duckdb` `7z` | ✅ 函数 | ✅ | ✅ | ✅ |
| `sd` | ❌ | ❌ `error: invalid path` | ✅ | ✅ |
| `ast-grep`（**全名**） | ❌ | ❌ `os error 3` | ✅ | ✅ |
| `git` | ❌ | ❌ `git -C /d/...` 报 `fatal: cannot change to` | ✅ `git -C D:/...` | ✅ |
| `gh` `dotnet` `node` | ❌ | ✅（自己处理路径） | ✅ | ✅ |
| `jq` `grep` `sed` `awk` `zstd` `xmllint` `tree` `strings` `bc` `dc` | ❌ | ✅（MSYS 版或内建） | ✅ | ✅ |

⚠ **最容易踩的**：`sg` 与 `ast-grep` 是同一个二进制的两个名字，**但路径规则不同** ——
`sg` 走 wrapper（吃 `/d/`），`ast-grep` 不走（只吃 `D:/`）。用全名时别忘了换形态。

```bash
sg       run -p 'class $N' -l csharp /d/git/HacknetMod/src/AutoHack/BootBoost.cs  # ✅ 命中
ast-grep run -p 'class $N' -l csharp /d/git/HacknetMod/src/AutoHack/BootBoost.cs  # ❌ os error 3
```

### 正则为什么没被误伤：`MSYS2_ARG_CONV_EXCL=*`

本机 `HKCU\Environment` 里设了 **`MSYS2_ARG_CONV_EXCL=*`**（`reg query` 实测 `REG_SZ  *`）。
它全局禁用 MSYS 自身的参数路径转换。

看到「全局禁用路径转换」的第一反应是取消它。**不行** —— 它保护的是**正则参数**：

```bash
MSYS2_ARG_CONV_EXCL='*' python -c "import sys;print(sys.argv[1:])" '^/AutoHack/' '/AutoHack/'
# ['^/AutoHack/', '/AutoHack/']                                          ← 原样

MSYS2_ARG_CONV_EXCL=    python -c "import sys;print(sys.argv[1:])" '^/AutoHack/' '/AutoHack/'
# ['^C:/Program Files/Git/AutoHack/', 'C:/Program Files/Git/AutoHack/']  ← 被毁
```

MSYS 的启发式规则把**任何以 `/` 开头的参数**当路径转换，正则 `/AutoHack/` 于是变成
`C:/Program Files/Git/AutoHack/`。

所以本机是**两层防御**：`MSYS2_ARG_CONV_EXCL=*` 关掉 MSYS 的盲转换，
wrapper 只对**确实存在的路径**做显式转换。实测 wrapper 不误伤正则：

```bash
rg -c -F '/AutoHack/' llms.txt     # 24（正确；若换成裸 MSYS 转换会返回空）
```

⚠ **不能用 `echo` 测参数转换** —— `echo` 是 bash 内建，不走转换。要用外部程序（`python -c`）。

### 手工转换

判据是启发式，不是解析器。要确定性就用 `winpath`：

```bash
source ~/.dsh/winpath.sh
rg -n PAT "$(winpath /c/Users/11/...)"     # 最稳
```

---

## 其他通用陷阱

| 陷阱 | 规避 |
|---|---|
| `run_code` 内无 shell 展开 | 显式写 `C:/...`（路径字面量会被 DSH 翻译，`$VARS` 不会） |
| heredoc 写含换行的 JS | 用 `write` 工具写脚本，不要 heredoc |
| 双引号内 `$$$` 被展开成 PID | 加反斜杠转义，或改用 rule 文件 |
| MSYS `find` 截获 Windows `find /c` | 路径判定用 Node API |
| 信 `mtime` 判新鲜度 | pnpm CAS 保留原 mtime；用 `cmp -s` / `md5sum` |
| JS 的 `String.replace` 替换串含 `$` | 触发 `$&`/`$\`` 等语义；改用 `write` 全量重写 |
| **TS-first 护栏拦新建 `.js`** | 内置 `bash` 与 `write` 新建 `*.js` 被硬拒；`.json/.txt/.yml/.md/.ts/.mts` 放行。**fastctx `run` 不受此约束**（实测 `.js` 建成功）。逃生舱 `DSH_TS_FIRST=off` |
| 往 `/usr/bin` 装东西 | `[ -w /usr/bin ]` 返回真但 `touch` 被拒（`Permission denied`；Unix mode bit 与 Windows ACL 不同步）。一律装 `~/.local/bin` |

**TS-first 护栏原文**（内置工具触发时）：

```
强制 TypeScript 优先：拒绝新建 JS 文件 ".tmp/g.js"（该路径不存在）。
改用 .mts（含 import / top-level await 的 ESM）或 .ts；
（用户侧逃生舱：设置环境变量 DSH_TS_FIRST=off 并重启 DSH。）
```

**fastctx `run` 的参数集与内置 bash 不同**：

| | fastctx `run` | 内置 bash |
|---|---|---|
| `command` | ✅ | ✅ |
| `cwd` | ✅（接受 `/d/...`） | ✅ |
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

---

## 工具链路由

| 需求 | 工具 | 入口 |
|---|---|---|
| 命令执行 / 文件读写搜索 | fastctx | `mcp__fastctx__run` / `inspect_local_file` / `grep` / `glob` / `replace` |
| 符号定位、调用链、影响面 | codebase-memory | `mcp__codebase_memory__*` |
| 找形状、批量结构审计 | ast-grep | `sg` CLI（路径见「路径铁律」） |
| 纯文本检索 | ripgrep | `rg` |
| JSON / YAML | `jq` / `yq` | — |
| XML / 存档取证 | xmllint | `xmllint --xpath` |
| 压缩文件 | `zstd -d -c` / `rg -z` | — |
| 结构化 diff | `difft`（重构）/ `delta`（git diff） | — |
| 任务编排 / 体检 | `just` | `~/.dsh/justfile`（23 任务） |
| 已卸 MCP 的退路 | `mcp2cli` | 缓存 `~/.cache/mcp2cli/` |
| 多步组合 | PTC | `run_code` 里写 TypeScript 一次跑完 |

**三条原则**：

1. **语义 → codebase-memory，形状 → ast-grep，文本 → rg。** 三者不可互替：
   rg 找字面量最快；ast-grep 能表达「带某修饰符的类声明」而 rg 不能；
   codebase-memory 能回答「谁调用它」而前两者都不能。
2. **多步无依赖 → 一个 `run_code` 程序一次跑完**，不要一步一问。
3. **结论必须复跑验证**：`||` 兜底文案、`grep -c` 计数都会伪造「结论」，见坑 1、4、11。

**合并规则**：无依赖调用**同轮并行**；可管道合成**一条 CLI**；禁止串行往返。

---

## ast-grep（`sg`）

### 语言名

`-l csharp` 与 `-l cs` **等价**（排序后逐字节相同，29/29 命中）。非法名会明确报错，不会静默：

```bash
sg run -p 'class $N' -l boguslang src/AutoHack/BootBoost.cs
# error: invalid value 'boguslang' for '--lang <LANG>': boguslang is not supported!
```

### 分水岭：表达式级 vs 声明级

这是本仓库最容易踩错的地方，**两条规则**：

**① 表达式 / 调用级 → `run --pattern` 可用**

```bash
ast-grep run --pattern 'NativeExes.Tick($$$)' --lang csharp src    # ✅ 命中 HackOverlay.cs:203
```

**② 方法 / 类声明级片段 → `run --pattern` 恒 0 命中**

顶层裸写的声明片段没有 class 包裹，会被解析成 `local_function_statement`：

```bash
ast-grep run -p 'private static string $NAME($$$) { $$$ }' -l csharp --debug-query=pattern src/AutoHack/HardenTools.cs
# Debug Pattern:
# local_function_statement          ← 不是 method_declaration
#   modifier
#     private
# 命中数：0
```

**可靠写法 = `scan --inline-rules` + `kind:` + `has`**：

```bash
sg scan --inline-rules 'id: m
language: csharp
rule:
  kind: method_declaration
  has:
    pattern: os.addExe($$$)
    stopBy: end' --json src/AutoHack | jq -r '.[] | "\(.file):\(.range.start.line+1)"'
# src/AutoHack\NativeExes.cs:277
```

### pattern 匹配的是完整 AST 节点，不是文本

**pattern 必须写出目标节点的全部修饰符与前置特性**，少写一个就落空：

```bash
# RefusedWhitelist.cs 第 25 行是 [HarmonyPatch]，第 26 行是 internal static class RefusedWhitelist
sg run -p 'class $N'                                 -l csharp src/AutoHack/RefusedWhitelist.cs  # 1（class 关键字节点）
sg run -p 'static class $N'                          -l csharp src/AutoHack/RefusedWhitelist.cs  # 0
sg run -p 'internal static class $N'                 -l csharp src/AutoHack/RefusedWhitelist.cs  # 0 ← 有特性，落空
sg run -p '[HarmonyPatch] internal static class $N'  -l csharp src/AutoHack/RefusedWhitelist.cs  # 1 ← 命中
```

对照无特性的 `BootBoost.cs`：`internal static class $N` 命中，`class $N` 只命中关键字。

`--debug-query=pattern` 是排错第一步：

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
  kind: method_declaration' src/AutoHack --json | jq length     # 193

sg scan --inline-rules 'id: a
language: csharp
rule:
  kind: attribute
  regex: ^Harmony' src/AutoHack --json | jq -r '[.[].file]|unique|length'  # 6
```

**特性节点用 `kind: attribute`，不要用 `pattern '[HarmonyPostfix]'`** —— 后者恒 0 命中（实测）。

### 计数：`--json | jq length` 而非 `grep -c`

默认输出是**多行上下文**（一个匹配展开成整节点），`grep -c` 数的是行不是匹配：

```bash
sg run -p 'internal static class $N' -l csharp src/AutoHack/BootBoost.cs | grep -c .          # 10（行）
sg run -p 'internal static class $N' -l csharp src/AutoHack/BootBoost.cs --json | jq length   # 1（匹配）
```

### `--rule` 的路径形态

```bash
sg scan --rule .tmp/r.yml                        src/AutoHack/BootBoost.cs   # ✅ 相对路径（推荐）
sg scan --rule D:/git/HacknetMod/.tmp/r.yml      src/AutoHack/BootBoost.cs   # ✅
sg scan --rule 'C:/Users/11/AppData/Local/Temp/r.yml' src/AutoHack/BootBoost.cs  # ✅
sg scan --rule /d/git/HacknetMod/.tmp/r.yml      src/AutoHack/BootBoost.cs   # ❌ Error: Cannot read rule
sg scan --rule /tmp/r.yml                        src/AutoHack/BootBoost.cs   # ❌ Error: Cannot read rule
```

⚠ 尽管 `sg` 在 wrapper 列表里，**`--rule` 的值不总是被正确转换**（`/tmp/...` 形态实测失败）。
**用相对路径最稳**；在 `run_code` 里靠 shell 变量或 `$(cygpath ...)` 传路径同样失败（不会展开）。
不需要落盘时优先 `--inline-rules`。

### 忽略规则

`sg` **默认尊重 `.gitignore`**，目录扫描跳过被忽略的文件。沙盒实测
（`.gitignore` 含 `ignored_sub/`，其中 `b.cs` 定义 `class Hidden`）：

```bash
sg run -p 'class $N' -l csharp .                   # 只出 Visible
sg run -p 'class $N' -l csharp ignored_sub         # 出 Hidden ← 显式指名即绕过忽略
sg run -p 'class $N' -l csharp --no-ignore vcs .   # 出 Hidden + Visible ← flag 有效
```

⚠ **别用 `git check-ignore` 推断 `sg` 的行为。** 本仓库 `decompiled/` 被 `.gitignore` 排除，
但 `sg` 扫目录仍能命中 **462** 个文件 —— 它不在 `sg` 实际读取的忽略源里，
`--no-ignore vcs` 加与不加结果相同。要扫 `decompiled/` 直接扫，不行再显式给文件。

### 稳定性

串行 500 次 + 并行 8×20 次目录扫描，退出码全部为 0。
但曾观测到**一次**偶发崩溃（`fatal runtime error: I/O error: operation failed to complete synchronously`，
退出码 127、输出截断，约 1/10 批次的早期小样本中出现过一次，之后 660 次未复现）。
**批量脚本仍应检查退出码**，避免把崩溃当成「无匹配」。

---

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
- `persistence=true` 写出 `.codebase-memory/graph.db.zst`（`.gitignore` 忽略，不入库）

重建后核对节点/边数：

```bash
jq -c '{nodes,edges,commit}' .codebase-memory/artifact.json
# {"nodes":888,"edges":2433,"commit":"a5ef1673d38c6a420c9a8488b2b5b63bb44a269d"}
```

### 判新鲜度

比对 `artifact.json` 的 `commit` 与当前 HEAD 即可。
**不要用「图里有没有某符号」判断** —— 未入索引的文件在重建前本就不在图里，会误判成过期。
行号是最可靠的交叉验证：

```bash
rg -n -F 'ConnectedPool(OS os)' src/AutoHack/     # HackEngine.cs:527
# 图内对应 qn 的 lines 字段也应是 527
```

### 常用查询

```js
search_graph({ project, query: "resolve target pool" })              // 自然语言
search_graph({ project, name_pattern: "^HackScope$" })               // 精确名
search_graph({ project, name_pattern: "Tick", file_pattern: "src/AutoHack/NativeExes.cs" })
trace_path({ project, function_name: "<完整 qn>", direction: "both", depth: 2 })
get_architecture({ project, aspects: ["overview", "hotspots"] })
check_index_coverage({ project, paths: ["src/AutoHack/NativeExes.cs"] })
```

⚠ **`trace_path` 只认完整 qualified name**，短名报 `function not found`。
拼法 = `qn_prefix + "." + name`：

```js
// ❌ trace_path({ function_name: "NativeExes.Tick" })
//    {"error":"function not found","hint":"Use search_graph(name_pattern=...) ..."}
// ✅ 先用 search_graph 拿 qn_prefix，再拼：
trace_path({ project, function_name: "D-git-HacknetMod.src.AutoHack.NativeExes.NativeExes.Tick" })
// callees: Count(1) / KillTrace(1) / HackEngine.KillTrace(2)   callers: HackOverlay.OnOSUpdate(1)
```

`format: "json"` 返回紧凑的 `cols/rows` 结构，比默认 tree 更好解析。

---

## 高危能力边界（会静默出错，必须记）

| 能力 | 边界 | 实测 |
|---|---|---|
| **ast-grep 声明级 pattern** | 顶层片段被解析成 `local_function_statement` | 0 命中；改 `kind: method_declaration` + `has` |
| **`sg` vs `ast-grep`** | 同名二进制、**不同路径规则** | `sg` 吃 `/d/`；`ast-grep` 只吃 `D:/` |
| **fastctx `grep`** | Rust regex，**无 lookaround / 反向引用** | `(?<=a)b` → `error: look-around ... is not supported`；要 lookaround 用 `rg -P` |
| **fastctx `run`** | 参数集与内置 bash 不同 | 传 `description` 硬报错 |
| **codebase-memory `compare_graphs`** | 两项目必须不同 | 同项目 → `base_project and target_project must be distinct` |
| **codebase-memory `query_graph`** | 不支持 `labels(n)[0]` | `unexpected input at pos 26 ('[')` |
| **`sd`** | 不在 wrapper 列表 | `sd ... /d/...` → `error: invalid path` |
| **`git -C`** | 原生 Windows git | `git -C /d/...` → `fatal: cannot change to` |
| **`duckdb`** | 读不了多帧 zstd | 须先 `zstd -d -c` 解出 |
| **`delta`** | 参数名 | 是 `--color-only`（无 `--color`） |
| **`7z`** | 两种路径形式 | 须先 `cd`（已进 wrapper，仍建议 `cd`） |
| **索引会过期** | 宿主升级 / 改 `~/.dsh` 配置 | `just reindex` / `just reindex-dsh` / `just reindex-all` |

---

## 存档取证

存档在 `C:/Users/11/Documents/My Games/HacknetPathfinder/Accounts/save_*.xml`
（当前：`save_a.xml` 2471574 字节 / `save_1.xml` 2527219 字节）。

### 陷阱：声明写 utf-16，实际是 utf-8

文件第 1 行是 `<?xml version="1.0" encoding="utf-16"?>`，但**内容是 UTF-8**
（`file` 确认：`XML 1.0 document, Unicode text, UTF-8 text, ... with CRLF line terminators`）。
直接喂解析器会被按 UTF-16 解，吐出 `㸿਍䠼捡湫瑥慓敶` 这类乱码：

```bash
xmllint --noout save_1.xml
# save_1.xml:1: parser error : Blank needed here
# 㸿਍䠼捡湫瑥慓敶朠湥牥瑡摥楍獳潩䍮畯瑮∽∲唠敳湲浡㵥

python -c "import xml.etree.ElementTree as ET; ET.parse('save_1.xml')"
# ParseError: encoding specified in XML declaration is incorrect: line 1, column 30
```

**最省事的解法：剥掉第一行，流式管道，不落盘。**

```bash
SAVE='/c/Users/11/Documents/My Games/HacknetPathfinder/Accounts/save_1.xml'
tail -n +2 "$SAVE" | xmllint --xpath 'count(//computer)' -
# 169
```

（`xmllint` 没有 `--encoding` 参数；`--recover` 也救不了，它照样按 UTF-16 解。）

### XPath 用法

元素名就是 C# 类名，属性就是字段名，层级直接对应。**一条 XPath 顶多行 `rg` 正则**：

```bash
Q() { tail -n +2 "$SAVE" | xmllint --xpath "$1" - 2>/dev/null; echo; }

Q 'count(//computer)'                                          # 169  机器总数
Q 'count(//computer[daemons/WhitelistAuthenticatorDaemon])'     # 4    白名单机器数
Q '//computer[daemons/WhitelistAuthenticatorDaemon]/@name'
#  name="太平洋航空_白名单_验证器" ... 太平洋_ATC_Skylink
Q '//computer[@id="playerComp"]/@ip'                           # 玩家机 IP
Q 'count(//ports/*)'                                           # 650  总端口数
```

⚠ XPath **不支持** `name()` 之类的函数出现在路径中段（`//x/*/name()` 报 Invalid expression）；
要取名用 `@name` 属性选择器。

### 不要用 sed 改这个声明

存档里**内嵌了 9 个 `.rec` 文件**，其内容本身也是 `<?xml version="1.0" encoding="utf-16"?>` 文本
（行 11983、11993、12003、12013、12023、12033、12044、12054、12064）。
实测 `encoding="utf-16"` 在整份存档里出现 **10** 次（1 个真头 + 9 个内嵌），
全局 `sed` 会把它们一并改掉 —— **改的是玩家数据，不只是文件头**。

必须限定只动第一处：

```bash
sed '1s/encoding="utf-16"/encoding="utf-8"/' "$SAVE" | xmllint --xpath 'count(//computer)' -
```

`sed` 还会把 CRLF 转成 LF（实测 2527219 → 2489228 字节，少 37990；CR 数 37990 → 0）。
**不要拿这个管道的结果覆盖原文件。**

---

## 本仓库的实测坑

| # | 坑 | 事实 |
|---|---|---|
| 1 | 兜底文案伪造结论 | `sg ... \|\| echo 'failed'` 会把**正常的无匹配**（rc=1）报成「工具失败」。判断工具是否报错要看 **stderr 与退出码**，不要看自己写的兜底串 |
| 2 | pattern 缺修饰符/特性 | `internal static class $N` 对**带特性**的类落空；`class $N` 只匹配关键字节点 |
| 3 | 特性用 pattern 匹配 | `[HarmonyPostfix]` 恒 0 命中，须 `kind: attribute` |
| 4 | `grep -c` 当匹配数 | 默认输出多行，须 `--json \| jq length` |
| 5 | MSYS 路径传 `--rule` | `/d/...` 与 `/tmp/...` 均报 Cannot read rule；用相对路径或 `D:/...` |
| 6 | 用 `git check-ignore` 推 `sg` 忽略行为 | 两者忽略源不同：`decompiled/` 被 git 忽略却被 `sg` 照常扫（462 文件），`--no-ignore vcs` 无差别。要确认就**直接试扫** |
| 7 | 未验证的「偶发崩溃」写进文档 | 单次 `fatal runtime error` 曾被写成「约 1/10」，复测 660 次 0 复现。**小样本异常不可当规律** |
| 8 | fastctx 传 `description` | 硬报错，见上表 |
| 9 | 用「符号是否存在」判索引新鲜度 | 新文件在重建前不在图里，会误判成过期；用 `artifact.json` 的 `commit` 与 HEAD 比对 |
| 10 | `replace` 的 replacement 含 `$N` | 会被当成捕获组引用而报 `undefined capture group`；整文件重写更省事 |
| 11 | **rg 报 os error 3 被当成「无匹配」** | 路径形态不对时 `rg` 退出码非 0、stderr 报 `IO error ... (os error 3)`。套上 `2>/dev/null \|\| echo '(无匹配)'` 就变成假结论 —— 本次真的据此误判「存档无非 ASCII 字节」（真值 4015 行含非 ASCII）。**这是坑 1 的第二次复发** |
| 12 | 原生 Windows 工具不认 `/c/` `/d/` 路径 | 根因 `MSYS2_ARG_CONV_EXCL=*`（HKCU 用户环境变量）。**现已由 `~/.dsh/winpath.sh` 的 wrapper 覆盖 10 个工具**；`sd`、`ast-grep`（全名）仍未覆盖 |
| 13 | `rg -c` 当匹配数 | 它数**行**。单行 3 个匹配时 `rg -c` 得 1，`--count-matches` 得 3 |
| 14 | 存档声明 `utf-16` 实际 `utf-8` | 直接喂解析器报 `encoding specified in XML declaration is incorrect`；`tail -n +2 \| xmllint -` 即可 |
| 15 | 用 `sed` 改存档声明 | 会连内嵌的 9 个 `.rec` 一起改（那是玩家数据），还把 CRLF 转 LF。必须 `1s/...` 限定首行 |
| 16 | `fastctx replace` 的 replacement 里 `$N` | `literal: true` **只作用于 pattern**，replacement 照样解析 `$SAVE` `$1` 并报 undefined capture group。字面 `$` 要写 `$$` |
| 17 | 往 `/usr/bin` 装东西 | `[ -w /usr/bin ]` 返回真但 `touch` 被拒。装 `~/.local/bin` |
| 18 | 取消 `MSYS2_ARG_CONV_EXCL` 来「修复」路径 | 会把**正则参数**当路径转换：`'/AutoHack/'` → `'C:/Program Files/Git/AutoHack/'`。这是二选一，不能两全 |
| 19 | 用 `echo` 测 MSYS 路径转换 | `echo` 是 bash 内建，**不走**参数转换，测不出任何东西。要用外部程序（`python -c "import sys;print(sys.argv[1:])"`） |
| 20 | **`sg` 与 `ast-grep` 混用** | 同一二进制、**路径规则不同**：`sg` 在 wrapper 里（吃 `/d/`），`ast-grep` 不在（只吃 `D:/`） |
| 21 | **声明级片段用 `run --pattern`** | 顶层裸写的方法/类片段被解析成 `local_function_statement`，**恒 0 命中**。改 `scan --inline-rules` + `kind:` |
| 22 | 路径里塞通配符 | `/d/.../**/*.cs` 与 `D:/.../**/*.cs` 都不满足 wrapper 判据 ③，双双报错。glob 用 `-g` |
| 23 | `trace_path` 传短名 | 只认完整 qualified name（`qn_prefix + "." + name`），短名报 `function not found` |
| 24 | **TS-first 护栏拦 `.js`** | 内置 `bash`/`write` 新建 `*.js` 硬拒；**fastctx `run` 不受约束**。脚本用 `.ts`/`.mts` |

> 坑 1、6、7、11 都是**曾经写错的结论**，记录于此以免重犯。早期版本的本文档曾把
> 「`-l cs` 静默失败」「`--no-ignore vcs` 必需」写成坑，实测证明均不成立 ——
> 前者是坑 1 的产物（`cs` 与 `csharp` 等价），后者加与不加结果相同。
>
> **坑 11 是坑 1 的复发**：同一个错误（兜底文案吞掉真实报错）在同一会话里又犯了一次，
> 只是换了个工具（`rg` 而非 `sg`）。教训不是「记住这个坑」，而是
> **不要在诊断命令外面套 `|| echo`** —— 要么看退出码，要么让 stderr 直接冒出来。

---

## 坑怎么避免

上面每一条坑都对应一个**可执行的规避动作**。分两类：能在环境里一次性根除的，和只能靠习惯约束的。

### 能一次性根除的

| 坑 | 规避动作 | 代价 |
|---|---|---|
| 12/20/22 路径形态 | wrapper 已覆盖 10 个工具；`sd`、`ast-grep`（全名）用相对路径或 `D:/...`；glob 用 `-g` | 无 |
| 5 `sg --rule` 路径 | 用相对路径；或优先 `--inline-rules` | 无 |
| 17 `/usr/bin` 权限 | 一律装 `~/.local/bin` | 无 |
| 16 `replace` 的 `$` | 字面 `$` 写 `$$`；或改用 `write` 整文件重写 | 无 |
| 14/15 存档编码 | 统一用 `tail -n +2 \| xmllint -`，不落盘、不 sed | 无 |
| 9 索引新鲜度 | 看 `artifact.json` 的 `commit`，不看符号有无 | 无 |
| 4/13 计数 | 记住 `--json \| jq length`（sg）、`--count-matches`（rg） | 无 |
| 21/23 ast-grep 与 trace | 声明级用 `kind:` 规则式；`trace_path` 先 `search_graph` 取 qn | 无 |
| 24 TS-first | 脚本一律 `.ts` / `.mts` | 无 |

### 只能靠习惯的（真正的根因）

**坑 1 与坑 11 是同一个错，犯了两次** —— 在诊断命令外面套 `|| echo`，
把非 0 退出码（工具报错）读成了「无匹配」（正常结果）。

这不是知识问题，是**动作习惯**问题。四条硬规则：

1. **诊断阶段不写 `|| echo` 兜底。** 要看工具是否报错，直接看**退出码与 stderr**。
   `||` 只该用在「失败也没关系」的地方，不能用在「我要靠这个输出下结论」的地方。
2. **下结论前先让数字自己说话。** 说「文件里没有 X」之前，必须有一条**独立路径**的验证
   （换个工具、换个计数方式），而不是只看一条命令的空输出。
3. **小样本异常不当规律。** 坑 7 就是这么来的：单次崩溃写成了「约 1/10」。
4. **区分「无匹配」与「出错」。** 两者退出码常常相同，只能靠 stderr：

   | 情形 | `sg` | `rg` |
   |---|---|---|
   | 有匹配 | rc=0 | rc=0 |
   | 无匹配（路径正常） | rc=1，stderr 空 | rc=1，stderr 空 |
   | 路径不存在 | rc=1，**stderr 有 ERROR** | **rc=2** |

**坑 2/3/21（ast-grep pattern）** 的规避：写 pattern 前先 `--debug-query=pattern` 看它解析成什么节点；
要「所有类/方法/特性」直接用 `kind:` 规则式，别拼 pattern。

**坑 6（忽略规则）** 的规避：**要确认就直接试扫**，别用 `git check-ignore` 推断另一个工具的忽略行为。

**坑 8（fastctx 参数）** 的规避：它和内置 bash 参数集不同，看表；传错是**硬报错**，不会静默忽略。

### 一句话总结

> 绝大多数坑不是「不知道」，而是**验证方式本身不可靠** ——
> 兜底文案吞掉报错、用行数当匹配数、拿一个工具的行为推另一个、
> 拿单次现象当规律。**换工具解决不了这个，改验证习惯才行。**

---

## 补充工具（MSYS2 直接取二进制）

本机 **无 pacman**（Git for Windows 不是 MSYS2 发行版），但可以下 MSYS2 官方包自己解。
装上后都落 `~/.local/bin`（已在 PATH 最前），**都是 MSYS 版，认 `/c/...` 路径**。

| 工具 | 用途 | 本仓库为什么需要 |
|---|---|---|
| `bc` `dc` | 任意精度计算 | 本机原本没有，只能 `awk` 凑；`dc` 同包白捡 |
| `xmllint` | XML 校验 + XPath | **存档取证**：一条 XPath 替代多行 `rg` 正则 |
| `zstd` | zstd 解压 CLI | 解 MSYS2 包 / `.zst`，此前被迫绕道 Node |
| `tree` | 目录树 | 目录概览 |
| `strings` | 提取二进制字符串 | 查 DLL 里的类型名（binutils 包） |

### 安装配方

```bash
# 1) 下载
curl -sSL -o p.zst https://repo.msys2.org/msys/x86_64/bc-1.08.2-1-x86_64.pkg.tar.zst

# 2) 解包 —— 首次没有 zstd 时用 Node 内置（Node 24 自带 zstd）
node -e "const z=require('zlib'),f=require('fs');f.writeFileSync('p.tar',z.zstdDecompressSync(f.readFileSync('p.zst')))"
# 装好 zstd 之后就可以直接：
zstd -q -d -c p.zst | tar -xf -

# 3) 取二进制到用户目录（不要试 /usr/bin，见下）
tar -xf p.tar && cp -v usr/bin/<工具>.exe ~/.local/bin/
```

**别往 `/usr/bin` 装**：`[ -w /usr/bin ]` 返回真，`touch` 却被拒（`Permission denied`）——
MSYS 的 Unix mode bit 与 Windows ACL 不同步，`/usr/bin` 实为 `C:\Program Files\Git\usr\bin`，需管理员。

本次实测的包名与版本：

| 包 | 版本 | 二进制 |
|---|---|---|
| `bc-1.08.2-1` | 1.08.2 | `bc.exe` `dc.exe` |
| `libxml2-2.15.4-1` | 2.15.4 | `xmllint.exe` `xmlcatalog.exe` + `msys-xml2-16.dll` |
| `zstd-1.5.7-1` | 1.5.7 | `zstd.exe` `unzstd.exe` `zstdcat.exe` `zstdmt.exe` |
| `tree-2.3.2-1` | 2.3.2 | `tree.exe` |
| `binutils-2.47-1` | 2.47 | `strings.exe` |

⚠ 装 `xmllint` 要**一并拷 `msys-xml2-16.dll`**（本机原本没有），否则起不来。

### `jq` 换成 MSYS 版

MSYS2 的 **msys 仓库**里 `jq` 是 MSYS 版（认 `/c/`）；而 `ripgrep` `fd` `bat` **只在 mingw64 仓库**，
那套同样是原生 Windows 版。官方 release 也只有 `pc-windows-msvc`，无 MSYS 构建。

```bash
curl -sSL -o jq.zst https://repo.msys2.org/msys/x86_64/jq-1.8.2-1-x86_64.pkg.tar.zst
curl -sSL -o o.zst  https://repo.msys2.org/msys/x86_64/oniguruma-6.9.10-1-x86_64.pkg.tar.zst
zstd -q -d -c jq.zst | tar -xf - && zstd -q -d -c o.zst | tar -xf -
cp usr/bin/jq.exe usr/bin/msys-onig-5.dll ~/.local/bin/   # 依赖必须一并拷
```

`~/.local/bin` 在 PATH 中排在 winget 之前，装上即生效。winget 版仍保留在原位，可随时回退。
⚠ 换完 `jq --version` 从 1.8.1 变 **1.8.2** —— 版本号会变，别以为是装错了。

### 计数用哪个

| 场景 | 命令 |
|---|---|
| 数匹配 | `rg -o PAT \| wc -l` |
| 数**含**匹配的行 | `rg -c PAT` |
| 数同一行内的多个匹配 | `rg --count-matches PAT` |

样本 `aaa aaa aaa` + 换行 + `bbb` 实测：
`rg -c aaa` → **1**，`rg --count-matches aaa` → **3**，`rg -o aaa | wc -l` → **3**。

---

## 关键路径

| 用途 | 路径 |
|---|---|
| 路径包装 | `~/.dsh/winpath.sh`（`~/.bashrc:72` source） |
| 全局约定 | `~/.dsh/AGENTS.md` |
| 深度参考 | `~/.dsh/docs/win-cli-toolkit.md` |
| 任务编排 | `~/.dsh/justfile`（23 任务） |
| profile | `~/.dsh/profiles/web/{cordis.patch.yml,package.json}` |
| 宿主 | `C:/Users/11/AppData/Roaming/npm/node_modules/@deepseek-ai/dsh` |
| 会话日志 | `~/.dsh/sessions/<ws>/<sid>/session.v4.jsonl.zstd` |
| mcp2cli 缓存 | `~/.cache/mcp2cli/` |
| 本仓库源码 | `src/AutoHack/`（29 个 `.cs`）+ `src/SaveFix/` |
| 代码索引 | `.codebase-memory/artifact.json` + `graph.db.zst` |
| 索引排除说明 | `.cbmignore` |
| 游戏插件目录 | `D:/steam/steamapps/common/Hacknet/BepInEx/plugins/` |

**构建**（`OutputPath` 直投游戏插件目录，无需手动拷贝）：

```bash
rm -rf src/AutoHack/obj src/AutoHack/bin && dotnet build src/AutoHack/AutoHack.csproj -c Release -v q --nologo
```

---

## 环境

| 工具 | 版本 | 路径形态 |
|---|---|---|
| node | v24.15.0 | — |
| dotnet | 10.0.301 | 都行 |
| ast-grep / `sg` | 0.42.3 | `sg` 走 wrapper；**`ast-grep` 全名只吃 `D:/`** |
| ripgrep | 15.1.0 | 走 wrapper |
| jq | 1.8.2 | 都行（**已换 MSYS 版**） |
| gh | 2.96.0 | 都行 |
| git | 2.55.0.windows.3 | **必须 `cd` 后调用，`git -C /d/...` 报错** |
| python | 3.14.5 | 都行 |
| ilspycmd | 10.1.1.8388 | 需 Windows 形态（`D:/steam/...`） |
| bc / dc | 1.08.2 | 都行（MSYS） |
| xmllint | 2.15.4 | 都行（MSYS） |
| zstd | 1.5.7 | 都行（MSYS） |
| tree | 2.3.2 | 都行（MSYS） |
| strings | 2.47 | 都行（MSYS） |

- 工作目录 `D:\git\HacknetMod`；**Git Bash 下所有命令都要显式 `cd`**（会话默认目录可能不是仓库）
- **`MSYS2_ARG_CONV_EXCL=*`** 设在 `HKCU\Environment`（用户级，非本会话临时）——
  它是坑 12 的根因；改它会影响所有原生 Windows 工具，且会毁掉正则参数（坑 18）
- `rg` 没有 `--include`（那是 `grep` 的参数），用 `-g`
- 新装的 MSYS2 工具都在 `~/.local/bin`（`bc` `dc` `xmllint` `zstd` `tree` `strings`，
  以及替换 winget 版的 `jq`）；`/usr/bin` 需管理员，别往那儿装（坑 17）
- `~/.local/bin` 在 PATH 中排在 `WinGet/Links` 之前，同名工具以 `~/.local/bin` 为准

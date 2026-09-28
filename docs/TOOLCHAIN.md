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
| XML / 存档取证 | xmllint | `xmllint --xpath` |
| 多步组合 | PTC | `run_code` 里写 TypeScript 一次跑完 |

三条原则：

1. **语义 → codebase-memory，形状 → ast-grep，文本 → rg。** 三者不可互替：
   rg 找字面量最快；ast-grep 能表达「带某修饰符的类声明」而 rg 不能；
   codebase-memory 能回答「谁调用它」而前两者都不能。
2. **多步无依赖 → 一个 `run_code` 程序一次跑完**，不要一步一问。
3. **结论必须复跑验证**：`||` 兜底文案、`grep -c` 计数都会伪造「结论」，
   见下文坑 1、坑 4 与坑 11（同一个错在同一会话里犯了两次）。

## 路径形态：本机最大的坑

本机 `HKCU\Environment` 里设了 **`MSYS2_ARG_CONV_EXCL=*`**（Windows 用户级环境变量，非 DSH 设置）。
它**全局禁用 MSYS 的参数路径转换**：传给原生 Windows 程序的 `/c/...` `/d/...` 不会被转成 `C:\...`。

后果按**工具的构建方式**分两类（与工具本身无关，只看是不是原生 Windows 版）：

| 工具 | 来源 | `/d/...` | `D:/...` |
|---|---|---|---|
| `rg` | winget（MSVC） | ❌ | ✅ |
| `fd` | winget | ❌ | ✅ |
| `bat` | winget | ❌ | ✅ |
| `jq` | winget | ❌ | ✅ |
| `grep` `sed` `awk` `diff` | Git for Windows（MSYS） | ✅ | ✅ |
| `tree` `strings` `xmllint` `zstd` `bc` | 本文档新装（MSYS） | ✅ | ✅ |
| `gh` `dotnet` | 原生，自己处理路径 | ✅ | ✅ |

失败长相：

```
rg: /d/git/HacknetMod/src: IO error for operation on /d/git/HacknetMod/src: 系统找不到指定的路径。 (os error 3)
```

**对策**：给 `rg` / `fd` / `bat` / `jq` 一律传 Windows 形态（`D:/git/HacknetMod/src`，正斜杠即可），
或在仓库内用相对路径。`grep` 不受影响，但既然 `rg` 更快，改路径比换工具省事。

⚠ **这也是坑 5 的真正根因** —— `sg` 拒收 `/d/...` 形态的 `--rule` 路径不是 ast-grep 的怪癖，而是这个变量。

⚠ **连带教训**：路径形态不对时 `rg` 报的是 **`os error 3`（退出码非 0）**，不是「无匹配」。
外面套 `2>/dev/null || echo '(无匹配)'` 就会把「路径错了」读成「文件里没有」——
本次会话真的这样误判过一次（见坑 11）。

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

## 补充工具（MSYS2 直接取二进制）

本机 **无 pacman**（Git for Windows 不是 MSYS2 发行版），但可以下 MSYS2 官方包自己解。
装上后都落 `~/.local/bin`（已在 PATH），**都是 MSYS 版，认 `/c/...` 路径**。

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

**别往 `/usr/bin` 装**：`[ -w /usr/bin ]` 返回真，`cp` 却被拒 ——
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

### 计数用哪个

| 场景 | 命令 |
|---|---|
| 数匹配 | `rg -o PAT | wc -l` |
| 数**含**匹配的行 | `rg -c PAT` |
| 数同一行内的多个匹配 | `rg --count-matches PAT` |

`rg -c` 数的是**行**不是匹配。样本 `aaa aaa aaa` + 换行 + `bbb` 实测：
`rg -c aaa` → **1**，`rg --count-matches aaa` → **3**，`rg -o aaa | wc -l` → **3**。

## 存档取证

存档在 `C:/Users/11/Documents/My Games/HacknetPathfinder/Accounts/save_*.xml`。

### 陷阱：声明写 utf-16，实际是 utf-8

文件第 1 行是 `<?xml version="1.0" encoding="utf-16"?>`，但**内容是 UTF-8**
（含 239413 个非 ASCII 字节、中文机器名）。直接喂解析器会被按 UTF-16 解，吐出 `㸿਍䠼捡湫瑥慓敶` 这类乱码：

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
`sed 's/encoding="utf-16"/encoding="utf-8"/'` 会把它们一并改掉 —— **改的是玩家数据，不只是文件头**。

必须限定只动第一处：

```bash
sed '1s/encoding="utf-16"/encoding="utf-8"/' "$SAVE" | xmllint --xpath 'count(//computer)' -
```

`sed` 还会把 CRLF 转成 LF（实测少 37990 字节）—— 存档是 CRLF，`file` 确认。



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
| 11 | **rg 报 os error 3 被当成「无匹配」** | 路径形态不对时 `rg` 退出码非 0、stderr 报 `IO error ... (os error 3)`。套上 `2>/dev/null || echo '(无匹配)'` 就变成假结论 —— 本次真的据此误判「存档无非 ASCII 字节」（真值 4015 行含非 ASCII）。**这是坑 1 的第二次复发** |
| 12 | 原生 Windows 工具不认 `/c/` `/d/` 路径 | 根因 `MSYS2_ARG_CONV_EXCL=*`（HKCU 用户环境变量）。`rg` `fd` `bat` `jq` 中招，`grep` `tree` `strings` `xmllint` 不受影响。给前四个传 `D:/...` |
| 13 | `rg -c` 当匹配数 | 它数**行**。单行 3 个匹配时 `rg -c` 得 1，`--count-matches` 得 3 |
| 14 | 存档声明 `utf-16` 实际 `utf-8` | 直接喂解析器报 `encoding specified in XML declaration is incorrect`；`tail -n +2 | xmllint -` 即可 |
| 15 | 用 `sed` 改存档声明 | 会连内嵌的 9 个 `.rec` 一起改（那是玩家数据），还把 CRLF 转 LF。必须 `1s/...` 限定首行，或用二进制替换 `count=1` |
| 16 | `fastctx replace` 的 replacement 里 `$N` | `literal: true` **只作用于 pattern**，replacement 照样解析 `$SAVE` `$1` 并报 undefined capture group。字面 `$` 要写 `$$` |
| 17 | 往 `/usr/bin` 装东西 | `[ -w /usr/bin ]` 返回真但 `cp` 被拒（Unix mode bit 与 Windows ACL 不同步）。装 `~/.local/bin` |


> 坑 1、6、7、11 都是**曾经写错的结论**，记录于此以免重犯。早期版本的本文档曾把
> 「`-l cs` 静默失败」「`--no-ignore vcs` 必需」写成坑，实测证明均不成立 ——
> 前者是坑 1 的产物（`cs` 与 `csharp` 等价），后者加与不加结果相同。
>
> **坑 11 是坑 1 的复发**：同一个错误（兜底文案吞掉真实报错）在同一会话里又犯了一次，
> 只是换了个工具（`rg` 而非 `sg`）。教训不是「记住这个坑」，而是
> **不要在诊断命令外面套 `|| echo`** —— 要么看退出码，要么让 stderr 直接冒出来。

## 环境

| 工具 | 版本 | 路径形态 |
|---|---|---|
| node | v24.15.0 | — |
| dotnet | 10.0.301 | 都行 |
| ast-grep | 0.42.3 | 见坑 5 |
| ripgrep | 15.1.0 | **必须 Windows** |
| jq | 1.8.1 | **必须 Windows** |
| gh | 2.96.0 | 都行 |
| bc / dc | 1.08.2 | 都行（MSYS） |
| xmllint | 2.15.4 | 都行（MSYS） |
| zstd | 1.5.7 | 都行（MSYS） |
| tree | 2.3.2 | 都行（MSYS） |
| strings | 2.47 | 都行（MSYS） |

- 工作目录 `D:\git\HacknetMod`；**Git Bash 下所有命令都要显式 `cd`**（会话默认目录是 `C:/Users/11`）
- **`MSYS2_ARG_CONV_EXCL=*`** 设在 `HKCU\Environment`（用户级，非本会话临时）——
  这是坑 12 与坑 5 的根因，改环境变量会影响所有原生 Windows 工具
- `git.exe` 是原生 Windows 版：**必须 `cd` 后调用，不能用 `git -C /d/...`**（报 cannot change to）
- `ilspycmd` 需 Windows 形态路径（`D:/steam/...`，非 `/d/steam/...`）
- `rg` 没有 `--include`（那是 `grep` 的参数），用 `-g`
- 新装的 MSYS2 工具都在 `~/.local/bin`（`bc` `dc` `xmllint` `zstd` `tree` `strings`）；
  `/usr/bin` 需管理员，别往那儿装（坑 17）

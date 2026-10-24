// 核心层离线单测：不引用 Hacknet / Pathfinder / BepInEx / Harmony。
// 手写断言（零 NuGet）：失败计数 > 0 时退出码为 1。
using AutoHack.Core;

var failures = 0;
var total = 0;

void Check(string name, bool ok, string detail = null)
{
    total++;
    if (ok)
    {
        return;
    }

    failures++;
    Console.WriteLine("FAIL " + name + (detail == null ? "" : " :: " + detail));
}

string ErrorOf(Action action)
{
    try
    {
        action();
        return null;
    }
    catch (Exception ex)
    {
        return ex.GetType().Name + ": " + ex.Message;
    }
}

HackScript Parse(string text) => HackScript.Parse(text, "test");

// ① 动作与别名
var basic = Parse("connect\nprobe\nopenPort 22\nporthack\ndc");
Check("basic.count", basic.Actions.Count == 5, "got " + basic.Actions.Count);
Check("basic.connect", basic.Actions[0].Kind == HackStepKind.Connect);
Check("basic.probe", basic.Actions[1].Kind == HackStepKind.Probe);
Check("basic.openPort.kind", basic.Actions[2].Kind == HackStepKind.OpenPort);
Check("basic.openPort.port", basic.Actions[2].Port == 22, "got " + basic.Actions[2].Port);
Check("basic.escalate", basic.Actions[3].Kind == HackStepKind.Escalate);
Check("basic.disconnect", basic.Actions[4].Kind == HackStepKind.Disconnect);

// ② 别名与大小写、缺省端口 0
var alias = Parse("C\nANTICOUNTER\ncrack");
Check("alias.connect", alias.Actions[0].Kind == HackStepKind.Connect);
Check("alias.neutralize", alias.Actions[1].Kind == HackStepKind.Neutralize);
Check("alias.openPort.default", alias.Actions[2].Port == 0, "got " + alias.Actions[2].Port);

// ③ 行尾分隔符 / 注释 / 空行
var messy = Parse("# 注释\n\nconnect $#%#$\nprobe\r\n");
Check("messy.count", messy.Actions.Count == 2, "got " + messy.Actions.Count);
Check("messy.connect", messy.Actions[0].Kind == HackStepKind.Connect);

// ④ 未知动作
var unknown = ErrorOf(() => Parse("bogus"));
Check("unknown.msg", unknown != null && unknown.Contains("unknown action"), unknown);

// ⑤ NPC 动作解释
var npc = ErrorOf(() => Parse("forkbomb"));
Check("npc.msg", npc != null && npc.Contains("game NPC action"), npc);

// ⑥ dc 之后再 rm 必须被拒
var badOrder = ErrorOf(() => Parse("connect\ndc\nrm"));
Check("order.msg", badOrder != null && badOrder.Contains("after 'dc'"), badOrder);

// ⑦ dc / connect / rm 合法
Check("order.legal", ErrorOf(() => Parse("connect\ndc\nconnect\nrm")) == null);

// ⑧ 空脚本
var empty = ErrorOf(() => Parse("# only comment\n\n"));
Check("empty.msg", empty != null && empty.Contains("no actions"), empty);

// ⑨ openPort 参数非数字
var badPort = ErrorOf(() => Parse("openPort ssh"));
Check("port.msg", badPort != null && badPort.Contains("takes a port number"), badPort);

// ⑩ Load 走注入的路径委托（核心不认识 Content 目录）
var dir = Path.Combine(Path.GetTempPath(), "autohack-core-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
var file = Path.Combine(dir, "demo.txt");
File.WriteAllText(file, "probe\nrm");
var loaded = HackScript.Load("demo", rel => rel.Contains("HackerScripts") || rel == "demo" ? file : rel);
Check("load.actions", loaded.Actions.Count == 2, "got " + loaded.Actions.Count);
Check("load.source", loaded.Source == file, loaded.Source);
Directory.Delete(dir, true);

var missing = ErrorOf(() => HackScript.Load("nope", rel => rel));
Check("load.missing", missing != null && missing.Contains("No such script"), missing);

Console.WriteLine(failures == 0
    ? $"PASS {total} checks"
    : $"FAILED {failures}/{total}");
return failures == 0 ? 0 : 1;

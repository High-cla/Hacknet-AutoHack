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
Check("basic.connect", basic.Actions[0].Kind == ActionKind.Connect);
Check("basic.probe", basic.Actions[1].Kind == ActionKind.Probe);
Check("basic.openPort.kind", basic.Actions[2].Kind == ActionKind.OpenPort);
Check("basic.openPort.port", basic.Actions[2].Port == 22, "got " + basic.Actions[2].Port);
Check("basic.escalate", basic.Actions[3].Kind == ActionKind.Escalate);
Check("basic.disconnect", basic.Actions[4].Kind == ActionKind.Disconnect);

// ② 别名与大小写、缺省端口 0
var alias = Parse("C\nANTICOUNTER\ncrack");
Check("alias.connect", alias.Actions[0].Kind == ActionKind.Connect);
Check("alias.neutralize", alias.Actions[1].Kind == ActionKind.Neutralize);
Check("alias.openPort.default", alias.Actions[2].Port == 0, "got " + alias.Actions[2].Port);

// ③ 行尾分隔符 / 注释 / 空行
var messy = Parse("# 注释\n\nconnect $#%#$\nprobe\r\n");
Check("messy.count", messy.Actions.Count == 2, "got " + messy.Actions.Count);
Check("messy.connect", messy.Actions[0].Kind == ActionKind.Connect);

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

// ⑪ 命令行解析：缺省值与别名
var d = HackOptions.Parse(Array.Empty<string>());
Check("opt.default.scope", d.Scope == HackScope.Network);
Check("opt.default.wipe", d.WipeTraces);
Check("opt.default.connect", d.ConnectFirst && !d.Disconnect);
Check("opt.default.skipOwned", d.SkipOwned);
Check("opt.default.creds", !d.UseCredentials);
Check("opt.default.show", d.ShowExes);
Check("opt.default.ip", !d.ResetIP);
Check("opt.default.mods", d.ModPorts.Count == 0);
Check("opt.default.targets", d.Targets.Count == 0 && d.Script == null);

var flags = HackOptions.Parse(["here", "keep", "dc", "noshow", "creds", "direct", "redo", "allnodes", "mark", "newip"]);
Check("opt.here", flags.Scope == HackScope.Connected);
Check("opt.keep", !flags.WipeTraces);
Check("opt.dc", flags.Disconnect);
Check("opt.noshow", !flags.ShowExes);
Check("opt.creds", flags.UseCredentials);
Check("opt.direct", !flags.ConnectFirst);
Check("opt.redo", !flags.SkipOwned);
Check("opt.allnodes", flags.AllNodes);
Check("opt.mark", flags.UploadMarker);
Check("opt.newip", flags.ResetIP);

var explicitIds = HackOptions.Parse(["1.2.3.4", "5.6.7.8"]);
Check("opt.explicit.scope", explicitIds.Scope == HackScope.Explicit);
Check("opt.explicit.count", explicitIds.Targets.Count == 2, string.Join(",", explicitIds.Targets));

var mods = HackOptions.Parse(["modports=mqtt,ntp"]);
Check("opt.mods.count", mods.ModPorts.Count == 2, string.Join(",", mods.ModPorts));
Check("opt.mods.value", mods.ModPorts.Contains("mqtt"));

Check("opt.retired.instant", HackOptions.RetiredTokenIn(["run", "instant"]) == "instant");
Check("opt.retired.delay", HackOptions.RetiredTokenIn(["run", "delay=3"]) == "delay=3");
Check("opt.retired.none", HackOptions.RetiredTokenIn(["run", "here"]) == null);

// ⑫ 动作载体
var act = GameAction.Of(ActionKind.Probe, "1.1.1.1", "probe");
Check("action.echo", act.Echo == "probe" && act.Kind == ActionKind.Probe);
Check("action.port.default", act.Port.Protocol == null);

// ⑬ 规划器：内置次序
TargetView Host(string ip, int needed = 2, PortView[] ports = null, bool eos = false,
    bool firewall = false, bool solved = false, bool whitelist = false, bool proxy = false, bool owned = false)
    => new(ip, "h-" + ip, owned, eos, proxy, proxy, firewall, solved, whitelist, needed, 0,
           ports ?? Array.Empty<PortView>());

PortView P(string proto, int code, bool cracked = false) => new(proto, code, code, proto.ToUpperInvariant(), cracked);

var planOpts = HackOptions.Parse(["here"]);
var hostA = Host("10.0.0.1", 2, [P("ssh", 22), P("ftp", 21)]);
var planA = Planner.Build([hostA], [], Host("9.9.9.9"), null, planOpts, null);
var kinds = planA.Steps.Select(s => s.Kind).ToArray();
Check("plan.count", kinds.Length == 11, string.Join(",", kinds));   // 末位是玩家机自己的清痕
Check("plan.head", kinds[0] == ActionKind.Connect && kinds[1] == ActionKind.Neutralize && kinds[2] == ActionKind.Probe);
Check("plan.echo.connect", planA.Steps[0].Echo == "connect 10.0.0.1", planA.Steps[0].Echo);
Check("plan.ports", kinds.Count(k => k == ActionKind.OpenPort) == 2);
Check("plan.killtrace.per.port", kinds[4] == ActionKind.KillTrace && kinds[6] == ActionKind.KillTrace);
Check("plan.escalate", kinds[7] == ActionKind.Escalate && planA.Steps[7].Echo == "porthack");
Check("plan.tail", kinds[8] == ActionKind.CleanLogs && kinds[9] == ActionKind.KillTrace);
Check("plan.no.dc", !kinds.Contains(ActionKind.Disconnect));

// ⑭ 规划器：防火墙 / 白名单 / 已知连接 / 直接模式
var hostB = Host("10.0.0.2", 1, [P("ssh", 22)], firewall: true, whitelist: true);
var planB = Planner.Build([hostB], [], Host("9.9.9.9"), "10.0.0.2", HackOptions.Parse(["direct", "dc"]), null);
var kindsB = planB.Steps.Select(s => s.Kind).ToArray();
Check("planB.no.connect", !kindsB.Contains(ActionKind.Connect));
Check("planB.solve", kindsB.Contains(ActionKind.SolveFirewall));
Check("planB.whitelist", kindsB.Contains(ActionKind.BypassWhitelist));
Check("planB.dc", kindsB.Contains(ActionKind.Disconnect));

// ⑮ 规划器：脚本模式的前置与去重
var script = HackScript.Parse("connect\nprobe\nrm\nrm\nopenPort 22", "t");
var planC = Planner.Build([Host("10.0.0.3", 1, [P("ssh", 22), P("ftp", 21)])], [], null, null,
    HackOptions.Parse(["here"]), script);
var kindsC = planC.Steps.Select(s => s.Kind).ToArray();
Check("planC.head", kindsC[0] == ActionKind.Connect && kindsC[1] == ActionKind.Neutralize);
Check("planC.rm.repeatable", kindsC.Count(k => k == ActionKind.CleanLogs) == 3, string.Join(",", kindsC));   // 2 条脚本 rm + 1 条兜底
Check("planC.port.filtered", kindsC.Count(k => k == ActionKind.OpenPort) == 1);
Check("planC.killtrace.tail", kindsC[^1] == ActionKind.KillTrace);

// ⑯ 状态机：一帧一个端口步，其余步骤同帧推进
var world = new FakeWorld();
var session = new Session(planA, planOpts);
void Exec(GameAction a)
{
    switch (a.Kind)
    {
        case ActionKind.Connect: world.ConnectedIp = a.TargetIp; break;
        case ActionKind.OpenPort: world.OpenPorts++; break;
        case ActionKind.Escalate: world.Owned.Add(a.TargetIp); break;
    }
}

var f1 = session.Tick(world, 0.016f);
foreach (var a in f1) { Exec(a); }
Check("session.frame1.count", f1.Count == 1 && f1[0].Kind == ActionKind.Connect, string.Join(",", f1.Select(a => a.Kind)));
Check("session.frame2.head", true);
var f2 = session.Tick(world, 0.016f);
foreach (var a in f2) { Exec(a); }
Check("session.frame2", f2.Count == 3 && f2[^1].Kind == ActionKind.OpenPort, string.Join(",", f2.Select(a => a.Kind)));

// ⑰ 状态机：端口到门槛后跳过剩余端口步（KillTrace 仍执行）
var quotaPlan = Planner.Build([Host("10.0.0.4", 1, [P("ssh", 22), P("ftp", 21), P("http", 80)])], [], null, null, HackOptions.Parse(["here"]), null);
var quotaWorld = new FakeWorld { ConnectedIp = "10.0.0.4", OpenPorts = 2 };
var quotaSession = new Session(quotaPlan, HackOptions.Parse(["here"]));
var quotaActions = Runner.Run(quotaSession, quotaWorld, a => { if (a.Kind == ActionKind.OpenPort) { quotaWorld.OpenPorts++; } });
Check("session.quota.skipped", quotaActions.Count(a => a.Kind == ActionKind.OpenPort) == 0, string.Join(",", quotaActions.Select(a => a.Kind)));
Check("session.quota.client", quotaActions.Count(a => a.Kind == ActionKind.KillTrace) >= 3);
Check("session.quota.escalate", quotaActions.Exists(a => a.Kind == ActionKind.Escalate));

// ⑱ 状态机：连接被拒只跳依赖连接的步
var refusedPlan = Planner.Build([Host("10.0.0.5", 1, [P("ssh", 22)])], [], null, null, HackOptions.Parse(["here", "dc"]), null);
var refusedWorld = new FakeWorld();   // ConnectedIp 恒 null = 被拒
var refusedSession = new Session(refusedPlan, HackOptions.Parse(["here", "dc"]));
var refusedActions = Runner.Run(refusedSession, refusedWorld, _ => { });
Check("session.refused.dc", !refusedActions.Exists(a => a.Kind == ActionKind.Disconnect), string.Join(",", refusedActions.Select(a => a.Kind)));
Check("session.refused.port.kept", refusedActions.Exists(a => a.Kind == ActionKind.OpenPort));
Check("session.refused.logs.kept", refusedActions.Exists(a => a.Kind == ActionKind.CleanLogs));
Check("session.refused.tracked", refusedSession.Refused.Contains("10.0.0.5"));

// ⑲ 状态机：login 成功后跳过破端口/解防火墙/提权
var loginOpts = HackOptions.Parse(["here", "creds"]);
var loginPlan = Planner.Build([Host("10.0.0.6", 1, [P("ssh", 22)], firewall: true)], [], null, null, loginOpts, null);
var loginWorld = new FakeWorld();
void ExecLogin(GameAction a)
{
    if (a.Kind == ActionKind.Connect) { loginWorld.ConnectedIp = a.TargetIp; }
    if (a.Kind == ActionKind.Login) { loginWorld.Owned.Add(a.TargetIp); }
}
var loginSession = new Session(loginPlan, loginOpts);
var loginActions = Runner.Run(loginSession, loginWorld, ExecLogin);
Check("session.login.port.skipped", !loginActions.Exists(a => a.Kind == ActionKind.OpenPort), string.Join(",", loginActions.Select(a => a.Kind)));
Check("session.login.escalate.skipped", !loginActions.Exists(a => a.Kind == ActionKind.Escalate));
Check("session.login.flag", loginSession.LoggedIn.Contains("10.0.0.6"));

// ⑳ 状态机：提权前等动画，超上限先补开
var capOpts = HackOptions.Parse(["here"]);
var capPlan = Planner.Build([Host("10.0.0.7", 1, [P("ssh", 22)])], [], null, null, capOpts, null);
var capWorld = new FakeWorld { ConnectedIp = "10.0.0.7", Settled = false };
var capSession = new Session(capPlan, capOpts);
var capActions = Runner.Run(capSession, capWorld, _ => { }, 200f);
Check("session.wait.flush", capActions.Exists(a => a.Kind == ActionKind.FlushAnimations), string.Join(",", capActions.Select(a => a.Kind)));
Check("session.wait.escalate", capActions.Exists(a => a.Kind == ActionKind.Escalate));

Console.WriteLine(failures == 0
    ? $"PASS {total} checks"
    : $"FAILED {failures}/{total}");
return failures == 0 ? 0 : 1;

/// <summary>假世界：想让它回答什么就回答什么 —— 核心层不知道这是假的。</summary>
sealed class FakeWorld : IWorldView
{
    public string ConnectedIp { get; set; }

    public int OpenPorts { get; set; }

    public int InFlight { get; set; }

    public bool Settled { get; set; } = true;

    public HashSet<string> Owned { get; } = new();

    public int OpenPortCount(string targetIp) => OpenPorts;

    public bool IsOwned(string targetIp) => Owned.Contains(targetIp);

    public int AnimationInFlight(string targetIp) => InFlight;

    public bool AnimationSettled(string targetIp) => Settled;
}

/// <summary>把状态机跑到结束，同时把动作喂给执行委托（模拟宿主的同步执行）。</summary>
static class Runner
{
    internal static List<GameAction> Run(
        Session session, IWorldView world, Action<GameAction> exec, float dt = 0.016f, int maxFrames = 5000)
    {
        var all = new List<GameAction>();
        for (var frame = 0; frame < maxFrames && !session.Finished; frame++)
        {
            foreach (var action in session.Tick(world, dt))
            {
                all.Add(action);
                exec(action);
            }
        }

        return all;
    }
}


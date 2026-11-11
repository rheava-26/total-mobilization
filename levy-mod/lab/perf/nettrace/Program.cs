using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

// usage: nt <file.nettrace> [marker=Terraria.Main.DoUpdate] [top=60] [depth=7] [minpct=0.7]
string path = args[0];
string marker = args.Length > 1 ? args[1] : "Terraria.Main.DoUpdate";
int top = args.Length > 2 ? int.Parse(args[2]) : 60;
int depthMax = args.Length > 3 ? int.Parse(args[3]) : 7;
double minPct = args.Length > 4 ? double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : 0.7;
string etlx = path + ".etlx";
if (File.Exists(etlx)) File.Delete(etlx);
TraceLog.CreateFromEventPipeDataFile(path, etlx);
using var log = new TraceLog(etlx);
var incl = new Dictionary<string, int>();
var excl = new Dictionary<string, int>();
var tree = new Node("<root>");
int total = 0, inMarker = 0, noStack = 0;
var folded = args.Length > 5 ? new StreamWriter(args[5]) : null;
var threadCounts = new Dictionary<int, int>();
foreach (var ev in log.Events)
{
    if (ev.EventName == null || !ev.EventName.Contains("Sample")) continue;
    total++;
    var cs = ev.CallStack();
    if (cs == null) { noStack++; continue; }
    var frames = new List<string>();
    for (var c = cs; c != null; c = c.Caller)
    {
        var m = c.CodeAddress.FullMethodName;
        frames.Add(string.IsNullOrEmpty(m) ? "?0x" + c.CodeAddress.Address.ToString("x") : m);
    }
    frames.Reverse(); // root first
    int mi = frames.FindIndex(f => f.Contains(marker));
    if (mi < 0) continue;
    inMarker++;
    threadCounts.TryGetValue(ev.ThreadID, out int tc); threadCounts[ev.ThreadID] = tc + 1;
    if (folded != null) folded.WriteLine(string.Join(";", frames.Skip(mi).Select(f => f.Replace(";", ",").Replace(" ", "_"))) + " 1");
    var seen = new HashSet<string>();
    foreach (var f in frames.Skip(mi)) if (seen.Add(f)) incl[f] = incl.GetValueOrDefault(f) + 1;
    string leaf = frames[^1]; excl[leaf] = excl.GetValueOrDefault(leaf) + 1;
    var n = tree; n.Count++;
    int d = 0;
    foreach (var f in frames.Skip(mi))
    {
        if (!n.Kids.TryGetValue(f, out var k)) n.Kids[f] = k = new Node(f);
        k.Count++; n = k; if (++d >= depthMax + 12) break;
    }
}
folded?.Dispose();
Console.WriteLine($"samples total={total} noStack={noStack} inMarker({marker})={inMarker} threads={string.Join(",", threadCounts.Select(kv => kv.Key + ":" + kv.Value))}");
Console.WriteLine("--- inclusive (top) ---");
foreach (var kv in incl.OrderByDescending(k => k.Value).Take(top)) Console.WriteLine($"{100.0 * kv.Value / inMarker,6:0.0}%  {kv.Value,6}  {kv.Key}");
Console.WriteLine("--- exclusive (top) ---");
foreach (var kv in excl.OrderByDescending(k => k.Value).Take(top / 2)) Console.WriteLine($"{100.0 * kv.Value / inMarker,6:0.0}%  {kv.Value,6}  {kv.Key}");
Console.WriteLine("--- tree ---");
void Print(Node nd, int depth)
{
    if (depth > depthMax) return;
    foreach (var k in nd.Kids.Values.OrderByDescending(x => x.Count))
    {
        double pct = 100.0 * k.Count / inMarker; if (pct < minPct) break;
        Console.WriteLine($"{new string(' ', depth * 2)}{pct:0.0}% {Short(k.Name)}");
        Print(k, depth + 1);
    }
}
Print(tree, 0);
static string Short(string s) { return s.Length > 150 ? s.Substring(0, 150) : s; }
class Node { public string Name; public int Count; public Dictionary<string, Node> Kids = new(); public Node(string n) { Name = n; } }

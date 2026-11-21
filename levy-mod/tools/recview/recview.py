#!/usr/bin/env python3
"""Read a Levy fight recording and print what happened. Python 3, standard library only.

  recview.py FILE [FILE ...]            header + summary of each recording
  recview.py FOLDER                     one line per recording in the folder (oldest first), then the summary of the newest
  recview.py FILE --timeline 5          the timeline every 5 seconds (default 10)
  recview.py FILE --json                the summary as JSON (for scripts)
  recview.py FILE --raw hdr|end|names   print one raw record

  recview.py FILE --episodes            the fights inside one recording (event NPCs such as the Celestial Pillars never count as a boss), each read by the result rule
  recview.py FILE --scan [--top 15]     suspicious moments: idle levies with enemies in range, stuck levies, one-hit deaths, shots appearing at the commander,
                                        levies far from the fight, damage outliers for one weapon (with a folder and --all: every recording)
  recview.py FILE --split OUTDIR        write each of those fights as its own .levyrec in OUTDIR (the original is never touched)

Reads .levyrec (gzip of JSON lines), .jsonl.gz and plain .jsonl. A file cut short by a crash (gzip not closed, no end record) is read as far
as it goes and says so. Positions are world pixels, times are seconds from the start of the recording (ticks / 60).
Format reference: the comment at the top of FightRecFormat.cs in the Levy mod.
"""
import argparse, glob, gzip, json, os, re, sys, zlib
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import recrules

TICKS = 60.0
# levy gear (lgear) slot names; 13 and 14 are the two mobility slots (a recording from before them has neither)
LSLOT = ["weapon", "sidearm", "head", "body", "legs", "accessory", "accessory", "accessory", "accessory", "accessory", "accessory", "ammo", "hook", "wings", "boots"]


# ------------------------------------------------------------------ reading
def open_lines(path):
    """Yield the JSON records of a file; sets truncated info in the returned list's .note."""
    with open(path, "rb") as f:
        magic = f.read(2)
    rows, note = [], None
    try:
        if magic == b"\x1f\x8b":
            with gzip.open(path, "rb") as g:
                for raw in g:
                    _take(rows, raw)
        else:
            with open(path, "rb") as g:
                for raw in g:
                    _take(rows, raw)
    except (EOFError, zlib.error, gzip.BadGzipFile, OSError) as e:
        note = "file ends early (%s): the recording was cut short, e.g. the game closed or crashed" % (type(e).__name__)
    return rows, note


def _take(rows, raw):
    raw = raw.strip()
    if not raw:
        return
    try:
        rows.append(json.loads(raw))
    except ValueError:
        pass  # a half-written last line


class Rec:
    def __init__(self, path):
        self.path = path
        self.rows, self.note = open_lines(path)
        self.hdr = next((r for r in self.rows if r.get("k") == "hdr"), {})
        self.end = next((r for r in reversed(self.rows) if r.get("k") == "end"), None)
        self.names = next((r for r in reversed(self.rows) if r.get("k") == "names"), {})
        self.frames = [r for r in self.rows if r.get("k") == "f"]
        self.events = [r for r in self.rows if r.get("k") == "e"]
        if self.end is None and self.note is None:
            self.note = "no end record: the recording was cut short"
        if self.end is None and not self.names:
            self.names = {}

    # name lookups ------------------------------------------------------
    def item(self, t):
        return self.names.get("items", {}).get(str(t), "item %s" % t)

    def npc(self, t):
        return self.names.get("npcs", {}).get(str(t), "npc %s" % t)

    def proj(self, t):
        return self.names.get("projectiles", {}).get(str(t), "projectile %s" % t)

    def soldier(self, i):
        return self.names.get("soldiers", {}).get(str(i), "levy %s" % i)


def fmt_t(ticks):
    s = ticks / TICKS
    return "%d:%02d" % (int(s) // 60, int(s) % 60)


def pct(a, b):
    return "%.0f%%" % (100.0 * a / b) if b else "0%"


# ------------------------------------------------------------------ analysis
def analyse(rec):
    h, e = rec.hdr, rec.end or {}
    out = {"file": os.path.basename(rec.path), "truncated": rec.note, "frames": len(rec.frames)}
    out["header"] = {k: h.get(k) for k in ("format", "date", "world", "player", "boss", "mode", "levy", "auto", "meta", "headless", "gzip")}
    out["mods"] = h.get("mods", [])
    c = recrules.classify(rec.rows)
    out["rule"] = {"result": c["result"], "outcome": c["outcome"], "deadAtEnd": c["deadAtEnd"], "lastDeathTick": c["lastDeath"], "eventNpcs": recrules.has_event_npcs(rec.rows) and not h.get("split")}
    cp = [len(f.get("c", [])) for f in rec.frames]
    out["commanderProjectiles"] = {"format11": any(cp), "framesWith": sum(1 for n in cp if n), "mostRows": max(cp) if cp else 0}
    if recrules.is_event_recording(h):
        out["event"] = recrules.classify_event(rec.rows)
        imp = recrules.importance_table(rec.rows)
        out["event"]["minibosses"] = sorted({rec.npc(t) for t, v in imp.items() if v == 1})
        out["event"]["bosses"] = sorted({rec.npc(t) for t, v in imp.items() if v == 2})
    out["army"] = h.get("army", {})
    out["commander"] = h.get("cmd", {})
    out["end"] = {k: e.get(k) for k in ("result", "outcome", "reason", "durationSec", "ticks", "frames", "events", "cmdDeaths", "levyDeaths", "levyHits",
                                        "armyAlive", "armyTotal", "bosses", "bossName", "dmgBoss", "dmgAll", "profiled", "dropped", "capped", "writeError")}
    # boss life over time
    tl = []
    boss_first, boss_min = {}, {}
    for f in rec.frames:
        t = f["t"]
        bosses = [b for b in f.get("b", []) if len(b) > 13 and b[13] == 0]
        life = sum(b[6] for b in bosses if b[7] > 0)
        mx = sum(b[7] for b in bosses if b[7] > 0)
        lev = f.get("l", [])
        p = f.get("p", [0] * 16)
        tl.append({"t": t, "boss": life, "bossMax": mx, "cmd": p[6], "levies": len(lev), "levyLife": sum(l[6] for l in lev), "levyMax": sum(l[10] for l in lev if len(l) > 10),
                   "adds": len(f.get("a", [])), "proj": len(f.get("j", [])), "flags": p[5]})
        for b in bosses:
            boss_first.setdefault(b[0], b[7])
    out["timeline"] = tl

    ev = defaultdict(list)
    for x in rec.events:
        ev[x.get("e")].append(x)

    # damage by owner and class
    dmg = defaultdict(lambda: [0, 0])      # (owner, class, target) -> dmg, hits
    src = defaultdict(lambda: [0, 0])      # (owner, kind, type, boss) -> dmg, hits
    for x in ev.get("dmg", []):
        for row in x.get("d", []):
            if len(row) < 7:
                continue
            owner, cls, kind, typ, boss, d, hits = row[:7]   # format 10 appends srcItem, ss, crits (and later formats may append more): only the first 7 are needed here
            dmg[(owner, cls, "boss" if boss else "add")][0] += d
            dmg[(owner, cls, "boss" if boss else "add")][1] += hits
            src[(owner, kind, typ, boss)][0] += d
            src[(owner, kind, typ, boss)][1] += hits
    out["damage"] = [{"owner": k[0], "class": k[1], "target": k[2], "damage": v[0], "hits": v[1]} for k, v in sorted(dmg.items(), key=lambda kv: -kv[1][0])]
    out["damageSources"] = [{"owner": k[0], "kind": k[1], "name": rec.item(k[2]) if k[1] == "item" else rec.proj(k[2]), "boss": bool(k[3]), "damage": v[0], "hits": v[1]}
                            for k, v in sorted(src.items(), key=lambda kv: -kv[1][0])]

    # commander hits and deaths
    ch = ev.get("hit", [])
    out["commanderHits"] = [{"t": x["t"], "dmg": x.get("dmg"), "by": rec.npc(x["npc"]) if x.get("npc", -1) > 0 else None, "proj": rec.proj(x["proj"]) if x.get("proj", 0) > 0 else None, "life": x.get("life")} for x in ch]
    out["commanderDeaths"] = [{"t": x["t"], "x": x.get("x"), "y": x.get("y")} for x in ev.get("cdeath", [])]
    ld = ev.get("ldeath", [])
    out["levyDeaths"] = [{"t": x["t"], "id": x["id"], "name": rec.soldier(x["id"]), "by": x.get("by"), "npc": rec.npc(x["npc"]) if x.get("npc", -1) > 0 else None} for x in ld]
    lh = ev.get("lhit", [])
    out["levyHitCount"] = len(lh)
    out["levyDamageTaken"] = sum(x.get("dmg", 0) for x in lh)
    byk = Counter()
    for x in lh:
        byk[(rec.npc(x["npc"]) if x.get("npc", -1) > 0 else "world", x.get("by"))] += x.get("dmg", 0)
    out["levyDamageBy"] = [{"source": k[0], "by": k[1], "damage": v} for k, v in byk.most_common(8)]

    # notable events
    notes = []
    for x in ev.get("boss", []):
        notes.append((x["t"], "boss appears: %s" % x.get("name")))
    for x in ev.get("bdeath", []) + ev.get("bdespawn", []):
        what = "killed" if recrules.end_is_kill(x, [d["t"] for d in ev.get("cdeath", [])]) else "left alive"
        extra = ""
        if "hn" in x:
            extra = ", last hit %s damage by %s (%s)" % (x.get("hd"), x.get("hn") or x.get("hs"), x.get("hs"))
        notes.append((x["t"], "%s %s at %s of %s life%s" % (x.get("name"), what, x.get("life"), x.get("max"), extra)))
    peak = max((m["bossMax"] for m in tl), default=0)
    seen = set()
    for m in tl:
        if not peak or not m["bossMax"]:
            continue
        frac = m["boss"] / float(m["bossMax"])
        for th in (0.75, 0.5, 0.25, 0.1):
            if frac <= th and th not in seen:
                seen.add(th)
                notes.append((m["t"], "boss at %d%% life" % int(th * 100)))
    for x in ev.get("cdeath", []):
        notes.append((x["t"], "the commander falls"))
    # clusters of levy deaths
    dts = sorted(x["t"] for x in ld)
    i = 0
    last_cluster = -10**9
    while i < len(dts):
        j = i
        while j < len(dts) and dts[j] - dts[i] <= 5 * TICKS:
            j += 1
        if j - i >= 4 and dts[i] - last_cluster > 5 * TICKS:
            notes.append((dts[i], "%d levies fall within 5 seconds" % (j - i)))
            last_cluster = dts[i]
        i = max(i + 1, j - 1) if j - i >= 4 else i + 1
    big = sorted(ch, key=lambda x: -x.get("dmg", 0))[:3]
    for x in big:
        notes.append((x["t"], "the commander takes %s damage from %s" % (x.get("dmg"), rec.npc(x["npc"]) if x.get("npc", -1) > 0 else (rec.proj(x["proj"]) if x.get("proj", 0) > 0 else "something"))))
    for x in ev.get("tgt", []):
        pass
    tk = Counter()
    for x in ev.get("tgt", []):
        tk[x.get("kind")] += 1
    out["bossTargetSwitches"] = {"none": tk.get(0, 0), "commander": tk.get(1, 0), "levy": tk.get(2, 0), "spite": tk.get(3, 0)}
    if rec.end:
        t = rec.end.get("target", {})
        out["bossTargetFrames"] = t
    notes.sort()
    out["notable"] = [{"t": t, "what": w} for t, w in notes]

    # profile
    pr = ev.get("prof", [])
    if pr:
        out["profile"] = analyse_profile(pr, tl)
    gear = [x for x in ev.get("gear", [])]
    out["commanderGearChanges"] = max(0, len(gear) - 1)
    # how many levies wear wings and boots in their mobility slots (the last lgear of each levy)
    last = {}
    for x in ev.get("lgear", []):
        last[x.get("id")] = x
    if last:
        slots = {i: {g[0] for g in x.get("g", [])} for i, x in last.items()}
        out["levyMobility"] = {"levies": len(last), "wings": sum(1 for s in slots.values() if 13 in s), "boots": sum(1 for s in slots.values() if 14 in s),
                               "both": sum(1 for s in slots.values() if 13 in s and 14 in s)}
    return out


# ------------------------------------------------------------------ profiler
def parse_top(text):
    """'combat 3.2, dodge 1.1' -> [('combat', 3.2), ('dodge', 1.1)] (ms over the window, summed over its updates)."""
    res = []
    for part in (text or "").split(","):
        part = part.strip()
        m = re.match(r"^(.*\S)\s+(-?[0-9.]+)$", part)
        if m:
            res.append((m.group(1), float(m.group(2))))
    return res


def analyse_profile(pr, tl):
    """One row per profiler window (a prof event is written when a window closes, t = its end), plus totals. Windows are 2 s in an automatic
    recording and 4 s when the player switched the profiler on; the length is the gap to the previous window."""
    pr = sorted(pr, key=lambda x: x.get("t", 0))
    rows, prev = [], 0
    for x in pr:
        t = x.get("t", 0)
        secs = x.get("win") or max(0.05, (t - prev) / TICKS)  # "win" = the window in real seconds (the game may run slower than 60 ticks per second)
        prev = t
        near = None
        if tl:
            near = min(tl, key=lambda m: abs(m["t"] - t))
        ups = x.get("ups", 0)
        rows.append({"t": t, "secs": secs, "fps": x.get("fps", 0), "ups": ups, "upd": x.get("upd", 0), "npc": x.get("npc", 0), "proj": x.get("proj", 0), "draw": x.get("draw", 0),
                     "ai": x.get("ai", 0), "gear": x.get("gear", 0), "updates": ups * secs, "top": parse_top(x.get("top", "")), "now": near})
    tot = sum(r["secs"] for r in rows) or 1.0
    totu = sum(r["updates"] for r in rows) or 1.0

    def wavg(k):
        return sum(r[k] * r["secs"] for r in rows) / tot

    def uavg(k):
        return sum(r[k] * r["updates"] for r in rows) / totu
    # biggest Levy sections over the whole fight, ms per update (the window figure is milliseconds summed over the window's updates)
    sums = defaultdict(float)
    for r in rows:
        for name, ms in r["top"]:
            sums[name] += ms
    costs = sorted(((n, v / totu) for n, v in sums.items()), key=lambda kv: -kv[1])
    worst = sorted(rows, key=lambda r: r["fps"])[:5]
    return {"windows": len(rows), "seconds": tot, "windowSecs": round(tot / len(rows), 2), "fps": wavg("fps"), "ups": wavg("ups"), "updateMs": uavg("upd"), "npcMs": uavg("npc"), "projMs": uavg("proj"),
            "drawMs": wavg("draw"), "levyAiMs": uavg("ai"), "gearMs": uavg("gear"), "worstUpdateMs": max(r["upd"] for r in rows), "lowestFps": min(r["fps"] for r in rows),
            "slowWindows": sum(1 for r in rows if r["fps"] < 30), "updateBound": sum(1 for r in rows if r["upd"] > 16.7), "drawBound": sum(1 for r in rows if r["draw"] > 16.7),
            "topCosts": [{"name": n, "msPerUpdate": round(v, 3)} for n, v in costs[:8]],
            "worst": [{"t": r["t"], "fps": r["fps"], "ups": r["ups"], "updateMs": r["upd"], "npcMs": r["npc"], "projMs": r["proj"], "drawMs": r["draw"], "levyAiMs": r["ai"],
                       "top": [[n, round(v / max(1.0, r["updates"]), 3)] for n, v in r["top"][:4]]} for r in worst],
            "rows": rows}


def why_slow(r):
    if r["upd"] > 16.7 and r["draw"] > 16.7:
        return "updates and drawing are both over 16.7 ms"
    if r["upd"] > 16.7:
        return "updates are the slow part (%.1f ms each, 60 per second needs under 16.7)" % r["upd"]
    if r["draw"] > 16.7:
        return "drawing is the slow part (%.1f ms per frame)" % r["draw"]
    return "neither updates (%.1f ms) nor drawing (%.1f ms) is over 16.7 ms, so the time went outside them (waiting on the graphics card or another program)" % (r["upd"], r["draw"])


def bar(v, top=60.0, width=24):
    n = int(round(width * max(0.0, min(v, top)) / top))
    return "#" * n + "." * (width - n)


def print_profile(a, rec, step):
    P = print
    pf = a["profile"]
    rows = pf["rows"]
    P("")
    mode = "profiled automatically with the recording" if (rec.hdr.get("auto") and pf["windowSecs"] < 3.2) else "the profiler was switched on by hand"
    P("PROFILER   %d windows of about %.0f s covering %s (%s). Frames per second is what the player sees; one update has 16.7 ms at 60 per second." % (
        pf["windows"], pf["windowSecs"], fmt_t(int(pf["seconds"] * TICKS)), mode))
    P("  whole fight:  %.1f fps on average, %.1f updates per second, lowest window %.1f fps; %d of %d windows under 30 fps (%d update-bound, %d draw-bound)" % (
        pf["fps"], pf["ups"], pf["lowestFps"], pf["slowWindows"], pf["windows"], pf["updateBound"], pf["drawBound"]))
    P("  per update:   %.1f ms (npcs %.1f, projectiles %.1f, the rest %.1f); worst window %.1f ms.  per frame draw: %.1f ms.  levy AI %.1f ms, gear upkeep %.1f ms" % (
        pf["updateMs"], pf["npcMs"], pf["projMs"], max(0.0, pf["updateMs"] - pf["npcMs"] - pf["projMs"]), pf["worstUpdateMs"], pf["drawMs"], pf["levyAiMs"], pf["gearMs"]))
    P("")
    P("  FPS OVER TIME (every %ss; mean fps in the span, lowest window in it, and the slowest part)" % step)
    P("  %6s  %5s %5s  %-24s  %7s %7s  %s" % ("time", "mean", "low", "0 ........ 60 fps", "upd ms", "draw ms", "state of the fight at the lowest window"))
    spans = defaultdict(list)
    for r in rows:
        spans[int((r["t"] / TICKS - 1e-6) // step)].append(r)
    for k in sorted(spans):
        g = spans[k]
        sec = sum(r["secs"] for r in g) or 1.0
        mean = sum(r["fps"] * r["secs"] for r in g) / sec
        low = min(g, key=lambda r: r["fps"])
        n = low["now"]
        st = ""
        if n:
            st = "boss %s, levies %d, adds %d, projectiles %d" % (pct(n["boss"], n["bossMax"]) if n["bossMax"] else "-", n["levies"], n["adds"], n["proj"])
        P("  %6s  %5.0f %5.0f  %s  %7.1f %7.1f  %s%s" % (fmt_t(g[-1]["t"]), mean, low["fps"], bar(mean), max(r["upd"] for r in g), max(r["draw"] for r in g), st, "  <" if low["fps"] < 30 else ""))
    P("")
    P("  WORST WINDOWS")
    for r in sorted(rows, key=lambda r: r["fps"])[:5]:
        n = r["now"]
        P("  %6s  %5.1f fps, %5.1f updates/s: %s" % (fmt_t(r["t"]), r["fps"], r["ups"], why_slow(r)))
        P("          update %.1f ms (npcs %.1f, projectiles %.1f), draw %.1f ms, levy AI %.1f ms%s" % (
            r["upd"], r["npc"], r["proj"], r["draw"], r["ai"], ("; levy costs per update: " + ", ".join("%s %.2f" % (nm, v / max(1.0, r["updates"])) for nm, v in r["top"][:4])) if r["top"] else ""))
        if n:
            P("          at that moment: %d levies standing, %d hostile adds, %d hostile projectiles, boss %s, commander %s" % (
                n["levies"], n["adds"], n["proj"], pct(n["boss"], n["bossMax"]) if n["bossMax"] else "-", n["cmd"]))
    P("")
    P("  TOP COSTS (ms per update, whole fight; a section can contain another, so they do not add up)")
    if pf["topCosts"]:
        for c in pf["topCosts"][:8]:
            P("    %-14s %6.2f ms   %s of the 16.7 ms budget" % (c["name"], c["msPerUpdate"], pct(c["msPerUpdate"], 16.7)))
    else:
        P("    no Levy section above 0.05 ms")



# ------------------------------------------------------------------ printing
def print_summary(rec, a, step):
    h, e = rec.hdr, rec.end or {}
    P = print
    P("=" * 78)
    P("%s" % a["file"])
    P("=" * 78)
    if a["truncated"]:
        P("!! %s" % a["truncated"])
    P("Recorded   %s   world %s   player %s" % (h.get("date", "?"), h.get("world", "?"), h.get("player", "?")))
    fight = e.get("bossName") or h.get("boss")
    if not fight or fight == "none":
        fight = (h.get("meta", {}).get("label") or "fight").replace("_", " ") + " (no boss)"
    P("Fight      %s   (%s)   difficulty %s" % (fight, "automatic, " + str(h.get("meta", {}).get("setting", "")) + ", started by " + str(h.get("meta", {}).get("trigger", "?")) if h.get("auto") else "started by hand or a harness", h.get("mode", "?")))
    ev12 = a.get("event")
    if ev12:
        P("Event      %s (%s)   result %s (%s)   duration %s   waves to %s   kills %s (minibosses %s)   levy deaths %s" % (ev12["name"], ", ".join(recrules.event_name(x) for x in ev12["seen"]), ev12["result"],
          ev12["reason"] or "cut short", fmt_t(e.get("ticks", 0)), ev12["waveMax"] or "-", ev12["kills"], ev12["minibossKills"], ev12["levyDeaths"]))
        if ev12["minibosses"] or ev12["bosses"]:
            P("            highlighted: %s" % ", ".join(ev12["bosses"] + ev12["minibosses"]))
        cpu = e.get("cpu") or {}
        if cpu:
            P("            recorder cost: %s us per tick on average, worst tick %s ms" % (cpu.get("meanUs"), cpu.get("maxMs")))
    res = e.get("result")
    P("Outcome    %s   (boss %s, %s)   duration %s   recorder format %s" % (res or "unknown", e.get("outcome", "?"), e.get("reason", "?"), fmt_t(e.get("ticks", 0)) if e else "?", h.get("format", "?")))
    rl = a.get("rule", {})
    if rl and e and not ev12 and res in ("won", "lost", "fled", "ended") and rl["result"] != res and not rl.get("eventNpcs"):
        P("            read by the result rule: %s (the file says %s)" % (rl["result"], res))
    if rl.get("eventNpcs"):
        P("            this recording holds event NPCs (Celestial Pillars and the like): run with --episodes to see the fights apart")
    cpj = a.get("commanderProjectiles", {})
    if cpj.get("format11"):
        P("Commander  own projectiles recorded in %d samples, at most %d at once (format 11 key c)" % (cpj["framesWith"], cpj["mostRows"]))
    P("Levy %s   %d mods: %s" % (h.get("levy", "?"), len(a["mods"]), ", ".join("%s %s" % (m[0], m[1]) for m in a["mods"])))
    army = a["army"]
    if army:
        cls = ", ".join("%s %d" % (k, v) for k, v in sorted(army.get("classes", {}).items(), key=lambda kv: -kv[1]))
        gr = ", ".join("%s %d" % (k, v) for k, v in army.get("grades", {}).items())
        P("Army       %s levies, average life %s   by weapon class: %s   grades: %s" % (army.get("n"), army.get("avgLife"), cls or "-", gr or "-"))
        wp = ", ".join("%s x%d" % (rec.item(t), n) for t, n in army.get("weapons", []))
        if wp:
            P("            main weapons: %s" % wp)
    lm = a.get("levyMobility")
    if lm:
        P("Mobility   %d levies seen: %d with wings, %d with boots, %d with both (the wings and boots slots, lgear 13 and 14)" % (lm["levies"], lm["wings"], lm["boots"], lm["both"]))
    c = a["commander"]
    if c:
        P("Commander  life %s  mana %s  defense %s" % (c.get("life"), c.get("mana"), c.get("defense")))
        P("            armor: %s" % (", ".join(rec.item(t) for t in c.get("armor", [])) or "-"))
        P("            accessories: %s" % (", ".join(rec.item(t) for t in c.get("accessories", [])) or "-"))
        P("            hotbar: %s" % (", ".join(rec.item(t) for t in c.get("hotbar", [])) or "-"))
    if e:
        P("End        commander deaths %s, levy deaths %s, levy hits taken %s, army alive %s of %s, boss damage %s, all damage %s%s%s" % (
            e.get("cmdDeaths"), e.get("levyDeaths"), e.get("levyHits"), e.get("armyAlive"), e.get("armyTotal"), e.get("dmgBoss"), e.get("dmgAll"),
            ", PROFILED" if e.get("profiled") else "", ", CAPPED (size limit)" if e.get("capped") else ""))
        if e.get("dropped"):
            P("            %s lines were dropped (the disk fell behind)" % e["dropped"])
        if e.get("writeError"):
            P("            write error: %s" % e["writeError"])

    tl = a["timeline"]
    P("")
    P("TIMELINE (every %ss)   boss life %% | commander life | levies standing, their life %% | hostile adds | hostile projectiles" % step)
    if tl:
        nxt = 0
        for m in tl:
            if m["t"] / TICKS + 1e-6 < nxt:
                continue
            nxt = (int(m["t"] / TICKS / step) + 1) * step
            bossp = pct(m["boss"], m["bossMax"]) if m["bossMax"] else "  - "
            ll = pct(m["levyLife"], m["levyMax"]) if m["levyMax"] else "-"
            P("  %5s   boss %5s  commander %4s   levies %3d (%4s)   adds %3d   proj %3d%s" % (fmt_t(m["t"]), bossp, m["cmd"], m["levies"], ll, m["adds"], m["proj"], "   [commander down]" if m["flags"] & 8192 else ""))
        m = tl[-1]
        P("  %5s   (last sample) boss %s  levies %d" % (fmt_t(m["t"]), pct(m["boss"], m["bossMax"]) if m["bossMax"] else "-", m["levies"]))
    else:
        P("  no samples")

    P("")
    P("DAMAGE DEALT (from the damage events; hits on the boss and its parts, and on its adds)")
    dm = a["damage"]
    if dm:
        tot = sum(d["damage"] for d in dm)
        P("  %-8s %-9s %-6s %12s %7s %8s" % ("by", "class", "on", "damage", "share", "hits"))
        for d in dm[:14]:
            P("  %-8s %-9s %-6s %12d %7s %8d" % (d["owner"], d["class"], d["target"], d["damage"], pct(d["damage"], tot), d["hits"]))
        byo = Counter()
        for d in dm:
            byo[d["owner"]] += d["damage"]
        P("  totals by who: " + ", ".join("%s %d (%s)" % (k, v, pct(v, tot)) for k, v in byo.most_common()))
        byc = Counter()
        for d in dm:
            if d["owner"] == "levy":
                byc[d["class"]] += d["damage"]
        if byc:
            P("  levy damage by class: " + ", ".join("%s %d" % (k, v) for k, v in byc.most_common()))
        P("  top weapons and projectiles:")
        for s in a["damageSources"][:10]:
            P("    %-6s %-5s %-28s %10d  %6d hits%s" % (s["owner"], s["kind"], s["name"][:28], s["damage"], s["hits"], "" if s["boss"] else "  (adds)"))
    else:
        P("  none recorded (older format, or nothing hit)")

    P("")
    P("TAKING DAMAGE")
    ch = a["commanderHits"]
    P("  commander: %d hits, %d damage, %d deaths" % (len(ch), sum(x["dmg"] or 0 for x in ch), len(a["commanderDeaths"])))
    for x in sorted(ch, key=lambda x: -(x["dmg"] or 0))[:5]:
        P("    %5s  %4s damage from %s  (life left %s)" % (fmt_t(x["t"]), x["dmg"], x["by"] or x["proj"] or "something", x["life"]))
    P("  levies: %d hits, %d damage, %d deaths" % (a["levyHitCount"], a["levyDamageTaken"], len(a["levyDeaths"])))
    for s in a["levyDamageBy"][:5]:
        P("    %-28s %-7s %7d damage" % (s["source"][:28], s["by"], s["damage"]))
    if a["levyDeaths"]:
        kc = Counter((x["npc"] or "world", x["by"]) for x in a["levyDeaths"])
        P("  killed by: " + ", ".join("%s x%d" % (k[0], v) for k, v in kc.most_common(6)))
        P("  first deaths: " + ", ".join("%s %s" % (fmt_t(x["t"]), x["name"]) for x in a["levyDeaths"][:6]))
    ts = a["bossTargetSwitches"]
    if ts and sum(ts.values()):
        P("  the boss changed target %d times: to the commander %d, to a levy %d, to none %d%s" % (sum(ts.values()), ts["commander"], ts["levy"], ts["none"], ", spite (redirected shots) %d" % ts["spite"] if ts.get("spite") else ""))
    bt = a.get("bossTargetFrames")
    if bt:
        tot = sum(bt.values()) or 1
        P("  boss target by sample: commander %s, levy %s, none %s%s" % (pct(bt.get("player", 0), tot), pct(bt.get("levy", 0), tot), pct(bt.get("none", 0), tot), ", spite (redirected shots) %s" % pct(bt["spite"], tot) if bt.get("spite") else ""))

    P("")
    P("NOTABLE")
    for n in a["notable"][:40]:
        P("  %5s  %s" % (fmt_t(n["t"]), n["what"]))
    if not a["notable"]:
        P("  nothing flagged")
    if "profile" in a:
        print_profile(a, rec, step)
    elif h.get("auto"):
        P("")
        P("PROFILER   no data (this recording was made without profiling: the Fight recorder option Profile recorded fights was off, or the recording is older than the option)")
    P("")



# ------------------------------------------------------------------ anomaly scan (--scan)
SCAN_ENEMY_TILES = 30        # an enemy this close counts as "in range"
SCAN_IDLE_SECS = 10          # a levy not engaging with an enemy in range this long
SCAN_STUCK_SECS = 15         # a levy that moved less than a tile this long while the commander moved 20+ tiles
SCAN_FAR_TILES = 150         # a regular levy this far from the commander and every enemy ...
SCAN_FAR_SECS = 20           # ... this long (60 s in an event recording) while the fight goes on
SCAN_ONEHIT = 0.9            # a death from one hit of at least 90 % of the levy's full life
SCAN_SPIKE = 10              # a weapon's damage per hit at least 10 times its own median (or the army's median weapon)
NOT_IDLE = {"Engage", "Shaken", "Routed", "Resupply", "Dead", "Haul", "Desert", "Mutiny", "ReturnHome"}


def _expand_s(frames):
    defs = {}
    out = []
    for f in frames:
        rows = []
        for r in f.get("s", []) or []:
            if len(r) >= 8:
                defs[r[1]] = r
                rows.append((r[2], r[3]))
            elif len(r) == 3 and r[0] in defs:
                rows.append((r[1], r[2]))
        out.append(rows)
    return out


def scan(rec, hz=2.0):
    """Suspicious moments in one recording: [dict(t, kind, what, score)], biggest score first. Frames are thinned to hz samples a second."""
    import bisect
    h = rec.hdr
    states = (h.get("stateNames") or "").split(",")
    event = recrules.is_event_recording(h)
    frames = rec.frames
    if not frames:
        return []
    segs = _expand_s(frames)
    dt = (frames[1]["t"] - frames[0]["t"]) if len(frames) > 1 else 2
    step = max(1, int(round(TICKS / hz / max(1, dt))))
    idx = list(range(0, len(frames), step))
    T = 16.0
    cell = SCAN_ENEMY_TILES * T
    R2 = cell ** 2
    out = []
    idle, far = {}, {}
    hist = defaultdict(list)          # levy id -> [(t, x, y)]
    lifemax = {}
    names = rec.soldier
    for n in idx:
        f = frames[n]
        t = f["t"]
        p = f.get("p", [0, 0])
        en = [(b[2], b[3]) for b in f.get("b", []) or []] + [(a[2], a[3]) for a in f.get("a", []) or []] + segs[n]
        boss_alive = any(len(b) > 13 and b[13] == 0 for b in f.get("b", []) or [])
        grid = defaultdict(list)
        for x, y in en:
            grid[(int(x // cell), int(y // cell))].append((x, y))
        for l in f.get("l", []) or []:
            lid, x, y = l[0], l[1], l[2]
            lifemax[lid] = max(lifemax.get(lid, 0), l[10] if len(l) > 10 else l[6])
            hist[lid].append((t, x, y))
            cx, cy = int(x // cell), int(y // cell)
            near = False
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    for (ex, ey) in grid.get((cx + dx, cy + dy), ()):
                        if (ex - x) ** 2 + (ey - y) ** 2 <= R2:
                            near = True
                            break
            st = states[l[7]] if 0 <= l[7] < len(states) else str(l[7])
            # 1. idle with an enemy in range
            if near and st not in NOT_IDLE and l[6] > 0:
                idle.setdefault(lid, [t, st])
            elif lid in idle:
                t0, st0 = idle.pop(lid)
                if t - t0 >= SCAN_IDLE_SECS * TICKS:
                    out.append({"t": t0, "kind": "idle", "levy": lid, "what": "%s stood %s for %s with an enemy within %d tiles" % (names(lid), st0, fmt_t(t - t0), SCAN_ENEMY_TILES), "score": (t - t0) / TICKS})
            # 5. a regular far from the fight
            if lid > 0 and (boss_alive or event) and en:
                dc = ((p[0] - x) ** 2 + (p[1] - y) ** 2) ** .5 / T
                de = min(((ex - x) ** 2 + (ey - y) ** 2) ** .5 for ex, ey in en) / T
                if dc > SCAN_FAR_TILES and de > SCAN_FAR_TILES:
                    far.setdefault(lid, [t, dc])
                elif lid in far:
                    t0, d0 = far.pop(lid)
                    if t - t0 >= (60 if event else SCAN_FAR_SECS) * TICKS:
                        out.append({"t": t0, "kind": "far", "levy": lid, "what": "%s was %.0f tiles from the commander and every enemy for %s" % (names(lid), d0, fmt_t(t - t0)), "score": (t - t0) / TICKS / 2})
    tend = frames[-1]["t"]
    for lid, (t0, st0) in idle.items():
        if tend - t0 >= SCAN_IDLE_SECS * TICKS:
            out.append({"t": t0, "kind": "idle", "levy": lid, "what": "%s stood %s for %s with an enemy within %d tiles (to the end)" % (names(lid), st0, fmt_t(tend - t0), SCAN_ENEMY_TILES), "score": (tend - t0) / TICKS})
    for lid, (t0, d0) in far.items():
        if tend - t0 >= (60 if event else SCAN_FAR_SECS) * TICKS:
            out.append({"t": t0, "kind": "far", "levy": lid, "what": "%s was %.0f tiles from the commander and every enemy for %s (to the end)" % (names(lid), d0, fmt_t(tend - t0)), "score": (tend - t0) / TICKS / 2})
    # 2. stuck: moved under a tile in SCAN_STUCK_SECS while the commander moved 20+ tiles
    ptr = [(frames[i]["t"], frames[i]["p"][0], frames[i]["p"][1]) for i in idx]
    pt = [x[0] for x in ptr]
    for lid, hs in hist.items():
        i0 = 0
        reported_until = -1
        for i1 in range(len(hs)):
            while hs[i1][0] - hs[i0][0] > SCAN_STUCK_SECS * TICKS:
                i0 += 1
            if hs[i1][0] - hs[i0][0] < SCAN_STUCK_SECS * TICKS - 2 * TICKS / hz or hs[i0][0] <= reported_until:
                continue
            xs = [q[1] for q in hs[i0:i1 + 1]]
            ys = [q[2] for q in hs[i0:i1 + 1]]
            if max(xs) - min(xs) > T or max(ys) - min(ys) > T:
                continue
            a, b = bisect.bisect_left(pt, hs[i0][0]), bisect.bisect_right(pt, hs[i1][0])
            seg = ptr[a:b]
            if not seg:
                continue
            cm = max(abs(q[1] - seg[0][1]) + abs(q[2] - seg[0][2]) for q in seg) / T
            if cm >= 20:
                out.append({"t": hs[i0][0], "kind": "stuck", "levy": lid, "what": "%s did not move a tile for %d s while the commander moved %.0f tiles" % (names(lid), SCAN_STUCK_SECS, cm), "score": SCAN_STUCK_SECS * 0.8})
                reported_until = hs[i1][0]
    # 3. one-hit deaths
    for x in rec.events:
        if x.get("e") == "lhit" and x.get("kill"):
            mx = x.get("max") or lifemax.get(x["id"], 0)
            if mx and x.get("dmg", 0) >= SCAN_ONEHIT * mx:
                src = rec.proj(x["proj"]) if x.get("proj", -1) > 0 else rec.npc(x["npc"]) if x.get("npc", -1) > 0 else "something"
                out.append({"t": x["t"], "kind": "onehit", "levy": x["id"], "what": "%s died to one hit of %s from %s (full life %s)" % (names(x["id"]), x.get("dmg"), src, mx), "score": 6})
    # 4. commander projectiles that appear at the commander while she is not using an item
    prev = {}
    cmdshots, first_t = Counter(), {}
    for f in frames:
        c = f.get("c")
        cnt = Counter(q[0] for q in c) if c else Counter()
        p = f.get("p", [])
        using = len(p) > 5 and int(p[5]) & 1024
        if c and not using:
            for q in c:
                if cnt[q[0]] > prev.get(q[0], 0) and (q[1] - p[0]) ** 2 + (q[2] - p[1]) ** 2 <= (3 * T) ** 2 and abs(q[3]) + abs(q[4]) > 4:
                    cmdshots[q[0]] += 1
                    first_t.setdefault(q[0], f["t"])
        prev = cnt
    for typ, k in cmdshots.items():
        if k >= 5:
            out.append({"t": first_t[typ], "kind": "cmdshot", "what": "%d %s shots appeared at the commander while she was not using an item" % (k, rec.proj(typ)), "score": min(20, 3 + k / 5)})
    # 6. damage outliers: a second whose damage per hit is SCAN_SPIKE times the weapon's median; and levy weapons far above the army's median weapon
    per = defaultdict(list)
    for x in rec.events:
        if x.get("e") != "dmg":
            continue
        for row in x.get("d", []):
            if len(row) < 7 or not row[6]:
                continue
            item = row[7] if len(row) > 7 and row[7] else (row[3] if row[2] == "item" else 0)
            per[(row[0], row[2], row[3], item)].append((x["t"], row[5] / row[6], row[5], row[6]))

    def wname(k):
        w = rec.item(k[2]) if k[1] == "item" else rec.proj(k[2])
        return (rec.item(k[3]) + " / " + w) if k[3] and k[1] != "item" else w
    meds = {}
    for k, v in per.items():
        ph = sorted(q[1] for q in v)
        meds[k] = ph[len(ph) // 2]
        if len(v) >= 5:
            for t, phit, dm, hits in v:
                if phit >= SCAN_SPIKE * max(1, meds[k]) and dm >= 500:
                    out.append({"t": t, "kind": "spike", "what": "%s (%s) did %d damage in %d hits, %.0f per hit, %.0fx its median" % (wname(k), k[0], dm, hits, phit, phit / max(1, meds[k])), "score": min(25, phit / max(1, meds[k]))})
    levy_w = [m for k, m in meds.items() if k[0] == "levy" and len(per[k]) >= 3]
    if len(levy_w) >= 4:
        am = sorted(levy_w)[len(levy_w) // 2]
        for k, m in meds.items():
            if k[0] == "levy" and len(per[k]) >= 3 and m >= SCAN_SPIKE * max(1, am):
                tot = sum(q[2] for q in per[k])
                out.append({"t": per[k][0][0], "kind": "weapon", "what": "levy weapon %s hits for %.0f (median per hit), %.0fx the army's median weapon (%.0f); %d damage in all" % (wname(k), m, m / max(1, am), am, tot), "score": min(25, m / max(1, am))})
    out.sort(key=lambda d: (-d["score"], d["t"]))
    return out


def print_scan(rec, top=15):
    found = scan(rec)
    kinds = Counter(d["kind"] for d in found)
    print("%s: %d suspicious moments (%s)" % (os.path.basename(rec.path), len(found), ", ".join("%s %d" % kv for kv in kinds.most_common()) or "none"))
    shown, per = 0, Counter()   # the top ones, at most 4 of each kind so one noisy kind does not hide the rest
    for d in found:
        if shown >= top:
            break
        if per[d["kind"]] >= 4:
            continue
        per[d["kind"]] += 1
        shown += 1
        print("  %7s  %-8s %s" % (fmt_t(d["t"]), d["kind"], d["what"]))
    return found


def list_folder(path):
    files = sorted(glob.glob(os.path.join(path, "*.levyrec")) + glob.glob(os.path.join(path, "*.jsonl.gz")) + glob.glob(os.path.join(path, "*.jsonl")), key=lambda f: os.path.basename(f))
    print("%d recordings in %s" % (len(files), path))
    tot = 0
    for f in files:
        size = os.path.getsize(f)
        tot += size
        print("  %-62s %8.1f KB" % (os.path.basename(f), size / 1024.0))
    print("  total %.1f MB" % (tot / 1048576.0))
    return files


def write_levyrec(path, rows):
    with gzip.open(path, "wt", encoding="utf-8") as g:
        for r in rows:
            g.write(json.dumps(r, separators=(",", ":")) + "\n")


def print_episodes(rec, split_dir):
    eps = recrules.episodes(rec.rows)
    base = os.path.basename(rec.path)
    end = rec.end or {}
    print("%s: file says %s; %d fight%s inside" % (base, end.get("result", "?"), len(eps), "" if len(eps) == 1 else "s"))
    for e in eps:
        if e.get("whole"):
            print("  whole file: %s (outcome %s, commander deaths %d)%s" % (e["result"], e["outcome"], e["cmdDeaths"], "" if e["result"] == end.get("result") else "  [the file says %s]" % end.get("result")))
            continue
        print("  %d/%d  %-34s %s to %s  %-6s outcome %-7s commander deaths %d%s" % (e["episode"], e["of"], e["name"][:34], fmt_t(e["start"]), fmt_t(e["end"]), e["result"], e["outcome"], e["cmdDeaths"], "  (dead when it left)" if e["deadAtEnd"] else ""))
        if split_dir:
            os.makedirs(split_dir, exist_ok=True)
            out = os.path.join(split_dir, recrules.file_name(base, e))
            write_levyrec(out, e["rows"])
            print("       wrote %s" % out)


def main():
    ap = argparse.ArgumentParser(description="Summarize Levy fight recordings.")
    ap.add_argument("paths", nargs="+")
    ap.add_argument("--episodes", action="store_true", help="list the fights inside each recording (event NPCs such as the Celestial Pillars are never a boss) with the result of each")
    ap.add_argument("--split", metavar="OUTDIR", help="with --episodes: write every fight as its own .levyrec into OUTDIR")
    ap.add_argument("--timeline", type=float, default=10, help="seconds between timeline rows")
    ap.add_argument("--json", action="store_true", help="print the summary as JSON")
    ap.add_argument("--raw", choices=("hdr", "end", "names"), help="print one raw record")
    ap.add_argument("--scan", action="store_true", help="list suspicious moments: idle levies with enemies in range, stuck levies, one-hit deaths, shots appearing at the commander, levies far from the fight, damage outliers")
    ap.add_argument("--top", type=int, default=15, help="with --scan: findings shown per recording")
    ap.add_argument("--all", action="store_true", help="with a folder: summarize every recording, not only the newest")
    a = ap.parse_args()
    files = []
    for p in a.paths:
        if os.path.isdir(p):
            fl = list_folder(p)
            files += fl if a.all else fl[-1:]
            print("")
        else:
            files.append(p)
    results = []
    for f in files:
        try:
            rec = Rec(f)
        except Exception as e:
            print("%s: cannot read (%s: %s)" % (f, type(e).__name__, e), file=sys.stderr)
            continue
        if a.raw:
            print(json.dumps({"hdr": rec.hdr, "end": rec.end, "names": rec.names}[a.raw], indent=1))
            continue
        if a.episodes or a.split:
            print_episodes(rec, a.split)
            continue
        if a.scan:
            found = print_scan(rec, a.top)
            if a.json:
                results.append({"file": os.path.basename(f), "findings": found})
            continue
        an = analyse(rec)
        results.append(an)
        if not a.json:
            print_summary(rec, an, int(a.timeline) if a.timeline == int(a.timeline) else a.timeline)
    if a.json:
        for r in results:
            r.pop("timeline", None)
            if "profile" in r:
                r["profile"].pop("rows", None)
        print(json.dumps(results if len(results) != 1 else results[0], indent=1, default=str))


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Read a Levy fight recording and print what happened. Python 3, standard library only.

  recview.py FILE [FILE ...]            header + summary of each recording
  recview.py FOLDER                     one line per recording in the folder (oldest first), then the summary of the newest
  recview.py FILE --timeline 5          the timeline every 5 seconds (default 10)
  recview.py FILE --json                the summary as JSON (for scripts)
  recview.py FILE --raw hdr|end|names   print one raw record

Reads .levyrec (gzip of JSON lines), .jsonl.gz and plain .jsonl. A file cut short by a crash (gzip not closed, no end record) is read as far
as it goes and says so. Positions are world pixels, times are seconds from the start of the recording (ticks / 60).
Format reference: the comment at the top of FightRecFormat.cs in the Levy mod.
"""
import argparse, glob, gzip, json, os, re, sys, zlib
from collections import Counter, defaultdict

TICKS = 60.0


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
            owner, cls, kind, typ, boss, d, hits = row
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
        what = "killed" if x["e"] == "bdeath" else "left alive"
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
    out["bossTargetSwitches"] = {"none": tk.get(0, 0), "commander": tk.get(1, 0), "levy": tk.get(2, 0)}
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
    res = e.get("result")
    P("Outcome    %s   (boss %s, %s)   duration %s   recorder format %s" % (res or "unknown", e.get("outcome", "?"), e.get("reason", "?"), fmt_t(e.get("ticks", 0)) if e else "?", h.get("format", "?")))
    P("Levy %s   %d mods: %s" % (h.get("levy", "?"), len(a["mods"]), ", ".join("%s %s" % (m[0], m[1]) for m in a["mods"])))
    army = a["army"]
    if army:
        cls = ", ".join("%s %d" % (k, v) for k, v in sorted(army.get("classes", {}).items(), key=lambda kv: -kv[1]))
        gr = ", ".join("%s %d" % (k, v) for k, v in army.get("grades", {}).items())
        P("Army       %s levies, average life %s   by weapon class: %s   grades: %s" % (army.get("n"), army.get("avgLife"), cls or "-", gr or "-"))
        wp = ", ".join("%s x%d" % (rec.item(t), n) for t, n in army.get("weapons", []))
        if wp:
            P("            main weapons: %s" % wp)
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
        P("  the boss changed target %d times: to the commander %d, to a levy %d, to none %d" % (sum(ts.values()), ts["commander"], ts["levy"], ts["none"]))
    bt = a.get("bossTargetFrames")
    if bt:
        tot = sum(bt.values()) or 1
        P("  boss target by sample: commander %s, levy %s, none %s" % (pct(bt.get("player", 0), tot), pct(bt.get("levy", 0), tot), pct(bt.get("none", 0), tot)))

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


def main():
    ap = argparse.ArgumentParser(description="Summarize Levy fight recordings.")
    ap.add_argument("paths", nargs="+")
    ap.add_argument("--timeline", type=float, default=10, help="seconds between timeline rows")
    ap.add_argument("--json", action="store_true", help="print the summary as JSON")
    ap.add_argument("--raw", choices=("hdr", "end", "names"), help="print one raw record")
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

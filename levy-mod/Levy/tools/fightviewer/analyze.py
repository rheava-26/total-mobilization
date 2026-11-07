#!/usr/bin/env python3
"""Analyze a Levy fight recording (.jsonl from /levyrec) and compare the player with the levies.

usage: analyze.py RECORDING.jsonl [--window TICKS] [--json]
  --window   ticks before a boss dash to inspect (default 30 = 0.5 s)
  --json     also dump the numbers as JSON on the last line

Definitions (same as viewer.html): grounded = recorded grounded flag (vertical velocity 0); still = speed under 0.3 px/tick (about 1.1 tiles/s);
speed in tiles/s = px/tick * 60 / 16; a jump from motion = grounded to airborne with vy < -1.5 px/tick (the only jump signal levies have; the
player also has real jump/dash events). A boss dash = the main boss's speed rises above max(10 px/tick, 3x its median speed) from below it.
"""
import json
import math
import statistics
import sys
from collections import Counter, defaultdict

TPS = 60
STILL = 0.3
JUMP_VY = -1.5


def load(path):
    rec = {"hdr": None, "arena": None, "frames": [], "events": [], "names": {}, "end": None}
    with open(path, "r", encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                o = json.loads(line)
            except ValueError:
                continue
            k = o.get("k")
            if k == "hdr":
                rec["hdr"] = o
            elif k == "arena":
                rec["arena"] = o
            elif k == "f":
                rec["frames"].append(o)
            elif k == "e":
                rec["events"].append(o)
            elif k == "names":
                rec["names"] = o
            elif k == "end":
                rec["end"] = o
    return rec


def nm(rec, tbl, i):
    return rec["names"].get(tbl, {}).get(str(i), "#%s" % i)


def main_boss(f):
    best = None
    for b in f.get("b", []):
        if not b[13]:
            return b
        if best is None:
            best = b
    return best


def summarize(samples):
    """samples: list of dict(t,vx,vy,grounded,d)"""
    n = len(samples)
    if not n:
        return None
    air = still = jumps = 0
    sp = []
    prev_g = True
    ds = []
    for s in samples:
        v = math.hypot(s["vx"], s["vy"])
        sp.append(v)
        if not s["grounded"]:
            air += 1
        if v < STILL:
            still += 1
        if prev_g and not s["grounded"] and s["vy"] < JUMP_VY:
            jumps += 1
        prev_g = s["grounded"]
        if s["d"] is not None:
            ds.append(s["d"])
    secs = n * 2.0 / TPS
    return {
        "samples": n, "secs": secs,
        "air_pct": 100.0 * air / n, "still_pct": 100.0 * still / n,
        "avg_speed": statistics.fmean(sp) * 60 / 16, "max_speed": max(sp) * 60 / 16,
        "jumps_per_min": jumps / secs * 60 if secs else 0.0,
        "dist_mean": statistics.fmean(ds) / 16 if ds else None,
        "dist_median": statistics.median(ds) / 16 if ds else None,
        "dist_min": min(ds) / 16 if ds else None,
    }


def build_series(rec):
    P, L = [], defaultdict(list)
    for f in rec["frames"]:
        b = main_boss(f)
        p = f["p"]
        P.append({"t": f["t"], "x": p[0], "y": p[1], "vx": p[2], "vy": p[3], "grounded": bool(int(p[5]) & 1),
                  "d": math.hypot(p[0] - b[2], p[1] - b[3]) if b else None, "flags": int(p[5])})
        for l in f.get("l", []):
            L[l[0]].append({"t": f["t"], "x": l[1], "y": l[2], "vx": l[3], "vy": l[4], "grounded": bool(int(l[5]) & 1),
                            "d": math.hypot(l[1] - b[2], l[2] - b[3]) if b else None, "state": int(l[7])})
    return P, L


def boss_dashes(rec):
    """Main boss speed spikes: list of (tick, bossx, bossy)."""
    series = []
    for f in rec["frames"]:
        b = main_boss(f)
        if b:
            series.append((f["t"], b[2], b[3], math.hypot(b[4], b[5])))
    if len(series) < 5:
        return []
    med = statistics.median(s[3] for s in series)
    thr = max(10.0, 3.0 * med)
    out, above = [], False
    for t, x, y, v in series:
        if v > thr and not above:
            out.append((t, x, y))
        above = v > thr
    return out


def at_or_before(series, t):
    best = None
    for s in series:
        if s["t"] <= t:
            best = s
        else:
            break
    return best


def window_behaviour(series, t0, W, bx, by, xjump_ticks=()):
    """What one entity did in [t0-W, t0]: jumped, speed, direction relative to the boss."""
    seg = [s for s in series if t0 - W <= s["t"] <= t0]
    if len(seg) < 2:
        return None
    jumped = False
    prev = seg[0]["grounded"]
    for s in seg[1:]:
        if prev and not s["grounded"] and s["vy"] < JUMP_VY:
            jumped = True
        prev = s["grounded"]
    air_frac = sum(1 for s in seg if not s["grounded"]) / len(seg)
    vx = statistics.fmean(s["vx"] for s in seg)
    spd = statistics.fmean(math.hypot(s["vx"], s["vy"]) for s in seg)
    last = seg[-1]
    to_boss = (bx - last["x"], by - last["y"])
    norm = math.hypot(*to_boss) or 1.0
    # radial speed: positive = moving toward the boss
    rad = statistics.fmean(((s["vx"] * to_boss[0] + s["vy"] * to_boss[1]) / norm) for s in seg)
    # horizontal direction relative to the boss's side: toward / away
    side = 1 if to_boss[0] >= 0 else -1
    horiz = "still" if abs(vx) < STILL else ("toward" if vx * side > 0 else "away")
    return {"jumped": jumped, "air_frac": air_frac, "speed": spd * 60 / 16, "radial": rad * 60 / 16, "horiz": horiz,
            "dist": norm / 16}


def pct(a, b):
    return "%5.1f" % (100.0 * a / b) if b else "    -"


def fmt(v, p=1):
    return "-" if v is None else ("%.*f" % (p, v))


def main(argv):
    if len(argv) < 2 or argv[1] in ("-h", "--help"):
        print(__doc__)
        return 2
    path = argv[1]
    W = 30
    dump = "--json" in argv
    if "--window" in argv:
        W = int(argv[argv.index("--window") + 1])
    rec = load(path)
    if not rec["frames"]:
        print("no frames in", path)
        return 1
    hdr = rec["hdr"] or {}
    frames = rec["frames"]
    dur = (frames[-1]["t"] - frames[0]["t"]) / TPS
    print("recording: %s" % path)
    print("boss %s | mode %s | Levy %s | player %s | %d samples, %.1f s%s" % (
        hdr.get("boss"), hdr.get("mode"), hdr.get("levy"), hdr.get("player"), len(frames), dur,
        (" | ended: " + rec["end"]["reason"]) if rec["end"] else ""))
    print("mods: " + ", ".join("%s %s" % (m[0], m[1]) for m in hdr.get("mods", [])))

    P, L = build_series(rec)
    ps = summarize(P)
    per = {i: summarize(s) for i, s in L.items()}
    per = {i: s for i, s in per.items() if s and s["secs"] >= 1.0}

    def lavg(k):
        vals = [s[k] for s in per.values() if s.get(k) is not None]
        return statistics.fmean(vals) if vals else None

    print("\n== player vs levy average (%d levies with at least 1 s on record) ==" % len(per))
    print("%-34s %10s %10s" % ("", "player", "levies"))
    for label, key in (("airborne %", "air_pct"), ("standing still %", "still_pct"), ("avg speed (tiles/s)", "avg_speed"),
                       ("max speed (tiles/s)", "max_speed"), ("jumps/min (from motion)", "jumps_per_min"),
                       ("distance to boss, mean (tiles)", "dist_mean"), ("distance to boss, median", "dist_median"),
                       ("closest approach to boss (tiles)", "dist_min")):
        print("%-34s %10s %10s" % (label, fmt(ps[key]), fmt(lavg(key))))

    ev = Counter()
    for e in rec["events"]:
        if e["e"] == "hook" and e.get("phase") != "launch":
            continue
        ev[e["e"]] += 1
    mins = max(dur / 60.0, 1e-9)
    print("\n== player actions (%.2f min) ==" % mins)
    for k, label in (("jump", "jumps"), ("xjump", "extra jumps"), ("dash", "dashes"), ("hook", "grapples"), ("use", "item uses"), ("hit", "hits taken")):
        print("%-14s %5d  (%.1f/min)" % (label, ev[k], ev[k] / mins))
    xj = Counter(e["name"] for e in rec["events"] if e["e"] == "xjump")
    if xj:
        print("extra jumps by kind: " + ", ".join("%s %d" % kv for kv in xj.most_common()))
    dt = Counter("%s/%s" % (e.get("type"), e.get("dashType")) for e in rec["events"] if e["e"] == "dash")
    if dt:
        print("dash types (dash/dashType): " + ", ".join("%s %d" % kv for kv in dt.most_common()))
    uses = Counter(nm(rec, "items", e["item"]) for e in rec["events"] if e["e"] == "use")
    if uses:
        print("items used: " + ", ".join("%s %d" % kv for kv in uses.most_common(8)))
    flies = [e for e in rec["events"] if e["e"] == "fly"]
    if flies:
        print("flight bursts: %d" % sum(1 for e in flies if e.get("on")))

    print("\n== hits taken by source ==")
    hits = defaultdict(lambda: [0, 0])
    for e in rec["events"]:
        if e["e"] != "hit":
            continue
        k = nm(rec, "npcs", e["npc"]) if e.get("npc", -1) >= 0 else (nm(rec, "projectiles", e["proj"]) if e.get("proj", 0) > 0 else "other")
        hits[k][0] += 1
        hits[k][1] += e.get("dmg", 0)
    if hits:
        for k, (n, d) in sorted(hits.items(), key=lambda kv: -kv[1][1]):
            print("  %-34s %3d hits %6d damage" % (k, n, d))
    else:
        print("  none")

    acc = [e for e in rec["events"] if e["e"] == "acc"]
    if acc:
        print("\n== equipped (last change %.1f s, %d change(s) after the start) ==" % (acc[-1]["t"] / TPS, len(acc) - 1))
        print("  " + ", ".join(nm(rec, "items", i) for i in acc[-1]["slots"] if i))

    # ---- behaviour before boss dashes
    dashes = boss_dashes(rec)
    print("\n== response before boss dashes (window %d ticks = %.2f s) ==" % (W, W / TPS))
    print("boss dashes detected: %d" % len(dashes))
    results = {"player": [], "levies": []}
    if dashes:
        player_dash_ticks = [e["t"] for e in rec["events"] if e["e"] == "dash"]
        player_xj_ticks = [e["t"] for e in rec["events"] if e["e"] == "xjump"]
        player_jump_ticks = [e["t"] for e in rec["events"] if e["e"] == "jump"]
        for (t0, bx, by) in dashes:
            r = window_behaviour(P, t0, W, bx, by)
            if r:
                r["dashed"] = any(t0 - W <= t <= t0 for t in player_dash_ticks)
                r["real_jump"] = any(t0 - W <= t <= t0 for t in player_jump_ticks + player_xj_ticks)
                results["player"].append(r)
            for lid, series in L.items():
                r = window_behaviour(series, t0, W, bx, by)
                if r:
                    results["levies"].append(r)
        for who in ("player", "levies"):
            rs = results[who]
            if not rs:
                print("%-7s no data" % who)
                continue
            n = len(rs)
            hz = Counter(r["horiz"] for r in rs)
            extra = ""
            if who == "player":
                extra = " | dashed %s%% | real jump event %s%%" % (pct(sum(r["dashed"] for r in rs), n).strip(), pct(sum(r["real_jump"] for r in rs), n).strip())
            print("%-7s n=%-4d jumped(from motion) %s%% | airborne %s%% | speed %.1f tiles/s | radial %+.1f (+ toward boss) | dist %.1f tiles | horizontal: %s%s" % (
                who, n, pct(sum(r["jumped"] for r in rs), n).strip(), "%.0f" % (100 * statistics.fmean(r["air_frac"] for r in rs)),
                statistics.fmean(r["speed"] for r in rs), statistics.fmean(r["radial"] for r in rs), statistics.fmean(r["dist"] for r in rs),
                ", ".join("%s %d%%" % (k, round(100 * v / n)) for k, v in hz.most_common()), extra))
        print("per dash (player): tick, jumped, dashed, horizontal, radial speed")
        for (t0, bx, by), r in zip(dashes, results["player"]):
            print("  t=%-6d %-6s %-6s %-7s %+5.1f" % (t0, "jump" if r["jumped"] or r["real_jump"] else "-", "dash" if r["dashed"] else "-", r["horiz"], r["radial"]))

    print("\n== read-out ==")
    notes = []
    if lavg("air_pct") is not None:
        notes.append("player is airborne %.0f%% of the time, levies %.0f%%" % (ps["air_pct"], lavg("air_pct")))
        notes.append("player stands still %.0f%%, levies %.0f%%" % (ps["still_pct"], lavg("still_pct")))
        notes.append("player averages %.1f tiles/s, levies %.1f" % (ps["avg_speed"], lavg("avg_speed")))
        if ps["dist_median"] is not None and lavg("dist_median") is not None:
            notes.append("player keeps a median %.1f tiles from the boss, levies %.1f" % (ps["dist_median"], lavg("dist_median")))
    notes.append("player dashes %.1f/min with no levy equivalent recorded" % (ev["dash"] / mins))
    for n in notes:
        print(" - " + n)

    if dump:
        print(json.dumps({"player": ps, "levy_avg": {k: lavg(k) for k in ("air_pct", "still_pct", "avg_speed", "max_speed", "jumps_per_min", "dist_mean", "dist_median", "dist_min")},
                          "events": dict(ev), "boss_dashes": len(dashes), "levies": len(per)}))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

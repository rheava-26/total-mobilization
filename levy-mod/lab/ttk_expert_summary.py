#!/usr/bin/env python3
"""Summaries for the hostile-levy / time-to-kill lab (CombatHarness.HostileLab.cs; warbands-mod/docs/TTK_EXPERT.md).
usage: ttk_expert_summary.py levy|enemy|npcdef|pvp|calib RUN_DIR [RUN_DIR ...]   (prints markdown tables)"""
import glob, json, statistics as st, sys, collections, os


def load(rundir):
    cells = {c["id"]: c for c in json.load(open(os.path.join(rundir, "cells.json")))["cells"]}
    rows = []
    for f in sorted(glob.glob(os.path.join(rundir, "jobs", "*", "try*", "out.tsv.hl.tsv"))):
        for l in open(f, errors="replace"):
            p = l.rstrip("\n").split("\t")
            if len(p) < 5: continue
            d = dict(kind=p[0], tag=p[1], seed=int(p[2]), s=p[3])
            fields = [x for x in p[4:] if "=" in x]
            d["pos"] = [x for x in p[4:] if "=" not in x]
            for x in fields:
                k, v = x.split("=", 1); d[k] = v
            d["cell"] = cells.get(p[1], {})
            rows.append(d)
    return rows


def fl(x, default=0.0):
    try: return float(x.split("/")[0])
    except Exception: return default


def mean(xs): return sum(xs) / len(xs) if xs else 0.0
def med(xs): return st.median(xs) if xs else None
def f1(x, n=1): return "-" if x is None else f"{x:.{n}f}"


def group(rows, key):
    g = collections.OrderedDict()
    for r in rows: g.setdefault(key(r), []).append(r)
    return g


def tp(r, k): return (r["cell"].get("tp") or {}).get(k)


def fight_stats(rs, window=30.0):
    """-> dict of per-cell stats over seeds."""
    n = len(rs); dead = [fl(r["ttk"]) for r in rs if fl(r["ttk"], -1) >= 0 and r["ttk"] != "-1"]
    secs = [fl(r["windowS"]) for r in rs]
    dps = [fl(r["dps"]) for r in rs]
    taken = [fl(r["taken"]) for r in rs]; landed = [fl(r["landed"]) for r in rs]; att = [fl(r["attempts"]) for r in rs]; shots = [fl(r["shots"]) for r in rs]
    return dict(n=n, kill=len(dead) / n if n else 0, ttk=med(dead), ttkall=med([fl(r["ttk"]) if r["ttk"] != "-1" else window for r in rs]),
                dps=mean(dps), hitdps=mean([t / max(0.5, s) for t, s in zip(taken, secs)]),
                hits_s=mean([l / max(0.5, s) for l, s in zip(landed, secs)]), att_s=mean([a / max(0.5, s) for a, s in zip(att, secs)]),
                meanhit=(sum(taken) / sum(landed)) if sum(landed) else 0, shots_s=mean([a / max(0.5, s) for a, s in zip(shots, secs)]),
                first=med([fl(r["firstHit"]) for r in rs if r["firstHit"] != "-1"]), df=mean([fl(r["def"]) for r in rs]),
                src=mean([fl(r.get("srcProj", "0")) for r in rs if fl(r.get("srcProj", "0"), 0) > 0] or [0]), srcm=mean([fl(r.get("srcMelee", "0")) for r in rs if fl(r.get("srcMelee", "0"), 0) > 0] or [0]))


def cmd_levy(rows):
    print("| stage | weapon | levies | arm | kills in 30 s | median TTK s | dps landed | hits/s landed | shots at player/s | mean landed hit | levy shot dmg (src) | player def |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|")
    g = group(rows, lambda r: (int(r["s"]), tp(r, "kind"), int(tp(r, "n") or 1), tp(r, "arm")))
    for (s, kind, n, arm), rs in sorted(g.items(), key=lambda x: (x[0][0], ["gun", "bow", "magic", "melee", "sniper"].index(x[0][1]), x[0][2], x[0][3])):
        x = fight_stats(rs)
        print(f"| {s} | {kind} ({tp(rs[0], 'w')}) | {n} | {arm} | {x['kill'] * 100:.0f}% ({len(rs)}) | {f1(x['ttk'])} | {x['dps']:.1f} | {x['hits_s']:.2f} | {x['att_s']:.2f} | {x['meanhit']:.1f} | {x['src'] if x['src'] else x['srcm']:.0f} | {x['df']:.0f} |")


def cmd_enemy(rows):
    print("| stage | enemy | n | arm | kills in 25 s | median TTK s | dps landed | hits/s | mean landed hit | npc.damage | hostile projectile (type:dmg x count) | player def |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|")
    g = group(rows, lambda r: (int(r["s"]), tp(r, "e"), int(tp(r, "n") or 1), tp(r, "arm")))
    for (s, e, n, arm), rs in sorted(g.items(), key=lambda x: (x[0][0], x[0][1], x[0][2], x[0][3])):
        x = fight_stats(rs, 25.0)
        pj = rs[0].get("hostileProj", "")
        print(f"| {s} | {e} | {n} | {arm} | {x['kill'] * 100:.0f}% ({len(rs)}) | {f1(x['ttk'])} | {x['dps']:.1f} | {x['hits_s']:.2f} | {x['meanhit']:.1f} | {rs[0].get('npcDamage')} | {pj} | {x['df']:.0f} |")


def cmd_npcdef(rows):
    print("| stage | class | weapon | hit | def | model | dps | per hit | hits/s | shots/s | TTK for life 100/300/1000/3000/band-shooter/band-melee |")
    print("|---|---|---|---|---|---|---|---|---|---|---|")
    for r in rows:
        if r["kind"] != "npcdef": continue
        print(f"| {r['s']} | {r['pos'][0]} | {r['pos'][1]} + {r['pos'][2]} | {r['hit']} | {r['def']} | {r['model']} | {r['dps']} | {r['perHit']} | {r['hitsPerSec']} | {r['shotsPerSec']} | {r['ttk']} |")


def cmd_pvp(rows):
    print("| stage | mode | weapon | hit | rule | i-frames | def | dps | per landed hit | landed/s | attempts/s | i-frame ticks after a hit |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|")
    for r in rows:
        if r["kind"] != "pvp": continue
        print(f"| {r['s']} | {r['gm']} | {r['pos'][0]} ({r['pos'][1]}) | {r['hit']} | {r['rule']} | {'off' if r['nif'] == '1' else 'on'} | {r['def']} | {r['dps']} | {r['perHit']} | {r['landed/s']} | {r['attempts/s']} | {r['immuneTicksAfterHit']} |")


def cmd_calib(rows):
    for r in rows:
        if r["kind"] == "calib" and r["pos"] and r["pos"][0] in ("direct", "proj"): print(r["pos"][0], {k: v for k, v in r.items() if k in ("mode", "dmg", "def", "endur", "taken", "immuneAfter")})


if __name__ == "__main__":
    what = sys.argv[1]; rows = []
    for d in sys.argv[2:]: rows += load(d)
    {"levy": cmd_levy, "enemy": cmd_enemy, "npcdef": cmd_npcdef, "pvp": cmd_pvp, "calib": cmd_calib}[what](rows)

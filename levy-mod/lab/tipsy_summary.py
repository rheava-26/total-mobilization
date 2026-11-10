#!/usr/bin/env python3
"""Pooled summaries of the tipsy lab runs (levy-mod/lab/specs/tipsy_*.yaml). Per weapon and condition: mean damage per levy-second over the seeds (a fight whose dps is negative, a dummy-life glitch, is dropped);
per weapon ratio against the baseline arm; then the mean of those ratios over the weapons of a group, so one lumpy weapon (the Sniper Rifle fires about 20 shots in a window) cannot decide a row.
  tipsy_summary.py main  RUN_DIR      sober vs Tipsy x1.6 by group x size x motion x distance
  tipsy_summary.py sweep RUN_DIR      damage kept vs aim-error multiplier at 25 tiles by group x size x motion
  tipsy_summary.py drinks RUN_DIR     the three drinks against sober at 25 tiles
"""
import csv, statistics, sys
from collections import defaultdict

GROUPS = {"gun": "shooters", "gun-hv": "shooters", "shotgun": "shooters", "bow": "shooters", "magic": "shooters", "beam": "beam swords", "melee": "true melee", "summon": "summoners"}

def load(run):
    meta = __import__("json").load(open(f"{run}/cells.json"))["cells"]; cells = {c["id"]: c["tp"] for c in meta if c.get("tp")}; rows = defaultdict(list)
    for r in csv.DictReader(open(f"{run}/results.csv")):
        t = cells.get(r["cell"])
        try: d = float(r["dps"])
        except (KeyError, ValueError): continue
        if t and d >= 0: rows[(t["kind"], t["w"] + "/" + str(t.get("a", "")), str(t["d"]), t["m"], t["z"], t["arm"])].append((d, float(r.get("shots") or 0), float(r.get("projHits") or 0)))
    return rows

def mean_dps(v): return statistics.mean(x[0] for x in v)

def pooled(rows, base, arm, keyf, groupf):
    """-> {group-key: (mean ratio over weapons, n weapons)}"""
    per = defaultdict(list)
    for (kind, w, d, m, z, a), v in rows.items():
        if a != arm: continue
        b = rows.get((kind, w, d, m, z, base))
        if not b or mean_dps(b) <= 0: continue
        per[(groupf(kind, w), keyf(d, m, z))].append(mean_dps(v) / mean_dps(b))
    return {k: (statistics.mean(v), len(v)) for k, v in per.items()}

def main():
    mode, run = sys.argv[1], sys.argv[2]; rows = load(run); pct = lambda x: f"{100 * x[0]:.0f}%"
    if mode == "main":
        res = pooled(rows, "sober", "tipsy", lambda d, m, z: (z, m, int(d)), lambda k, w: GROUPS[k])
        dists = sorted({k[1][2] for k in res}); print("Damage kept with Tipsy (x1.6 aim error), % of sober, mean over the weapons of a group\n")
        print("| group (weapons) | target | " + " | ".join(f"{d} tiles" for d in dists) + " |"); print("|---|---|" + "---|" * len(dists))
        for g in ["shooters", "beam swords", "true melee", "summoners"]:
            for z in ("small", "boss"):
                for m in ("static", "strafe"):
                    cells = [res.get((g, (z, m, d))) for d in dists]
                    if any(cells): print(f"| {g} ({cells[0][1] if cells[0] else '?'}) | {z} {m} | " + " | ".join(pct(c) if c else "-" for c in cells) + " |")
    elif mode == "sweep":
        arms = []
        for k in rows:
            if k[5] not in arms: arms.append(k[5])
        arms.sort(key=lambda a: float(a[1:]))
        print("Damage kept at 25 tiles vs the aim-error multiplier, % of x1, mean over the weapons of a group\n")
        print("| group | target | " + " | ".join(arms[1:]) + " |"); print("|---|---|" + "---|" * (len(arms) - 1))
        tab = {a: pooled(rows, arms[0], a, lambda d, m, z: (z, m), lambda k, w: GROUPS[k] if GROUPS[k] != "beam swords" else "beam: " + w.split("/")[0]) for a in arms[1:]}
        keys = sorted({k for a in tab for k in tab[a]}, key=lambda k: (k[0], k[1]))
        for k in keys: print(f"| {k[0]} | {k[1][0]} {k[1][1]} | " + " | ".join(pct(tab[a][k]) if k in tab[a] else "-" for a in arms[1:]) + " |")
        # all shooters, both sizes and motions
        print("\nShooters pooled over size and motion (small = zombie hitbox, boss = Eye-sized): ")
        for z in ("small", "boss", "both"):
            vals = []
            for a in arms[1:]:
                xs = [tab[a][k][0] for k in tab[a] if k[0] == "shooters" and (z == "both" or k[1][0] == z)]
                vals.append(f"{a} {100 * statistics.mean(xs):.0f}%" if xs else f"{a} -")
            print(f"- {z}: " + ", ".join(vals))
    elif mode == "drinks":
        arms = []
        for k in rows:
            if k[5] not in arms: arms.append(k[5])
        print("Damage with each drink, % of sober, at 25 tiles on a still small target, mean over the weapons of a group\n")
        print("| group | " + " | ".join(a for a in arms[1:]) + " |"); print("|---|" + "---|" * (len(arms) - 1))
        tab = {a: pooled(rows, arms[0], a, lambda d, m, z: 0, lambda k, w: GROUPS[k]) for a in arms[1:]}
        for g in ["shooters", "beam swords", "true melee", "summoners"]:
            print(f"| {g} | " + " | ".join(pct(tab[a][(g, 0)]) if (g, 0) in tab[a] else "-" for a in arms[1:]) + " |")

main()

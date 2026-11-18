#!/usr/bin/env python3
"""gearcost.py RUN [RUN ...]  -- the gear-cost table of the lagsweep lab (LEVY_GEARCOST=1): what each levy's puppet player costs through every mod's equipment hooks, in microseconds per levy per tick.

Reads <job>/out.tsv.gearcost.tsv (key, ticks, levies, method, excl ms/tick, incl ms/tick, calls/tick) and the cell names (meta=) from cells.json. Per arm: the mean over its fights.
Prints, per arm: the frame totals (inclusive), the cost by mod (exclusive, summed), and the costliest methods (exclusive), each as us per levy per tick and calls per levy per tick."""
import sys, os, re, json, glob, collections

def arm_of(rd):
    m = {}
    for c in json.load(open(os.path.join(rd, "cells.json")))["cells"]:
        mm = re.search(r"meta=(\w+)", c.get("name", "")); m[c["id"]] = mm[1] if mm else c["id"]
    return m

arms = collections.defaultdict(lambda: collections.defaultdict(lambda: [0.0, 0.0, 0.0])); nfights = collections.Counter(); lev = collections.defaultdict(list)
for rd in sys.argv[1:]:
    am = arm_of(rd)
    for f in glob.glob(os.path.join(rd, "jobs", "*", "try*", "out.tsv.gearcost.tsv")):
        seen = set()
        for line in open(f):
            k, ticks, levies, name, ex, inc, calls = line.rstrip("\n").split("\t")
            cid = re.search(r"\|t(c\d+)", k); arm = am.get(cid[1], cid[1]) if cid else "?"
            if k not in seen: seen.add(k); nfights[arm] += 1; lev[arm].append(float(levies))
            a = arms[arm][name]; L = float(levies)
            a[0] += float(ex) * 1000 / L; a[1] += float(inc) * 1000 / L; a[2] += float(calls) / L

for arm, rows in arms.items():
    n = nfights[arm]; levies = sum(lev[arm]) / len(lev[arm])
    R = {k: [v / n for v in vals] for k, vals in rows.items()}
    print(f"\n### {arm}: {n} fights, {levies:.1f} levies alive on average (us per levy per tick; calls per levy per tick)\n")
    print("| frame step (inclusive) | us | calls |\n|---|---|---|")
    for k in ["Levy:frame.AIBody", "Levy:frame.GearFrame", "Levy:frame.RefreshStats", "Levy:frame.DebuffDot", "Levy:frame.GearOnHurt", "Terraria:Player.ResetEffects", "Terraria:Player.UpdateEquips", "Terraria:Player.UpdateArmorSets", "Terraria:Player.UpdateBuffs"]:
        if k in R: print(f"| {k} | {R[k][1]:.1f} | {R[k][2]:.2f} |")
    bymod = collections.Counter()
    for k, v in R.items():
        if k.startswith("Levy:frame") or k.startswith("Terraria:"): continue
        bymod[k.split(":")[0]] += v[0]
    print("\n| mod (exclusive, all its instrumented hooks) | us |\n|---|---|")
    for m, v in bymod.most_common(8): print(f"| {m} | {v:.1f} |")
    print("\n| method (exclusive) | us | inclusive us | calls |\n|---|---|---|---|")
    for k, v in sorted(R.items(), key=lambda kv: -kv[1][0])[:28]: print(f"| {k} | {v[0]:.2f} | {v[1]:.2f} | {v[2]:.2f} |")

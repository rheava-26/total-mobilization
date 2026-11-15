#!/usr/bin/env python3
"""cmp_results.py NAME=RESULTS_CSV [...]  [--cell c0]: win count, means of the damage / survival columns per arm (fights restricted to seeds in range via --seeds A-B)."""
import csv, statistics as st, sys, argparse

ap = argparse.ArgumentParser()
ap.add_argument("arms", nargs="+")
ap.add_argument("--seeds", default="1-1000")
a = ap.parse_args()
lo, hi = map(int, a.seeds.split("-"))
COLS = ["avgDef", "avgHp", "potionsDrunk", "dpsHittable", "halfSec", "avgSurvSec", "dmgBoss", "sec", "deaths", "hitsTaken", "wingHoldSec", "wingRelSec", "takenOtherContact", "takenProj"]


def m(rs, c):
    v = [float(r[c]) for r in rs if r.get(c) not in ("", None, "-")]
    return st.mean(v) if v else float("nan")


for spec in a.arms:
    name, path = spec.split("=", 1)
    rs = [r for r in csv.DictReader(open(path)) if lo <= int(r["seed"]) <= hi and r["result"] not in ("MISSING", "ERROR")]
    w = sum(r["result"] == "WIN" for r in rs)
    print(f"{name:12s} n={len(rs)} wins={w} ({100*w/max(1,len(rs)):.0f}%) " + " ".join(f"{c}={m(rs, c):.0f}" for c in COLS))

#!/usr/bin/env python3
"""seedcorr.py A=csv B=csv ... : per-seed agreement between arms (is a fight with a given seed repeatable / correlated across arms?)"""
import csv, sys, statistics as st, itertools


def load(p):
    return {int(r["seed"]): r for r in csv.DictReader(open(p)) if r.get("result") in ("WIN", "LOSS") and (not r.get("cell") or r["cell"] in ("c0", "baseline"))}


def corr(a, b):
    ma, mb = st.mean(a), st.mean(b)
    d = (sum((x - ma) ** 2 for x in a) * sum((y - mb) ** 2 for y in b)) ** 0.5
    return sum((x - ma) * (y - mb) for x, y in zip(a, b)) / d if d else float("nan")


arms = {}
for s in sys.argv[1:]:
    n, p = s.split("=", 1)
    arms[n] = load(p)
for (n1, a), (n2, b) in itertools.combinations(arms.items(), 2):
    seeds = sorted(set(a) & set(b))
    same = sum(a[s]["result"] == b[s]["result"] for s in seeds)
    x = [float(a[s]["bossLeft"]) for s in seeds]
    y = [float(b[s]["bossLeft"]) for s in seeds]
    print(f"{n1} vs {n2}: n={len(seeds)} same result {same}  corr(bossLeft)={corr(x, y):.2f}")

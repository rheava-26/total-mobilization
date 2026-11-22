#!/usr/bin/env python3
"""Checks that a NEW pre-hardmode run carried the same army as lab 1, seed for seed: the class:weapon list of every levy (the perLevy column of the
harness row) is compared with every lab-1 raw row of the same boss, mode, levy count and seed.
  check_armies.py RUN_DIR"""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import compare, old_prehm


def wl(row): return [":".join(p.split(":")[:2]) for p in row["perLevy"].split(";") if p]


old, _ = old_prehm.rows(); same = diff = 0; bad = []
for c, r in compare.run_rows(sys.argv[1]):
    k = (r["boss"], r["mode"], int(r["n"]))
    for o in old.get(k, []):
        if o["seed"] != r["seed"]: continue
        if wl(o) == wl(r): same += 1
        else: diff += 1; bad.append((k, r["seed"], o["_file"], wl(o), wl(r)))
print(f"same army: {same} pairs, different: {diff}")
for b in bad[:10]: print(" ", b)

#!/usr/bin/env python3
"""perf_pairs.py RUN_DIR BASE_CELL OTHER_CELL  -- paired per-seed comparison of two cells of a perf run (the same seeds are the same army in both cells).
For every seed: CPU ms per tick, the projectile hit loop (Projectile.Damage, ms per tick), ticks over 22 ms after 10 s, shot hits per tick, pairs the mods were still asked about, pairs culled.
Then the median / mean of OTHER - BASE and how many seeds improved. Reads the same logs as lab/perf_summary.py (which it runs)."""
import sys, os, io, re, runpy, statistics, contextlib

run, a, b = sys.argv[1:4]
sys.argv = [os.path.join(os.path.dirname(os.path.abspath(__file__)), "perf_summary.py"), run]
with contextlib.redirect_stdout(io.StringIO()):
    g = runpy.run_path(sys.argv[0], run_name="perf_summary")
rows = g["rows"]

def grab(prof, key):
    m = re.search(re.escape(key) + r"=([0-9.]+)", prof)
    return float(m[1]) if m else 0.0

def table(cell):
    t = {}
    for sd, d in rows[cell]:
        p = d["prof"]
        t[sd] = dict(cpu=d["cpu"], dmg=grab(p, "damage ms/tick"), spikes=d["spikes"], hits=grab(p, "hits/tick"), pairs=grab(p, "pairs/tick"), culled=grab(p, "culled/tick"), proj=d["cpup"], npc=d["cpun"])
    return t

A, B = table(a), table(b)
seeds = sorted(set(A) & set(B))
cols = [("cpu", "CPU ms/tick"), ("dmg", "hit loop ms/tick"), ("proj", "projectiles ms/tick"), ("spikes", "ticks > 22 ms"), ("hits", "hits/tick"), ("pairs", "pairs asked/tick"), ("culled", "culled/tick")]
print(f"| seed | " + " | ".join(f"{n} {a} / {b}" for _, n in cols) + " |")
print("|---|" + "---|" * len(cols))
for s in seeds:
    print(f"| {s} | " + " | ".join(f"{A[s][k]:.2f} / {B[s][k]:.2f}" for k, _ in cols) + " |")
print()
print(f"| metric ({len(seeds)} paired seeds) | {a} median | {b} median | {b} - {a} mean | {b} better on |")
print("|---|---|---|---|---|")
for k, n in cols:
    xa, xb = [A[s][k] for s in seeds], [B[s][k] for s in seeds]
    d = [y - x for x, y in zip(xa, xb)]
    better = sum(1 for v in d if v < 0)
    print(f"| {n} | {statistics.median(xa):.2f} | {statistics.median(xb):.2f} | {statistics.mean(d):+.2f} ({statistics.mean(d) / statistics.mean(xa) * 100 if statistics.mean(xa) else 0:+.0f}%) | {better}/{len(seeds)} |")

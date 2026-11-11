#!/usr/bin/env python3
# paired.py BEFORE AFTER GEAR : per run (course start), arrivals, mean trip time, mean max depth, mean depth; plus the totals over runs where BOTH builds had >= half of the levies arrive
import csv, glob, sys, os
b, a, gear = sys.argv[1:4]
def load(pre):
    d = {}
    for f in sorted(glob.glob(f'/home/user/lab/t6/res/{pre}_*.csv')):
        run = os.path.basename(f)[len(pre) + 1:-4]
        rows = [r for r in csv.DictReader(open(f)) if (('wings' if r['gear'].startswith('w:') else r['gear']) == gear)]
        if rows: d[run] = rows
    return d
def fl(r, k):
    try: return float(r[k])
    except Exception: return 0.0
B, A = load(b), load(a)
tot = {'b': [0, 0, 0, 0, 0, 0.0, 0.0, 0.0], 'a': [0, 0, 0, 0, 0, 0.0, 0.0, 0.0]}
print(f"{'run':14} {'arr b/a':>9} {'time b/a':>12} {'maxDep b/a':>12} {'meanDep b/a':>12} {'fly% b/a':>10}")
for run in sorted(set(B) & set(A)):
    rb, ra = B[run], A[run]
    ab = [r for r in rb if r['arrived'] == '1']; aa = [r for r in ra if r['arrived'] == '1']
    f = lambda rows, k: sum(fl(r, k) for r in rows) / max(1, len(rows))
    tb = f(ab, 'arriveSec'); ta = f(aa, 'arriveSec')
    print(f"{run:14} {len(ab)}/{len(rb)} {len(aa)}/{len(ra)} {tb:6.0f}/{ta:5.0f} {f(rb,'maxDepth'):6.1f}/{f(ra,'maxDepth'):5.1f} {f(rb,'meanDepth'):6.1f}/{f(ra,'meanDepth'):5.1f} {f(rb,'flyShare')*100:5.0f}/{f(ra,'flyShare')*100:4.0f}")
    if len(ab) * 2 >= len(rb) and len(aa) * 2 >= len(ra):
        for k, rows in (('b', rb), ('a', ra)):
            t = tot[k]; t[0] += 1; t[1] += len(rows); t[2] += len([r for r in rows if r['arrived'] == '1'])
            t[3] += sum(fl(r, 'arriveSec') for r in rows if r['arrived'] == '1'); t[5] += sum(fl(r, 'maxDepth') for r in rows); t[6] += sum(fl(r, 'meanDepth') for r in rows); t[7] += sum(fl(r, 'flyShare') for r in rows)
for k in ('b', 'a'):
    t = tot[k]
    if t[1]: print(f"BOTH-HALF {k}: runs={t[0]} levies={t[1]} arrived={t[2]} trip={t[3] / max(1, t[2]):.0f}s maxDepth={t[5] / t[1]:.1f} meanDepth={t[6] / t[1]:.1f} fly={t[7] / t[1] * 100:.0f}%")

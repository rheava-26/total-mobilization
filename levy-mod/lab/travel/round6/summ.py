#!/usr/bin/env python3
# summ.py : markdown before/after table for the travel5 lab (round 6)
import csv, glob, sys
PAIRS = [
    ('lips course, bare (12 seeds x 6)', 'c_lipbo', 'q_lipbo', 'none'),
    ('lips course, Hermes Boots (12 x 6)', 'c_lipb', 'q_lipb', 'boots'),
    ('lips course, wings (12 x 8)', 'c_lipw', 'v_lipw', 'wings'),
    ('ravine trek, wings (12 x 8)', 'b_ravw', 'v_ravw', 'wings'),
    ('ravine trek (valleys, hills), boots (12 x 6)', 'e_ravb', 'q_ravb', 'boots'),
    ('real worlds 800 tiles, wings (6 worlds x 4 starts x 8)', 'b_wrw', 'v_wrw', 'wings'),
    ('real worlds 800 tiles, boots (6 x 4 x 6)', 'b_wrb', 'q_wrb', 'boots'),
]
def load(pre, gear):
    rows = []
    for f in sorted(glob.glob(f'/home/user/lab/t6/res/{pre}_*.csv')):
        for r in csv.DictReader(open(f)):
            g = 'wings' if r['gear'].startswith('w:') else r['gear']
            if g == gear: rows.append(r)
    return rows
def fl(r, k):
    try: return float(r[k])
    except Exception: return 0.0
def stats(rows):
    n = len(rows)
    if n == 0: return None
    ar = [r for r in rows if r['arrived'] == '1']
    t = sum(fl(r, 'arriveSec') for r in ar) / max(1, len(ar))
    md = sorted(fl(r, 'maxDepth') for r in rows)
    return dict(n=n, arr=len(ar), t=t, md=sum(md) / n, p90=md[min(n - 1, int(n * 0.9))], mean=sum(fl(r, 'meanDepth') for r in rows) / n,
                fly=sum(fl(r, 'flyShare') for r in rows) / n * 100, air=sum(fl(r, 'airShare') for r in rows) / n * 100,
                se=int(sum(fl(r, 'stuckEps') for r in rows)), sl=sum(1 for r in rows if fl(r, 'stuckEps') > 0))
print('| set | arrive | trip s (arrivers) | max depth mean / p90 (tiles) | mean depth | thrust share | stuck episodes (levies) |')
print('|---|---|---|---|---|---|---|')
for name, b, a, gear in PAIRS:
    sb, sa = stats(load(b, gear)), stats(load(a, gear))
    if not sb or not sa: print(f'| {name} | (missing {b if not sb else a}) | | | | | |'); continue
    print(f"| {name} | {sb['arr']}/{sb['n']} -> {sa['arr']}/{sa['n']} | {sb['t']:.0f} -> {sa['t']:.0f} | {sb['md']:.1f} / {sb['p90']:.0f} -> {sa['md']:.1f} / {sa['p90']:.0f} | {sb['mean']:.1f} -> {sa['mean']:.1f} | {sb['fly']:.0f}% -> {sa['fly']:.0f}% | {sb['se']} ({sb['sl']}) -> {sa['se']} ({sa['sl']}) |")

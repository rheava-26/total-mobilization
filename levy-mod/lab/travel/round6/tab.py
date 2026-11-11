#!/usr/bin/env python3
# tab.py TAGPREFIX...  : per-gear summary over all res/<prefix>*.csv ; -v prints each levy; -e N prints events head
import csv, glob, sys, gzip, collections
args = [a for a in sys.argv[1:] if not a.startswith('-')]
verbose = '-v' in sys.argv
rows = []
for pre in args:
    for f in sorted(glob.glob(f'/home/user/lab/t6/res/{pre}*.csv')):
        for r in csv.DictReader(open(f)):
            r['run'] = f.split('/')[-1][:-4]
            rows.append(r)
def num(r, k, d=0.0):
    try: return float(r[k])
    except Exception: return d
by = collections.defaultdict(list)
for r in rows:
    g = r['gear']
    if g.startswith('w:'): g = 'wings'
    by[g].append(r)
print(f"{'gear':8} {'n':>4} {'arrive':>8} {'time_s':>7} {'maxDep':>7} {'p90Dep':>7} {'meanDep':>7} {'fly%':>6} {'air%':>6} {'stuckEps':>8} {'stuckLev':>8} {'falls':>6}")
for g, rs in sorted(by.items()):
    n = len(rs); ar = sum(1 for r in rs if r['arrived'] == '1')
    t = [num(r, 'arriveSec') for r in rs if r['arrived'] == '1']
    md = sorted(num(r, 'maxDepth') for r in rs)
    meand = sum(num(r, 'meanDepth') for r in rs) / n
    fly = sum(num(r, 'flyShare') for r in rs) / n * 100; air = sum(num(r, 'airShare') for r in rs) / n * 100
    se = sum(num(r, 'stuckEps') for r in rs); sl = sum(1 for r in rs if num(r, 'stuckEps') > 0)
    falls = sum(num(r, 'falls') for r in rs)
    print(f"{g:8} {n:4d} {ar:4d}/{n:<3d} {sum(t)/max(1,len(t)):7.1f} {sum(md)/n:7.1f} {md[int(n*0.9)-1 if n>1 else 0]:7.1f} {meand:7.1f} {fly:6.1f} {air:6.1f} {se:8.0f} {sl:8d} {falls:6.0f}")
if verbose:
    for r in rows:
        print(r['run'], r['name'], r['gear'], r['arrived'], r['arriveSec'], 'stuck', r['stuckEps'], r['stuckWhere'], 'end', r['endOffset'], r['endFeature'], 'dep', r['maxDepth'], 'fly', r['flyShare'])

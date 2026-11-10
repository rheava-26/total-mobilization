#!/usr/bin/env python3
# regtab.py TAGA TAGB : arrivals (arrived/levies), bodies lost, lava entries, hook casts per set of runs, two tags side by side
import csv, glob, sys, os, collections
def load(tag):
    out = collections.OrderedDict()
    for f in sorted(glob.glob(f'/home/user/lab/t5/res/{tag}_*.csv')):
        name = os.path.basename(f)[len(tag) + 1:-4]
        grp = 'worlds-hook-far' if name.startswith('hfar') else 'worlds-hook-near' if name.startswith('hnear') else 'zoo/corr hooks' if name.startswith(('cor_', 'zoo_')) and not name.startswith('nh_') else 'nh courses mixed' if name.startswith('nh_') else 'other'
        rows = list(csv.DictReader(open(f)))
        a = out.setdefault(grp, [0, 0, 0, 0, 0, 0])
        for r in rows:
            a[0] += 1; a[1] += int(r['arrived']); a[2] += int(r['bodyGone']); a[3] += int(r['lavaEntries']); a[4] += int(r['hkCasts']); a[5] += float(r['stuckSec'] or 0)
    return out
A, B = load(sys.argv[1]), load(sys.argv[2])
print('group'.ljust(20), 'before (arr/n, lost, lava, casts, stuck s)'.ljust(44), 'after')
for g in A:
    a, b = A[g], B.get(g, [0] * 6)
    print(g.ljust(20), f'{a[1]}/{a[0]} lost {a[2]} lava {a[3]} casts {a[4]} stuck {a[5]:.0f}'.ljust(44), f'{b[1]}/{b[0]} lost {b[2]} lava {b[3]} casts {b[4]} stuck {b[5]:.0f}')
ta, tb = [sum(v[i] for v in A.values()) for i in range(6)], [sum(v[i] for v in B.values()) for i in range(6)]
print('TOTAL'.ljust(20), f'{ta[1]}/{ta[0]} lost {ta[2]} lava {ta[3]} casts {ta[4]} stuck {ta[5]:.0f}'.ljust(44), f'{tb[1]}/{tb[0]} lost {tb[2]} lava {tb[3]} casts {tb[4]} stuck {tb[5]:.0f}')

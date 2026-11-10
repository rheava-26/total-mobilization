#!/usr/bin/env python3
# lavatab.py TAG : per run: levies that touched lava, lava seconds, bodies lost, arrivals, by gear
import csv, glob, sys, os, collections
tag = sys.argv[1]
for f in sorted(glob.glob(f'/home/user/lab/t5/res/{tag}_lav*.csv')):
    rows = list(csv.DictReader(open(f)))
    g = collections.OrderedDict()
    for r in rows:
        k = 'hook' if r['gear'].startswith('hook') else r['gear']
        a = g.setdefault(k, [0, 0, 0.0, 0, 0, 0])
        a[0] += 1; a[1] += int(r['lavaEntries']) > 0; a[2] += float(r['lavaSec']); a[3] += int(r['bodyGone']); a[4] += int(r['arrived']); a[5] += int(r['lavaEntries'])
    name = os.path.basename(f)[len(tag) + 1:-4]
    print(name.ljust(14), ' | '.join(f'{k} {v[1]}/{v[0]} in lava ({v[5]} entries, {v[2]:.0f}s, lost {v[3]}, arr {v[4]})' for k, v in g.items()))
    tot = [sum(v[1] for v in g.values()), sum(v[0] for v in g.values()), sum(v[2] for v in g.values()), sum(v[3] for v in g.values())]
    print('   total', tot)

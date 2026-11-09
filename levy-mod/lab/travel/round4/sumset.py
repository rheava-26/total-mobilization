#!/usr/bin/env python3
# sumset.py TAG PREFIX : arrivals over all runs res/TAG_PREFIX*.csv, split hook levies / others, falls, mean seconds
import sys, glob, csv, collections
tag, pre = sys.argv[1:3]
d = collections.OrderedDict()
n = 0
for f in sorted(glob.glob(f'/home/user/lab/t4/res/{tag}_{pre}*.csv')):
    n += 1
    for r in csv.DictReader(open(f)):
        k = 'hook' if r['gear'].startswith('hook') else r['gear']
        a = d.setdefault(k, [0, 0, 0, 0.0, 0])
        a[1] += 1; a[0] += int(r['arrived']); a[2] += int(r['falls'])
        if r['arrived'] == '1' and r['arriveSec']: a[3] += float(r['arriveSec']); a[4] += 1
tot = [sum(v[0] for v in d.values()), sum(v[1] for v in d.values())]
print(f'{tag} {pre}* runs={n} all {tot[0]}/{tot[1]} | ' + ' | '.join(f'{k} {v[0]}/{v[1]} falls {v[2]} avg {v[3] / max(1, v[4]):.0f}s' for k, v in d.items()))

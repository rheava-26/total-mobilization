#!/usr/bin/env python3
# sum.py glob...   per run dir: arrived/total by gear
import sys, glob, csv, os, collections
for pat in sys.argv[1:]:
    for d in sorted(glob.glob('/home/user/lab/t2/th/runs/' + pat)):
        f = d + '/out/levies.csv'
        if not os.path.exists(f):
            print(os.path.basename(d), 'NO RESULT'); continue
        a = collections.OrderedDict()
        for r in csv.DictReader(open(f)):
            g = r['gear']; a.setdefault(g, [0, 0, 0.0, 0])
            a[g][0] += int(r['arrived']); a[g][1] += 1
            a[g][3] += int(r['teleports'])
        tot = sum(v[0] for v in a.values()); n = sum(v[1] for v in a.values())
        print('%-28s %2d/%2d  ' % (os.path.basename(d), tot, n) + '  '.join('%s %d/%d' % (g, v[0], v[1]) for g, v in a.items()))

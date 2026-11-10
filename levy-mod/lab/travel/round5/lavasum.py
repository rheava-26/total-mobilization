#!/usr/bin/env python3
# lavasum.py TAGBEFORE TAGAFTER : per gear, levies that touched lava / bodies lost / arrived, over every lava run of the tag (excluding the plain course set, which has no lava entries either way)
import sys, csv, glob, collections
def load(tag, pat):
    g = collections.OrderedDict()
    for f in sorted(glob.glob(f'/home/user/lab/t5/res/{tag}_{pat}*.csv')):
        for r in csv.DictReader(open(f)):
            a = g.setdefault(r['gear'], [0, 0, 0, 0]); a[0] += 1; a[1] += int(r['lavaEntries']) > 0; a[2] += int(r['bodyGone']); a[3] += int(r['arrived'])
    return g
for pat, title in (('lavacliff', 'cliff over a lava basin (wings spent or not, hook or not)'), ('lavakm', 'knocked toward lava, mixed gear'), ('lavak_', 'knocked toward lava, standard gear'), ('lavadm', 'dropped into the 40-deep shaft, mixed gear'), ('lavad_', 'dropped into the shaft, standard gear'), ('lavam', 'lava course, mixed gear (no push)'), ('lava_', 'lava course, standard gear (no push)'), ('lavah', 'hook arm on the gap and the sea')):
    A, B = load(sys.argv[1], pat), load(sys.argv[2], pat)
    if not A: continue
    print(title)
    for k in A:
        a, b = A[k], B.get(k, [0] * 4)
        print(f'   {k:6s} before: touched {a[1]}/{a[0]} lost {a[2]}   after: touched {b[1]}/{b[0]} lost {b[2]}')

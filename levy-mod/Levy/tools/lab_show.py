#!/usr/bin/env python3
"""Compact viewer for OwnerLab TSVs.  usage: lab_show.py file.tsv [col1,col2,...]   (default: the minion/scaling columns)
A column name may be an unambiguous prefix (e.g. 'subsetBreak', 'hooksProj', 'stealth(')."""
import csv, sys
rows = list(csv.reader(open(sys.argv[1]), delimiter='\t'))
h = rows[0]
cols = sys.argv[2].split(',') if len(sys.argv) > 2 else ['item', 'N', 'flags', 'uses', 'projSpawnedByUse', 'maxLiveTagged', 'minionsEnd', 'dmgTotal', 'netClears', 'netThreadPassesInFrame', 'ms_wholeTick']


def resolve(c):
    if c in h:
        return c
    m = [x for x in h if x.startswith(c)]
    return m[0] if m else c


cols = [resolve(c) for c in cols]
print('\t'.join(cols))
for r in rows[1:]:
    d = dict(zip(h, r))
    out = []
    for c in cols:
        v = d.get(c, '?')
        out.append(v if len(v) < 160 else v[:157] + '...')
    print('\t'.join(out))

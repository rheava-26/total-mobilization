#!/usr/bin/env python3
"""perlevy.py DIR : per levy-slot (by start order: 12 R, 12 M, 5 E, 5 S) grounded speed stats and max wingTime."""
import glob, json, sys, statistics as st, collections, os
d = sys.argv[1]
fs = sorted(glob.glob(d + '/**/*.jsonl', recursive=True) + glob.glob(d + '/**/*.levyrec', recursive=True))
import gzip
acc = collections.defaultdict(lambda: dict(sp=[], mx=0, wt=0, n=0))
for f in fs[:int(sys.argv[2]) if len(sys.argv) > 2 else 12]:
    order = []
    for l in (gzip.open(f, 'rt') if f.endswith('.levyrec') else open(f)):
        o = json.loads(l)
        if o.get('k') != 'f': continue
        for L in o.get('l', []):
            if L[0] not in order: order.append(L[0])
            idx = order.index(L[0])
            if L[6] <= 0: continue
            a = acc[idx]; a['n'] += 1
            if L[5] & 1: a['sp'].append(abs(L[3]))
            a['mx'] = max(a['mx'], abs(L[3])); a['wt'] = max(a['wt'], L[9] if len(L) > 9 else 0)
for i in sorted(acc):
    a = acc[i]
    if a['sp']: print(i, 'grounded mean %.2f p95 %.2f max %.2f wingTimeMax %s n=%d' % (st.mean(a['sp']), sorted(a['sp'])[int(.95*len(a['sp']))], a['mx'], a['wt'], a['n']))

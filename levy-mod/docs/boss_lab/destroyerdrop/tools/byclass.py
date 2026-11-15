#!/usr/bin/env python3
"""byclass.py ARM=DIR ... : survival (fraction alive) and mean boss damage per class (R x12, M x12, E x5, S x5 in start order) at checkpoints."""
import glob, gzip, json, os, statistics as st, sys

CK = [10, 20, 30, 45, 60]
CLS = [("R", 0, 12), ("M", 12, 24), ("E", 24, 29), ("S", 29, 34)]


def fight(path):
    order = []; alive = {c: {} for c, _, _ in CLS}
    for l in (gzip.open(path, 'rt') if path.endswith('.levyrec') else open(path)):
        o = json.loads(l)
        if o.get('k') != 'f': continue
        t = o['t'] / 60.0
        L = o.get('l', [])
        for r in L:
            if r[0] >= 0 and r[0] not in order: order.append(r[0])
        for c in CK:
            for cn, lo, hi in CLS:
                if c in alive[cn] or t < c: continue
                ids = order[lo:hi]
                n = sum(1 for r in L if r[0] in ids and r[6] > 0)
                alive[cn][c] = n / (hi - lo)
    return alive


for spec in sys.argv[1:]:
    name, d = spec.split('=', 1)
    fs = sorted(glob.glob(os.path.join(d, '**', '*.jsonl'), recursive=True) + glob.glob(os.path.join(d, '**', '*.levyrec'), recursive=True))
    rs = [fight(f) for f in fs]
    print(f"== {name} ({len(rs)}): fraction alive at " + " ".join(f"{c}s" for c in CK))
    for cn, _, _ in CLS:
        print(f"  {cn}: " + " ".join(f"{st.mean(r[cn][c] for r in rs if c in r[cn]):.2f}" for c in CK))

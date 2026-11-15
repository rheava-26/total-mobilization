#!/usr/bin/env python3
"""curves.py ARM=DIR [ARM=DIR ...] : per-arm mean (and SE) of levies alive and Destroyer life left at checkpoints (seconds into the fight), from the recordings."""
import glob, gzip, json, os, statistics as st, sys

CK = [5, 10, 20, 30, 40, 50, 60, 70]


def fight(path):
    alive = {}; boss = {}; t0 = None
    for l in (gzip.open(path, 'rt') if path.endswith('.levyrec') else open(path)):
        o = json.loads(l)
        if o.get('k') != 'f': continue
        t = o['t'] / 60.0
        for c in CK:
            if c not in alive and t >= c:
                alive[c] = sum(1 for L in o.get('l', []) if L[0] >= 0 and L[6] > 0)
                hd = [b for b in o.get('b', []) if b[0] == 134]
                boss[c] = hd[0][6] / hd[0][7] if hd and hd[0][7] else None
    return alive, boss


for spec in sys.argv[1:]:
    name, d = spec.split('=', 1)
    fs = sorted(glob.glob(os.path.join(d, '**', '*.levyrec'), recursive=True) + glob.glob(os.path.join(d, '**', '*.jsonl'), recursive=True))
    rs = [fight(f) for f in fs]
    print(f"== {name} ({len(rs)} fights)")
    print("  t   : " + " ".join(f"{c:>12d}" for c in CK))
    print("  alive: " + " ".join(f"{st.mean(r[0][c] for r in rs if c in r[0]):6.1f}+-{st.pstdev([r[0][c] for r in rs if c in r[0]]) / max(1, len(rs)) ** .5:4.1f}" for c in CK))
    print("  boss%: " + " ".join(f"{100 * st.mean(r[1][c] for r in rs if r[1].get(c) is not None):6.1f}+-{100 * st.pstdev([r[1][c] for r in rs if r[1].get(c) is not None]) / max(1, len(rs)) ** .5:4.1f}" for c in CK))

#!/usr/bin/env python3
"""posdist.py ARM=DIR ... : where the levies stand relative to the Destroyer. per arm: share of levy-frames by distance band (px) to the nearest segment, share blocked/hooking, mean height over the arena floor row."""
import glob, gzip, json, math, os, statistics as st, sys, collections

BANDS = [0, 60, 120, 240, 480, 960, 1e9]


def fight(path):
    hist = collections.Counter(); n = 0; blocked = 0; hooking = 0; ys = []
    for l in (gzip.open(path, 'rt') if path.endswith('.levyrec') else open(path)):
        o = json.loads(l)
        if o.get('k') != 'f': continue
        B = [(b[2], b[3]) for b in o.get('b', []) if b[0] in (134, 135, 136)]
        if not B: continue
        for L in o.get('l', []):
            if L[0] < 0 or L[6] <= 0: continue
            n += 1
            d = min(math.hypot(L[1] - bx, L[2] - by) for bx, by in B)
            for i in range(len(BANDS) - 1):
                if BANDS[i] <= d < BANDS[i + 1]: hist[i] += 1; break
            if L[5] & 16: blocked += 1
            if L[5] & 8: hooking += 1
    return hist, n, blocked, hooking


for spec in sys.argv[1:]:
    name, d = spec.split('=', 1)
    fs = sorted(glob.glob(os.path.join(d, '**', '*.jsonl'), recursive=True) + glob.glob(os.path.join(d, '**', '*.levyrec'), recursive=True))
    rs = [fight(f) for f in fs]
    print(f"== {name} ({len(rs)})  share of levy-frames by distance to the nearest segment (px)  [mean per fight +- se]")
    for i in range(len(BANDS) - 1):
        v = [r[0][i] / max(1, r[1]) * 100 for r in rs]
        print(f"   {BANDS[i]:>5.0f}-{BANDS[i+1]:<6.0f}: {st.mean(v):6.2f}% +-{st.pstdev(v) / len(v) ** .5:4.2f}")
    v = [r[2] / max(1, r[1]) * 100 for r in rs]; w = [r[3] / max(1, r[1]) * 100 for r in rs]
    print(f"   blocked {st.mean(v):.2f}% +-{st.pstdev(v) / len(v) ** .5:.2f}   hooking {st.mean(w):.2f}% +-{st.pstdev(w) / len(w) ** .5:.2f}")

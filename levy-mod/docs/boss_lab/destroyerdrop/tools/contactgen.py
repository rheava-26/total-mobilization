#!/usr/bin/env python3
"""contactgen.py ARM=DIR [...]  (FILT=substring of the recording file name): any boss. Per fight and per 1000 levy-frames: levy frames whose hitbox overlaps a boss-part hitbox (b array, all parts),
hits taken per contact episode (a contact episode = consecutive frames of one levy overlapping), mean |vx|, distance to the nearest part, time share within 100 px; plus levy knock/speed excursions: share of frames with |vx| > 6 px/tick and |vy| > 8."""
import glob, gzip, json, math, os, statistics as st, sys, collections


def fight(path):
    n = contact = 0; spd = []; fast = 0; hits = 0; episodes = 0; inb = {}; near100 = 0; dist = []; hv = 0
    for l in (gzip.open(path, 'rt') if path.endswith('.levyrec') else open(path)):
        o = json.loads(l); k = o.get('k')
        if k == 'e' and o.get('e') == 'lhit' and o.get('by') == 'boss' and o.get('src') == 'body': hits += 1
        if k != 'f': continue
        B = [(b[2], b[3], b[16] or 40, b[17] or 40) for b in o.get('b', []) if b[6] > 0]
        for L in o.get('l', []):
            if L[0] < 0 or L[6] <= 0: continue
            n += 1; w = L[11] if len(L) > 11 else 20; h = L[12] if len(L) > 12 else 42
            spd.append(abs(L[3]))
            if abs(L[3]) > 6: fast += 1
            if abs(L[4]) > 8: hv += 1
            hit = False; dm = 1e9
            for bx, by, bw, bh in B:
                dm = min(dm, math.hypot(L[1] - bx, L[2] - by))
                if abs(L[1] - bx) * 2 < (w + bw) and abs(L[2] - by) * 2 < (h + bh): hit = True
            if dm < 1e9: dist.append(dm)
            if dm < 100: near100 += 1
            if hit:
                contact += 1
                if not inb.get(L[0]): episodes += 1
            inb[L[0]] = hit
    return dict(n=n, contact=contact, spd=st.mean(spd) if spd else 0, fast=fast, hits=hits, episodes=episodes, near=near100, dist=st.median(dist) if dist else 0, hv=hv)


def mse(xs):
    return f"{st.mean(xs):9.3f} +-{st.pstdev(xs) / max(1, len(xs)) ** .5:6.3f}"


arms = []
for spec in sys.argv[1:]:
    name, d = spec.split('=', 1)
    fs = sorted(glob.glob(os.path.join(d, '**', '*.jsonl'), recursive=True) + glob.glob(os.path.join(d, '**', '*.levyrec'), recursive=True))
    fs = [f for f in fs if os.environ.get('FILT', '') in os.path.basename(f)]
    arms.append((name, [fight(f) for f in fs]))
rows = [("levy-frames/fight", lambda r: r['n']), ("contact frames /1000 levy-frames", lambda r: 1000 * r['contact'] / max(1, r['n'])), ("contact episodes /fight", lambda r: r['episodes']),
        ("boss contact hits /fight", lambda r: r['hits']), ("hits per episode", lambda r: r['hits'] / max(1, r['episodes'])), ("frames within 100px /1000", lambda r: 1000 * r['near'] / max(1, r['n'])),
        ("median distance to nearest part px", lambda r: r['dist']), ("mean |vx| px/tick", lambda r: r['spd']), ("frames |vx|>6 /1000", lambda r: 1000 * r['fast'] / max(1, r['n'])), ("frames |vy|>8 /1000", lambda r: 1000 * r['hv'] / max(1, r['n']))]
print(f"{'':40s}" + "".join(f"{n:>24s}" for n, _ in arms) + f"   ({'/'.join(str(len(r)) for _, r in arms)} fights)")
for lab, f in rows:
    print(f"{lab:40s}" + "".join(f"{mse([f(r) for r in rs]):>24s}" for _, rs in arms))

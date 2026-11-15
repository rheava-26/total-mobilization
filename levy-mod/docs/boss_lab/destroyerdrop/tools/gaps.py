#!/usr/bin/env python3
"""gaps.py ARM=DIR [...] [--src body] : gap (ticks) between two consecutive hits on the SAME levy, from the recordings' lhit events, by source kind (body = NPC contact, minion, proj).
Shows whether the levy's hit-immunity window is the same in every build. Recordings carry the tick t of each event (60 Hz)."""
import collections, glob, gzip, json, os, statistics as st, sys

BUCKETS = [0, 10, 20, 30, 38, 42, 50, 60, 90, 180, 10 ** 9]
for spec in sys.argv[1:]:
    name, d = spec.split('=', 1)
    fs = sorted(glob.glob(os.path.join(d, '**', '*.jsonl'), recursive=True) + glob.glob(os.path.join(d, '**', '*.levyrec'), recursive=True))
    fs = [f for f in fs if os.environ.get('FILT', '') in os.path.basename(f)]
    gaps = collections.defaultdict(list); nfight = 0; counts = collections.Counter()
    for f in fs:
        if 'Destroyer' in os.path.basename(f) and 'x' in sys.argv[0]: pass
        last = {}; nfight += 1
        for l in (gzip.open(f, 'rt') if f.endswith('.levyrec') else open(f)):
            if '"lhit"' not in l: continue
            o = json.loads(l)
            if o.get('e') != 'lhit' or o.get('by') == 'world': continue
            key = o['id']; src = o.get('src', '?'); t = o['t']
            counts[src] += 1
            if key in last: gaps[src].append(t - last[key][0])
            last[key] = (t, src)
    print(f"== {name}: {nfight} fights; lhit by source per fight: " + ', '.join(f"{k} {v / nfight:.1f}" for k, v in counts.items()))
    for src in ('body', 'proj', 'minion'):
        g = gaps.get(src, [])
        if not g: continue
        h = [sum(1 for x in g if BUCKETS[i] <= x < BUCKETS[i + 1]) for i in range(len(BUCKETS) - 1)]
        print(f"   {src:7s} n={len(g):5d} min={min(g):3d} median={st.median(g):5.0f}  gap histogram (ticks) " + ' '.join(f"{BUCKETS[i]}-{BUCKETS[i+1] if BUCKETS[i+1] < 10**8 else 'inf'}:{h[i]}" for i in range(len(h))))

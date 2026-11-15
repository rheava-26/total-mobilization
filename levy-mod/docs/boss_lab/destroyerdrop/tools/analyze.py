#!/usr/bin/env python3
"""analyze.py DIR [DIR...]  : per-arm recording statistics for the Destroyer fights (levy speed, time touching body segments, hits by source).
DIR = a directory with *.levyrec (gz) files (recordings of one arm)."""
import gzip, json, sys, glob, statistics as st, collections, os, re

def aabb(x1,y1,w1,h1,x2,y2,w2,h2): return abs(x1-x2)*2 < (w1+w2) and abs(y1-y2)*2 < (h1+h2)

def fight(path):
    r = dict(file=os.path.basename(path)); frames = 0
    spd = []; spd_g = []; contact = collections.Counter(); lframes = 0; fly = 0; aer = 0; hits = collections.Counter(); dmg = collections.Counter(); deaths = 0
    near = 0; first_death = None; speed_by_state = collections.defaultdict(list); end = {}
    for l in (gzip.open(path, 'rt') if path.endswith('.levyrec') else open(path)):
        o = json.loads(l); k = o.get('k')
        if k == 'f':
            frames += 1
            B = [b for b in o.get('b', []) if b[0] in (134, 135, 136)]
            for L in o.get('l', []):
                if L[0] < 0: continue
                _, x, y, vx, vy, fl, life, state = L[:8]
                w = L[11] if len(L) > 11 else 20; h = L[12] if len(L) > 12 else 42
                if life <= 0: continue
                lframes += 1
                if fl & 2: fly += 1
                if fl & 4: aer += 1
                a = abs(vx); spd.append(a)
                if fl & 1: spd_g.append(a)
                speed_by_state[state].append(a)
                hit = None
                for b in B:
                    if aabb(x, y, w, h, b[2], b[3], b[16] or 57, b[17] or 57):
                        hit = b[0]; break
                if hit: contact[hit] += 1
        elif k == 'e':
            if o['e'] == 'lhit' and o.get('by') == 'boss':
                key = (o.get('src'), o.get('npc')); hits[key] += 1; dmg[key] += o.get('dmg', 0)
            elif o['e'] == 'ldeath':
                deaths += 1
                if first_death is None: first_death = o['t'] / 60
        elif k == 'end': end = o
    r.update(frames=frames, lframes=lframes, spd=st.mean(spd) if spd else 0, spd_g=st.mean(spd_g) if spd_g else 0, fly=fly / max(1, lframes), aer=aer / max(1, lframes),
             contact=dict(contact), hits=dict(hits), dmg=dict(dmg), deaths=deaths, first_death=first_death, result=end.get('result'), dur=end.get('durationSec'), by_state={s: (len(v), st.mean(v)) for s, v in speed_by_state.items()})
    return r

if __name__ == '__main__':
    for d in sys.argv[1:]:
        fs = sorted(glob.glob(os.path.join(d, '**', '*.levyrec'), recursive=True) + glob.glob(os.path.join(d, '**', '*.jsonl'), recursive=True))
        rs = [fight(f) for f in fs]
        n = len(rs); wins = sum(1 for r in rs if r['result'] == 'won')
        tot_l = sum(r['lframes'] for r in rs)
        cf = collections.Counter(); hc = collections.Counter(); dc = collections.Counter()
        for r in rs:
            for k, v in r['contact'].items(): cf[k] += v
            for k, v in r['hits'].items(): hc[k] += v
            for k, v in r['dmg'].items(): dc[k] += v
        print(f"== {d}: {n} fights, {wins} won")
        print(f"  mean |vx| px/tick: all {st.mean(r['spd'] for r in rs):.3f}  grounded {st.mean(r['spd_g'] for r in rs):.3f}  flying frac {st.mean(r['fly'] for r in rs):.3f} aerial frac {st.mean(r['aer'] for r in rs):.3f}")
        print(f"  levy-frames per fight {tot_l/max(1,n):.0f}; contact-frames/1000 levy-frames by part: " + ', '.join(f"{k}:{1000*v/max(1,tot_l):.1f}" for k, v in sorted(cf.items())))
        print(f"  hits per fight by (src,npc): " + ', '.join(f"{k}:{v/n:.1f}" for k, v in sorted(hc.items(), key=lambda kv: str(kv[0]))))
        print(f"  dmg per fight: " + ', '.join(f"{k}:{v/n:.0f}" for k, v in sorted(dc.items(), key=lambda kv: str(kv[0]))))
        sc = collections.Counter(); sv = collections.defaultdict(list)
        for r in rs:
            for k, (c, v) in r['by_state'].items(): sc[k] += c; sv[k].append(v)
        tot = sum(sc.values())
        print("  state occupancy % (speed): " + ', '.join(f"{k}:{100*c/tot:.1f}({st.mean(sv[k]):.2f})" for k, c in sorted(sc.items())))
        print(f"  deaths/fight {st.mean(r['deaths'] for r in rs):.1f}  first death s median {st.median([r['first_death'] for r in rs if r['first_death'] is not None] or [0]):.1f}  dur median {st.median([r['dur'] or 0 for r in rs]):.1f}")

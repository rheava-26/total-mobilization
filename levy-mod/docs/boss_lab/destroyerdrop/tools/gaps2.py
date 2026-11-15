#!/usr/bin/env python3
"""gaps2.py ARM=DIR [...] (FILT=...) : gap in ticks between two consecutive contact ('body') hits on the SAME levy by the SAME boss part type, and by ANY part; plus how many levy hits land while a hit of a 1-damage projectile
(window 20) or a bigger hit (window 40) is still open. Reads lhit events (id, t, npc, dmg, src)."""
import collections, glob, gzip, json, os, statistics as st, sys

B = [0, 5, 10, 20, 30, 38, 42, 60, 90, 180, 10 ** 9]
for spec in sys.argv[1:]:
    name, d = spec.split('=', 1)
    fs = sorted(glob.glob(os.path.join(d, '**', '*.jsonl'), recursive=True) + glob.glob(os.path.join(d, '**', '*.levyrec'), recursive=True))
    fs = [f for f in fs if os.environ.get('FILT', '') in os.path.basename(f)]
    same = []; anyp = []; viol_after_proj = 0; viol_after_body = 0; nbody = 0; after_proj = []; byprev = collections.Counter()
    for f in fs:
        last = {}; lastany = {}
        for l in (gzip.open(f, 'rt') if f.endswith('.levyrec') else open(f)):
            if '"lhit"' not in l: continue
            o = json.loads(l)
            if o.get('e') != 'lhit' or o.get('by') == 'world': continue
            lid, t, npc, src, dmg = o['id'], o['t'], o.get('npc'), o.get('src'), o.get('dmg', 0)
            if src == 'body':
                nbody += 1
                if (lid, npc) in last: same.append(t - last[(lid, npc)])
                if lid in lastany:
                    pt, psrc, pdmg = lastany[lid]
                    anyp.append(t - pt); byprev[(psrc, 'dmg<=1' if pdmg <= 1 else 'dmg>1', 'gap<20' if t - pt < 20 else ('gap<40' if t - pt < 40 else 'gap>=40'))] += 1
                last[(lid, npc)] = t
            lastany[lid] = (t, src, dmg)
    h = lambda g: ' '.join(f"{B[i]}-{B[i+1] if B[i+1] < 10**8 else 'inf'}:{sum(1 for x in g if B[i] <= x < B[i+1])}" for i in range(len(B) - 1))
    print(f"== {name} ({len(fs)} fights) body hits {nbody / max(1, len(fs)):.1f}/fight")
    print("   same levy + same part gap:  " + h(same))
    print("   same levy, ANY previous hit gap (body hit after any hit): " + h(anyp))
    print("   body hit by previous hit kind: " + ', '.join(f"{k[0]}/{k[1]}/{k[2]}={v}" for k, v in sorted(byprev.items())))

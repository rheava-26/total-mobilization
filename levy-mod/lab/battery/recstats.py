"""Per-fight stats from a harness recording (.jsonl or gzip .levyrec): levy state shares, distance to the boss, idle/out-of-range time, deaths, damage by item.
  recstats.py FILE [--json]"""
import gzip, json, sys, collections, math
def opener(p):
    with open(p, 'rb') as f: magic = f.read(2)
    return gzip.open(p, 'rt', errors='replace') if magic == b'\x1f\x8b' else open(p, errors='replace')

def stats(path):
    hdr = None; names = {}; end = {}; st = collections.Counter(); alive_samples = 0; far = 0; idle = 0; nfr = 0
    first_t = None; last_t = 0; deaths = []; dmg_item = collections.Counter(); dmg_owner = collections.Counter(); hits_taken = 0
    dist_sum = 0.0; dist_n = 0; stack = []; lhit_src = collections.Counter()
    for line in opener(path):
        try: o = json.loads(line)
        except Exception: continue
        k = o.get('k')
        if k == 'hdr': hdr = o
        elif k == 'names': names = o
        elif k == 'end': end = o
        elif k == 'f':
            nfr += 1; t = o['t']; first_t = t if first_t is None else first_t; last_t = t
            bs = [b for b in o.get('b', [])]
            levs = [l for l in o.get('l', []) if l[0] > 0 and l[6] > 0]
            xs = []
            for l in levs:
                s = int(l[7]); st[s] += 1; alive_samples += 1
                if bs:
                    d = min(math.hypot(l[1] - b[2], l[2] - b[3]) for b in bs) / 16.0
                    dist_sum += d; dist_n += 1
                    if d > 50: far += 1
                if s != 3: idle += 1
                xs.append((l[1], l[2]))
            if len(xs) >= 5 and nfr % 15 == 0:  # crowding: share of levies within 3 tiles of another levy
                close = sum(1 for i, a in enumerate(xs) if any(j != i and abs(a[0] - b[0]) < 48 and abs(a[1] - b[1]) < 48 for j, b in enumerate(xs)))
                stack.append(close / len(xs))
        elif k == 'e':
            e = o['e']
            if e == 'ldeath': deaths.append((o['t'], o.get('id'), o.get('npc'), o.get('by')))
            elif e == 'lhit': hits_taken += o.get('dmg', 0); lhit_src[(o.get('src'), o.get('npc'), o.get('proj'))] += o.get('dmg', 0)
            elif e == 'dmg':
                for r in o['d']:
                    owner, cls, src, typ, onb, dmg = r[:6]; si = r[7] if len(r) > 7 else 0
                    dmg_owner[owner] += dmg
                    if onb:
                        nm = names.get('items', {}).get(str(si)) if si else None
                        nm = nm or ((names.get('projectiles', {}).get(str(typ)) if src == 'proj' else names.get('items', {}).get(str(typ))) or f'{src}:{typ}')
                        dmg_item[nm] += dmg
    sn = (hdr or {}).get('stateNames', '').split(',')
    t0 = first_t or 0
    return dict(frames=nfr, durTicks=last_t - t0, stateShare={sn[s] if s < len(sn) else s: round(v / max(1, alive_samples), 3) for s, v in st.most_common()},
                notEngagedPct=round(100 * idle / max(1, alive_samples), 1), farOver50tilesPct=round(100 * far / max(1, alive_samples), 1),
                meanDistToBoss=round(dist_sum / max(1, dist_n), 1), crowdedPct=round(100 * sum(stack) / max(1, len(stack)), 1),
                deaths=[(t - t0, i, n, b) for t, i, n, b in deaths], dmgByItem=dmg_item.most_common(12), dmgByOwner=dict(dmg_owner),
                end={k: end.get(k) for k in ('result', 'outcome', 'durationSec', 'levyDeaths')}, levyHitDmg=hits_taken,
                topHitSrc=[(f'{a}/{names.get("npcs", {}).get(str(b), b)}/{names.get("projectiles", {}).get(str(c), c)}', v) for (a, b, c), v in lhit_src.most_common(5)])

if __name__ == '__main__':
    s = stats(sys.argv[1])
    print(json.dumps(s, indent=None if '--json' in sys.argv else 1))

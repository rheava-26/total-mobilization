"""Per-levy view of one recording: held item, state shares, mean distance to the nearest boss part, death tick, shots seen near the levy.
  perlevy.py REC [ids e.g. 1-6]"""
import gzip, json, sys, math, collections
p = sys.argv[1]; lo, hi = (map(int, sys.argv[2].split('-')) if len(sys.argv) > 2 else (1, 99))
op = gzip.open(p, 'rt') if open(p, 'rb').read(2) == b'\x1f\x8b' else open(p)
st = collections.defaultdict(collections.Counter); dist = collections.defaultdict(list); held = {}; death = {}; hdr = None; t0 = None
for line in op:
    try: o = json.loads(line)
    except Exception: continue
    if o['k'] == 'hdr': hdr = o
    elif o['k'] == 'f':
        t0 = o['t'] if t0 is None else t0
        bs = o.get('b', [])
        for l in o.get('l', []):
            i = l[0]
            if not (lo <= i <= hi) or l[6] <= 0: continue
            st[i][int(l[7])] += 1; held[i] = int(l[8])
            if bs: dist[i].append(min(math.hypot(l[1] - b[2], l[2] - b[3]) for b in bs) / 16)
    elif o['k'] == 'e' and o['e'] == 'ldeath' and lo <= o['id'] <= hi: death[o['id']] = (o['t'] - t0) / 60
sn = hdr['stateNames'].split(',')
for i in sorted(st):
    tot = sum(st[i].values())
    print(i, 'held', held.get(i), 'states', {sn[k]: round(v / tot, 2) for k, v in st[i].most_common(4)}, 'meanDist %.0f' % (sum(dist[i]) / max(1, len(dist[i]))), 'death', death.get(i))

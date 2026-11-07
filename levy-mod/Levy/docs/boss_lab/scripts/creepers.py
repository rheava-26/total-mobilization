import re, sys, collections
f = sys.argv[1]
byk = collections.OrderedDict()
for l in open(f):
    r = l.rstrip('\n').split('\t')
    if r[0].startswith('fight|brain|rev'): byk.setdefault(r[0], []).append(r)
for k, v in byk.items():
    pts = []
    for r in v:
        t = int(r[1]); j = ' '.join(r)
        a = int(re.search(r'adds=(\d+)', j).group(1)); d = 1 if 'dtd=1' in j else 0; al = int(re.search(r'alive=(\d+)', j).group(1))
        ai = int(float(re.search(r'ai=(-?[\d.]+)', j).group(1)))
        pts.append((t, a, d, al, ai))
    zero = [t for t, a, d, al, ai in pts if a == 0 and t > 900]
    resp = sum(1 for i in range(1, len(pts)) if pts[i][1] > pts[i - 1][1] + 3)
    print(k.replace('fight|brain|rev|', ''), f"dtd={100*sum(p[2] for p in pts)//len(pts)}% creeperRespawnEvents={resp} firstZeroCreepersAt={'-' if not zero else str(zero[0]//60)+'s'}")
    print('   t(s):creepers/dtd/levies alive:', ' '.join(f"{t//60}:{a}/{d}/{al}" for t, a, d, al, ai in pts[::max(1, len(pts)//14)]))

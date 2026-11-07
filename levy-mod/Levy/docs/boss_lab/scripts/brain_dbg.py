import sys, re, collections
f = sys.argv[1]; key = sys.argv[2]
rows = [l.rstrip('\n').split('\t') for l in open(f) if l.startswith(key)]
byk = collections.OrderedDict()
for r in rows: byk.setdefault(r[0], []).append(r)
names = ['UgSpawn','SurfSpawn','P1Idle','CrSwipes','CrSwings','CrOrbit','CrSpiral','Telekinetic','Stunned','P2TrClosed','P2TrOpen','P2Idle','CrimsonEyes','SanguineScythes','Bloodletting','IllusionDash','IllusionTrick','Death']
for k, v in byk.items():
    n = len(v); dtd = sum(1 for r in v if r[-3].endswith('dtd=1') or 'dtd=1' in r[-3] or 'dtd=1' in r[-2] or 'dtd=1' in r[-1])
    first_v = next((int(r[1]) for r in v if not any('dtd=1' in c for c in r)), None)
    ai0 = [int(float(re.search(r'ai=([\d.\-]+)', r[10]).group(1))) for r in v if re.search(r'ai=([\d.\-]+)', r[10])]
    tr = []; prev = None
    for r, a in zip(v, ai0):
        if a != prev: tr.append(f"{int(r[1])/60:.0f}s:{names[a] if 0 <= a < len(names) else a}"); prev = a
    adds = [int(re.search(r'adds=(\d+)', ' '.join(r)).group(1)) for r in v]
    alive = [int(re.search(r'alive=(\d+)', ' '.join(r)).group(1)) for r in v]
    print(k, f"samples={n} dtd%={100*dtd//max(1,n)} firstVulnerable={'-' if first_v is None else f'{first_v/60:.0f}s'} addsMax={max(adds)} addsAt30s={adds[min(len(adds)-1,59)]} alive: start {alive[0]} end {alive[-1]}")
    print('   ', ' > '.join(tr[:14]))

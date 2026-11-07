import csv, sys, collections, statistics as st
def load(files):
    seen = {};
    for f in files:
        for r in csv.DictReader(open(f), delimiter='\t'):
            if r['kind'] == 'fight' and r['result'] != 'ERROR': seen[r['key']] = r
    return list(seen.values())
def grp(rows, keyf):
    g = collections.defaultdict(list)
    for r in rows: g[keyf(r)].append(r)
    return g
def f(r, k):
    try: return float(r[k])
    except: return float('nan')
def phase_dps(r):
    # dmg in vulnerable-and-engaged phases: sum dmg / sum ticks over phases with dmg>0
    t = d = 0
    for p in r.get('phases', '').split():
        try:
            nm, rest = p.split(':'); s, dd = rest.split('s/'); dd = dd.split('d')[0]; s = float(s); dd = float(dd)
            if dd > 0: t += s; d += dd
        except: pass
    return d / t if t else float('nan')
mode = sys.argv[1]; hs = sys.argv[2]; files = sys.argv[3:]
rows = [r for r in load(files) if r['boss'] == 'eye' and r['mode'] == mode and (('|hs0' in r['key']) == (hs == '0')) and '|cfar' in r['key'] and r['gear'] == 'mod' and '|bhp' not in r['key'] and '|dr' not in r['key'] and '|cl' not in r['key']]
g = grp(rows, lambda r: int(r['n']))
print(f"eye {mode} hpscale={'0' if hs=='0' else '2%/cp'}: n | fights | kills | ttk mean/median (kills) | deaths | dmg/s overall | dpsHittable (dmg/s while boss hittable) | dmg/s per levy hittable | HP max")
for n in sorted(g):
    v = g[n]; w = [r for r in v if r['result'] == 'WIN']
    ttk = [f(r, 'sec') for r in w]
    dh = [f(r, 'dpsHittable') for r in v if r.get('dpsHittable')]
    print(f"{n:3d} | {len(v):2d} | {len(w):2d} | {st.mean(ttk) if ttk else float('nan'):6.1f}/{st.median(ttk) if ttk else float('nan'):6.1f} | {st.mean(f(r,'deaths') for r in v):4.1f} | {st.mean(f(r,'dmgPerSec') for r in v):6.1f} | {st.mean(dh) if dh else float('nan'):6.1f} | {(st.mean(dh)/n) if dh else float('nan'):5.2f} | {st.mean(f(r,'bossMax') for r in v):6.0f}")

import csv, sys, glob, collections, statistics, re, os
# usage: tab_o.py [outdir]  -> markdown tables of the options lab (arms = files o_<arm>.tsv)
D = sys.argv[1] if len(sys.argv) > 1 else '/home/user/lab/out_o'
CELLS = [('eye', 'rev', 'Eye of Cthulhu Rev'), ('brain', 'rev', 'Brain of Cthulhu Rev'), ('qb', 'exp', 'Queen Bee Expert'), ('eow', 'exp', 'Eater of Worlds Expert'), ('ds', 'exp', 'Desert Scourge Expert'), ('skel', 'exp', 'Skeletron Expert')]
ORDER = ['base', 'fm_events', 'fm_slow', 'fm_rally', 'sv_parity', 'sv_veteran', 'sv_formation']
def num(r, k, d=None):
    try: return float(r[k])
    except Exception: return d
def result(r):
    x = r['result']
    if x == 'TIMEOUT' and (num(r, 'bossMoved', 0) or 0) > 100000: return 'DESPAWN'  # a boss that flew away is an unrecorded despawn (RESULTS2 A1)
    return x
def load(arm):
    rows = {}
    f = f'{D}/o_{arm}.tsv'
    if not os.path.exists(f): return rows
    for r in csv.DictReader(open(f), delimiter='\t'):
        if r.get('kind') == 'fight' and r['result'] != 'ERROR': rows[(r['boss'], r['mode'], int(r['seed']))] = r
    return rows
def mean(xs):
    xs = [x for x in xs if x is not None]
    return statistics.mean(xs) if xs else None
def f1(x, d=0): return '-' if x is None else f'{x:.{d}f}'
def cell(rows, boss, mode):
    v = [r for (b, m, s), r in sorted(rows.items()) if b == boss and m == mode]
    if not v: return None
    c = collections.Counter(result(r) for r in v)
    w = [r for r in v if result(r) == 'WIN']
    return dict(n=len(v), W=c['WIN'], D=c['DESPAWN'], T=c['TIMEOUT'], L=c['LOSS'], deaths=mean([num(r, 'deaths') for r in v]), ttk=mean([num(r, 'sec') for r in w]),
                fd=mean([num(r, 'firstDeathSec') for r in v]), fear=mean([num(r, 'fearPct') for r in v]), hp=mean([num(r, 'avgHp') for r in v]), de=mean([num(r, 'avgDef') for r in v]), dps=mean([num(r, 'dmgPerSec') for r in v]),
                seeds={s: result(r)[0] for (b, m, s), r in rows.items() if b == boss and m == mode})
ARMS = [a for a in ORDER + sorted(set(os.path.basename(f)[2:-4] for f in glob.glob(f'{D}/o_*.tsv')) - set(ORDER)) if os.path.exists(f'{D}/o_{a}.tsv')]
DATA = {a: load(a) for a in ARMS}
print('| cell | arm | win/desp/timeout/loss (n) | deaths | kill time s | first death s | Shaken+Routed % | avg HP | avg def | boss dmg/s |')
print('|---|---|---|---|---|---|---|---|---|---|')
for boss, mode, name in CELLS:
    for a in ARMS:
        c = cell(DATA[a], boss, mode)
        if not c: continue
        print(f"| {name} | {a} | {c['W']}/{c['D']}/{c['T']}/{c['L']} ({c['n']}) | {f1(c['deaths'],1)} | {f1(c['ttk'])} | {f1(c['fd'],1)} | {f1(c['fear'])} | {f1(c['hp'])} | {f1(c['de'],1)} | {f1(c['dps'])} |")
print()
print('Wins per arm (paired seeds; W = kills):')
print('| arm | ' + ' | '.join(n for _, _, n in CELLS) + ' | total |')
print('|---|' + '---|' * (len(CELLS) + 1))
for a in ARMS:
    cs = [cell(DATA[a], b, m) for b, m, _ in CELLS]
    tw = sum(c['W'] for c in cs if c); tn = sum(c['n'] for c in cs if c)
    print(f'| {a} | ' + ' | '.join(f"{c['W']}/{c['n']}" if c else '-' for c in cs) + f' | {tw}/{tn} |')
print()
print('Per-seed results (W/D/T/L letters, seeds 1..6):')
for boss, mode, name in CELLS:
    for a in ARMS:
        c = cell(DATA[a], boss, mode)
        if c: print(f"- {name} {a}: " + ''.join(c['seeds'].get(s, '.') for s in range(1, 7)))

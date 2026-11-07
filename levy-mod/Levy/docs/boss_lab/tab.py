import csv, sys, collections
# usage: tab.py file.tsv [file2.tsv ...]   -> grouped table (boss mode gear arena n)
rows = []
for f in sys.argv[1:]:
    try:
        rows += [r for r in csv.DictReader(open(f), delimiter='\t') if r.get('kind') == 'fight']
    except FileNotFoundError:
        pass
def num(r, k, d=0.0):
    try: return float(r[k])
    except Exception: return d
g = collections.OrderedDict()
for r in rows:
    key = (r['boss'], r['mode'], r.get('gear', 'van'), r.get('arena', 'surf'), r.get('n', '10'), r.get('key', '').split('|bhp')[1].split('|')[0] if '|bhp' in r.get('key', '') else '', r.get('key', '').split('|hs')[1].split('|')[0] if '|hs' in r.get('key', '') else '')
    g.setdefault(key, []).append(r)
print("boss mode gear arena n [bhp][hs] | kill/desp/tmo/loss | deaths | ttkWin(s) | hits | dps | tgt L/P/N% | addTgt L/P% | enrS | seeds")
for key, v in g.items():
    res = collections.Counter(r['result'] for r in v)
    w = [r for r in v if r['result'] == 'WIN']
    kill = len(w); desp = res['DESPAWN']; tmo = res['TIMEOUT']; loss = res['LOSS']; err = res['ERROR']
    ttk = sum(num(r, 'sec') for r in w) / len(w) if w else float('nan')
    d = sum(num(r, 'deaths') for r in v) / len(v)
    hits = sum(num(r, 'hitsBoss') for r in v) / len(v)
    dps = sum(num(r, 'dmgPerSec') for r in v) / len(v)
    def p(k):
        xs = [num(r, k) for r in v if r.get(k, '-') not in ('-', '')]
        return f"{sum(xs)/len(xs):.0f}" if xs else '-'
    enr = sum(num(r, 'enragedSec') for r in v) / len(v)
    seeds = ','.join(r['seed'] + ':' + r['result'][:1] + f"{num(r,'sec'):.0f}" for r in v)
    print(f"{key[0]:7} {key[1]:6} {key[2]:3} {key[3]:4} n={key[4]:>2} {key[5]:>3}{key[6]:>3} | {kill}/{desp}/{tmo}/{loss}{'/E'+str(err) if err else ''} | {d:4.1f}/{key[4]} | {ttk:6.1f} | {hits:6.0f} | {dps:6.1f} | {p('tgtLevyPct')}/{p('tgtPlayerPct')}/{p('tgtNonePct')} | {p('addTgtLevyPct')}/{p('addTgtPlayerPct')} | {enr:5.1f} | {seeds}")

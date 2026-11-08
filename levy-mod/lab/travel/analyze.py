import csv, glob, os, sys, statistics as st, collections
S = os.environ.get('TRAVEL_HOME', os.path.expanduser('~/travel-lab')) + '/runs'
tag = sys.argv[1] if len(sys.argv) > 1 else 'B'
bygear = '--gear' in sys.argv

def f(x):
    try: return float(x)
    except: return None

def summarize(rows):
    n = len(rows)
    arr = [r for r in rows if r['arrived'] == '1']
    t = [f(r['arriveSec']) for r in arr if f(r['arriveSec']) is not None]
    return dict(n=n, arr=len(arr), tmed=(st.median(t) if t else None),
                ratio=st.median([f(r['ratio']) for r in rows]),
                stuck=sum(f(r['stuckSec']) for r in rows) / n,
                stall=sum(f(r['stallSec']) for r in rows) / n,
                tele=sum(int(r['teleports']) for r in rows),
                falls=sum(int(r['falls']) for r in rows),
                gone=sum(int(r['bodyGone']) for r in rows),
                gaveup=sum(1 for r in rows if r['gaveUpSec'] and r['arrived'] != '1'))

print(f"{'run':34} {'gear':6} arr/n  t_med  ratio stuck_s stall_s tele falls gone gaveup  where-stuck")
for d in sorted(glob.glob(f'{S}/{tag}_*')):
    p = d + '/out/levies.csv'
    if not os.path.exists(p):
        print(os.path.basename(d), 'NO RESULT'); continue
    rows = list(csv.DictReader(open(p)))
    groups = {'all': rows}
    if bygear:
        groups = collections.OrderedDict((g, [r for r in rows if r['gear'] == g]) for g in ['none', 'boots', 'wings', 'hook'])
    for g, rs in groups.items():
        if not rs: continue
        s = summarize(rs)
        where = collections.Counter()
        for r in rs:
            for w in r['stuckWhere'].split():
                where[w.split('@')[0]] += 1
        w = ','.join(f'{k}x{v}' for k, v in where.most_common(3))
        end = collections.Counter(r['endFeature'] for r in rs if r['arrived'] != '1')
        e = ','.join(f'{k}x{v}' for k, v in end.most_common(2))
        tm = f"{s['tmed']:.0f}" if s['tmed'] is not None else '-'
        print(f"{os.path.basename(d):34} {g:6} {s['arr']:>2}/{s['n']:<2} {tm:>6} {s['ratio']:>5.2f} {s['stuck']:>7.1f} {s['stall']:>7.1f} {s['tele']:>4} {s['falls']:>5} {s['gone']:>4} {s['gaveup']:>5}  {w} | end:{e}")

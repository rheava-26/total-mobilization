import csv, sys, os, glob, collections, statistics, re
# lab 2 tables. usage: tab2.py main|fear|spread   (reads /home/user/lab/out2/*.tsv for lab 2 and docs/boss_lab/raw/*.tsv for lab 1)
HERE = os.path.dirname(os.path.abspath(__file__))
OUT2 = '/home/user/lab/out2'
def load(files):
    rows = []
    for f in files:
        try: rows += [r for r in csv.DictReader(open(f), delimiter='\t') if r.get('kind') == 'fight']
        except FileNotFoundError: pass
    return rows
def num(r, k, d=0.0):
    try: return float(r[k])
    except Exception: return d
def dedup(rows): return list({r['key']: r for r in rows}.values())
def cmdof(r): return 'far' if '|cfar' in r['key'] else 'near'
def firstdeath(r):
    xs = []
    for t in r.get('perLevy', '').split(';'):
        m = re.search(r'surv([\d.]+):dead', t)
        if m: xs.append(float(m.group(1)))
    return min(xs) if xs else None
def cell(v):
    res = collections.Counter(r['result'] for r in v)
    w = [r for r in v if r['result'] == 'WIN']
    ttk = statistics.mean(num(r, 'sec') for r in w) if w else None
    fd = [firstdeath(r) for r in v]; fd = [x for x in fd if x is not None]
    def p(k):
        xs = [num(r, k) for r in v if r.get(k, '-') not in ('-', '')]
        return statistics.mean(xs) if xs else None
    return dict(n=len(v), W=res['WIN'], D=res['DESPAWN'], T=res['TIMEOUT'], L=res['LOSS'], E=res['ERROR'], deaths=statistics.mean(num(r, 'deaths') for r in v), ttk=ttk,
                fd=statistics.mean(fd) if fd else None, tL=p('tgtLevyPct'), tP=p('tgtPlayerPct'), aL=p('addTgtLevyPct'), aP=p('addTgtPlayerPct'), hits=p('hitsBoss'), dps=p('dmgPerSec'),
                ncp=v[0].get('n', ''), seeds=','.join(f"{r['seed']}:{r['result'][0]}{num(r,'sec'):.0f}" for r in sorted(v, key=lambda r: int(r['seed']))))
def f1(x, d=0): return '-' if x is None else f"{x:.{d}f}"
def wl(c): return f"{c['W']}/{c['D']}/{c['T']}/{c['L']} ({c['n']})" if c else '-'
BOSS = ['kslime', 'ds', 'eye', 'eow', 'brain', 'qb', 'skel']; NAME = dict(kslime='King Slime', ds='Desert Scourge', eye='Eye of Cthulhu', eow='Eater of Worlds', brain='Brain of Cthulhu', qb='Queen Bee', skel='Skeletron')
MODE = dict(exp='Expert', rev='Rev', death='Death')
def lab1():
    """Lab-1 cells parsed from main_table.txt (what RESULTS.md section 3 summarises): {(boss, mode, 'far'|'near', n): dict(wl, deaths, ttk, hits, tgt)}"""
    G = {}
    for l in open(HERE + '/main_table.txt'):
        p = [x.strip() for x in l.split('|')]
        if len(p) < 9: continue
        h = p[0].split()
        if len(h) < 5 or h[2] != 'mod' or not h[3].startswith('surf'): continue
        far = 'far' if h[3].endswith('/far') else 'near'
        n = h[4].replace('n=', '') if h[4].startswith('n=') else (h[5] if len(h) > 5 else '')
        kdl = p[1].split('/'); w, d, t, lo = kdl[0], kdl[1], kdl[2], kdl[3].split()[0]
        ns = len(p[-1].split(','))
        G[(h[0], h[1], far, n)] = dict(wl=f"{w}/{d}/{t}/{lo} ({ns})", deaths=p[2].split('/')[0].strip(), ttk=p[3].strip(), tgt=p[6].strip())
    return G
CP = dict(kslime=10, ds=11, eye=11, eow=13, brain=13, qb=15, skel=16)
def main():
    G1 = lab1()
    new = dedup(load([OUT2 + '/main_a.tsv', OUT2 + '/main_b.tsv']))
    G2 = collections.defaultdict(list)
    for r in new: G2[(r['boss'], r['mode'])].append(r)
    print('| boss | mode | n | lab2 near: kill/desp/timeout/loss (seeds) | deaths | kill time s | first death s | boss tgt L/P % | add tgt L/P % | lab1 far | lab1 far deaths / ttk | lab1 near | lab1 near deaths / ttk |')
    print('|---|---|---|---|---|---|---|---|---|---|---|---|---|')
    for b in BOSS:
        for m in ('exp', 'rev', 'death'):
            v = G2.get((b, m))
            if not v: continue
            c = cell(v)
            def l1(cm):
                d = G1.get((b, m, cm, str(CP[b])))
                return (d['wl'], f"{d['deaths']} / {d['ttk'].replace('nan', '-')}") if d else ('-', '-')
            a, ad = l1('far'); bb, bd = l1('near')
            print(f"| {NAME[b]} | {MODE[m]} | {c['ncp']} | {wl(c)} | {c['deaths']:.1f} | {f1(c['ttk'])} | {f1(c['fd'], 1)} | {f1(c['tL'])}/{f1(c['tP'])} | {f1(c['aL'])}/{f1(c['aP'])} | {a} | {ad} | {bb} | {bd} |")
def pair(a_files, b_files, la, lb, extra_key=None):
    A = collections.defaultdict(list); B = collections.defaultdict(list)
    for r in dedup(load(a_files)): A[(r['boss'], r['mode'])].append(r)
    for r in dedup(load(b_files)): B[(r['boss'], r['mode'])].append(r)
    print(f'| boss | mode | {la}: kill/desp/timeout/loss | deaths | kill time s | first death s | boss tgt L/P % | {lb}: kill/desp/timeout/loss | deaths | kill time s | first death s | boss tgt L/P % | boss dmg/s {la} / {lb} |')
    print('|---|---|---|---|---|---|---|---|---|---|---|---|---|')
    for b in BOSS:
        for m in ('exp', 'rev', 'death'):
            if (b, m) not in B: continue
            # only compare on seeds present in both
            sb = {r['seed'] for r in B[(b, m)]}
            av = [r for r in A.get((b, m), []) if r['seed'] in sb]
            if not av: continue
            bv = B[(b, m)]
            ca, cb = cell(av), cell(bv)
            print(f"| {NAME[b]} | {MODE[m]} | {wl(ca)} | {ca['deaths']:.1f} | {f1(ca['ttk'])} | {f1(ca['fd'],1)} | {f1(ca['tL'])}/{f1(ca['tP'])} | {wl(cb)} | {cb['deaths']:.1f} | {f1(cb['ttk'])} | {f1(cb['fd'],1)} | {f1(cb['tL'])}/{f1(cb['tP'])} | {f1(ca['dps'],1)} / {f1(cb['dps'],1)} |")
if __name__ == '__main__':
    w = sys.argv[1]
    mains = [OUT2 + '/main_a.tsv', OUT2 + '/main_b.tsv']
    if w == 'main': main()
    elif w == 'fear': pair(mains, [OUT2 + '/fearoff_a.tsv', OUT2 + '/fearoff_b.tsv'], 'fear on', 'fear off')
    elif w == 'spread': pair(mains, [OUT2 + '/spreadoff.tsv'], 'spread on', 'spread off')
    elif w == 'seeds':
        for r in sorted(dedup(load(sorted(glob.glob(OUT2 + '/*.tsv')))), key=lambda r: r['key']): print(r['key'], r['result'], r['sec'], r['deaths'])

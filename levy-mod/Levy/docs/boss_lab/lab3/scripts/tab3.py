#!/usr/bin/env python3
"""Lab 3 tables. Usage: tab3.py <outdir with *.tsv and *.tsv.l3.tsv> main|arms|vert [spec names...]
Joins <spec>.tsv (calrev row) with <spec>.tsv.l3.tsv (lab 3 row) on the key.
Result labels: WIN = kill; a TIMEOUT whose boss travelled > FLED_TILES is reported as FLED (an unrecorded despawn).
arms usage: tab3.py <dir> arms a=cal_a1,cal_a2 b=cal_b ..."""
import sys, os, csv, collections, statistics as st

FLED_TILES = 1500.0
NAMES = {'kslime': 'King Slime', 'ds': 'Desert Scourge', 'eye': 'Eye of Cthulhu', 'eow': 'Eater of Worlds', 'brain': 'Brain of Cthulhu',
         'hive': 'Hive Mind', 'perf': 'Perforators', 'qb': 'Queen Bee', 'skel': 'Skeletron', 'sg': 'Slime God'}
ORDER = list(NAMES)
MODES = {'exp': 'Expert', 'rev': 'Revengeance', 'death': 'Death'}


def load(d, name):
    rows = {}
    p = os.path.join(d, name + '.tsv')
    if not os.path.exists(p):
        return rows
    with open(p) as f:
        for r in csv.DictReader(f, delimiter='\t'):
            rows[r['key']] = r
    p3 = p + '.l3.tsv'
    if os.path.exists(p3):
        with open(p3) as f:
            for r in csv.DictReader(f, delimiter='\t'):
                if r['key'] in rows:
                    rows[r['key']].update({'l3_' + k: v for k, v in r.items()})
    return rows


def fl(x, d=0.0):
    try:
        return float(x)
    except Exception:
        return d


def label(r):
    res = r['result']
    if res == 'TIMEOUT' and fl(r.get('bossMoved')) > FLED_TILES:
        return 'FLED'
    return res


def cell(rows):
    c = collections.Counter(label(r) for r in rows)
    wins = [r for r in rows if label(r) == 'WIN']
    nw = [r for r in rows if label(r) != 'WIN']
    return {
        'n': len(rows), 'kill': c['WIN'], 'desp': c['DESPAWN'], 'fled': c['FLED'], 'to': c['TIMEOUT'], 'loss': c['LOSS'],
        'deaths': st.mean(fl(r['deaths']) for r in rows) if rows else 0, 'cp': rows[0]['n'] if rows else '',
        'ttk': st.mean(fl(r['sec']) for r in wins) if wins else None,
        'left': st.mean(100.0 * fl(r['bossLeft']) / max(1.0, fl(r['bossMax'])) for r in nw) if nw else None,
    }


def fmt(v, d=0):
    return '-' if v is None else f'{v:.{d}f}'


def cellkey(r):
    parts = r['key'].split('|')
    return parts[1], parts[2]


def group(rows):
    g = collections.defaultdict(list)
    for r in rows.values():
        b, m = cellkey(r)
        g[(b, m)].append(r)
    return g


def main_table(d, names):
    rows = {}
    for n in names:
        rows.update(load(d, n))
    g = group(rows)
    out = ['| boss | mode | levies (CP) | seeds | kill / despawn / fled / timeout / wipe | levy deaths | kill time s | boss HP left % (non-wins) |',
           '|---|---|---|---|---|---|---|---|']
    for b in ORDER:
        for m in MODES:
            if (b, m) not in g:
                continue
            c = cell(g[(b, m)])
            out.append(f"| {NAMES[b]} | {MODES[m]} | {c['cp']} | {c['n']} | {c['kill']} / {c['desp']} / {c['fled']} / {c['to']} / {c['loss']} | {c['deaths']:.1f} | {fmt(c['ttk'])} | {fmt(c['left'])} |")
    return '\n'.join(out)


def arms_table(d, arms):
    data = {}
    for a, ns in arms.items():
        rows = {}
        for n in ns:
            rows.update(load(d, n))
        data[a] = group(rows)
    keys = sorted({k for g in data.values() for k in g}, key=lambda k: (ORDER.index(k[0]), list(MODES).index(k[1])))
    out = ['| cell | ' + ' | '.join(arms) + ' |', '|---|' + '---|' * len(arms)]
    for k in keys:
        cols = []
        for a in arms:
            if k not in data[a]:
                cols.append('-')
                continue
            c = cell(data[a][k])
            cols.append(f"{c['kill']}/{c['n']} kills, {c['deaths']:.1f} dead, ttk {fmt(c['ttk'])}, left {fmt(c['left'])}%")
        out.append(f"| {NAMES[k[0]]} {MODES[k[1]]} | " + ' | '.join(cols) + ' |')
    return '\n'.join(out)


def vert_table(d, names):
    rows = {}
    for n in names:
        rows.update(load(d, n))
    by = collections.defaultdict(list)
    for r in rows.values():
        if 'l3_vFloorPct' in r:
            by[cellkey(r)[0]].append(r)
    out = ['| boss | fights | on floor % | on platform rows % | in air % | mean height (tiles) | height sd | rows 0..5 % | boss height (tiles) |',
           '|---|---|---|---|---|---|---|---|---|']
    tot = collections.defaultdict(list)
    for b in ORDER:
        if b not in by:
            continue
        rs = by[b]
        w = lambda k: st.mean(fl(r['l3_' + k]) for r in rs)
        lv = [st.mean(fl(r['l3_vLevels'].split(',')[i]) for r in rs) for i in range(6)]
        out.append(f"| {NAMES[b]} | {len(rs)} | {w('vFloorPct'):.0f} | {w('vPlatPct'):.0f} | {w('vAirPct'):.0f} | {w('hMean'):.1f} | {w('hStd'):.1f} | {','.join(f'{x:.0f}' for x in lv)} | {w('bossHMean'):.1f} |")
        for k in ('vFloorPct', 'vPlatPct', 'vAirPct', 'hMean', 'hStd'):
            tot[k].append(w(k))
    if tot:
        out.append(f"| ALL (mean over bosses) | {sum(len(v) for v in by.values())} | {st.mean(tot['vFloorPct']):.0f} | {st.mean(tot['vPlatPct']):.0f} | {st.mean(tot['vAirPct']):.0f} | {st.mean(tot['hMean']):.1f} | {st.mean(tot['hStd']):.1f} | | |")
    return '\n'.join(out)


if __name__ == '__main__':
    d, what = sys.argv[1], sys.argv[2]
    names = sys.argv[3:]
    if what == 'main':
        print(main_table(d, names))
    elif what == 'vert':
        print(vert_table(d, names))
    elif what == 'arms':
        arms = {}
        for a in names:
            k, v = a.split('=')
            arms[k] = v.split(',')
        print(arms_table(d, arms))

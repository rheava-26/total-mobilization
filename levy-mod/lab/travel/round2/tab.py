#!/usr/bin/env python3
# tab.py TAG [TAG2 ...]  -> per set, per gear arrived/total, plus planner ms
import sys, glob, csv, os, re, collections
GEARS = ['none', 'boots', 'wings', 'hook']
SETS = [
    ('real 800 map', r'_M\d_x\d+_long$'),
    ('real 150 map', r'_M\d_x\d+_med\d$'),
    ('real 800 follow', r'_M\d_x\d+_fol$'),
    ('zoo map', r'_zoo_map_Z\d$'),
    ('zoo follow', r'_zoo_follow_Z\d$'),
    ('corr holes (C0)', r'_cor_map_C0$'),
    ('corr cave mouth (C1)', r'_cor_map_C1$'),
    ('corr chasm60 (C2)', r'_cor_map_C2$'),
    ('corr hole4 (C3)', r'_cor_map_C3$'),
    ('corr pit+stairs (C4)', r'_cor_map_C4$'),
    ('carved legs x6', r'_walk_map_L\d$'),
    ('carved full 920', r'_walk_map_full$'),
    ('carved follow 920', r'_walk_follow_full$'),
]
def load(tag):
    res = collections.defaultdict(lambda: collections.defaultdict(lambda: [0, 0]))
    ms = collections.defaultdict(lambda: [0.0, 0, 0.0, 0])
    for d in sorted(glob.glob('/home/user/lab/t2/th/runs/%s_*' % tag)):
        name = os.path.basename(d)[len(tag):]
        f = d + '/out/levies.csv'
        if not os.path.exists(f): continue
        for sname, pat in SETS:
            if re.search(pat, name):
                for r in csv.DictReader(open(f)):
                    v = res[sname][r['gear']]; v[0] += int(r['arrived']); v[1] += 1
                    t = res[sname]['tele']; t[0] += int(r['teleports'])
                ev = d + '/out/events.txt'
                if os.path.exists(ev):
                    for line in open(ev):
                        m = re.search(r'planner: routes=(\d+).*? nodes=(\d+) ms=([\d.]+) maxSliceMs=([\d.]+)', line)
                        if m:
                            a = ms[sname]; a[0] += float(m.group(3)); a[1] += int(m.group(2)); a[2] = max(a[2], float(m.group(4))); a[3] += int(m.group(1))
    return res, ms
tags = sys.argv[1:]
data = {t: load(t) for t in tags}
print('%-22s' % 'set' + ''.join('| %-34s' % t for t in tags))
for sname, _ in SETS:
    row = '%-22s' % sname
    for t in tags:
        res, ms = data[t]
        r = res.get(sname)
        if not r: row += '| %-34s' % '-'; continue
        tot = sum(r[g][0] for g in GEARS); n = sum(r[g][1] for g in GEARS)
        cell = '%d/%d (%d%%) ' % (tot, n, 100 * tot // max(1, n)) + ' '.join('%s%d/%d' % (g[0], r[g][0], r[g][1]) for g in GEARS if r[g][1])
        row += '| %-34s' % cell
    print(row)
print()
for t in tags:
    res, ms = data[t]
    tot = sum(v[0] for v in ms.values()); sl = sum(v[1] for v in ms.values()) / 700.0; mx = max([v[2] for v in ms.values()] or [0])
    tele = sum(res[s]['tele'][0] for s in res)
    print('%s: planner total %.0f ms over %.0f 700-node slices = %.2f ms/slice, max slice %.1f ms; teleports %d' % (t, tot, sl, tot / max(1, sl), mx, tele))

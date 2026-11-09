#!/usr/bin/env python3
# tabr.py TAG [TAG...]  : per category arrivals by gear from res/*.csv  (category = run name without the tag, with the world/leg parts folded)
import sys, glob, csv, re, os, collections
tags = sys.argv[1:]
def cat(n):
    n = re.sub(r'_x\d+', '', n)
    n = re.sub(r'^(g_|far_|farg_|near_|nearg_)?(M\d)_(long|med\d)$', lambda m: (m.group(1) or '') + {'long': 'real800', 'med1': 'real150', 'med2': 'real150', 'med3': 'real150'}[m.group(3)], n)
    n = re.sub(r'_(Z|C|L)\d$', lambda m: '_' + m.group(1) + '*', n) if False else n
    return n
rows = collections.OrderedDict()
for tag in tags:
    for f in sorted(glob.glob(f'/home/user/lab/t3/res/{tag}_*.csv')):
        name = os.path.basename(f)[len(tag) + 1:-4]
        c = cat(name)
        d = rows.setdefault(c, {}).setdefault(tag, {'g': collections.OrderedDict(), 'tele': 0, 'vis': 0, 'clim': 0, 'sec': [], 'runs': 0, 'falls': 0})
        d['runs'] += 1
        for r in csv.DictReader(open(f)):
            g = d['g'].setdefault(r['gear'], [0, 0]); g[1] += 1; g[0] += int(r['arrived'])
            d['tele'] += int(r['teleports']); d['vis'] += int(r.get('visTele') or 0); d['clim'] += int(r.get('climbEdges') or 0); d['falls'] += int(r['falls'])
            if r['arrived'] == '1' and r['arriveSec']: d['sec'].append(float(r['arriveSec']))
for c, byt in rows.items():
    for tag, d in byt.items():
        tot = sum(v[0] for v in d['g'].values()); n = sum(v[1] for v in d['g'].values())
        sec = sum(d['sec']) / len(d['sec']) if d['sec'] else 0
        print('%-16s %-4s runs %2d  %3d/%3d (%2d%%)  %s | tele %d vis %d climbs %d falls %d avgSec %.0f' % (c, tag, d['runs'], tot, n, 100 * tot // max(1, n), ' '.join('%s %d/%d' % (g, v[0], v[1]) for g, v in d['g'].items()), d['tele'], d['vis'], d['clim'], d['falls'], sec))

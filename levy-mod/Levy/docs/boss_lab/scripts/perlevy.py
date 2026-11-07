import csv, sys
for r in csv.DictReader(open(sys.argv[1]), delimiter='\t'):
    per = [p.split(':') for p in r['perLevy'].split(';')]
    d = [int(p[2][3:]) for p in per]; cls = [p[0] for p in per]; wp = [p[1] for p in per]
    z = sum(1 for x in d if x == 0)
    n = len(d)
    bycls = {c: sum(x for x, cc in zip(d, cls) if cc == c) for c in 'RMES'}
    ncls = {c: cls.count(c) for c in 'RMES'}
    print(r['seed'], 'n', n, 'dps', r['dmgPerSec'], 'zero-dmg levies', z, 'median', sorted(d)[n // 2], 'max', max(d), 'dmg/class', bycls, 'count', ncls, 'unattr', r['dmgUnattr'])

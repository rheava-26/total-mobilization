import csv, sys, collections
tot = collections.defaultdict(lambda: collections.defaultdict(float)); cnt = collections.Counter()
for r in csv.DictReader(open(sys.argv[1]), delimiter='\t'):
    if r['kind'] != 'fight' or 'dmgSources' not in r: continue
    k = (r['boss'], r['mode'], r['arena']); cnt[k] += 1
    for it in r['dmgSources'].split():
        if '=' in it:
            n, v = it.rsplit('=', 1); tot[k][n] += float(v)
    print(r['boss'], r['mode'], r['arena'], 's' + r['seed'], r['result'], r['sec'] + 's', 'deaths', r['deaths'], '| avg death time', r['avgSurvSec'], '| takenBoss', r['takenBoss'], 'takenAdd', r['takenAdd'], 'takenOther', r['takenOther'], '|', r['dmgSources'][:150])
print()
for k, d in tot.items():
    s = sum(d.values())
    print(k, 'n=%d' % cnt[k], ', '.join(f"{n} {100*v/s:.0f}%" for n, v in sorted(d.items(), key=lambda x: -x[1])[:5]))

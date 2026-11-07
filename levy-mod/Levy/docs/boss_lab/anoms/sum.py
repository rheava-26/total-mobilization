import csv, sys, glob, collections
# usage: sum.py out/<file>.tsv ...   one line per fight + aggregate
for fn in sys.argv[1:]:
    rows = list(csv.DictReader(open(fn), delimiter='\t'))
    print(fn.split('/')[-1], len(rows), 'fights')
    agg = collections.Counter(); dm = []; sec = []
    for x in rows:
        k = x['key'].split('|')
        print('  %-34s %-7s %6s left %5s/%-5s dmg %5s deaths %2s tb %5s ta %4s hitsB %4s addK %3s dpsH %5s E-dmg %4s R %4s M %4s S %4s' % (
            '|'.join(k[1:5]) + ('|' + '|'.join(k[6:]) if len(k) > 6 else ''), x['result'], x['sec'], x['bossLeft'], x['bossMax'], x['dmgBoss'], x['deaths'], x['takenBoss'], x['takenAdd'], x['hitsBoss'], x['addKills'], x['dpsHittable'], x['dmgE'], x['dmgR'], x['dmgM'], x['dmgS']))
        agg[x['result']] += 1; dm.append(float(x['dmgBoss'])); sec.append(float(x['sec']))
    print('  ->', dict(agg), 'mean dmg %.0f' % (sum(dm) / max(1, len(dm))), 'mean sec %.0f' % (sum(sec) / max(1, len(sec))))

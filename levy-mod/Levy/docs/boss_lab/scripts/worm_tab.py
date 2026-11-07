import csv, sys, collections
rows = []
for f in sys.argv[1:]:
    rows += [r for r in csv.DictReader(open(f), delimiter='\t') if r['kind'] == 'fight' and r['boss'] in ('eow', 'ds') and 'cfar' in r['key']]
g = collections.OrderedDict()
for r in rows: g.setdefault((r['boss'], r['mode'], r['arena']), []).append(r)
print("boss mode arena | n | wins/lost | deaths | win sec | boss%dmg(avg of sec) | head/body/tail/add dmg | hits/s | deaths-at(s) avg | tgt L%")
for k, v in sorted(g.items()):
    w = [r for r in v if r['result'] == 'WIN']
    def a(key): return sum(float(r[key]) for r in v) / len(v)
    pre = a('dmgHead') + a('dmgBody') + a('dmgTail')
    hp = sum(float(r['bossLeft']) / max(1, float(r['bossMax'])) for r in v) / len(v)
    avgdeath = sum(float(r['avgSurvSec']) for r in v) / len(v)
    print(f"{k[0]:4} {k[1]:4} {k[2]:5} | {len(v)} | {len(w)}/{sum(1 for r in v if r['result']=='LOSS')} | {a('deaths'):4.1f}/{v[0]['n']} | {sum(float(r['sec']) for r in w)/len(w) if w else float('nan'):5.0f} | left {100*hp:3.0f}% | {a('dmgHead'):5.0f}/{a('dmgBody'):5.0f}/{a('dmgTail'):4.0f}/{a('dmgAdd'):4.0f} | {a('hitsPerSec'):4.1f} | {avgdeath:4.0f} | {a('tgtLevyPct'):.0f}")

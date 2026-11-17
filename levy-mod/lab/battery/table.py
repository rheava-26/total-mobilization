"""Win/loss table per boss from fights.json: table.py FIGHTS.json"""
import json, sys, statistics as S
F = json.load(open(sys.argv[1]))
ORDER = ['eve', 'eve_brimflame', 'optimal', 'optimal_ranged']
def med(x): return S.median(x) if x else None
for boss, title in (('ravager', 'Ravager (Master+Death)'), ('eol', 'Empress of Light, night (Master+Death)')):
    print(f'\n### {title}\n')
    print('| army | wins | kill time s, median (range) | boss HP left in losses | army DPS median | per-levy DPS | levy deaths median | first death s | levies not engaged % | levies >50 tiles from boss % |')
    print('|---|---|---|---|---|---|---|---|---|---|')
    for a in ORDER:
        fs = [f for f in F if f['army'] == a and f['boss'] == boss]
        if not fs: continue
        w = [f for f in fs if f['result'] == 'WIN']; l = [f for f in fs if f['result'] != 'WIN']
        kt = sorted(f['sec'] for f in w)
        left = ', '.join(f"{100 * f['bossLeft'] / f['bossMax']:.0f}%" for f in l) or '-'
        ne = [f['rec']['notEngagedPct'] for f in fs if 'rec' in f]; far = [f['rec']['farOver50tilesPct'] for f in fs if 'rec' in f]
        fd = [float(f['firstDeathSec']) for f in fs if f['firstDeathSec'] not in ('', None)]
        print(f"| {a} | **{len(w)}/{len(fs)}** | {med(kt) or '-':} ({kt[0]:.0f}-{kt[-1]:.0f}) | {left} | {med([f['armyDps'] for f in fs]):.0f} | {med([f['perLevyDps'] for f in fs]):.0f} | {med([f['deaths'] for f in fs])}/34 | {med(fd):.1f} | {med(ne)} | {med(far)} |"
              if kt else f"| {a} | **0/{len(fs)}** | - | {left} | {med([f['armyDps'] for f in fs]):.0f} | {med([f['perLevyDps'] for f in fs]):.0f} | {med([f['deaths'] for f in fs])}/34 | {med(fd):.1f} | {med(ne)} | {med(far)} |")

"""Collects a battery run: one row per fight (results.csv + the recording's levy-state stats), recordings gzipped as <army>_<boss>_<n>.levyrec.
  collect.py RUN_DIR [RUN_DIR...] --dest DIR   -> DIR/*.levyrec, DIR/fights.json; prints a per-fight and a per-cell table."""
import argparse, csv, gzip, json, re, shutil, statistics, sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import recstats
ap = argparse.ArgumentParser(); ap.add_argument('runs', nargs='+'); ap.add_argument('--dest', required=True); a = ap.parse_args()
dest = Path(a.dest); dest.mkdir(parents=True, exist_ok=True)
fj = dest / 'fights.json'; fights = json.loads(fj.read_text()) if fj.exists() else []
seen = {(f['run'], f['cell'], f['seed']) for f in fights}
BOSSN = {'ravager': 'ravager', 'eol': 'eol'}
for run in map(Path, a.runs):
    cells = {c['id']: c for c in json.loads((run / 'cells.json').read_text())['cells']}
    recs = {}
    for f in sorted((run / 'rec').glob('*/*.jsonl')):
        try: hdr = json.loads(open(f).readline())
        except Exception: continue
        m = re.search(r'\|t(c\d+)(?:\||$)', hdr.get('meta', {}).get('cellkey', ''))
        if m:
            k = (m[1], int(hdr['meta'].get('seed', 0)))
            if k not in recs or f.stat().st_size > recs[k].stat().st_size: recs[k] = f
    for r in csv.DictReader(open(run / 'results.csv')):
        key = (str(run), r['cell'], int(r['seed']))
        if key in seen: continue
        mm = re.search(r'meta=bat_(\w+?)_(ravager|eol)\b', cells[r['cell']]['name'])
        if not mm: continue
        army, boss = mm.groups()
        n = int(r['seed'])  # file name carries the seed
        row = dict(run=str(run), cell=r['cell'], seed=int(r['seed']), army=army, boss=boss, result=r['result'], sec=float(r['sec'] or 0), levies=int(r['n'] or 0), deaths=int(r['deaths'] or 0),
                   firstDeathSec=r['firstDeathSec'], bossMax=float(r['bossMax'] or 0), bossLeft=float(r['bossLeft'] or 0), dmgBoss=float(r['dmgBoss'] or 0), potionsDrunk=r['potionsDrunk'],
                   weaponDmg=r['weaponDmg'], deathTimes=r['deathTimes'], classDmg=r['classDmg'], dmgRMES=[r['dmgR'], r['dmgM'], r['dmgE'], r['dmgS']])
        row['armyDps'] = round(row['dmgBoss'] / row['sec'], 1) if row['sec'] else 0
        row['perLevyDps'] = round(row['armyDps'] / max(1, row['levies']), 1)
        f = recs.get((r['cell'], int(r['seed'])))
        if f:
            s = recstats.stats(str(f)); row['rec'] = {k: s[k] for k in ('stateShare', 'notEngagedPct', 'farOver50tilesPct', 'meanDistToBoss', 'crowdedPct', 'deaths', 'levyHitDmg', 'topHitSrc')}
            t = dest / f'{army}_{boss}_{n}.levyrec'
            with open(f, 'rb') as src, gzip.open(t, 'wb', compresslevel=9) as z: shutil.copyfileobj(src, z)
            row['file'] = str(t)
        fights.append(row); seen.add(key)
fj.write_text(json.dumps(fights, indent=1))
print('| army | boss | seed | result | time s | boss HP left | army DPS | per-levy DPS | deaths | first death s | not engaged % | >50 tiles from boss % | file |')
print('|---|---|---|---|---|---|---|---|---|---|---|---|---|')
for f in fights:
    rc = f.get('rec', {})
    print(f"| {f['army']} | {f['boss']} | {f['seed']} | {f['result']} | {f['sec']:.0f} | {100 * f['bossLeft'] / max(1, f['bossMax']):.0f}% | {f['armyDps']:.0f} | {f['perLevyDps']:.0f} | {f['deaths']}/{f['levies']} | {f['firstDeathSec']} | {rc.get('notEngagedPct', '')} | {rc.get('farOver50tilesPct', '')} | {Path(f.get('file', '-')).name} |")
print()
print('| army | boss | wins | median time s (wins) | median army DPS | median deaths |')
print('|---|---|---|---|---|---|')
groups = {}
for f in fights: groups.setdefault((f['army'], f['boss']), []).append(f)
for (ar, b), fs in groups.items():
    w = [f for f in fs if f['result'] == 'WIN']
    print(f"| {ar} | {b} | {len(w)}/{len(fs)} | {statistics.median([f['sec'] for f in w]) if w else '-'} | {statistics.median([f['armyDps'] for f in fs]):.0f} | {statistics.median([f['deaths'] for f in fs])} |")

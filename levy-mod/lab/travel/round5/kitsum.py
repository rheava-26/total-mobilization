#!/usr/bin/env python3
# kitsum.py RUN_DIR : per cell (with the arm/boss name), wins, deaths, hits taken, damage taken (boss+add+other), damage dealt, boss life left %, avg survival s, hook casts/min per levy, hooked s per levy, wing hold/release s per levy
import csv, json, sys, statistics as st
rd = sys.argv[1]
meta = json.load(open(rd + '/cells.json'))
names = {c['id']: c['name'] for c in meta['cells']}
rows = list(csv.DictReader(open(rd + '/results.csv')))
def f(x):
    try: return float(x)
    except Exception: return None
def mean(v):
    v = [x for x in v if x is not None]
    return st.mean(v) if v else float('nan')
print('cell'.ljust(22), 'wins', 'deaths/n', 'hits', 'dmgTaken', 'dmgDealt', 'bossLeft%', 'surv s', 'sec', 'casts/min/levy', 'hooked s/levy', 'hang s/levy', 'wingHold s/levy', 'wingRel s/levy')
for cid in sorted(names, key=lambda c: int(c[1:]) if c[1:].isdigit() else 0):
    r = [x for x in rows if x['cell'] == cid and x['result'] not in ('MISSING', 'ERROR')]
    if not r: continue
    n = mean([f(x['n']) for x in r]); wins = sum(x['result'] == 'WIN' for x in r)
    left = mean([f(x['bossLeft']) / f(x['bossMax']) * 100 for x in r if f(x['bossMax'])])
    tk = mean([sum((f(x.get(k)) or 0) for k in ('takenBoss', 'takenAdd', 'takenOther')) for x in r])
    mins = mean([f(x['sec']) for x in r]) / 60
    cm = mean([(f(x.get('hkCasts')) or 0) for x in r]) / mins / n
    print(names[cid].ljust(22), f'{wins}/{len(r)}', f'{mean([f(x["deaths"]) for x in r]):.1f}/{n:.0f}', f'{mean([f(x["hitsTaken"]) for x in r]):.0f}', f'{tk:.0f}', f'{mean([f(x["dmgBoss"]) for x in r]):.0f}', f'{left:.0f}', f'{mean([f(x["avgSurvSec"]) for x in r]):.0f}', f'{mins * 60:.0f}',
          f'{cm:.2f}', f'{mean([(f(x.get("hookedSec")) or 0) for x in r]) / n:.2f}', f'{mean([(f(x.get("hangSec")) or 0) for x in r]) / n:.2f}', f'{mean([(f(x.get("wingHoldSec")) or 0) for x in r]) / n:.2f}', f'{mean([(f(x.get("wingRelSec")) or 0) for x in r]) / n:.2f}')

import csv, glob, gzip, json, os, shutil, re
# lab 2 notable-replay picker. Reads /home/user/lab/out2/<spec>.tsv + /home/user/lab/rec2/<spec>/*.jsonl, copies gz'd picks to OUT with index.txt
OUT = '/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/replays/notable2'
R2 = '/home/user/lab/rec2'; O2 = '/home/user/lab/out2'
os.makedirs(OUT, exist_ok=True)
idx = {}  # (spec, cellkey) -> [files]
for d in os.listdir(R2):
    for fn in os.listdir(f'{R2}/{d}'):
        try: m = json.loads(open(f'{R2}/{d}/{fn}').readline())['meta']
        except Exception: continue
        idx.setdefault((d, m.get('cellkey')), []).append(f'{R2}/{d}/{fn}')
rows = []
for f in glob.glob(O2 + '/*.tsv'):
    b = os.path.basename(f)[:-4]
    if b.endswith(('.hit', '.dbg', '.levy')) or b == 'smoke': continue
    for r in csv.DictReader(open(f), delimiter='\t'):
        if r.get('kind') == 'fight': r['_spec'] = b; rows.append(r)
def fl(r, k):
    try: return float(r[k])
    except Exception: return 0.0
def rec(r):
    ks = [k for k in idx if k[0] == r['_spec'] and r['key'].startswith(k[1] or '#')]
    ks = [k for k in ks if (k[1] == r['key']) or ('|fe' not in r['key'])]
    return idx[ks[0]][0] if ks else None
picked = []
def add(r, why):
    p = rec(r)
    if p and p not in [x[0] for x in picked]: picked.append((p, why, r))
def sel(spec_prefix, boss=None, mode=None, res=None):
    return [r for r in rows if r['_spec'].startswith(spec_prefix) and (boss is None or r['boss'] == boss) and (mode is None or r['mode'] == mode) and (res is None or r['result'] == res)]
mains = [r for r in rows if r['_spec'] in ('main_a', 'main_b', 'main_x')]
# closest wins (highest deaths/levy, then longest)
for r in sorted([r for r in mains if r['result'] == 'WIN'], key=lambda r: -(fl(r, 'deaths') / fl(r, 'n') + fl(r, 'sec') / 3000))[:4]:
    add(r, f"closest win: {r['boss']} {r['mode']}, {r['deaths']}/{r['n']} levies dead, kill at {r['sec']} s")
# worms, spread on: one typical DS Expert wipe, one EoW Expert win, one EoW Expert wipe; spread-off contrast
# NOTE: the recorder produced no file for Eater of Worlds fights (observed, also in lab 1), so the worm picks are Desert Scourge
for boss, res, why in (('ds', 'LOSS', 'typical DS Expert wipe, spread ON'),):
    v = sorted([r for r in mains if r['boss'] == boss and r['mode'] == 'exp' and r['result'] == res], key=lambda r: fl(r, 'sec'))
    if v: add(v[len(v) // 2], f"{why}: {v[len(v)//2]['deaths']}/{v[len(v)//2]['n']} dead at {v[len(v)//2]['sec']} s")
v = sorted(sel('spreadoff', 'eow', 'exp'), key=lambda r: fl(r, 'sec'))
if v: add(v[len(v) // 2], f"EoW Expert, spread OFF (LEVY_SPREAD=0): {v[len(v)//2]['result']} at {v[len(v)//2]['sec']} s, {v[len(v)//2]['deaths']} dead")
v = sorted([r for r in mains if r['boss'] == 'ds' and r['mode'] == 'exp'], key=lambda r: fl(r, 'sec'))
if v: add(v[-1], f"DS Expert, spread ON: longest fight {v[-1]['sec']} s")
v = sorted([r for r in mains if r['boss'] == 'ds' and r['mode'] == 'rev'], key=lambda r: fl(r, 'sec'))
if v: add(v[len(v) // 2], f"DS Rev, spread ON: typical wipe {v[len(v)//2]['sec']} s")
v = sorted(sel('spreadoff', 'ds', 'exp'), key=lambda r: fl(r, 'sec'))
if v: add(v[0], f"DS Expert, spread OFF: first-seen fastest wipe {v[0]['sec']} s")
# weird
for r in mains:
    if r['boss'] == 'skel' and r['result'] == 'TIMEOUT' and fl(r, 'bossMoved') > 400000 and fl(r, 'deaths') <= 8 and r['seed'] in ('5', '9'):
        add(r, f"WEIRD: Skeletron flies away (moved {fl(r,'bossMoved'):.0f} px), counted TIMEOUT, {r['deaths']} dead, boss left {r['bossLeft']}/{r['bossMax']}");
for boss, mode, res, why in (('qb', 'exp', 'LOSS', 'QB Expert wipe (all 15 dead)'), ('qb', 'rev', 'WIN', 'QB Rev win, few deaths'), ('eye', 'rev', 'LOSS', 'Eye Rev wipe, boss targets levies 100%'),
                             ('brain', 'rev', 'LOSS', 'Brain Rev wipe behind the creeper shield (0 damage to the Brain)'), ('kslime', 'death', 'LOSS', 'King Slime Death wipe'), ('eye', 'rev', 'WIN', 'Eye Rev win')):
    v = [r for r in mains if r['boss'] == boss and r['mode'] == mode and r['result'] == res]
    if v: v.sort(key=lambda r: fl(r, 'sec')); add(v[len(v) // 2], f"{why}: {v[len(v)//2]['deaths']}/{v[len(v)//2]['n']} dead at {v[len(v)//2]['sec']} s")
for r in sel('fearoff_x', 'eow', 'exp', 'WIN')[:1]: add(r, f"fear OFF EoW Expert win at {r['sec']} s")
for r in sel('revoff', 'qb')[:1]: add(r, f"QB Rev with levy Rage/Adrenaline OFF: {r['result']} {r['sec']} s, {r['deaths']} dead")
lines = ['Notable replays, lab 2 (Levy 2.0.16, Calamity 2.2.2). jsonl.gz, FightRecorder format; open with levy-mod/Levy/tools/fightviewer.', 'Cell key, spec file and seed are in each file header; the spec the fight came from is in the "src" column.', '']
for p, why, r in picked[:18]:
    dst = os.path.basename(p) + '.gz'
    # prefix with the spec so same-named files from different specs do not collide
    dst = r['_spec'] + '__' + dst
    with open(p, 'rb') as a, gzip.open(f'{OUT}/{dst}', 'wb') as b: shutil.copyfileobj(a, b)
    lines += [dst, f"    {why}", f"    key {r['key']}  result {r['result']}  src {r['_spec']}", '']
open(OUT + '/index.txt', 'w').write('\n'.join(lines)); print(len(picked[:18]), 'picked ->', OUT)

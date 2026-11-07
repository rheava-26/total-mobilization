import os, csv, json, gzip, shutil, glob, statistics as st, subprocess
OUT = '/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/replays/notable'
D = '/home/user/lab/rec'
subprocess.run(['python3', '-I', '/home/user/lab/recindex.py'], capture_output=True)
idx = json.load(open('/home/user/lab/rec_index.json'))
rows = {}
for f in glob.glob('/home/user/lab/out/*.tsv'):
    if f.endswith('.dbg.tsv') or 'smoke' in f: continue
    for r in csv.DictReader(open(f), delimiter='\t'):
        if r.get('kind') == 'fight' and r['result'] != 'ERROR': rows[r['key']] = r
rows = list(rows.values())
def fl(r, k):
    try: return float(r[k])
    except: return 0.0
picked = []  # (key, why)
def add(r, why):
    if r['key'] in idx and r['key'] not in [p[0] for p in picked]: picked.append((r['key'], why, r))
far = [r for r in rows if '|cfar' in r['key'] and r['gear'] == 'mod' and '|bhp' not in r['key'] and '|dr' not in r['key'] and '|cl' not in r['key'] and '|hs' not in r['key']]
# closest wins: wins with the most deaths per levy, then longest
wins = [r for r in far if r['result'] == 'WIN']
for r in sorted(wins, key=lambda r: -(fl(r, 'deaths') / fl(r, 'n') + fl(r, 'sec') / 3000))[:5]:
    add(r, f"closest win: {r['deaths']}/{r['n']} levies dead, kill at {r['sec']}s")
# fastest wipes
for r in sorted([r for r in far if r['result'] == 'LOSS'], key=lambda r: fl(r, 'sec'))[:5]:
    add(r, f"fast wipe: all {r['n']} levies dead in {r['sec']}s (boss {r['boss']} {r['mode']} {r['arena']})")
# one typical per boss/mode (kill or loss nearest the median seconds)
cells = {}
for r in far: cells.setdefault((r['boss'], r['mode'], r['arena']), []).append(r)
for k, v in sorted(cells.items()):
    if len(v) < 3: continue
    med = st.median(fl(r, 'sec') for r in v)
    r = min((r for r in v if r['key'] in idx), key=lambda r: abs(fl(r, 'sec') - med), default=None)
    if r: add(r, f"typical {k[0]} {k[1]} {k[2]} (cell n={len(v)}, median {med:.0f}s): {r['result']} {r['sec']}s deaths {r['deaths']}/{r['n']}")
# named anomalies (key substring -> why)
named = [
    ('fight|eye|rev|free|1|18000|n11|gmod|grlv', 'ANOMALY targeting: Eye Rev with the commander near the levies; boss target is the Spectator commander ~54% of ticks and hovers over him (timeout, 0 hits on levies in those spans)'),
    ('fight|brain|rev|free|1|7200|n13|gmod|grlv|cfar', 'ANOMALY targeting + shield: Brain Rev, diag run; target = commander while its lock is -1 (Compete: vanilla pick closer), dontTakeDamage ~100%, creepers 18'),
    ('fight|ds|rev|sd|2|36000|n11|gmod|grlv|cfar', 'ANOMALY: Desert Scourge Rev, all 11 levies dead in 3 s (2.0.12 build): the head+body drove through the clustered levies'),
    ('fight|kslime|exp|free|1|18000|n10|gmod|grlv|cfar', 'ANOMALY: King Slime Expert timeout at 300 s, only ~6 dmg/s from 10 levies'),
    ('fight|qb|exp|free|1|18000|n15|gmod|grlv|cfar', 'Queen Bee Expert: 15 levies wiped in 40 s'),
    ('fight|ds|exp|free|1|18000|n11|gmod|grlv|cfar', 'worm damage-source log (worms_fix): Desert Scourge Expert surface, every hit attributed (body 74% / head 18% / unattributed 8%)'),
    ('fight|eow|exp|free|1|18000|n13|gmod|grlv|cfar', 'worm damage-source log (worms_fix): Eater of Worlds Expert surface (may be several files: segments split)'),
]
for sub, why in named:
    for key in idx:
        if key.startswith(sub) and '|bhp' not in key:
            r = next((x for x in rows if x['key'] == key), None)
            if r and key not in [p[0] for p in picked]: picked.append((key, why, r))
os.makedirs(OUT, exist_ok=True)
for fn in os.listdir(OUT): os.remove(os.path.join(OUT, fn))
lines = []
for key, why, r in picked:
    names = []
    for fn in idx[key]:
        src = os.path.join(D, fn)
        dst = os.path.join(OUT, fn if fn.endswith('.gz') else fn + '.gz')
        if fn.endswith('.gz'): shutil.copy(src, dst)
        else:
            with open(src, 'rb') as a, gzip.open(dst, 'wb') as b: shutil.copyfileobj(a, b)
        names.append(os.path.basename(dst))
    lines.append(f"{' + '.join(names)}\n    {why}\n    cell {key}: {r['result']} at {r['sec']}s, boss left {r['bossLeft']}/{r['bossMax']}, deaths {r['deaths']}/{r['n']}, boss target L{r.get('tgtLevyPct','-')}/P{r.get('tgtPlayerPct','-')}%\n")
open(os.path.join(OUT, 'index.txt'), 'w').write('Notable replays (jsonl.gz, FightRecorder format; open with levy-mod/Levy/tools/fightviewer, gallery: make_gallery.py <out.html> <files>).\nMode/arena/seed/gear are in each file name; the full cell key is below.\n\n' + '\n'.join(lines))
print(len(picked), 'picked;', sum(os.path.getsize(os.path.join(OUT, f)) for f in os.listdir(OUT)) // 1024 // 1024, 'MB')

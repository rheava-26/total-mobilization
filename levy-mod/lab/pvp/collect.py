"""Collects PvP lab runs: one row per fight from <run>/jobs/*/try*/out.tsv.pvp.tsv, recordings gzipped as <A>_vs_<B>_seed<N>.levyrec (A = the left army).
  collect.py RUN_DIR [RUN_DIR...] --dest DIR    -> DIR/*.levyrec, DIR/fights.json; prints the per-fight table, the per-matchup table and the weapon table."""
import argparse, collections, csv, gzip, json, re, shutil, statistics
from pathlib import Path
ap = argparse.ArgumentParser(); ap.add_argument('runs', nargs='+'); ap.add_argument('--dest', required=True); ap.add_argument('--no-rec', action='store_true'); ap.add_argument('--skip', default='', help='armies whose fights in these runs are left out (a rerun replaces them)'); a = ap.parse_args()
SKIP = set(filter(None, a.skip.split(',')))
dest = Path(a.dest); dest.mkdir(parents=True, exist_ok=True)
fj = dest / 'fights.json'; fights = json.loads(fj.read_text()) if fj.exists() else []
seen = {f['key'] for f in fights}

def kv(s):
    return {k: int(v) for k, v in (x.split('=') for x in s.split()) if v.lstrip('-').isdigit()} if s else {}

for run in map(Path, a.runs):
    recs = {}
    for f in sorted((run / 'rec').glob('*/*.jsonl')):
        try: hdr = json.loads(open(f).readline())
        except Exception: continue
        ck = hdr.get('meta', {}).get('cellkey')
        if ck and (ck not in recs or f.stat().st_size > recs[ck].stat().st_size): recs[ck] = f
    for t in sorted(run.glob('jobs/*/try*/out.tsv.pvp.tsv')):
        for r in csv.DictReader(open(t, errors='replace'), delimiter='\t'):
            if r['key'] in seen or r['teamA'] in SKIP or r['teamB'] in SKIP: continue
            row = dict(key=r['key'], run=str(run), seed=int(r['seed']), A=r['teamA'], B=r['teamB'], result=r['result'], winner=r['winner'], sec=float(r['sec']),
                       nA=int(r['nA']), nB=int(r['nB']), aliveA=int(r['aliveA']), aliveB=int(r['aliveB']), hpLeftA=int(r['hpLeftA']), hpLeftB=int(r['hpLeftB']),
                       dmgA=int(r['dmgA']), dmgB=int(r['dmgB']), classA=kv(r['classA']), classB=kv(r['classB']), topA=r['topA'], topB=r['topB'],
                       deathsA=[float(x) for x in r['deathsA'].split(',') if x], deathsB=[float(x) for x in r['deathsB'].split(',') if x],
                       potionsA=int(r['potionsA']), potionsB=int(r['potionsB']), unattr=[int(r['unattrA']), int(r['unattrB'])], dot=[int(r['dotA']), int(r['dotB'])], perA=r['perA'], perB=r['perB'])
            f = recs.get(r['key'])
            if f and not a.no_rec:
                out = dest / f"{row['A']}_vs_{row['B']}_seed{row['seed']}.levyrec"
                with open(f, 'rb') as src, gzip.open(out, 'wb', compresslevel=9) as z: shutil.copyfileobj(src, z)
                row['file'] = str(out)
            fights.append(row); seen.add(r['key'])
fj.write_text(json.dumps(fights, indent=1))

def side(f, army):  # -> 'A' or 'B' for the army's side in this fight
    return 'A' if f['A'] == army else 'B'
def won(f, army): return f['winner'] == army

print('| left (A) | right (B) | seed | winner | time s | survivors A / B | damage dealt A / B | deaths by 10 s A / B | file |')
print('|---|---|---|---|---|---|---|---|---|')
for f in fights:
    d10 = lambda ds: sum(1 for x in ds if x <= 10)
    print(f"| {f['A']} | {f['B']} | {f['seed']} | **{f['winner']}**{' (cap)' if 'TIMEOUT' in f['result'] else ''} | {f['sec']:.0f} | {f['aliveA']}/{f['nA']} vs {f['aliveB']}/{f['nB']} | {f['dmgA']} / {f['dmgB']} | {d10(f['deathsA'])} / {d10(f['deathsB'])} | {Path(f.get('file', '-')).name} |")
print()
pairs = collections.OrderedDict()
for f in fights: pairs.setdefault(tuple(sorted((f['A'], f['B']), key=lambda x: (not x.startswith('eve'), x))), []).append(f)
print('| matchup | wins X / Y / draw | X wins on the left / right | median time s | median survivors (winner) | X damage dealt (median) | Y damage dealt (median) | X class split of damage | Y class split of damage |')
print('|---|---|---|---|---|---|---|---|---|')
def cls(fs, army):
    tot = collections.Counter()
    for f in fs: tot.update(f['classA'] if side(f, army) == 'A' else f['classB'])
    s = sum(tot.values()) or 1
    return ' '.join(f"{k} {100 * v / s:.0f}%" for k, v in sorted(tot.items(), key=lambda kv: -kv[1]))
for (x, y), fs in pairs.items():
    wx = sum(won(f, x) for f in fs); wy = sum(won(f, y) for f in fs); dr = len(fs) - wx - wy
    wl = sum(won(f, x) for f in fs if f['A'] == x); wr = sum(won(f, x) for f in fs if f['B'] == x)
    nl = sum(1 for f in fs if f['A'] == x); nr = len(fs) - nl
    surv = [f['aliveA'] if f['winner'] == f['A'] else f['aliveB'] for f in fs if f['winner'] in (f['A'], f['B'])]
    dx = [f['dmgA'] if side(f, x) == 'A' else f['dmgB'] for f in fs]; dy = [f['dmgA'] if side(f, y) == 'A' else f['dmgB'] for f in fs]
    print(f"| {x} vs {y} | {wx} / {wy} / {dr} | {wl}/{nl} / {wr}/{nr} | {statistics.median(f['sec'] for f in fs):.0f} | {statistics.median(surv) if surv else '-'} | {statistics.median(dx):.0f} | {statistics.median(dy):.0f} | {cls(fs, x)} | {cls(fs, y)} |")
print()
# weapons: total damage per weapon per army over all its fights, and per-levy average
print('| army | weapon x levies | damage per levy per fight | share of the army damage | died (% of levies) |')
print('|---|---|---|---|---|')
for army in sorted({f['A'] for f in fights} | {f['B'] for f in fights}):
    wd = collections.Counter(); wn = collections.Counter(); wdead = collections.Counter(); nf = 0
    for f in fights:
        if army not in (f['A'], f['B']): continue
        nf += 1
        per = f['perA'] if side(f, army) == 'A' else f['perB']
        for e in per.split(';'):
            p = e.split(':')
            if len(p) < 4: continue
            w = p[1].replace('CalamityMod/', '').split('@')[0]; wd[w] += int(p[2][3:]); wn[w] += 1; wdead[w] += p[3].startswith('dead')
    tot = sum(wd.values()) or 1
    for w, d in wd.most_common(8):
        print(f"| {army} | {w} x{wn[w] // max(1, nf)} | {d / max(1, wn[w]):.0f} | {100 * d / tot:.0f}% | {100 * wdead[w] / max(1, wn[w]):.0f}% |")

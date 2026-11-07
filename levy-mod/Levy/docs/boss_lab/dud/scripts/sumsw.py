import sys, re, csv
# usage: sumsw.py out.tsv [serverlog]  -> weapon, family, dmg total, per-levy dps (n=4, 30 s), hitsBoss
f = sys.argv[1]
rows = list(csv.reader(open(f), delimiter='\t'))
h = rows[0]; ix = {k: i for i, k in enumerate(h)}
fam = {}
for lg in sys.argv[2:]:
    for l in open(lg, errors='ignore'):
        m = re.search(r'weapon (CalamityMod/\w+): family=(\w+) supported=(\w+) melee=(\w+) projMelee=(\w+) shortShot=(\w+) pureContact=(\w+) range=(\d+) meleeReach=(\d+)', l)
        if m: fam[m.group(1)] = m.groups()[1:]
out = []
for r in rows[1:]:
    if len(r) < len(h) - 3: continue
    m = re.search(r'cw(CalamityMod/\w+)', r[0])
    if not m: continue
    w = m.group(1)
    sec = float(r[ix['sec']]); n = int(r[ix['n']])
    d = float(r[ix['dmgBoss']]); hits = r[ix['hitsBoss']]
    # dmg to adds too is not counted
    out.append((w, fam.get(w, ('?',) * 8), d, d / sec / n, hits, r[ix['dmgProj']], r[ix['dmgMelee']], r[ix['result']]))
for w, fm, d, dps, hits, dp, dm, res in sorted(out, key=lambda x: x[3]):
    print(f"{w.split('/')[1]:28s} {fm[0]:12s} sup={fm[1][:1]} melee={fm[2][:1]} pm={fm[3][:1]} ss={fm[4][:1]} rng={fm[6]:>3s} dmg={d:8.0f} dps/levy={dps:6.2f} hits={hits} proj={dp} mel={dm} {res}")

import re, csv, subprocess, sys
# usage: table.py  -> prints markdown table joining DS-before, SP-before, SP-after
W = '/home/user/total-mobilization/.claude/worktrees/agent-ae27987db61f1c9b4/levy-mod/Levy/tools/weapon-investigation/s3.txt'
nom = {}
for l in open(W):
    if l.startswith('CalamityMod\t'):
        r = l.rstrip('\n').split('\t')
        nom.setdefault(r[1], (r[3], float(r[7])))   # class, dpsAdj


def load(f, logs):
    rows = list(csv.reader(open(f), delimiter='\t'))
    h = rows[0]; ix = {k: i for i, k in enumerate(h)}
    fam = {}
    for lg in logs:
        for l in open(lg, errors='ignore'):
            m = re.search(r'weapon (CalamityMod/\w+): family=(\w+) supported=(\w+) melee=(\w+) projMelee=(\w+) shortShot=(\w+) pureContact=(\w+) range=(\d+)', l)
            if m: fam[m.group(1)] = (m.group(2), m.group(8))
    d = {}
    for r in rows[1:]:
        if len(r) < len(h) - 3: continue
        m = re.search(r'cw(CalamityMod/\w+)', r[0])
        if not m: continue
        w = m.group(1); sec = float(r[ix['sec']]); n = int(r[ix['n']])
        d[w.split('/')[1]] = (float(r[ix['dmgBoss']]) / sec / n, fam.get(w, ('?', '?')))
    return d


ds = load('/home/user/lab/out_e/sw_ds_before.tsv', ['/home/user/lab/scratchf/ds_before_server.log'])
spb = load('/home/user/lab/out_e/swB_sp_before.tsv', sys.argv[1:2] or ['/home/user/lab/scratchf/spb_server.log'])
spa = load('/home/user/lab/out_e/swF.tsv', sys.argv[2:3] or ['/home/user/lab/scratche/swF_server.log'])
print('| weapon | class (Calamity) | Levy family before -> after | nominal dps | DS-lab before | SP before | SP after |')
print('|---|---|---|---|---|---|---|')
for k in sorted(spa, key=lambda k: (spb.get(k, (0,))[0], k)):
    c, nd = nom.get(k, ('?', 0))
    fb = spb.get(k, (0, ('?', '?')))[1]; fa = spa[k][1]
    fam = fb[0] if fb[0] == fa[0] else f'{fb[0]} -> {fa[0]}'
    print(f"| {k} | {c} | {fam} | {nd:.0f} | {ds.get(k, (float('nan'),))[0]:.2f} | {spb.get(k, (float('nan'),))[0]:.2f} | {spa[k][0]:.2f} |")

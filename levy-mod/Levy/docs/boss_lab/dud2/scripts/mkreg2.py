import re, csv
D = '/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/dud2/'
def load(f):
    rows = list(csv.reader(open(D + f), delimiter='\t')); h = rows[0]; ix = {k: i for i, k in enumerate(h)}
    d = {}
    for r in rows[1:]:
        if len(r) < len(h) - 3: continue
        m = re.search(r'cw(CalamityMod/\w+)', r[0])
        if m: d[m.group(1).split('/')[1]] = float(r[ix['dmgBoss']]) / float(r[ix['sec']]) / int(r[ix['n']])
    return d
a = load('reg1b_out.tsv'); b = load('reg1_out.tsv')
cls = {}
for r in csv.DictReader(open('/home/user/total-mobilization/.claude/worktrees/agent-a23640dd34068e3b6/levy-mod/Levy/docs/boss_lab/dud/sweep_table.tsv'), delimiter='\t'):
    cls[r['weapon']] = {'magic': 'M', 'ranged': 'R', 'melee': 'E', 'summon': 'S'}.get(r['calamity_class'], 'M')
ws = [w for w in a if w in b and a[w] > 20 and b[w] < 0.8 * a[w]]
print(len(ws), ws)
open(D + 'wreg2.txt', 'w').write('\n'.join(f"{w} {cls.get(w, 'M')}" for w in ws) + '\n')

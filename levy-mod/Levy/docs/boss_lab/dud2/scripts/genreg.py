import csv, sys
# regression spec: every weapon the held routing table already covered, at the lab default mana pool, Eye x40, n=4, seeds given
R = '/home/user/total-mobilization/.claude/worktrees/agent-a23640dd34068e3b6/levy-mod/Levy/docs/boss_lab/dud/'
routed = []
for r in csv.DictReader(open(R + 'holdout_routes.tsv'), delimiter='\t'):
    if r['routed_to_held'].startswith('yes'): routed.append((r['weapon'], float(r['plain_dps_per_levy']), float(r['held_dps_per_levy'])))
cls = {}
for r in csv.DictReader(open(R + 'sweep_table.tsv'), delimiter='\t'):
    cls[r['weapon']] = {'magic': 'M', 'ranged': 'R', 'melee': 'E', 'summon': 'S'}.get(r['calamity_class'], 'M')
seeds = [int(x) for x in sys.argv[2].split(',')]
out = sys.argv[1]
L = []
for s in seeds:
    for w, _, _ in routed:
        L.append(f"fight\teye\texp\t{s}\t1800\tgear=mod\tn=4\tdr=1\tbhp=40\tcmd=far\tclone={cls.get(w, 'M')}\tcw=CalamityMod/{w}")
open(out, 'w').write('\n'.join(L) + '\n')
open(out + '.ref', 'w').write('\n'.join(f"{w}\t{p}\t{h}" for w, p, h in routed) + '\n')
print(len(routed), len(L))

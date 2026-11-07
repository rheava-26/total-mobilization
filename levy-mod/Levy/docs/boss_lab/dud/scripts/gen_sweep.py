import sys
# usage: gen_sweep.py <maxroute> <out.tsv> [cap]
maxroute = float(sys.argv[1]); out = sys.argv[2]; cap = sys.argv[3] if len(sys.argv) > 3 else '1800'
W = '/home/user/total-mobilization/.claude/worktrees/agent-ae27987db61f1c9b4/levy-mod/Levy/tools/weapon-investigation/s3.txt'
rows = [l.rstrip('\n').split('\t') for l in open(W) if l.startswith('CalamityMod\t')]
seen = set(); lines = []
extra = [a for a in sys.argv[4:]]
names = []
for r in rows:
    if float(r[12]) <= maxroute and r[1] not in seen:
        seen.add(r[1]); names.append((r[1], r[2], r[3]))
for e in extra:
    if e not in seen: names.append((e, '?', '?')); seen.add(e)
for n, fam, cls in names:
    c = {'Gun': 'R', 'Bow': 'R', 'Summon': 'S', 'Sentry': 'S'}.get(fam)
    if c is None:
        c = {'ranged': 'R', 'magic': 'M', 'melee': 'E', 'summon': 'S'}.get(cls, 'M') if fam == 'Magic' else 'E'
    lines.append(f"fight\teye\texp\t1\t{cap}\tgear=mod\tn=4\tdr=1\tbhp=40\tcmd=far\tclone={c}\tcw=CalamityMod/{n}")
open(out, 'w').write('\n'.join(lines) + '\n'); print(len(lines))

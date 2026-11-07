import sys
sys.path.insert(0, '/home/user/total-mobilization/.claude/worktrees/agent-a99f9c13d1a134264/levy-mod/Levy/docs/boss_lab/scripts')
# anomaly reruns with dbg=1 (30-tick boss traces in <out>.dbg.tsv); same line() helper as gen_specs2.py
def line(boss, mode, seed, **kw):
    cap = kw.pop('cap', 18000)
    kw.setdefault('gear', 'mod'); kw.setdefault('n', 'cp'); kw.setdefault('cmd', 'near')
    if boss == 'qb': kw.setdefault('jungle', '1')
    return f"fight\t{boss}\t{mode}\t{seed}\t{cap}" + ''.join(f"\t{k}={v}" for k, v in kw.items())
A = []
for s in (1, 2, 3): A.append(line('qb', 'exp', s, dbg='1'))
A.append(line('qb', 'rev', 1, dbg='1')); A.append(line('qb', 'rev', 1, dbg='1', jungle='0'))
for s in (1, 3, 5): A.append(line('skel', 'exp', s, dbg='1'))
A.append(line('ds', 'exp', 1, dbg='1')); A.append(line('eow', 'rev', 1, dbg='1')); A.append(line('eow', 'exp', 2, dbg='1'))
open('/home/user/lab/specs2/anom.tsv', 'w').write('\n'.join(A) + '\n'); print('anom', len(A))

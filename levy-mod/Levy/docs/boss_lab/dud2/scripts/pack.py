import subprocess, shutil, os, glob
D = '/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/dud2/'
R = '/home/user/total-mobilization/.claude/worktrees/agent-a23640dd34068e3b6/levy-mod/Levy/docs/boss_lab/dud2/'
os.makedirs(R + 'raw', exist_ok=True); os.makedirs(R + 'scripts', exist_ok=True); os.makedirs(R + 'specs', exist_ok=True)
def table(before, after):
    return subprocess.run(['python3', D + 'mktable.py', before, after], capture_output=True, text=True, cwd=D).stdout
out = []
out.append("# default lab mana pool (gear mana now counted), Eye x40, n=4 invulnerable levies, 30 s, LEVY_CR_SP=1: before = shipped 2.0.17 behaviour (same seeds), after = this branch\n" + table('F1b_out.tsv', 'F1_out.tsv'))
out.append("# mana pool 200 (mp=200: the weapon's cost is payable). before = shipped behaviour (plain shot), after = this branch\n" + table('mpA_out.tsv', 'F2_out.tsv'))
out.append("# sentries / summons: before = LEVY_SENTRY_OLD=1 (timeLeft pinned every tick, no owner target), after = this branch\n" + table('sOld_out.tsv,sO_out.tsv', 'sF1_out.tsv,sN_out.tsv,sA_out.tsv'))
out.append("# PlasmaRod / SparklingEmpress: before = base1 (shipped), after = wP (default pool, then mp200)\n" + table('base1_out.tsv', 'wP_out.tsv'))
out.append("# bdef0 rows: boss defense held at 0 (token bdef=0), same build as the after column\n" + table('base1_out.tsv', 'dZ_out.tsv'))
open(R + 'results_table.tsv', 'w').write('\n'.join(out))
for f in ['base1', 'mpA', 'mpB', 'F1', 'F1b', 'F2', 'hN', 'wP', 'dZ', 'sOld', 'sB', 'sC', 'sO', 'sN', 'sA', 'sF1', 'reg1', 'reg1b', 'reg2', 'reg2b', 'reg3', 'reg3b']:
    if os.path.exists(D + f + '_out.tsv'): shutil.copy(D + f + '_out.tsv', R + 'raw/')
for f in ['regcmp.py', 'mkreg2.py', 'mktable.py', 'genreg.py', 'shipcheck.sh', 'ab.py', 'sum.py', 'gen.py', 'tn.sh', 'go.sh', 'runx.sh', 'buildx.sh', 'buildbase.sh', 'pack.py']:
    if os.path.exists(D + f): shutil.copy(D + f, R + 'scripts/')
for f in ['wreg2.txt', 'wreg3.txt', 'wsf.txt', 'wsp.txt', 'ws5.txt', 'wh5.txt', 'wh.txt', 'w1.txt', 'wr.txt', 'wm.txt', 'ws.txt']:
    if os.path.exists(D + f): shutil.copy(D + f, R + 'specs/')
print('packed', len(open(R + 'results_table.tsv').read().splitlines()), 'lines')

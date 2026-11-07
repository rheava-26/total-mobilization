import sys
# lab 2: generates spec TSVs in /home/user/lab/specs2 (seed-major so a partial run still has every cell at 3 seeds)
CAP = 18000  # 5 min of game time
SEEDS = range(1, 7)
def line(boss, mode, seed, **kw):
    cap = kw.pop('cap', CAP)
    kw.setdefault('gear', 'mod'); kw.setdefault('n', 'cp'); kw.setdefault('cmd', 'near')
    if boss == 'qb': kw.setdefault('jungle', '1')
    toks = ''.join(f"\t{k}={v}" for k, v in kw.items())
    return f"fight\t{boss}\t{mode}\t{seed}\t{cap}{toks}"
def write(name, lines):
    open(f'/home/user/lab/specs2/{name}.tsv', 'w').write('\n'.join(lines) + '\n'); print(name, len(lines))
M3 = ['exp', 'rev', 'death']
def cells(bosses, extra=None, seeds=SEEDS):
    L = []
    for s in seeds:
        for boss, modes in bosses:
            for m in modes:
                L.append(line(boss, m, s, **(extra or {})))
    return L
# main table (fear on, cmd=near)
write('main_a', cells([('kslime', M3), ('ds', M3), ('eye', M3)]))
write('main_b', cells([('eow', M3), ('brain', M3), ('qb', M3), ('skel', ['exp'])]))
# fear-off arm (FearMultiplier=0)
write('fearoff_a', cells([('ds', ['exp', 'rev']), ('eye', ['rev']), ('eow', ['exp', 'rev'])], {'fear': '0'}))
write('fearoff_b', cells([('qb', M3), ('brain', ['rev'])], {'fear': '0'}))
# worm spread A/B: run with LEVY_SPREAD=0 (spread-on cells are the main table)
write('spreadoff', cells([('ds', ['exp']), ('eow', ['exp'])]))

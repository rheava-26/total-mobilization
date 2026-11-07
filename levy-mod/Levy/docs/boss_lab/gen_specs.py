import sys
# generates spec TSVs in /home/user/lab/specs
CAP = 36000  # 10 min of game time
def line(boss, mode, seed, **kw):
    cap = kw.pop('cap', CAP)
    kw.setdefault('cmd', 'far')  # commander parked outside the arena so the boss is never drawn to him (see RESULTS: Spectator targeting)
    toks = ''.join(f"\t{k}={v}" for k, v in kw.items())
    return f"fight\t{boss}\t{mode}\t{seed}\t{cap}{toks}"
def write(name, lines):
    open(f'/home/user/lab/specs/{name}.tsv', 'w').write('\n'.join(lines) + '\n'); print(name, len(lines))

# B1: tier gear at the real count, every boss/mode of the list
b1a, b1b = [], []
for seed in (1, 2, 3):
    for boss, modes in [('eye', ['exp', 'rev', 'death']), ('brain', ['exp', 'rev', 'death']), ('kslime', ['exp', 'rev']), ('qb', ['exp', 'rev'])]:
        for m in modes:
            (b1a if boss in ('eye', 'kslime') else b1b).append(line(boss, m, seed, gear='mod', n='cp', dbg='1' if seed == 1 else '0', **({'cmd': 'near'} if boss in ('eye', 'kslime') else {})))
    for boss, modes in [('eow', ['exp', 'rev']), ('ds', ['exp', 'rev'])]:
        for m in modes:
            b1b.append(line(boss, m, seed, gear='mod', n='cp', arena='surf', dbg='1' if seed == 1 else '0'))
    b1a.append(line('skel', 'exp', seed, gear='mod', n='cp', cmd='near'))
write('b1a', b1a); write('b1b', b1b)

# B2: count sweep eye / brain, exp / rev
for boss in ('eye', 'brain'):
    L = []
    for seed in (1, 2, 3):
        for mode in ('exp', 'rev'):
            for n in (4, 8, 12, 16, 19, 24):
                L.append(line(boss, mode, seed, gear='mod', n=n))
    write('b2_' + boss, L)

# B3: dps linearity (levies invulnerable, boss x40 hp, 60 s) + hpscale=0 count sweep
L = []
for seed in (1, 2):
    for n in (4, 8, 12, 16, 19, 24):
        L.append(line('eye', 'exp', seed, gear='mod', n=n, dr='1', bhp='40', cap=3600))
write('b3_dps', L)
L = []
for seed in (1, 2, 3):
    for n in (4, 8, 12, 16, 19, 24):
        L.append(line('eye', 'exp', seed, gear='mod', n=n, hpscale='0'))
write('b3_hpscale0', L)

# B4: worms, underground arena (surface cells are in B1); plus extra surface seeds
L = []
for seed in (1, 2, 3):
    for boss in ('eow', 'ds'):
        for mode in ('exp', 'rev'):
            L.append(line(boss, mode, seed, gear='mod', n='cp', arena='ug', dbg='1' if seed == 1 else '0'))
write('b4_ug', L)

# B5: Brain of Cthulhu, Rev (and Expert for contrast), surface vs underground, 6 seeds
L = []
for seed in range(1, 7):
    for arena in ('surf', 'ug'):
        for mode in ('rev', 'exp'):
            L.append(line('brain', mode, seed, gear='mod', n='cp', arena=arena, dbg='1' if seed <= 2 else '0'))
write('b5_brain', L)

# far-commander counterparts of the near cells in b1a that s1_main does not already have
L = []
for seed in (1, 2, 3):
    L.append(line('eye', 'exp', seed, gear='mod', n='cp'))
    L.append(line('kslime', 'exp', seed, gear='mod', n='cp')); L.append(line('kslime', 'rev', seed, gear='mod', n='cp'))
    L.append(line('skel', 'exp', seed, gear='mod', n='cp'))
write('b1a_far', L)

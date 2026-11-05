#!/usr/bin/env python3
"""Gear matrix from an OwnerLab 'gear' TSV: per gear piece and mode, what happened beyond the no-gear control of the same weapon + mode.
usage: lab_gearmatrix.py file.tsv[:file.spec] ...   (the :spec form recovers g= from the spec line of the same index for TSVs written before the gear column existed)
Mode = np (npcProj true) | real (npcProj false, owner = soldier) ; pu0 (no per-tick Player.Update subset) | s1 | s2 | full."""
import csv, sys, re, collections


def mode_of(flags):
    f = flags.split(',')
    real = 'nonpc' in f
    pu = next((x[3:] for x in f if x.startswith('pu=')), 'pu0')
    return ('real' if real else 'np') + '_' + (pu if pu.startswith('s') or pu == 'full' else 'pu0')


def gear_of(flags):
    m = [x for x in flags.split(',') if x.startswith('g=')]
    return m[0][2:] if m else ''


def parse_loc(s):
    d = {}
    if s in ('-', ''):
        return d
    for part in s.split(';'):
        m = re.match(r'(.*) S(\d+) C(\d+) T(\d+) O(\d+)$', part)
        if m:
            d[m.group(1)] = tuple(int(m.group(i)) for i in range(2, 6))
    return d


rows = []
for arg in sys.argv[1:]:
    fn, _, spec = arg.partition(':')
    rr = list(csv.DictReader(open(fn), delimiter='\t'))
    if spec:
        lines = [l for l in open(spec).read().split('\n') if l.strip()]
        for r, l in zip(rr, lines):
            m = re.search(r'(?:^|,)(g=[^,|]+)', l.split('|')[4])
            if m and 'g=' not in r['flags']:
                r['flags'] += ',' + m.group(1)
    rows += rr

ctl = {}
for r in rows:
    if not gear_of(r['flags']):
        ctl[(r['item'], mode_of(r['flags']))] = r


def fmt_loc(k, v):
    where = ''.join(c + str(n) for c, n in zip('SCTO', v) if n)
    return f"{k}[{where}]"


print(f"{'gear':44}{'weapon':22}{'mode':10}| flagsOn | new spawns [S=at soldier C=at commander T=at target O=elsewhere] | new target buffs | life gain | hooks proj/melee/hurt (cmd/other) | exceptions")
for r in rows:
    g = gear_of(r['flags'])
    if not g:
        continue
    mode = mode_of(r['flags'])
    c = ctl.get((r['item'], mode))
    cl = parse_loc(c['spawnLoc']) if c else {}
    loc = parse_loc(r['spawnLoc'])
    new = [fmt_loc(k, v) for k, v in loc.items() if k not in cl and not k.startswith('item:GlowingMushroom')]
    cb = set(c['targetBuffs'].split(',')) if c else set()
    nb = [b for b in r['targetBuffs'].split(',') if b not in cb and b != '-']
    cflags = set(c['flagsOn'].split()) if c else set()
    fo = [x for x in r['flagsOn'].split() if x not in cflags and x != '-']
    exc = r['exceptions']
    exc = '' if exc.startswith('- [Get') or exc == '-' else exc[:120]
    print(f"{g[:43]:44}{r['item']:22}{mode:10}| {' '.join(fo)[:60]} | {'; '.join(new)[:150]} | {','.join(nb)} | {r['dummyLifeGain']} | p{r['hooksProj(cmd/other)']} m{r['hooksMelee(cmd/other)']} h{r['hooksHurt(cmd/other)']} | {exc}")

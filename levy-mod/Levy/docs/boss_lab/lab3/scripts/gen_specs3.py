#!/usr/bin/env python3
"""Lab 3 spec generator. Usage: gen_specs3.py <outdir>
Writes one TSV per (table, arm). Spec token reference: CombatHarness.CalRev.cs / CalRevLab.cs / Lab3.cs.
Fear model and formation come from the ENVIRONMENT (LEVY_FEARMODEL=events LEVY_SURV=formation), not from the spec."""
import sys, os
out = sys.argv[1]; os.makedirs(out, exist_ok=True)
CAP = 18000  # 300 s
SEEDS = range(1, 7)
CAL_BOSSES = ['kslime', 'ds', 'eye', 'eow', 'brain', 'hive', 'perf', 'qb', 'skel', 'sg']
REV_BOSSES = {'eye', 'brain', 'eow', 'hive', 'perf', 'sg'}
VAN_BOSSES = ['kslime', 'eye', 'eow', 'brain', 'qb', 'skel']

def line(boss, mode, seed, extra=()):
    t = ['fight', boss, mode, str(seed), str(CAP), 'cmd=near', 'n=cp', 'arena=a3', 'gear=mod', 'grade=lv', 'l3=1']
    if boss == 'qb': t.append('jungle=1')
    t += list(extra)
    return '\t'.join(t)

def write(name, lines):
    with open(os.path.join(out, name + '.tsv'), 'w') as f: f.write('\n'.join(lines) + '\n')

ARMS = {'a': [], 'b': ['food=1'], 'c': ['food=2'], 'd': ['hazard=1'], 'e': ['rogue=2']}
for arm, extra in ARMS.items():
    L = []
    for b in CAL_BOSSES:
        for mode in ['exp'] + (['rev'] if b in REV_BOSSES else []):
            for s in SEEDS: L.append(line(b, mode, s, extra))
    write('cal_' + arm, L)
# vanilla table (arm a), Expert; the vanilla run uses VANILLA=1 (no Calamity loaded)
write('van_a', [line(b, 'exp', s) for b in VAN_BOSSES for s in SEEDS])
# ammo A/B: Brain and EoW in the enclosed arena: plain vs chosen (piercing arrows / Silver) vs not-tier-legal Meteor Shot what-if
AB = []
for b in ['brain', 'eow']:
    for mode in ['exp', 'rev']:
        for am in ['plain', 'best', 'meteor']:
            for s in SEEDS:
                AB.append(line(b, mode, s, ['ammo=' + am]).replace('arena=a3', 'arena=a3e'))
write('ammo_ab', AB)

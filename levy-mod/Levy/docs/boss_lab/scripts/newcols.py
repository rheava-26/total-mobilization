import csv, sys
for f in sys.argv[1:]:
    for r in csv.DictReader(open(f), delimiter='\t'):
        if 'dtdPct' not in r or r['kind'] != 'fight': continue
        print(r['boss'], r['mode'], r['arena'], 'n', r['n'], 's' + r['seed'], r['result'], r['sec'] + 's', 'dtd%', r['dtdPct'], 'dpsHittable', r['dpsHittable'], 'addKills', r['addKills'], f"({r['addKillsPerSec']}/s)", 'phases', r['phases'][:110], 'enr', r['enragedSec'])

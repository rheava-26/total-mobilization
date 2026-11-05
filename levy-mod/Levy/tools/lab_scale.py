#!/usr/bin/env python3
"""Scaling table from an OwnerLab 'cost' TSV: per item and mode, a metric vs N (and the N=10 / N=1 ratio).
usage: lab_scale.py file.tsv [metric]     metric = any TSV column (default dmgTotal)"""
import csv, sys, collections
rows = list(csv.DictReader(open(sys.argv[1]), delimiter='\t'))
metric = sys.argv[2] if len(sys.argv) > 2 else 'dmgTotal'
tab = collections.OrderedDict()
for r in rows:
    mode = 'vslot' if 'vslot' in r['flags'] else 'shared255'
    if 'netlock' in r['flags']:
        mode += '+netlock'
    tab.setdefault((r['item'], mode), {})[int(r['N'])] = r
print(f"{'item':26}{'mode':16}" + ''.join(f"{'N=' + str(n):>12}" for n in (1, 2, 5, 10)) + "   ratio N10/N1 (ideal 10)")
for (it, mode), d in tab.items():
    vals = [float(d[n][metric]) if n in d else float('nan') for n in (1, 2, 5, 10)]
    ratio = vals[3] / vals[0] if vals[0] else float('nan')
    print(f"{it:26}{mode:16}" + ''.join(f"{v:12.0f}" for v in vals) + f"   {ratio:6.1f}")

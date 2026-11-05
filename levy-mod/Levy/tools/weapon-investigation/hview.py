import csv
rows=list(csv.DictReader(open('/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/harness_cal.tsv'),delimiter='\t'))
keys=list(rows[0].keys())
print([k for k in keys if k.startswith('pl_')])
for r in rows:
    print(r['kind'],r['weapon'],r['classifier'],'range',r['rangeTiles'],'fired',r['fired(proj)'],'proj',r['projectiles'],'dmg',r['damageToTarget'],'hitTicks',r['targetHitTicks'],'plstatus',r['pl_status'],'plproj',r['pl_projectiles'], {k:r[k] for k in keys if k.startswith('pl_dam') or k=='pl_targetHitTicks'})

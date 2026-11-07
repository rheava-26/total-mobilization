import csv
rows = list(csv.DictReader(open('/home/user/lab/out/b3_clone.tsv'), delimiter='\t'))
cl = {}
for r in rows:
    c = r['key'].split('|cl')[1][0]
    cl.setdefault(c, []).append(r)
for c, v in cl.items():
    p = v[0]['perLevy'].split(';')[0].split(':')[1]
    print(f"class {c} weapon {p}")
    print("  n  dps  hits/s  dps/levy  hits/s/levy")
    for r in v:
        n = int(r['n']); d = float(r['dmgPerSec']); h = float(r['hitsPerSec'])
        print(f"  {n:2d} {d:6.1f} {h:6.1f} {d/n:7.2f} {h/n:7.2f}")

import sys, re, csv, collections
# usage: sum.py out.tsv -> per weapon per seed dps per levy
rows = list(csv.reader(open(sys.argv[1]), delimiter='\t'))
h = rows[0]; ix = {k: i for i, k in enumerate(h)}
agg = collections.OrderedDict()
for r in rows[1:]:
    if len(r) < len(h) - 3: continue
    m = re.search(r'cw(CalamityMod/\w+)', r[0])
    if not m: continue
    w = m.group(1).split('/')[1]
    boss = r[ix['boss']]
    sec = float(r[ix['sec']]); n = int(r[ix['n']])
    d = float(r[ix['dmgBoss']]) + 0
    agg.setdefault((w, boss), []).append((d / sec / n, int(r[ix['hitsBoss']]), r[ix['result']], float(r[ix['dmgAdd']] or 0) / sec / n))
for (w, b), v in agg.items():
    print(f"{w:24s} {b:6s} " + " ".join(f"{x[0]:8.2f}(h{x[1]})" for x in v) + f"  mean={sum(x[0] for x in v)/len(v):8.2f} addDps={sum(x[3] for x in v)/len(v):6.2f}")

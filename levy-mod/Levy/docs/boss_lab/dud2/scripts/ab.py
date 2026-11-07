import sys, re, csv, collections
# usage: ab.py before.tsv after.tsv [more after.tsv ...]: per weapon/boss: mean dmg/s per levy (seeds) before -> after
def load(f):
    rows = list(csv.reader(open(f), delimiter='\t'))
    h = rows[0]; ix = {k: i for i, k in enumerate(h)}
    agg = collections.OrderedDict()
    for r in rows[1:]:
        if len(r) < len(h) - 3: continue
        m = re.search(r'cw(CalamityMod/\w+)', r[0])
        if not m: continue
        w = m.group(1).split('/')[1]; boss = r[ix['boss']]
        sec = float(r[ix['sec']]); n = int(r[ix['n']])
        d = float(r[ix['dmgBoss']])
        agg.setdefault((w, boss), []).append(d / sec / n)
    return agg
a = load(sys.argv[1]); b = collections.OrderedDict()
for f in sys.argv[2:]:
    for k, v in load(f).items(): b.setdefault(k, []).extend(v)
print(f"{'weapon':24s} {'boss':7s} {'before(n)':>16s} {'after(n)':>16s}")
for k in list(a.keys()) + [k for k in b if k not in a]:
    av = a.get(k, []); bv = b.get(k, [])
    f = lambda v: f"{sum(v)/len(v):9.2f}({len(v)})" if v else "        -"
    print(f"{k[0]:24s} {k[1]:7s} {f(av):>16s} {f(bv):>16s}")

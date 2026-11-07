import sys, re, csv, collections, os
D = '/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/dud2/'
def load(files):
    agg = collections.defaultdict(list)
    for f in files:
        if not os.path.exists(D + f): continue
        rows = list(csv.reader(open(D + f), delimiter='\t')); h = rows[0]; ix = {k: i for i, k in enumerate(h)}
        for r in rows[1:]:
            if len(r) < len(h) - 3: continue
            m = re.search(r'cw(CalamityMod/\w+)', r[0])
            if not m: continue
            w = m.group(1).split('/')[1]
            mp = 'mp' + re.search(r'\|mp(\d+)', r[0]).group(1) if '|mp' in r[0] else 'default'
            seed = r[ix['seed']]
            agg[(w, r[ix['boss']], mp)].append((seed, float(r[ix['dmgBoss']]) / float(r[ix['sec']]) / int(r[ix['n']])))
    return agg
def fmt(v):
    if not v: return '-'
    return f"{sum(x for _, x in v) / len(v):.1f} (n={len(v)}: {'/'.join(f'{x:.0f}' for _, x in v)})"
if __name__ == '__main__':
    before = load(sys.argv[1].split(','))
    after = load(sys.argv[2].split(','))
    keys = sorted(set(before) | set(after))
    print('weapon\tboss\tpool\tbefore\tafter')
    for k in keys:
        print(f"{k[0]}\t{k[1]}\t{k[2]}\t{fmt(before.get(k))}\t{fmt(after.get(k))}")

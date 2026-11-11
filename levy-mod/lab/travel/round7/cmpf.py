import csv, sys
leg = sys.argv[1]
def load(f): return {r['name']: r for r in csv.DictReader(open(f))}
for g in ('b3', 'a3'):
    for rep in (1, 2, 3):
        d = load(f'/home/user/lab/t7/res/{g}_rep{rep}_{leg}.csv')
        print(g, rep, ' '.join(f"{n[-3:]}:{'A' if r['arrived']=='1' else r['endOffset']}" for n, r in d.items()))

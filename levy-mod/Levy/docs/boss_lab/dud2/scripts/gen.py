import sys, os
# usage: gen.py out.tsv seeds(comma) cap [boss] -- weapons from stdin lines "Name Class"
out = sys.argv[1]; seeds = [int(x) for x in sys.argv[2].split(',')]; cap = sys.argv[3]
boss = sys.argv[4] if len(sys.argv) > 4 else 'eye'
ws = [l.split() for l in sys.stdin if l.strip()]
L = []
for s in seeds:
    for w, c in ws:
        extra = '\tbhp=40'
        L.append(f"fight\t{boss}\texp\t{s}\t{cap}\tgear=mod\tn=4\tdr=1{extra}\tcmd=far\tclone={c}\tcw=CalamityMod/{w}" + os.environ.get('EXTRA',''))
open(out, 'w').write('\n'.join(L) + '\n')
print(len(L))

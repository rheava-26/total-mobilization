import sys
sys.path.insert(0, '.')
from potions import *
ents = []
for k, vs in POTIONS.items():
    for l, fn in vs:
        ents.append((k + ' ' + l, fn().im))
sheet(ents, 8, cols=3).save(sys.argv[1])

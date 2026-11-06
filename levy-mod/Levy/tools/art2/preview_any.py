import sys, importlib
sys.path.insert(0, '.')
from lib import *
mod = importlib.import_module(sys.argv[1])
d = getattr(mod, sys.argv[2])
ents = []
for k, vs in d.items():
    for l, fn in vs:
        ents.append((k[:14] + ' ' + l, fn().im))
sheet(ents, 8, cols=int(sys.argv[4]) if len(sys.argv) > 4 else 3).save(sys.argv[3])

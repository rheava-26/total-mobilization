#!/usr/bin/env python3
# hooktab.py TAG [prefix...] : per-run table for hook levies (gear starts with "hook")
import csv, glob, sys, os
tag = sys.argv[1]; pats = sys.argv[2:] or ['']
files = sorted(glob.glob(f'/home/user/lab/t4/res/{tag}_*.csv'))
for f in files:
    name = os.path.basename(f)[len(tag) + 1:-4]
    if not any(name.startswith(p) for p in pats): continue
    rows = list(csv.DictReader(open(f)))
    hk = [r for r in rows if r['gear'].startswith('hook')]
    if not hk: continue
    arr = [r for r in hk if r['arrived'] == '1']
    t = sum(float(r['arriveSec']) for r in arr) / max(1, len(arr))
    falls = sum(int(r['falls']) for r in hk); casts = sum(int(r['hkCasts']) for r in hk); ch = sum(int(r['hkChains']) for r in hk)
    print(f"{name:22s} {len(arr):2d}/{len(hk):2d} t={t:6.1f}s falls={falls:3d} casts={casts:4d} chains={ch:3d} fail=" + ",".join(r['gear'].split(':')[-1][:6] for r in hk if r['arrived'] != '1'))

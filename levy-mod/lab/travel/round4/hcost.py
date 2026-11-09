#!/usr/bin/env python3
# hcost.py TAG PATTERN : planner + hook costs over the matching runs (events.txt of each run)
import sys, glob, gzip, re
tag, pat = sys.argv[1:3]
n = 0; pms = psl = 0.0; mx = 0.0
hp = hpl = 0; hplms = hticks = 0.0; hproj = 0.0; casts = ok = 0; scans = edges = 0
for f in glob.glob(f'/home/user/lab/t4/res/{tag}_{pat}.ev.gz'):
    n += 1
    for line in gzip.open(f, 'rt'):
        m = re.search(r'planner: routes=(\d+) .*?nodes=(\d+) ms=([\d.]+) maxSliceMs=([\d.]+) slices=(\d+)', line)
        if m: pms += float(m.group(3)); mx = max(mx, float(m.group(4))); psl += int(m.group(5))
        m = re.match(r'hookstats: plans=(\d+) found=\d+ planMs=([\d.]+) tickCalls=\d+ tickMs=([\d.]+) projMs=([\d.]+) casts=(\d+) ok=(\d+) .*scans=(\d+) edges=(\d+)', line)
        if m:
            hpl += int(m.group(1)); hplms += float(m.group(2)); hticks += float(m.group(3)); hproj += float(m.group(4)); casts += int(m.group(5)); ok += int(m.group(6)); scans += int(m.group(7)); edges += int(m.group(8))
print(f'{tag} {pat}: runs {n} | route planner {pms:.0f} ms / {psl:.0f} slices = {pms / max(1, psl):.2f} ms/slice (max {mx:.1f}) | hook plans {hpl} = {hplms / max(1, hpl):.2f} ms each ({hplms:.0f} ms), hook tick+proj {hticks + hproj:.0f} ms, casts {casts}, hook-edge scans {scans} edges {edges}')

#!/usr/bin/env python3
# costs.py TAG PATTERN : planner ms / slices / max slice, hop ms over the matching runs
import sys, glob, gzip, re
tag, pat = sys.argv[1:3]
ms = sl = routes = nodes = 0; mx = 0.0; hopms = 0.0; hops = 0; n = 0
for f in glob.glob(f'/home/user/lab/t3/res/{tag}_{pat}.ev.gz'):
    n += 1
    for line in gzip.open(f, 'rt'):
        m = re.search(r'planner: routes=(\d+) .*?nodes=(\d+) ms=([\d.]+) maxSliceMs=([\d.]+) slices=(\d+)', line)
        if m:
            routes += int(m.group(1)); nodes += int(m.group(2)); ms += float(m.group(3)); mx = max(mx, float(m.group(4))); sl += int(m.group(5))
        m = re.match(r'hops: total=(\d+) seenByMod=\d+ ms=([\d.]+)', line)
        if m: hops += int(m.group(1)); hopms += float(m.group(2))
print(f'{tag} {pat}: runs {n} routes {routes} nodes {nodes} planner {ms:.0f} ms over {sl} slices = {ms / max(1, sl):.2f} ms/slice, max slice {mx:.2f} ms; hops {hops} cost {hopms:.2f} ms ({hopms / max(1, hops):.3f} ms/hop)')

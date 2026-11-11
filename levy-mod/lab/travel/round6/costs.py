#!/usr/bin/env python3
# costs.py TAG... : planner cost and arc counters from res/<tag>*.ev.gz (sum over runs; max of the max slice)
import glob, gzip, re, sys
for pre in sys.argv[1:]:
    runs = 0; ms = 0.0; nodes = 0; routes = 0; slices = 0; mx = 0.0; arcs = 0; fuel = 0; shouts = 0; lines = 0; arcsc = 0; arce = 0; slices_over = 0
    allmx = []
    for f in sorted(glob.glob(f'/home/user/lab/t6/res/{pre}*.ev.gz')):
        t = gzip.open(f, 'rt', errors='ignore').read()
        m = re.search(r'planner: routes=(\d+) found=\d+ partial=\d+ failed=\d+ nodes=(\d+) ms=([\d.]+) maxSliceMs=([\d.]+) slices=(\d+)', t)
        if not m: continue
        runs += 1; routes += int(m.group(1)); nodes += int(m.group(2)); ms += float(m.group(3)); mx = max(mx, float(m.group(4))); slices += int(m.group(5)); allmx.append(float(m.group(4)))
        m = re.search(r'arcScans=(\d+) arcEdges=(\d+)', t)
        if m: arcsc += int(m.group(1)); arce += int(m.group(2))
        m = re.search(r'arcs: starts=(\d+) fuelOut=(\d+) shouts=(\d+) flightLines=(\d+)', t)
        if m: arcs += int(m.group(1)); fuel += int(m.group(2)); shouts += int(m.group(3)); lines += int(m.group(4))
    allmx.sort()
    med = allmx[len(allmx) // 2] if allmx else 0
    print(f'{pre}: runs={runs} routes={routes} nodes={nodes} planner_ms={ms:.0f} ms/slice={ms / max(1, slices):.2f} worst_slice_ms={mx:.2f} median_run_worst={med:.2f} arcScans={arcsc} arcEdges={arce} arcStarts={arcs} fuelOut={fuel} shouts={shouts} flightLines={lines}')

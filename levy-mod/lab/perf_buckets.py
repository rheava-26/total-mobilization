#!/usr/bin/env python3
"""perf_buckets.py RUN_DIR CELL [CELL...]  -- median over seeds of every LEVY_PROF bucket (ms per tick) of the named cells of a perf run, side by side (see lab/perf_summary.py)."""
import sys, re, subprocess, statistics, os, collections
run = sys.argv[1]; cells = sys.argv[2:]
out = subprocess.run([sys.executable, os.path.join(os.path.dirname(__file__), "perf_summary.py"), run], capture_output=True, text=True).stdout
vals = collections.defaultdict(lambda: collections.defaultdict(list)); cur = None
for line in out.splitlines():
    m = re.match(r"### (\S+): LEVY_PROF", line)
    if m: cur = m[1]; continue
    if cur and line.startswith("seed"):
        for k, v in re.findall(r"([A-Za-z.]+)=([0-9.]+)", line.split("|")[0] + " " + line.split("|")[1] if "|" in line else line):
            vals[cur][k].append(float(v))
        for k, v in re.findall(r"(pairs/tick|culled/tick|hits/tick|damage ms/tick)=([0-9.]+)", line): vals[cur][k].append(float(v))
        t = re.search(r"tick=([0-9.]+)", line)
        if t: vals[cur]["tick(wall)"].append(float(t[1]))
keys = ["tick(wall)", "npc.levy", "ai", "stats", "g.reset", "g.equips", "g.sets", "g.misc", "move", "dodge", "hits", "combat", "brain", "npc.boss", "npc.add", "npc.other", "proj.army", "damage ms/tick", "pairs/tick", "hits/tick"]
print("| bucket (ms per tick) | " + " | ".join(cells) + " |"); print("|---|" + "---|" * len(cells))
for k in keys:
    row = [f"{statistics.median(vals[c][k]):.2f}" if vals[c].get(k) else "-" for c in cells]
    print(f"| {k} | " + " | ".join(row) + " |")

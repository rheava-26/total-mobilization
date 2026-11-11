#!/usr/bin/env python3
"""perf_summary.py RUN_DIR  -- per-cell update cost of a levylab perf run (specs/perf_destroyer.yaml).

Reads, per fight, the [Crowd] census lines of server.log.gz (ms/tick entities / npcs / projectiles, entity counts, allocation) between the fight's
`HARNESS-CR: start` and `RESULT` lines, plus the LEVY_PROF bucket line of out.tsv.prof.tsv, and prints one markdown table per cell (median over seeds).
The first 5 s of a fight (spawn burst) are skipped. Counts are per tick (census averages)."""
import sys, json, gzip, re, glob, statistics, os, collections

run = sys.argv[1]
cells = {c["id"]: c["name"] for c in json.load(open(os.path.join(run, "cells.json")))["cells"]}
F = r"([0-9.]+)"
rows = collections.defaultdict(list)
for job in sorted(glob.glob(os.path.join(run, "jobs", "*", "try*"))):
    lg = os.path.join(job, "server.log.gz")
    if not os.path.exists(lg): continue
    prof = {}
    pf = os.path.join(job, "out.tsv.prof.tsv")
    if os.path.exists(pf):
        for l in open(pf):
            k, _, v = l.rstrip("\n").partition("\t"); prof[k] = v
    cur = None; samples = []; skip = 0; spikes = []
    def flush(key, samples):
        if not key or not samples: return
        m = re.search(r"\|tc(\d+)", key); cid = "c" + m[1] if m else "?"
        sd = re.search(r"follow\|(\d+)\|", key); seed = int(sd[1]) if sd else 0
        d = {k: statistics.mean(s[k] for s in samples) for k in samples[0]}
        d["max"] = max(s["max"] for s in samples)
        d["prof"] = prof.get(key, "")
        late = [ms for tk, ms in spikes if tk > 600]  # after the first ten seconds (the boss spawn and the army's first reaction are always heavy)
        d["spikes"] = len(late); d["spikemax"] = max(late) if late else 0.0; spikes.clear()
        rows[cells.get(cid, cid)].append((seed, d))
    for line in gzip.open(lg, "rt", errors="replace"):
        m = re.search(r"HARNESS-CR: start (\S+?):? [a-z]+ \d+/", line)
        if m: flush(cur, samples); cur = m[1]; samples = []; skip = 3; continue
        if "HARNESS-CR: RESULT" in line: flush(cur, samples); cur = None; samples = []; continue
        if cur is not None and "[Crowd] spike" in line:
            sp = re.search(r"spike tick=(\d+) ([0-9.]+) ms", line)
            if sp: spikes.append((int(sp[1]), float(sp[2])))
            continue
        if cur is None or "[Crowd] census" not in line: continue
        if skip > 0: skip -= 1; continue
        e = re.search(r"ms/tick entities avg=" + F + r" max=" + F + r" npcs=" + F + r" projectiles=" + F, line)
        if not e: continue
        n = re.search(r"npc (\d+)/\d+ \(levy (\d+) militia \d+ drone (\d+) enemy (\d+)", line)
        p = re.search(r"proj (\d+)/\d+ \(armyShot (\d+) lingering (\d+) minion (\d+) sentry (\d+) drone (\d+) enemy (\d+)", line)
        c = re.search(r"cpu ms/tick entities=" + F + r" npcs=" + F + r" projectiles=" + F, line)
        a = re.search(r"alloc KB/tick=" + F + r" gc0/1/2 \+(\d+)/(\d+)/(\d+) gcPause ms \+" + F, line)
        d = dict(cpu=float(c[1]) if c else 0.0, cpun=float(c[2]) if c else 0.0, cpup=float(c[3]) if c else 0.0, tick=float(e[1]), max=float(e[2]), npcms=float(e[3]), projms=float(e[4]), npc=int(n[1]) + int(n[2]) + int(n[3]) + int(n[4]) if n else 0,
                 levy=int(n[2]) if n else 0, drone=int(n[3]) if n else 0, enemy=int(n[4]) if n else 0, proj=int(p[1]) if p else 0, army=int(p[2]) + int(p[4]) if p else 0,
                 hostile=int(p[7]) if p else 0, alloc=float(a[1]) if a else 0.0, gc0=int(a[2]) if a else 0, pause=float(a[5]) if a else 0.0)
        samples.append(d)

def med(cell, k): return statistics.median(d[k] for _, d in rows[cell])
print("| cell | seeds | CPU ms/tick (npc + proj) | wall ms/tick | worst wall tick | levies | drones | enemy NPCs | projectiles (army/hostile) | alloc KB/tick | GC pause ms per 2 s | ticks over 22 ms after 10 s (max ms) |")
print("|---|---|---|---|---|---|---|---|---|---|---|---|")
for c in rows:
    print(f"| {c} | {len(rows[c])} | {med(c,'cpu'):.2f} ({med(c,'cpun'):.2f} + {med(c,'cpup'):.2f}) | {med(c,'tick'):.2f} | {med(c,'max'):.1f} | {med(c,'levy'):.0f} | {med(c,'drone'):.0f} | {med(c,'enemy'):.0f} | {med(c,'proj'):.0f} ({med(c,'army'):.0f}/{med(c,'hostile'):.0f}) | {med(c,'alloc'):.0f} | {med(c,'pause'):.1f} | {med(c,'spikes'):.0f} ({med(c,'spikemax'):.0f}) |")
print()
for c in rows:
    print(f"### {c}: LEVY_PROF buckets (ms per tick, whole fight, per seed)")
    for sd, d in sorted(rows[c]): print(f"seed {sd} tick={d['tick']:.2f}: {d['prof']}")
    print()

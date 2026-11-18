#!/usr/bin/env python3
"""crowd.py RUN_DIR [RUN_DIR ...]  -- the lagsweep A/B table: CPU ms per tick of a pillar (or boss) fight per arm, from the [Crowd] census lines (LEVY_CROWD=1).

Per fight: the census windows between `HARNESS-CR: start` and `RESULT`, the first 6 s skipped (the opening wave). Per arm (the meta= token of the spec line): the median over seeds of the
fight means. Damage per second = damage the army dealt to the boss and its adds (out.tsv dmgBoss + dmgAdd) over the fight's seconds. Several run dirs (a --rev before and an after) print one table each."""
import sys, json, gzip, re, glob, os, csv, statistics, collections

F = r"([0-9.]+)"

def arm_of(rundir):
    m = {}
    try:
        for c in json.load(open(os.path.join(rundir, "cells.json")))["cells"]:
            mm = re.search(r"meta=(\w+)", c.get("name", "")); m[c["id"]] = mm[1] if mm else c.get("name", c["id"])
    except Exception: pass
    return m

def table(rundir):
    arms = arm_of(rundir); rows = collections.defaultdict(list)
    for job in sorted(glob.glob(os.path.join(rundir, "jobs", "*", "try*"))):
        lg = os.path.join(job, "server.log.gz")
        if not os.path.exists(lg): continue
        out = {}
        if os.path.exists(os.path.join(job, "out.tsv")):
            for r in csv.DictReader(open(os.path.join(job, "out.tsv"), errors="replace"), delimiter="\t"): out[r["key"]] = r
        cur = None; samples = []
        def flush():
            if not cur or not samples: return
            m = re.search(r"\|t(c\d+)", cur); arm = arms.get(m[1], m[1]) if m else "?"
            d = {k: statistics.mean(s[k] for s in samples) for k in samples[0]}
            r = out.get(cur)
            if r:
                secs = float(r["sec"]) or 1; d["dps"] = (float(r["dmgBoss"]) + float(r["dmgAdd"])) / secs; d["deaths"] = int(r["deaths"])
            rows[arm].append(d)
        for line in gzip.open(lg, "rt", errors="replace"):
            m = re.search(r"HARNESS-CR: start (\S+?):? [a-z]+ \d+/", line)
            if m: flush(); cur = m[1]; samples = []; skip = 3; continue
            if "HARNESS-CR: RESULT" in line: flush(); cur = None; samples = []; continue
            if cur is None or "[Crowd] census" not in line: continue
            if skip > 0: skip -= 1; continue
            c = re.search(r"cpu ms/tick entities=" + F + r" npcs=" + F + r" projectiles=" + F, line)
            e = re.search(r"ms/tick entities avg=" + F, line)
            n = re.search(r"npc \d+/\d+ \(levy (\d+) militia \d+ drone \d+ enemy (\d+)", line)
            p = re.search(r"proj (\d+)/\d+ \(armyShot (\d+) lingering (\d+) minion (\d+) sentry (\d+)", line)
            a = re.search(r"alloc KB/tick=" + F, line)
            if not (c and n and p): continue
            samples.append(dict(cpu=float(c[1]), cpun=float(c[2]), cpup=float(c[3]), wall=float(e[1]) if e else 0, levy=int(n[1]), enemy=int(n[2]), proj=int(p[1]),
                                army=int(p[2]) + int(p[3]) + int(p[4]) + int(p[5]), alloc=float(a[1]) if a else 0))
        flush()
    print(f"\n### {rundir}\n")
    print("| arm | fights | CPU ms/tick (NPCs + projectiles) | per fight | levies | enemies | army shots | alloc KB/tick | army damage/s | levy deaths |")
    print("|---|---|---|---|---|---|---|---|---|---|")
    for arm, ds in rows.items():
        med = lambda k: statistics.median(d.get(k, 0) for d in ds)
        print(f"| {arm} | {len(ds)} | {med('cpu'):.2f} ({med('cpun'):.2f} + {med('cpup'):.2f}) | {', '.join(f'{d['cpu']:.1f}' for d in ds)} | {med('levy'):.0f} | {med('enemy'):.0f} | {med('army'):.0f} | {med('alloc'):.0f} | {med('dps'):.0f} | {med('deaths'):.0f} |")

for rd in sys.argv[1:]: table(rd)

#!/usr/bin/env python3
"""ac_table.py RUN_DIR -- the active army size lab: per arm (meta=), from each fight between `HARNESS-CR: start` and RESULT:
  update ms per tick (the server analyzer's own [PerfWin] windows, LEVY_PERFAN_SERVER=1 LEVY_PERFAN_ECHO=1; the first window skipped), the army's share of it (wpr),
  soldier bodies in the field, the [Crowd] CPU census (LEVY_CROWD=1, NPC + projectile phases), and the army over time (HARNESS-AC lines every 5 s: fielded, waiting, dead, damage).
Damage per second = the army's damage to the pillar and its enemies over each 10 s slice (median over seeds)."""
import sys, gzip, glob, os, re, json, statistics, collections

def med(xs): xs = [x for x in xs if x is not None]; return statistics.median(xs) if xs else float("nan")

def fights(rundir):
    for lg in sorted(glob.glob(os.path.join(rundir, "jobs", "*", "try*", "server.log.gz"))):
        cur = None
        for line in gzip.open(lg, "rt", errors="replace"):
            m = re.search(r"HARNESS-CR: start (\S+?):? [a-z]+ \d+/", line)
            if m:
                cur = {"key": m[1], "win": [], "ac": [], "crowd": [], "meta": None, "fielded0": None}
                mm = re.search(r"\|m([A-Za-z0-9_]+)", m[1]); continue
            if cur is None: continue
            m = re.search(r"active army size (\d+): (\d+) of (\d+)", line)
            if m: cur["fielded0"] = int(m[2])
            if "[PerfWin]" in line:
                try: cur["win"].append(json.loads(line[line.index("{"):]))
                except Exception: pass
            m = re.search(r"HARNESS-AC: t=(\d+) fielded=(\d+) waiting=(\d+) dead=(\d+) dmg=(\d+)", line)
            if m: cur["ac"].append(tuple(int(x) for x in m.groups()))
            if "[Crowd] census" in line:
                m = re.search(r"cpu ms/tick entities=([0-9.]+)", line)
                if m: cur["crowd"].append(float(m[1]))
            if "HARNESS-CR: RESULT" in line:
                cur["result"] = line; yield cur; cur = None

def arm_of(key, rundir):
    # the spec line's meta= rides in the cells file; fall back to the key
    return key

def main(rundir):
    arms = collections.defaultdict(list)
    spec_meta = {}
    try:
        for c in json.load(open(os.path.join(rundir, "cells.json")))["cells"]:
            mm = re.search(r"meta=(\w+)", c.get("name", "")); spec_meta[c["id"]] = mm[1] if mm else c["id"]
    except Exception: pass
    for f in fights(rundir):
        m = re.search(r"\|t(c\d+)", f["key"]); arm = spec_meta.get(m[1], m[1]) if m else f["key"]
        w = f["win"][1:] if len(f["win"]) > 1 else f["win"]
        upd = [x.get("upd") for x in w if "upd" in x]
        army = [sum(x["wpr"]) for x in w if "wpr" in x]
        bodies = [x.get("cnt", {}).get("levy") for x in w]
        ac = f["ac"]
        dps = []
        for i in range(2, len(ac), 2):  # 10 s slices
            dt = (ac[i][0] - ac[i - 2][0]) / 60.0
            if dt > 0: dps.append((ac[i][0] // 60, (ac[i][4] - ac[i - 2][4]) / dt))
        arms[arm].append(dict(upd=med(upd), army=med(army), crowd=med(f["crowd"][3:]), fielded=med([a[1] for a in ac]), bodies=med(bodies), dead=ac[-1][3] if ac else None,
                               waiting_end=ac[-1][2] if ac else None, dmg=ac[-1][4] if ac else None, secs=ac[-1][0] / 60.0 if ac else None, dps=dps, res=f["result"].strip()[-80:]))
    print("| arm | fights | update ms/tick | army ms/tick | CPU census ms | fielded (median) | dead at end | waiting at end | army dmg/s | dmg/s by 10 s |")
    print("|---|---|---|---|---|---|---|---|---|---|")
    for arm in sorted(arms):
        rs = arms[arm]
        slices = collections.defaultdict(list)
        for r in rs:
            for t, d in r["dps"]: slices[t].append(d)
        sl = ", ".join(f"{t}s {med(v) / 1000:.1f}k" for t, v in sorted(slices.items()))
        dmgps = med([r["dmg"] / r["secs"] for r in rs if r["dmg"] is not None and r["secs"]])
        print(f"| {arm} | {len(rs)} | {med([r['upd'] for r in rs]):.2f} ({', '.join(f'{r['upd']:.1f}' for r in rs)}) | {med([r['army'] for r in rs]):.2f} | {med([r['crowd'] for r in rs]):.2f} | {med([r['fielded'] for r in rs]):.0f} | {med([r['dead'] for r in rs]):.0f} | {med([r['waiting_end'] for r in rs]):.0f} | {dmgps:,.0f} | {sl} |")

if __name__ == "__main__":
    main(sys.argv[1])

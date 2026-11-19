#!/usr/bin/env python3
"""l4_weapons.py RUN_DIR -- the lag4 weapon experiment table: per arm (meta=), the always-on analyzer's own per-weapon attribution ([PerfWin] lines, LEVY_PERFAN_SERVER=1 LEVY_PERFAN_ECHO=1)
and the [Crowd] census (LEVY_CROWD=1), per fight between `HARNESS-CR: start` and RESULT, the first window / first 3 census lines skipped.
Columns: levies alive, ms per tick the analyzer charged to the army's weapons (bodies + shots), of which bodies and shots, ms per tick per levy, army shots alive, the costliest projectile type, CPU ms/tick of the NPC and projectile phases."""
import sys, json, gzip, re, glob, os, statistics, collections

def arms(rundir):
    m = {}
    for c in json.load(open(os.path.join(rundir, "cells.json")))["cells"]:
        mm = re.search(r"meta=(\w+)", c.get("name", "")); m[c["id"]] = mm[1] if mm else c["id"]
    return m

def main(rundir):
    am = arms(rundir); rows = collections.defaultdict(list)
    for lg in sorted(glob.glob(os.path.join(rundir, "jobs", "*", "try*", "server.log.gz"))):
        cur = None; win = []; cen = []; skipw = skipc = 0
        def flush():
            if not cur or not win: return
            m = re.search(r"\|t(c\d+)", cur); arm = am.get(m[1], m[1]) if m else "?"
            u = sum(w["nu"] for w in win) or 1
            wpa = sum(w.get("wpa", 0) * w["nu"] for w in win) / u
            body = sum(sum(r[2] for r in w.get("wp", [])) * w["nu"] for w in win) / u
            shot = sum(sum(r[3] for r in w.get("wp", [])) * w["nu"] for w in win) / u
            lev = sum(w.get("cnt", {}).get("levy", 0) * w["nu"] for w in win) / u
            shots = sum(sum(r[4] for r in w.get("wp", [])) * w["nu"] for w in win) / u
            pt = collections.Counter()
            for w in win:
                for r in w.get("wp", []):
                    if r[5]: pt[r[5]] += r[6] * w["nu"]
            top = pt.most_common(1)[0] if pt else ("", 0)
            cpu = statistics.mean(c for c in cen) if cen else 0
            rows[arm].append(dict(wpa=wpa, body=body, shot=shot, lev=lev, shots=shots, per=wpa / max(1, lev), top=top[0].split("/")[-1], topms=top[1] / u, cpu=cpu))
        for line in gzip.open(lg, "rt", errors="replace"):
            m = re.search(r"HARNESS-CR: start (\S+?):? [a-z]+ \d+/", line)
            if m: flush(); cur = m[1]; win = []; cen = []; skipw = 1; skipc = 3; continue
            if "HARNESS-CR: RESULT" in line: flush(); cur = None; win = []; cen = []; continue
            if cur is None: continue
            if "[PerfWin] " in line:
                if skipw > 0: skipw -= 1; continue
                try: w = json.loads(line.split("[PerfWin] ", 1)[1])
                except Exception: continue
                if w.get("cnt", {}).get("levy", 0) > 0: win.append(w)
            elif "[Crowd] census" in line:
                if skipc > 0: skipc -= 1; continue
                c = re.search(r"cpu ms/tick entities=([0-9.]+)", line)
                if c: cen.append(float(c[1]))
        flush()
    print("| army (every levy holds) | fights | levies | army ms/tick (bodies + shots) | ms/tick per levy | shots alive | costliest projectile (ms/tick) | CPU ms/tick NPC+proj phases |")
    print("|---|---|---|---|---|---|---|---|")
    order = sorted(rows, key=lambda a: -statistics.mean(d["per"] for d in rows[a]))
    for a in order:
        ds = rows[a]; M = lambda k: statistics.mean(d[k] for d in ds)
        print(f"| {a} | {len(ds)} | {M('lev'):.0f} | {M('wpa'):.2f} ({M('body'):.2f} + {M('shot'):.2f}) | {M('per'):.3f} ({', '.join(f'{d['per']:.3f}' for d in ds)}) | {M('shots'):.0f} | {ds[0]['top']} ({M('topms'):.2f}) | {M('cpu'):.2f} |")

main(sys.argv[1])

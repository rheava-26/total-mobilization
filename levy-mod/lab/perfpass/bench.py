#!/usr/bin/env python3
"""bench.py RUN_DIR [RUN_DIR ...]  -- the perf-pass benchmark table (lab/perfpass/README.md).

Per fight (between `HARNESS-CR: start` and `RESULT` in server.log.gz), the first 6 s skipped:
  [Crowd] census (LEVY_CROWD=1): thread-CPU ms per tick of the whole entity update and of the NPC and projectile phases, alloc KB/tick, levies alive, army shots alive.
  [PerfWin] (LEVY_PERFAN_SERVER=1 LEVY_PERFAN_ECHO=1): the always-on analyzer's windows: Levy's share (levy bodies + army shots + Levy's global hooks, per update),
            Levy's buckets, per-weapon rows, the top cost rows.
  out.tsv: behaviour (damage to the boss, hits on the boss, levy deaths, share of levy samples with no target).
Prints, per cell (the fight's meta= label), the median over seeds, then (with --top) the top-cost tables."""
import sys, gzip, glob, os, re, json, statistics as S, collections, csv
args = [a for a in sys.argv[1:] if not a.startswith("--")]; TOP = "--top" in sys.argv
def med(v): return S.median(v) if v else float("nan")
def fights(run):
    out = []
    for job in sorted(glob.glob(os.path.join(run, "jobs", "*", "try*"))):
        lg = os.path.join(job, "server.log.gz")
        if not os.path.exists(lg): continue
        beh = {}
        o = os.path.join(job, "out.tsv")
        if os.path.exists(o):
            for r in csv.DictReader(open(o, errors="replace"), delimiter="\t"): beh[r["key"]] = r
        cur = None
        for line in gzip.open(lg, "rt", errors="replace"):
            m = re.search(r"HARNESS-CR: start (\S+):", line)
            if m: cur = dict(key=m[1], cen=[], win=[], n=0); out.append(cur); continue
            if cur is None: continue
            if "HARNESS-CR: RESULT" in line: cur["beh"] = beh.get(cur["key"], {}); cur = None; continue
            if "[Crowd] census" in line:
                cur["n"] += 1
                if cur["n"] <= 3: continue
                m = re.search(r"cpu ms/tick entities=([0-9.]+) npcs=([0-9.]+) projectiles=([0-9.]+) \| alloc KB/tick=([0-9.]+)", line)
                lv = re.search(r"levy (\d+)", line); sh = re.search(r"armyShot (\d+)", line); mn = re.search(r"\bminion (\d+)", line); en = re.search(r"enemy (\d+)", line); ep = re.search(r"proj (\d+)/1000", line)
                gc = re.search(r"gc0/1/2 \+(\d+)/(\d+)/(\d+) gcPause ms \+([0-9.]+)", line)
                if m: cur["cen"].append(dict(cpu=float(m[1]), npc=float(m[2]), proj=float(m[3]), alloc=float(m[4]), levy=int(lv[1]), shots=int(sh[1]), minions=int(mn[1]), enemy=int(en[1]), projs=int(ep[1]),
                                             gc0=int(gc[1]) if gc else 0, gcms=float(gc[4]) / 120.0 if gc else 0.0))
            elif "[PerfWin]" in line and cur["n"] > 3:
                try: cur["win"].append(json.loads(line.split("[PerfWin] ", 1)[1]))
                except Exception: pass
    return [f for f in out if f["cen"]]
def label(key):
    m = re.search(r"\|(meta=)?([a-z]+_[a-z]+)", key)
    p = key.split("|"); return p[1] + ("/dr" if "|dr" in key else "")
def summarize(f):
    c = f["cen"]; d = {k: S.mean(x[k] for x in c) for k in c[0]}
    W = f["win"]; lv = collections.Counter(); top = collections.Counter(); wp = collections.defaultdict(lambda: [0.0] * 4); nw = 0; upd = []
    for w in W:
        nu = max(1, w["nu"]); nw += 1; upd.append(w["upd"])
        for k, v in w.get("lv", []): lv[k] += v
        for cat, name, ms, n in w["top"]: top[(cat, name)] += ms / nu
        for r in w.get("wp", []): a = wp[r[0]]; a[0] += r[1]; a[1] += r[2]; a[2] += r[3]; a[3] += r[4]
    nw = max(1, nw)
    d["levyms"] = (top[("npc", "Levy")] + top[("army", "all")] + top[("glob", "Levy")]) / nw
    d["updwall"] = S.mean(upd) if upd else float("nan")
    d["lv"] = {k: v / nw for k, v in lv.items()}; d["top"] = {k: v / nw for k, v in top.items()}; d["wp"] = {k: [x / nw for x in v] for k, v in wp.items()}
    b = f.get("beh", {})
    for k in ("dmgBoss", "hitsBoss", "deaths", "tgtNonePct", "firstHitSec", "sec"):
        try: d[k] = float(b.get(k, "nan"))
        except ValueError: d[k] = float("nan")
    return d
for run in args:
    cells = collections.defaultdict(list)
    for f in fights(run): cells[label(f["key"])].append(summarize(f))
    print(f"\n### {run}   ({os.popen('cat ' + os.path.join(run, 'loadavg.txt') + ' 2>/dev/null').read().strip() or 'load: not recorded'})")
    print("| scene | fights | CPU ms/tick (entities) | NPC phase | proj phase | Levy ms/update (analyzer) | Levy share | alloc KB/tick | gen0 GCs per 10 s | GC pause ms/tick | levies | army shots | minions | enemy NPCs | dmg to boss | hits on boss | deaths | no-target % |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    for k, L in cells.items():
        g = lambda key: med([x[key] for x in L])
        share = med([x["levyms"] / x["updwall"] for x in L if x["updwall"] == x["updwall"] and x["updwall"] > 0])
        print(f"| {k} | {len(L)} | {g('cpu'):.2f} | {g('npc'):.2f} | {g('proj'):.2f} | {g('levyms'):.2f} | {100*share:.0f}% | {g('alloc'):.0f} | {5*g('gc0'):.1f} | {g('gcms'):.3f} | {g('levy'):.0f} | {g('shots'):.0f} | {g('minions'):.0f} | {g('enemy'):.0f} | {g('dmgBoss')/1e3:.0f}k | {g('hitsBoss'):.0f} | {g('deaths'):.0f} | {g('tgtNonePct'):.0f} |")
    if TOP:
        for k, L in cells.items():
            print(f"\n#### {k}: top cost rows (ms per update, analyzer windows; median over fights)")
            rows = collections.defaultdict(list)
            for x in L:
                for kk, v in x["top"].items(): rows[kk].append(v)
            for kk, v in sorted(rows.items(), key=lambda kv: -med(kv[1]))[:14]: print(f"  {kk[0]:6s} {kk[1][-42:]:42s} {med(v):.3f}")
            bl = collections.defaultdict(list)
            for x in L:
                for kk, v in x["lv"].items(): bl[kk].append(v)
            print("  Levy buckets (1 update in 66, ms/update): " + ", ".join(f"{kk} {med(v):.2f}" for kk, v in sorted(bl.items(), key=lambda kv: -med(kv[1]))[:12]))
            ww = collections.defaultdict(list)
            for x in L:
                for kk, v in x["wp"].items(): ww[kk].append(v)
            print("  weapons (levies, body ms, shot ms, shots): " + "; ".join(f"{kk} {med([a[0] for a in v]):.1f}/{med([a[1] for a in v]):.2f}/{med([a[2] for a in v]):.2f}/{med([a[3] for a in v]):.0f}" for kk, v in sorted(ww.items(), key=lambda kv: -med([a[1] + a[2] for a in kv[1]]))[:10]))

#!/usr/bin/env python3
"""Collects battery recordings (hm_battery.py runs): hm_bcollect.py RUN_DIR... [--dest /home/user/lab/hmreplays/battery]
-> <dest>/<boss>/<cell>/<boss>_<cell>_s<seed>_<result>.levyrec (gzip) and <dest>/fights.csv: every results.csv column of the fight plus boss, cell, file,
and from the recording: tgtLevyPct / tgtPlayerPct (share of boss samples whose target field read levy / player; NOT proof the boss hunts levies: the field also
reads levy for the targeting lock, an NPC target >= 300 and the spite target)."""
import argparse, csv, gzip, json, re, shutil, sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "Levy" / "tools" / "fightviewer"))
import make_site
ap = argparse.ArgumentParser(); ap.add_argument("runs", nargs="+"); ap.add_argument("--dest", default="/home/user/lab/hmreplays/battery"); a = ap.parse_args()
dest = Path(a.dest); dest.mkdir(parents=True, exist_ok=True); fc = dest / "fights.csv"
rows_out = list(csv.DictReader(open(fc))) if fc.exists() else []
seen = {(r["run"], r["boss"], r["cell"], r["seed"]) for r in rows_out}
for run in map(Path, a.runs):
    cells = {c["id"]: c for c in json.loads((run / "cells.json").read_text())["cells"]}
    res = {(r["cell"], int(r["seed"])): r for r in csv.DictReader(open(run / "results.csv"))}
    best = {}
    for f in sorted((run / "rec").glob("*/*.jsonl")):
        try: hdr = json.loads(open(f).readline())
        except Exception: continue
        m = re.search(r"\|t(c\d+)(?:\||$)", hdr.get("meta", {}).get("cellkey", ""))
        if not m: continue
        k = (m[1], int(hdr["meta"].get("seed", 0)))
        if k not in best or f.stat().st_size > best[k].stat().st_size: best[k] = f
    for (cid, seed), f in sorted(best.items()):
        mm = re.search(r"meta=hmb_(\w+?)_(\w+)_(rev|mdeath)\b", cells[cid]["name"])
        if not mm: continue
        boss, cell, pair = mm.groups(); r = res.get((cid, seed), {})
        if (str(run), boss, cell, str(seed)) in seen: continue
        result = (r.get("result") or "nores").lower()
        d = dest / boss / cell; d.mkdir(parents=True, exist_ok=True); t = d / f"{boss}_{cell}_s{seed}_{result}.levyrec"
        s = make_site.summarize(make_site.load(str(f)))
        with open(f, "rb") as src, gzip.open(t, "wb", compresslevel=9) as z: shutil.copyfileobj(src, z)
        row = dict(r); row.update(boss=boss, cell=cell, pair=pair, seed=seed, file=str(t.relative_to(dest)), run=str(run), tgtLevyPct=s.get("pl", ""), tgtPlayerPct=s.get("pp", ""))
        rows_out.append(row)
keys = []
for r in rows_out:
    for k in r:
        if k not in keys: keys.append(k)
with open(fc, "w", newline="") as o:
    w = csv.DictWriter(o, fieldnames=keys, restval=""); w.writeheader(); w.writerows(rows_out)
print(len(rows_out), "fights in", fc)

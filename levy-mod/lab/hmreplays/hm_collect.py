#!/usr/bin/env python3
"""Collects the headless recordings of levylab run dirs into /home/user/lab/hmreplays/recordings/<boss-slug>/ as gzip .levyrec files,
and appends one row per fight to <dest>/fights.csv (boss, pair, kit, seed, levies, result, kill seconds, levy deaths, ...).
  hm_collect.py RUN_DIR [RUN_DIR ...] [--dest /home/user/lab/hmreplays/recordings]
A recording is matched to its fight through the cellkey in its header (the levylab cell id tcN + the seed), the cell's meta=<slug>_<kit>_<pair>_n<N>_T<tier>
token, and the run's results.csv row. File name: <slug>_<pair>_<kit>_s<seed>_<result>.levyrec (a re-run of the same fight gets -2, -3 ...)."""
import argparse, csv, gzip, json, re, shutil, sys
from pathlib import Path

ap = argparse.ArgumentParser()
ap.add_argument("runs", nargs="+"); ap.add_argument("--dest", default="/home/user/lab/hmreplays/recordings")
a = ap.parse_args()
dest = Path(a.dest); dest.mkdir(parents=True, exist_ok=True)
FIELDS = ["slug", "pair", "kit", "seed", "levies", "result", "sec", "deaths", "firstDeathSec", "bossMax", "bossLeft", "file", "run"]
fc = dest / "fights.csv"
have = set()
if fc.exists():
    for r in csv.DictReader(open(fc)): have.add((r["run"], r["file"]))
need_hdr = not fc.exists() or fc.stat().st_size == 0
out = csv.DictWriter(open(fc, "a", newline=""), fieldnames=FIELDS)
if need_hdr: out.writeheader()
n_new = 0
for run in map(Path, a.runs):
    cells = {c["id"]: c for c in json.loads((run / "cells.json").read_text())["cells"]}
    res = {(r["cell"], int(r["seed"])): r for r in csv.DictReader(open(run / "results.csv"))} if (run / "results.csv").exists() else {}
    best = {}
    for f in sorted((run / "rec").glob("*/*.jsonl")):
        try: hdr = json.loads(open(f).readline())
        except Exception: continue
        key = hdr.get("meta", {}).get("cellkey", ""); m = re.search(r"\|t(c\d+)(?:\||$)", key)
        if not m: print("no cell in", f, file=sys.stderr); continue
        seed = int(hdr["meta"].get("seed", 0)); k = (m[1], seed)
        if k not in best or f.stat().st_size > best[k].stat().st_size: best[k] = f
    for (cid, seed), f in sorted(best.items()):
        nm = cells[cid]["name"]; mm = re.search(r"meta=(\w+?)_(on|mid)_(rev|mdeath)_n(\d+)_T([\d.]+)", nm)
        if not mm: print("no meta in cell", nm, file=sys.stderr); continue
        slug, kit, pair, n, T = mm.groups()
        r = res.get((cid, seed), {}); result = (r.get("result") or "NORES").lower()
        d = dest / slug; d.mkdir(exist_ok=True)
        base = f"{slug}_{pair}_{kit}_s{seed}_{result}"; target = d / (base + ".levyrec"); i = 2
        while target.exists() and (str(run), target.name) not in have:
            if (str(run), target.name) in have: break
            target = d / f"{base}-{i}.levyrec"; i += 1
        if (str(run), target.name) in have: continue
        with open(f, "rb") as src, gzip.open(target, "wb", compresslevel=9) as z: shutil.copyfileobj(src, z)
        out.writerow(dict(slug=slug, pair=pair, kit=kit, seed=seed, levies=n, result=result, sec=r.get("sec", ""), deaths=r.get("deaths", ""), firstDeathSec=r.get("firstDeathSec", ""),
                          bossMax=r.get("bossMax", ""), bossLeft=r.get("bossLeft", ""), file=str(target.relative_to(dest)), run=str(run)))
        n_new += 1
        print("collected", target.relative_to(dest), result, r.get("sec", ""), "s", f.stat().st_size // 1024, "KB ->", target.stat().st_size // 1024, "KB")
print(n_new, "new recordings")

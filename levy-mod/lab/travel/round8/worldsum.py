#!/usr/bin/env python3
# worldsum.py RUNDIR_BEFORE RUNDIR_AFTER [RUNDIR_AFTER2 ...] : markdown "before -> after" per goal and gear tier for travelab run directories (TRAVELAB_HOME/runs/<spec>_<tag>); several after dirs are pooled
import glob, importlib.machinery, importlib.util, os, sys
from collections import defaultdict
from pathlib import Path
loader = importlib.machinery.SourceFileLoader("travelab", str(Path(__file__).resolve().parent.parent / "travelab"))
spec = importlib.util.spec_from_loader("travelab", loader); ta = importlib.util.module_from_spec(spec); loader.exec_module(ta)

def rows_of(dirs):
    out = defaultdict(list)
    for d in dirs:
        for c in sorted(Path(d).iterdir()):
            if c.is_dir() and "__" in c.name:
                arm, g = c.name.split("__", 1)
                out[g] += ta.load_cell(dict(dir=c))
    return out

def cell(rows, tier=None):
    s = ta.stats([r for r in rows if tier is None or r["tier"] == tier])
    return None if not s else s

before = rows_of([sys.argv[1]]); after = rows_of(sys.argv[2:])
tiers = ["bare", "boots", "wings", "hook", "prehm", "hm", "endgame"]
print("| goal | tier | arrive before -> after | trip s before -> after | stuck eps (levies) before -> after |\n|---|---|---|---|---|")
for g in after:
    for t in tiers + [None]:
        b, a = cell(before.get(g, []), t), cell(after[g], t)
        if not a: continue
        f = lambda s: "-" if not s else f"{s['ok']}/{s['n']}"
        tm = lambda s: "-" if not s or s["t"] != s["t"] else f"{s['t']:.0f}"
        se = lambda s: "-" if not s else f"{s['se']} ({s['sl']})"
        print(f"| {g} | {t or 'ALL'} | {f(b)} -> {f(a)} | {tm(b)} -> {tm(a)} | {se(b)} -> {se(a)} |")

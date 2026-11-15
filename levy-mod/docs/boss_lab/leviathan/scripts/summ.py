#!/usr/bin/env python3
"""summ.py LABEL=RUN_DIR ... : one summary row per (difficulty, arm): wins, median kill, mean deaths, deaths to Leviathan dashes, to Anahita, and the per-dash lane stats."""
import csv, glob, sys, statistics as st
from collections import defaultdict
def rd(p):
    try: return list(csv.DictReader(open(p, errors="replace"), delimiter="\t"))
    except FileNotFoundError: return []
def f(x):
    try: return float(x)
    except: return 0.0
for arg in sys.argv[1:]:
    label, run = arg.split("=")
    fights = []; hits = defaultdict(list); dashes = []; lv = []
    for d in sorted(glob.glob(run + "/jobs/*/try*")):
        fights += [r for r in rd(d + "/out.tsv") if r.get("result") not in (None, "", "result")]
        for r in rd(d + "/out.tsv.levihit.tsv"): hits[r["key"]].append(r)
        dashes += rd(d + "/out.tsv.levi.tsv"); lv += rd(d + "/out.tsv.levilv.tsv")
    for mode in ("rev", "mdeath"):
        fs = [r for r in fights if r["mode"] == mode]
        if not fs: continue
        wins = [r for r in fs if r["result"] == "WIN"]
        dd = an = 0
        for r in fs:
            for h in hits[r["key"]]:
                if h["died"] != "1": continue
                if "Leviathan" in h["attacker"] and h["charge"] != "-1": dd += 1
                elif "Anahita" in h["attacker"]: an += 1
        ds = [x for x in dashes if ("|" + mode + "|") in x["key"]]
        il = [x for x in lv if ("|" + mode + "|") in x["key"] and x["inLane"] == "1"]
        print(f"{label} {mode}: wins {len(wins)}/{len(fs)}, median kill {st.median(f(r['sec']) for r in wins) if wins else '-'}, mean deaths {sum(f(r['deaths']) for r in fs)/len(fs):.1f}, dash deaths {dd}, anahita deaths {an}, dashes {len(ds)}, "
              f"in-lane obs {len(il)} hit {100*sum(x['hit']=='1' for x in il)/max(1,len(il)):.0f}% died {100*sum(x['died']=='1' for x in il)/max(1,len(il)):.0f}%")

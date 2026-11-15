#!/usr/bin/env python3
"""summ.py LABEL=RUN_DIR ... [--md]: per-fight rows from levylab run dirs (out.tsv, out.tsv.bat.tsv for the kit, out.tsv.hit.tsv for contact damage).
Columns: boss, difficulty, kit, seed, result, kill s, levy deaths, damage the boss's own bodies did to levies by contact (all of it, and the part a player standing in the same spot would NOT have taken: the
`notplayer:` keys the harness writes by running the player's hit rules on every landed contact hit), and how many contact hits each was.
Then one pooled row per (boss, difficulty, label): wins, median kill, mean deaths, contact damage and the share a player would not have taken."""
import csv, glob, re, sys, statistics as st
from collections import defaultdict
md = "--md" in sys.argv
def rd(p):
    try: return list(csv.DictReader(open(p, errors="replace"), delimiter="\t"))
    except FileNotFoundError: return []
def f(x):
    try: return float(x)
    except Exception: return 0.0
MODE = {"rev": "Expert+Rev", "mdeath": "Master+Death"}
rows = []
for arg in [a for a in sys.argv[1:] if "=" in a]:
    label, run = arg.split("=", 1)
    for d in sorted(glob.glob(run + "/jobs/*/try*")):
        meta = {r["key"]: r.get("meta", "") for r in rd(d + "/out.tsv.bat.tsv")}
        hit = {}
        try:
            for line in open(d + "/out.tsv.hit.tsv", errors="replace"):
                k, _, rest = line.rstrip("\n").partition("\t"); hit[k] = rest
        except FileNotFoundError: pass
        for r in rd(d + "/out.tsv"):
            if r.get("result") in (None, "", "result"): continue
            kit = (re.match(r"[a-z]+_([a-z]+)_", meta.get(r["key"], "")) or [None, "-"])[1]
            c_d = c_n = n_d = n_n = 0
            for m in re.finditer(r"(contact|notplayer):(.+?)=(\d+)/(\d+)(?= (?:contact|notplayer|proj|veto|extra):|$)", hit.get(r["key"], "")):
                if m[1] == "contact": c_d += int(m[3]); c_n += int(m[4])
                else: n_d += int(m[3]); n_n += int(m[4])
            rows.append(dict(label=label, boss=r["boss"], mode=r["mode"], kit=kit, seed=int(r["seed"]), result=r["result"], sec=f(r["sec"]), deaths=int(f(r["deaths"])), cd=c_d, cn=c_n, nd=n_d, nn=n_n))
rows.sort(key=lambda r: (r["boss"], r["mode"], r["label"], r["kit"], r["seed"]))
H = ["boss", "difficulty", "arm", "kit", "seed", "result", "kill s", "levy deaths /30", "contact dmg", "of it a player would not take", "contact hits", "of them a player would not take"]
def pr(cells):
    print(("| " + " | ".join(str(c) for c in cells) + " |") if md else "\t".join(str(c) for c in cells))
pr(H)
if md: print("|" + "---|" * len(H))
for r in rows: pr([r["boss"], MODE.get(r["mode"], r["mode"]), r["label"], r["kit"], r["seed"], r["result"], f"{r['sec']:.1f}" if r["result"] == "WIN" else "-", r["deaths"], r["cd"], r["nd"], r["cn"], r["nn"]])
print()
P = ["boss", "difficulty", "arm", "wins", "median kill s (wins)", "levy deaths mean", "contact dmg", "not-a-player dmg", "share %"]
pr(P)
if md: print("|" + "---|" * len(P))
g = defaultdict(list)
for r in rows: g[(r["boss"], r["mode"], r["label"])].append(r)
for (b, m, l), rs in sorted(g.items()):
    w = [x for x in rs if x["result"] == "WIN"]; cd = sum(x["cd"] for x in rs); nd = sum(x["nd"] for x in rs)
    pr([b, MODE.get(m, m), l, f"{len(w)}/{len(rs)}", f"{st.median(x['sec'] for x in w):.0f}" if w else "-", f"{sum(x['deaths'] for x in rs) / len(rs):.1f}", cd, nd, f"{100 * nd / cd:.0f}" if cd else "-"])

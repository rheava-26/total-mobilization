#!/usr/bin/env python3
"""analyze.py RUN_DIR [--md] : per fight and per Leviathan dash rows from a levylab run (out.tsv + the .levi / .levilv / .levihit probe files).
Fight row: result, kill time, deaths, deaths to a Leviathan dash / other Leviathan contact / Anahita / bubbles+aberrations / unattributed, dashes, deaths per dash.
Dash row: windup ticks (line-up length before the launch), how many levies stood in the lane at launch, deaths and hits, whether the field listed the boss, and what the in-lane levies did."""
import csv, glob, re, sys, statistics as st
from collections import defaultdict
run = sys.argv[1]
def rd(p):
    try: return list(csv.DictReader(open(p, errors="replace"), delimiter="\t"))
    except FileNotFoundError: return []
fights = []; dashes = defaultdict(list); lv = defaultdict(list); hits = defaultdict(list); bat = {}
for d in sorted(glob.glob(run + "/jobs/*/try*")):
    for r in rd(d + "/out.tsv"):
        if r.get("result") in (None, "", "result"): continue
        fights.append(r)
    for r in rd(d + "/out.tsv.bat.tsv"): bat[r["key"]] = r.get("meta", "")
    for r in rd(d + "/out.tsv.levi.tsv"): dashes[r["key"]].append(r)
    for r in rd(d + "/out.tsv.levilv.tsv"): lv[r["key"]].append(r)
    for r in rd(d + "/out.tsv.levihit.tsv"): hits[r["key"]].append(r)
def f(x, d=0.0):
    try: return float(x)
    except: return d
out = []
for r in fights:
    k = r["key"]; meta = bat.get(k, ""); h = hits[k]
    m = re.match(r"(\w+?)_(mid|on)_(\w+?)_", meta)
    cat = defaultdict(int)
    for x in h:
        if x["died"] != "1": continue
        a = x["attacker"]
        if a.startswith("Leviathan") or "Leviathan" in a: cat["levi_dash" if x["charge"] != "-1" else "levi_other"] += 1
        elif "Anahita" in a: cat["anahita"] += 1
        elif "Bubble" in a or "Aberration" in a: cat["adds"] += 1
        else: cat["env/unattributed"] += 1
    ds = dashes[k]
    out.append(dict(boss=r["boss"], kit=m.group(2) if m else "", pair=r["mode"], seed=r["seed"], result=r["result"], sec=r["sec"], deaths=int(f(r["deaths"])), firstDeath=r.get("firstDeathSec", ""), dashes=len(ds),
                    dd=cat["levi_dash"], lo=cat["levi_other"], an=cat["anahita"], ad=cat["adds"], en=cat["env/unattributed"], key=k))
def cell(rows):
    n = len(rows); w = sum(1 for r in rows if r["result"] == "WIN")
    ks = [f(r["sec"]) for r in rows if r["result"] == "WIN"]
    return f"{w}/{n} wins, kill {st.median(ks):.0f}s" if ks else f"{w}/{n} wins", sum(r["deaths"] for r in rows) / max(1, n)
if "--md" in sys.argv:
    print("| boss | difficulty | kit | seed | result | kill s | levy deaths /30 | to a dash | other Leviathan contact | Anahita | bubbles+aberrations | unattributed | dashes | deaths per dash |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    P = dict(rev="Expert+Rev", mdeath="Master+Death", exp="Expert", death="Death")
    for r in sorted(out, key=lambda r: (r["boss"], r["pair"] != "rev", r["kit"] != "mid", int(r["seed"]))):
        print(f"| {r['boss']} | {P.get(r['pair'], r['pair'])} | {r['kit']} | {r['seed']} | {r['result']} | {r['sec'] if r['result']=='WIN' else '-'} | {r['deaths']} | {r['dd']} | {r['lo']} | {r['an']} | {r['ad']} | {r['en']} | {r['dashes']} | {(r['dd']/r['dashes']) if r['dashes'] else 0:.1f} |")
else:
    for r in sorted(out, key=lambda r: (r["pair"], r["kit"], int(r["seed"]))):
        print({k: v for k, v in r.items() if k != "key"})
    print("--- dashes (all fights): dashNo, windTicks, inLane, deaths, hits, fieldSeenDt")
    for k, ds in dashes.items():
        print(k[:40], [(d.get("dashNo", "?"), d["windTicks"], d["inLane"], d["deaths"], d["hits"], d["fieldSeenDt"]) for d in ds])

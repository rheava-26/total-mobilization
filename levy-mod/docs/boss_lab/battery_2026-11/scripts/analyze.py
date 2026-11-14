#!/usr/bin/env python3
"""analyze.py RUN_DIR [RUN_DIR ...] --csv results.csv --md tables.md
Joins every fight's out.tsv row (result, time, deaths, damage ...) with its bat.tsv row (hit-immunity counters, meta) and writes results.csv plus markdown tables."""
import argparse, csv, glob, re, statistics as st, sys
from pathlib import Path

ap = argparse.ArgumentParser(); ap.add_argument("runs", nargs="+"); ap.add_argument("--csv", default="results.csv"); ap.add_argument("--md", default="tables.md")
a = ap.parse_args()
ORDER = ["wof", "qslime", "cryo", "twins", "aqua", "destroyer", "brim", "prime", "cal", "plant", "fish", "eol"]
NAME = dict(wof="Wall of Flesh", qslime="Queen Slime", cryo="Cryogen", twins="The Twins", aqua="Aquatic Scourge", destroyer="The Destroyer", brim="Brimstone Elemental", prime="Skeletron Prime", cal="Calamitas Clone", plant="Plantera", fish="Duke Fishron", eol="Empress of Light")
PAIR = dict(rev="Expert+Rev", mdeath="Master+Death")
CLS = "RMESO"
BATK = "calls pairs landed blkShared blkLocal blkStatic other collateral viol singleCalls dmg crossBlocked".split()


def f(x, d=0.0):
    try: return float(x)
    except (TypeError, ValueError): return d


rows = []
for run in a.runs:
    for out in sorted(glob.glob(run + "/jobs/*/try*/out.tsv")):
        base = out
        bat = {}
        if Path(base + ".bat.tsv").exists():
            for r in csv.DictReader(open(base + ".bat.tsv", errors="replace"), delimiter="\t"): bat[r["key"]] = r
        for r in csv.DictReader(open(out, errors="replace"), delimiter="\t"):
            if r.get("result") in (None, "", "result"): continue
            b = bat.get(r["key"], {})
            meta = b.get("meta", "")
            m = re.match(r"(\w+?)_(mid|on)_(\w+?)_(\w+?)_T([\d.]+)", meta)
            if not m: continue
            boss, kit, pair, imm, T = m.groups()
            sec = f(r["sec"]); dmg = f(r["dmgBoss"]); bmax = f(r["bossMax"]); bleft = f(r["bossLeft"])
            row = dict(boss=boss, kit=kit, pair=pair, imm=imm, seed=int(r["seed"]), gearT=T, result=r["result"], sec=sec, ttk=sec if r["result"] == "WIN" else "", levies=int(f(r["n"])), deaths=int(f(r["deaths"])),
                       deathsR=r["deathsR"], deathsM=r["deathsM"], deathsE=r["deathsE"], deathsS=r["deathsS"], firstDeathSec=r["firstDeathSec"], bossMax=int(bmax), bossLeft=int(bleft),
                       bossLeftPct=round(100 * bleft / bmax, 1) if bmax else "", dmgCredited=int(dmg), armyDps=round(dmg / sec, 1) if sec else "", dpsPctBossHpPerSec=round(100 * dmg / sec / bmax, 3) if sec and bmax else "",
                       dmgR=r["dmgR"], dmgM=r["dmgM"], dmgE=r["dmgE"], dmgS=r["dmgS"], dmgProj=r["dmgProj"], dmgMelee=r["dmgMelee"], takenBoss=r["takenBoss"], takenAdd=r["takenAdd"], takenOther=r["takenOther"],
                       tgtLevyPct=r["tgtLevyPct"], tgtPlayerPct=r["tgtPlayerPct"], commanderDamageTaken=0, potionsDrunk=r["potionsDrunk"], avgHp=r["avgHp"], enragedSec=r["enragedSec"], addsPeak=r["addsPeak"], halfSec=r["halfSec"],
                       drCuts=r["drCuts"], shakenPct=r["shakenPct"], routedPct=r["routedPct"], avgSurvSec=r["avgSurvSec"], hitsBoss=r["hitsBoss"], dtdPct=r["dtdPct"], bossMoved=r["bossMoved"])
            if b:
                row.update(meleeHits=b["meleeHits"], detourUsPerTick=b["detourUsPerTick"], altUsPerTick=b["altUsPerTick"], avgImmValue=b["avgImmValue"], avgStaticValue=b.get("avgStaticValue", ""))
                tot = {k: 0 for k in BATK}
                for c in CLS:
                    for k in BATK:
                        v = int(f(b.get(f"{c}_{k}", 0))); row[f"{c}_{k}"] = v; tot[k] += v
                for k in BATK: row["all_" + k] = tot[k]
            rows.append(row)
if not rows: sys.exit("no rows")
cols = list(dict.fromkeys(k for r in rows for k in r))
rows.sort(key=lambda r: (ORDER.index(r["boss"]) if r["boss"] in ORDER else 99, r["pair"], r["kit"], r["imm"], r["seed"]))
with open(a.csv, "w", newline="") as fh:
    w = csv.DictWriter(fh, fieldnames=cols); w.writeheader(); w.writerows(rows)
print(len(rows), "rows ->", a.csv)


def med(v): v = [x for x in v if x != ""]; return st.median(v) if v else None
def rng(v):
    v = [float(x) for x in v if x != ""]
    if not v: return "-"
    if len(v) == 1: return f"{v[0]:.0f}"
    return f"{st.median(v):.0f} ({min(v):.0f}-{max(v):.0f})"
def cell(rs):
    n = len(rs); wins = sum(r["result"] == "WIN" for r in rs)
    return dict(n=n, wins=wins, ttk=rng([r["ttk"] for r in rs]), deaths=rng([r["deaths"] for r in rs]), first=rng([r["firstDeathSec"] for r in rs if r["firstDeathSec"] not in ("-", "")]),
                dps=rng([r["armyDps"] for r in rs]), left=rng([r["bossLeftPct"] for r in rs if r["result"] != "WIN"]), hp=rng([r["bossMax"] for r in rs]), pot=rng([r["potionsDrunk"] for r in rs]))


md = []
base = [r for r in rows if r["imm"] == "shared"]
for boss in ORDER:
    rs = [r for r in base if r["boss"] == boss]
    if not rs: continue
    md.append(f"### {NAME[boss]}\n")
    md.append("| difficulty | kit (gear route tier) | wins | kill time s (median, range) | levy deaths /30 | first death s | army DPS | boss HP | boss HP left % (losses) | potions drunk |")
    md.append("|---|---|---|---|---|---|---|---|---|---|")
    for pair in ("rev", "mdeath"):
        for kit in ("mid", "on"):
            c = [r for r in rs if r["pair"] == pair and r["kit"] == kit]
            if not c: continue
            s = cell(c)
            md.append(f"| {PAIR[pair]} | {kit} (T {c[0]['gearT']}) | {s['wins']}/{s['n']} | {s['ttk']} | {s['deaths']} | {s['first']} | {s['dps']} | {s['hp']} | {s['left']} | {s['pot']} |")
    md.append("")
Path(a.md).write_text("\n".join(md) + "\n")
print("tables ->", a.md)

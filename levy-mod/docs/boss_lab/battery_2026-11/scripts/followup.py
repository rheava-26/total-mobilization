#!/usr/bin/env python3
"""followup.py OUT_CSV OUT_MD : every fight of the bossbiome follow-up runs, one row each, plus per-fight markdown tables."""
import csv, glob, re, statistics as st, sys
R = "/home/user/lab/bossbiome/runs/"
SETS = [  # run dir, label, code, daytime
    ("f_before", "before", "no EoL fix; surface arena", "night"), ("f_after", "after", "EoL fix; proper-biome arena (EoL: surface arena)", "night"),
    ("f_qb_before", "before", "surface arena", "night"), ("f_qb_after", "after", "proper (underground jungle) arena", "night"),
    ("f_day_before", "before", "no EoL fix (LEVY_EOLFLY=0)", "day"), ("f_day_after", "after", "EoL fix", "day")]
ARENA = {"brim": "Brimstone Crags depth (a3@crag)", "plant": "underground jungle box (a3e@jungle)", "fish": "ocean strip (a3@ocean)", "qb": "underground jungle box (a3e@jungle)"}
NAME = dict(brim="Brimstone Elemental", plant="Plantera", fish="Duke Fishron", eol="Empress of Light", qb="Queen Bee")
PAIR = dict(rev="Expert+Rev", mdeath="Master+Death")
def f(x):
    try: return float(x)
    except: return None
rows = []
for d, label, code, day in SETS:
    for fn in sorted(glob.glob(R + d + "/jobs/*/try*/out.tsv")):
        bat = {}
        try:
            for r in csv.DictReader(open(fn + ".bat.tsv", errors="replace"), delimiter="\t"): bat[r["key"]] = r.get("meta", "")
        except FileNotFoundError: pass
        for r in csv.DictReader(open(fn, errors="replace"), delimiter="\t"):
            if r.get("result") in (None, "", "result"): continue
            k = r["key"]; a = re.search(r"\|a([^|]*)\|", k); arena = a.group(1) if a else "a3"
            m = re.match(r"(\w+?)_(mid|on)_", bat.get(k, "")); kit = m.group(2) if m else "on"
            boss = r["boss"]; bm = f(r["bossMax"]); bl = f(r["bossLeft"])
            rows.append(dict(run=d, set=label, code=code, time=day, boss=boss, pair=r["mode"], kit=kit, seed=int(r["seed"]), arena=arena, result=r["result"], sec=r["sec"], deaths=r["deaths"],
                             firstDeathSec=r["firstDeathSec"], bossLeftPct=round(100 * bl / bm, 1) if bm and r["result"] != "WIN" else "", tgtLevyPct=r["tgtLevyPct"], tgtPlayerPct=r["tgtPlayerPct"],
                             enragedSec=r["enragedSec"], levies=r["n"]))
order = ["brim", "plant", "fish", "qb", "eol"]
rows.sort(key=lambda r: (order.index(r["boss"]) if r["boss"] in order else 9, r["time"], r["pair"] != "rev", r["kit"] != "mid", r["set"] != "before", r["seed"]))
with open(sys.argv[1], "w", newline="") as fh:
    w = csv.DictWriter(fh, fieldnames=list(rows[0])); w.writeheader(); w.writerows(rows)
md = []
for boss in order:
    for tm in ("night", "day"):
        rs = [r for r in rows if r["boss"] == boss and r["time"] == tm]
        if not rs: continue
        md.append(f"#### {NAME[boss]}" + (" (daytime fights)" if tm == "day" else "") + "\n")
        md.append("| difficulty | kit | seed | set | arena | result | kill s | levy deaths /30 | first death s | boss HP left % (loss) | attacks on levies % | on commander % | enraged s |")
        md.append("|---|---|---|---|---|---|---|---|---|---|---|---|---|")
        for r in rs:
            ar = "surface (a3)" if r["arena"] == "a3" else ARENA.get(boss, r["arena"]) if r["set"] == "after" else r["arena"]
            md.append(f"| {PAIR.get(r['pair'], r['pair'])} | {r['kit']} | {r['seed']} | {r['set']} | {ar} | {r['result']} | {r['sec'] if r['result']=='WIN' else '-'} | {r['deaths']} | {r['firstDeathSec']} | {r['bossLeftPct'] or '-'} | {r['tgtLevyPct']} | {r['tgtPlayerPct']} | {r['enragedSec']} |")
        md.append("")
        md.append("Summary (wins / fights, enraged seconds mean, median kill s of wins):\n")
        md.append("| set | wins | mean enraged s | mean attacks on levies % | median kill s |")
        md.append("|---|---|---|---|---|")
        for s in ("before", "after"):
            c = [r for r in rs if r["set"] == s]
            if not c: continue
            wins = [r for r in c if r["result"] == "WIN"]
            en = st.mean(f(r["enragedSec"]) or 0 for r in c); tl = st.mean(f(r["tgtLevyPct"]) or 0 for r in c)
            ks = st.median(f(r["sec"]) for r in wins) if wins else None
            md.append(f"| {s} | {len(wins)}/{len(c)} | {en:.0f} | {tl:.0f} | {'-' if ks is None else f'{ks:.0f}'} |")
        md.append("")
open(sys.argv[2], "w").write("\n".join(md) + "\n")
print(len(rows), "rows")

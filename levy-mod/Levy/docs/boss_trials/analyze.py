#!/usr/bin/env python3
"""Aggregates the boss-trial TSVs into CSV + markdown tables. usage: analyze.py <outdir> <out_*.tsv ...>"""
import csv, sys, os, statistics as st, collections, re, json

outdir = sys.argv[1]
files = sys.argv[2:]
rows = []
for f in files:
    with open(f) as fh:
        r = csv.DictReader(fh, delimiter="\t")
        for x in r:
            x["_file"] = os.path.basename(f)
            rows.append(x)
cfgdesc = {}
for f in files:
    p = f + ".cfgs.tsv"
    if os.path.exists(p):
        for l in open(p):
            a = l.rstrip("\n").split("\t")
            if len(a) >= 3: cfgdesc[a[0]] = (a[1], a[2])

def fl(x, k, d=0.0):
    try: return float(x[k])
    except Exception: return d

def variant(x):
    # the key's flag field: camp/honey/ns/np/nb
    parts = x["key"].split("|")
    return parts[6] if len(parts) > 6 else "?"

def mean(v): return sum(v) / len(v) if v else float("nan")
def sd(v): return st.pstdev(v) if len(v) > 1 else 0.0
def med(v): return st.median(v) if v else float("nan")
def pct(n, d): return 100.0 * n / d if d else float("nan")

fights = [x for x in rows if x["mode"] == "fight" and x["result"] in ("WIN", "LOSS", "TIMEOUT", "DESPAWN")]
errs = [x for x in rows if x["result"] == "ERROR"]

def summarize(group):
    n = len(group); wins = [g for g in group if g["result"] == "WIN"]
    ttk = [fl(g, "sec") for g in wins]
    return {
        "runs": n, "winPct": pct(len(wins), n), "ttkMean": mean(ttk), "ttkSd": sd(ttk), "ttkMed": med(ttk),
        "bossMax": mean([fl(g, "bossMax") for g in group]),
        "deaths": mean([fl(g, "deaths") for g in group]), "nLevies": mean([fl(g, "nLevies") for g in group]),
        "survSec": mean([fl(g, "avgSurvSec") for g in group]),
        "dps": mean([fl(g, "dmgBoss") / max(1.0, fl(g, "sec")) for g in group]),
        "dpsPerLevy": mean([fl(g, "dmgBoss") / max(1.0, fl(g, "sec")) / max(1.0, fl(g, "nLevies")) for g in group]),
        "takenBoss": mean([fl(g, "takenBoss") for g in group]), "takenAdd": mean([fl(g, "takenAdd") for g in group]),
    }

def table(name, groups, keyhdr):
    """groups: ordered dict key -> list of rows; writes csv + returns markdown"""
    cols = ["runs", "winPct", "ttkMean", "ttkSd", "ttkMed", "bossMax", "nLevies", "deaths", "survSec", "dps", "dpsPerLevy", "takenBoss", "takenAdd"]
    path = os.path.join(outdir, name + ".csv")
    md = ["| " + " | ".join(keyhdr + cols) + " |", "|" + "---|" * (len(keyhdr) + len(cols))]
    with open(path, "w", newline="") as fh:
        w = csv.writer(fh); w.writerow(keyhdr + cols)
        for k, g in groups.items():
            s = summarize(g)
            kk = list(k) if isinstance(k, tuple) else [k]
            vals = [("%.1f" % s[c]) if isinstance(s[c], float) else str(s[c]) for c in cols]
            w.writerow(kk + vals); md.append("| " + " | ".join(kk + vals) + " |")
    return "\n".join(md)

def group(rowsx, keyf):
    d = collections.OrderedDict()
    for x in sorted(rowsx, key=lambda r: str(keyf(r))):
        d.setdefault(keyf(x), []).append(x)
    return d

out = []
roll = [x for x in fights if x["cfg"] != "" and not x["cfg"].startswith("fixed")]
fixed = [x for x in fights if x["cfg"].startswith("fixed")]

# 1 rolled main table: boss x difficulty x order, control variant only
ctl = [x for x in roll if variant(x) == "1" and x["arena"] == "big" and x["auto"] == "1"]
out.append("### Rolled configurations (control: big arena with platforms, campfire + heart lantern, potions, Autonomous, Spectator)\n")
out.append(table("roll_main", group(ctl, lambda r: (r["boss"], r["diff"], r["order"])), ["boss", "difficulty", "order"]))
# 2 variables: paired toggles on the first 12 configs (eye + brain, classic, free rein)
tog = [x for x in roll if x["diff"] == "classic" and x["order"] == "free" and x["boss"] in ("eye", "brain", "kslime")]
def cfgnum(x): return int(x["cfg"].split("-")[-1])
tog = [x for x in tog if cfgnum(x) <= 12]
def togname(x):
    v = variant(x); a = x["auto"]; ar = x["arena"]
    parts = []
    if a == "0": parts.append("no Autonomy")
    if ar != "big": parts.append("arena " + ar)
    if v.startswith("0"): parts.append("no campfire/lantern")
    if "h" in v: parts.append("honey")
    if "np" in v: parts.append("no potions")
    return " + ".join(parts) if parts else "CONTROL"
out.append("\n### Variable toggles on the same 12 rolled configurations (Classic, Free Rein)\n")
out.append(table("roll_toggles", group(tog, lambda r: (r["boss"], togname(r))), ["boss", "variant"]))

# 3 feature analysis over every rolled control fight: win rate and time by feature
def feats(x):
    comp, desc = cfgdesc.get(x["cfg"], ("", ""))
    lv = [l for l in desc.split(" ; ") if l]
    f = {}
    f["levies"] = len(lv)
    f["pure"] = x["comp"]
    f["rangers"] = sum(1 for l in lv if l.startswith("R:")); f["mages"] = sum(1 for l in lv if l.startswith("M:"))
    f["melee"] = sum(1 for l in lv if l.startswith("E:")); f["summ"] = sum(1 for l in lv if l.startswith("S:"))
    f["avgHp"] = fl(x, "avgHp"); f["avgMana"] = fl(x, "avgMana")
    f["acc"] = sum(0 if l.split("|")[2] == "-" else len(l.split("|")[2].split("+")) for l in lv) / max(1, len(lv))
    f["maa"] = sum(1 for l in lv if "|MaA|" in l) / max(1, len(lv))
    return f
feat_rows = []
for x in ctl:
    f = feats(x); f["x"] = x; feat_rows.append(f)
def bucket(v, edges):
    for i, e in enumerate(edges):
        if v < e: return "<%g" % e if i == 0 else "%g-%g" % (edges[i - 1], e)
    return ">=%g" % edges[-1]
out.append("\n### Which rolled variables matter (all control fights, Classic + Expert, all bosses; win % and mean time to kill of the wins)\n")
def featable(name, keyf, hdr):
    for boss in ("kslime", "eye", "brain"):
        sub = [f["x"] for f in feat_rows if f["x"]["boss"] == boss]
        if not sub: continue
    d = collections.OrderedDict()
    for f in sorted(feat_rows, key=lambda f: (f["x"]["boss"], str(keyf(f)))):
        d.setdefault((f["x"]["boss"], keyf(f)), []).append(f["x"])
    return table(name, d, ["boss", hdr])
out.append(featable("feat_levies", lambda f: bucket(f["levies"], [5, 7, 9, 11]), "levy count") + "\n")
out.append(featable("feat_avghp", lambda f: bucket(f["avgHp"], [120, 140, 160, 190]), "avg HP") + "\n")
out.append(featable("feat_mana", lambda f: bucket(f["avgMana"], [50, 70, 90, 120]), "avg mana") + "\n")
out.append(featable("feat_acc", lambda f: bucket(f["acc"], [0.5, 1.0, 1.5]), "accessories/levy") + "\n")
out.append(featable("feat_mage_share", lambda f: bucket(f["mages"] / max(1, f["levies"]), [0.2, 0.4, 0.6]), "mage share") + "\n")
out.append(featable("feat_ranger_share", lambda f: bucket(f["rangers"] / max(1, f["levies"]), [0.2, 0.4, 0.6]), "ranger share") + "\n")
out.append(featable("feat_melee_share", lambda f: bucket(f["melee"] / max(1, f["levies"]), [0.1, 0.25, 0.4]), "melee share") + "\n")
out.append(featable("feat_summ_share", lambda f: bucket(f["summ"] / max(1, f["levies"]), [0.1, 0.25, 0.4]), "summoner share") + "\n")

# 4 per-config detail (lowest win rates first)
percfg = collections.OrderedDict()
for x in roll:
    if variant(x) == "1" and x["arena"] == "big" and x["auto"] == "1":
        percfg.setdefault((x["cfg"], x["diff"], x["order"]), []).append(x)
path = os.path.join(outdir, "roll_configs.csv")
with open(path, "w", newline="") as fh:
    w = csv.writer(fh); w.writerow(["cfg", "difficulty", "order", "comp", "trials", "win%", "ttkMean", "deaths", "levyDescription"])
    for k, g in percfg.items():
        s = summarize(g); comp, desc = cfgdesc.get(k[0], ("", ""))
        w.writerow([k[0], k[1], k[2], comp, s["runs"], "%.0f" % s["winPct"], "%.1f" % s["ttkMean"], "%.1f" % s["deaths"], desc])

# 5 fixed tables (tier x composition x stats)
fx = [x for x in fixed if x["comp"] not in ()]
fx_tier = [x for x in fx if re.match(r"^(m10|R3M3E2S2|R1M1E1S1|R2M2)$", x["comp"]) or x["comp"] in ("R3M3E2S2", "R1M1E1S1", "R2M2")]
out.append("\n### Fixed loadout tiers (Eye / King Slime; stats best = 400 hp / 200 mana, real = 140 hp / 20 mana, mages 100 mana)\n")
def fx_key(x):
    tier = x["tier"]; stats = x["key"].split("|")[9] if len(x["key"].split("|")) > 9 else "?"
    name = {"R3M3E2S2": "10 levies (3R 3M 2Me 2Su)", "R1M1E1S1": "4 levies (1 each)", "R2M2": "4 levies (2R 2M)"}.get(x["comp"], x["comp"])
    return (x["boss"], x["diff"], name, stats, tier)
out.append(table("fixed_tiers", group(fx_tier, fx_key), ["boss", "difficulty", "composition", "stats", "tier"]))

# 6 count curve
def ncount(code): return sum(int(n) for n in re.findall(r"\d+", code))
curve = [x for x in fixed if (re.match(r"^[RM]\d+$", x["comp"]) or (re.match(r"^([RMES]\d+){4}$", x["comp"]) and ncount(x["comp"]) in (4, 6, 8, 10, 12, 15, 20, 30))) and x["tier"] == "opt" and "real" in x["key"] and x["boss"] in ("eye", "brain", "kslime")]
def ckey(x): return (x["boss"], x["diff"], x["comp"][0] if re.match(r"^[RM]\d+$", x["comp"]) else "mix", ncount(x["comp"]))
cg = group(curve, ckey)
cg = collections.OrderedDict(sorted(cg.items(), key=lambda kv: kv[0]))
cg2 = collections.OrderedDict(((k[0], k[1], k[2], str(k[3])), v) for k, v in cg.items())
out.append("\n### Levy-count curve (identical loadouts, only the count changes; Spectator, Autonomous)\n")
out.append(table("count_curve", cg2, ["boss", "difficulty", "class", "levies"]))

# 7 melee pick
pick = [x for x in fixed if "E4" == x["comp"]]
pk = collections.OrderedDict()
for x in sorted(pick, key=lambda r: (r["boss"], r["diff"], r["key"])):
    parts = x["key"].split("|"); beam = "boost OFF (2.0.4 ranking)" if "nb" in parts[6] else "boost ON"
    stats = parts[9]
    pk.setdefault((x["boss"], x["diff"], stats, beam), []).append(x)
out.append("\n### Part C: four melee levies pick their weapon by rank from {Enchanted Sword, Night's Edge, Muramasa, Katana, Platinum Broadsword}\n")
out.append(table("partC_melee_pick", pk, ["boss", "difficulty", "stats", "ranking"]))

# 8 reference DPS
refs = [x for x in rows if x["mode"] == "ref"]
path = os.path.join(outdir, "ref_dps.csv")
rmd = ["| class | weapon | stats | levy DPS | player DPS | player:levy |", "|---|---|---|---|---|---|"]
with open(path, "w", newline="") as fh:
    w = csv.writer(fh); w.writerow(["class", "weapon", "stats", "levyDps", "playerDps", "ratio", "note"])
    for x in refs:
        secs = fl(x, "sec", 30.0)
        ld, pd = fl(x, "dmgR") * 0, 0
        ld = fl(x, "dmgBoss"); pd = fl(x, "dmgR") if False else fl(x, "dmgR")
        # ref rows store levy window damage in dmgBoss and player window damage in dmgR
        ld = fl(x, "dmgBoss"); pd = fl(x, "dmgR")
        lps, pps = ld / secs, pd / secs
        stats = x["key"].split("|")[9]
        ratio = (pps / lps) if lps > 0 else float("nan")
        w.writerow([x["comp"][0], x["perLevy"], stats, "%.1f" % lps, "%.1f" % pps, "%.2f" % ratio, x["note"]])
        rmd.append(f"| {x['comp'][0]} | {x['perLevy'].replace('_', ' ')} | {stats} | {lps:.1f} | {pps:.1f} | {ratio:.2f} |")
out.append("\n### Player-reference DPS (live Player.ItemCheck vs the same gear on a levy; pinned Eye-like dummy, defense 12, 30 s)\n")
out.append("\n".join(rmd))
out.append(f"\n\nruns: {len(fights)} fights, {len(refs)} reference windows, {len(errs)} errors")
open(os.path.join(outdir, "tables.md"), "w").write("\n".join(out))
print("\n".join(out))

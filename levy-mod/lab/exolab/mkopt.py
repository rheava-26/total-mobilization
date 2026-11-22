#!/usr/bin/env python3
"""The "optimal at her tier" army for the Exo Mechs lab: the best of her own stock, fight-tested, in the best armour she can make.
  mkopt.py RUN_DIR [RUN_DIR ...] OUT.json
Weapons: every weapon her 40 regulars carried, ranked by the damage one carrier did to the mechs per fight, pooled over her army's lab fights
(the kit's per-weapon damage, kit.tsv, divided by how many of her levies carry that weapon); the top ones fill 40 places, at most 6 levies per weapon
(so no single weapon's per-type hit cooldown is shared by the whole army), best prefix she has.
Each levy keeps the sidearm, accessories, wings, boots, hook and ammo of her levy that carried that weapon (her Armory's own deal for that class).
Armour: Auric Tesla for everyone, the head of the levy's class (post-Yharon; her commander wears it, so she can craft it).
Not her stock (untested in levy hands, left out): the commander's own Auric weapons (Dynamic Pursuer, Eclipse's Fall, The Final Dawn)."""
import csv, glob, json, re, sys
from collections import Counter, defaultdict
from pathlib import Path

H = Path(__file__).resolve().parent
runs, out = sys.argv[1:-1], sys.argv[-1]
eve = json.loads((H / "armies" / "eve_army.json").read_text())
base = lambda w: re.sub(r"@.*$", "", w)
carriers = Counter(base(l["weapon"]) for l in eve["levies"])
src = {}
for l in eve["levies"]:
    src.setdefault(base(l["weapon"]), l)
dmg, fights = defaultdict(float), 0
for run in runs:
    cells = {c["id"]: c["name"] for c in json.load(open(Path(run) / "cells.json"))["cells"]}
    for t in glob.glob(str(Path(run) / "jobs/*/try*/out.tsv.kit.tsv")):
        for r in csv.DictReader(open(t), delimiter="\t"):
            if "meta=exo_eve_exo" not in cells.get(r["tag"], ""): continue
            fights += 1
            for kv in (r.get("weaponDmg") or "").split():
                k, _, v = kv.rpartition("=")
                k = k.replace("C:", "CalamityMod/")
                dmg[base(k)] += float(v)
per = {w: dmg[w] / fights / carriers[w] for w in carriers if fights}
rank = sorted(per, key=lambda w: -per[w])
HEAD = dict(E="Melee", R="Ranged", T="Rogue", M="Magic", S="Summon")
levies, i = [], 0
for w in rank:  # greedy: the best weapon per carrier first, up to 6 levies each
    for _ in range(min(6, 40 - len(levies))):
        s0 = src[w]; i += 1
        lv = json.loads(json.dumps(s0)); lv["id"] = i
        lv["weapon"] = max((l["weapon"] for l in eve["levies"] if base(l["weapon"]) == w), key=len)  # a prefixed copy beats the bare one
        lv["armor"] = ["CalamityMod/AuricTeslaHead" + HEAD[s0["cls"]], "CalamityMod/AuricTeslaBodyArmor", "CalamityMod/AuricTeslaCuisses"]
        lv["measuredPerCarrier"] = round(per[w])
        levies.append(lv)
    if len(levies) >= 40: break
army = dict(eve, levies=levies, note=__doc__, ranking=[(w, round(per[w])) for w in sorted(per, key=lambda w: -per[w])], fightsUsed=fights)
Path(out).write_text(json.dumps(army, indent=1))
print(fights, "fights;", Counter(base(l["weapon"]).split("/")[-1] for l in levies).most_common())
for w in sorted(per, key=lambda w: -per[w])[:15]: print(f"  {w.split('/')[-1]:24s} {per[w]:>10,.0f} per carrier per fight ({carriers[w]} carriers)")

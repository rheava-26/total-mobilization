#!/usr/bin/env python3
"""mkarmy.py: Eve's army files for the lagsweep pillar lab (run from anywhere; writes next to this script).
  eve45.json   her 34 recorded levies plus copies of levies 1-11 (her 11-17 pillar fights had 44-46 levies), the base of every pillar cell
  eve45_drop_<slug>.json   eve45 with every levy whose weapon, sidearm or ammo names <slug> taken out (the "one or two weapons" hypothesis)
  eve45_minus<N>.json      eve45 with N levies taken out at random (seeded), the same count as a weapon drop: the control for it
Usage: mkarmy.py [drop SLUG[,SLUG...]] [minus N]"""
import json, random, sys, re
from pathlib import Path
HERE = Path(__file__).resolve().parent
base = json.load(open(HERE.parent / "battery" / "eve_army.json"))

def write(name, levies, note):
    d = dict(base); d["levies"] = levies; d["note"] = note
    (HERE / name).write_text(json.dumps(d, indent=1)); print(name, len(levies), "levies")

lv = [dict(l) for l in base["levies"]]
extra = [dict(l, id=100 + i) for i, l in enumerate(lv[:11])]
army = lv + extra
write("eve45.json", army, "Eve's 34 levies (2026-11-16 Empress recording) plus copies of levies 1-11: 45 levies, the size of her army in the 11-17 pillar fights")
args = sys.argv[1:]
while args:
    k = args.pop(0)
    if k == "drop":
        slugs = args.pop(0).split(",")
        keep = [l for l in army if not any(s.lower() in (l.get("weapon", "") + " " + str(l.get("side", "")) + " " + str(l.get("ammo", ""))).lower() for s in slugs)]
        write("eve45_drop_" + re.sub(r"\W", "", "_".join(slugs)) + ".json", keep, f"eve45 without the levies carrying {slugs} ({len(army) - len(keep)} taken out)")
    elif k == "minus":
        n = int(args.pop(0)); rnd = random.Random(17); drop = set(rnd.sample(range(len(army)), n))
        write(f"eve45_minus{n}.json", [l for i, l in enumerate(army) if i not in drop], f"eve45 with {n} levies taken out at random (seed 17)")
    elif k == "gearcost":
        # the gear-cost labs: eve40 (her first 40) and eve40v (the same 40 with every Calamity item swapped for a vanilla one of the same job), so the same army runs with and without Calamity
        SETS = {"E": ["Beetle Helmet", "Beetle Shell", "Beetle Leggings"], "R": ["Shroomite Headgear", "Shroomite Breastplate", "Shroomite Leggings"], "M": ["Spectre Hood", "Spectre Robe", "Spectre Pants"],
                "S": ["Spooky Helmet", "Spooky Breastplate", "Spooky Leggings"], "T": ["Shroomite Headgear", "Shroomite Breastplate", "Shroomite Leggings"]}
        WEAP = {"E": ("Terra Blade", None), "R": ("Chain Gun", "Chlorophyte Bullet"), "M": ("Nebula Arcanum", None), "S": ("Morning Star", None), "T": ("Chain Gun", "Chlorophyte Bullet")}
        cal = lambda x: isinstance(x, str) and x.startswith("CalamityMod/")
        def vanilla(l):
            l = dict(l); c = l.get("cls", "R")
            if any(cal(a) for a in l.get("armor", [])): l["armor"] = SETS[c]
            l["acc"] = [("Butterfly Wings" if "Wings" in a else "Terraspark Boots" if ("Treads" in a or "Boots" in a) else "Avenger Emblem") if cal(a) else a for a in l.get("acc", [])]
            if cal(l.get("weapon")): w, am = WEAP[c]; l["weapon"] = w; l["weaponName"] = w; l["ammo"] = am; l["cls"] = "R" if c == "T" else c
            if cal(l.get("ammo")): l["ammo"] = "Chlorophyte Arrow"
            if cal(l.get("side")): l.pop("side", None)
            return l
        write("eve40.json", army[:40], "Eve's army, the first 40 of eve45 (the gear-cost lab)")
        write("eve40v.json", [vanilla(l) for l in army[:40]], "eve40 with every Calamity item swapped for a vanilla one of the same job (the gear-cost lab, with and without Calamity)")

#!/usr/bin/env python3
"""Gear kits for the 2026-11 boss battery. A kit = what the army carries when a boss with progression P is fought.
on-tier  = everything obtainable BEFORE the boss (route tier <= P - 0.01); mid = one tier below (route <= P - 1.5).
Weapons come from the game's own route table (item_tiers.tsv, dumped by the Levy weapon-choice probe: boss drops, shop conditions, recipes), ranked by Levy's WeaponClassifier.Score.
Armor / accessory / ammo ladders are hand-set vanilla sets (the probe table has no armor). Same (boss, kit, seed) = same army in every arm."""
import random, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
TIERS = HERE.parent / "item_tiers.tsv"

# Boss Checklist style progression (ProgressionTierProbe): WoF 7, Queen Slime 8, Cryogen 8.5, Twins 9, Aquatic Scourge 9.5, Destroyer 10, Brimstone Elemental 10.5, Prime 11, Calamitas Clone 11.7, Plantera 12, Fishron 14, Empress 15
PROG = dict(wof=7.0, qslime=8.0, cryo=8.5, twins=9.0, aqua=9.5, destroyer=10.0, brim=10.5, prime=11.0, cal=11.7, plant=12.0, fish=14.0, eol=15.0)

ARMOR = {  # min route tier -> {class: [head, body, legs]}; the highest key <= T wins; summoners wear the melee head
    0.0:   {"R": ["Platinum Helmet", "Platinum Chainmail", "Platinum Greaves"], "M": ["Jungle Hat", "Jungle Shirt", "Jungle Pants"], "E": ["Molten Helmet", "Molten Breastplate", "Molten Greaves"], "S": ["Bee Headgear", "Bee Breastplate", "Bee Greaves"]},
    5.0:   {"R": ["CalamityMod/AerospecHeadRanged", "CalamityMod/AerospecBreastplate", "CalamityMod/AerospecLeggings"], "M": ["CalamityMod/AerospecHeadMagic", "CalamityMod/AerospecBreastplate", "CalamityMod/AerospecLeggings"],
            "E": ["Molten Helmet", "Molten Breastplate", "Molten Greaves"], "S": ["CalamityMod/AerospecHeadSummon", "CalamityMod/AerospecBreastplate", "CalamityMod/AerospecLeggings"]},
    6.9:   {"R": ["Mythril Hood", "Mythril Chainmail", "Mythril Greaves"], "M": ["Mythril Hat", "Mythril Chainmail", "Mythril Greaves"], "E": ["Orichalcum Helmet", "Orichalcum Breastplate", "Orichalcum Leggings"], "S": ["Mythril Helmet", "Mythril Chainmail", "Mythril Greaves"]},
    8.9:   {"R": ["Adamantite Mask", "Adamantite Breastplate", "Adamantite Leggings"], "M": ["Adamantite Headgear", "Adamantite Breastplate", "Adamantite Leggings"], "E": ["Titanium Helmet", "Titanium Breastplate", "Titanium Leggings"], "S": ["Adamantite Helmet", "Adamantite Breastplate", "Adamantite Leggings"]},
    10.99: {"R": ["Hallowed Helmet", "Hallowed Plate Mail", "Hallowed Greaves"], "M": ["Hallowed Headgear", "Hallowed Plate Mail", "Hallowed Greaves"], "E": ["Hallowed Mask", "Hallowed Plate Mail", "Hallowed Greaves"], "S": ["Hallowed Mask", "Hallowed Plate Mail", "Hallowed Greaves"]},
    12.4:  {"R": ["Shroomite Helmet", "Shroomite Breastplate", "Shroomite Leggings"], "M": ["Spectre Hood", "Spectre Robe", "Spectre Pants"], "E": ["Chlorophyte Mask", "Chlorophyte Plate Mail", "Chlorophyte Greaves"], "S": ["Chlorophyte Mask", "Chlorophyte Plate Mail", "Chlorophyte Greaves"]},
    13.4:  {"R": ["Shroomite Helmet", "Shroomite Breastplate", "Shroomite Leggings"], "M": ["Spectre Hood", "Spectre Robe", "Spectre Pants"], "E": ["Beetle Helmet", "Beetle Scale Mail", "Beetle Leggings"], "S": ["Beetle Helmet", "Beetle Scale Mail", "Beetle Leggings"]},
}
CLASS_ACC = {"R": "Molten Quiver", "M": "Band of Starpower", "E": "Feral Claws", "S": "Hermes Boots"}


def accessories(T, cls):
    return ["Cloud in a Bottle", "Shield of Cthulhu", CLASS_ACC[cls], "Band of Regeneration", "Angel Wings" if T >= 6.9 else "Hermes Boots"]


def ammo_for(kind, T, idx):
    if kind == "arrow":
        if T >= 12.4: return "Chlorophyte Arrow" if idx % 2 == 0 else "Holy Arrow"
        if T >= 10.99: return "Holy Arrow" if idx % 2 == 0 else "Hellfire Arrow"
        return "Hellfire Arrow" if idx % 2 else "Jester's Arrow"
    if kind == "bullet":
        if T >= 12.4: return "Chlorophyte Bullet"
        if T >= 6.9: return "Crystal Bullet"
        return "Meteor Shot"
    if kind == "rocket": return "Rocket III" if T >= 6.9 else "Rocket I"
    if kind == "dart": return "Poison Dart"
    if kind == "gel": return "Gel"
    return ""


AMMO_KIND = {"40": "arrow", "97": "bullet", "771": "rocket", "283": "dart", "23": "gel", "0": ""}  # AmmoID: Arrow 40, Bullet 97, Rocket 771, Dart 283, Gel 23
EXCLUDE = {"UrchinMace", "SaharaSlicers", "PlagueStaff"}
_ITEMS = None


def load_items():
    global _ITEMS
    if _ITEMS is not None: return _ITEMS
    rows = []; started = False; hdr = None
    for line in open(TIERS, encoding="utf-8", errors="replace"):
        line = line.rstrip("\n")
        if line.startswith("mod\tname\tfamily"): started = True; hdr = line.split("\t"); continue
        if not started or not line or line.startswith("#"): continue
        f = line.split("\t")
        if len(f) < len(hdr): continue
        rows.append(dict(zip(hdr, f)))
    _ITEMS = rows
    return rows


def pool(T):
    out = {c: [] for c in "RMES"}
    for d in load_items():
        cls = {"ranged": "R", "magic": "M", "melee": "E", "summon": "S"}.get(d["class"])
        if not cls or d["name"] in EXCLUDE: continue
        try: route = float(d["route"]); score = float(d["score"])
        except ValueError: continue
        if route <= 0 or route > T or d["routeSrc"].startswith(("unknown", "cycle", "none")): continue
        ak = AMMO_KIND.get(d["useAmmo"])
        if ak is None: continue
        w = (d.get("displayName") or d["name"]) if d["mod"] == "Vanilla" else d["mod"] + "/" + d["name"]
        out[cls].append((score, w, ak, route))
    for c in out: out[c].sort(reverse=True)
    return out


def threshold(boss, kit): return PROG[boss] - 0.01 if kit == "on" else PROG[boss] - 1.5


def make_army(boss, kit, seed, n=30, topk=6, lc=15):
    T = threshold(boss, kit); pl = pool(T)
    rng = random.Random(f"{boss}:{kit}:{seed}")
    counts = {"R": round(n * 0.34), "M": round(n * 0.34), "E": round(n * 0.16)}; counts["S"] = n - sum(counts.values())
    am = ARMOR[max(k for k in ARMOR if k <= T)]
    levies = []; ridx = 0
    for cls in "RMES":
        top = pl[cls][:topk]
        for _ in range(counts[cls]):
            sc, w, ak, route = rng.choice(top)
            ammo = ammo_for(ak, T, ridx) if ak else ""
            if ak: ridx += 1
            levies.append(dict(cls=cls, weapon=w, ammo=ammo, armor=am[cls], acc=accessories(T, cls), lc=lc, mc=9, potions=8, potion="Greater Healing Potion" if T >= 6.9 else "Healing Potion"))
    return dict(T=T, tops={c: [(round(s), w, round(r, 2)) for s, w, _, r in pl[c][:topk]] for c in "RMES"}, levies=levies, armor=am)


if __name__ == "__main__":
    b, k = sys.argv[1], sys.argv[2]
    a = make_army(b, k, 1)
    print(b, k, "T=%.2f" % a["T"])
    for c in "RMES": print(" ", c, a["tops"][c])
    print(" armor", a["armor"]["R"][0], "|", a["armor"]["E"][0])

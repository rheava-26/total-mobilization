"""Exo Mechs lab armies (lab only).
  mkarmies.py EVE_REC CALNAMES.json OUT_DIR
eve_army.json          Eve's 40 regular levies (ids 1-40) as worn in her 2026-11-21 17:19 Exo Mechs fight (Levy 3.1.8), via mkeve.py (battery/mkeve.py): the last lgear
                       baseline of every levy: weapon and prefix, sidearm, armour, accessories, wings, boots, hook, ammo, max life. Officers and militia (ids < 0) are left out.
eve_matched_army.json  the same levies wearing the armour the 3.1.9+ Armory deals (armour class = class of the weapon in hand): the head of the same set for that class
                       (God Slayer melee/ranged/rogue; Bloodflare and Tarragon all five). Mages and summoners in God Slayer (it has no head for them) and a non-summoner in the
                       summoner Fearmonger set get the full set of their class: Bloodflare for magic and summon, God Slayer for the rest.
                       INFERRED: that Bloodflare magic and summon heads are in her Armory (she owns Bloodflare bodies and legs; Bloodflare is within the Armory's 3-tier class tolerance of God Slayer).
Not recorded, stand-ins: 9 mana crystals, 8 Super Healing Potions, no buff potions (none in her lbuf rows), the full lab station set."""
import json, subprocess, sys
from pathlib import Path

rec, calf, out = sys.argv[1:4]
out = Path(out); out.mkdir(parents=True, exist_ok=True)
H = Path(__file__).resolve().parent
subprocess.run([sys.executable, str(H / "mkeve.py"), rec, calf, str(out / "eve_raw.json")], check=True)
a = json.loads((out / "eve_raw.json").read_text())
for l in a["levies"]:
    l["potion"] = "Super Healing Potion"; l["potions"] = 8
a["note"] = "Eve's 40 regular levies as worn in her 2026-11-21 17:19 Exo Mechs fight (Levy 3.1.8)."
(out / "eve_army.json").write_text(json.dumps(a, indent=1))

C = "CalamityMod/"
FIVE = dict(E="Melee", R="Ranged", T="Rogue", M="Magic", S="Summon")
HEADS = {"GodSlayer": {"E": "GodSlayerHeadMelee", "R": "GodSlayerHeadRanged", "T": "GodSlayerHeadRogue"},
         "Bloodflare": {k: "BloodflareHead" + v for k, v in FIVE.items()},
         "Tarragon": {k: "TarragonHead" + v for k, v in FIVE.items()}}
BLOODFLARE = {k: [C + HEADS["Bloodflare"][k], C + "BloodflareBodyArmor", C + "BloodflareCuisses"] for k in FIVE}
GODSLAYER = {k: [C + HEADS["GodSlayer"][k], C + "GodSlayerChestplate", C + "GodSlayerLeggings"] for k in HEADS["GodSlayer"]}
changes = []
m = json.loads(json.dumps(a))
for l in m["levies"]:
    head = l["armor"][0].split("/")[-1]; cls = l["cls"]; old = list(l["armor"])
    fam = next((f for f in HEADS if head.startswith(f + "Head")), None)
    if fam and cls in HEADS[fam]:
        l["armor"][0] = C + HEADS[fam][cls]
    elif fam == "GodSlayer":
        l["armor"] = BLOODFLARE[cls]
    elif head.startswith("Fearmonger") and cls != "S":
        l["armor"] = GODSLAYER.get(cls) or BLOODFLARE[cls]
    if l["armor"] != old:
        changes.append(f"levy {l['id']} ({cls} {l['weaponName']}): {' / '.join(x.split('/')[-1] for x in old)} -> {' / '.join(x.split('/')[-1] for x in l['armor'])}")
m["note"] = "Eve's 40 levies with class-matched armour (the 3.1.9+ Armory rule); weapons and accessories unchanged."
m["changes"] = changes
(out / "eve_matched_army.json").write_text(json.dumps(m, indent=1))
print(len(a["levies"]), "levies;", len(changes), "armour changes")
print("\n".join(changes))

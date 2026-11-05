#!/usr/bin/env python3
"""Generates gear_thorium.spec and gear_cal.spec: modded accessories / sets x {npcProj=true, real owner, real owner + Player.Update subset s2}; each weapon also gets a no-gear control."""
MODES = [("np_pu0", "", ""), ("real_pu0", "nonpc", ""), ("real_s2", "nonpc", "pu=s2")]
BASE = "vslot,netlock,halflife,hurt=150"

THOR = [
    ("VampireGland", "T:VampireGland", "V:Musket", "fam=other,dist=14"),
    ("FrostburnPouch", "T:FrostburnPouch", "T:GoblinWarSpear", "fam=other,dist=14"),
    ("BlizzardPouch", "T:BlizzardPouch", "T:GoblinWarSpear", "fam=other,dist=14"),
    ("SandshroudPouch", "T:SandshroudPouch", "T:GoblinWarSpear", "fam=other,dist=14"),
    ("GutWrenchersGauntlet", "T:GutWrenchersGauntlet", "V:Spear", "fam=spear"),
    ("InfernoLordsFocus", "T:InfernoLordsFocus", "V:AmethystStaff", "fam=other,dist=12"),
    ("MagmaLocket", "T:MagmaLocket", "V:Musket", "fam=other,dist=14"),
    ("ConcussiveWarhead", "T:ConcussiveWarhead", "V:Musket", "fam=other,dist=14"),
    ("CrystalScorpion", "T:CrystalScorpion", "V:Musket", "fam=other,dist=14"),
    ("LifeQuartzShield", "T:LifeQuartzShield", "V:Musket", "fam=other,dist=14"),
    ("HexingTalisman", "T:HexingTalisman", "V:AmethystStaff", "fam=other,dist=12"),
    ("ShinobiSigil", "T:ShinobiSigil", "T:GoblinWarSpear", "fam=other,dist=14"),
    ("Set:LifeBloom", "S:ThoriumMod/LifeBloomMask", "V:AmethystStaff", "fam=other,dist=12"),
    ("Set:Dragon", "S:ThoriumMod/DragonMask", "V:Musket", "fam=other,dist=14"),
    ("Set:Pyromancer", "S:ThoriumMod/PyromancerCowl", "V:AmethystStaff", "fam=other,dist=12"),
    ("Set:Cryomancer", "S:ThoriumMod/CryomancersCrown", "V:AmethystStaff", "fam=other,dist=12"),
]
CAL = [
    ("TheBee", "C:TheBee", "V:Musket", "fam=other,dist=14"),
    ("ElectriciansGlove", "C:ElectriciansGlove", "V:Musket", "fam=other,dist=14"),
    ("StarbusterCore", "C:StarbusterCore", "V:Musket", "fam=other,dist=14"),
    ("ToxicHeart", "C:ToxicHeart", "V:Musket", "fam=other,dist=14"),
    ("GladiatorsLocket", "C:GladiatorsLocket", "V:Musket", "fam=other,dist=14"),
    ("NuclearFuelRod", "C:NuclearFuelRod", "V:Musket", "fam=other,dist=14"),
    ("VampiricTalisman", "C:VampiricTalisman", "C:AdamantiteThrowingAxe", "fam=other,dist=10"),
    ("Nanotech", "C:Nanotech", "C:AdamantiteThrowingAxe", "fam=other,dist=10"),
    ("Statigel rogue set", "S:CalamityMod/StatigelHeadRogue", "C:AdamantiteThrowingAxe", "fam=other,dist=10"),
    ("Statigel rogue set (skewer)", "S:CalamityMod/StatigelHeadRogue", "C:AntlionSkewer", "fam=other,dist=10"),
    ("Tarragon rogue set", "S:CalamityMod/TarragonHeadRogue", "C:AdamantiteThrowingAxe", "fam=other,dist=10"),
]


def emit(fname, rows):
    out = []
    for name, gear, weapon, fam in rows:
        for mname, a, b in MODES:
            flags = ",".join([x for x in [fam, BASE, "g=" + gear, a, b] if x])
            out.append(f"gear|{weapon}|1|600|{flags}")
    weapons = []
    for _, _, w, fam in rows:
        if (w, fam) not in weapons:
            weapons.append((w, fam))
    for w, fam in weapons:
        for mname, a, b in MODES:
            flags = ",".join([x for x in [fam, BASE, "refresh", a, b] if x])
            out.append(f"gear|{w}|1|600|{flags}")
    open(fname, "w").write("\n".join(out) + "\n")
    print(fname, len(out))


emit("gear_thorium.spec", THOR)
emit("gear_cal.spec", CAL)

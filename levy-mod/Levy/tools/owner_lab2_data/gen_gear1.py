#!/usr/bin/env python3
"""Generates gear1.spec: vanilla gear x {npcProj=true, npcProj=false} x {no Player.Update subset, s2 subset}."""
GEAR = [
    # name, gear keys, weapon, fam/dist flags, extra flags
    ("Chlorophyte(CrystalLeaf)", "V:ChlorophyteMask+V:ChlorophytePlateMail+V:ChlorophyteGreaves", "V:Musket", "fam=other,dist=14", ""),
    ("SpectreHood(heal+bolts)", "V:SpectreHood+V:SpectreRobe+V:SpectrePants", "V:AmethystStaff", "fam=other,dist=12", "halflife"),
    ("SpectreMask(bolts)", "V:SpectreMask+V:SpectreRobe+V:SpectrePants", "V:AmethystStaff", "fam=other,dist=12", "halflife"),
    ("Stardust(Guardian)", "V:StardustHelmet+V:StardustBreastplate+V:StardustLeggings", "V:Musket", "fam=other,dist=14", ""),
    ("Frost(set)+Spear", "V:FrostHelmet+V:FrostBreastplate+V:FrostLeggings", "V:Spear", "fam=spear", ""),
    ("Frost(set)+Musket", "V:FrostHelmet+V:FrostBreastplate+V:FrostLeggings", "V:Musket", "fam=other,dist=14", ""),
    ("VolatileGelatin", "V:VolatileGelatin", "V:Musket", "fam=other,dist=14", ""),
    ("BoneGlove", "V:BoneGlove", "V:Musket", "fam=other,dist=14", ""),
    ("MagmaStone+Spear", "V:MagmaStone", "V:Spear", "fam=spear", ""),
    ("MagmaStone+Sword", "V:MagmaStone", "V:CopperBroadsword", "fam=spear,dist=3", ""),
    ("StarCloak(hurt)", "V:StarCloak", "V:Musket", "fam=other,dist=14", "hurt=120"),
]
MODES = [("np_pu0", "", ""), ("real_pu0", "nonpc", ""), ("real_s2", "nonpc", "pu=s2")]
out = []
for name, g, w, fam, extra in GEAR:
    for mname, a, b in MODES:
        flags = ",".join([x for x in [fam, "vslot", "netlock", "g=" + g, extra, a, b] if x])
        out.append(f"gear|{w}|1|600|{flags}")
open("gear1.spec", "w").write("\n".join(out) + "\n")
print(len(out), "scenarios")

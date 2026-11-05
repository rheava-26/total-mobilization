#!/usr/bin/env python3
"""Generates cost40b.spec (old toolchain, Thorium loaded) and cost40cal.spec (Calamity): N=40 soldiers in Chlorophyte + 5 accessories, per Player.Update subset mode, two repeats after a warm-up."""
G = "g=V:ChlorophyteMask+V:ChlorophytePlateMail+V:ChlorophyteGreaves+V:VolatileGelatin+V:MagmaStone+V:BoneGlove+V:StarCloak+V:WormScarf"
base = "fam=other,dist=14,vslot,netlock," + G
out = [f"gear|V:Musket|5|300|{base},nonpc,pu=s2"]
for rep in (1, 2):
    for mode in ("", ",nonpc", ",nonpc,pu=s1", ",nonpc,pu=s2", ",nonpc,pu=s3"):
        out.append(f"gear|V:Musket|40|600|{base}{mode}")
    out.append(f"ctl|none|40|600|vslot,netlock,{G},refresh,nonpc,pu=s2")
open("cost40b.spec", "w").write("\n".join(out) + "\n")
open("cost40cal.spec", "w").write("\n".join([out[0]] + [l for l in out[1:] if "pu=s3" not in l]) + "\n")
print(len(out))

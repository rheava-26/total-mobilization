exec(open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/mk.py").read().split("s=[]")[0])
weak=["W Vanilla/Musket","W Vanilla/Minishark","W Vanilla/BladeofGrass","W Vanilla/LightsBane","W Vanilla/DemonBow","W Vanilla/SpaceGun","W Vanilla/Boomstick","W Vanilla/Starfury","W Vanilla/Muramasa",
"W CalamityMod/WulfrumKnife x2","W CalamityMod/Kylie","W CalamityMod/ShinobiBlade","W CalamityMod/Cinquedea","W CalamityMod/WulfrumProsthesis","W CalamityMod/GildedDagger","W CalamityMod/Archerfish"]
s=[]
s.append(scen("F_weak_then_strong",["SOLDIERS 9"]+weak+ammo+["STAGE"]+W()+["STAGE"]))
# only-pre-HM stock, no armor: what does each class of stock pick (Wulfrum competitiveness)
pre=["W Vanilla/Musket","W Vanilla/BladeofGrass","W Vanilla/LightsBane","W Vanilla/DemonBow","W Vanilla/SpaceGun","W Vanilla/Boomstick","W Vanilla/Muramasa","W Vanilla/Minishark",
"W CalamityMod/WulfrumKnife x3","W CalamityMod/Kylie","W CalamityMod/GildedDagger","W CalamityMod/WulfrumProsthesis","W CalamityMod/WulfrumBlunderbuss","W CalamityMod/BurntSienna","W CalamityMod/UrchinStinger","W CalamityMod/StormSurge","W CalamityMod/Fungicide","W CalamityMod/Mycoroot"]
s.append(scen("G_prehm_only",["SOLDIERS 9"]+pre+ammo+["STAGE"]))
open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/scen2.txt","w").write("\n".join(s))

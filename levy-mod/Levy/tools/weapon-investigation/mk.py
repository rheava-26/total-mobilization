V="Minishark Musket ClockworkAssaultRifle PhoenixBlaster FieryGreatsword Muramasa NightsEdge BladeofGrass BeeKeeper LightsBane Starfury DemonBow MoltenFury HellwingBow SpaceGun WaterBolt DemonScythe BeeGun Flamelash FlowerofFire CobaltSword MythrilSword CobaltRepeater MythrilRepeater CobaltNaginata PalladiumSword Boomstick".split()
C="WulfrumKnife WulfrumBlunderbuss WulfrumProsthesis WulfrumScrewdriver Kylie GildedDagger UrchinStinger ShinobiBlade Cinquedea SludgeSplotch Mycoroot Archerfish Shadethrower Fungicide Eviscerator MycelialClaws TaintedBlade BloodBath AbyssShocker Tradewinds BlackAnurian StormSurge SaharaSlicers TeardropCleaver WindBlade BurntSienna Avalanche SpeedBlaster Needler".split()
T="HitScanner EnergyStormBolter ChampionsTrifectaShot CobaltRevolver CobaltStaff MythrilStaff PalladiumSMG ThoriumWand DarkMageStaff HoneySuckle BackStabber PinkPhaseblade".split()
armor={'melee':"MythrilHelmet MythrilChainmail MythrilGreaves",'ranged':"MythrilHood MythrilChainmail MythrilGreaves",'magic':"MythrilHat MythrilChainmail MythrilGreaves",'summon':"BeeHeadgear BeeBreastplate BeeGreaves"}
def W():
    out=[]
    for n in V: out.append("W Vanilla/"+n)
    for n in C: out.append("W CalamityMod/"+n)
    for n in T: out.append("W ThoriumMod/"+n)
    return out
def scen(name,lines): return "[%s]\n"%name+"\n".join(lines)+"\n"
ammo=["M Vanilla/MusketBall x999","M Vanilla/WoodenArrow x999"]
s=[]
s.append(scen("A_noArmor_ammoYes",["SOLDIERS 9"]+W()+ammo+["STAGE"]))
s.append(scen("B_noArmor_NOammo",["SOLDIERS 9"]+W()+["STAGE"]))
allarm=["A Vanilla/"+n for c in armor.values() for n in c.split()]
s.append(scen("C_classArmors_ammoYes",["SOLDIERS 9"]+W()+ammo+allarm+["STAGE"]))
s.append(scen("D_rogueOnlyStock_then_upgrade",["SOLDIERS 9","W CalamityMod/WulfrumKnife x12","STAGE"]+W()+ammo+["STAGE"]))
s.append(scen("E_meleeArmorOnly",["SOLDIERS 9"]+W()+ammo+["A Vanilla/MythrilHelmet","A Vanilla/MythrilChainmail","A Vanilla/MythrilGreaves","STAGE"]))
open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/scen1.txt","w").write("\n".join(s))

S="/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/"
strong=["TerraBlade","InfluxWaver","TheHorsemansBlade","RazorbladeTyphoon","LaserMachinegun","Xenopopper","Daybreak"]
junk=["WoodenSword","CopperShortsword","IronBroadsword","Muramasa","LightsBane","BladeofGrass","FieryGreatsword","Spear","Trident",
      "Musket","Boomstick","Minishark","Handgun","PhoenixBlaster","SpaceGun","WaterBolt","FlowerofFire","DemonScythe","SlimeStaff","HornetStaff","ImpStaff"]
W=["W Vanilla/"+n for n in strong+junk]
ammo=["M Vanilla/MusketBall x999","M Vanilla/Gel x999","M Vanilla/RocketI x999"]
def scen(name,lines): return "[%s]\n"%name+"\n".join(lines)+"\n"
s=[]
mix9="melee=4 ranged=2 magic=2 summon=1"
mix20="melee=7 ranged=5 magic=4 summon=2 none=2"
for tag,mix in (("9",mix9),("20",mix20)):
    for order in ("interleave","meleelast"):
        s.append(scen("H_eveStock_%s_%s"%(tag,order),["MIX %s %s"%(order,mix)]+W+ammo+["STAGE"]))
# unarmored fluid army (no class armor at all)
s.append(scen("I_eveStock_9_noArmor",["SOLDIERS 9"]+W+ammo+["STAGE"]))
show=["ZombieArm","ChlorophyteClaymore","ChlorophyteSaber","ChlorophytePartisan","ChlorophyteShotbow","ChlorophyteJackhammer","ChlorophyteDrill","ChlorophytePickaxe","ChlorophyteWarhammer","ChlorophyteChainsaw","Drax","Pwnhammer","Hammush","TheAxe","Chlorophyte" ,"Minishark","TerraBlade","Daybreak","Xenopopper","LaserMachinegun","InfluxWaver","RazorbladeTyphoon","TheHorsemansBlade","WoodenSword","CopperShortsword"]
s.append(scen("J_show",["SOLDIERS 0"]+["SHOW Vanilla/"+n for n in show]+["STAGE"]))
open(S+"scen3.txt","w").write("\n".join(s))

S="/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/"
def scen(name,lines): return "[%s]\n"%name+"\n".join(lines)+"\n"
chl=["ChlorophyteSaber","ChlorophyteClaymore","ChlorophytePartisan","ChlorophyteShotbow","ChlorophyteWarhammer","ChlorophyteJackhammer","ChlorophyteDrill","ChlorophytePickaxe","ChlorophyteChainsaw","Drax"]
others=["ZombieArm","IronBroadsword","Musket","SpaceGun"]
armor=["ChlorophyteMask","ChlorophytePlateMail","ChlorophyteGreaves","ChlorophyteHeadgear","ChlorophyteHelmet","ZombieArm"]
s=[]
s.append(scen("K_chlorophyte_vs_zombie_5",["SOLDIERS 5"]+["W Vanilla/"+n for n in chl+others]+["M Vanilla/MusketBall x999","M Vanilla/Arrow x999" if False else "M Vanilla/WoodenArrow x999","STAGE"]))
s.append(scen("K_zombie_only_extra",["SOLDIERS 3","W Vanilla/ZombieArm","W Vanilla/ChlorophyteSaber","W Vanilla/Musket","M Vanilla/MusketBall x999","STAGE"]))
s.append(scen("K_armor_chlorophyte",["SOLDIERS 3","W Vanilla/ZombieArm","W Vanilla/WoodenSword","A Vanilla/ChlorophyteMask","A Vanilla/ChlorophytePlateMail","A Vanilla/ChlorophyteGreaves","A Vanilla/ChlorophyteHeadgear","A Vanilla/CobaltHelmet","A Vanilla/CobaltBreastplate","A Vanilla/CobaltLeggings","A Vanilla/ZombieArm","STAGE"]))
open(S+"scen4.txt","w").write("\n".join(s))

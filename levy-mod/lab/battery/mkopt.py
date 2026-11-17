"""The optimal 34-levy army at Eve's tier (post Golem, Plaguebringer, Ravager, Astrum Deus, Lunatic Cultist, pillars up; no Moon Lord,
no Duke Fishron or Empress drops). Same grade (Levy), same slots as Eve's levies: weapon + sidearm, 3 armor, 2 accessories + wings + boots + hook.
Weapons: the top measured levy DPS at boss defense 50 (specs/battery_dps*.yaml; whips from docs/ARMYSWEEP_DPS.tsv because the
dummy trial cannot measure them), at most 6 levies per weapon so no single weapon's static hit cooldown is shared by the whole army.
Excluded: weapons broken or suspect in levy hands (Apoctosis Array reads 41k DPS, Doomsday Device / Star Shower / Ultimus Cleaver 0,
Wingman half rate, the weak spears) and pure-contact blades (Psycho Knife: it has to touch the boss)."""
import json
from pathlib import Path
H = Path(__file__).resolve().parent
WEAPONS = [  # (weapon, class, ammo, count, measured levy DPS at def 50)
    ('Daybreak@Godly', 'E', '', 6, 1452),
    ('Solar Eruption@Godly', 'E', '', 6, 1407),
    ('Chlorophyte Saber@Legendary', 'E', '', 6, 1149),
    ('Morning Star@Legendary', 'S', '', 6, 979),          # ARMYSWEEP_DPS (idle commander); 0 in the dummy trial, which cannot measure whips
    ('CalamityMod/StarSputter@Unreal', 'R', '', 6, 828),
    ('Vortex Beater@Unreal', 'R', 'CalamityMod/BubonicRound', 4, 808),
]
ARMOR = ['CalamityMod/AstralHelm', 'CalamityMod/AstralBreastplate', 'CalamityMod/AstralLeggings']
MOB = ['CalamityMod/HadarianWings@Menacing', 'CalamityMod/AngelTreads@Menacing']
ACC = {'E': ['Destroyer Emblem@Menacing', 'Celestial Stone@Menacing'], 'R': ['Destroyer Emblem@Menacing', 'Celestial Stone@Menacing'],
       'S': ['Destroyer Emblem@Menacing', 'Papyrus Scarab@Menacing']}
levies = []; i = 0
for w, c, ammo, n, dps in WEAPONS:
    for _ in range(n):
        i += 1
        lv = dict(id=i, cls=c, weapon=w, armor=ARMOR, acc=MOB + ACC[c], hook='Spooky Hook', lc=15, lf=20, mc=9, potions=8, potion='Greater Healing Potion', measuredDps=dps)
        if ammo: lv['ammo'] = ammo
        if c == 'S': lv['side'] = 'Stardust Cell Staff@Ruthless'
        levies.append(lv)
assert len(levies) == 34
eve = json.loads((H / 'eve_army.json').read_text())
army = {'note': __doc__, 'accSlots': 2, 'honey': eve['honey'], 'stations': eve['stations'], 'stationLevel': eve['stationLevel'], 'buffs': eve['buffs'], 'levies': levies}
(H / 'optimal_army.json').write_text(json.dumps(army, indent=1)); print(len(levies), 'levies')

# Fight-tested variant: in the first optimal fights the three melee picks did 0-7% of the damage (Daybreak levies stayed in Follow about
# 55 tiles out; Solar Eruption and Chlorophyte Saber levies all died in the first Ravager slam). Their 18 places go to the next ranged and
# rogue weapons by measured DPS; gear unchanged.
RANGED = [('CalamityMod/StarSputter@Unreal', 'R', '', 6, 828), ('Vortex Beater@Unreal', 'R', 'CalamityMod/BubonicRound', 6, 808),
          ('CalamityMod/AstralBlaster@Unreal', 'R', 'CalamityMod/BubonicRound', 6, 673), ('CalamityMod/RealmRavager@Unreal', 'R', 'CalamityMod/BubonicRound', 5, 618),
          ('Morning Star@Legendary', 'S', '', 6, 979), ('CalamityMod/RegulusRiot@Flawless', 'T', '', 5, 635)]
ACC['T'] = ACC['R']
lv2 = []; i = 0
for w, c, ammo, n, dps in RANGED:
    for _ in range(n):
        i += 1
        lv = dict(id=i, cls=c, weapon=w, armor=ARMOR, acc=MOB + ACC[c], hook='Spooky Hook', lc=15, lf=20, mc=9, potions=8, potion='Greater Healing Potion', measuredDps=dps)
        if ammo: lv['ammo'] = ammo
        if c == 'S': lv['side'] = 'Stardust Cell Staff@Ruthless'
        lv2.append(lv)
assert len(lv2) == 34
(H / 'optimal_ranged_army.json').write_text(json.dumps(dict(army, levies=lv2, note='Fight-tested optimal: ranged and rogue in place of the three melee picks.'), indent=1)); print(len(lv2), 'levies (ranged variant)')

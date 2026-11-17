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
    ('Daybreak@Legendary', 'E', '', 6, 1452),
    ('Solar Eruption@Legendary', 'E', '', 6, 1407),
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

"""PvP lab armies at Eve's CURRENT tier (post-Providence, 2026-11-19 recording prov_1014.levyrec): her 40 levies and two 40-levy optimal armies.
  mkarmies.py PROV_REC.levyrec CALNAMES.json
Her army: battery/mkeve.py's reconstruction (the last lgear baseline of every regular levy), written to eve_prov_army.json.
Optimal: weapons by the measured levy DPS in Levy/Common/Weapons/LevyFit.tsv (the armory's own measured-fit table; endgame sweep), status OK, tier at most Providence
(item_tiers.tsv route <= 19 from a known source: Providence, the Profaned Guardians, the Dragonfolly, Uelibloom, Divine Geode, Moon Lord, lunar and astral recipes; items of unknown
source and anything needing Bloodstone, Reaper Teeth or later are left out), at most 6 levies per weapon. Whips are not in the endgame sweep: Morning Star from ARMYSWEEP_DPS, as in the battery.
  optimal_prov_army.json:        the top measured weapons of any class (18 melee).
  optimal_prov_ranged_army.json: the battery's fight-tested rule (melee did little in boss fights): ranged, rogue and whips only.
Gear for both: Tarragon armour with the class helm (the best set before Polterghast), Elysian Wings, Angel Treads, Lunar Hook, the battery's accessory pairs, 15 crystals and 20 fruit (500 HP),
9 mana crystals, 8 Greater Healing Potions (the same potions as her army in the lab), the battery's stations."""
import json, subprocess, sys
from pathlib import Path
H = Path(__file__).resolve().parent
BAT = H.parent / 'battery'
rec, calnames = sys.argv[1:3]
subprocess.run([sys.executable, '-I', str(BAT / 'mkeve.py'), rec, calnames, str(H / 'eve_prov_army.json')], check=True)
eve = json.loads((H / 'eve_prov_army.json').read_text())
# "Moon Boots" (item 5819 in her world) comes from a mod the lab does not load; it sits in her levies' wings slot, so it is replaced by Solar Wings (the ML wings her other levies wear), same prefix.
SUB = 0
for lv in eve['levies']:
    for i, a in enumerate(lv['acc']):
        if a.split('@')[0] == 'Moon Boots': lv['acc'][i] = 'Solar Wings' + ('@' + a.split('@')[1] if '@' in a else ''); SUB += 1
eve['substituted'] = f'Moon Boots -> Solar Wings on {SUB} levies (Moon Boots is from a mod the lab does not load)'
eve['note'] = "Eve's 40 regular levies (ids 1-40) as worn at the end of her Providence win (2026-11-19 10:14); officers and militia are not levies and are left out. Potions and stations are the battery's stand-ins."
(H / 'eve_prov_army.json').write_text(json.dumps(eve, indent=1))

HELM = {'E': 'CalamityMod/TarragonHeadMelee', 'R': 'CalamityMod/TarragonHeadRanged', 'M': 'CalamityMod/TarragonHeadMagic', 'T': 'CalamityMod/TarragonHeadRogue', 'S': 'CalamityMod/TarragonHeadSummon'}
BODY = ['CalamityMod/TarragonBreastplate', 'CalamityMod/TarragonLeggings']
MOB = ['CalamityMod/ElysianWings@Menacing', 'CalamityMod/AngelTreads@Menacing']
ACC = {c: ['Destroyer Emblem@Menacing', 'Celestial Stone@Menacing'] for c in 'ERMT'}
ACC['S'] = ['Destroyer Emblem@Menacing', 'Papyrus Scarab@Menacing']

ALL = [  # (weapon, class, ammo, count, LevyFit levy_dps)
    ('CalamityMod/MoltenAmputator@Flawless', 'T', '', 6, 2991),
    ('CalamityMod/Lightspeed@Godly', 'E', '', 6, 2894),
    ('CalamityMod/ArkoftheElements@Godly', 'E', '', 6, 2893),
    ('CalamityMod/GalactusBlade@Legendary', 'E', '', 6, 2737),
    ('CalamityMod/RougeSlash@Mythical', 'M', '', 6, 2666),
    ('CalamityMod/PristineFury@Unreal', 'R', '', 6, 2528),
    ('Morning Star@Legendary', 'S', '', 4, 979),
]
RANGED = [
    ('CalamityMod/MoltenAmputator@Flawless', 'T', '', 6, 2991),
    ('CalamityMod/PristineFury@Unreal', 'R', '', 6, 2528),
    ('CalamityMod/Spyker@Unreal', 'R', 'Luminite Bullet', 6, 2328),
    ('CalamityMod/TelluricGlare@Unreal', 'R', 'CalamityMod/SproutingArrow', 6, 2276),
    ('CalamityMod/ReboundingRainbow@Flawless', 'T', '', 5, 2389),
    ('CalamityMod/PlanetaryAnnihilation@Unreal', 'R', 'CalamityMod/SproutingArrow', 5, 2032),
    ('Morning Star@Legendary', 'S', '', 6, 979),
]

def army(rows, note):
    levies = []
    for w, c, ammo, n, dps in rows:
        for _ in range(n):
            lv = dict(id=len(levies) + 1, cls=c, weapon=w, armor=[HELM[c]] + BODY, acc=MOB + ACC[c], hook='Lunar Hook', lc=15, lf=20, mc=9, potions=8, potion='Greater Healing Potion', measuredDps=dps)
            if ammo: lv['ammo'] = ammo
            if c == 'S': lv['side'] = 'Stardust Cell Staff@Ruthless'
            levies.append(lv)
    assert len(levies) == 40, len(levies)
    return {'note': note, 'accSlots': 2, 'honey': eve['honey'], 'stations': eve['stations'], 'stationLevel': eve['stationLevel'], 'buffs': eve['buffs'], 'levies': levies}

(H / 'optimal_prov_army.json').write_text(json.dumps(army(ALL, 'Optimal 40-levy army at the post-Providence tier: top measured levy DPS (LevyFit.tsv), any class.'), indent=1))
(H / 'optimal_prov_ranged_army.json').write_text(json.dumps(army(RANGED, 'Optimal 40-levy army at the post-Providence tier, fight-tested rule: ranged, rogue and whips only.'), indent=1))
print('eve', len(eve['levies']), 'optimal 40 + 40')

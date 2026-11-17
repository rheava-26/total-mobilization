"""Writes the per-weapon DPS trial spec (tipsy lab, Calamity world, def=50 boss-size dummy, 20 tiles, still)."""
import json, sys
cal = json.load(open('/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/battery_work/calnames.json'))
def n(dn):
    return 'CalamityMod/' + cal[dn] if dn in cal else dn
BUL, ARR = 'CalamityMod/BubonicRound', 'CalamityMod/CinderArrow'
W = {
 'E': ['Terra Blade', 'Ark of the Ancients', 'Virulence', 'Ultimus Cleaver', 'Solar Eruption', 'Daybreak', 'Psycho Knife', 'Chlorophyte Saber', 'Flying Dragon',
       'Hellfire Flamberge', 'Aegis Blade', 'The Microwave', 'The Eye of Cthulhu', 'Kraken', 'Vampire Knives', 'Grand Guardian', 'Tenebreus Tides', 'Influx Waver', 'Death Sickle',
       'Galvanizing Glaive', 'Astral Scythe', 'Christmas Tree Sword', 'Botanic Piercer', 'Vulcanite Lance', 'Titan Arm', 'Forsaken Saber'],
 'R': [('Star Sputter', None), ('Vortex Beater', BUL), ('Chain Gun', BUL), ('Astral Blaster', BUL), ('Xenopopper', BUL), ('Aurora Blazer', 'Gel'), ('Candy Corn Rifle', 'Candy Corn'),
       ('Tactical Shotgun', BUL), ('Malevolence', ARR), ('Realm Ravager', BUL), ('Stake Launcher', 'Stake'), ('Vernal Bolter', ARR), ("Havoc's Breath", 'Gel'), ('Sniper Rifle', BUL),
       ('Elf Melter', 'Gel'), ('Megalodon', BUL), ('Plague Tainted SMG', BUL), ('Arietes 41', BUL), ('Electrosphere Launcher', 'Rocket III'), ('Nullification Pistol', None),
       ('Arc Nova Diffuser', None), ("Dead Sun's Wind", 'Gel'), ('Blossom Flux', ARR), ('Marksman Bow', ARR), ('Venus Magnum', BUL), ('Aerial Bane', ARR)],
 'M': ['Astral Staff', 'Nebula Arcanum', 'Nebula Blaze', 'Infernal Rift', 'Charged Blaster Cannon', 'Everglade Spray', 'Laser Machinegun', 'Heat Ray', 'Leaf Blower',
       'Blizzard Staff', 'Apoctosis Array', 'Apathanull', 'Shadowbeam Staff', 'Staff of Earth', 'Razorpine', 'Star Shower', 'Wingman', 'Bat Scepter', 'Spectre Staff', 'Magnet Sphere'],
 'S': ['Starspawn Helix Staff', 'Stardust Cell Staff', 'Stardust Dragon Staff', 'Morning Star', 'Dark Harvest', 'Deadly Sphere Staff', 'Pygmy Staff', 'Xeno Staff', 'Raven Staff',
       'Desert Tiger Staff', 'Sand Sharknado Staff', 'Daedalus Golem Staff', 'Abandoned Slime Staff', 'Pulse Turret Remote', 'Dreadmine Staff'],
 'T': ['Cranium Smasher', 'Corpus Avertor', 'Shard of Antumbra', 'Stellar Knife', 'Apoctolith', 'Terror Talons', 'Doomsday Device', 'Regulus Riot', 'The Syringe',
       'Star of Destruction', 'Plaguenade', 'Epidemic Shredder', 'Defective Sphere', 'Radiant Star'],
}
AMMO_B = [BUL, 'CalamityMod/HyperiusBullet', 'Venom Bullet', 'Ichor Bullet', 'Chlorophyte Bullet', 'Crystal Bullet', 'CalamityMod/HallowPointRound']
AMMO_A = [ARR, 'CalamityMod/VeriumBolt', 'CalamityMod/SproutingArrow', 'Holy Arrow', 'Ichor Arrow', 'Venom Arrow', 'Chlorophyte Arrow']
out = ['name: battery_dps', 'mods: calamity', 'arena: a3', 'seeds: 3', 'tipsy:', '  warm: 600', '  win: 1200', '  dist: [20]', '  motion: [static]', '  size: [boss]',
       '  arms: {d50: {}}', '  every: {def: 50}', '  weapons:']
miss = []
for c, ws in W.items():
    for w in ws:
        dn, am = (w if isinstance(w, tuple) else (w, None))
        nm = n(dn)
        line = f'    - {{name: "{nm}", cls: {c}, kind: "{dn}"' + (f', ammo: "{am}"' if am else '') + (', every: {pin: 0}' if c == 'E' else '') + '}'
        out.append(line)
for am in AMMO_B: out.append(f'    - {{name: "Chain Gun", cls: R, kind: "ammo Chain Gun", ammo: "{am}"}}')
for am in AMMO_A: out.append(f'    - {{name: "CalamityMod/Malevolence", cls: R, kind: "ammo Malevolence", ammo: "{am}"}}')
open(sys.argv[1], 'w').write('\n'.join(out) + '\n')
print(len(out) - 13, 'cells')

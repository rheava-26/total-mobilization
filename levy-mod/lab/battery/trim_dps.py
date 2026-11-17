"""Trims specs/battery_dps.yaml to the short list re-run with one seed (specs/battery_dps2.yaml): the first two-seed run showed seeds agree within 1-2%."""
import re
from pathlib import Path
H = Path(__file__).resolve().parent
keep_disp = set("""Kraken|The Microwave|Psycho Knife|Chlorophyte Saber|Vampire Knives|Grand Guardian|Tenebreus Tides|Star Sputter|Vortex Beater|Chain Gun|Astral Blaster|Xenopopper|Aurora Blazer|Tactical Shotgun|Malevolence|Stake Launcher|Sniper Rifle|Arc Nova Diffuser|Nullification Pistol|Candy Corn Rifle|Realm Ravager|Astral Staff|Nebula Arcanum|Nebula Blaze|Infernal Rift|Charged Blaster Cannon|Laser Machinegun|Heat Ray|Apathanull|Apoctosis Array|Wingman|Star Shower|Razorpine|Starspawn Helix Staff|Stardust Cell Staff|Stardust Dragon Staff|Morning Star|Dark Harvest|Deadly Sphere Staff|Xeno Staff|Raven Staff|Cranium Smasher|Corpus Avertor|Shard of Antumbra|Stellar Knife|Doomsday Device|Regulus Riot|The Syringe""".split('|'))
keep_ammo = {'CalamityMod/BubonicRound', 'CalamityMod/HyperiusBullet', 'Venom Bullet', 'Chlorophyte Bullet', 'CalamityMod/CinderArrow', 'CalamityMod/VeriumBolt', 'CalamityMod/SproutingArrow', 'Holy Arrow'}
out = []
for line in open(H / 'specs/battery_dps.yaml'):
    if line.startswith('    - '):
        k = re.search(r'kind: "([^"]+)"', line).group(1)
        if k.startswith('ammo '):
            if re.search(r'ammo: "([^"]+)"', line).group(1) not in keep_ammo: continue
        elif k not in keep_disp: continue
    out.append(line.replace('name: battery_dps', 'name: battery_dps2').replace('seeds: 2', 'seeds: 1'))
(H / 'specs/battery_dps2.yaml').write_text(''.join(out))
print(sum(1 for l in out if l.startswith('    - ')), 'cells')

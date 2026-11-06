"""Contact sheets of the vanilla study sample (70+ sprites: weapons, enemies, accessories, furniture, stations).
Writes art4/study_<k>.png (4x, labelled)."""
import os
import sys
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from palette import load

OUT = '/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/art4'
SETS = {
    'weapons': ['Muramasa', 'Blade_of_Grass', 'Starfury', 'Space_Gun', 'Megashark', 'Onyx_Blaster', 'Flintlock_Pistol', 'Musket', 'Boomstick',
                'Shotgun', 'Aqua_Scepter', 'Vilethorn', 'Crimson_Rod', 'Cobalt_Sword', 'Mythril_Sword', 'Ice_Blade', 'Ballista_Staff',
                'Flameburst_Staff', 'Trident', 'Cannon', 'Rocket_Launcher', 'Phoenix_Blaster', 'Sniper_Rifle', 'Tactical_Shotgun'],
    'gear': ['Cloud_in_a_Bottle', 'Hermes_Boots', 'Shield_of_Cthulhu', 'Cobalt_Shield', 'Obsidian_Shield', 'Ankh_Charm', 'Band_of_Regeneration',
             'Magic_Quiver', "Philosopher's_Stone", 'Ice_Skates', 'Magic_Mirror', 'Cell_Phone', 'Radar', 'Metal_Detector', 'Gold_Watch',
             'Iron_Helmet', 'Iron_Chainmail', 'Bomb', 'Dynamite', 'Healing_Potion', 'Mechanical_Eye', 'Mechanical_Skull', 'Barrel', 'Keg'],
    'enemies': ['Blue_Slime', 'Zombie', 'Demon_Eye', 'Eye_of_Cthulhu', 'Skeleton', 'Goblin_Scout', 'Harpy', 'Bee', 'Hornet', 'Mimic'],
    'furniture': ['Gold_Chest', 'Dresser', 'Bathtub', 'Grandfather_Clock', 'Toilet', 'Adamantite_Forge', 'Piano', 'Chest', 'Candelabra', 'Anvil',
                  'Furnace', 'Hellforge', 'Mythril_Anvil', 'Bookcase', 'Loom', 'Sawmill', "Tinkerer's_Workshop", 'Weapon_Rack', 'Mannequin',
                  'Work_Bench', 'Alchemy_Table', 'Crystal_Ball', 'Safe', 'Piggy_Bank', 'Wooden_Table', 'Wooden_Chair', 'Bed_(placed)'],
}


def main():
    for k, names in SETS.items():
        ims = []
        for n in names:
            try:
                ims.append((n, load(n)))
            except Exception:
                pass
        sc = 4
        cell = max(max(i.width, i.height) for _, i in ims) * sc + 10
        cols = 2400 // cell
        rows = (len(ims) + cols - 1) // cols
        sheet = Image.new('RGBA', (cols * cell, rows * (cell + 12)), (66, 72, 84, 255))
        d = ImageDraw.Draw(sheet)
        for i, (n, im) in enumerate(ims):
            x, y = (i % cols) * cell, (i // cols) * (cell + 12)
            sheet.alpha_composite(im.resize((im.width * sc, im.height * sc), Image.NEAREST), (x + 5, y + 2))
            d.text((x + 4, y + cell - 4), n[:20], fill=(220, 220, 220, 255))
        sheet.save('%s/study_%s.png' % (OUT, k))
        print(k, len(ims), sheet.size)


main()

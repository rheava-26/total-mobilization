"""Per-item 8x comparison sheets + overview. Run after gear.py, staves_radio.py, approved_potions.py."""
import os
from lib3 import *

ITEMS = [
    ('spectator_potion', 'Spectator Potion', ['Builder_Potion', 'Invisibility_Potion', 'Obsidian_Skin_Potion']),
    ('seeker_potion', 'Seeker Potion', ['Magic_Power_Potion', 'Obsidian_Skin_Potion', 'Hunter_Potion']),
    ('gopro_a_cam', 'GoPro (action cam)', ['Radar', 'GPS', 'Cell_Phone', 'Stopwatch']),
    ('gopro_b_collar', 'GoPro collar', ['Stopwatch', 'Fish_Finder', 'Goblin_Tech']),
    ('gopro_carry_icon', 'GoPro carry indicator', ['Dog_Whistle']),
    ('static_camera_item', 'Static Camera item', ['Radar', 'GPS', 'Tally_Counter', 'Fish_Finder']),
    ('static_camera_tile', 'Static Camera tile 2x3', ['Alchemy_Table', 'Bookcase', 'Imbuing_Station']),
    ('guide_to_autonomy', 'Guide to Autonomy', ['Spell_Tome', 'Book_of_Skulls', 'Water_Bolt']),
    ('sentry_rod', 'Levy Sentry Rod', ['Flameburst_Rod', 'Lightning_Aura_Rod', 'Ballista_Rod', 'Explosive_Trap_Rod']),
    ('sentry_cane', 'Levy Sentry Cane', ['Flameburst_Cane', 'Lightning_Aura_Cane', 'Ballista_Cane', 'Explosive_Trap_Cane']),
    ('sentry_staff', 'Levy Sentry Staff', ['Flameburst_Staff', 'Lightning_Aura_Staff', 'Ballista_Staff', 'Explosive_Trap_Staff']),
    ('command_radio', 'Command Radio (28x26)', ['Weather_Radio', 'Cell_Phone', 'Tally_Counter']),
]

ents = []
for key, title, refs in ITEMS:
    im = Image.open(os.path.join(PNG, key + '.png')).convert('RGBA')
    compare(os.path.join(OUT, 'cmp_' + key + '.png'), title + ' vs vanilla, 8x', [(title + ' %dx%d' % im.size, im)], refs)
    ents.append((key, im))

# overview: everything true-scale-doubled-by-6
sc = 6
cols = 4
cw = max(i.width for _, i in ents) * sc
ch = max(i.height for _, i in ents) * sc
rows = (len(ents) + cols - 1) // cols
pad = 14
out = Image.new('RGB', (cols * (cw + pad) + pad, 44 + rows * (ch + pad + 24) + pad), (30, 33, 60))
d = ImageDraw.Draw(out)
d.text((pad, 8), 'Levy 2.0 art pass 3 (6x)', fill=(240, 240, 255), font=font(22))
for k, (n, i) in enumerate(ents):
    r, c = divmod(k, cols)
    x, y = pad + c * (cw + pad), 44 + pad + r * (ch + pad + 24)
    d.rectangle([x, y, x + cw - 1, y + ch - 1], fill=BG, outline=(110, 120, 190))
    b = i.resize((i.width * sc, i.height * sc), Image.NEAREST)
    out.paste(b, (x + (cw - b.width) // 2, y + (ch - b.height) // 2), b)
    d.text((x + 2, y + ch + 3), '%s %dx%d' % (n, i.width, i.height), fill=(225, 228, 250), font=font(14))
out.save(os.path.join(OUT, 'overview.png'))

"""Builds every Levy 2.0 concept sprite.
usage: python3 build.py <out_dir> [refs_dir]
  <out_dir>/png/...      true-scale sprites (also copied into tools/art2/png by the caller)
  <out_dir>/preview8x/   8x nearest previews
  <out_dir>/sheets/      one contact sheet per item + overview + potion comparison rows
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image
from lib import *
import potions, gear, stations, staves

out = sys.argv[1]
refs = sys.argv[2] if len(sys.argv) > 2 else None
for d in ('png', 'preview8x', 'sheets'):
    os.makedirs(os.path.join(out, d), exist_ok=True)

GROUPS = {}
GROUPS.update(potions.POTIONS)
GROUPS.update(gear.GEAR)
GROUPS.update(stations.STATIONS)
GROUPS['sentry_staves'] = [(k.replace('sentry_', '') + ' ' + l, fn)
                           for k, vs in staves.STAVES.items() for l, fn in vs]

TITLES = {
    'spectator': 'Spectator Potion', 'seeker': 'Seeker Potion', 'gopro': 'GoPro (item)',
    'gopro_indicator': 'GoPro "levy is carrying it" indicator (16-18px)', 'radio': 'Radio',
    'static_camera_item': 'Static Camera (item)', 'static_camera_tile': 'Static Camera (tile 2x3)',
    'guide_to_autonomy': 'Guide to Autonomy', 'armory_heart': 'Armory Heart (tile)',
    'bulk_factory': 'Bulk Factory station (tile 3x2)', 'bulk_potion': 'Bulk Potion station (tile 3x3)',
    'sentry_staves': "Old One's Army levy sentry staves: Rod / Cane / Staff, 3 variants",
}

sheets = []
for key, vs in GROUPS.items():
    ents = []
    for i, (label, fn) in enumerate(vs):
        sp = fn()
        name = '%s_%s' % (key, 'v%d' % (i + 1) if key != 'sentry_staves' else label.replace(' ', '_'))
        sp.im.save(os.path.join(out, 'png', name + '.png'))
        sp.im.resize((sp.w * 8, sp.h * 8), Image.NEAREST).save(os.path.join(out, 'preview8x', name + '.png'))
        ents.append((label, sp.im))
    scale = 8 if key != 'sentry_staves' else 6
    cols = 3
    sh = sheet(ents, scale, title=TITLES.get(key, key), cols=cols)
    sh.save(os.path.join(out, 'sheets', 'sheet_' + key + '.png'))
    sheets.append(sh)

# overview: stack the item sheets, scaled down to a common width
W = max(s.width for s in sheets)
H = sum(s.height for s in sheets)
ov = Image.new('RGB', (W, H), (30, 33, 60))
y = 0
for s in sheets:
    ov.paste(s, (0, y)); y += s.height
ov.save(os.path.join(out, 'sheets', 'overview_all.png'))

# ---------- comparison row vs vanilla (cropped from Eve's recipe sheet at matched scale)
if refs:
    ref = Image.open(os.path.join(refs, 'potion_recipes.jpg')).convert('RGB')
    S = 1.06  # displayed -> original coords

    def crop(box, k=1.12):
        b = [int(v * S) for v in box]
        c = ref.crop(b)
        # make the white page background transparent so it sits on the slot colour
        c = c.convert('RGBA')
        px = c.load()
        for yy in range(c.height):
            for xx in range(c.width):
                r, g, bb, _ = px[xx, yy]
                if r > 228 and g > 228 and bb > 228:
                    px[xx, yy] = (0, 0, 0, 0)
        return c.resize((int(c.width * k), int(c.height * k)), Image.LANCZOS)
    van_sq = [('vanilla Builder', crop((1048, 510, 1192, 722))),
              ('vanilla grey box', crop((1225, 510, 1365, 722))),
              ('vanilla gold box', crop((362, 510, 502, 722)))]
    van_tr = [('vanilla purple tri', crop((8, 510, 148, 722))),
              ('vanilla cyan tri', crop((372, 1020, 506, 1215))),
              ('vanilla blue tri', crop((1748, 520, 1888, 722)))]

    def row(vans, mine, title, name):
        ents = [(l, i) for l, i in vans] + [(l, fn().im.resize((fn().w * 8, fn().h * 8), Image.NEAREST)) for l, fn in mine]
        ents2 = []
        for l, im in ents:
            ents2.append((l, im))
        # sheet() upscales by `scale`; vanilla crops are already at 8x-equivalent, so pass scale=1
        s = sheet(ents2, 1, title=title, cols=len(ents2), cell=(300, 340))
        s.save(os.path.join(out, 'sheets', name))
    row(van_sq, potions.POTIONS['spectator'], 'Spectator Potion vs vanilla square-box potions (same scale)', 'compare_spectator.png')
    row(van_tr, potions.POTIONS['seeker'], 'Seeker Potion vs vanilla triangle potions (same scale)', 'compare_seeker.png')
print('ok', len(sheets), 'sheets')

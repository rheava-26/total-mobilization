"""usage: python3 potions_v2_compare.py <refs_dir> <out_compare.png> [out_png_dir]
8x comparison: vanilla Builder / purple triangle (cropped from Eve's sheet at matching scale) vs the new variants.
Vanilla crops are box-resampled to their true 20x30 size, so every vanilla 2x2 cell lands on exactly 2x2 pixels."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image
from lib import *
import potions

refs, out = sys.argv[1], sys.argv[2]
pngdir = sys.argv[3] if len(sys.argv) > 3 else None
ref = Image.open(os.path.join(refs, 'potion_recipes.jpg')).convert('RGB')
S = 1.06


def vanilla(x, y, ox, oy):
    """(x,y) displayed coords of the crop, (ox,oy) body origin in the 4x probe crop -> 20x30 sprite on a 24x30 canvas."""
    x0 = x * S + ox / 4
    y0 = y * S + oy / 4
    c = ref.crop((int(round(x0)), int(round(y0)), int(round(x0 + 144)), int(round(y0 + 216)))).resize((20, 30), Image.BOX).convert('RGBA')
    px = c.load()
    for yy in range(30):
        for xx in range(20):
            r, g, b, _ = px[xx, yy]
            if r > 215 and g > 215 and b > 215:
                px[xx, yy] = (0, 0, 0, 0)
    canvas = Image.new('RGBA', (24, 30), (0, 0, 0, 0))
    canvas.paste(c, (2, 0))
    return canvas


van_builder = vanilla(1040, 505, 52, 30)
van_tri = vanilla(0, 505, 40, 35)
P = potions.POTIONS
ents = [('vanilla Builder', van_builder)] + [('Spectator ' + l, fn().im) for l, fn in P['spectator']] + \
       [('vanilla purple tri', van_tri)] + [('Seeker ' + l, fn().im) for l, fn in P['seeker']]
sheet(ents, 8, title='Levy 2.0 potions v2 vs vanilla (8x, same scale)', cols=3, cell=(24 * 8 + 8, 30 * 8 + 8)).save(out)
if pngdir:
    for k, vs in P.items():
        for i, (l, fn) in enumerate(vs):
            fn().im.save(os.path.join(pngdir, '%s_v%d.png' % (k, i + 1)))
print('ok')

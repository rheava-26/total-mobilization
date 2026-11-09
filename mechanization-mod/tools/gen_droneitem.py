#!/usr/bin/env python3
"""Draws Content/Items/DroneItem.png (28x24, a folded combat drone) and a 4x preview.

Vanilla item art rules: a 2x2 pixel grid (the picture is drawn on a 14x12 grid, every cell two pixels square),
pixel noise in the fills (a few cells nudged one step up or down their colour ramp), and a dark outline in the
item's own darkest shade. Steel greys like DroneNPC.png, with the amber status light of the live drone.
Usage: gen_droneitem.py <out item png> <out preview png>
"""
import random
import sys
from PIL import Image

PAL = {
    'O': (26, 28, 34),      # outline: the darkest shade in the picture
    'd': (50, 53, 62),
    'm': (84, 87, 98),
    'l': (122, 126, 138),
    'h': (168, 172, 184),
    'A': (176, 104, 18),
    'a': (238, 166, 34),
    'b': (52, 104, 186),    # lens
    'g': (150, 200, 250),    # lens glint
}
RAMP = ['d', 'm', 'l', 'h']   # steel ramp, dark to light (noise moves a cell one step along it)

ART = [
    "..............",
    "........OOOO..",
    ".......OhhllO.",
    "..OOO..OOOOOO.",
    ".OdmdO.OhllmdO",
    "OdmlmdOOlmmmdO",
    "OmldmOOOmmmddO",
    "OdmmmdOOmmmdaO",
    ".OddmOOddmmAaO.",
    "..OOO.OOddddOO",
    ".......OOOOOO.",
    "..............",
]
# the rows above are written loosely; pad or trim every row to the 14-cell grid
ART = [(r + '.' * 14)[:14] for r in ART]


def build(seed=7):
    rnd = random.Random(seed)
    cells = [list(r) for r in ART]
    for y, row in enumerate(cells):
        for x, c in enumerate(row):
            if c in RAMP and rnd.random() < 0.2:                       # pixel noise
                i = RAMP.index(c) + rnd.choice((-1, 1))
                row[x] = RAMP[max(0, min(len(RAMP) - 1, i))]
    img = Image.new('RGBA', (28, 24), (0, 0, 0, 0))
    for y, row in enumerate(cells):
        for x, c in enumerate(row):
            if c == '.':
                continue
            for dy in range(2):
                for dx in range(2):
                    img.putpixel((x * 2 + dx, y * 2 + dy), PAL[c] + (255,))
    return img


if __name__ == '__main__':
    out, preview = sys.argv[1], sys.argv[2]
    im = build()
    im.save(out)
    bg = Image.new('RGBA', im.size, (74, 92, 120, 255))
    bg.alpha_composite(im)
    bg.resize((im.width * 4, im.height * 4), Image.NEAREST).save(preview)
    print('wrote', out, preview, im.size)

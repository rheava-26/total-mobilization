#!/usr/bin/env python3
"""Barracks Ale / Black Stout / Spiced Rum / Meteor Beacon sprites.
Vanilla method: art drawn on a 2x2 grid (every art pixel = 2x2 real pixels), ramp-shaded, with seeded +/-1 ramp-level noise inside shapes."""
import random, sys, os
from PIL import Image
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Levy', 'Content', 'Items')
def hx(s): return tuple(int(s[i:i+2], 16) for i in (0, 2, 4)) + (255,)
def render(rows, pal, shade, seed, noise=0.4):
    rnd = random.Random(seed); h = len(rows); w = len(rows[0]); out = {}
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch == '.': continue
            if ch == 'O': out[(x, y)] = hx(pal['O']); continue
            ramp, lv = shade(ch, x, y)
            n = len(pal[ramp])
            if rnd.random() < noise: lv += rnd.choice((-1, -1, 1, 1, 2 if rnd.random() < .15 else 1))
            lv = max(0, min(n - 1, lv)); out[(x, y)] = hx(pal[ramp][lv])
    im = Image.new('RGBA', (w * 2, h * 2), (0, 0, 0, 0))
    for (x, y), c in out.items():
        for dx in (0, 1):
            for dy in (0, 1): im.putpixel((x * 2 + dx, y * 2 + dy), c)
    return im

# ---- tankards 9x9 art ----
MUG = ["...OOOOO.",
       "..OFFFFFO",
       "..OffFfFO",
       "OOOBBBBBO",
       "OhOBBBBBO",
       "OhOBBBBBO",
       "OOOBBBBBO",
       "..OBBBBBO",
       "...OOOOO."]
def mug_shade(ch, x, y):
    if ch == 'F': return 'foam', 3 if x < 5 else 2
    if ch == 'f': return 'foam', 2 if x < 5 else 1
    if ch == 'h': return 'hand', 1
    col = 3 if x == 3 else 2 if x < 6 else 1 if x < 8 else 0
    if y >= 7: col -= 1
    return 'body', col + (1 if y == 3 else 0)
ALE = dict(O='2e2100', foam=['8a9fb8', 'acc0d7', 'd2dde9', 'f5f7fa'], hand=['3f4a30', '56633c'],
           body=['a87802', 'b88602', 'cd9802', 'e0aa02', 'eabc19', 'f0d058'])
STOUT = dict(O='0b0605', foam=['8c6a3e', 'b08a52', 'd2ae76', 'ecd3a0'], hand=['2a2a2e', '45454c'],
             body=['1a100d', '261812', '33211a', '432d22', '564034', '6a5242'])
# ---- potion 10x15 art ----
POT = ["...OOOO...",
       "...OsSO...",
       "...OsSO...",
       "...OgGO...",
       "..OOLLOO..",
       ".OLLLLLLO.",
       "OLLLLLLLLO",
       "OLLLLLLLLO",
       "OLLLLLLLLO",
       "OLLLLLLLLO",
       "OLLLLLLLLO",
       "OLLLLLLLLO",
       "OLLLLLLLLO",
       "OLLLLLLLLO",
       ".OOOOOOOO."]
GLINT = {(2, 5), (1, 7), (1, 8)}
def pot_shade(ch, x, y):
    if ch in 'sS': return 'stop', (3 if ch == 's' else 1) + (1 if y == 1 else 0)
    if ch in 'gG': return 'glass', 2 if ch == 'g' else 1
    if (x, y) in GLINT: return 'liq', 6
    lv = 4 if x <= 2 else 3 if x <= 4 else 2 if x <= 6 else 1
    if y >= 12: lv -= 1
    if y == 5: lv += 1
    return 'liq', lv
RUM = dict(O='24100c', stop=['5a5f66', '7c828b', '9ca2ab', 'bcc2ca', 'd4d9df'], glass=['6a5a52', '8a766a', 'a89488'],
           liq=['4a180e', '63200f', '7e2c15', '9a3b1c', 'b64f27', 'd06a36', 'f0b090'])
# ---- meteor 12x12 art ----
MET = ["....OOO.....",
       "..OOrrrOO...",
       ".OrrrrrrrOO.",
       ".OrrrrrrrrrO",
       "OOrrrCCrrrrO",
       "OrrrCCCCrrrO",
       "OrrrCCCCCrrO",
       "OOrrrCCCrrO.",
       ".OrrrrrrrrO.",
       ".OOrrrrrrOO.",
       "..OrrrOOrO..",
       "...OOO.OO..."]
def met_shade(ch, x, y):
    if ch == 'C':
        d = abs(x - 5.5) + abs(y - 5.5) * .9
        return 'core', 4 if d < 1.6 else 3 if d < 2.4 else 2 if d < 3.2 else 1
    lv = 4 - int((x * .5 + y * .6) / 2.3)
    if y >= 8: lv -= 1
    return 'rock', lv
METEOR = dict(O='140c10', rock=['3a2a34', '4c3a46', '5f4c58', '75616b', '8c7882'], core=['8a2a10', 'c8480f', 'f07a14', 'ffb52e', 'fff0a0'])
if __name__ == '__main__':
    seed = int(sys.argv[1]) if len(sys.argv) > 1 else 13
    jobs = [('OOA/BarracksAle.png', MUG, mug_shade, ALE, 11), ('OOA/BlackStout.png', MUG, mug_shade, STOUT, 12),
            ('OOA/SpicedRum.png', POT, pot_shade, RUM, seed), ('MeteorBeacon.png', MET, met_shade, METEOR, 14)]
    for p, rows, sh, pal, sd in jobs:
        render(rows, pal, sh, sd).save(os.path.join(ROOT, p))

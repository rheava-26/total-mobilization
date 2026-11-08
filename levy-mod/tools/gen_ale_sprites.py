#!/usr/bin/env python3
"""Barracks Keg / Case of Stout / Rum Cooler / Meteor Beacon sprites (item files keep their old class names).
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

# ---- Barracks Keg: upright oak barrel, 14x15 art ----
KEG = ["....OOOOOO....",
       "..OOLLLLLLOO..",
       ".OLLLLDLLLLLO.",
       ".OHHHHHHHHHHO.",
       "OWWWWWWWWWWWWO",
       "OWWWWWWWWWWWWO",
       "OWWWWWWWWWWWWO",
       "OWWWWWWWWWWWWO",
       "OWWWOBBBBOWWWO",
       "OWWWWOBBOWWWWO",
       "OWWWWWOaOWWWWO",
       "OWWWWWWWWWWWWO",
       ".OHHHHHHHHHHO.",
       ".OWWWWWWWWWWO.",
       "..OOOOOOOOOO.."]
def keg_shade(ch, x, y):
    if ch == 'L': return 'wood', 5 if x < 7 else 4
    if ch == 'H': return 'iron', 4 if x < 4 else 3 if x < 8 else 2 if x < 11 else 1
    if ch == 'B': return 'brass', 4 if y == 8 and x < 7 else 3 if y == 8 else 2
    if ch == 'a': return 'beer', 1 if y == 10 else 2
    if ch == 'D': return 'wood', 0
    lv = 5 if x <= 2 else 4 if x <= 4 else 3 if x <= 7 else 2 if x <= 10 else 1
    if x in (5, 8, 11): lv -= 1   # stave seams
    if y >= 11: lv -= 1
    return 'wood', lv
KEGP = dict(O='1c1008', wood=['2e1a0c', '4a2c12', '633c18', '7e4f22', '9a6530', 'b57d3e', 'cf9a58'],
            iron=['22262c', '38404a', '505b68', '6c7a8a', '8d9eb0', 'b4c4d4'], brass=['5a3e08', '8a6410', 'b88a1c', 'e0b030', 'f8dc6a'], beer=['a87802', 'e0aa02', 'f0d058'])
# ---- Case of Stout: cardboard case with four cans showing, 13x13 art ----
DIG = {'2': ["ccc", "..c", "ccc", "c..", "ccc"], '4': ["c.c", "c.c", "ccc", "..c", "..c"]}
CASE_ROWS = [".OO.OO.OO.OO.",
             "OsSOsSOsSOsSO",
             "OxzOxzOxzOxzO",
             "OxzOxzOxzOxzO",
             "OKKKKKKKKKKKO",
             "OKBBBBBBBBBKO",
             "OKBBBBBBBBBKO",
             "OKBBBBBBBBBKO",
             "OKBBBBBBBBBKO",
             "OKBBBBBBBBBKO",
             "OKBBBBBBBBBKO",
             "OKBBBBBBBBBKO",
             "OKKGGGGGGGKKO",
             "OKKKKKKKKKKKO",
             ".OOOOOOOOOOO."]
CASE = [list(r) for r in CASE_ROWS]
for ox, d in ((3, '2'), (7, '4')):
    for dy, row in enumerate(DIG[d]):
        for dx, c in enumerate(row):
            if c == 'c': CASE[6 + dy][ox + dx] = 'c'
CASE = [''.join(r) for r in CASE]
def case_shade(ch, x, y):
    if ch in 'sS': return 'tin', 4 if ch == 'S' else 3
    if ch == 'G': return 'gold', 3 if x < 6 else 2
    if ch in 'xz': return 'ink', 4 if ch == 'x' else 2
    if ch == 'B': return 'ink', 1 if x < 6 else 0
    if ch == 'c': return 'cream', 2 if x in (3, 7) else 1
    lv = 4 if x <= 2 else 3 if x <= 5 else 2 if x <= 9 else 1
    if y == 3: lv += 1
    if y >= 13: lv -= 1
    return 'card', lv
CASEP = dict(O='160d07', tin=['3a4048', '5a626e', '8a94a2', 'b8c2cc', 'e0e8f0'], card=['5a4020', '7a5a30', '9a7644', 'b8935a', 'd2ae74', 'e6c892'],
             gold=['6a4a10', 'b8861c', 'e8b830', 'f8d868'], ink=['0e0a08', '1a120e', '2a1c14', '3e2a1e', '5a4434', '78604a'], cream=['b09450', 'e6c888', 'f6e4b4'])
# ---- Rum Cooler: red body, white lid, brass latches, 14x13 art ----
COOL = ["....OOOOOO....",
        "...OHHHHHHO...",
        "..OOHOOOOHOO..",
        ".OLLLLLLLLLLO.",
        "OLLLLLLLLLLLLO",
        "OLLLLLLLLLLLLO",
        "OOLBOOOOOOBLOO",
        "ORRRRRRRFRRRRO",
        "ORRRRRRFFFRRRO",
        "ORRRRRFFeFFRRO",
        "ORRRRRFeeeFRRO",
        "ORRRRRRFFFRRRO",
        ".OOOOOOOOOOOO."]
def cool_shade(ch, x, y):
    if ch == 'H': return 'iron', 4 if x < 7 else 2
    if ch == 'B': return 'brass', 3 if x < 7 else 2
    if ch == 'L': return 'lid', (5 if y == 3 else 4 if x < 5 else 3 if x < 9 else 2) - (1 if y == 5 else 0)
    if ch == 'F': return 'fl', 2 if y < 9 else 1
    if ch == 'e': return 'fl', 4 if y < 10 else 3
    lv = 5 if x <= 2 else 4 if x <= 5 else 3 if x <= 9 else 2 if x <= 11 else 1
    if y >= 11: lv -= 1
    if y == 7: lv += 1
    return 'red', lv
COOLP = dict(O='1e0808', iron=['22262c', '38404a', '505b68', '6c7a8a', '8d9eb0'], brass=['5a3e08', '8a6410', 'b88a1c', 'e0b030'],
             lid=['4a5468', '6a7890', '90a0b8', 'b8c8dc', 'dce6f2', 'f6faff'], red=['3a0e10', '561418', '7a1c20', '9c2a2a', 'bc3c36', 'd85a4a', 'ee8466'],
             fl=['b8340c', 'e0561a', 'f58a28', 'ffc040', 'ffe890'])
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
    jobs = [('OOA/BarracksAle.png', KEG, keg_shade, KEGP, seed), ('OOA/BlackStout.png', CASE, case_shade, CASEP, seed + 1),
            ('OOA/SpicedRum.png', COOL, cool_shade, COOLP, seed + 2), ('MeteorBeacon.png', MET, met_shade, METEOR, 14)]
    for p, rows, sh, pal, sd in jobs:
        render(rows, pal, sh, sd).save(os.path.join(ROOT, p))

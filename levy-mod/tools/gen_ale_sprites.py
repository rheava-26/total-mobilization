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

def outline(rows):
    """Adds a one-cell dark outline ('O') around every filled cell of a '.'-padded grid."""
    h = len(rows); w = len(rows[0]); g = [list(r) for r in rows]
    for y in range(h):
        for x in range(w):
            if rows[y][x] != '.': continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and rows[ny][nx] not in '.O': g[y][x] = 'O'; break
    return [''.join(r) for r in g]
# ---- Barracks Keg: oak barrel lying on its side, end cap and tap facing right, 18x15 art ----
def _keg():
    W, H = 19, 15; g = [['.'] * W for _ in range(H)]
    for y in range(1, 12):
        for x in range(1, 12):
            if y in (1, 11) and (x < 3 or x > 10): continue
            g[y][x] = 'W'
    for y in range(1, 12):
        for x in (3, 4, 8, 9):
            if g[y][x] == 'W': g[y][x] = 'H'
    for y in range(1, 12):
        for x in range(9, 16):
            dx = (x - 12.0) / 3.1; dy = (y - 6.0) / 5.4; d = dx * dx + dy * dy
            if d <= 1.0: g[y][x] = 'R' if d > 0.7 else 'E'
    for x in (15, 16, 17):
        g[6][x] = 'B'; g[7][x] = 'B'
    g[8][17] = 'B'; g[9][17] = 'a'
    g[5][16] = 'B'; g[4][16] = 'B'; g[3][16] = 'B'; g[3][15] = 'B'
    for x in (3, 4, 5, 9, 10, 11): g[12][x] = 'C'
    for x in (4, 10): g[13][x] = 'C'
    return outline([''.join(r) for r in g])
KEG = _keg()
def keg_shade(ch, x, y):
    if ch == 'E': return 'wood', 6 if (x + y) % 3 else 5
    if ch == 'R': return 'iron', 4 if y < 6 else 2
    if ch == 'H': return 'iron', 4 if y < 5 else 3 if y < 8 else 2
    if ch == 'B': return 'brass', 4 if y == 6 else 3 if y == 7 else 2
    if ch == 'a': return 'beer', 2
    if ch == 'C': return 'wood', 1
    lv = 5 if y <= 2 else 4 if y <= 4 else 3 if y <= 7 else 2 if y <= 9 else 1
    if y in (4, 7, 9): lv -= 1   # stave seams run along the barrel
    return 'wood', lv
KEGP = dict(O='1c1008', wood=['2e1a0c', '4a2c12', '633c18', '7e4f22', '9a6530', 'b57d3e', 'cf9a58'],
            iron=['22262c', '38404a', '505b68', '6c7a8a', '8d9eb0', 'b4c4d4'], brass=['5a3e08', '8a6410', 'b88a1c', 'e0b030', 'f8dc6a'], beer=['a87802', 'e0aa02', 'f0d058'])
# ---- Case of Stout: shallow cardboard crate lying wide, four bottles standing out of it, 17x12 art ----
def _case():
    W, H = 19, 13; g = [['.'] * W for _ in range(H)]
    for i in range(4):
        x0 = i * 5
        g[0][x0 + 1] = 'g'; g[0][x0 + 2] = 'g'                 # cap
        for y in (1, 2): g[y][x0 + 1] = 'n'; g[y][x0 + 2] = 'n'  # neck
        g[3][x0 + 1] = 'v'; g[3][x0 + 2] = 'v'
        for y in range(4, 9):
            for x in range(x0, x0 + 4): g[y][x] = 'v'
        for y in (6, 7):
            for x in (x0 + 1, x0 + 2): g[y][x] = 'l'            # label
    for y in range(9, 13):
        for x in range(0, 19): g[y][x] = 'K'
    for x in range(0, 19): g[9][x] = 'k'          # lit rim
    for x in range(0, 19): g[11][x] = 'B'         # black band
    return outline([''.join(r) for r in g])
CASE = _case()
def case_shade(ch, x, y):
    if ch == 'g': return 'gold', 3 if x % 5 == 1 else 2
    if ch == 'n': return 'glass', 3 if x % 5 == 1 else 2
    if ch == 'v':
        c = x % 5
        return 'glass', 4 if c == 0 else 3 if c == 1 else 2 if c == 2 else 1
    if ch == 'l': return 'cream', 2 if y == 6 and x % 5 == 1 else 1
    if ch == 'k': return 'card', 5
    if ch == 'B': return 'ink', 2 if x % 5 else 3
    lv = 4 if y == 10 else 3
    if y == 12: lv -= 1
    return 'card', lv
CASEP = dict(O='160d07', gold=['6a4a10', 'b8861c', 'e8b830', 'f8d868'], glass=['2a1a12', '48301f', '6e4a2e', '94683c', 'bc8a4c'],
             card=['5a4020', '7a5a30', '9a7644', 'b8935a', 'd2ae74', 'e6c892'], ink=['0e0a08', '1a120e', '2a1c14', '3e2a1e'], cream=['b09450', 'e6c888', 'f6e4b4'])
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

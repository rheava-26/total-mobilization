"""Provisions art pass v2: drawn on a half-resolution cell grid (every vanilla food/drink is drawn in 2x2 "big pixels"), rendered strict 2x2.
Cells are two-character tokens: ramp letter + shade digit (0 = darkest ... top = highlight), '..' = clear.
Outlines are automatic: a 1-cell ring around each sprite coloured with the darkest shade of the region it hugs, as vanilla does
(brown round bread, dark steel round a plate, dark blue round glass). Some ramps get deterministic pixel noise for texture.
usage: python3 -I provisions.py   -> writes the 8 item PNGs, provisions_preview_4x.png and provisions_vs_vanilla_4x.png."""
import os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
MOD = os.path.abspath(os.path.join(HERE, '..', '..', 'Levy'))
OUT = os.path.join(MOD, 'Content', 'Items', 'Provisions')
REF = os.path.join(MOD, 'tools', 'art8', 'vanilla_ref')
PREVIEW = os.path.join(HERE, 'provisions_preview_4x.png')
COMPARE = os.path.join(HERE, 'provisions_vs_vanilla_4x.png')
os.makedirs(OUT, exist_ok=True)

# Muted vanilla-ish ramps, darkest first (hues sampled from the vanilla Cream Soda, Bottled Water, Burger, Fruit Salad, Roasted Duck, Sake sprites).
RAMPS = {
    'b': ['142840', '093dbf', '1e54dc', '336bf9', '7197fb', 'c8f6fe'],   # soda blue
    's': ['1c303d', '40515c', '77858e', 'a1a9b0', 'bec7ce', 'ecebf9'],   # steel / plate
    'c': ['3a2309', '7a4f22', 'b48449', 'cfa972', 'e6ce94', 'fbe5bb'],   # cardboard
    'r': ['3e1418', '7b0021', 'b13827', 'd85d3d', 'ec8c78', 'ffb09a'],   # box red
    'y': ['551d00', '8f563b', 'c58855', 'e0a24a', 'ffb459', 'ffdb94'],   # bread / fried gold
    'w': ['3a2309', '663931', '84471f', 'b48449', 'cfa972'],             # wicker
    'i': ['142840', '3e7994', '57adbd', '8fd8e8', 'c8f6fe', 'f7fafc'],   # frost / ice-cold glass
    'g': ['0f4230', '274e15', '0f8530', '649c3a', '9bbf4a'],             # greens
    'a': ['4f0027', '862d2d', 'b91b2a', 'ea363e', 'ff866a'],             # apple
    'n': ['5a3a0a', 'a87a0a', 'ca9307', 'e4c620', 'ffe67a'],             # banana / lemon / label yellow
    'p': ['2a0a3a', '50063a', '803a9a', 'b068c0', 'd8a0e8'],             # grape
    'o': ['833403', 'b4500a', 'e17e17', 'f7b234', 'ffd154'],             # orange
    'd': ['4a1f1a', '803131', 'aa5139', 'c77854', 'e4986b'],             # roast
    'f': ['423127', '7f6951', '9e876e', 'c7a681', 'e5e0ba'],             # fish skin / bone
}
NOISY = set('cywdf')


def hx(s):
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


def rows_to_grid(rows):
    g = {}
    for y, r in enumerate(rows):
        for x in range(len(r) // 2):
            t = r[x * 2:x * 2 + 2]
            if t != '..':
                g[(x, y)] = (t[0], min(int(t[1]), len(RAMPS[t[0]]) - 1))
    return g


class Cv:
    def __init__(self):
        self.g = {}

    def put(self, x, y, ramp, sh):
        self.g[(x, y)] = (ramp, max(0, min(len(RAMPS[ramp]) - 1, sh)))

    def stamp(self, rows, x0, y0):
        for (x, y), v in rows_to_grid(rows).items():
            self.g[(x0 + x, y0 + y)] = v

    def ellipse(self, cx, cy, rx, ry, ramp, lo, hi, light=(0.55, 0.75)):
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                nx, ny = (x - cx) / (rx + .35), (y - cy) / (ry + .35)
                if nx * nx + ny * ny <= 1.0:
                    l = .5 - .5 * (nx * light[0] + ny * light[1])
                    self.put(x, y, ramp, lo + round(max(0, min(1, l)) * (hi - lo)))


def noise(x, y):
    h = ((x * 73856093) ^ (y * 19349663) ^ 0x5bd1e995) & 0xffffffff
    h = (h * 2654435761) & 0xffffffff
    return (h >> 7) % 100


def finish(cv):
    """Pixel noise on the textured ramps, then the auto outline ring, rendered strict 2x2."""
    g = dict(cv.g)
    for (x, y), (r, s) in list(g.items()):
        if r in NOISY and 1 <= s < len(RAMPS[r]) - 1:
            n = noise(x, y)
            if n < 16 and s > 1:
                g[(x, y)] = (r, s - 1)
            elif n > 86 and s < len(RAMPS[r]) - 2:
                g[(x, y)] = (r, s + 1)
    xs = [p[0] for p in g]
    ys = [p[1] for p in g]
    x0, y0, x1, y1 = min(xs) - 1, min(ys) - 1, max(xs) + 1, max(ys) + 1
    W, H = x1 - x0 + 1, y1 - y0 + 1
    im = Image.new('RGBA', (W * 2, H * 2), (0, 0, 0, 0))
    px = im.load()

    def put(x, y, col):
        for dy in (0, 1):
            for dx in (0, 1):
                px[(x - x0) * 2 + dx, (y - y0) * 2 + dy] = col
    for (x, y), (r, s) in g.items():
        put(x, y, hx(RAMPS[r][s]))
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if (x, y) in g:
                continue
            nb = [g[p] for p in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)) if p in g]
            if nb:
                put(x, y, min((hx(RAMPS[r][0]) for r, s in nb), key=lambda c: sum(c[:3])))
    return im


S = {}


def fizzcan(cv, x0, y0, tall=12, narrow=False):
    cv.stamp([
        "s5s4s4s4s3s2",
        "s4s1s2s2s1s1",
        "b5b4b3b3b2b1",
        "b5b4b3b2b2b1",
        "n4n4n3n3n2n1",
        "n5s5n4n3n2n1",
        "n4n4s5n3n2n1",
        "n3n3n3n2n2n1",
        "b5b4b3b3b2b1",
        "b4b4b3b2b1b1",
        "b4b3b3b2b1b1",
        "s3s3s2s2s1s1",
    ][:tall], x0, y0)
    if narrow:
        for y in range(y0, y0 + tall):
            cv.g.pop((x0 + 5, y), None)


# FIZZ CAN: blue soda can, steel rim and base, yellow label with a bubble (8x14 cells -> 16x28 px)
cv = Cv()
fizzcan(cv, 0, 0)
S['FizzPop'] = cv

# ICEBERG SHOT: frosted shot glass of ice-cold vodka (9x10 cells -> 18x20 px)
cv = Cv()
cv.stamp([
    "i5i5i4i4i4i4i3",
    "i4i3i3i3i3i3i2",
    "i5i3i4i3i3i3i1",
    "i4i3i3i3i3i2i1",
    "..i4i3i3i2i1..",
    "..i5i3i4i2i1..",
    "..i5i4i4i3i2..",
    "..i4i5i4i3i2..",
], 0, 0)
S['MagmaShot'] = cv

# CASE OF FIZZ: cardboard case with three cans standing in it (17x13 cells -> 34x26 px)
cv = Cv()
for k in range(3):
    fizzcan(cv, k * 6, 0, 5, narrow=True)
for x in range(17):
    edge = 2 if x >= 15 else 3
    cv.put(x, 5, 'c', 5 if x < 15 else 4)
    cv.put(x, 6, 'c', 4 if x < 15 else 3)
    if 0 < x < 16:
        cv.put(x, 7, 'b', 4 if x % 2 else 3)
        cv.put(x, 8, 'b', 5 if x % 3 == 1 else 3)
        cv.put(x, 9, 'b', 2)
    else:
        for y in (7, 8, 9):
            cv.put(x, y, 'c', edge)
    cv.put(x, 10, 'c', edge)
    cv.put(x, 11, 'c', 1)
S['FizzTank'] = cv

# ICEBERG TRAY: frosted vodka bottle and two shots on a steel tray (17x13 cells -> 34x26 px)
cv = Cv()
cv.stamp(["s4s3s1"], 6, 0)
for y in range(1, 3):
    cv.stamp(["i5i4i2"], 6, y)
for y in range(3, 8):
    a = 5 if y == 3 else 4
    for x in range(a, 11):
        cv.put(x, y, 'i', 5 if x == a else 4 if x == a + 1 else 2 if x >= 9 else 3)
for x, y, sh in [(5, 5, 5), (5, 6, 5), (7, 5, 4), (8, 7, 5), (6, 4, 5)]:
    cv.put(x, y, 'i', sh)
for x in range(5, 10):
    cv.put(x, 7, 'b', 3 if x % 2 else 2)
cv.put(6, 7, 'b', 5)
for sx in (0, 12):
    cv.stamp(["i5i4i3", "i4i3i2", "i3i3i1", "i2i2i1"], sx, 4)
for y, (a, b, sh) in zip(range(8, 11), [(0, 14, 5), (0, 14, 4), (1, 13, 2)]):
    for x in range(a, b + 1):
        cv.put(x, y, 's', max(1, sh - (1 if x > b - 3 else 0)))
cv.put(1, 8, 's', 5)
S['MagmaCrucible'] = cv

# FISH FRY PLATTER: steel plate, two fried fish, lemon wedge, parsley (15x10 cells -> 30x20 px)
cv = Cv()
cv.ellipse(7, 3.5, 7, 3.5, 's', 2, 5)
for y in range(1, 6):
    for x in range(2, 13):
        nx, ny = (x - 7) / 5.6, (y - 3.3) / 2.6
        if nx * nx + ny * ny <= 1:
            cv.put(x, y, 's', 4 if (x + y) % 5 else 5)
fish = [
    "..y1y1y1y1..y2",
    "y2y4y5y4y3y2y1",
    "..y2y3y3y2y1..",
]
cv.stamp(fish, 1, 1)
cv.stamp(fish, 2, 4)
cv.put(2, 2, 'f', 0)
cv.put(3, 5, 'f', 0)
cv.stamp(["n3n4n3", "n2n4n2", "..n2.."], 9, 1)
cv.stamp(["g3g2", "g2g3"], 10, 4)
cv.put(11, 4, 'g', 4)
S['FishFryPlatter'] = cv

# FRUIT BASKET: wicker basket heaped with apple, banana, grapes, orange (16x14 cells -> 32x28 px)
cv = Cv()
for x, y in [(4, 0), (5, 0), (6, 0), (7, 0), (8, 0), (9, 0), (3, 1), (10, 1), (2, 2), (11, 2), (2, 3), (11, 3), (2, 4), (11, 4)]:
    cv.put(x, y, 'w', 3 if x < 6 else 2)
cv.put(4, 0, 'w', 4)
cv.ellipse(4.5, 4.5, 2.5, 2.2, 'a', 1, 4)
cv.put(4, 2, 'g', 3)
cv.put(5, 1, 'w', 1)
cv.put(4, 4, 'a', 4)
cv.ellipse(8.5, 4.2, 2.2, 2.4, 'n', 1, 4)
cv.put(8, 3, 'n', 4)
cv.put(9, 4, 'n', 3)
cv.stamp(["p3p2", "p4p3p1", "..p2"], 10, 3)
cv.put(10, 4, 'p', 4)
cv.ellipse(7.0, 5.2, 1.6, 1.4, 'o', 1, 4)
for y in range(6, 13):
    a = 0 if y < 8 else (1 if y < 11 else 2)
    b = 13 - a
    for x in range(a, b + 1):
        base = 3 if (x + y) % 2 == 0 else 2
        base += 1 if x - a < 2 else 0
        base -= 1 if b - x < 2 else 0
        if y % 2 == 0 and x % 4 == 1:
            base = 1
        cv.put(x, y, 'w', max(1, min(4, base)))
for x in range(0, 14):
    cv.put(x, 6, 'w', 4 if x < 11 else 3)
    cv.put(x, 7, 'w', 3 if x < 11 else 2)
for x in range(2, 12):
    cv.put(x, 12, 'w', 1)
S['FruitBasket'] = cv

# BURGER BOX: red take-away box, yellow handle, burger emblem (15x15 cells -> 30x30 px)
cv = Cv()
for x, y in [(3, 0), (4, 0), (5, 0), (6, 0), (7, 0), (8, 0), (9, 0), (2, 1), (10, 1), (2, 2), (10, 2)]:
    cv.put(x, y, 'n', 4 if x < 6 else 2)
for y in range(3, 13):
    for x in range(0, 13):
        sh = 4 if x < 2 else (3 if x < 9 else (2 if x < 11 else 1))
        if y == 3:
            sh = 4 if x < 10 else 3
        if y >= 11:
            sh -= 1
        if y == 12:
            sh = 1
        cv.put(x, y, 'r', max(1, sh))
cv.stamp([
    "y3y5y4y3y2",
    "y2y4y3y3y1",
    "g3g4g3g4g2",
    "d1d2d1d2d1",
    "n3n4n3n3n2",
    "y3y3y3y2y2",
], 4, 4)
for x in range(3, 10):
    cv.put(x, 10, 'n', 3 if x % 2 else 2)
S['BurgerBox'] = cv

# GRAND FEAST: roast bird on a steel platter with garnish (20x14 cells -> 40x28 px)
cv = Cv()
cv.ellipse(9, 3.8, 5.4, 3.2, 'y', 1, 4)
for x in range(5, 14):
    cv.put(x, 5, 'y', 1)
for x, y in [(6, 4), (8, 3), (10, 4), (12, 3), (7, 2)]:
    cv.put(x, y, 'y', 2)
for x, y in [(7, 1), (8, 1), (6, 2)]:
    cv.put(x, y, 'y', 5)
cv.stamp(["g3g4", "g2g3"], 10, 0)
cv.ellipse(3.0, 4.6, 1.8, 1.6, 'y', 1, 4)
cv.ellipse(15.0, 4.6, 1.8, 1.6, 'y', 1, 4)
for x, y, sh in [(1, 6, 4), (2, 7, 5), (17, 6, 4), (16, 7, 5)]:
    cv.put(x, y, 'f', sh)
for y, (a, b, sh) in zip(range(7, 13), [(0, 17, 5), (0, 17, 4), (1, 16, 3), (1, 16, 3), (2, 15, 2), (4, 13, 1)]):
    for x in range(a, b + 1):
        cv.put(x, y, 's', max(1, sh - (1 if x > b - 4 else 0)))
for x, r, sh in [(2, 'a', 3), (4, 'g', 3), (6, 'o', 3), (11, 'o', 3), (13, 'g', 3), (15, 'a', 3)]:
    cv.put(x, 8, r, sh)
    cv.put(x + 1, 8, r, sh - 1)
S['GrandFeast'] = cv

ORDER = ['FizzPop', 'FizzTank', 'MagmaShot', 'MagmaCrucible', 'FishFryPlatter', 'FruitBasket', 'BurgerBox', 'GrandFeast']
LABEL = {'FizzPop': 'Fizz Can', 'FizzTank': 'Case of Fizz', 'MagmaShot': 'Iceberg Shot', 'MagmaCrucible': 'Iceberg Tray', 'FishFryPlatter': 'Fish Fry', 'FruitBasket': 'Fruit Basket', 'BurgerBox': 'Burger Box', 'GrandFeast': 'Grand Feast'}
VAN = ['Cream_Soda', 'Ale', 'Bottled_Water', 'Obsidian_Skin_Potion', 'Cooked_Fish', 'Fruit_Salad', 'Burger', 'Roasted_Duck', 'Pad_Thai']


def sheet(rows, path, scale=4, pad=14, bg=(58, 66, 88, 255)):
    rw = [sum(im.width * scale + pad for _, im in r) + pad for r in rows]
    rh = [max(im.height * scale for _, im in r) + 14 + pad for r in rows]
    sh = Image.new('RGBA', (max(rw), sum(rh) + pad), bg)
    d = ImageDraw.Draw(sh)
    y = pad
    for r, h in zip(rows, rh):
        x = pad
        for lab, im in r:
            big = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
            sh.paste(big, (x, y + 12 + (h - pad - 12 - big.height) // 2), big)
            d.text((x, y), lab, fill=(200, 210, 230, 255))
            x += big.width + pad
        y += h
    sh.save(path)


def main():
    ims = {}
    for n in ORDER:
        ims[n] = finish(S[n])
        ims[n].save(os.path.join(OUT, n + '.png'))
        print(n, ims[n].size)
    sheet([[(LABEL[n], ims[n]) for n in ORDER[:4]], [(LABEL[n], ims[n]) for n in ORDER[4:]]], PREVIEW)
    van = [(v.replace('_', ' '), Image.open(os.path.join(REF, v + '.png')).convert('RGBA')) for v in VAN]
    sheet([van, [(LABEL[n], ims[n]) for n in ORDER]], COMPARE)


if __name__ == '__main__':
    main()

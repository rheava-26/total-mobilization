#!/usr/bin/env python3
"""Draws Warbands' art: the 20 summoning items (32x32 on a 2x2 pixel grid, four base designs with per-band colours and emblems, pixel noise, a dark outline in each item's own darkest shade)
and the red pennant a band member carries. Also writes a 4x preview sheet. Run from anywhere: python3 make_art.py [preview_out.png]"""
import os, random, sys
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ITEMS = os.path.join(HERE, '..', 'Warbands', 'Content', 'Items')
NPCS = os.path.join(HERE, '..', 'Warbands', 'Content', 'NPCs')

# id, kind, tint (r,g,b), emblem
BANDS = [
    ("ForestDeserters", "poster", (150, 120, 80), "bow"),
    ("IronBrigands", "poster", (120, 120, 130), "skull"),
    ("DuneRaiders", "flag", (210, 170, 80), "sun"),
    ("SnowRangers", "flag", (150, 200, 230), "snow"),
    ("CorruptZealots", "effigy", (150, 90, 190), "eye"),
    ("CrimsonFleshCult", "effigy", (200, 50, 60), "drop"),
    ("JunglePoachers", "poster", (90, 170, 80), "leaf"),
    ("MeteorMarauders", "standard", (230, 120, 60), "star"),
    ("AshScavengers", "standard", (220, 90, 40), "flame"),
    ("DungeonWardens", "flag", (110, 130, 190), "key"),
    ("HiveRaiders", "flag", (230, 190, 60), "hex"),
    ("HallowPaladins", "effigy", (240, 220, 150), "cross"),
    ("MythrilMercenaries", "poster", (110, 200, 150), "coin"),
    ("HellfireLegion", "standard", (230, 70, 30), "horn"),
    ("TitaniumLegion", "flag", (190, 190, 200), "chevron"),
    ("ChlorophyteWardens", "standard", (120, 220, 70), "tree"),
    ("ShroomiteRaiders", "standard", (70, 120, 230), "mush"),
    ("FrostLegion", "flag", (170, 220, 255), "diamond"),
    ("DungeonRitualists", "effigy", (140, 150, 230), "rune"),
    ("CultistArchers", "effigy", (70, 90, 220), "eye"),
]

EMBLEMS = {
    "bow":     ["#...#", "#.#..", "#..#.", "#.#..", "#...#"],
    "skull":   [".###.", "#.#.#", "#####", ".###.", ".#.#."],
    "sun":     ["#.#.#", ".###.", "#####", ".###.", "#.#.#"],
    "snow":    ["#.#.#", ".###.", "#####", ".###.", "#.#.#"],
    "eye":     [".....", ".###.", "##.##", ".###.", "....."],
    "drop":    ["..#..", ".###.", "#####", "#####", ".###."],
    "leaf":    ["...##", "..###", ".###.", "###..", "#...."],
    "star":    ["..#..", "..#..", "#####", ".###.", ".#.#."],
    "flame":   ["..#..", ".##..", ".###.", "#####", ".###."],
    "key":     [".##..", "#..#.", ".##..", "..#..", "..##."],
    "hex":     [".###.", "#...#", "#...#", "#...#", ".###."],
    "cross":   ["..#..", "..#..", "#####", "..#..", "..#.."],
    "coin":    [".###.", "#.#.#", "#.###", "#.#.#", ".###."],
    "horn":    ["#...#", "#...#", ".#.#.", ".###.", "..#.."],
    "chevron": ["#...#", ".#.#.", "..#..", "#...#", ".#.#."],
    "tree":    ["..#..", ".###.", "#####", "..#..", "..#.."],
    "mush":    [".###.", "#####", "#####", "..#..", "..#.."],
    "diamond": ["..#..", ".###.", "#####", ".###.", "..#.."],
    "rune":    ["#.#.#", ".###.", "..#..", ".###.", "#.#.#"],
}


def shade(c, f):
    return tuple(max(0, min(255, int(v * f))) for v in c)


def palette(tint):
    return {
        'light': shade(tint, 1.25), 'mid': tint, 'dark': shade(tint, 0.62), 'darkest': shade(tint, 0.26),
    }


WOOD = {'light': (176, 130, 84), 'mid': (138, 96, 58), 'dark': (96, 64, 38), 'darkest': (46, 28, 18)}
PARCH = {'light': (240, 222, 176), 'mid': (222, 196, 146), 'dark': (180, 152, 108), 'darkest': (74, 56, 36)}
METAL = {'light': (214, 214, 224), 'mid': (160, 162, 176), 'dark': (104, 106, 122), 'darkest': (36, 38, 50)}


def blank():
    return [[None] * 16 for _ in range(16)]


def put(img, x, y, c):
    if 0 <= x < 16 and 0 <= y < 16:
        img[y][x] = c


def rect(img, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put(img, x, y, c)


def emblem(img, name, ox, oy, c):
    for j, row in enumerate(EMBLEMS[name]):
        for i, ch in enumerate(row):
            if ch == '#':
                put(img, ox + i, oy + j, c)


def design(kind, tint, emb):
    img = blank(); p = palette(tint)
    if kind == "poster":
        rect(img, 3, 1, 12, 14, PARCH['mid']); rect(img, 3, 1, 12, 2, PARCH['light'])
        for y in (14,):
            for x in (4, 7, 10):
                put(img, x, y, None)          # torn lower edge
        put(img, 3, 14, None); put(img, 12, 13, None)
        rect(img, 4, 3, 11, 3, PARCH['dark'])  # header bar
        emblem(img, emb, 5, 5, p['dark'])
        rect(img, 5, 11, 10, 11, PARCH['dark']); rect(img, 6, 12, 9, 12, PARCH['dark'])
        put(img, 7, 0, METAL['mid']); put(img, 8, 0, METAL['light'])   # the nail
        put(img, 7, 1, METAL['dark'])
        outline = PARCH['darkest']
    elif kind == "flag":
        for y in range(1, 15):
            put(img, 3, y, WOOD['mid'])
        put(img, 3, 1, (230, 200, 90)); put(img, 3, 0, (250, 230, 130))
        rect(img, 4, 2, 13, 9, p['mid']); rect(img, 4, 2, 13, 3, p['light'])
        for x in range(4, 14):
            if (x // 2) % 2 == 0: put(img, x, 10, p['mid']); put(img, x, 9, p['dark'])
        rect(img, 4, 8, 13, 8, p['dark'])
        emblem(img, emb, 7, 3, p['darkest'] if sum(p['mid']) > 400 else (250, 236, 200))
        rect(img, 2, 14, 4, 14, WOOD['dark'])
        outline = p['darkest']
    elif kind == "effigy":
        for y in range(3, 15):
            put(img, 8, y, WOOD['mid']); put(img, 7, y, WOOD['dark'])
        rect(img, 3, 6, 12, 7, WOOD['mid']); rect(img, 3, 7, 12, 7, WOOD['dark'])
        rect(img, 6, 0, 9, 3, WOOD['light']); put(img, 6, 0, None); put(img, 9, 0, None)   # the head
        put(img, 7, 2, p['darkest']); put(img, 9, 2, p['darkest'])
        rect(img, 5, 8, 10, 11, p['mid']); rect(img, 5, 8, 10, 8, p['light']); rect(img, 5, 11, 10, 11, p['dark'])   # the cloth tied on
        put(img, 7, 9, p['darkest']); put(img, 8, 10, p['darkest']); put(img, 9, 9, p['darkest'])
        rect(img, 5, 14, 11, 14, WOOD['dark'])
        outline = WOOD['darkest']
        # the tint belongs in the outline's neighbour: darker of the two
        if sum(p['darkest']) < sum(outline): outline = p['darkest']
    else:  # standard
        for y in range(2, 15):
            put(img, 4, y, WOOD['mid'])
        put(img, 4, 0, METAL['light']); put(img, 4, 1, METAL['mid']); put(img, 3, 1, METAL['dark']); put(img, 5, 1, METAL['dark'])
        rect(img, 5, 2, 12, 11, p['mid']); rect(img, 5, 2, 12, 3, p['light']); rect(img, 5, 10, 12, 11, p['dark'])
        put(img, 6, 12, p['dark']); put(img, 7, 12, p['dark']); put(img, 10, 12, p['dark']); put(img, 11, 12, p['dark'])
        put(img, 8, 11, None); put(img, 9, 11, None)   # swallowtail
        emblem(img, emb, 7, 4, p['darkest'] if sum(p['mid']) > 400 else (250, 236, 200))
        rect(img, 3, 14, 6, 14, WOOD['dark'])
        outline = p['darkest']
    return img, outline


def finish(img, outline, seed):
    rnd = random.Random(seed)
    out = [[None] * 16 for _ in range(16)]
    for y in range(16):
        for x in range(16):
            c = img[y][x]
            if c is None: continue
            n = rnd.choice([-9, -5, 0, 0, 0, 5, 9])      # pixel noise
            out[y][x] = tuple(max(0, min(255, v + n)) for v in c)
    for y in range(16):
        for x in range(16):
            if out[y][x] is not None: continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                xx, yy = x + dx, y + dy
                if 0 <= xx < 16 and 0 <= yy < 16 and img[yy][xx] is not None:
                    out[y][x] = ('o',)
                    break
    for y in range(16):
        for x in range(16):
            if out[y][x] == ('o',): out[y][x] = outline
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    for y in range(16):
        for x in range(16):
            c = out[y][x]
            if c is None: continue
            for dy in range(2):
                for dx in range(2):
                    px[x * 2 + dx, y * 2 + dy] = (c[0], c[1], c[2], 255)
    return im


def pennant():
    img = blank()
    for y in range(0, 16): put(img, 2, y, WOOD['mid'])
    put(img, 2, 0, (230, 200, 90))
    red = (200, 40, 40)
    rect(img, 3, 1, 9, 5, red); rect(img, 3, 1, 9, 1, (236, 80, 70)); rect(img, 3, 5, 9, 5, (140, 20, 28))
    put(img, 8, 5, None); put(img, 9, 5, None); put(img, 9, 4, None)
    put(img, 5, 3, (250, 220, 200)); put(img, 6, 3, (250, 220, 200))
    return finish(img, (60, 8, 12), 77)


def main():
    os.makedirs(ITEMS, exist_ok=True)
    sheet = []
    for i, (bid, kind, tint, emb) in enumerate(BANDS):
        img, outline = design(kind, tint, emb)
        im = finish(img, outline, 1000 + i)
        im.save(os.path.join(ITEMS, bid + '.png'))
        sheet.append((bid, im))
    pennant().save(os.path.join(NPCS, 'BandPennant.png'))
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'docs', 'item_preview_4x.png')
    cols, cell = 5, 32 * 4 + 40
    rows = (len(sheet) + cols - 1) // cols
    W, H = cols * cell, rows * (32 * 4 + 44)
    pv = Image.new('RGBA', (W, H), (48, 56, 72, 255)); d = ImageDraw.Draw(pv)
    for i, (bid, im) in enumerate(sheet):
        cx, cy = (i % cols) * cell, (i // cols) * (32 * 4 + 44)
        pv.alpha_composite(im.resize((128, 128), Image.NEAREST), (cx + 20, cy + 6))
        d.text((cx + 8, cy + 138), bid, fill=(230, 230, 240, 255))
    pv.save(out)
    print('wrote', len(sheet), 'items and', out)


if __name__ == '__main__':
    main()

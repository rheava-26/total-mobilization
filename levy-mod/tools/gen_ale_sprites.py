#!/usr/bin/env python3
"""Barracks Ale / Black Stout / Spiced Rum item sprites (20x28, vanilla potion conventions: dark outline, lit left edge, shaded right edge and base,
glass above the liquid line, a cork or cap). Usage: python3 gen_ale_sprites.py [out_dir] [preview_dir]
(default out: Levy/Content/Items/OOA; preview_dir, if given, receives x4 PNGs)."""
import os, sys
from PIL import Image

W, H = 20, 28
OUT = (22, 16, 24, 255)
GLASS_L, GLASS, GLASS_D = (214, 238, 246, 255), (160, 196, 214, 255), (112, 146, 170, 255)
WHITE = (255, 255, 255, 255)


def ramp(hex_list):
    return [tuple(int(h[i:i + 2], 16) for i in (1, 3, 5)) + (255,) for h in hex_list]


def build(spans, liquid_top, liq, cork_box, cork, extras, name, out):
    """spans: {y: (x0, x1)} inclusive silhouette; liq: [highlight, light, mid, dark, deep]; cork: [light, mid, dark]."""
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = img.load()
    inside = {(x, y) for y, (a, b) in spans.items() for x in range(a, b + 1)}
    bottom = max(spans)
    for (x, y) in inside:
        a, b = spans[y]
        if any((x + dx, y + dy) not in inside for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1))):
            px[x, y] = OUT
            continue
        rel_l, rel_r = x - a, b - x
        if y < liquid_top:
            px[x, y] = GLASS_L if rel_l == 1 else (GLASS_D if rel_r <= 1 else GLASS)
        else:
            if rel_l <= 1:
                c = liq[1]
            elif rel_r <= 1:
                c = liq[3]
            else:
                c = liq[2]
            if y >= bottom - 2 and rel_l > 1:
                c = liq[3]
            if y == bottom - 1:
                c = liq[4] if rel_l > 0 else liq[3]
            if y == liquid_top:
                c = liq[0]
            px[x, y] = c
    for fn in extras:
        fn(px, inside)
    y0, y1, x0, x1 = cork_box
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            edge = y in (y0, y1) or x in (x0, x1)
            px[x, y] = OUT if edge else (cork[0] if x == x0 + 1 else (cork[2] if x >= x1 - 1 else cork[1]))
    img.save(os.path.join(out, name + ".png"))
    return img


def glint(pts):
    def f(px, inside):
        for (x, y) in pts:
            if (x, y) in inside:
                px[x, y] = WHITE
    return f


def label(x0, x1, y0, y1, paper, ink, mark):
    def f(px, inside):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                edge = y in (y0, y1) or x in (x0, x1)
                px[x, y] = OUT if edge else (ink if (x, y) in mark else paper)
    return f


def span(rows):
    d = {}
    for y0, y1, x0, x1 in rows:
        for y in range(y0, y1 + 1):
            d[y] = (x0, x1)
    return d


def taper(y0, y1, a0, a1, b0, b1):
    n = max(1, y1 - y0)
    return [(y, y, round(a0 + (a1 - a0) * (y - y0) / n), round(b0 + (b1 - b0) * (y - y0) / n)) for y in range(y0, y1 + 1)]


def beacon(path):
    """24x24: a glowing meteorite rock on a small brass stand (shaded left-lit, orange glow in the cracks)."""
    im = Image.new("RGBA", (24, 24), (0, 0, 0, 0)); px = im.load()
    rock = {(y, x) for y in range(3, 19) for x in range(5, 19) if ((x - 11.5) / 7.0) ** 2 + ((y - 10.5) / 7.5) ** 2 <= 1.0}
    for (y, x) in rock:
        edge = any((y + dy, x + dx) not in rock for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)))
        if edge: px[x, y] = OUT; continue
        t = (x - 5) + (y - 3) * 0.5
        px[x, y] = (150, 128, 120, 255) if t < 6 else ((112, 92, 90, 255) if t < 12 else (74, 58, 62, 255))
    for (x, y) in ((8, 7), (9, 8), (10, 9), (13, 11), (14, 12), (11, 14), (12, 15)):
        px[x, y] = (255, 190, 70, 255)
    for (x, y) in ((10, 8), (14, 11), (12, 14)):
        px[x, y] = (255, 120, 40, 255)
    for (x, y) in ((8, 5), (9, 5), (7, 6)):
        px[x, y] = WHITE
    for y in range(19, 23):
        for x in range(7, 17):
            e = y in (19, 22) or x in (7, 16)
            px[x, y] = OUT if e else ((226, 184, 92, 255) if x < 10 else ((160, 118, 52, 255) if x > 13 else (196, 150, 70, 255)))
    im.save(path)
    return im


def main(out, preview=None):
    os.makedirs(out, exist_ok=True)
    made = {}
    # BARRACKS ALE: tall long-neck bottle, amber ale, rust crown cap
    ale = span([(4, 11, 8, 11)] + taper(12, 15, 7, 4, 12, 15) + [(16, 26, 4, 15), (27, 27, 5, 14)])
    made["BarracksAle"] = build(ale, 13, ramp(["#fff0a0", "#f6b83a", "#d98322", "#a3511a", "#6b3112"]), (2, 4, 7, 12), ramp(["#d9714a", "#a8452c", "#6e2a1c"]),
                                [glint([(5, 17), (5, 18), (5, 19), (5, 20), (9, 6), (9, 7)])], "BarracksAle", out)
    # BLACK STOUT: squat round-bellied bottle, near-black stout with tan foam head, cork
    stout = span([(6, 9, 7, 12)] + taper(10, 13, 6, 2, 13, 17) + [(14, 25, 1, 18)] + taper(26, 27, 2, 3, 17, 16))

    def foam(px, inside):
        for x in range(2, 18):
            if (x, 15) in inside and px[x, 15] != OUT:
                px[x, 15] = (240, 220, 170, 255) if x < 14 else (190, 160, 115, 255)
            if (x, 16) in inside and px[x, 16] != OUT and 3 < x < 16:
                px[x, 16] = (214, 184, 134, 255) if x < 12 else (160, 128, 92, 255)
    made["BlackStout"] = build(stout, 15, ramp(["#f0dcaa", "#6a4a38", "#3a2620", "#241512", "#150b0a"]), (3, 5, 7, 12), ramp(["#caa072", "#9a7248", "#6a4c30"]),
                               [foam, glint([(3, 19), (3, 20), (3, 21), (4, 22), (8, 8), (8, 9)])], "BlackStout", out)
    # SPICED RUM: square decanter, crimson rum, cream label with a dark flame mark, cork
    rum = span([(4, 8, 8, 11)] + taper(9, 11, 7, 3, 12, 16) + [(12, 26, 3, 16), (27, 27, 4, 15)])
    mark = {(9, 19), (10, 18), (10, 19), (10, 20), (9, 20), (11, 19), (11, 20), (10, 21)}
    made["SpicedRum"] = build(rum, 12, ramp(["#ffb0a0", "#e0423a", "#b01e2c", "#7a1020", "#4a0814"]), (1, 4, 8, 11), ramp(["#d8b078", "#a67c48", "#6c4a2a"]),
                              [label(6, 13, 16, 23, (240, 226, 190, 255), (122, 24, 36, 255), mark), glint([(4, 13), (4, 14), (4, 15), (4, 24), (4, 25), (9, 6), (9, 7)])], "SpicedRum", out)
    made["MeteorBeacon"] = beacon(os.path.join(out, "..", "MeteorBeacon.png"))
    if preview:
        os.makedirs(preview, exist_ok=True)
        sheet = Image.new("RGBA", (4 * 90 + 10, 132), (70, 80, 100, 255))
        for i, n in enumerate(["BarracksAle", "BlackStout", "SpicedRum", "MeteorBeacon"]):
            im = made[n].resize((made[n].width * 4, made[n].height * 4), Image.NEAREST)
            im.save(os.path.join(preview, n + "_x4.png"))
            sheet.paste(im, (10 + i * 90, 10), im)
        sheet.save(os.path.join(preview, "all_x4.png"))


if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "..", "Levy", "Content", "Items", "OOA"), sys.argv[2] if len(sys.argv) > 2 else None)

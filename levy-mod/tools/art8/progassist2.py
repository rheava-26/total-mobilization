"""Progression assistance art: the Superpowered Magnet (30x30) and the Slime Agitator (26x28). Original pixel art in vanilla's item style:
a hand-built silhouette, a 5-step ramp shaded from an upper-left light, a dark rim outline, a few highlight pixels and a little pixel noise, no flat fills.
usage: python3 -I progassist2.py  -> writes the two item PNGs and progassist2_preview_4x.png"""
import math
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, '..', '..', 'Levy', 'Content', 'Items'))
PREVIEW = os.path.join(HERE, 'progassist2_preview_4x.png')


def hx(s):
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


# ramps, darkest first
STEEL = [hx(c) for c in ('1d1a2e', '3a3556', '5b5580', '8680ad', 'b6b1d6', 'e4e1f6')]      # exodium-violet steel
RED = [hx(c) for c in ('3e0f18', '7a1429', 'b02a35', 'd9503f', 'f08460')]
GEL = [hx(c) for c in ('0d3a52', '17648a', '2b95b8', '5cc3d6', '9be6ea', 'e2fbff')]        # slime gel, teal
GLASS = [hx(c) for c in ('1c3d5e', '3f78a8', '7fb7dc', 'bfe3f4')]
CORK = [hx(c) for c in ('3a2309', '6b4219', '9a6a30', 'c79a58')]
GOLD = [hx(c) for c in ('5a3a0a', 'a87a0a', 'dba90b', 'ffe67a')]
PINK = [hx(c) for c in ('4a1040', '8e2a78', 'cf55a8', 'f8a0d8')]
CYAN = [hx(c) for c in ('0c4a5a', '1d96aa', '5ae0e8', 'd6ffff')]


def noise(x, y, k=0):
    h = ((x * 73856093) ^ (y * 19349663) ^ (k * 83492791) ^ 0x5bd1e995) & 0xffffffff
    h = (h * 2654435761) & 0xffffffff
    return (h >> 9) % 100


def shade(ramp, light, jitter=0, x=0, y=0):
    i = light * (len(ramp) - 1) + (noise(x, y) / 100.0 - 0.5) * jitter
    return ramp[max(0, min(len(ramp) - 1, int(round(i))))]


def outline(img, rim_of=None):
    """1px dark ring around every opaque pixel, coloured by the darkest shade of what it hugs."""
    w, h = img.size
    src = img.copy()
    px, out = src.load(), img.load()
    for y in range(h):
        for x in range(w):
            if px[x, y][3]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and px[nx, ny][3]:
                    out[x, y] = rim_of(px[nx, ny]) if rim_of else (20, 14, 28, 255)
                    break


def darken(c, f=0.45):
    return (int(c[0] * f), int(c[1] * f), int(c[2] * f), 255)


def magnet():
    W = H = 32
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    px = img.load()
    cx, cy, ro, ri = 15.5, 14.5, 12.5, 6.0
    legs_y = 27
    # horseshoe: an arch (upper half ring) with two straight legs going down
    def inside(x, y):
        d = math.hypot(x - cx, y - cy)
        if y <= cy:
            return ri <= d <= ro
        return (ri <= abs(x - cx) <= ro) and y <= legs_y
    for y in range(H):
        for x in range(W):
            if not inside(x + 0.5, y + 0.5):
                continue
            # light from the upper left: surface normal for the ring, a flat gradient for the legs
            if y + 0.5 <= cy:
                ang = math.atan2(y + 0.5 - cy, x + 0.5 - cx)
                d = math.hypot(x + 0.5 - cx, y + 0.5 - cy)
                across = (d - ri) / (ro - ri)               # 0 inner edge .. 1 outer edge
                lit = 0.5 + 0.5 * math.cos(ang - math.radians(-135))
                v = 0.25 + 0.55 * lit * (1 - abs(across - 0.5) * 0.9) + 0.15 * (1 - abs(across - 0.45) * 2)
            else:
                across = (abs(x + 0.5 - cx) - ri) / (ro - ri)
                left = (x + 0.5) < cx
                v = 0.55 - 0.35 * abs(across - (0.35 if left else 0.65)) + (0.12 if left else -0.1)
            c = shade(STEEL, max(0.0, min(1.0, v)), jitter=0.9, x=x, y=y)
            px[x, y] = c
    # the pole faces: red / silver bands on the last rows of each leg, like a real magnet
    for y in range(legs_y - 7, legs_y + 1):
        for x in range(W):
            if not (px[x, y][3]):
                continue
            left = x < cx
            band = y >= legs_y - 3
            if band:                                          # silver pole cap
                lit = 0.78 - 0.1 * (y - (legs_y - 3)) + (0.1 if left else -0.05)
                px[x, y] = shade(STEEL, lit, jitter=0.5, x=x, y=y)
            elif y >= legs_y - 7:                            # red stripe
                row = y - (legs_y - 7)
                lit = [0.75, 0.55, 0.4, 0.3][row] + (0.12 if left else -0.05)
                px[x, y] = shade(RED, lit, jitter=0.6, x=x, y=y)
    # rivets on the arch
    for (rx, ry) in ((15, 3), (7, 8), (24, 8)):
        px[rx, ry] = STEEL[5]
        px[rx + 1, ry + 1] = STEEL[1]
    outline(img, lambda c: darken(c, 0.35))
    # exodium sparks between the poles
    for (sx, sy, ramp) in ((15, 25, CYAN), (13, 22, PINK), (17, 21, CYAN), (15, 18, PINK), (14, 28, CYAN)):
        px[sx, sy] = ramp[3]
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = sx + dx, sy + dy
            if 0 <= nx < W and 0 <= ny < H and not px[nx, ny][3]:
                px[nx, ny] = ramp[1]
    for (sx, sy) in ((11, 27), (19, 26), (16, 29)):
        px[sx, sy] = CYAN[2]
    return img


def agitator():
    W, H = 28, 30
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    px = img.load()
    cx, cy, rx, ry = 13.5, 19.0, 10.0, 9.0
    gel_top = 15.0
    # flask: a round belly with a narrow neck
    def glass(x, y):
        if y >= 9 and ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0:
            return True
        return 6 <= y < 12 and abs(x - cx) <= 3.4
    for y in range(H):
        for x in range(W):
            if not glass(x + 0.5, y + 0.5):
                continue
            nx, ny = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
            lit = 0.5 - 0.5 * (nx * 0.65 + ny * 0.75)
            if y + 0.5 >= gel_top + math.sin(x * 0.9) * 0.8:       # gel fills the lower belly
                v = 0.15 + 0.7 * lit + 0.08 * math.sin(x * 0.5 + y * 0.3)
                px[x, y] = shade(GEL, max(0.0, min(1.0, v)), jitter=0.8, x=x, y=y)
            else:                                                   # empty glass
                px[x, y] = shade(GLASS, 0.25 + 0.5 * lit, jitter=0.4, x=x, y=y)
    # glass rim highlight along the upper left of the belly
    for y in range(H):
        for x in range(W):
            if px[x, y][3] and px[x - 1, y][3] == 0 and x > 0 and y > 12 and y < 22:
                px[x, y] = GLASS[3]
    # ore chunks floating in the gel: gold, pink, cyan (three ores the slimes carry)
    for (ox, oy, ramp) in ((10, 20, GOLD), (16, 22, PINK), (13, 25, CYAN), (18, 19, GOLD), (8, 24, CYAN)):
        for dx, dy, s in ((0, 0, 2), (1, 0, 1), (0, 1, 1), (1, 1, 0), (-1, 0, 0), (0, -1, 3)):
            if px[ox + dx, oy + dy][3]:
                px[ox + dx, oy + dy] = ramp[s]
    # gel highlights and bubbles
    for (bx, by) in ((7, 21), (9, 17), (17, 17)):
        px[bx, by] = GEL[5]
    px[8, 21] = GEL[4]
    # cork and a bent crank handle (the agitator)
    for y in range(3, 7):
        for x in range(9, 18):
            lit = 0.3 + 0.5 * (1 - (x - 9) / 8.0) - 0.05 * (y - 3)
            px[x, y] = shade(CORK, max(0.0, min(1.0, lit)), jitter=0.7, x=x, y=y)
    for y in range(0, 4):                                           # rod up from the cork
        px[13, y] = shade(STEEL, 0.75, x=13, y=y)
        px[14, y] = shade(STEEL, 0.35, x=14, y=y)
    for x in range(13, 21):                                         # crank arm
        px[x, 0] = shade(STEEL, 0.8, x=x, y=0)
        px[x, 1] = shade(STEEL, 0.4, x=x, y=1)
    px[20, 2] = RED[3]; px[21, 2] = RED[2]; px[20, 3] = RED[2]; px[21, 3] = RED[1]   # knob
    px[20, 1] = RED[4]; px[21, 1] = RED[3]
    outline(img, lambda c: darken(c, 0.4))
    return img


def main():
    items = {'SuperpoweredMagnet.png': magnet(), 'SlimeAgitator.png': agitator()}
    for name, im in items.items():
        im.save(os.path.join(OUT, name))
    sheet = Image.new("RGBA", (4 * 64 + 24, 4 * 36), (60, 90, 140, 255))
    x = 8
    for im in items.values():
        sheet.alpha_composite(im.resize((im.width * 4, im.height * 4), Image.NEAREST), (x, 4))
        x += im.width * 4 + 8
    sheet.save(PREVIEW)
    for n, im in items.items():
        print(n, im.size)


main()

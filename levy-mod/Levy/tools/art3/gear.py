"""GoPro (cam + collar + carry icon), Static Camera (item + front-flat tripod tile), Guide to Autonomy."""
import math
from lib3 import *

BLK = {
    '0': rgb(14, 14, 22), '1': rgb(30, 32, 44), '2': rgb(48, 50, 66), '3': rgb(72, 76, 96),
    '4': rgb(106, 110, 134), '5': rgb(152, 156, 182),
}
LENS = {
    'a': rgb(14, 20, 52), 'b': rgb(28, 46, 104), 'c': rgb(48, 80, 160), 'd': rgb(96, 140, 214),
    'e': rgb(206, 232, 255),
}
RED = {'r': rgb(120, 22, 32), 'R': rgb(226, 58, 56), 'Q': rgb(255, 130, 110)}
SCREEN = {'g': rgb(92, 150, 108), 'G': rgb(170, 220, 150)}
STEEL = {
    's0': None,
}
SIL = {  # silver
    'S': rgb(58, 62, 84), 'T': rgb(104, 110, 138), 'U': rgb(150, 156, 184), 'V': rgb(194, 200, 222), 'W': rgb(238, 242, 252),
}
GOLD = {'h': rgb(110, 62, 22), 'j': rgb(176, 112, 34), 'k': rgb(228, 170, 58), 'l': rgb(252, 218, 112), 'm': rgb(255, 242, 176)}
STRAP = {'x': rgb(70, 14, 28), 'y': rgb(124, 26, 40), 'z': rgb(180, 46, 52), 'Z': rgb(224, 86, 70), 'Y': rgb(250, 134, 104)}
PAL = {}
for d in (BLK, LENS, RED, SCREEN, SIL, GOLD, STRAP):
    PAL.update(d)
GROUPS = ['?12345', '?abcde', '?rRQ', '?gG', 'STUVW', '?hjklm', '?xyzZY', 'S']
GROUPS = ['?12345', '?abcde', '?SSTUVW'[1:], '?hjklm', '?xyzZY']
OUTLINE_GROUPS = ['012345', 'abcde', 'rRQ', 'gG', 'SSTUVW'[1:], 'hjklm', 'xyzZY']
MESS = ['?12345', '?abcde', '?TUVW', '?jklm', '?yzZY']

LENS8 = [
    "..0000..",
    ".043340.",
    "04edcb30",
    "04dcbb30",
    "03cbaa30",
    "03baaa30",
    ".033330.",
    "..0000..",
]


def outline_all(g):
    auto_outline(g, OUTLINE_GROUPS)


def gopro_a():
    g = G(16, 12, PAL)
    g.rect(1, 3, 14, 10, '2')
    g.hline(1, 14, 3, '4')
    g.hline(1, 14, 4, '3')
    g.vline(1, 3, 10, '3')
    g.vline(14, 4, 10, '1')
    g.hline(2, 14, 10, '1')
    g.hline(10, 13, 2, '3')      # shutter bump
    g.put(11, 2, 'R')
    g.put(12, 2, 'r')
    mess(g, MESS, seed=4, edge=0.32, clump=0.07)
    g.art(LENS8, 2, 3)
    # status screen + led + mic holes (right half)
    g.rect(10, 4, 13, 6, '0')
    g.hline(11, 12, 5, 'g')
    g.put(11, 5, 'G')
    g.put(12, 8, 'R')
    g.put(12, 9, 'r')
    g.put(10, 8, '0')
    g.put(10, 9, '0')
    g.put(13, 9, '0')
    mess(g, ['?gG'], seed=2, edge=.2, clump=0)
    outline_all(g)
    # warm glint on the top-left rim
    g.put(2, 3, '5')
    g.put(3, 3, '5')
    return g.render()


def gopro_collar():
    g = G(16, 16, PAL)
    cx, cy = 7.5, 6.0
    for y in range(16):
        for x in range(16):
            d_out = ((x - cx) / 7.2) ** 2 + ((y - cy) / 6.2) ** 2
            d_in = ((x - cx) / 4.7) ** 2 + ((y - cy) / 3.9) ** 2
            if d_out <= 1.0 and d_in >= 1.0:
                g.put(x, y, 'z')
    # light from top-left: lit upper-left arc, shaded lower-right arc
    for y in range(16):
        for x in range(16):
            if g.get(x, y) == 'z':
                if x + y < 11 and (g.get(x - 1, y) == '.' or g.get(x, y - 1) == '.'):
                    g.put(x, y, 'Z')
                elif x + y > 15 and (g.get(x + 1, y) == '.' or g.get(x, y + 1) == '.'):
                    g.put(x, y, 'y')
    # stitching
    for (x, y) in ((3, 2), (6, 0), (10, 0), (13, 2), (14, 5), (1, 7), (2, 9)):
        if g.get(x, y) in 'zZy':
            g.put(x, y, 'Y')
    mess(g, MESS, seed=7, edge=0.35, clump=0.09)
    # gold buckle on the left side of the strap
    g.art(["kjl", "ljh", "kjl"], 0, 5)
    # ring + pendant cam (hangs from bottom of strap)
    g.put(7, 11, 'k')
    g.put(8, 11, 'j')
    cam = G(7, 5, PAL)
    cam.rect(0, 0, 6, 4, '2')
    cam.hline(0, 6, 0, '4')
    cam.vline(0, 0, 4, '3')
    cam.hline(0, 6, 4, '1')
    cam.art(["0000", "0dc0"[0:0] or "0ecb0"[0:0] or "0dcb", "0baa", "0000"], 1, 0)
    # simpler: lens 3x3 + led
    cam.rect(1, 1, 3, 3, '0')
    cam.put(2, 2, 'c')
    cam.put(1, 1, 'e')
    cam.put(2, 1, 'd')
    cam.put(1, 2, 'd')
    cam.put(5, 1, 'R')
    cam.put(5, 3, '0')
    for y in range(5):
        for x in range(7):
            if cam.c[y][x] != '.':
                g.put(5 - 0 + x - 0, 11 + y - 0 + 0, cam.c[y][x]) if False else g.put(4 + x, 11 + y, cam.c[y][x])
    outline_all(g)
    return g.render()


def gopro_icon():
    g = G(8, 8, PAL)
    g.rect(0, 2, 7, 6, '2')
    g.hline(0, 7, 2, '4')
    g.vline(0, 2, 6, '3')
    g.hline(1, 7, 6, '1')
    g.rect(1, 3, 3, 5, '0')
    g.put(2, 4, 'c')
    g.put(1, 3, 'e')
    g.put(5, 3, 'R')
    g.put(6, 4, 'g')
    g.put(6, 1, '3')
    g.put(5, 1, '3')
    # green carried tick
    g.put(7, 0, 'G')
    return g.render()


# ---------------------------------------------------------------- photo camera
def camera(g, x0, y0, w, h, lens_r, mess_seed=1):
    """Front-facing photo camera: silver top plate, dark leather body, big lens."""
    top = 2
    g.rect(x0, y0 + top, x0 + w - 1, y0 + h - 1, '3')
    # silver top plate
    g.rect(x0, y0 + top, x0 + w - 1, y0 + top + 1, 'U')
    g.hline(x0, x0 + w - 1, y0 + top, 'W')
    g.hline(x0 + 1, x0 + w - 1, y0 + top + 1, 'V')
    g.hline(x0 + w - 3, x0 + w - 1, y0 + top + 1, 'T')
    # leather body
    g.vline(x0, y0 + top + 2, y0 + h - 1, '4')
    g.vline(x0 + w - 1, y0 + top + 2, y0 + h - 1, '2')
    g.hline(x0, x0 + w - 1, y0 + h - 1, '2')
    # viewfinder hump + flash + shutter
    g.rect(x0 + 1, y0, x0 + 3, y0 + top - 1, 'U')
    g.put(x0 + 1, y0, 'W')
    g.rect(x0 + w - 5, y0, x0 + w - 3, y0 + top - 1, 'l')
    g.put(x0 + w - 5, y0, 'm')
    g.put(x0 + w - 3, y0 + top - 1, 'j')
    g.put(x0 + w // 2, y0 + top - 1, 'R')
    mess(g, ['?12345', '?TUVW', '?jklm'], seed=mess_seed, edge=.3, clump=.07)
    # lens
    lw = lens_r * 2 + 2
    lx = x0 + (w - lw) // 2
    ly = y0 + top + 1 + (h - top - 1 - lw) // 2 + (0 if lw < h - top - 1 else 0)
    return lx, ly, lw


def lens_n(n):
    if n == 8:
        return LENS8
    if n == 6:
        return [
            ".0000.",
            "04edb0",
            "03dcb0",
            "03cbb0",
            "03baa0",
            ".0000.",
        ]


def cam_item():
    g = G(18, 15, PAL)
    camera(g, 1, 1, 16, 13, 3, 3)
    g.art(LENS8, 5, 5)
    # lens ring ticks (little gold marks)
    g.put(4, 8, 'k')
    g.put(13, 9, 'j')
    g.put(7, 4, 'T') if False else None
    outline_all(g)
    return g.render()


def cam_tile():
    g = G(16, 24, PAL)
    camera(g, 1, 1, 14, 11, 2, 9)
    g.art(lens_n(6), 5, 5)
    # head mount
    g.rect(6, 12, 9, 13, 'T')
    g.hline(6, 9, 12, 'V')
    g.put(9, 13, 'S')
    g.rect(7, 14, 8, 15, '2')
    # legs: splayed pair + centre leg seen end-on, steel with lit left edge
    for k in range(8):
        lx = 7 - (k * 5) // 8
        rx = 8 + (k * 5) // 8
        for dx in (0, 1):
            g.put(lx - 1 + dx, 14 + k, '3' if dx == 0 else '2')
            g.put(rx + dx - 0, 14 + k, '2' if dx == 0 else '1')
    # centre leg
    g.rect(7, 16, 8, 20, '3')
    g.vline(7, 16, 20, '4')
    g.vline(8, 16, 20, '2')
    # feet
    for (x, y) in ((1, 22), (2, 22), (3, 22), (12, 22), (13, 22), (14, 22), (7, 21), (8, 21), (6, 22), (7, 22), (8, 22), (9, 22)):
        g.put(x, y, '1')
    mess(g, ['?12345'], seed=5, edge=.3, clump=.05)
    outline_all(g)
    return g.render()


# ---------------------------------------------------------------- guide to autonomy
BROWN = {'B': rgb(52, 28, 22), 'C': rgb(96, 56, 36), 'D': rgb(138, 84, 50), 'E': rgb(176, 112, 66), 'F': rgb(212, 150, 92)}
SLIME = {'n': rgb(18, 70, 44), 'o': rgb(34, 130, 70), 'p': rgb(70, 190, 90), 'q': rgb(140, 236, 130), 'u': rgb(214, 255, 190)}
PAGE = {'v': rgb(120, 96, 100), 'w': rgb(214, 190, 190), 'X': rgb(246, 236, 230)}


def book():
    W, H = 15, 16
    pal = dict(PAL)
    pal.update(BROWN)
    pal.update(SLIME)
    pal.update(PAGE)
    g = G(W, H, pal)
    th = math.radians(29)
    ux, uy = math.cos(th), math.sin(th)
    vx, vy = -math.sin(th), math.cos(th)
    cx, cy = 7.5, 8.0
    a, b = 4.6, 5.2
    for y in range(H):
        for x in range(W):
            dx, dy = x + .5 - cx, y + .5 - cy
            u = dx * ux + dy * uy
            v = dx * vx + dy * vy
            if abs(u) <= a and abs(v) <= b:
                if v > b - 1.5:
                    g.put(x, y, 'w' if v < b - 0.5 else 'v')
                elif abs(u) > a - 1.0 or v < -b + 1.0:
                    g.put(x, y, 'l' if (x + y) < 13 else 'k' if (x + y) < 17 else 'j')   # gold trim
                else:
                    sh = (u * -0.5 + v * -0.5) / 6
                    g.put(x, y, 'E')
    # lit top-left cover face, shadowed lower-right (light from top-left)
    for y in range(H):
        for x in range(W):
            if g.get(x, y) == 'E':
                dx, dy = x + .5 - cx, y + .5 - cy
                t = (dx + dy) / 9.0
                g.put(x, y, 'F' if t < -0.45 else 'E' if t < 0.0 else 'D' if t < 0.45 else 'C')
    mess(g, ['?CDEF'], seed=14, edge=.33, clump=.08)
    # slime blob, centred, upright
    blob = [
        "..nnn..",
        ".noqpn.",
        "nonqnon",
        "noppppn",
        ".nnnnn.",
    ]
    g.art(blob, 4, 6)
    mess(g, ['?nopqu'], seed=3, edge=.22, clump=0)
    auto_outline(g, ['BCDEF', 'nopqu', 'vwX', 'hjklm'])
    # page edge outline stays dark brown-ish
    return g.render()


def build():
    out = {}
    out['gopro_a_cam'] = gopro_a()
    out['gopro_b_collar'] = gopro_collar()
    out['gopro_carry_icon'] = gopro_icon()
    out['static_camera_item'] = cam_item()
    out['static_camera_tile'] = cam_tile()
    out['guide_to_autonomy'] = book()
    for k, v in out.items():
        save(v, k)
    # tML-padded tile sheet (CoordinatePadding 2): 2x3 tiles -> 34x52
    t = out['static_camera_tile']
    sheet = Image.new('RGBA', (34, 52), (0, 0, 0, 0))
    for r in range(3):
        for c in range(2):
            sheet.paste(t.crop((c * 16, r * 16, c * 16 + 16, r * 16 + 16)), (c * 18, r * 18))
    save(sheet, 'static_camera_tile_padded')
    return out


if __name__ == '__main__':
    o = build()
    compare(OUT + '/cmp_gear.png', 'GoPro / Static Camera / Guide vs vanilla (8x)',
            [('gopro A', o['gopro_a_cam']), ('gopro B collar', o['gopro_b_collar']),
             ('carry icon', o['gopro_carry_icon']), ('camera item', o['static_camera_item']),
             ('camera tile 2x3', o['static_camera_tile']), ('guide', o['guide_to_autonomy'])],
            ['Radar', 'GPS', 'Stopwatch', 'Spell_Tome', 'Book_of_Skulls', 'Water_Bolt', 'Cell_Phone'], scale=6)

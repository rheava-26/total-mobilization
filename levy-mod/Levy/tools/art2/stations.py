"""Armory Heart (2x2 / 3x2 tile), Bulk Factory (3x2 tile), Bulk Potion station (3x3 tile)."""
from lib import *

K = rgb
WOOD = K(150, 98, 52)
WOOD_D = K(98, 62, 32)
WOOD_L = K(190, 130, 72)
IRON = K(96, 102, 120)
IRON_D = K(52, 56, 70)
IRON_L = K(168, 174, 194)
BRASS = K(222, 178, 62)
STEEL_L = K(206, 212, 228)


def heart(sp, cx, cy, r, base, hi, dk):
    """Pixel heart: two circles + a triangle."""
    sp.circle(cx - r + 1, cy, r, base)
    sp.circle(cx + r - 1, cy, r, base)
    sp.poly([(cx - 2 * r + 1, cy + 1), (cx + 2 * r - 1, cy + 1), (cx, cy + 2 * r + 1)], base)
    sp.rect(cx - 2 * r + 1, cy - 1, cx + 2 * r - 1, cy + 1, base)
    for dy in range(0, 2 * r + 1):                      # darker lower-right
        for dx in range(-2 * r, 2 * r + 1):
            x, y = cx + dx, cy + dy
            if sp.get(x, y)[3] and dx - 0.5 * (cy - y) > 2 and dy > 1:
                sp.px(x, y, dk)
    sp.px(cx - r - 1, cy - r + 1, hi); sp.px(cx - r, cy - r + 1, hi); sp.px(cx - r - 1, cy - r + 2, hi)


def sword(sp, x, y0, y1, blade=K(206, 212, 230), hilt=BRASS):
    sp.rect(x, y0, x + 1, y1 - 4, blade)
    sp.px(x, y0, K(250, 252, 255))
    sp.rect(x - 1, y1 - 3, x + 2, y1 - 3, hilt)
    sp.rect(x, y1 - 2, x + 1, y1, WOOD_D)


def spear(sp, x, y0, y1):
    sp.rect(x, y0 + 3, x, y1, WOOD_D)
    sp.poly([(x, y0), (x - 1, y0 + 4), (x + 1, y0 + 4)], K(206, 212, 230))


# ----------------------------------------------------------------- ARMORY HEART
def armory_a():
    """2x2 tile: wooden weapon rack, swords hung left/right, glowing crystal heart in the middle."""
    sp = Sprite(32, 32)
    sp.rect(1, 1, 30, 30, WOOD_D)
    sp.rect(3, 3, 28, 28, K(60, 40, 40))
    sp.rect(1, 1, 30, 2, WOOD_L)
    sp.rect(1, 29, 30, 30, K(70, 44, 22))
    sp.rect(1, 9, 30, 10, WOOD)                         # rails
    sp.rect(1, 22, 30, 23, WOOD)
    sword(sp, 5, 4, 26); sword(sp, 8, 6, 27)
    sword(sp, 22, 6, 27); sword(sp, 25, 4, 26)
    # glow
    for (x, y) in [(x, y) for x in range(9, 23) for y in range(9, 24)]:
        if (x - 15.5) ** 2 + (y - 16) ** 2 < 42:
            sp.px(x, y, K(110, 40, 90))
    heart(sp, 16, 15, 4, K(236, 60, 110), K(255, 190, 210), K(150, 24, 80))
    return finish(sp, do_bevel=False, f=0.3)


def armory_b():
    """2x2 tile: iron cabinet frame (Storage Heart-like), heart crystal in a window, crossed sword + spear behind."""
    sp = Sprite(32, 32)
    sp.rect(1, 1, 30, 30, IRON_D)
    sp.rect(2, 2, 29, 29, IRON)
    sp.rect(2, 2, 29, 3, IRON_L)
    sp.rect(5, 5, 26, 26, K(34, 24, 52))               # window
    sp.line(7, 24, 24, 7, K(190, 196, 214), 1)          # sword
    sp.line(7, 7, 24, 24, WOOD_D, 1)                    # spear shaft
    sp.poly([(24, 24), (22, 23), (23, 22)], STEEL_L)
    sp.rect(5, 23, 9, 24, BRASS)
    for p in [(4, 4), (27, 4), (4, 27), (27, 27)]:
        sp.rect(p[0], p[1], p[0] + 1, p[1] + 1, BRASS)  # rivets
    for (x, y) in [(x, y) for x in range(6, 26) for y in range(6, 26)]:
        d = (x - 15.5) ** 2 + (y - 15) ** 2
        if d < 60:
            sp.px(x, y, mix(K(34, 24, 52), K(150, 60, 210), max(0, 1 - d / 60) * 0.7))
    heart(sp, 16, 14, 4, K(186, 90, 255), K(240, 210, 255), K(100, 40, 170))
    return finish(sp, do_bevel=False, f=0.3)


def armory_c():
    """3x2 tile: wide rack, heart crystal centred on a plinth, axe/sword/bow hung either side."""
    sp = Sprite(48, 32)
    sp.rect(1, 1, 46, 30, WOOD_D)
    sp.rect(3, 3, 44, 28, K(70, 46, 36))
    sp.rect(1, 1, 46, 2, WOOD_L)
    sp.rect(3, 9, 44, 10, WOOD)
    sp.rect(3, 21, 44, 22, WOOD)
    sword(sp, 6, 4, 26); sword(sp, 10, 5, 27)
    for dx in (0, 1):                                   # bow
        for y in range(5, 27):
            sp.px(14 + dx + int(3 * ((y - 16) / 11) ** 2), y, K(180, 130, 70))
    sp.rect(15, 5, 15, 26, K(230, 230, 210))
    spear(sp, 36, 3, 28)
    sword(sp, 40, 4, 26)
    sp.poly([(30, 5), (34, 5), (34, 11), (30, 9)], K(176, 182, 200)); sp.rect(32, 5, 32, 27, WOOD_D)  # axe
    sp.rect(18, 22, 29, 27, IRON); sp.rect(18, 22, 29, 22, IRON_L)
    for (x, y) in [(x, y) for x in range(15, 33) for y in range(7, 22)]:
        if (x - 23.5) ** 2 + (y - 14) ** 2 < 40:
            sp.px(x, y, K(120, 40, 90))
    heart(sp, 24, 14, 4, K(236, 60, 110), K(255, 190, 210), K(150, 24, 80))
    return finish(sp, do_bevel=False, f=0.3)


# ----------------------------------------------------------------- BULK FACTORY
def factory_a():
    """3x2: workbench with a steel hydraulic press standing on it and ingot stack beneath."""
    sp = Sprite(48, 32)
    sp.rect(2, 20, 45, 23, WOOD); sp.rect(2, 20, 45, 20, WOOD_L); sp.rect(2, 23, 45, 23, WOOD_D)
    sp.rect(4, 24, 8, 30, WOOD_D); sp.rect(39, 24, 43, 30, WOOD_D)
    sp.rect(4, 24, 5, 30, WOOD)
    sp.rect(10, 4, 13, 19, IRON); sp.rect(34, 4, 37, 19, IRON)   # uprights
    sp.rect(10, 4, 10, 19, IRON_L); sp.rect(34, 4, 34, 19, IRON_L)
    sp.rect(8, 3, 39, 8, IRON_D); sp.rect(8, 3, 39, 4, IRON)
    sp.rect(20, 9, 27, 14, K(176, 182, 200)); sp.rect(20, 9, 21, 14, STEEL_L)   # piston
    sp.rect(16, 15, 31, 17, IRON_D); sp.rect(16, 15, 31, 15, IRON)
    sp.rect(15, 18, 32, 19, IRON_L)                                           # anvil plate
    for i in range(3):
        sp.rect(17 + i * 5, 16, 20 + i * 5, 16, BRASS)
    sp.circle(41, 6, 2, K(220, 60, 50))
    sp.rect(12, 26, 28, 30, K(108, 70, 40))                                   # crates under
    sp.rect(12, 26, 28, 26, K(150, 100, 56)); sp.rect(20, 26, 20, 30, K(80, 52, 30))
    return finish(sp, f=0.3)


def factory_b():
    """3x2: conveyor belt feeding crates, with a stack of finished bulk crates."""
    sp = Sprite(48, 32)
    sp.rect(2, 20, 33, 24, K(60, 62, 72))
    for x in range(3, 33, 3):
        sp.rect(x, 21, x, 23, K(100, 104, 116))
    sp.circle(4, 26, 3, IRON); sp.circle(31, 26, 3, IRON)
    sp.rect(2, 27, 33, 28, IRON_D)
    sp.rect(5, 29, 7, 30, IRON_D); sp.rect(28, 29, 30, 30, IRON_D)
    for i, x in enumerate((6, 15, 24)):                  # items on belt
        sp.rect(x, 14, x + 6, 19, K(120, 188, 220)); sp.rect(x, 14, x + 6, 14, K(190, 230, 250))
    sp.rect(34, 5, 46, 30, K(150, 100, 56))              # crate stack
    sp.rect(34, 17, 46, 17, K(80, 52, 30)); sp.rect(34, 5, 46, 6, K(190, 136, 80))
    sp.rect(40, 5, 40, 30, K(110, 72, 40))
    sp.rect(36, 9, 38, 13, K(240, 230, 190))
    sp.rect(36, 21, 38, 25, K(240, 230, 190))
    sp.rect(20, 3, 30, 9, IRON); sp.rect(20, 3, 30, 3, IRON_L)   # overhead arm
    sp.rect(24, 9, 26, 12, IRON_D)
    return finish(sp, f=0.3)


def factory_c():
    """3x2: workbench with hopper funnel, big gear wheel and output crate."""
    sp = Sprite(48, 32)
    sp.rect(2, 21, 45, 24, WOOD); sp.rect(2, 21, 45, 21, WOOD_L); sp.rect(2, 24, 45, 24, WOOD_D)
    sp.rect(4, 25, 8, 30, WOOD_D); sp.rect(39, 25, 43, 30, WOOD_D)
    sp.poly([(4, 3), (22, 3), (16, 14), (10, 14)], IRON); sp.rect(4, 3, 22, 4, IRON_L)
    sp.rect(11, 14, 15, 20, IRON_D)
    sp.circle(30, 12, 7, BRASS)
    for a in range(0, 8):
        import math
        x = 30 + int(round(9 * math.cos(a * math.pi / 4))); y = 12 + int(round(9 * math.sin(a * math.pi / 4)))
        sp.rect(x - 1, y - 1, x + 1, y + 1, BRASS)
    sp.circle(30, 12, 3, IRON_D); sp.circle(30, 12, 1, BRASS)
    sp.rect(36, 14, 45, 20, K(150, 100, 56)); sp.rect(36, 14, 45, 14, K(190, 136, 80)); sp.rect(40, 14, 40, 20, K(110, 72, 40))
    sp.rect(20, 17, 27, 20, K(120, 188, 220))
    return finish(sp, f=0.3)


# ----------------------------------------------------------------- BULK POTION STATION
def _bubbles(sp, pts, c):
    for (x, y) in pts:
        sp.rect(x, y, x + 1, y + 1, c)
        sp.px(x, y, K(240, 255, 240))


def potion_a():
    """3x3: huge iron cauldron full of bubbling green brew over a fire."""
    sp = Sprite(48, 48)
    sp.ell(6, 14, 41, 22, IRON_D)                      # rim back
    sp.ell(9, 29, 38, 43, IRON)                        # belly
    sp.rect(6, 18, 41, 36, IRON)
    sp.rect(6, 18, 9, 36, IRON_L)
    sp.rect(38, 18, 41, 36, IRON_D)
    sp.rect(10, 37, 37, 40, IRON_D)
    sp.ell(6, 14, 41, 24, IRON_L)
    sp.ell(9, 16, 38, 22, K(70, 190, 80))              # brew
    sp.ell(13, 17, 30, 20, K(130, 235, 120))
    _bubbles(sp, [(14, 10), (22, 6), (30, 11), (26, 15), (18, 14)], K(110, 220, 110))
    for (x, y) in [(20, 3), (33, 7)]:
        sp.px(x, y, K(180, 240, 170))
    for x in (8, 18, 28, 36):                          # rivets
        sp.px(x, 30, IRON_L)
    sp.rect(6, 41, 10, 45, IRON_D); sp.rect(37, 41, 41, 45, IRON_D); sp.rect(21, 42, 26, 45, IRON_D)
    sp.poly([(14, 45), (18, 41), (22, 45)], K(255, 150, 30)); sp.poly([(24, 45), (28, 40), (33, 45)], K(255, 200, 60))
    return finish(sp, f=0.3)


def potion_b():
    """3x3: alchemy rig. Brass-piped glass tank in the middle, flasks either side, burner beneath."""
    sp = Sprite(48, 48)
    sp.rect(2, 34, 45, 38, WOOD); sp.rect(2, 34, 45, 34, WOOD_L); sp.rect(2, 38, 45, 38, WOOD_D)
    sp.rect(4, 39, 8, 46, WOOD_D); sp.rect(39, 39, 43, 46, WOOD_D)
    sp.ell(14, 6, 33, 33, K(150, 200, 230))            # tank
    sp.ell(16, 16, 31, 32, K(150, 60, 220))            # purple liquid
    sp.rect(17, 24, 30, 32, K(150, 60, 220))
    sp.ell(18, 17, 29, 21, K(190, 120, 255))
    sp.rect(20, 3, 27, 6, STEEL_L); sp.rect(20, 3, 27, 3, K(255, 255, 255))   # cap
    sp.px(17, 10, K(255, 255, 255)); sp.px(17, 11, K(255, 255, 255)); sp.px(18, 9, K(255, 255, 255))
    for (x, y) in [(22, 26), (26, 22), (21, 20)]:
        sp.px(x, y, K(230, 200, 255))
    # side flasks
    sp.rect(5, 22, 7, 25, STEEL_L); sp.rect(3, 26, 9, 33, K(60, 180, 230)); sp.rect(3, 26, 4, 33, K(160, 230, 255))
    sp.rect(38, 22, 40, 25, STEEL_L); sp.rect(36, 26, 42, 33, K(230, 60, 80)); sp.rect(36, 26, 37, 33, K(255, 160, 170))
    # brass pipes
    sp.line(7, 22, 7, 16, BRASS, 2); sp.line(7, 16, 18, 16, BRASS, 2)
    sp.line(39, 22, 39, 16, BRASS, 2); sp.line(39, 16, 29, 16, BRASS, 2)
    sp.rect(20, 33, 27, 34, BRASS)
    sp.rect(12, 36, 34, 37, IRON_D)
    sp.poly([(20, 33), (23, 28), (26, 33)], K(255, 170, 40))
    return finish(sp, f=0.3)


def potion_c():
    """3x3: cauldron in front of a shelf of colourful stored potion bottles."""
    sp = Sprite(48, 48)
    sp.rect(2, 2, 45, 28, K(86, 54, 30))
    sp.rect(2, 2, 45, 3, WOOD_L)
    sp.rect(2, 14, 45, 15, WOOD); sp.rect(2, 27, 45, 28, WOOD)
    cols = [K(220, 60, 60), K(70, 140, 255), K(240, 200, 60), K(150, 70, 220), K(80, 210, 110), K(255, 130, 200)]
    for row, y in enumerate((6, 18)):
        for i in range(8):
            c = cols[(i + row * 2) % len(cols)]
            x = 5 + i * 5
            sp.rect(x + 1, y, x + 2, y + 1, STEEL_L)
            sp.rect(x, y + 2, x + 3, y + 7, c)
            sp.px(x, y + 3, shade(c, 1.5))
    sp.ell(8, 29, 39, 35, IRON_L)                       # cauldron
    sp.ell(8, 32, 39, 44, IRON)
    sp.rect(8, 33, 39, 40, IRON)
    sp.ell(11, 31, 36, 35, K(110, 70, 220))
    sp.ell(15, 32, 28, 34, K(180, 140, 255))
    _bubbles(sp, [(16, 28), (26, 29)], K(150, 110, 240))
    sp.rect(10, 42, 14, 45, IRON_D); sp.rect(33, 42, 37, 45, IRON_D)
    return finish(sp, f=0.3)


STATIONS = {
    'armory_heart': [('A wood rack 2x2', armory_a), ('B iron cabinet 2x2', armory_b), ('C wide rack 3x2', armory_c)],
    'bulk_factory': [('A hydraulic press', factory_a), ('B conveyor + crates', factory_b), ('C hopper + gear', factory_c)],
    'bulk_potion': [('A giant cauldron', potion_a), ('B alchemy rig', potion_b), ('C cauldron + shelf', potion_c)],
}

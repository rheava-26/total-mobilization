"""Old One's Army levy sentry staves: Rod -> Cane -> Staff. Three themed variants x 3 tiers."""
from lib import *

K = rgb
S = 40
WOOD = K(150, 98, 52)
WOOD_D = K(98, 62, 32)
IRON = K(110, 118, 140)
IRON_D = K(60, 66, 88)
GOLD = K(236, 196, 70)
GOLD_D = K(170, 122, 40)
RED = K(220, 50, 50)
STONE = K(150, 150, 160)
STONE_D = K(100, 100, 114)


def shaft(sp, a, b, col, w, hi=None):
    sp.line(a[0], a[1], b[0], b[1], col, w)
    if hi:
        sp.line(a[0] - 1, a[1] - 1, b[0] - 1, b[1] - 1, hi, 1)


def crook(sp, tip, col, r=5):
    """Hooked top (cane): small arc curling right and down from the shaft tip."""
    x, y = tip
    pts = [(x, y), (x + 1, y - 2), (x + 3, y - 3), (x + 5, y - 3), (x + 7, y - 2), (x + 8, y), (x + 8, y + 2)]
    for i in range(len(pts) - 1):
        sp.line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], col, 1)
    return (x + 8, y + 2)


# ---------------- Variant A: banner / pennant
def a_rod():
    sp = Sprite(S, S)
    shaft(sp, (6, 34), (25, 15), WOOD, 2, K(190, 130, 72))
    sp.rect(25, 12, 27, 14, GOLD)
    sp.line(27, 12, 27, 4, WOOD_D, 1)
    sp.poly([(28, 4), (35, 7), (28, 10)], RED)
    sp.px(29, 6, K(255, 160, 160))
    return finish(sp)


def a_cane():
    sp = Sprite(S, S)
    shaft(sp, (5, 35), (24, 14), IRON, 2, K(180, 190, 214))
    sp.rect(10, 29, 12, 31, GOLD)
    end = crook(sp, (24, 13), IRON, 5)
    sp.line(end[0], end[1], end[0], end[1] + 1, IRON, 1)
    sp.rect(end[0] - 3, end[1] + 2, end[0] + 3, end[1] + 11, RED)       # hanging banner
    sp.rect(end[0] - 3, end[1] + 2, end[0] + 3, end[1] + 3, GOLD)
    sp.rect(end[0] - 1, end[1] + 5, end[0] + 1, end[1] + 7, GOLD)
    sp.circle(24, 13, 1, GOLD)
    return finish(sp)


def a_staff():
    sp = Sprite(S, S)
    shaft(sp, (3, 36), (28, 11), K(70, 56, 100), 3, K(120, 100, 170))
    for t in (0.3, 0.55):
        x = int(3 + 25 * t); y = int(36 - 25 * t)
        sp.rect(x - 1, y - 1, x + 2, y + 2, GOLD)
    sp.poly([(24, 14), (34, 14), (35, 6), (31, 9), (29, 3), (27, 9), (23, 6)], GOLD)  # crown
    sp.rect(24, 13, 34, 15, GOLD_D)
    sp.circle(29, 10, 2, K(240, 70, 80)); sp.px(28, 9, K(255, 200, 200))
    sp.line(36, 6, 36, 1, WOOD_D, 1); sp.poly([(37, 1), (39, 3), (37, 5)], RED)
    sp.line(22, 6, 22, 1, WOOD_D, 1); sp.poly([(21, 1), (19, 3), (21, 5)], RED)
    return finish(sp)


# ---------------- Variant B: watchtower
def _tower(sp, x, y, w, h, wall, dark, roof=None, glow=None):
    sp.rect(x, y, x + w, y + h, wall)
    sp.rect(x, y + h - 1, x + w, y + h, dark)
    for bx in range(x, x + w + 1, 2):                    # battlements
        sp.rect(bx, y - 2, bx, y - 1, wall)
    if roof:
        sp.poly([(x - 1, y), (x + w // 2, y - 6), (x + w + 1, y)], roof)
    if glow:
        sp.rect(x + w // 2 - 1, y + 2, x + w // 2, y + 4, glow)


def b_rod():
    sp = Sprite(S, S)
    shaft(sp, (5, 35), (24, 16), WOOD, 2, K(190, 130, 72))
    sp.rect(22, 13, 30, 15, WOOD_D)                      # platform
    sp.rect(23, 9, 24, 13, WOOD); sp.rect(28, 9, 29, 13, WOOD)
    sp.poly([(21, 9), (26, 4), (31, 9)], K(170, 70, 50))
    return finish(sp)


def b_cane():
    sp = Sprite(S, S)
    shaft(sp, (4, 36), (23, 17), IRON, 2, K(180, 190, 214))
    end = crook(sp, (23, 16), IRON, 5)
    _tower(sp, 24, 7, 8, 8, STONE, STONE_D, roof=K(60, 110, 200), glow=K(255, 220, 100))
    sp.rect(22, 15, 33, 16, IRON)
    return finish(sp)


def b_staff():
    sp = Sprite(S, S)
    shaft(sp, (3, 37), (24, 16), K(96, 76, 40), 3, GOLD)
    for t in (0.35, 0.6):
        x = int(3 + 21 * t); y = int(37 - 21 * t)
        sp.rect(x - 1, y - 1, x + 2, y + 2, GOLD)
    sp.rect(19, 14, 36, 17, GOLD_D); sp.rect(19, 14, 36, 14, GOLD)
    _tower(sp, 23, 9, 10, 6, K(210, 200, 170), K(150, 140, 110), glow=K(255, 220, 100))
    sp.rect(21, 6, 35, 7, GOLD)
    sp.rect(24, 2, 24, 5, GOLD); sp.rect(34, 2, 34, 5, GOLD)
    sp.rect(29, 3, 29, 7, GOLD_D)
    sp.poly([(30, 2), (36, 4), (30, 6)], K(230, 60, 60))
    sp.circle(28, 12, 1, K(120, 220, 255))
    sp.circle(35, 15, 1, K(240, 70, 80)); sp.circle(20, 15, 1, K(240, 70, 80))
    return finish(sp)


# ---------------- Variant C: summoning orb
def _prongs(sp, x, y, col):
    sp.line(x - 3, y + 4, x - 4, y - 2, col, 1); sp.line(x + 3, y + 4, x + 4, y - 2, col, 1)


def c_rod():
    sp = Sprite(S, S)
    shaft(sp, (5, 35), (24, 16), K(190, 110, 60), 2, K(240, 170, 110))
    sp.rect(22, 14, 26, 16, K(190, 110, 60))
    _prongs(sp, 26, 12, K(190, 110, 60))
    sp.circle(26, 9, 3, K(60, 200, 190)); sp.circle(26, 9, 1, K(200, 255, 245)); sp.px(24, 7, K(220, 255, 250))
    return finish(sp)


def c_cane():
    sp = Sprite(S, S)
    shaft(sp, (4, 36), (22, 18), IRON, 2, K(180, 190, 214))
    end = crook(sp, (22, 17), IRON, 5)
    sp.circle(30, 11, 4, K(90, 110, 240)); sp.circle(30, 11, 2, K(200, 215, 255)); sp.px(28, 9, K(255, 255, 255))
    sp.line(25, 6, 35, 6, K(150, 170, 255), 1)
    sp.px(25, 7, K(150, 170, 255)); sp.px(35, 7, K(150, 170, 255))
    return finish(sp)


def c_staff():
    sp = Sprite(S, S)
    shaft(sp, (3, 37), (25, 15), K(60, 44, 90), 3, K(150, 110, 220))
    for t in (0.3, 0.6):
        x = int(3 + 22 * t); y = int(37 - 22 * t)
        sp.rect(x - 1, y - 1, x + 2, y + 2, GOLD)
    sp.rect(23, 13, 28, 16, GOLD)
    sp.line(21, 11, 21, 4, GOLD, 1); sp.line(34, 15, 38, 8, GOLD, 1)
    sp.circle(30, 9, 6, K(150, 60, 230))
    sp.circle(30, 9, 4, K(210, 130, 255))
    sp.circle(30, 9, 2, K(250, 230, 255))
    sp.px(27, 6, K(255, 255, 255))
    for p in [(22, 2), (37, 3), (24, 1), (36, 15), (20, 7)]:
        sp.px(p[0], p[1], K(250, 220, 120))
    sp.line(24, 9, 36, 9, GOLD, 1)                       # ring
    return finish(sp)


STAVES = {
    'sentry_A_banner': [('T1 Rod', a_rod), ('T2 Cane', a_cane), ('T3 Staff', a_staff)],
    'sentry_B_tower': [('T1 Rod', b_rod), ('T2 Cane', b_cane), ('T3 Staff', b_staff)],
    'sentry_C_orb': [('T1 Rod', c_rod), ('T2 Cane', c_cane), ('T3 Staff', c_staff)],
}

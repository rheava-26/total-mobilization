"""Levy 2.0 potions, v2: built on vanilla's chunky 2x2-pixel grid.

Measured from Eve's recipe sheet (Builder Potion + purple triangle potion):
  * vanilla potions are drawn on a 10-col x 15-row grid of 2x2 true pixels (20x30 true size)
  * rows 0-6 are the neck: cap top (2w), cap (4w, 2 rows), gap (2w, dark), collar (4w),
    flange (6w incl. dark wings), neck base (4w, dark) -- 7 rows, always identical
  * rows 7-14 are the body (8 rows). Builder = 10w x 8h box: 1-cell dark tinted outline,
    8x6 liquid, a pale highlight stripe down the left, a few pale glints.
  * purple triangle body widths per row: 4,6,6,8,8,10,10,8; the hole is a 2x1 transparent slot
    with a 2-cell dark lintel above it and dark cells either side.
Everything is built on the 12x15 grid (1 col margin) then upscaled 2x to a 24x30 sprite;
the round hole and glints are finished at true-pixel resolution.
"""
from lib import *

GW, GH = 12, 15           # grid size
OX = 1                    # grid column offset (body cols 0-9 -> 1-10)
W, H = GW * 2, GH * 2     # 24 x 30 true size

# vanilla grey neck palette (sampled from the sheet)
D0 = rgb(34, 36, 54)
D1 = rgb(52, 54, 72)
D2 = rgb(86, 89, 108)
M1 = rgb(130, 134, 156)
L1 = rgb(164, 168, 198)
L2 = rgb(192, 196, 226)
WH = rgb(232, 235, 255)

# (local col, colour) per neck row, measured from the reference
NECK = [
    {4: D2, 5: D2},
    {3: D2, 4: L2, 5: L1, 6: D2},
    {3: D1, 4: WH, 5: L2, 6: D1},
    {4: D0, 5: D0},
    {3: D2, 4: M1, 5: M1, 6: D2},
    {2: D0, 3: L2, 4: WH, 5: L1, 6: M1, 7: D0},
    {3: D0, 4: D1, 5: D1, 6: D0},
]


class Grid:
    def __init__(s):
        s.cells = {}

    def set(s, c, r, col):
        s.cells[(c, r)] = col

    def neck(s):
        for r, row in enumerate(NECK):
            for c, col in row.items():
                s.set(c, r, col)

    def render(s):
        sp = Sprite(W, H)
        for (c, r), col in s.cells.items():
            x, y = (c + OX) * 2, r * 2
            sp.rect(x, y, x + 1, y + 1, col)
        return sp


# ------------------------------------------------------------------ SPECTATOR
# Builder body luminance map (rows 7..14, cols 0..9), read off the reference.
BUILDER_LUM = [
    [71, 73, 47, 71, 85, 86, 70, 47, 47, 72],
    [73, 113, 171, 133, 114, 114, 115, 135, 114, 47],
    [46, 205, 113, 100, 85, 85, 101, 173, 135, 26],
    [46, 206, 100, 86, 134, 73, 85, 101, 116, 26],
    [46, 205, 132, 73, 71, 72, 85, 85, 133, 26],
    [25, 172, 100, 86, 70, 170, 71, 86, 172, 26],
    [27, 115, 133, 114, 115, 133, 135, 171, 114, 26],
    [26, 27, 27, 27, 26, 26, 28, 26, 27, 26],
]
# white liquid ramp (cool, slightly blue shadows) and dark blue-grey tinted outline
WL = [rgb(170, 178, 216), rgb(204, 211, 238), rgb(226, 231, 249), rgb(244, 246, 255), rgb(255, 255, 255)]
WO = [rgb(40, 44, 84), rgb(68, 76, 122), rgb(104, 112, 162)]


def white_ramp(l):
    return WL[0] if l <= 90 else WL[1] if l <= 120 else WL[2] if l <= 140 else WL[3] if l <= 180 else WL[4]


def outline_ramp(l):
    return WO[0] if l <= 30 else WO[1] if l <= 50 else WO[2]


def spectator_a():
    """Builder silhouette, neck and highlight pattern, white liquid."""
    g = Grid()
    g.neck()
    for i, row in enumerate(BUILDER_LUM):
        r = 7 + i
        for c, l in enumerate(row):
            edge = c in (0, 9) or i in (0, 7)
            g.set(c, r, outline_ramp(l) if edge else white_ramp(l))
    return g.render()


def spectator_b():
    """Builder silhouette with thick glass: a pale-blue glass wall (extra thick base) around
    a smaller white liquid core."""
    g = Grid()
    g.neck()
    GLASS = [rgb(120, 144, 196), rgb(158, 182, 224), rgb(196, 214, 244)]
    for r in range(7, 15):
        for c in range(10):
            if c in (0, 9) or r in (7, 14):
                g.set(c, r, outline_ramp(26 if (r == 14 or c == 9) else 47))
                continue
            wall = c in (1, 8) or r in (8, 13)
            if wall:
                col = GLASS[2] if (c == 1 or r == 8) else GLASS[0]
                if c == 1 and r in (9, 10):
                    col = rgb(236, 244, 255)
            else:
                col = WL[3] if (c < 6 and r < 12) else WL[2]
                if c >= 6 and r >= 11:
                    col = WL[1]
                if c == 2 and r == 9:
                    col = WL[4]
            g.set(c, r, col)
    # inner liquid rim (dark inner edge) at the top and left of the core
    for c in range(2, 8):
        g.set(c, 9, WL[4] if c == 2 else WL[2] if c < 6 else WL[1])
    sp = g.render()
    return sp


# ------------------------------------------------------------------ SEEKER
P_OUT = [rgb(26, 12, 52), rgb(40, 18, 76), rgb(62, 30, 112)]
P_FILL = [rgb(62, 26, 118), rgb(84, 38, 148), rgb(110, 60, 184)]
P_LT = rgb(166, 118, 244)
P_HI = rgb(206, 170, 255)
TRI_W = [10, 10, 8, 8, 6, 6, 4, 2]           # body rows 7..14, point down


def seeker_body(g, hole_slot=False):
    for i, w in enumerate(TRI_W):
        r = 7 + i
        a = 5 - w // 2
        b = a + w - 1
        for c in range(a, b + 1):
            h = ((c * 7 + r * 13) % 5)
            if i == 0:                                   # top rim row
                col = P_OUT[2] if c not in (a, b) else P_OUT[1]
            elif c in (a, b):                            # stepped outline
                col = P_OUT[0] if (c == b or i == 7) else P_OUT[1]
            elif c == a + 1:                             # lit left edge
                col = P_LT if i in (2, 3, 4) else P_FILL[2]
            elif c == b - 1:                             # shaded right edge
                col = P_FILL[0]
            else:
                col = P_FILL[1] if h < 3 else (P_FILL[0] if h == 3 else P_FILL[2])
            g.set(c, r, col)
    g.set(2, 8, P_HI)
    g.set(2, 9, P_LT)
    g.set(4, 8, P_FILL[2])
    g.set(3, 9, P_FILL[2])
    if hole_slot:
        # vanilla slot: dark lintel above, 2x1 hole, dark flanks, darker row below
        for c in (4, 5):
            g.set(c, 9, P_OUT[0])
            g.cells.pop((c, 10), None)
            g.set(c, 11, P_FILL[0])
        g.set(3, 10, P_OUT[1])
        g.set(6, 10, P_OUT[1])
        g.set(3, 9, P_OUT[2])
        g.set(6, 9, P_OUT[2])


def seeker_a():
    """Inverted equilateral triangle, deep purple, round-ish hole at the centroid."""
    g = Grid()
    g.neck()
    seeker_body(g)
    sp = g.render()
    # true-res round hole: 4x4 octagon (2,4,4,2) in a dark ring, glint on lower-left rim
    cx = OX * 2 + 10            # grid col 5 boundary -> true x 12
    y0 = 17
    rows = {0: (5, 7), 1: (4, 8), 2: (4, 8), 3: (4, 8), 4: (4, 8), 5: (5, 7)}   # dark ring, relative to cx-6
    for dy, (a, b) in rows.items():
        for dx in range(a, b):
            sp.px(cx - 6 + dx, y0 - 1 + dy, P_OUT[0])
    # clean octagon: widths 2,4,4,2 centred on cx
    for dy, wd in enumerate((2, 4, 4, 2)):
        for dx in range(wd):
            sp.px(cx - wd // 2 + dx, y0 + dy, (0, 0, 0, 0))
    # lit rim glint bottom-left, dark lintel on top
    sp.px(cx - 3, y0 + 2, P_LT)
    sp.px(cx - 2, y0 + 3, P_LT)
    sp.px(cx + 2, y0 + 2, P_OUT[1])
    return sp


def seeker_b():
    """Vanilla-style slot hole: 2x1 grid cell (4x2 true px) with a dark lintel above."""
    g = Grid()
    g.neck()
    seeker_body(g, hole_slot=True)
    return g.render()


POTIONS = {
    'spectator': [('A plain white box', spectator_a), ('B thick glass wall', spectator_b)],
    'seeker': [('A round hole', seeker_a), ('B slot hole', seeker_b)],
}

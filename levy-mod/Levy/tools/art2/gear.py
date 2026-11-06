"""GoPro, GoPro indicator, Radio, Static Camera (item + tile), Guide to Autonomy."""
from lib import *

K = rgb
STEEL = K(150, 156, 172)
DARK = K(44, 48, 58)
MID = K(70, 76, 90)
LENS = K(48, 120, 210)
LENS_HI = K(150, 210, 255)
RED = K(220, 40, 40)
OLIVE = K(88, 100, 56)
OLIVE_D = K(62, 72, 40)
OLIVE_L = K(122, 136, 76)
WOOD = K(140, 92, 48)
WOOD_D = K(96, 60, 30)
BRASS = K(220, 176, 60)


def lens(sp, cx, cy, r, glass=LENS, ring=MID):
    sp.circle(cx, cy, r + 1, ring)
    sp.circle(cx, cy, r, shade(glass, 0.8))
    sp.circle(cx, cy, r - 1 if r > 2 else 1, glass)
    sp.px(cx - r // 2 - 1, cy - r // 2 - 1, LENS_HI)
    sp.px(cx - r // 2, cy - r // 2 - 1, LENS_HI)
    sp.px(cx - r // 2 - 1, cy - r // 2, LENS_HI)


# ----------------------------------------------------------------- GOPRO
def gopro_a():
    """Classic black action cam: big lens, front status screen, red record button, 3-prong mount."""
    sp = Sprite(26, 26)
    sp.rect(3, 6, 22, 18, DARK)
    sp.rect(3, 6, 22, 7, MID)
    sp.rect(7, 4, 10, 5, RED)                          # shutter
    lens(sp, 10, 13, 4)
    sp.rect(16, 9, 20, 12, K(40, 130, 90))             # status screen
    sp.rect(17, 10, 19, 10, K(150, 255, 190))
    sp.px(17, 15, RED); sp.px(19, 15, K(240, 200, 60))
    sp.rect(5, 19, 6, 22, DARK); sp.rect(10, 19, 11, 22, DARK); sp.rect(15, 19, 16, 22, DARK)
    sp.rect(4, 22, 17, 23, MID)
    return finish(sp)


def gopro_b():
    """Cage/housing version: orange mounting frame, screw knob, clear housing around lens."""
    sp = Sprite(26, 26)
    sp.rect(2, 5, 23, 19, K(224, 120, 30))             # orange frame
    sp.rect(4, 7, 21, 17, DARK)
    lens(sp, 12, 12, 4)
    sp.rect(15, 8, 19, 10, K(40, 130, 90))
    sp.px(17, 9, K(150, 255, 190))
    sp.rect(10, 20, 15, 22, K(224, 120, 30))           # base
    sp.circle(13, 23, 1, STEEL)                        # thumb screw
    sp.rect(6, 3, 9, 4, RED)
    return finish(sp)


def gopro_c():
    """On a leather helmet strap, REC light lit: 'worn by a levy'."""
    sp = Sprite(26, 26)
    sp.rect(1, 17, 24, 22, K(120, 78, 40))             # strap
    sp.rect(1, 17, 24, 18, K(160, 108, 56))
    sp.rect(10, 16, 14, 23, STEEL)                     # buckle
    sp.rect(11, 18, 13, 21, K(120, 78, 40))
    sp.rect(4, 5, 21, 16, DARK)
    sp.rect(4, 5, 21, 6, MID)
    lens(sp, 10, 11, 3)
    sp.rect(15, 8, 19, 11, K(40, 130, 90))
    sp.px(17, 9, K(150, 255, 190))
    sp.rect(18, 7, 19, 7, RED)
    sp.rect(5, 2, 7, 3, RED)
    sp.px(6, 2, K(255, 140, 140))
    return finish(sp)


def indicator_a():
    """Small camera glyph with a red record dot."""
    sp = Sprite(18, 18)
    sp.rect(2, 5, 13, 12, DARK)
    sp.rect(2, 5, 13, 5, MID)
    sp.circle(7, 9, 2, LENS)
    sp.px(6, 8, LENS_HI)
    sp.rect(14, 6, 15, 11, MID)
    sp.circle(13, 3, 2, RED)
    sp.px(12, 2, K(255, 170, 170))
    return finish(sp)


def indicator_b():
    """Helmet with a lens clipped on the brow: the levy carrying the cam."""
    sp = Sprite(18, 18)
    sp.ell(2, 3, 15, 15, K(96, 110, 70))
    sp.rect(2, 10, 15, 15, K(96, 110, 70))
    sp.rect(1, 12, 16, 14, K(66, 78, 46))
    sp.rect(2, 4, 8, 5, K(140, 156, 100))
    sp.circle(12, 8, 2, LENS)
    sp.px(11, 7, LENS_HI)
    sp.rect(15, 5, 16, 6, RED)
    return finish(sp)


def indicator_c():
    """Viewfinder brackets around a red REC dot: 'being watched'."""
    sp = Sprite(18, 18)
    for (x, y, dx, dy) in ((2, 2, 1, 1), (15, 2, -1, 1), (2, 15, 1, -1), (15, 15, -1, -1)):
        for i in range(4):
            sp.px(x + dx * i, y, LENS_HI); sp.px(x, y + dy * i, LENS_HI)
    sp.circle(8, 8, 3, RED)
    sp.px(7, 7, K(255, 180, 180))
    return finish(sp, do_bevel=False, f=0.35)


# ----------------------------------------------------------------- RADIO
def radio_a():
    """Olive field radio set: handle, speaker grille, tuning dial, knobs, long whip antenna."""
    sp = Sprite(30, 36)
    sp.line(22, 12, 25, 1, MID, 1)                     # antenna
    sp.rect(24, 0, 26, 1, K(200, 200, 210))
    sp.rect(8, 8, 17, 9, DARK)                         # handle
    sp.rect(8, 8, 9, 12, DARK); sp.rect(16, 8, 17, 12, DARK)
    sp.rect(4, 12, 24, 32, OLIVE)
    sp.rect(4, 12, 24, 13, OLIVE_L)
    sp.rect(4, 31, 24, 32, OLIVE_D)
    for y in (15, 17, 19):                             # grille
        sp.rect(7, y, 15, y, OLIVE_D)
    sp.rect(7, 22, 15, 26, DARK)                       # display
    sp.rect(8, 23, 14, 25, K(120, 200, 110))
    sp.rect(9, 24, 11, 24, K(200, 255, 170))
    sp.circle(20, 17, 2, K(190, 195, 205))
    sp.px(19, 16, K(250, 250, 255))
    sp.circle(20, 24, 2, K(190, 195, 205))
    sp.px(19, 23, K(250, 250, 255))
    sp.rect(7, 28, 9, 29, RED)                         # PTT
    sp.rect(12, 28, 16, 29, K(220, 190, 70))           # stencil
    return finish(sp)


def radio_b():
    """Slim handheld walkie-talkie: stubby antenna, grille, screen, push-to-talk bar."""
    sp = Sprite(24, 36)
    sp.rect(15, 1, 16, 9, MID)
    sp.rect(15, 0, 16, 0, K(210, 210, 220))
    sp.rect(5, 9, 17, 33, OLIVE)
    sp.rect(5, 9, 17, 10, OLIVE_L)
    sp.rect(5, 32, 17, 33, OLIVE_D)
    sp.rect(2, 15, 4, 21, DARK)                        # PTT side bar
    sp.rect(2, 15, 2, 21, MID)
    for y in (12, 14, 16, 18):
        sp.rect(8, y, 14, y, OLIVE_D)
    sp.rect(8, 21, 14, 25, DARK)
    sp.rect(9, 22, 13, 24, K(120, 200, 110))
    sp.px(10, 23, K(200, 255, 170))
    sp.px(8, 28, RED); sp.rect(10, 28, 11, 29, K(190, 195, 205)); sp.rect(13, 28, 14, 29, K(190, 195, 205))
    sp.rect(10, 30, 14, 31, OLIVE_D)
    return finish(sp)


def radio_c():
    """Backpack radio with coiled handset on top and a flag-tipped antenna."""
    sp = Sprite(32, 36)
    sp.line(24, 12, 26, 2, MID, 1)
    sp.poly([(27, 2), (31, 3), (27, 5)], RED)
    sp.rect(4, 13, 24, 32, OLIVE_D)
    sp.rect(4, 13, 24, 14, OLIVE)
    sp.rect(6, 16, 22, 29, OLIVE)
    sp.rect(6, 16, 22, 17, OLIVE_L)
    sp.rect(8, 19, 14, 23, DARK)
    sp.rect(9, 20, 13, 22, K(120, 200, 110))
    sp.circle(18, 20, 2, K(190, 195, 205)); sp.px(17, 19, K(250, 250, 255))
    sp.circle(18, 26, 2, K(190, 195, 205)); sp.px(17, 25, K(250, 250, 255))
    sp.rect(8, 26, 13, 27, OLIVE_D)
    # handset on top, cord looping down
    sp.rect(5, 9, 15, 12, DARK)
    sp.rect(3, 8, 6, 13, MID); sp.rect(14, 8, 17, 13, MID)
    for i, y in enumerate(range(13, 31, 2)):
        sp.px(2 if i % 2 else 3, y, K(30, 30, 36)); sp.px(3 if i % 2 else 2, y + 1, K(30, 30, 36))
    sp.rect(26, 15, 28, 30, K(120, 78, 40))            # side strap
    return finish(sp)


# ----------------------------------------------------------------- STATIC CAMERA
def _tripod(sp, ax, ay, spread, h, leg=MID, w=1):
    cx = ax
    for dx in (-spread, 0, spread):
        sp.line(ax, ay, cx + dx, ay + h, leg, w)
    for dx in (-spread, spread):
        sp.rect(cx + dx - 1, ay + h, cx + dx + 1, ay + h, DARK)
    sp.rect(ax - 1, ay + h, ax + 1, ay + h, DARK)


def cam_item_a():
    """Item: boxy video camera on a small tripod."""
    sp = Sprite(32, 32)
    _tripod(sp, 14, 17, 7, 12)
    sp.rect(5, 6, 20, 16, K(70, 76, 92))
    sp.rect(5, 6, 20, 7, K(110, 118, 138))
    sp.rect(20, 8, 26, 14, DARK)
    lens(sp, 24, 11, 2)
    sp.rect(8, 3, 15, 5, K(40, 44, 54))                # top viewfinder
    sp.rect(7, 9, 9, 11, RED); sp.px(8, 9, K(255, 160, 160))
    sp.rect(11, 17, 16, 18, STEEL)
    return finish(sp)


def cam_tile_a():
    """Tile 2x3 (32x48): tall tripod, big video camera, red tally lamp."""
    sp = Sprite(32, 48)
    _tripod(sp, 15, 22, 11, 24, w=2)
    sp.rect(14, 20, 16, 24, STEEL)
    sp.rect(4, 6, 22, 21, K(70, 76, 92))
    sp.rect(4, 6, 22, 8, K(116, 124, 146))
    sp.rect(22, 9, 29, 18, DARK)
    sp.rect(29, 10, 30, 17, MID)
    lens(sp, 26, 13, 3)
    sp.rect(8, 2, 17, 5, K(40, 44, 54))
    sp.rect(7, 11, 10, 15, RED); sp.px(8, 11, K(255, 170, 170))
    sp.rect(12, 12, 18, 17, K(40, 130, 90))
    sp.rect(13, 13, 17, 13, K(150, 255, 190))
    return finish(sp)


def cam_item_b():
    """Item: dome-less photo camera with flash block on tripod."""
    sp = Sprite(32, 32)
    _tripod(sp, 15, 18, 8, 11)
    sp.rect(7, 7, 24, 17, K(60, 64, 76))
    sp.rect(7, 7, 24, 9, K(180, 184, 200))
    lens(sp, 15, 13, 4)
    sp.rect(20, 4, 24, 6, K(250, 240, 170))            # flash
    sp.rect(8, 4, 11, 6, RED)
    return finish(sp)


def cam_tile_b():
    sp = Sprite(32, 48)
    _tripod(sp, 15, 20, 12, 26, w=2)
    sp.rect(3, 6, 28, 21, K(60, 64, 76))
    sp.rect(3, 6, 28, 9, K(180, 184, 200))
    lens(sp, 15, 15, 5)
    sp.rect(21, 2, 27, 5, K(250, 240, 170))
    sp.rect(5, 3, 10, 5, RED)
    sp.rect(22, 13, 26, 18, DARK)
    sp.rect(23, 14, 25, 14, K(150, 255, 190))
    return finish(sp)


def cam_item_c():
    """Item: surveillance-style camera on a pole/tripod with sunshade."""
    sp = Sprite(32, 32)
    _tripod(sp, 14, 17, 7, 12)
    sp.poly([(3, 7), (22, 5), (24, 15), (4, 16)], K(200, 204, 216))
    sp.rect(22, 6, 27, 14, DARK)
    lens(sp, 25, 10, 2)
    sp.rect(5, 17, 20, 18, STEEL)
    sp.circle(6, 11, 1, RED)
    return finish(sp)


def cam_tile_c():
    sp = Sprite(32, 48)
    _tripod(sp, 14, 24, 11, 22, w=2)
    sp.rect(12, 22, 16, 26, STEEL)
    sp.poly([(1, 7), (21, 4), (24, 20), (2, 22)], K(200, 204, 216))
    sp.poly([(1, 7), (21, 4), (21, 6), (1, 9)], K(236, 240, 250))
    sp.rect(22, 7, 29, 18, DARK)
    lens(sp, 26, 12, 3)
    sp.circle(6, 14, 2, RED)
    sp.px(5, 13, K(255, 170, 170))
    return finish(sp)


# ----------------------------------------------------------------- GUIDE TO AUTONOMY
def _book(cover, trim, emblem, drip=False):
    sp = Sprite(30, 34)
    sp.rect(5, 3, 23, 28, cover)
    sp.rect(5, 3, 8, 28, shade(cover, 0.7))            # spine
    sp.rect(9, 3, 9, 28, shade(cover, 1.2))
    sp.rect(24, 5, 26, 29, K(238, 226, 190))           # page block (right)
    for y in range(7, 29, 3):
        sp.rect(24, y, 26, y, K(200, 186, 150))
    sp.rect(7, 29, 25, 30, K(238, 226, 190))           # page block (bottom)
    sp.rect(7, 30, 25, 30, K(200, 186, 150))
    sp.rect(11, 5, 21, 5, trim); sp.rect(11, 26, 21, 26, trim)   # trim bars
    sp.rect(11, 5, 11, 26, trim); sp.rect(21, 5, 21, 26, trim)
    emblem(sp)
    if drip:
        sp.rect(12, 3, 20, 5, K(70, 150, 255))
        sp.rect(13, 6, 14, 10, K(70, 150, 255)); sp.rect(18, 6, 19, 8, K(70, 150, 255))
        sp.px(13, 11, K(70, 150, 255)); sp.px(13, 4, K(180, 220, 255)); sp.px(14, 4, K(180, 220, 255))
    return sp


def book_a():
    """Green cover, gold cog: 'autonomy' as machinery."""
    def cog(sp):
        cx, cy = 16, 16
        sp.circle(cx, cy, 4, BRASS)
        for (dx, dy) in ((0, -5), (0, 5), (-5, 0), (5, 0), (-4, -4), (4, -4), (-4, 4), (4, 4)):
            sp.rect(cx + dx - (1 if abs(dx) != 5 else 0), cy + dy - (1 if abs(dy) != 5 else 0),
                    cx + dx, cy + dy, BRASS)
        sp.circle(cx, cy, 1, K(40, 90, 50))
    return finish(_book(K(52, 120, 72), BRASS, cog))


def book_b():
    """Navy cover, silver eye: watching over levies who act on their own."""
    def eye(sp):
        sp.rect(12, 15, 20, 17, K(230, 236, 250))
        sp.rect(13, 14, 19, 14, K(230, 236, 250)); sp.rect(13, 18, 19, 18, K(230, 236, 250))
        sp.rect(15, 14, 17, 18, K(70, 130, 220))
        sp.rect(16, 15, 16, 17, K(20, 24, 60))
    return finish(_book(K(40, 60, 130), K(190, 196, 215), eye))


def book_c():
    """Brown cover, little banner-levy figure, dripping slime (dropped in Slime Rain)."""
    def levy(sp):
        sp.rect(14, 12, 18, 14, K(230, 190, 150))      # head
        sp.rect(13, 11, 19, 11, K(150, 156, 170))      # helmet
        sp.rect(14, 15, 18, 22, K(90, 110, 70))        # body
        sp.rect(19, 12, 19, 22, K(120, 78, 40))        # pole
        sp.rect(20, 12, 21, 15, RED)
        sp.rect(14, 23, 15, 24, K(60, 50, 40)); sp.rect(17, 23, 18, 24, K(60, 50, 40))
    return finish(_book(K(124, 76, 40), BRASS, levy, drip=True))


GEAR = {
    'gopro': [('A black action cam', gopro_a), ('B orange cage', gopro_b), ('C on helmet strap', gopro_c)],
    'gopro_indicator': [('A cam + REC dot', indicator_a), ('B helmet + lens', indicator_b), ('C viewfinder REC', indicator_c)],
    'radio': [('A olive field radio', radio_a), ('B walkie-talkie', radio_b), ('C backpack set + handset', radio_c)],
    'static_camera_item': [('A video cam', cam_item_a), ('B photo cam', cam_item_b), ('C security cam', cam_item_c)],
    'static_camera_tile': [('A video cam', cam_tile_a), ('B photo cam', cam_tile_b), ('C security cam', cam_tile_c)],
    'guide_to_autonomy': [('A green + cog', book_a), ('B navy + eye', book_b), ('C brown + levy + slime', book_c)],
}

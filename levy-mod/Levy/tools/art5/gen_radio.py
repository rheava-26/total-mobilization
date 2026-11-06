"""Command Radio, redrawn on vanilla's 2x2 pixel grid (logical 15x20 -> 30x40 texture).
Light from the top-left, tone ramps, direction-dependent outline. Run: python3 gen_radio.py <out.png> [preview_dir]"""
import sys, os
from PIL import Image

W, H = 15, 20
img = [[None] * W for _ in range(H)]

# ramps (light -> dark)
OLIVE = [(186, 198, 96), (150, 164, 70), (112, 124, 52), (82, 92, 40), (58, 66, 32)]
OL_LIGHT = (66, 74, 34)    # outline on lit (top/left) edges
OL_DARK = (26, 30, 16)     # outline on shadow (bottom/right) edges
GREY = [(214, 218, 228), (170, 176, 192), (128, 134, 152), (88, 94, 112), (58, 62, 78)]
KNOB_OL_L = (74, 80, 98)
KNOB_OL_D = (28, 30, 40)
LCD_BEZEL = [(70, 74, 100), (46, 50, 72), (30, 32, 48)]
LCD = [(190, 236, 160), (134, 208, 118), (92, 168, 90)]
RED = [(255, 110, 100), (226, 44, 44), (140, 20, 28)]


def shape_mask(xs, ys, cut):
    m = set()
    for y in range(ys[0], ys[1] + 1):
        for x in range(xs[0], xs[1] + 1):
            m.add((x, y))
    for c in cut:
        m.discard(c)
    return m


def paint(mask, tone_fn, ol_l, ol_d):
    """Fill mask: ring pixels get an outline whose tone depends on which side is open; interior via tone_fn(x,y)."""
    for (x, y) in mask:
        outs = [(dx, dy) for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)) if (x + dx, y + dy) not in mask]
        if outs:
            lit = sum(1 for dx, dy in outs if dx < 0 or dy < 0)
            dark = len(outs) - lit
            img[y][x] = ol_l if lit > dark else ol_d if dark > lit else tuple((a + b) // 2 for a, b in zip(ol_l, ol_d))
        else:
            img[y][x] = tone_fn(x, y)


# --- side knob / clip (left), drawn first so the body overlaps it
knob = shape_mask((0, 3), (9, 15), [(0, 9), (0, 15), (3, 9), (3, 15)])
paint(knob, lambda x, y: GREY[1] if y <= 11 and x <= 1 else GREY[2] if y <= 13 else GREY[3], KNOB_OL_L, KNOB_OL_D)
for y in (11, 12):
    img[y][1] = GREY[0]

# --- antenna (x 9..12 incl. outline), tall, sticks up from the body's top-right
ant = shape_mask((9, 12), (0, 6), [(9, 0), (12, 0)])
paint(ant, lambda x, y: (GREY[0] if y == 1 else GREY[1] if x == 10 else GREY[3]), KNOB_OL_L, KNOB_OL_D)
img[1][10] = (250, 252, 255)  # tip glint
img[2][10] = GREY[0]

# --- body x 2..12, y 5..19, rounded corners
T, B, L, R = 5, 19, 2, 12
body = shape_mask((L, R), (T, B), [(L, T), (R, T), (L, B), (R, B), (L, B - 1), (R, B - 1), (L + 1, B), (R - 1, B)])


def body_tone(x, y):
    if y == T + 1 and x < R - 1:
        return OLIVE[0] if x < 8 else OLIVE[1]
    if x == L + 1 and y < B - 3:
        return OLIVE[1]
    if y >= B - 1:
        return OLIVE[4]
    if y == B - 2:
        return OLIVE[3]
    if x == R - 1:
        return OLIVE[3]
    if x == R - 2:
        return OLIVE[2] if y > 8 else OLIVE[1]
    s = (x - 3) / 9.0 * 0.5 + (y - T) / 14.0 * 0.5
    return OLIVE[1] if s < 0.2 else OLIVE[2]


paint(body, body_tone, OL_LIGHT, OL_DARK)
for x in range(4, 11):
    img[T][x] = (96, 108, 46) if x < 8 else (74, 84, 38)

# --- speaker grille: 3 slots
for y in (7, 9, 11):
    for x in range(4, 11):
        img[y][x] = OLIVE[4]
    img[y][4] = OLIVE[3]
    for x in range(4, 11):
        img[y + 1][x] = OLIVE[1] if x < 9 else OLIVE[2]

# --- LCD: bezel rows 12..15, screen 13..14
for y in range(12, 16):
    for x in range(4, 11):
        edge = y in (12, 15) or x in (4, 10)
        img[y][x] = LCD_BEZEL[0] if (edge and (y == 12 or x == 4)) else LCD_BEZEL[2] if edge else LCD[2]
for y in (13, 14):
    for x in range(5, 10):
        img[y][x] = LCD[1]
for x in range(5, 10):
    img[13][x] = LCD[0] if x < 8 else LCD[1]
img[14][9] = LCD[2]
img[14][8] = LCD[2]
img[13][5] = (236, 255, 220)

# --- LED + two buttons
img[16][4] = RED[0]
img[17][4] = RED[2]
for bx in (6, 9):
    img[16][bx] = GREY[0]; img[16][bx + 1] = GREY[1]
    img[17][bx] = GREY[2]; img[17][bx + 1] = GREY[3]

out = sys.argv[1]
tex = Image.new('RGBA', (W * 2, H * 2), (0, 0, 0, 0))
for y in range(H):
    for x in range(W):
        c = img[y][x]
        if c:
            for dy in (0, 1):
                for dx in (0, 1):
                    tex.putpixel((x * 2 + dx, y * 2 + dy), c + (255,))
tex.save(out)

if len(sys.argv) > 2:
    pd = sys.argv[2]
    bg = (30, 30, 42, 255)
    big = Image.new('RGBA', (tex.width * 8 + 40, tex.height * 8 + 40), bg)
    b = tex.resize((tex.width * 8, tex.height * 8), Image.NEAREST)
    big.paste(b, (20, 20), b)
    big.save(os.path.join(pd, 'radio_v5_8x.png'))
    old = sys.argv[3] if len(sys.argv) > 3 else None
    sm = Image.new('RGBA', (160, 80), bg)
    sm.paste(tex, (20, 20), tex)
    if old:
        o = Image.open(old).convert('RGBA')
        sm.paste(o, (80, 25), o)
    sm = sm.resize((sm.width * 3, sm.height * 3), Image.NEAREST)
    sm.save(os.path.join(pd, 'radio_v5_1x.png'))

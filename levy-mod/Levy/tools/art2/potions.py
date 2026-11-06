from lib import *

W, H = 32, 34
CX = 16
G_DK = rgb(74, 76, 94)
G_MD = rgb(138, 140, 160)
G_LT = rgb(190, 192, 214)
G_HI = rgb(232, 234, 255)


def neck(sp, top=1, base=12):
    """Vanilla-style grey stoppered neck: cap, gap, collar with flange, dark neck base."""
    y = top
    sp.rect(CX - 2, y, CX + 1, y, G_DK)                       # cap top
    sp.rect(CX - 3, y + 1, CX + 2, y + 2, G_LT)               # cap
    sp.rect(CX - 3, y + 1, CX - 2, y + 2, G_HI)
    sp.rect(CX + 1, y + 1, CX + 2, y + 2, G_MD)
    sp.rect(CX - 2, y + 3, CX + 1, y + 3, G_DK)               # gap
    sp.rect(CX - 3, y + 4, CX + 2, y + 4, G_MD)               # collar
    sp.rect(CX - 5, y + 5, CX + 4, y + 5, G_DK)               # flange wings
    sp.rect(CX - 3, y + 5, CX + 2, y + 5, G_LT)
    sp.rect(CX - 3, y + 5, CX - 2, y + 5, G_HI)
    sp.rect(CX - 3, y + 6, CX + 2, y + 6, G_MD)
    sp.rect(CX - 3, y + 7, CX + 2, base - 1, G_DK)            # neck base (runs down into the body)


def hsh(x, y, s=0):
    return ((x * 73856093) ^ (y * 19349663) ^ (s * 83492791)) & 0xFF


def liquid(sp, mask, base, hi=None, speck=0.10, seed=0):
    """Fill mask with shaded liquid: light upper-left, dark lower-right, dithered speckle."""
    ys = [p[1] for p in mask]
    xs = [p[0] for p in mask]
    y0, y1, x0, x1 = min(ys), max(ys), min(xs), max(xs)
    for (x, y) in mask:
        fx = (x - x0) / max(1, x1 - x0)
        fy = (y - y0) / max(1, y1 - y0)
        f = 1.12 - 0.30 * (fx * 0.55 + fy * 0.75)
        h = hsh(x, y, seed)
        if h < 255 * speck:
            f *= 1.18
        elif h > 255 * (1 - speck):
            f *= 0.86
        sp.px(x, y, shade(base, f))


def glints(sp, pts, c=(250, 250, 255, 255)):
    for (x, y) in pts:
        if sp.opaque(x, y) if hasattr(sp, 'opaque') else True:
            sp.px(x, y, c)


def edge_pixels(mask):
    ms = set(mask)
    return [(x, y) for (x, y) in mask if not all(n in ms for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))]


def body_finish(sp, mask, base, tint, hole=None):
    """Darken inner rim (vanilla bottles have a darker glass ring) + dark bottom, then outline."""
    ms = set(mask)
    for (x, y) in mask:
        if (x, y + 1) not in ms or (x + 1, y) not in ms:
            sp.px(x, y, shade(sp.get(x, y), 0.72))
        elif (x, y - 1) not in ms or (x - 1, y) not in ms:
            sp.px(x, y, shade(sp.get(x, y), 1.28))
    outline(sp, 0.30, tint)


def box_mask(x0, y0, x1, y1):
    return [(x, y) for y in range(y0, y1 + 1) for x in range(x0, x1 + 1)]


def tri_down_mask(cx, y0, y1, w0):
    """Inverted equilateral triangle: width w0 at y0 shrinking to ~1-2 at y1."""
    m = []
    n = y1 - y0
    for i in range(n + 1):
        w = max(2, round(w0 * (1 - i / (n + 0.6))))
        if w % 2:
            w += 1
        a = cx - w // 2
        m += [(x, y0 + i) for x in range(a, a + w)]
    return m


def potion(mask, base, tint, hole=None, hi_pts=(), seed=0, neck_top=1, extra=None, speck=0.10):
    sp = Sprite(W, H)
    neck(sp, mask[0][1] - 8, mask[0][1])
    liquid(sp, mask, base, seed=seed, speck=speck)
    if hole:
        for p in hole:
            sp.clear(*p)
    body_finish(sp, mask, base, tint)
    # re-outline neck (outline() above handles all); neck gets deep grey outline automatically
    for p in hi_pts:
        sp.px(p[0], p[1], (255, 255, 255, 255))
    if extra:
        extra(sp)
    return sp


# ----------------------------------------------------------------- SPECTATOR
WHITE = rgb(238, 240, 250)
TINT_W = (70, 90, 150)


def spectator_a():
    """Plain white square box: the Builder silhouette, white liquid."""
    m = box_mask(6, 14, 25, 29)
    return potion(m, WHITE, TINT_W, hi_pts=[(8, 17), (8, 18), (8, 19), (9, 17), (23, 26), (22, 27)], seed=3, speck=0.04)


def spectator_b():
    """Thick-walled box: pale frosted glass frame 2px thick around white liquid, bubbles."""
    sp = Sprite(W, H)
    neck(sp, 6, 14)
    outer = box_mask(5, 14, 26, 30)
    inner = box_mask(8, 17, 23, 27)
    liquid(sp, outer, rgb(176, 196, 222), seed=5)
    liquid(sp, inner, WHITE, speck=0.07, seed=6)
    for (x, y) in inner:                # inner rim shading
        if x == 8 or y == 17:
            sp.px(x, y, shade(WHITE, 0.82))
    for x in range(5, 27):              # thick base
        sp.px(x, 30, shade(rgb(176, 196, 222), 0.7))
    body_finish(sp, outer, WHITE, TINT_W)
    for p in [(7, 16), (7, 17), (7, 18), (10, 19), (10, 20), (21, 24), (20, 25)]:
        sp.px(p[0], p[1], (255, 255, 255, 255))
    for p in [(14, 23), (18, 21), (16, 25)]:
        sp.px(p[0], p[1], rgb(205, 215, 240))
    return sp


def spectator_c():
    """White box with an eye (lens) emblem in the liquid: 'watching'."""
    m = box_mask(6, 14, 25, 29)

    def eye(sp):
        eyec = rgb(96, 110, 170)
        for x in range(11, 21):
            t = abs(x - 15.5) / 5
            h = int(round(2.2 * (1 - t * t)))
            for dy in range(-h, h + 1):
                sp.px(x, 22 + dy, rgb(250, 250, 255) if abs(dy) < h or h == 0 else eyec)
            sp.px(x, 22 - h, eyec)
            sp.px(x, 22 + h, eyec)
        sp.rect(14, 21, 17, 23, rgb(60, 70, 140))
        sp.rect(15, 21, 16, 22, rgb(30, 36, 90))
        sp.px(14, 21, rgb(255, 255, 255))
    return potion(m, WHITE, TINT_W, hi_pts=[(8, 17), (8, 18), (9, 17), (23, 26)], seed=3, extra=eye, speck=0.04)


# ----------------------------------------------------------------- SEEKER
PURP = rgb(112, 52, 190)
TINT_P = (60, 20, 120)


def rows(spec):
    return [(x, y) for y, (a, b) in spec.items() for x in range(a, b + 1)]


def seeker_a():
    """Inverted triangle, deep purple, round hole at the centroid."""
    m = tri_down_mask(CX, 14, 30, 20)
    hole = rows({18: (15, 16), 19: (14, 17), 20: (14, 17), 21: (14, 17), 22: (15, 16)})
    return potion(m, PURP, TINT_P, hole=hole, hi_pts=[(9, 16), (10, 16), (10, 17)], seed=9)


def seeker_b():
    """Hole is itself a small inverted triangle (nested equilateral)."""
    m = tri_down_mask(CX, 14, 30, 20)
    hole = rows({17: (12, 19), 18: (13, 18), 19: (14, 17), 20: (15, 16)})
    return potion(m, PURP, TINT_P, hole=hole, hi_pts=[(9, 16), (10, 16), (10, 17)], seed=9)


def seeker_c():
    """Deeper purple; diamond lens hole with a pale-violet glint."""
    m = tri_down_mask(CX, 14, 30, 20)
    hole = rows({17: (15, 16), 18: (14, 17), 19: (13, 18), 20: (14, 17), 21: (15, 16)})

    def glint(sp):
        for p in [(12, 19), (13, 18), (19, 19), (18, 18)]:
            sp.px(p[0], p[1], rgb(190, 150, 255))
    return potion(m, rgb(86, 30, 160), TINT_P, hole=hole, hi_pts=[(9, 16), (10, 16), (10, 17), (13, 23)], seed=11, extra=glint)


POTIONS = {
    'spectator': [('A plain white box', spectator_a), ('B thick frosted glass', spectator_b), ('C white box + eye', spectator_c)],
    'seeker': [('A round hole', seeker_a), ('B triangle hole', seeker_b), ('C diamond lens hole', seeker_c)],
}

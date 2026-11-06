#!/usr/bin/env python3
"""Combat-drone + turret art, v4 (Mechanization). Vanilla-Terraria look: rounded silhouettes, curved 3-5 step shading, rim light, dark outline, strict 2x2 blocks.

Learned from ~130 vanilla sprites (Martian Drone / Probe / Tesla Turret / Saucer parts / Prime limbs / Gigazapper / weapons / accessories) and applied here:
  * every sprite is authored on a LOGICAL grid (1 block = 2x2 real px) and scaled x2 nearest-neighbour; nothing is rotated at real-pixel resolution.
    Tilt (drone) and aim (turret barrel) are pre-rendered FRAMES rotated on the logical grid, so the 2x2 blocks survive in game.
  * silhouettes come from smooth profiles (cosine-interpolated top / bottom curves, discs) so edges step 1-1-2-3 like vanilla rounds, never boxes.
  * shading is computed from a surface normal against a top-left light and quantised to palette steps, so the bands CURVE with the form
    (crest highlight, mid, shadow, a lighter bounce row on the underside, one glint). Detail (visor, hatch, lamps) is hand-placed on top.
  * outline is the darkest palette tone (vanilla uses #323232 / #2c2c2c, never pure black).
Palettes are exact vanilla values: neutral Destroyer / Prime metal grays plus vanilla's amber lamp ramp. Silhouettes are NOT vanilla: they come from the designer's concept.

All three drone variants share ONE sheet layout and ONE set of mast / LED anchors: swap a variant by replacing the two PNGs.
Usage: gen_drone4.py <outdir>
"""
import math, os, sys
from PIL import Image

PX = 2
BASE_W, BASE_H = 34, 13                    # logical canvas the body is drawn on (centre = 17, 6.5), faces right
FR_W, FR_H = 40, 25                        # logical canvas of one pre-rotated frame (centre = 20, 12.5)
TILT_STEP = 0.16                           # radians per tilt level; levels -2..2 (MechConstants.Drone.MaxTilt = 0.32)
TILT_LEVELS = 5
BODY_FRAMES = 4                            # fan spin frames (rows)
ROT_W, ROT_H, ROT_FRAMES = 24, 8, 5        # rotor sheet frame, logical

def hx(s): return (int(s[1:3], 16), int(s[3:5], 16), int(s[5:7], 16), 255)

# Vanilla Destroyer / Skeletron Prime metal grays (neutral, no Martian violet, no red) and the amber lamp ramp used on vanilla machinery glows.
AMBER = {'b': hx('#9d4a00'), 'c': hx('#f0a60b'), 'l': hx('#f0a60b'), 'w': hx('#ffff80'), 'g': hx('#f0a60b'), 'y': hx('#ffff80'), 'G': hx('#9d4a00')}
def _mk(o, d, m, h, s): return dict({'o': hx(o), 'd': hx(d), 'm': hx(m), 'h': hx(h), 's': hx(s)}, **AMBER)
GRAPHITE = _mk('#2c2c2c', '#414141', '#575757', '#858585', '#bdbdbd')      # A: dark graphite
GUNMETAL = _mk('#2c2c2c', '#575757', '#666666', '#949494', '#dbdbdb')      # B: lighter gunmetal
BLACKSTEEL = _mk('#2c2c2c', '#2e2e32'.replace('#2e2e32', '#414141'), '#575757', '#666666', '#949494')   # C: darkest ramp
MARTIAN = GRAPHITE   # turret keeps the same neutral steel
PROBE = GUNMETAL
CLEAR = (0, 0, 0, 0)
LIGHT = (-0.55, -0.65, 0.52)
_n = math.sqrt(sum(c * c for c in LIGHT)); LIGHT = tuple(c / _n for c in LIGHT)


class Cv:
    def __init__(self, w, h, pal):
        self.w, self.h, self.pal = w, h, pal
        self.im = Image.new("RGBA", (w, h), CLEAR)
    def put(self, x, y, k):
        if 0 <= x < self.w and 0 <= y < self.h: self.im.putpixel((int(x), int(y)), self.pal[k] if isinstance(k, str) else k)
    def get(self, x, y): return self.im.getpixel((x, y)) if 0 <= x < self.w and 0 <= y < self.h else CLEAR
    def outline(self, mask, k='o'):
        for (x, y) in mask:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                n = (x + dx, y + dy)
                if n not in mask: self.put(n[0], n[1], k)
    def paint(self, rows):
        for y, x0, s in rows:
            for i, ch in enumerate(s):
                if ch != '.': self.put(x0 + i, y, ch)
    def scaled(self): return self.im.resize((self.w * PX, self.h * PX), Image.NEAREST)


def cosint(pts, x):
    if x <= pts[0][0]: return pts[0][1]
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        if x <= x1:
            t = (x - x0) / (x1 - x0); t = (1 - math.cos(t * math.pi)) / 2
            return y0 + (y1 - y0) * t
    return pts[-1][1]


def ramp(b, th):
    if b > th[2]: return 's'
    if b > th[0]: return 'h'
    if b > th[1]: return 'm'
    return 'd'


def lit(nx, ny, nz):
    m = math.sqrt(nx * nx + ny * ny + nz * nz) or 1
    return (nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]) / m


# ---------------------------------------------------------------------------------------------
# DRONE BODY
# ---------------------------------------------------------------------------------------------
def shade_hull(cv, v):
    top, bot, x0, x1 = v["top"], v["bot"], v["x0"], v["x1"]
    mask = set(); info = {}
    for x in range(x0, x1 + 1):
        t, b = cosint(top, x + 0.5), cosint(bot, x + 0.5)
        for y in range(BASE_H):
            if t <= y + 0.5 <= b: mask.add((x, y)); info[(x, y)] = (t, b)
    for (x, y) in mask:
        t, b = info[(x, y)]
        u = (y + 0.5 - t) / max(0.5, b - t)
        ft = cosint(top, x + 1.0) - cosint(top, x); fb = cosint(bot, x + 1.0) - cosint(bot, x)
        fn_t = ft / math.sqrt(1 + ft * ft); fn_b = fb / math.sqrt(1 + fb * fb)
        ny = 2 * u - 1
        nz = math.sqrt(max(0.0, 1 - ny * ny))
        nx = (1 - u) * fn_t - u * fn_b
        if x > x1 - 3: nx += 0.7 * ((x + 0.5) - (x1 - 3)) / 4.0       # the nose turns away from the light
        bb = lit(nx, ny, nz)
        k = ramp(bb, v["th"])
        if (x, y + 1) not in mask and (b - t) > 2.5 and k == 'd': k = 'm'   # lighter bounce row on the underside
        cv.put(x, y, k)
    cv.outline(mask)
    return mask


def ring(cv, cx, cy, r, fan):
    inner = r - 1.9
    disc = {(x, y) for y in range(cv.h) for x in range(cv.w) if math.hypot(x + 0.5 - cx, y + 0.5 - cy) <= r}
    for (x, y) in disc:
        d = math.hypot(x + 0.5 - cx, y + 0.5 - cy); ang = math.atan2(y + 0.5 - cy, x + 0.5 - cx)
        if d > inner:
            t = math.cos(ang + 3 * math.pi / 4)                 # 1 at the top-left of the rim
            cv.put(x, y, 's' if (t > 0.95 and d > r - 1.2) else ('h' if t > 0.55 else ('m' if t > -0.2 else 'd')))
        else:
            t = math.cos(ang - math.pi / 4)                       # inner wall lit at the bottom-right
            cv.put(x, y, 'd' if (t > 0.7 and d > inner - 1.1) else 'o')
    cv.outline(disc)
    for i in range(3):
        a = fan + i * 2 * math.pi / 3
        for j in range(1, 4):
            t = j / 3 * (inner - 0.35)
            cv.put(math.floor(cx + math.cos(a) * t), math.floor(cy + math.sin(a) * t), 'h' if j == 3 else 'm')
    cv.put(int(cx - 0.5), int(cy - 0.5), 's')


def base_frame(v, k):
    cv = Cv(BASE_W, BASE_H, v["pal"])
    shade_hull(cv, v)
    cv.paint(v.get("detail", []))
    r = v["ring"]
    ring(cv, r[0], r[1], r[2], k * math.radians(30) + 0.35)
    return cv


def rotate_logical(src, theta):
    """src: base canvas image (logical px) -> FR_W x FR_H image rotated clockwise by theta about the body centre, nearest neighbour on the LOGICAL grid."""
    out = Image.new("RGBA", (FR_W, FR_H), CLEAR)
    c, s = math.cos(theta), math.sin(theta)
    for fy in range(FR_H):
        for fx in range(FR_W):
            dx, dy = fx + 0.5 - FR_W / 2, fy + 0.5 - FR_H / 2
            sx, sy = dx * c + dy * s, -dx * s + dy * c
            bx, by = int(math.floor(sx + BASE_W / 2)), int(math.floor(sy + BASE_H / 2))
            if 0 <= bx < BASE_W and 0 <= by < BASE_H:
                p = src.getpixel((bx, by))
                if p[3]: out.putpixel((fx, fy), p)
    px = out.load()
    for y in range(FR_H):
        for x in range(FR_W):
            if px[x, y][3] and not any(0 <= x + dx < FR_W and 0 <= y + dy < FR_H and px[x + dx, y + dy][3] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                px[x, y] = CLEAR
    return out


def body_sheet(v):
    """4 fan frames (rows) x 5 tilt levels (columns -2..+2, nose-down positive), each FR_W x FR_H logical = 80x50 px."""
    fw, fh = FR_W * PX, FR_H * PX
    sh = Image.new("RGBA", (fw * TILT_LEVELS, fh * BODY_FRAMES), CLEAR)
    for k in range(BODY_FRAMES):
        base = base_frame(v, k).im
        for li in range(TILT_LEVELS):
            if li == 2:
                fr = Image.new("RGBA", (FR_W, FR_H), CLEAR); fr.paste(base, ((FR_W - BASE_W) // 2, (FR_H - BASE_H) // 2))
            else:
                fr = rotate_logical(base, (li - 2) * TILT_STEP)
            sh.paste(fr.resize((fw, fh), Image.NEAREST), (li * fw, k * fh))
    return sh, None


# ---------------------------------------------------------------------------------------------
# ROTOR: edge-on disc, drawn LEVEL. Blades droop at the tips, round hub, tapered mast with a flared collar.
# 24x8 logical, mast centre between cols 11|12 (px 24 = the game's origin).
# ---------------------------------------------------------------------------------------------
def rotor_frame(v, k):
    cv = Cv(ROT_W, ROT_H, v["pal"])
    L = v["rotor_len"]; cx = 12
    x0, x1 = cx - L // 2, cx + L // 2 - 1
    still = (k == 4)
    mast = {(11, 4), (12, 4), (11, 5), (12, 5), (11, 6), (12, 6), (10, 7), (11, 7), (12, 7), (13, 7)}
    for (x, y) in mast: cv.put(x, y, 'd')
    cv.outline(mast)
    for y in (4, 5, 6): cv.put(11, y, 'm')
    cv.put(10, 7, 'm'); cv.put(11, 7, 'h')
    if v["pod"]:
        pm = {(x, 5) for x in range(9, 14)} | {(x, 6) for x in range(8, 16)} | {(x, 7) for x in range(7, 17)}
        for (x, y) in pm: cv.put(x, y, 'd')
        cv.outline(pm)
        for x in range(9, 14): cv.put(x, 5, 'h' if x < 12 else 'm')
        for x in range(8, 16): cv.put(x, 6, 'm' if x < 11 else 'd')
        for x in range(7, 17): cv.put(x, 7, 'm' if 9 < x < 15 else 'd')
        cv.put(15, 6, v["lens"])
    for x in range(x0, x1 + 1):
        if cx - 2 <= x <= cx + 1: continue
        e = min(x - x0, x1 - x)                        # distance from the tip
        y = 2 if e >= 2 else 3                         # the tips droop one block: a curve
        d = abs(x - (cx - 0.5))
        if still:
            c = ('h' if (x - x0) % 5 < 2 else 'm') if x < cx else ('m' if (x1 - x) % 5 < 2 else 'd')
            cv.put(x, y, c)
            if e == 0: cv.put(x, 3, 'o'); cv.put(x, 4, 'o'); cv.put(x, 3, v["tip"] if v["tip"] != 'h' else 'm')
            elif y == 2: cv.put(x, 3, 'o') if e % 3 == 0 else None
        else:
            ph = (x + 2 * k) % 9
            c = 'h' if ph < 3 else ('m' if ph < 6 else 'd')
            if e == 0: c = v["tip"]
            cv.put(x, y, c)
            col = cv.pal['h'] if ph < 4 else cv.pal['m']
            a = max(40, 130 - int(d * 7))
            cv.put(x, y - 1, (col[0], col[1], col[2], a // 2))
            if d > 3 and y == 2: cv.put(x, 3, (col[0], col[1], col[2], a // 3))
    head = {(11, 1), (12, 1), (10, 2), (11, 2), (12, 2), (13, 2), (10, 3), (11, 3), (12, 3), (13, 3)}
    for (x, y) in head: cv.put(x, y, 'm')
    cv.outline(head)
    cv.paint([(1, 11, "hm"), (2, 10, "shmd"), (3, 10, "mmdd")])
    cv.put(11, 2, v["hubglint"])
    return cv


def rotor_sheet(v):
    sh = Image.new("RGBA", (ROT_W * PX, ROT_H * PX * ROT_FRAMES), CLEAR)
    for k in range(ROT_FRAMES): sh.paste(rotor_frame(v, k).scaled(), (0, k * ROT_H * PX))
    return sh, None


def geometry(v):
    fw, fh = FR_W * PX, FR_H * PX
    return dict(bw=fw, bh=fh, rw=ROT_W * PX, rh=ROT_H * PX, tilt_step=TILT_STEP, tilt_levels=TILT_LEVELS,
                anchors=[dict(x=15.0, y=-5.0, tint=1.0), dict(x=11.0, y=-9.0, tint=0.62)],
                bay_x=15.0, led=(11.0, -3.0), rotor_cx=ROT_W * PX / 2)


# ---------------------------------------------------------------------------------------------
# VARIANTS (top / bot = control points of the hull outline in logical px; y grows down)
# ---------------------------------------------------------------------------------------------
A_TOP = [(9, 4.6), (15, 4.7), (18, 4.0), (21, 2.7), (24, 2.2), (28, 2.4), (31, 3.5), (33.2, 5.2), (34, 6.2)]
A_BOT = [(9, 7.4), (15, 7.4), (18, 8.3), (21, 9.7), (24, 10.1), (28, 10.2), (30.5, 10.3), (32.5, 9.3), (33.6, 7.8), (34, 7.0)]

A_DETAIL = [
    (8, 21, "odddddgdo"),                                   # belly hatch with a small amber ready lamp
    (5, 30, "bcw"), (6, 30, "bcc"),                         # amber sensor slit in the nose
    (3, 26, "d"), (4, 26, "d"), (5, 26, "d"),               # panel seam behind the mast
    (4, 22, "o"), (5, 22, "o"),                             # LED socket on the shoulder
    (5, 13, "d"), (5, 16, "d"),                             # boom ribs
]
B_TOP = [(9, 4.7), (18, 4.6), (21, 3.4), (24, 2.9), (28, 3.0), (31, 3.8), (33.2, 5.3), (34, 6.2)]
B_BOT = [(9, 7.4), (18, 7.6), (21, 9.2), (24, 9.6), (28, 9.7), (30.5, 9.8), (32.5, 9.0), (33.6, 7.8), (34, 7.0)]
B_DETAIL = [(8, 21, "odddddgdo"), (5, 29, "bcw"), (3, 26, "d"), (4, 26, "d"), (4, 22, "o"), (5, 22, "o"), (5, 13, "d"), (5, 16, "d")]
# C: the needle: slimmest fuselage, biggest ring, long pointed nose
C_TOP = [(9, 4.7), (16, 4.7), (19, 3.5), (23, 3.0), (28, 3.2), (31, 4.1), (33.3, 5.6), (34, 6.3)]
C_BOT = [(9, 7.3), (16, 7.4), (19, 8.4), (23, 9.3), (28, 9.5), (31, 9.4), (32.8, 8.6), (33.8, 7.4), (34, 6.9)]
C_DETAIL = [(8, 22, "oddgddo"), (5, 29, "bcw"), (3, 25, "d"), (4, 25, "d"), (4, 22, "o"), (5, 22, "o"), (5, 13, "d"), (5, 16, "d"), (6, 20, "d")]

VARIANTS = {
    "A": dict(name="A Osprey (recommended)", pal=GRAPHITE, top=A_TOP, bot=A_BOT, x0=9, x1=33, ring=(5.5, 6.5, 5.3),
              th=(0.74, 0.42, 0.97), detail=A_DETAIL, rotor_len=22, pod=False, tip='h', hubglint='s', lens='c'),
    "B": dict(name="B Gunmetal nacelles", pal=GUNMETAL, top=B_TOP, bot=B_BOT, x0=9, x1=33, ring=(4.5, 6.5, 4.6),
              th=(0.70, 0.40, 0.97), detail=B_DETAIL, rotor_len=20, pod=True, tip='m', hubglint='h', lens='c'),
    "C": dict(name="C Needle", pal=BLACKSTEEL, top=C_TOP, bot=C_BOT, x0=9, x1=33, ring=(5.5, 6.5, 5.3),
              th=(0.70, 0.40, 0.97), detail=C_DETAIL, rotor_len=18, pod=False, tip='m', hubglint='h', lens='c'),
}


# ---------------------------------------------------------------------------------------------
# TURRET (Martian-steel sentry): fixed base 34x16 px, dome hub 14x14 px, barrel = 17 pre-rotated frames (aim -90..+90 deg, 32x32 px each, pivot at the
# frame centre). Aiming left draws the same frame mirrored. Everything is shaded from a surface normal against the same top-left light.
# ---------------------------------------------------------------------------------------------
BARREL_FRAMES, BARREL_PX = 17, 16          # frames, logical canvas size (pivot at 8,8)


def turret_base():
    cv = Cv(17, 8, MARTIAN)
    rows = {1: (6, 10), 2: (6, 10), 3: (5, 11), 4: (3, 13), 5: (1, 15), 6: (1, 15)}
    mask = {(x, y) for y, (a, b) in rows.items() for x in range(a, b + 1)}
    for (x, y) in mask:
        k = 'm'
        if x >= 10 or (y >= 5 and x >= 11): k = 'd'
        if x <= 4 and y >= 4: k = 'h'
        if (x, y - 1) not in mask: k = 'h'
        if (x + 1, y) not in mask and (x, y - 1) in mask: k = 'd'
        if y == 6: k = 'd' if x > 3 else 'm'
        cv.put(x, y, k)
    cv.outline(mask)
    cv.paint([(4, 5, "s"), (1, 6, "hm"), (5, 6, "o"), (5, 10, "o"), (6, 6, "o"), (6, 10, "o"), (5, 3, "l"), (5, 13, "c"), (3, 8, "d"), (2, 8, "d")])
    return cv.scaled()


def turret_hub():
    cv = Cv(7, 7, MARTIAN)
    mask = {(x, y) for y in range(7) for x in range(7) if math.hypot(x + 0.5 - 3.5, y + 0.5 - 3.5) <= 2.75}
    for (x, y) in mask:
        nx, ny = (x + 0.5 - 3.5) / 2.9, (y + 0.5 - 3.5) / 2.9
        nz = math.sqrt(max(0.0, 1 - nx * nx - ny * ny))
        cv.put(x, y, ramp(lit(nx, ny, nz), (0.62, 0.30, 0.9)))
    cv.outline(mask)
    cv.put(3, 3, 'l'); cv.put(3, 2, 'w')
    return cv.scaled()


def turret_barrel():
    sh = Image.new("RGBA", (BARREL_PX * PX, BARREL_PX * PX * BARREL_FRAMES), CLEAR)
    c = BARREL_PX / 2
    for f in range(BARREL_FRAMES):
        th = -math.pi / 2 + f * math.pi / (BARREL_FRAMES - 1)
        cv = Cv(BARREL_PX, BARREL_PX, MARTIAN)
        ct, st = math.cos(th), math.sin(th)
        mask = set(); prof = {}
        def hw(u): return 1.3 if u < 8.0 else (1.8 if u < 10.0 else 2.1)
        for y in range(BARREL_PX):
            for x in range(BARREL_PX):
                dx, dy = x + 0.5 - c, y + 0.5 - c
                u, w = dx * ct + dy * st, -dx * st + dy * ct
                if 0.5 <= u <= 12.3 and abs(w) <= hw(u): mask.add((x, y)); prof[(x, y)] = (u, w, hw(u))
        for (x, y), (u, w, h) in prof.items():
            vn = w / h
            nz = math.sqrt(max(0.0, 1 - vn * vn))
            k = ramp(lit(vn * -st, vn * ct, nz), (0.66, 0.34, 0.92))
            if u > 10.0 and 10.3 < u < 10.9: k = 'o'           # muzzle brake slit
            if 5.6 < u < 6.5: k = 'd' if k in ('h', 's') else 'o'  # clamp band
            cv.put(x, y, k)
        cv.outline(mask)
        lx, ly = int(math.floor(c + ct * 11.6)), int(math.floor(c + st * 11.6))
        if (lx, ly) in mask: cv.put(lx, ly, 'l')
        sh.paste(cv.scaled(), (0, f * BARREL_PX * PX))
    return sh


def build_turret(outdir):
    os.makedirs(outdir, exist_ok=True)
    turret_base().save(os.path.join(outdir, "TurretNPC.png"))
    turret_hub().save(os.path.join(outdir, "TurretHub.png"))
    turret_barrel().save(os.path.join(outdir, "TurretBarrel.png"))


def build(key, outdir, body_name="DroneNPC.png", rotor_name="DroneRotor.png"):
    v = VARIANTS[key]; os.makedirs(outdir, exist_ok=True)
    body_sheet(v)[0].save(os.path.join(outdir, body_name)); rotor_sheet(v)[0].save(os.path.join(outdir, rotor_name))


if __name__ == "__main__":
    out = sys.argv[1]
    for k in VARIANTS: build(k, os.path.join(out, k))
    build_turret(os.path.join(out, "turret"))
    print("ok")

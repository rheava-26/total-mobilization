#!/usr/bin/env python3
"""Combat-drone art generator (Mechanization). Writes the layered sprite sheets.

SIDE VIEW (what the game shows), art faces RIGHT (the game mirrors it for a drone facing left):
  * BODY sheet (rotates with the drone's tilt): tail ring (ducted fan, face-on circle) + tail boom + main body with a bay.
    4 frames stacked vertically; they differ only in the tail fan's blade angle.
  * ROTOR sheet (always drawn LEVEL, never rotated): the rotor discs seen edge-on ("-=--"): a thin horizontal blade strip on a short mast
    (or a mast on a nacelle pod). 5 frames stacked vertically: 0..3 spin blur, 4 = still (landed).

Art style: everything is drawn on a LOGICAL grid and upscaled x2 with nearest-neighbour, so every pixel is a 2x2 block like vanilla Terraria.
Outlines are a dark tint of the metal (not pure black), shading is top-lit with a rim highlight and a bottom shadow.

Usage:  gen_drone.py [outdir]   writes DroneNPC.png + DroneRotor.png (variant A) and prints the C# constants for it.
Import it (preview.py does) to render any variant.
"""
import math, sys, os
from PIL import Image, ImageDraw

PX = 2  # block size, like Terraria's 2x2 art

# Darker, slightly warm gunmetal (the old sprite was a cool grey-blue). Outline is a dark tint of the metal.
OL   = (22, 20, 25, 255)
D2   = (36, 34, 39, 255)
D1   = (52, 50, 57, 255)
BASE = (72, 70, 77, 255)
MID  = (97, 94, 102, 255)
HI   = (132, 128, 137, 255)
SPEC = (178, 172, 180, 255)
VOID = (16, 15, 20, 255)
AMB  = (238, 164, 66, 255)
AMB_D = (150, 92, 34, 255)

RAMP = dict(top=HI, top2=MID, mid=BASE, bot2=D1, bot=D2)


class Cv:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    def put(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h: self.im.putpixel((int(x), int(y)), c)
    def get(self, x, y):
        return self.im.getpixel((x, y)) if 0 <= x < self.w and 0 <= y < self.h else (0, 0, 0, 0)
    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1): self.put(x, y, c)
    def mask_poly(self, pts):
        m = Image.new("L", (self.w, self.h), 0); ImageDraw.Draw(m).polygon(pts, fill=255)
        return {(x, y) for y in range(self.h) for x in range(self.w) if m.getpixel((x, y))}
    def mask_rect(self, x0, y0, x1, y1):
        return {(x, y) for y in range(y0, y1 + 1) for x in range(x0, x1 + 1)}
    def mask_disc(self, cx, cy, r):
        return {(x, y) for y in range(self.h) for x in range(self.w) if (x - cx) ** 2 + (y - cy) ** 2 <= r * r}
    def part(self, mask, ramp=RAMP, outline=True):
        """Outline then top-lit shade a part. Later parts overwrite earlier ones, so the outline doubles as the seam between parts."""
        if outline:
            for (x, y) in mask:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    n = (x + dx, y + dy)
                    if n not in mask: self.put(n[0], n[1], OL)
        for (x, y) in mask:
            if (x, y - 1) not in mask: c = ramp["top"]
            elif (x, y - 2) not in mask: c = ramp["top2"]
            elif (x, y + 1) not in mask: c = ramp["bot"]
            elif (x, y + 2) not in mask: c = ramp["bot2"]
            else: c = ramp["mid"]
            self.put(x, y, c)
    def line(self, x0, y0, x1, y1, c):
        n = int(max(abs(x1 - x0), abs(y1 - y0)) * 2) + 1
        for i in range(n + 1):
            t = i / n; self.put(round(x0 + (x1 - x0) * t), round(y0 + (y1 - y0) * t), c)
    def scaled(self):
        return self.im.resize((self.w * PX, self.h * PX), Image.NEAREST)


# ---------------------------------------------------------------------------------------------
# Variants. Everything is in LOGICAL blocks (1 block = 2x2 px).
# ---------------------------------------------------------------------------------------------
VARIANTS = {
    # A: the straight reading of Eve's drawing. Medium boom; both rotors on one mast line, the far (starboard) one peeking out a block behind.
    "A": dict(name="A  Osprey (default)", boom=8, body=15, ring_r=5, rotor_len=22, mast=6, pod=False,
              rotors=[dict(dx=0, dy=0, tint=1.0), dict(dx=-2, dy=-2, tint=0.62)], mast_at=6),
    # B: longer and skinnier: long boom, tiltrotor nacelle pods under the rotors.
    "B": dict(name="B  Long boom, nacelle pods", boom=13, body=17, ring_r=5, rotor_len=20, mast=6, pod=True,
              rotors=[dict(dx=0, dy=0, tint=1.0), dict(dx=-2, dy=-2, tint=0.62)], mast_at=8),
    # C: compact, rotors staggered like the top view in the drawing (port forward, starboard aft): two separate "-=--" strips.
    "C": dict(name="C  Compact, staggered rotors", boom=4, body=14, ring_r=5, rotor_len=16, mast=6, pod=False,
              rotors=[dict(dx=4, dy=0, tint=1.0), dict(dx=-4, dy=0, tint=0.7)], mast_at=6),
}
ROTOR_FRAMES = 5   # 0..3 spin, 4 still
BODY_FRAMES = 4


def body_frame(v, fan):
    ring_d = 2 * v["ring_r"] + 1
    x0 = ring_d - 1 + v["boom"]            # first column of the main body
    w = v["body"]
    W = x0 + w + 1
    H = 13
    yc = 6
    cv = Cv(W, H)
    ring_c = (v["ring_r"], yc)
    # tail boom (thin, drawn first so the ring and body seam over its ends)
    boom = cv.mask_rect(ring_c[0] + 2, yc - 1, x0 + 1, yc + 1)
    cv.part(boom, dict(top=MID, top2=BASE, mid=D1, bot2=D1, bot=D2))
    for bx in range(ring_c[0] + 5, x0 - 1, 4):            # boom ribs
        cv.put(bx, yc, D2)
    # main body: slim, slightly humped, nose dipping toward the front
    pts = [(x0, yc - 1), (x0 + 2, 4), (x0 + 5, 3), (x0 + w - 8, 3), (x0 + w - 4, 4), (x0 + w - 1, 6), (x0 + w - 2, 8), (x0 + w - 5, 9), (x0 + 3, 9), (x0, yc + 1)]
    body = cv.mask_poly(pts)
    cv.part(body)
    # bay: a recessed hatch along the belly, with a lip
    bx0, bx1 = x0 + 4, x0 + w - 7
    cv.rect(bx0, 7, bx1, 8, VOID)
    for x in range(bx0, bx1 + 1): cv.put(x, 7, D2)
    cv.put(bx0 - 1, 7, OL); cv.put(bx1 + 1, 7, OL)
    for x in range(bx0, bx1 + 1, 3): cv.put(x, 8, D1)      # hatch ribs
    cv.put(bx1, 8, AMB)                                      # bay-ready lamp
    # dorsal spine highlight + panel seam + mast plate
    for x in range(x0 + 3, x0 + w - 6): cv.put(x, 4, HI if x % 3 else SPEC)
    cv.line(x0 + 7, 5, x0 + 7, 6, D1)
    # sensor visor in the nose
    cv.rect(x0 + w - 5, 5, x0 + w - 2, 6, VOID); cv.put(x0 + w - 4, 5, AMB); cv.put(x0 + w - 3, 5, AMB_D)
    # tail ring (ducted fan, face-on): rim, dark duct, spinning fan
    rim = cv.mask_disc(*ring_c, v["ring_r"]) - cv.mask_disc(*ring_c, v["ring_r"] - 1.6)
    cv.part(rim, dict(top=HI, top2=MID, mid=BASE, bot2=D1, bot=D2))
    inner = cv.mask_disc(*ring_c, v["ring_r"] - 1.6) - cv.mask_disc(*ring_c, v["ring_r"] - 2.6)
    for (x, y) in cv.mask_disc(*ring_c, v["ring_r"] - 1.6): cv.put(x, y, D2)
    for (x, y) in inner: cv.put(x, y, VOID)
    for i in range(3):                                        # fan blades
        a = fan + i * 2 * math.pi / 3
        cv.line(ring_c[0], ring_c[1], ring_c[0] + math.cos(a) * 3.3, ring_c[1] + math.sin(a) * 3.3, MID if i else HI)
    cv.put(*ring_c, SPEC)
    # anchors in REAL px relative to the sprite centre
    info = dict(W=W, H=H, x0=x0, body_len=w, ring_c=ring_c)
    return cv, info


def body_sheet(v):
    frames = []
    for k in range(BODY_FRAMES):
        cv, info = body_frame(v, k * math.radians(30) + 0.35)
        frames.append(cv.scaled())
    W, H = frames[0].size
    sh = Image.new("RGBA", (W, H * BODY_FRAMES), (0, 0, 0, 0))
    for k, f in enumerate(frames): sh.paste(f, (0, k * H))
    return sh, info


def rotor_frame(v, k):
    L = v["rotor_len"]; W = L + 2
    mast = v["mast"]
    pod = v["pod"]
    H = mast + 2 + (1 if pod else 0)
    cv = Cv(W, H)
    cx = W // 2
    still = (k == 4)
    # blade strip (edge-on rotor disc) on row 1, faint blur halo on rows 0 and 2 when spinning
    for x in range(1, W - 1):
        d = abs(x - cx)
        if still:
            cv.put(x, 1, MID if d > 2 else BASE)
            if x in (1, W - 2): cv.put(x, 1, D1)
            continue
        ph = (x + 2 * k) % 8
        c = HI if ph < 3 else (MID if ph < 6 else BASE)
        if x in (1, W - 2): c = D1
        cv.put(x, 1, c)
        a = max(30, 120 - int(d * 6))
        cv.put(x, 0, (150, 146, 156, a // 2) if ph < 4 else (110, 106, 116, a // 3))
        if d > 2: cv.put(x, 2, (110, 106, 116, a // 3))
    # rotor head
    head = cv.mask_rect(cx - 2, 1, cx + 1, 2)
    cv.part(head, dict(top=HI, top2=MID, mid=BASE, bot2=D1, bot=D2))
    # mast (2 blocks wide) and base collar
    cv.part(cv.mask_rect(cx - 1, 3, cx, H - 1), dict(top=MID, top2=MID, mid=BASE, bot2=D1, bot=D1))
    if pod:   # tiltrotor nacelle: a capsule under the mast, nose toward the front
        pm = cv.mask_poly([(cx - 4, H - 3), (cx + 3, H - 4), (cx + 5, H - 3), (cx + 5, H - 2), (cx - 4, H - 1), (cx - 5, H - 2)])
        cv.part(pm)
        cv.put(cx + 5, H - 3, AMB_D); cv.put(cx - 2, H - 2, D1)
    else:
        pass
    return cv, dict(W=W, H=H, cx=cx)


def rotor_sheet(v):
    fr = [rotor_frame(v, k)[0].scaled() for k in range(ROTOR_FRAMES)]
    info = rotor_frame(v, 0)[1]
    W, H = fr[0].size
    sh = Image.new("RGBA", (W, H * ROTOR_FRAMES), (0, 0, 0, 0))
    for k, f in enumerate(fr): sh.paste(f, (0, k * H))
    return sh, info


def geometry(v):
    """All the numbers the draw code needs, in REAL px relative to the body sprite's centre (art faces right)."""
    bsh, bi = body_sheet(v)
    rsh, ri = rotor_sheet(v)
    bw, bh = bsh.size[0], bsh.size[1] // BODY_FRAMES
    rw, rh = rsh.size[0], rsh.size[1] // ROTOR_FRAMES
    mast_x = (bi["x0"] + v["mast_at"]) * PX + 1          # px from the sprite's left edge
    mast_y = 4 * PX                                        # a block inside the hull top
    anchors = []
    for r in v["rotors"]:
        anchors.append(dict(x=mast_x + r["dx"] * PX - bw / 2, y=mast_y + r["dy"] * PX - bh / 2, tint=r["tint"]))
    bay_x = (bi["x0"] + (4 + bi["body_len"] - 7) / 2 + 0.5) * PX - bw / 2   # centre of the bay, for the weapon
    led = ((bi["x0"] + 4) * PX + 1 - bw / 2, 5 * PX - bh / 2)                 # status light, on the body's rear shoulder
    return dict(bw=bw, bh=bh, rw=rw, rh=rh, anchors=anchors, bay_x=bay_x, led=led, rotor_cx=ri["cx"] * PX + 1)


def csharp(v):
    g = geometry(v)
    f = lambda a: ("%.1ff" % a)
    s = "// generated by tools/drone_art/gen_drone.py (variant %s): body sprite %dx%d (4 fan frames), rotor sprite %dx%d (5 frames: 0..3 spin, 4 still)\n" % (v["name"][0], g["bw"], g["bh"], g["rw"], g["rh"])
    s += "public const int BodyFrames = %d, RotorFrames = %d;\n" % (BODY_FRAMES, ROTOR_FRAMES)
    s += "public static readonly Vector2[] RotorMastPx = { %s };  // body-local, art facing right\n" % ", ".join("new Vector2(%s, %s)" % (f(a["x"]), f(a["y"])) for a in g["anchors"])
    s += "public static readonly float[] RotorTint = { %s };\n" % ", ".join(f(a["tint"]) for a in g["anchors"])
    s += "public const float WeaponDrawOffsetX = %s, LedX = %s, LedY = %s;\n" % (f(g["bay_x"]), f(g["led"][0]), f(g["led"][1]))
    return s


def build(variant, outdir, body_name="DroneNPC.png", rotor_name="DroneRotor.png"):
    v = VARIANTS[variant]
    bsh, _ = body_sheet(v); rsh, _ = rotor_sheet(v)
    os.makedirs(outdir, exist_ok=True)
    bsh.save(os.path.join(outdir, body_name)); rsh.save(os.path.join(outdir, rotor_name))
    return v


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    v = build("A", out)
    print(csharp(v))

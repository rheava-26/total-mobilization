#!/usr/bin/env python3
"""Turret v5: a swivel CRADLE (U-yoke on a neck) instead of a barrel. 17 pre-rotated frames (aim -90..+90 deg), 18x18 logical px = 36x36 px each, pivot at the frame centre;
aiming left draws the mirrored frame. The mounted weapon sprite is drawn on top of it, grip at the pivot, so you see the plate under the gun and the cheek tips above it.
Usage: gen_turret5.py <outdir> -> TurretCradle.png"""
import math, os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
from gen_drone4 import Cv, ramp, lit, MARTIAN, PX, CLEAR
from PIL import Image

FRAMES, N = 17, 18


def cradle(th):
    cv = Cv(N, N, MARTIAN)
    c = N / 2
    ct, st = math.cos(th), math.sin(th)
    # parts in aim space (u along the aim, w across it; +w = underside for a right-facing turret)
    parts = [  # (u0,u1,w0,w1, narrow-axis) ; narrow axis 'w' -> shaded across w, 'u' -> shaded across u
        (-5.4, 5.4, 3.5, 5.3, 'w'),        # cradle plate under the weapon
        (-5.4, -3.6, -4.6, 5.3, 'u'),      # rear cheek
        (3.6, 5.4, -4.2, 5.3, 'u'),        # front cheek
        (-1.1, 1.1, 0.0, 3.6, 'u'),        # neck down to the pivot
    ]
    mask = {}
    for y in range(N):
        for x in range(N):
            dx, dy = x + .5 - c, y + .5 - c
            u, w = dx * ct + dy * st, -dx * st + dy * ct
            for (u0, u1, w0, w1, ax) in reversed(parts):   # first listed part wins the overlap
                if u0 <= u <= u1 and w0 <= w <= w1:
                    vn = ((w - (w0 + w1) / 2) / ((w1 - w0) / 2)) if ax == 'w' else ((u - (u0 + u1) / 2) / ((u1 - u0) / 2))
                    mask[(x, y)] = (ax, vn)
    for (x, y), (ax, vn) in mask.items():
        nz = math.sqrt(max(0.0, 1 - vn * vn))
        axv = (-st, ct) if ax == 'w' else (ct, st)
        k = ramp(lit(vn * axv[0], vn * axv[1], nz), (0.62, 0.30, 0.9))
        cv.put(x, y, k)
    cv.outline(set(mask))
    # bolt on each cheek
    for u in (-4.5, 4.5):
        x, y = int(math.floor(c + ct * u + (-st) * 3.0)), int(math.floor(c + st * u + ct * 3.0))
        if (x, y) in mask: cv.put(x, y, 'h')
    return cv


def sheet():
    sh = Image.new("RGBA", (N * PX, N * PX * FRAMES), CLEAR)
    for f in range(FRAMES):
        th = -math.pi / 2 + f * math.pi / (FRAMES - 1)
        sh.paste(cradle(th).scaled(), (0, f * N * PX))
    return sh


if __name__ == "__main__":
    os.makedirs(sys.argv[1], exist_ok=True)
    sheet().save(os.path.join(sys.argv[1], "TurretCradle.png"))
    print("ok")

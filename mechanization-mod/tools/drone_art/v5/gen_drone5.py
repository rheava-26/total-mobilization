#!/usr/bin/env python3
"""Combat drone v5: v4 body + tail ring, plus the designer's side DUCTED-FAN NACELLES painted into the body sheet.
Near nacelle: a capsule cowl in FRONT of the fuselage at body height (intake lip at the nose end, dark exhaust ring at the back).
Far nacelle: the same pod BEHIND the hull, 2 art-px up and 3 back, in darker tones, mostly hidden.
Nothing sits above the fuselage top line except the far pod's peeking pixels. Frames are rotated on the logical grid exactly like v4.
Usage: gen_drone5.py <outdir>   -> <outdir>/pod/DroneNPC.png (default, shipped) and <outdir>/ring/DroneNPC.png (ring-cowl comparison)"""
import math, os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import gen_drone4 as g
from gen_drone4 import Cv, shade_hull, ring, rotate_logical, PX, BASE_W, BASE_H, FR_W, FR_H, TILT_STEP, TILT_LEVELS, BODY_FRAMES, CLEAR
from PIL import Image

# pod boxes (logical px, inclusive x0,x1,y0,y1): near pod in front of the hull at its forward-middle, far pod 2 up and 3 back
NEAR = (20, 28, 2, 9)
FAR = (NEAR[0] - 3, NEAR[1] - 3, NEAR[2] - 2, NEAR[3] - 2)
BANDS = ['s', 'h', 'm', 'm', 'd', 'm']
DARKER = {'s': 'h', 'h': 'm', 'm': 'd', 'd': 'd', 'o': 'o'}


def capsule_mask(x0, x1, y0, y1):
    """Rounded horizontal capsule filling the box (inclusive), superellipse so the ends step 1-1-2 like vanilla rounds."""
    cx, cy = (x0 + x1 + 1) / 2, (y0 + y1 + 1) / 2
    rx, ry = (x1 - x0 + 1) / 2, (y1 - y0 + 1) / 2
    m = set()
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if (abs(x + .5 - cx) / rx) ** 3.2 + (abs(y + .5 - cy) / ry) ** 2.4 <= 1.0: m.add((x, y))
    return m


def draw_pod(cv, x0, x1, y0, y1, k, far):
    mask = capsule_mask(x0, x1, y0, y1)
    ys = [y for _, y in mask]; ty, by = min(ys), max(ys)
    cy = (ty + by + 1) / 2; ry = (by - ty + 1) / 2
    inner = {p for p in mask if all((p[0] + dx, p[1] + dy) in mask for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))}
    pal = g.GRAPHITE if far else g.GUNMETAL          # the near pod is a lighter steel than the hull so it reads against it; the far pod keeps the hull's tones, one step darker
    put = (lambda x, y, c: cv.put(x, y, pal[DARKER[c] if far else c]))
    for (x, y) in inner:
        ny = (y + .5 - cy) / ry                      # -1 top .. +1 bottom
        nx = (x + .5 - (x0 + x1 + 1) / 2) / ((x1 - x0 + 1) / 2)
        # side-on cylinder: bands run along the pod, brightest at the top-left, a bounce row under the belly
        rowk = int((ny + 1) / 2 * len(BANDS)); c = BANDS[min(len(BANDS) - 1, max(0, rowk))]
        nx = (x + .5 - (x0 + x1 + 1) / 2) / ((x1 - x0 + 1) / 2)
        if nx < -0.55 and c in ('h', 'm'): c = 's' if c == 'h' else 'h'     # the left end catches the light
        if nx > 0.6 and c == 's': c = 'h'
        put(x, y, c)
    for (px, py) in mask - inner: cv.put(px, py, 'o')     # the box is the whole pod including its outline
    # intake lip at the nose (right) end: a darker ring (graphite, not the outline black) so it reads as a lip, not a grille
    xin = max(p[0] for p in inner)
    for (x, y) in inner:
        if x == xin: put(x, y, 'd' if ty + 2 <= y <= by - 2 else 'm')
    # exhaust ring at the back (left) end: one dark column and a hot-metal column inside it
    xb = min(p[0] for p in inner)
    for (x, y) in inner:
        if x == xb: put(x, y, 'd')
        elif x == xb + 1 and ty + 2 <= y <= by - 2: put(x, y, 'm')
    # animation: a glint travelling the top rim and a shimmer in the exhaust
    gx = xb + 2 + (k * 2) % max(1, (xin - xb - 3))
    if (gx, ty + 1) in inner: put(gx, ty + 1, 's' if k % 2 == 0 else 'h')
    ex = (xb, (ty + by) // 2)
    if ex in inner: put(ex[0], ex[1], ['m', 'd', 'h', 'd'][k])
    return mask


def draw_ring_cowl(cv, cx, cy, r, far, k):
    disc = {(x, y) for y in range(cv.h) for x in range(cv.w) if math.hypot(x + .5 - cx, y + .5 - cy) <= r}
    inner_r = r - 1.9
    for (x, y) in disc:
        d = math.hypot(x + .5 - cx, y + .5 - cy); a = math.atan2(y + .5 - cy, x + .5 - cx)
        if d > inner_r:
            t = math.cos(a + 3 * math.pi / 4)
            c = 's' if (t > 0.95 and d > r - 1.2) else 'h' if t > 0.55 else 'm' if t > -0.2 else 'd'
        else:
            t = math.cos(a - math.pi / 4)
            c = 'd' if (t > 0.7 and d > inner_r - 1.1) else 'o'
        cv.put(x, y, DARKER[c] if far else c)
    cv.outline(disc)
    if not far and k % 2 == 0: cv.put(int(cx - r * .55), int(cy - r * .75), 's')


def base_frame(v, k):
    cv = Cv(BASE_W, BASE_H, v["pal"])
    if v["pod"] == "capsule":
        draw_pod(cv, FAR[0], FAR[1], FAR[2], FAR[3], k, far=True)
    else:
        draw_ring_cowl(cv, 21.0, 4.5, 3.8, True, k)
    shade_hull(cv, v)
    cv.paint(v.get("detail", []))
    if v["pod"] == "capsule":
        draw_pod(cv, NEAR[0], NEAR[1], NEAR[2], NEAR[3], k, far=False)
    else:
        draw_ring_cowl(cv, 24.0, 6.5, 3.9, False, k)
    r = v["ring"]
    ring(cv, r[0], r[1], r[2], k * math.radians(30) + 0.35)
    return cv


def body_sheet(v):
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
    return sh


def preview(sheet, path):
    """8x preview of the level column over 4 fan frames, plus the full tilt row, plus a 1x strip."""
    fw, fh = FR_W * PX, FR_H * PX
    bg = (30, 30, 42, 255)
    out = Image.new("RGBA", (fw * 8 + 40, fh * 8 * 2 + 60), bg)
    for i, (col, row) in enumerate([(2, 0), (2, 2)]):
        f = sheet.crop((col * fw, row * fh, (col + 1) * fw, (row + 1) * fh)).resize((fw * 8, fh * 8), Image.NEAREST)
        out.paste(f, (20, 20 + i * (fh * 8 + 20)), f)
    out.save(path)
    strip = Image.new("RGBA", (fw * TILT_LEVELS * 3 + 20, fh * 3 * BODY_FRAMES + 20), bg)
    s3 = sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST)
    strip.paste(s3, (10, 10), s3)
    strip.save(path.replace('.png', '_sheet3x.png'))


if __name__ == "__main__":
    out = sys.argv[1]
    A = dict(g.VARIANTS["A"])
    for name in ("pod", "ring"):
        v = dict(A, pod="capsule" if name == "pod" else "ring")
        os.makedirs(os.path.join(out, name), exist_ok=True)
        sh = body_sheet(v)
        sh.save(os.path.join(out, name, "DroneNPC.png"))
        preview(sh, os.path.join(out, name, "drone_v5_8x.png"))
    print("ok")

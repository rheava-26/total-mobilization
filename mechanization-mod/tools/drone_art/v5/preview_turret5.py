#!/usr/bin/env python3
"""8x preview of the mount aiming -60/0/+45 deg, right then left. Usage: preview_turret5.py <NPCs dir> <out.png> <gun.png> <staff.png> (gun/staff sprites from the wiki, preview only, never committed)"""
import math, sys, os
from PIL import Image
npcs, out, gun, staff = sys.argv[1:5]
base = Image.open(os.path.join(npcs, 'TurretNPC.png')).convert('RGBA')
hub = Image.open(os.path.join(npcs, 'TurretHub.png')).convert('RGBA')
cr = Image.open(os.path.join(npcs, 'TurretCradle.png')).convert('RGBA')
PLINTH, PED, PIVOT_UP, MAXPX, RECOIL = 7, 7, 7, 56, 0
W, H = 120, 100
def render(elev_deg, left, wpath, is_staff):
    cv = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    bx, bottom = W // 2, H - 10
    cv.alpha_composite(base, (bx - base.width // 2, bottom - base.height))
    pedTop = bottom - PLINTH - PED
    px, py = bx, pedTop - PIVOT_UP
    cv.alpha_composite(hub, (px - hub.width // 2, pedTop - 2 - hub.height // 2))
    e = math.radians(elev_deg)
    f = round((e + math.pi / 2) / (math.pi / 16)); f = max(0, min(16, f))
    fr = cr.crop((0, f * 36, 36, f * 36 + 36))
    if left: fr = fr.transpose(Image.FLIP_LEFT_RIGHT)
    cv.alpha_composite(fr, (px - 18, py - 18))
    if wpath:
        w = Image.open(wpath).convert('RGBA'); L = max(w.size); s = min(1, MAXPX / L)
        if s < 1: w = w.resize((max(1, round(w.width * s)), max(1, round(w.height * s))), Image.NEAREST)
        aim = math.pi - e if left else e
        rot = aim; 
        if is_staff:
            rot = aim + (3 if left else 1) * math.pi / 4
            if left: w = w.transpose(Image.FLIP_LEFT_RIGHT)
        elif left: w = w.transpose(Image.FLIP_TOP_BOTTOM)
        pad = Image.new('RGBA', (L * 2, L * 2), (0, 0, 0, 0)); pad.alpha_composite(w, (L - w.width // 2, L - w.height // 2))
        r = pad.rotate(-math.degrees(rot), resample=Image.BICUBIC, center=(L, L))
        cv.alpha_composite(r, (px - L, py - L))
    return cv
angles = (-60, 0, 45)
rows = [(gun, False, 'gun'), (staff, True, 'staff'), (None, False, 'empty')]
sheet = Image.new('RGBA', (W * 6, H * 3), (34, 34, 46, 255))
for ri, (wp, st, _) in enumerate(rows):
    for ci, (left, a) in enumerate([(False, a) for a in angles] + [(True, a) for a in angles]):
        sheet.alpha_composite(render(a, left, wp, st), (ci * W, ri * H))
sheet.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST).save(out)

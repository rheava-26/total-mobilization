#!/usr/bin/env python3
"""v4 previews: mirrors DroneNPC.PreDraw / TurretNPC.PreDraw (pre-tilted body columns, level rotors, pre-rotated barrel frames).
Writes per variant: 8x still + flight GIF; plus drone-vs-vanilla, turret sheet, overview.
Usage: preview4.py <outdir> <vanilla_dir>   (vanilla_dir: Martian_Drone.png, Probe.png, Tesla_Turret.png, Ballista.png, Flameburst_Tower.png; reference only, never shipped)"""
import math, os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_drone4 as G

BG = (88, 118, 160, 255)
FONT = ImageFont.load_default(size=15); FONT_S = ImageFont.load_default(size=12)
TILT_PER_SPEED, MAX_TILT, SMOOTH = 0.08, 0.32, 0.18        # = MechConstants.Drone
_CACHE = {}


def sheets(key):
    if key not in _CACHE:
        v = G.VARIANTS[key]; _CACHE[key] = (G.body_sheet(v)[0], G.rotor_sheet(v)[0], G.geometry(v))
    return _CACHE[key]


def tint(img, t):
    if t >= 0.999: return img
    r, g, b, a = img.split(); f = lambda ch: ch.point(lambda v: int(v * t))
    return Image.merge("RGBA", (f(r), f(g), f(b), a))


def put(dst, spr, pos, origin, flip):
    if flip: spr = spr.transpose(Image.FLIP_LEFT_RIGHT); origin = (spr.width - origin[0], origin[1])
    dst.alpha_composite(spr, (int(round(pos[0] - origin[0])), int(round(pos[1] - origin[1]))))


def draw_drone(dst, key, centre, tilt, sd, tick, landed=False):
    body, rotor, g = sheets(key)
    bw, bh, rw, rh = g["bw"], g["bh"], g["rw"], g["rh"]
    step = g["tilt_step"]; half = g["tilt_levels"] // 2
    level = max(-half, min(half, int(round(tilt * sd / step)))); rot = level * step * sd
    spin = (tick // 2) % 4
    bsrc = body.crop(((level + half) * bw, (0 if landed else spin) * bh, (level + half + 1) * bw, ((0 if landed else spin) + 1) * bh))
    rf = 4 if landed else spin
    rsrc = rotor.crop((0, rf * rh, rw, (rf + 1) * rh))
    cx, cy = round(centre[0]), round(centre[1]); c, s = math.cos(rot), math.sin(rot)
    def local(a):
        lx, ly = a["x"] * sd, a["y"]
        return (round(cx + lx * c - ly * s), round(cy + lx * s + ly * c))
    an = g["anchors"]
    for a in an[1:]: put(dst, tint(rsrc, a["tint"]), local(a), (rw // 2, rh), sd < 0)
    put(dst, bsrc, (cx, cy), (bw // 2, bh // 2), sd < 0)
    lx, ly = g["led"][0] * sd, g["led"][1]
    px, py = round(cx + lx * c - ly * s), round(cy + lx * s + ly * c)
    ImageDraw.Draw(dst).rectangle([px - 1, py - 1, px, py], fill=(110, 235, 130, 255))
    put(dst, rsrc, local(an[0]), (rw // 2, rh), sd < 0)


def zoom(img, z): return img.resize((img.width * z, img.height * z), Image.NEAREST)


def still(key, z=8):
    im = Image.new("RGBA", (96, 56), BG); draw_drone(im, key, (48, 34), 0.0, 1, 3)
    return zoom(im, z)


def flight_gif(key, path):
    W, H = 230, 100; frames = []
    x, vx, tilt, cam, sd = 0.0, 0.0, 0.0, 0.0, 1
    plan = [(30, 0.0), (80, 4.0), (60, 4.0), (70, 0.0), (80, -2.8), (70, -2.8), (70, 0.0)]
    tick = 0
    for dur, target in plan:
        for _ in range(dur):
            vx += (target - vx) * 0.07; x += vx
            tilt += (max(-MAX_TILT, min(MAX_TILT, vx * TILT_PER_SPEED)) - tilt) * SMOOTH
            if abs(vx) > 0.4: sd = 1 if vx > 0 else -1
            cam += (x - cam) * 0.05; tick += 1
            if tick % 3: continue
            im = Image.new("RGBA", (W, H), BG); dr = ImageDraw.Draw(im)
            for gy in range(H):
                f = gy / H; dr.line([(0, gy), (W, gy)], fill=(int(88 + 40 * f), int(118 + 50 * f), int(160 + 40 * f), 255))
            dr.rectangle([0, H - 10, W, H], fill=(52, 60, 44, 255)); dr.rectangle([0, H - 10, W, H - 9], fill=(86, 120, 60, 255))
            for k in range(-2, 12):
                px = k * 40 - cam % 40; dr.rectangle([px, H - 16, px + 1, H - 10], fill=(40, 36, 32, 255))
            draw_drone(im, key, (W / 2 + (x - cam), 44 + math.sin(tick * 0.08) * 2), tilt, sd, tick)
            big = zoom(im, 4).convert("RGB"); d = ImageDraw.Draw(big)
            d.text((8, 6), "variant %s   vx %+.1f px/tick   pre-tilted body (%+.0f deg), rotors level" % (key, vx, math.degrees(tilt)), font=FONT, fill=(255, 255, 255))
            frames.append(big)
    frames[0].save(path, save_all=True, append_images=frames[1:], duration=50, loop=0)


def side_by_side(keys, vdir, path, z2=3):
    van = [("Martian Drone", "Martian_Drone"), ("Destroyer Probe", "Probe")]
    cw, ch = 96, 56
    sh = Image.new("RGBA", ((len(keys) + len(van)) * (cw * z2 + 8) + 8, ch * z2 + 40), (30, 32, 40, 255)); d = ImageDraw.Draw(sh)
    x = 8
    for k in keys:
        im = Image.new("RGBA", (cw, ch), BG); draw_drone(im, k, (48, 34), 0.0, 1, 3)
        sh.alpha_composite(zoom(im, z2), (x, 28)); d.text((x, 6), G.VARIANTS[k]["name"], font=FONT_S, fill=(255, 255, 255, 255)); x += cw * z2 + 8
    for label, n in van:
        src = Image.open(os.path.join(vdir, n + ".png")).convert("RGBA")
        im = Image.new("RGBA", (cw, ch), BG); im.alpha_composite(src, ((cw - src.width) // 2, (ch - src.height) // 2))
        sh.alpha_composite(zoom(im, z2), (x, 28)); d.text((x, 6), "vanilla: " + label, font=FONT_S, fill=(255, 220, 150, 255)); x += cw * z2 + 8
    sh.convert("RGB").save(path)


def turret_cell(tur, aim, w=80, h=64):
    base = Image.open(tur + "/TurretNPC.png").convert("RGBA"); barrel = Image.open(tur + "/TurretBarrel.png").convert("RGBA"); hub = Image.open(tur + "/TurretHub.png").convert("RGBA")
    im = Image.new("RGBA", (w, h), BG); dr = ImageDraw.Draw(im)
    dr.rectangle([0, h - 12, w, h], fill=(52, 60, 44, 255)); dr.rectangle([0, h - 12, w, h - 11], fill=(86, 120, 60, 255))
    bottom = h - 11
    im.alpha_composite(base, (w // 2 - base.width // 2, bottom - base.height))
    pivot = (w // 2, bottom - 16)
    left = math.cos(aim) < 0
    ang = math.atan2(math.sin(aim), -math.cos(aim)) if left else aim
    fh = barrel.height // 17
    f = max(0, min(16, int(round((ang + math.pi / 2) / (math.pi / 16)))))
    spr = barrel.crop((0, f * fh, barrel.width, (f + 1) * fh))
    put(im, spr, pivot, (spr.width // 2, fh // 2), left)
    im.alpha_composite(hub, (pivot[0] - hub.width // 2, pivot[1] - hub.height // 2))
    return im


def turret_sheet(out, vdir, path, z2=3):
    tur = os.path.join(out, "turret")
    aims = [0, -0.5, -1.2, math.pi + 1.2, math.pi + 0.5, math.pi]
    refs = [("Tesla_Turret", "vanilla: Tesla Turret"), ("Flameburst_Tower", "vanilla: Flameburst Tower"), ("Ballista", "vanilla: Ballista"), ("Martian_Saucer_Turret", "vanilla: Saucer Turret")]
    W = len(aims) * (80 * z2 + 8) + 8
    sh = Image.new("RGBA", (W, 64 * z2 + 60 + 54 * z2 + 40), (30, 32, 40, 255)); d = ImageDraw.Draw(sh)
    d.text((8, 6), "TURRET: aim right / up / left (pre-rotated barrel frames, mirrored for left)", font=FONT_S, fill=(255, 255, 255, 255))
    for i, a in enumerate(aims): sh.alpha_composite(zoom(turret_cell(tur, a), z2), (8 + i * (80 * z2 + 8), 28))
    x = 8; y = 64 * z2 + 60
    for n, lab in refs:
        src = Image.open(os.path.join(vdir, n + ".png")).convert("RGBA"); s2 = zoom(src, z2)
        sh.alpha_composite(s2, (x, y)); d.text((x, y + s2.height + 2), lab, font=FONT_S, fill=(255, 220, 150, 255)); x += s2.width + 24
    sh.convert("RGB").save(path)


if __name__ == "__main__":
    out, vdir = sys.argv[1], sys.argv[2]
    for k in G.VARIANTS:
        zoom_ = still(k); zoom_.convert("RGB").save(os.path.join(out, "drone_%s_still8x.png" % k))
        flight_gif(k, os.path.join(out, "drone_%s_flight.gif" % k))
    side_by_side(list(G.VARIANTS), vdir, os.path.join(out, "drone_vs_vanilla.png"))
    turret_sheet(out, vdir, os.path.join(out, "turret_sheet.png"))
    a = Image.open(os.path.join(out, "drone_vs_vanilla.png")); b = Image.open(os.path.join(out, "turret_sheet.png"))
    o = Image.new("RGB", (max(a.width, b.width), a.height + b.height), (30, 32, 40)); o.paste(a, (0, 0)); o.paste(b, (0, a.height)); o.save(os.path.join(out, "overview.png"))
    print("ok")

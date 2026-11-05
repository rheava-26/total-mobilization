#!/usr/bin/env python3
"""Offline preview for Eve: contact sheets, a flight GIF, variant comparison. Mirrors the in-game draw (DroneNPC.PreDraw):
body rotated by the smoothed tilt, rotors LEVEL at mast points computed from the rotated body, far rotor first, near rotor last.
Usage: preview.py <outdir> <vanilla_png_dir>"""
import math, sys, os
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_drone as G

TILT_PER_SPEED, MAX_TILT, SMOOTH = 0.08, 0.32, 0.18   # keep in sync with MechConstants.Drone
BG = (88, 118, 160, 255)
FONT = ImageFont.load_default(size=15)
FONT_S = ImageFont.load_default(size=12)


def tint(img, t):
    if t >= 0.999: return img
    r, g, b, a = img.split()
    f = lambda ch: ch.point(lambda v: int(v * t))
    return Image.merge("RGBA", (f(r), f(g), f(b), a))


def blit_rot(dst, spr, pos, origin, rot, flip):
    """Draw spr onto dst (1x real px) with its `origin` at `pos`, rotated by `rot` radians clockwise (XNA convention), nearest-neighbour like PointClamp."""
    if flip: spr = spr.transpose(Image.FLIP_LEFT_RIGHT); origin = (spr.width - origin[0], origin[1])
    R = int(max(spr.size) * 1.5) + 4
    pad = Image.new("RGBA", (2 * R, 2 * R), (0, 0, 0, 0))
    pad.alpha_composite(spr, (R - int(round(origin[0])), R - int(round(origin[1]))))
    if rot: pad = pad.rotate(-math.degrees(rot), resample=Image.NEAREST, center=(R, R))
    dst.alpha_composite(pad, (int(round(pos[0])) - R, int(round(pos[1])) - R))


class Drone:
    def __init__(self, key):
        self.v = G.VARIANTS[key]; self.key = key
        self.body, _ = G.body_sheet(self.v); self.rotor, _ = G.rotor_sheet(self.v)
        self.g = G.geometry(self.v)
        self.bh = self.g["bh"]; self.rh = self.g["rh"]

    def draw(self, dst, centre, tilt, sd, tick, landed=False, weapon=None, frame=None):
        """centre: (x,y) of the NPC centre in dst px. tilt: radians (positive = clockwise on screen, nose down for a right-facing drone)."""
        g = self.g
        f = (tick // 2) % 4 if frame is None else frame      # in game: (GameUpdateCount / 2 + whoAmI) % 4
        bf = 0 if landed else f
        rf = 4 if landed else f
        body = self.body.crop((0, bf * self.bh, g["bw"], (bf + 1) * self.bh))
        rotor = self.rotor.crop((0, rf * self.rh, g["rw"], (rf + 1) * self.rh))
        cx, cy = centre
        def mast_world(a):
            lx, ly = a["x"] * sd, a["y"]
            c, s = math.cos(tilt), math.sin(tilt)
            return (cx + lx * c - ly * s, cy + lx * s + ly * c)
        order = sorted(range(len(g["anchors"])), key=lambda i: g["anchors"][i]["tint"])   # far (dimmest) first
        far = [i for i in order if g["anchors"][i]["tint"] < 0.999]
        near = [i for i in order if g["anchors"][i]["tint"] >= 0.999]
        def rotor_draw(i):
            a = g["anchors"][i]; wx, wy = mast_world(a)
            blit_rot(dst, tint(rotor, a["tint"]), (wx, wy), (g["rotor_cx"], g["rh"]), 0.0, sd < 0)   # level: rotation 0
        for i in far: rotor_draw(i)
        blit_rot(dst, body, (cx, cy), (g["bw"] / 2, self.bh / 2), tilt, sd < 0)
        # status light (a 2x2 dot on the hull, turned with the body)
        lx, ly = g["led"][0] * sd, g["led"][1]; c, s = math.cos(tilt), math.sin(tilt)
        px, py = cx + lx * c - ly * s, cy + lx * s + ly * c
        ImageDraw.Draw(dst).rectangle([round(px) - 1, round(py) - 1, round(px), round(py)], fill=(110, 235, 130, 255))
        if weapon is not None:
            wx, wy = cx + 0 + (g["bay_x"] * sd) * c - 9 * s, cy + (g["bay_x"] * sd) * s + 9 * c
            sc = min(1.0, 28 / max(weapon.size))
            w2 = weapon.resize((max(1, int(weapon.width * sc)), max(1, int(weapon.height * sc))), Image.NEAREST)
            blit_rot(dst, w2, (wx, wy), (w2.width / 2, w2.height / 2), 0.0 if sd > 0 else 0.0, sd < 0)
        for i in near: rotor_draw(i)


def smooth_tilt(tilt, vx, landed=False):
    tgt = 0.0 if landed else max(-MAX_TILT, min(MAX_TILT, vx * TILT_PER_SPEED))
    return tilt + (tgt - tilt) * SMOOTH


def zoom(img, z=4): return img.resize((img.width * z, img.height * z), Image.NEAREST)


def label(d, xy, text, font=FONT, fill=(255, 255, 255, 255)): d.text(xy, text, font=font, fill=fill)


def cell(drone, tilt, sd, tick, w=96, h=60, landed=False, weapon=None):
    im = Image.new("RGBA", (w, h), BG)
    drone.draw(im, (w // 2, h // 2 + 6), tilt, sd, tick, landed, weapon)
    return im


def contact_sheet(key, vanilla_dir, weapon_img, path):
    D = Drone(key); z = 4; w, h = 96, 60
    cols = 7; pad = 8
    W = pad + cols * (w * z + pad); Hrow = h * z + 26
    rows = 5
    sheet = Image.new("RGBA", (W, 60 + rows * (Hrow + pad)), (30, 32, 40, 255)); d = ImageDraw.Draw(sheet)
    label(d, (pad, 8), "COMBAT DRONE  variant " + D.v["name"] + "   (4x zoom, 2x2 pixel blocks; rotors stay LEVEL, body tilts)", FONT)
    label(d, (pad, 30), "side view, art faces right, mirrored for left. Tilt = nose down toward travel. Speeds in px/tick (loiter 2.8, pursuit 4.0).", FONT_S, (190, 200, 215, 255))
    y = 60
    def place(i, im, cap, row):
        sheet.alpha_composite(zoom(im, z), (pad + i * (w * z + pad), y + row * (Hrow + pad)))
        label(d, (pad + i * (w * z + pad) + 4, y + row * (Hrow + pad) + h * z + 4), cap, FONT_S)
    # row 0: rest (landed, rotors still), hover frame, hover facing left, tilt label
    place(0, cell(D, 0, 1, 0, w, h, landed=True), "landed (rotors still)", 0)
    place(1, cell(D, 0, 1, 1), "hover, facing right", 0)
    place(2, cell(D, 0, -1, 1), "hover, facing left", 0)
    place(3, cell(D, 0, 1, 1, weapon=weapon_img), "with a mounted weapon (stand-in item)", 0)
    # row 1: tilt right at 3 speeds; row 2: tilt left
    for row, sd in ((1, 1), (2, -1)):
        for i, spd in enumerate((1.5, 2.8, 4.0)):
            t = max(-MAX_TILT, min(MAX_TILT, spd * TILT_PER_SPEED)) * sd
            place(i, cell(D, t, sd, 1 + i), "%s at %.1f px/tick (%.0f deg)" % ("right" if sd > 0 else "left", spd, abs(math.degrees(t))), row)
    # row 3: the rotor spin frames (rotors always level; body shown tilted so the independence is visible) + still frame
    for i in range(5):
        im = Image.new("RGBA", (w, h), BG)
        D.draw(im, (w // 2, h // 2 + 6), 0.2 if i < 4 else 0.0, 1, 0, landed=(i == 4), frame=i)
        place(i, im, "rotor spin frame %d (fan frame %d)" % (i, i) if i < 4 else "rotors still (landed)", 3)
    # row 4: vanilla comparison, same 4x
    van = [("NPC_24", (0, 0, 54, 56), "vanilla: Fire Imp"), ("NPC_32", (0, 0, 40, 56), "vanilla: Dark Caster"),
           ("NPC_36", (0, 0, 56, 76), "vanilla: Skeletron hand"), ("NPC_94", (0, 0, 64, 94), "vanilla: Eater of Souls")]
    place(0, cell(D, 0.0, 1, 1), "this drone", 4)
    for i, (n, box, cap) in enumerate(van):
        im = Image.new("RGBA", (w, h), BG)
        src = Image.open(os.path.join(vanilla_dir, n + ".png")).convert("RGBA").crop(box)
        s = min(1.0, (h - 4) / src.height)
        if s < 1: src = src.resize((max(1, int(src.width * s)), int(src.height * s)), Image.NEAREST)  # only the tall ones are shrunk (to fit the cell)
        im.alpha_composite(src, ((w - src.width) // 2, (h - src.height) // 2))
        place(i + 1, im, cap + ("" if s >= 1 else " (shrunk)"), 4)
    sheet.convert("RGB").save(path)


def flight_gif(key, path, weapon_img=None):
    D = Drone(key); W, H = 230, 100; z = 4
    frames = []
    x, vx, tilt, cam, sd = 0.0, 0.0, 0.0, 0.0, 1
    # script: hover -> accelerate right -> cruise -> settle -> accelerate left -> settle
    plan = [(30, 0.0), (80, 4.0), (60, 4.0), (70, 0.0), (80, -2.8), (70, -2.8), (70, 0.0)]
    tick = 0
    for dur, target in plan:
        for _ in range(dur):
            vx += (target - vx) * 0.07; x += vx; tilt = smooth_tilt(tilt, vx)
            if abs(vx) > 0.4: sd = 1 if vx > 0 else -1
            cam += (x - cam) * 0.05
            tick += 1
            if tick % 3: continue
            im = Image.new("RGBA", (W, H), BG); dr = ImageDraw.Draw(im)
            for gy in range(H):      # sky gradient
                f = gy / H; dr.line([(0, gy), (W, gy)], fill=(int(88 + 40 * f), int(118 + 50 * f), int(160 + 40 * f), 255))
            dr.rectangle([0, H - 10, W, H], fill=(52, 60, 44, 255)); dr.rectangle([0, H - 10, W, H - 9], fill=(86, 120, 60, 255))
            for k in range(-2, 12):  # ground posts scroll with the camera so the motion reads
                px = k * 40 - cam % 40 * 1.0
                dr.rectangle([px, H - 16, px + 1, H - 10], fill=(40, 36, 32, 255))
            D.draw(im, (W / 2 + (x - cam), 44 + math.sin(tick * 0.08) * 2), tilt, sd, tick, weapon=weapon_img)
            big = zoom(im, z).convert("RGB"); d = ImageDraw.Draw(big)
            label(d, (8, 6), "variant %s   vx %+.1f px/tick   body tilt %+.0f deg   rotors stay level" % (key, vx, math.degrees(tilt)), FONT, (255, 255, 255))
            frames.append(big)
    frames[0].save(path, save_all=True, append_images=frames[1:], duration=50, loop=0)


def variants_sheet(path):
    z = 4; w, h = 96, 60
    sheet = Image.new("RGBA", (3 * (w * z + 8) + 8, 60 + 3 * (h * z + 30)), (30, 32, 40, 255)); d = ImageDraw.Draw(sheet)
    label(d, (8, 8), "PICK A VARIANT: same art language, different proportions / rotor mounting", FONT)
    for ci, k in enumerate("ABC"):
        D = Drone(k)
        label(d, (8 + ci * (w * z + 8), 34), D.v["name"], FONT_S, (255, 220, 150, 255))
        for ri, (t, sd, landed) in enumerate(((0.0, 1, False), (0.3, 1, False), (-0.3, -1, False))):
            sheet.alpha_composite(zoom(cell(D, t, sd, 3 + ri, w, h, landed), z), (8 + ci * (w * z + 8), 54 + ri * (h * z + 30)))
        label(d, (8 + ci * (w * z + 8), 54 + 3 * (h * z + 30) - 24), "rest / tilt right / tilt left", FONT_S)
    sheet.convert("RGB").save(path)


if __name__ == "__main__":
    out, vdir = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    weapon = Image.open(os.path.join(vdir, "Item_1697.png")).convert("RGBA")
    for k in "ABC":
        contact_sheet(k, vdir, weapon, os.path.join(out, "drone_contact_%s.png" % k))
        flight_gif(k, os.path.join(out, "drone_flight_%s.gif" % k))
    variants_sheet(os.path.join(out, "drone_variants_compare.png"))
    print("ok")

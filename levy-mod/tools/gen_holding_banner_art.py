#!/usr/bin/env python3
"""Holding Banner art (tile 2x4, item 30x30, buff icon 32x32) in the vanilla banner palette, plus an enlarged review preview.

    python3 levy-mod/tools/gen_holding_banner_art.py [--preview OUT.png] [--vanilla DIR]

Writes Content/Tiles/HoldingBannerTile.png, Content/Items/Placeables/HoldingBannerItem.png, Content/Buffs/HoldingBannerBuff.png.
Tile sheet convention (CoordinateWidth 16, padding 2): a C x R tile object is a (C*18) x (R*18) sheet; tile (c, r) is the 16x16 square at (c*18, r*18).
Palette: the red banner of vanilla Tiles_91 (outline 70,7,8; cloth 116..139 reds; gold 233,191,63 / 241,214,132 / 236,200,94; grey pegs 76,76,76) and the wood of the vanilla lamp post (Tiles_93).
"""
import os, sys, argparse
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
LEVY = os.path.join(HERE, "..", "Levy")
CLEAR = (0, 0, 0, 0)
# vanilla red banner (Tiles_91)
R_OUT, R_A, R_B, R_C, R_HI = (70, 7, 8, 255), (130, 13, 15, 255), (121, 12, 14, 255), (116, 11, 13, 255), (139, 14, 16, 255)
G_MID, G_HI, G_SH = (233, 191, 63, 255), (241, 214, 132, 255), (236, 200, 94, 255)
PEG = (76, 76, 76, 255)
# vanilla lamp-post wood (Tiles_93)
W_OUT, W_MID, W_SH, W_HI = (50, 36, 28, 255), (148, 107, 80, 255), (97, 71, 57, 255), (177, 133, 103, 255)


def px(img, x, y, c):
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), c)


def rect(img, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            px(img, x, y, c)


def draw_banner(w, h, pole_x, top, bar_x1, cloth_x0, cloth_x1, cloth_y0, cloth_y1, tail, foot_y, emblem=True):
    """One standing war banner on a w x h canvas: pole at pole_x (3 px body), crossbar at `top`, cloth hanging from the bar, swallowtail of `tail` rows, foot plate at foot_y."""
    im = Image.new("RGBA", (w, h), CLEAR)
    # pole
    rect(im, pole_x - 2, 2, pole_x + 2, h - 1, W_OUT)
    rect(im, pole_x - 1, 2, pole_x + 1, h - 1, W_MID)
    rect(im, pole_x - 1, 2, pole_x - 1, h - 1, W_HI)
    rect(im, pole_x + 1, 2, pole_x + 1, h - 1, W_SH)
    # gold finial (a ball on the pole top)
    rect(im, pole_x - 2, 0, pole_x + 2, 4, W_OUT)
    rect(im, pole_x - 1, 0, pole_x + 1, 3, G_MID)
    px(im, pole_x - 1, 0, G_HI); px(im, pole_x - 1, 1, G_HI); px(im, pole_x + 1, 3, G_SH)
    px(im, pole_x - 2, 0, CLEAR); px(im, pole_x + 2, 0, CLEAR)
    # foot plate
    if foot_y is not None:
        rect(im, pole_x - 5, foot_y, pole_x + 5, h - 1, W_OUT)
        rect(im, pole_x - 4, foot_y + 1, pole_x + 4, h - 1, W_MID)
        rect(im, pole_x - 4, foot_y + 1, pole_x + 4, foot_y + 1, W_HI)
        rect(im, pole_x - 4, h - 1, pole_x + 4, h - 1, W_SH)
        px(im, pole_x - 5, h - 1, CLEAR); px(im, pole_x + 5, h - 1, CLEAR)
    # crossbar
    rect(im, pole_x - 2, top, bar_x1, top + 3, W_OUT)
    rect(im, pole_x - 1, top + 1, bar_x1 - 1, top + 2, W_MID)
    rect(im, pole_x - 1, top + 1, bar_x1 - 1, top + 1, W_HI)
    rect(im, pole_x - 1, top + 2, bar_x1 - 1, top + 2, W_SH)
    rect(im, bar_x1 - 1, top - 1, bar_x1, top + 4, PEG)  # grey peg at the bar end, like the vanilla banner top
    px(im, bar_x1, top - 1, CLEAR); px(im, bar_x1, top + 4, CLEAR)
    # cloth (outline, then stripes of folds)
    cw = cloth_x1 - cloth_x0 + 1
    for y in range(cloth_y0, cloth_y1 + 1):
        tail_row = y - (cloth_y1 - tail)  # >0 inside the swallowtail
        x0, x1 = cloth_x0, cloth_x1
        if tail_row > 0:
            notch = int(cw / 2 * min(1.0, (tail - tail_row + 1) / float(tail)))  # the cut deepens toward the middle, the sides stay
            # swallowtail: the middle is cut away, the outer thirds stay, shortened by one row per step
            pass
        for x in range(x0, x1 + 1):
            rel = x - cloth_x0
            edge = rel == 0 or x == x1
            c = R_OUT if edge else (R_HI if rel == 1 else R_C if rel % 6 == 3 else R_B if rel % 6 in (2, 4) else R_A)
            px(im, x, y, c)
    # swallowtail cut: a V notch from the bottom edge
    mid = (cloth_x0 + cloth_x1) / 2.0
    for k in range(tail + 1):
        y = cloth_y1 - k
        half = (tail - k) * (cw / 2.0) / (tail + 1.0) * 0.85
        for x in range(cloth_x0 + 1, cloth_x1):
            if abs(x + 0.5 - mid - 0.5) <= half - 0.3:
                px(im, x, y, CLEAR)
    # outline the notch and the bottom
    for y in range(cloth_y0, cloth_y1 + 1):
        for x in range(cloth_x0, cloth_x1 + 1):
            if im.getpixel((x, y))[3] == 0:
                continue
            nb = [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
            if any(0 <= nx < w and 0 <= ny < h and im.getpixel((nx, ny))[3] == 0 and ny >= cloth_y0 and nx > cloth_x0 - 1 and nx < cloth_x1 + 1 for nx, ny in nb) or y == cloth_y1:
                if y > cloth_y0 + 1:
                    px(im, x, y, R_OUT)
    # gold trim under the bar
    rect(im, cloth_x0 + 1, cloth_y0, cloth_x1 - 1, cloth_y0, G_SH)
    rect(im, cloth_x0 + 1, cloth_y0 + 1, cloth_x1 - 1, cloth_y0 + 1, G_MID)
    # gold emblem: a small shield / chevron crest centred in the cloth
    if emblem:
        cx = (cloth_x0 + cloth_x1) // 2
        cy = cloth_y0 + (cloth_y1 - tail - cloth_y0) // 2 + 1
        for dy in range(-4, 5):
            hw = 4 - abs(dy) // 2 if dy > 0 else 4
            for dx in range(-hw, hw + 1):
                px(im, cx + dx, cy + dy, G_MID)
        for dy in range(-3, 4):
            px(im, cx - 2, cy + dy, G_HI) if dy < 1 else None
        for dx in range(-2, 3):
            px(im, cx + dx, cy - 3, G_HI)
        for dy in range(-1, 3):
            hw = 1 if dy < 2 else 0
            for dx in range(-hw, hw + 1):
                px(im, cx + dx, cy + dy, R_A)
    return im


def tile_art():
    # 32 x 64 painted seamlessly, then cut into the padded 36 x 72 sheet (2 columns x 4 rows)
    art = draw_banner(32, 64, pole_x=5, top=7, bar_x1=30, cloth_x0=9, cloth_x1=28, cloth_y0=11, cloth_y1=44, tail=7, foot_y=59)
    sheet = Image.new("RGBA", (2 * 18, 4 * 18), CLEAR)
    for r in range(4):
        for c in range(2):
            sheet.paste(art.crop((c * 16, r * 16, c * 16 + 16, r * 16 + 16)), (c * 18, r * 18))
    return art, sheet


def item_art():
    # 30 x 30: the same standard, shrunk by hand (pole 3 px, bar, 14 px cloth with a notch)
    im = Image.new("RGBA", (30, 30), CLEAR)
    art = draw_banner(18, 28, pole_x=3, top=4, bar_x1=16, cloth_x0=6, cloth_x1=15, cloth_y0=8, cloth_y1=22, tail=4, foot_y=25, emblem=False)
    # small gold crest
    cx, cy = 10, 14
    for dy in range(-3, 4):
        hw = 2 if dy < 1 else 1 if dy < 3 else 0
        for dx in range(-hw, hw + 1):
            px(art, cx + dx, cy + dy, G_MID)
    px(art, cx - 1, cy - 2, G_HI); px(art, cx, cy - 2, G_HI)
    im.paste(art, (6, 1), art)
    return im


def buff_art():
    # 32 x 32 buff icon: the standard on a dark rounded plate, the way vanilla buff icons sit on their own backdrop
    im = Image.new("RGBA", (32, 32), CLEAR)
    d = ImageDraw.Draw(im)
    art = draw_banner(18, 28, pole_x=3, top=4, bar_x1=16, cloth_x0=6, cloth_x1=15, cloth_y0=8, cloth_y1=22, tail=4, foot_y=25, emblem=False)
    cx, cy = 10, 14
    for dy in range(-3, 4):
        hw = 2 if dy < 1 else 1 if dy < 3 else 0
        for dx in range(-hw, hw + 1):
            px(art, cx + dx, cy + dy, G_MID)
    px(art, cx - 1, cy - 2, G_HI); px(art, cx, cy - 2, G_HI)
    im.paste(art, (7, 2), art)
    return im


def preview(out, vanilla_dir):
    art, sheet = tile_art()
    item, buff = item_art(), buff_art()
    Z = 8
    bg_sky, bg_dark = (95, 150, 215, 255), (28, 24, 32, 255)
    pad = 12
    cells = []  # (title, image)
    v = lambda n: Image.open(os.path.join(vanilla_dir, n)).convert("RGBA") if vanilla_dir and os.path.exists(os.path.join(vanilla_dir, n)) else None
    t91 = v("Tiles_91.png")
    if t91 is not None:
        for i, name in enumerate(("vanilla red banner", "vanilla green banner", "vanilla blue banner")):
            col = Image.new("RGBA", (16, 48), CLEAR)
            for r in range(3):
                col.paste(t91.crop((i * 18 * 0 + i * 18 * 1 * 0, r * 18, 16, r * 18 + 16)) if False else t91.crop((i * 18, r * 18, i * 18 + 16, r * 18 + 16)), (0, r * 16))
            cells.append((name, col))
    t93 = v("Tiles_93.png")
    if t93 is not None:
        lamp = Image.new("RGBA", (16, 48), CLEAR)
        for r in range(3):
            lamp.paste(t93.crop((0, r * 18, 16, r * 18 + 16)), (0, r * 16))
        cells.append(("vanilla lamp post", lamp))
    cells.append(("patrol banner (mod)", Image.open(os.path.join(LEVY, "Content/Tiles/PatrolBannerTile.png")).convert("RGBA").crop((0, 0, 16, 54)).resize((16, 54), Image.NEAREST)))
    cells.append(("HOLDING BANNER tile", art))
    cells.append(("item 30x30", item))
    cells.append(("buff 32x32", buff))
    W = sum(c[1].width * Z + pad for c in cells) + pad
    H = max(c[1].height for c in cells) * Z + 2 * pad + 22
    for bg, name in ((bg_sky, out), (bg_dark, out.replace(".png", "_dark.png"))):
        canvas = Image.new("RGBA", (W, H), bg)
        d = ImageDraw.Draw(canvas)
        x = pad
        for title, im in cells:
            big = im.resize((im.width * Z, im.height * Z), Image.NEAREST)
            canvas.paste(big, (x, H - pad - big.height), big)
            d.text((x, 4), title, fill=(255, 255, 255, 255))
            x += big.width + pad
        canvas.convert("RGB").save(name)
        print("preview", name, canvas.size)
    # in context: a row of grass tiles, banners and the vanilla red banner hung above
    ctx = Image.new("RGBA", (16 * 14, 16 * 8), bg_sky)
    for i in range(14):
        for j in range(2):
            ctx.paste(Image.new("RGBA", (16, 16), (110, 74, 48, 255) if j else (45, 160, 60, 255)), (i * 16, (6 + j) * 16))
    for bx in (2, 7, 11):
        ctx.paste(art, (bx * 16, 6 * 16 - 64), art)
    ctx = ctx.resize((ctx.width * 5, ctx.height * 5), Image.NEAREST)
    ctx.convert("RGB").save(out.replace(".png", "_context.png")); print("context", out.replace(".png", "_context.png"))


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview")
    ap.add_argument("--vanilla", default=None)
    a = ap.parse_args()
    art, sheet = tile_art()
    sheet.save(os.path.join(LEVY, "Content/Tiles/HoldingBannerTile.png"))
    item_art().save(os.path.join(LEVY, "Content/Items/Placeables/HoldingBannerItem.png"))
    buff_art().save(os.path.join(LEVY, "Content/Buffs/HoldingBannerBuff.png"))
    print("wrote tile", sheet.size)
    if a.preview:
        preview(a.preview, a.vanilla)

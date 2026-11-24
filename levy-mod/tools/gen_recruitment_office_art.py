#!/usr/bin/env python3
"""Recruitment Office art: a 3x2 wooden desk with a brass sundial and an open ledger (placeholder, vanilla-furniture palette).

    python3 levy-mod/tools/gen_recruitment_office_art.py [--preview OUT.png]

Writes Content/Tiles/RecruitmentOfficeTile.png (54x36 sheet: 3x2 tiles, 16px with 2px padding) and Content/Items/Placeables/RecruitmentOfficeItem.png (30x30).
"""
import os, argparse
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
LEVY = os.path.join(HERE, "..", "Levy")
CLEAR = (0, 0, 0, 0)
W_OUT, W_MID, W_SH, W_HI = (50, 36, 28, 255), (148, 107, 80, 255), (97, 71, 57, 255), (177, 133, 103, 255)
G_MID, G_HI, G_SH, G_OUT = (233, 191, 63, 255), (241, 214, 132, 255), (186, 142, 38, 255), (110, 76, 14, 255)
PAPER, PAPER_SH, INK, SPINE = (226, 208, 170, 255), (190, 170, 130, 255), (70, 50, 40, 255), (130, 13, 15, 255)


def px(im, x, y, c):
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c)


def rect(im, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            px(im, x, y, c)


def desk():
    """48 x 32: desk top at y 18..21, legs and a drawer apron below, sundial on the left, ledger on the right."""
    im = Image.new("RGBA", (48, 32), CLEAR)
    # legs
    for lx in (3, 40):
        rect(im, lx, 22, lx + 4, 31, W_OUT)
        rect(im, lx + 1, 22, lx + 3, 31, W_MID)
        rect(im, lx + 1, 22, lx + 1, 31, W_HI)
        rect(im, lx + 3, 22, lx + 3, 31, W_SH)
    # apron with two drawers
    rect(im, 8, 22, 39, 27, W_OUT)
    rect(im, 9, 23, 38, 26, W_MID)
    rect(im, 9, 23, 38, 23, W_HI)
    rect(im, 9, 26, 38, 26, W_SH)
    rect(im, 23, 23, 24, 26, W_OUT)
    for kx in (15, 32):
        rect(im, kx, 24, kx + 1, 25, G_MID)
        px(im, kx, 24, G_HI)
    # desk top
    rect(im, 1, 18, 46, 22, W_OUT)
    rect(im, 2, 19, 45, 21, W_MID)
    rect(im, 2, 19, 45, 19, W_HI)
    rect(im, 2, 21, 45, 21, W_SH)
    # sundial: brass plate with tick marks and a gnomon
    rect(im, 5, 14, 20, 17, G_OUT)
    rect(im, 6, 15, 19, 17, G_MID)
    rect(im, 6, 15, 19, 15, G_HI)
    rect(im, 6, 17, 19, 17, G_SH)
    for tx in (7, 9, 11, 14, 16, 18):
        px(im, tx, 16, G_OUT)
    for k in range(9):  # gnomon: a thin wedge rising from the plate
        px(im, 12 + k // 3, 14 - k, G_OUT)
        px(im, 13 + k // 3, 14 - k, G_MID if k < 7 else G_HI)
    rect(im, 12, 14, 15, 14, G_SH)
    # an open ledger
    rect(im, 26, 14, 41, 18, INK)
    rect(im, 27, 15, 33, 17, PAPER)
    rect(im, 34, 15, 40, 17, PAPER)
    rect(im, 27, 17, 40, 17, PAPER_SH)
    rect(im, 33, 14, 34, 18, SPINE)
    for lx in (28, 30, 35, 37):
        px(im, lx, 16, INK); px(im, lx + 1, 16, INK)
    # a quill leaning on the ledger
    for k in range(6):
        px(im, 43 + k // 2, 17 - k, W_HI if k < 3 else PAPER)
    return im


def tile_sheet(art):
    sheet = Image.new("RGBA", (3 * 18, 2 * 18), CLEAR)
    for r in range(2):
        for c in range(3):
            sheet.paste(art.crop((c * 16, r * 16, c * 16 + 16, r * 16 + 16)), (c * 18, r * 18))
    return sheet


def item_art(art):
    im = Image.new("RGBA", (30, 30), CLEAR)
    small = art.crop((0, 8, 48, 32)).resize((24, 12), Image.NEAREST)  # the desk, halved
    im.paste(small, (3, 9), small)
    return im


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview")
    a = ap.parse_args()
    art = desk()
    tile_sheet(art).save(os.path.join(LEVY, "Content", "Tiles", "RecruitmentOfficeTile.png"))
    item_art(art).save(os.path.join(LEVY, "Content", "Items", "Placeables", "RecruitmentOfficeItem.png"))
    if a.preview:
        pv = Image.new("RGBA", (48 * 6 + 30 * 6 + 24, 32 * 6), (40, 60, 90, 255))
        pv.paste(art.resize((48 * 6, 32 * 6), Image.NEAREST), (0, 0), art.resize((48 * 6, 32 * 6), Image.NEAREST))
        it = item_art(art).resize((180, 180), Image.NEAREST)
        pv.paste(it, (48 * 6 + 24, 0), it)
        pv.save(a.preview)

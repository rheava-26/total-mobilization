#!/usr/bin/env python3
"""Placeholder pixel art for the Levy tModLoader mod (PIL). Run from anywhere:

    python3 levy-tools/gen_sprites.py [path/to/levy]

TILE SHEET CONVENTION (tModLoader / vanilla TileObjectData with CoordinateWidth 16, CoordinatePadding 2):
  a multi-tile of C columns x R rows is a sheet of (C*18) x (R*18) pixels. Tile (c, r) is drawn from the 16x16 square at
  x = c*18 .. c*18+15, y = r*18 .. r*18+15 - i.e. each 16px tile sits on an 18px stride with a 2px transparent gutter on
  its right and bottom. The 2px padding is never drawn. We paint the whole object on a seamless (C*16) x (R*16) canvas
  and then slice it into the padded sheet, so lines run continuously across tile borders.

Items / NPCs / projectiles are plain single-frame PNGs (SoldierNPC uses Main.npcFrameCount = 1).
"""
import os
import sys
from PIL import Image, ImageDraw

ROOT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "levy")
TILE, STRIDE = 16, 18
CLEAR = (0, 0, 0, 0)

# palette
WOOD, WOOD_D, WOOD_L = (150, 105, 60, 255), (105, 70, 38, 255), (185, 140, 85, 255)
ROOF, ROOF_D = (120, 55, 40, 255), (85, 38, 28, 255)
STONE, STONE_D, STONE_L = (112, 112, 125, 255), (72, 72, 85, 255), (150, 150, 165, 255)
IRON, IRON_L = (160, 165, 175, 255), (215, 220, 230, 255)
RED, RED_D, RED_L = (175, 40, 40, 255), (120, 25, 25, 255), (215, 80, 70, 255)
GOLD, GOLD_D = (230, 190, 70, 255), (165, 125, 35, 255)
BLACK = (25, 20, 20, 255)
GLASS = (140, 190, 220, 255)
SKIN = (240, 200, 160, 255)
IVORY, IVORY_D = (235, 225, 200, 255), (190, 175, 140, 255)


def canvas(w, h):
    return Image.new("RGBA", (w, h), CLEAR)


def rect(d, x0, y0, x1, y1, fill, outline=None):
    """Inclusive pixel rectangle; corners may be given in any order (taper loops can cross over)."""
    if x1 < x0:
        x0, x1 = x1, x0
    if y1 < y0:
        y0, y1 = y1, y0
    d.rectangle([x0, y0, x1, y1], fill=fill, outline=outline)


def slice_to_sheet(art, cols, rows):
    """Seamless (cols*16)x(rows*16) art -> padded tile sheet (cols*18)x(rows*18)."""
    assert art.size == (cols * TILE, rows * TILE), art.size
    sheet = canvas(cols * STRIDE, rows * STRIDE)
    for r in range(rows):
        for c in range(cols):
            cell = art.crop((c * TILE, r * TILE, (c + 1) * TILE, (r + 1) * TILE))
            sheet.paste(cell, (c * STRIDE, r * STRIDE))
    return sheet


def save(img, rel):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    print(f"{rel}  {img.size[0]}x{img.size[1]}")


def fit_icon(art, size):
    """Nearest-neighbour downscale into a size x size icon, centred."""
    w, h = art.size
    scale = min(size / w, size / h)
    nw, nh = max(1, int(w * scale)), max(1, int(h * scale))
    small = art.resize((nw, nh), Image.NEAREST)
    icon = canvas(size, size)
    icon.paste(small, ((size - nw) // 2, (size - nh) // 2))
    return icon


# ----------------------------------------------------------------------------- barracks 3x3: wooden guardhouse
def barracks_art():
    img = canvas(48, 48)
    d = ImageDraw.Draw(img)
    # walls (planks)
    rect(d, 2, 14, 45, 47, WOOD, outline=WOOD_D)
    for y in range(18, 46, 6):
        d.line([(3, y), (44, y)], fill=WOOD_D)
    # roof: peaked, overhanging
    for i in range(8):
        rect(d, 1 + i * 3, 13 - i, 46 - i * 3, 13 - i, ROOF)
    d.line([(1, 13), (46, 13)], fill=ROOF_D)
    d.line([(0, 14), (47, 14)], fill=ROOF_D)
    rect(d, 22, 4, 25, 7, ROOF_D)  # ridge cap
    # door, bottom centre
    rect(d, 20, 30, 27, 47, WOOD_D, outline=BLACK)
    d.point((26, 39), fill=GOLD)
    rect(d, 21, 31, 26, 36, (90, 60, 32, 255))
    # window left + right
    rect(d, 7, 22, 13, 28, GLASS, outline=BLACK)
    d.line([(10, 22), (10, 28)], fill=BLACK)
    d.line([(7, 25), (13, 25)], fill=BLACK)
    rect(d, 34, 22, 40, 28, GLASS, outline=BLACK)
    d.line([(37, 22), (37, 28)], fill=BLACK)
    d.line([(34, 25), (40, 25)], fill=BLACK)
    # flagpole + flag (top right)
    rect(d, 40, 0, 40, 12, IRON_L)
    rect(d, 41, 1, 47, 5, RED, outline=RED_D)
    d.point((45, 3), fill=GOLD)
    # sign board over the door with a chevron
    rect(d, 17, 16, 30, 20, WOOD_L, outline=WOOD_D)
    d.line([(21, 19), (23, 17), (25, 19)], fill=RED_D)
    # stone footing
    rect(d, 0, 46, 47, 47, STONE_D)
    return img


# ----------------------------------------------------------------------------- armory 2x3: weapon racks
def armory_art():
    img = canvas(32, 48)
    d = ImageDraw.Draw(img)
    rect(d, 0, 0, 31, 47, STONE, outline=STONE_D)
    for y in range(6, 48, 8):  # brick courses
        d.line([(1, y), (30, y)], fill=STONE_D)
    for y, off in ((0, 7), (8, 15), (16, 7), (24, 15), (32, 7), (40, 15)):
        d.line([(off, y + 1), (off, y + 5)], fill=STONE_D)
    # top rack: three swords hanging
    rect(d, 2, 4, 29, 5, WOOD_D)
    for x in (6, 15, 24):
        rect(d, x, 6, x, 17, IRON_L)          # blade
        rect(d, x - 2, 8, x + 2, 8, GOLD_D)   # crossguard
        rect(d, x, 9, x, 11, WOOD_D)          # grip
        d.point((x, 6), fill=IRON)
    # middle shelf: shield + helmet
    rect(d, 2, 22, 29, 23, WOOD_D)
    rect(d, 5, 24, 12, 31, RED, outline=IRON)
    d.line([(8, 25), (8, 30)], fill=GOLD)
    d.line([(6, 27), (11, 27)], fill=GOLD)
    rect(d, 18, 25, 26, 31, IRON, outline=STONE_D)
    rect(d, 20, 28, 24, 29, STONE_D)
    rect(d, 21, 24, 23, 24, IRON_L)
    # bottom: barrel of arrows + crate
    rect(d, 3, 38, 11, 47, WOOD, outline=WOOD_D)
    d.line([(3, 42), (11, 42)], fill=WOOD_D)
    for x in (5, 7, 9):
        rect(d, x, 33, x, 38, WOOD_L)
        d.point((x, 33), fill=IRON_L)
    rect(d, 16, 39, 29, 47, WOOD_L, outline=WOOD_D)
    d.line([(16, 39), (29, 47)], fill=WOOD_D)
    d.line([(29, 39), (16, 47)], fill=WOOD_D)
    rect(d, 0, 46, 31, 47, STONE_D)
    return img


# ----------------------------------------------------------------------------- patrol banner 1x3: hanging cloth
def patrol_banner_art():
    img = canvas(16, 48)
    d = ImageDraw.Draw(img)
    rect(d, 2, 0, 13, 1, WOOD_D)      # crossbar on the ceiling
    rect(d, 7, 0, 8, 3, IRON)         # hook
    rect(d, 3, 3, 12, 38, RED, outline=RED_D)
    rect(d, 4, 4, 4, 37, RED_L)       # highlight edge
    # tapered tail
    for i in range(6):
        rect(d, 3 + i, 39 + i, 12 - i, 39 + i, RED if i < 5 else RED_D)
    # gold emblem: chevron + dot
    d.line([(5, 18), (8, 13), (10, 18)], fill=GOLD)
    d.line([(5, 22), (8, 17), (10, 22)], fill=GOLD)
    d.point((8, 26), fill=GOLD)
    rect(d, 4, 30, 11, 30, GOLD_D)
    return img


# ----------------------------------------------------------------------------- war horn 30x26
def war_horn_art():
    img = canvas(30, 26)
    d = ImageDraw.Draw(img)
    # curved horn body: a thick arc drawn as stacked ellipses
    for t in range(12):
        x = 4 + t * 2
        y = 20 - int((t / 11) ** 1.6 * 16)
        r = 2 + t // 3
        d.ellipse([x - r, y - r, x + r, y + r], fill=IVORY, outline=IVORY_D)
    # bell opening
    d.ellipse([21, 0, 29, 10], fill=IVORY, outline=IVORY_D)
    d.ellipse([23, 2, 27, 8], fill=(90, 60, 50, 255))
    # mouthpiece
    rect(d, 1, 19, 4, 22, GOLD, outline=GOLD_D)
    # gold bands
    rect(d, 9, 15, 12, 20, GOLD, outline=GOLD_D)
    rect(d, 17, 7, 20, 14, GOLD, outline=GOLD_D)
    # strap
    d.line([(3, 23), (12, 25), (22, 12)], fill=WOOD_D)
    return img


# ----------------------------------------------------------------------------- books 24x28
def book_art(cover, accent, mark):
    img = canvas(24, 28)
    d = ImageDraw.Draw(img)
    dark = tuple(max(0, c - 60) for c in cover[:3]) + (255,)
    rect(d, 3, 1, 22, 26, cover, outline=dark)
    rect(d, 3, 1, 6, 26, dark)             # spine
    rect(d, 7, 25, 22, 26, IVORY_D)        # pages at the bottom
    rect(d, 21, 2, 22, 24, IVORY_D)        # pages at the side
    rect(d, 9, 5, 19, 7, accent)           # title bar
    rect(d, 9, 20, 19, 21, accent)
    # emblem per book
    if mark == "chevron":
        d.line([(10, 16), (14, 11), (18, 16)], fill=accent)
    elif mark == "target":
        d.ellipse([11, 10, 17, 16], outline=accent)
        d.point((14, 13), fill=accent)
    elif mark == "coin":
        d.ellipse([11, 10, 17, 16], fill=accent, outline=dark)
    elif mark == "wall":
        rect(d, 10, 11, 18, 16, accent)
        d.line([(10, 13), (18, 13)], fill=dark)
        d.line([(14, 11), (14, 13)], fill=dark)
        d.line([(12, 13), (12, 16)], fill=dark)
        d.line([(16, 13), (16, 16)], fill=dark)
    elif mark == "stripes":
        for y in (11, 13, 15):
            d.line([(10, y), (18, y)], fill=accent)
    return img


BOOKS = {
    "GuideToConscription": ((160, 40, 40, 255), GOLD, "chevron"),
    "GuideToDrill": ((40, 70, 150, 255), IVORY, "target"),
    "GuideToPlunder": ((120, 90, 20, 255), GOLD, "coin"),
    "GuideToFortification": ((85, 85, 95, 255), STONE_L, "wall"),
    "GuideToDiscipline": ((40, 110, 60, 255), IVORY, "stripes"),
}


# ----------------------------------------------------------------------------- soldier placeholder 20x42
def soldier_art():
    img = canvas(20, 42)
    d = ImageDraw.Draw(img)
    body = (60, 60, 70, 200)
    d.ellipse([5, 1, 14, 10], fill=SKIN, outline=body)   # head
    rect(d, 5, 11, 14, 25, body)                          # torso
    rect(d, 2, 12, 4, 22, body)                           # arms
    rect(d, 15, 12, 17, 22, body)
    rect(d, 6, 26, 9, 40, body)                           # legs
    rect(d, 10, 26, 13, 40, body)
    rect(d, 5, 40, 14, 41, BLACK)                         # boots
    rect(d, 15, 4, 15, 30, WOOD_D)                        # a spear shaft, so it reads as a soldier
    d.point((15, 3), fill=IRON_L)
    return img


# ----------------------------------------------------------------------------- mod icon 80x80
def icon_art():
    img = canvas(80, 80)
    d = ImageDraw.Draw(img)
    rect(d, 0, 0, 79, 79, (40, 32, 28, 255), outline=(20, 15, 12, 255))
    # banner
    rect(d, 14, 6, 44, 60, RED, outline=RED_D)
    for i in range(10):
        rect(d, 14 + i, 61 + i, 44 - i, 61 + i, RED if i < 9 else RED_D)
    rect(d, 10, 4, 48, 7, WOOD_D)
    d.line([(20, 34), (29, 20), (38, 34)], fill=GOLD, width=3)
    d.line([(20, 46), (29, 32), (38, 46)], fill=GOLD, width=3)
    # horn in front, bottom right
    for t in range(14):
        x = 36 + t * 2.4
        y = 70 - (t / 13) ** 1.6 * 28
        r = 3 + t // 3
        d.ellipse([x - r, y - r, x + r, y + r], fill=IVORY, outline=IVORY_D)
    d.ellipse([64, 32, 78, 50], fill=IVORY, outline=IVORY_D)
    d.ellipse([67, 36, 75, 46], fill=(90, 60, 50, 255))
    rect(d, 32, 67, 36, 73, GOLD, outline=GOLD_D)
    rect(d, 46, 60, 50, 68, GOLD, outline=GOLD_D)
    rect(d, 57, 46, 61, 56, GOLD, outline=GOLD_D)
    return img


def main():
    print(f"writing sprites under {ROOT}")
    b, a, p = barracks_art(), armory_art(), patrol_banner_art()
    save(slice_to_sheet(b, 3, 3), "Content/Tiles/BarracksTile.png")
    save(slice_to_sheet(a, 2, 3), "Content/Tiles/ArmoryTile.png")
    save(slice_to_sheet(p, 1, 3), "Content/Tiles/PatrolBannerTile.png")
    save(fit_icon(b, 30), "Content/Items/Placeables/BarracksItem.png")
    save(fit_icon(a, 30), "Content/Items/Placeables/ArmoryItem.png")
    save(fit_icon(p, 30), "Content/Items/Placeables/PatrolBannerItem.png")
    save(war_horn_art(), "Content/Items/WarHorn.png")
    for name, (cover, accent, mark) in BOOKS.items():
        save(book_art(cover, accent, mark), f"Content/Items/Books/{name}.png")
    save(soldier_art(), "Content/NPCs/SoldierNPC.png")
    save(canvas(18, 18), "Content/Projectiles/SoldierThrust.png")
    save(icon_art(), "icon.png")


if __name__ == "__main__":
    main()

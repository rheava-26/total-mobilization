#!/usr/bin/env python3
"""Vanilla-style item sprites: Barracks Ale, Black Stout, Spiced Rum, Meteor Beacon.
Flat pixel art in vanilla Terraria conventions: small canvas, 1px outline in the item's own darkest shade (not black),
3-4 flat shades, a simple left highlight, no gradients or anti-aliasing.
Each sprite is a character grid (one char = one pixel, '.' = transparent) plus a palette.
Usage: python3 gen_ale_sprites.py [out_dir] [preview_dir]
(default out: Levy/Content/Items/OOA, MeteorBeacon goes one folder up; preview_dir receives x4 PNGs)."""
import os, sys
from PIL import Image


def H(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5)) + (255,)


def make(rows, pal):
    w = max(len(r) for r in rows)
    img = Image.new("RGBA", (w, len(rows)), (0, 0, 0, 0))
    for y, r in enumerate(rows):
        for x, c in enumerate(r):
            if c != '.':
                img.putpixel((x, y), pal[c])
    return img


# ---- Barracks Ale: iron-banded wooden tankard, amber ale, cream foam. 18x20 ----
# o outline, f foam light, F foam shade, a ale light, A ale mid, d ale dark, i iron light, I iron mid, w wood light, W wood mid, v wood dark
BARRACKS = [
    "..oooooooooo......",
    ".offfffffffFfo....",
    "offfFffffffffFo...",
    "offffffFfffffFo...",
    ".oFFFFFFFFFFFo....",
    ".oiiiiiiiiiiIo....",
    ".oIIIIIIIIIIIo....",
    ".owWWWWWWWWWvoooo.",
    ".owWWWWWWWWWvoWWWo",
    ".owWWWWWWWWWvoWvvo",
    ".owWWWWWWWWWvo.oWo",
    ".oiiiiiiiiiiIo.oWo",
    ".oIIIIIIIIIIIo.oWo",
    ".owWWWWWWWWWvoWWvo",
    ".owWWWWWWWWWvoWWo.",
    ".owWWWWWWWWWvoooo.",
    ".oiiiiiiiiiiIo....",
    ".oIIIIIIIIIIIo....",
    ".owvvvvvvvvvvo....",
    "..oooooooooooo....",
]
BARRACKS_P = dict(o=H('#2b1a0e'), f=H('#fff1c0'), F=H('#e3bf63'), i=H('#c9ced6'), I=H('#8d949f'),
                  w=H('#c0813f'), W=H('#93602b'), v=H('#5f3b1c'))

# ---- Black Stout: pint-style mug of near-black stout with a tan foam head. 18x20 ----
# o outline, t foam light, T foam shade, k stout light, K stout mid, D stout dark, g glass-glint
STOUT = [
    "...oooooooooo.....",
    "..ottttttTtttto...",
    ".otttttTttttttTo..",
    ".ottttttttTttto...",
    "..oTTTTTTTTTTTo...",
    "..okkKKKKKKKKKooooo",
    "..okKKKKKKKKKKoKKKo",
    "..okKKKKKKKKKKo.oKo",
    "..okKKKKKKKKKKo.oKo",
    "..okKKKKKKKKKKo.oKo",
    "..okKKKKKKKKKKo.oKo",
    "..okKKKKKKKKKKoKKKo",
    "..okKKKKKKKKKKooooo",
    "..okKKKKKKKKKKDo...",
    "..okKKKKKKKKKDDo...",
    "..okKKKKKKKKDDDo...",
    "..oKKKKKKKKDDDDo...",
    "..oDDDDDDDDDDDDo...",
    "..oooooooooooooo...",
]
STOUT_P = dict(o=H('#120b0a'), t=H('#f3dba6'), T=H('#c79f62'), k=H('#5a382a'), K=H('#34201a'), D=H('#1d1210'))

# ---- Spiced Rum: potion-style bottle, red-brown rum, grey cork stopper. 20x26 (vanilla potions are ~20x26) ----
# o outline, c cork light, C cork dark, g glass light, G glass dark, r rum light, R rum mid, s rum dark, h highlight
RUM = [
    ".......ooooooo......",
    "......occccCCCo.....",
    "......occcccCCo.....",
    "......oocccCCoo.....",
    ".......ogggGGo......",
    ".......ogggGGo......",
    ".......ogggGGo......",
    "......orrrrRRso.....",
    ".....orhrrrRRRso....",
    "....orhrrrrRRRRso...",
    "...orhrrrrrRRRRRso..",
    "...orhrrrrrRRRRRso..",
    "...orrrrrrrRRRRRso..",
    "...orrrrrrrRRRRRso..",
    "...orrrrrrrRRRRRso..",
    "...orrrrrrrRRRRRso..",
    "...orrrrrrrRRRRRso..",
    "...orrrrrrrRRRRRso..",
    "...orrrrrrRRRRRRso..",
    "...orrrrrRRRRRRsso..",
    "...oRRRRRRRRRRsssso.",
    "....osRRRRRRRRssso..",
    ".....ossssssssssso..",
    "......ooooooooooo...",
]
RUM_P = dict(o=H('#3a1410'), c=H('#c9b48a'), C=H('#8f7c58'), g=H('#cfd9e0'), G=H('#8e9ba6'),
             r=H('#c2522c'), R=H('#9c3a1e'), s=H('#6a2412'), h=H('#f08a55'))

# ---- Meteor Beacon: small glowing meteorite chunk with an orange core, like a vanilla ore/bar icon. 20x20 ----
# o outline, s rock light, S rock mid, d rock dark, e ember core, E bright core, y hot yellow
BEACON = [
    "....................",
    ".......ooooo........",
    ".....ooSssssoo......",
    "....oSssssssSSo.....",
    "...oSssSeeesSSSoo...",
    "..osssSeEEEeSSSSdo..",
    "..osssSeEyyEeSSSdo..",
    ".oSssSeEyyyEeSSSddo.",
    ".oSsSSeEyyyEeSSSddo.",
    ".oSSSSeEEyEEeSSSddo.",
    ".oSSSSSeEEEeSSSdddo.",
    "..oSSSSSeeeSSSSddo..",
    "..odSSSSSSSSSSdddo..",
    "...oddSSSSSSSddoo...",
    "....oodddddddoo.....",
    "......ooooooo.......",
    "....................",
]
BEACON_P = dict(o=H('#3a1a10'), s=H('#8e8a92'), S=H('#625e68'), d=H('#3f3b46'),
                e=H('#e8741c'), E=H('#f6a02a'), y=H('#ffe27a'))

SPRITES = [("BarracksAle", BARRACKS, BARRACKS_P), ("BlackStout", STOUT, STOUT_P),
           ("SpicedRum", RUM, RUM_P), ("MeteorBeacon", BEACON, BEACON_P)]


def main(out, preview=None):
    made = {}
    for name, rows, pal in SPRITES:
        img = make(rows, pal)
        dest = os.path.join(out, "..", name + ".png") if name == "MeteorBeacon" else os.path.join(out, name + ".png")
        img.save(dest)
        made[name] = img
        print(name, img.size)
    if preview:
        os.makedirs(preview, exist_ok=True)
        imgs = [made[n] for n, _, _ in SPRITES]
        ale = os.environ.get("VANILLA_ALE")
        if ale and os.path.exists(ale):
            imgs = [Image.open(ale).convert("RGBA")] + imgs
        for n, im in made.items():
            im.resize((im.width * 4, im.height * 4), Image.NEAREST).save(os.path.join(preview, n + "_x4.png"))
        pad = 12
        bg = (74, 96, 140, 255)
        W = sum(i.width * 4 + pad for i in imgs) + pad
        Hh = max(i.height for i in imgs) * 4 + 2 * pad
        sheet = Image.new("RGBA", (W, Hh), bg)
        x = pad
        for im in imgs:
            big = im.resize((im.width * 4, im.height * 4), Image.NEAREST)
            sheet.alpha_composite(big, (x, pad))
            x += big.width + pad
        sheet.save(os.path.join(preview, "all_x4.png"))


if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "..", "Levy", "Content", "Items", "OOA"),
         sys.argv[2] if len(sys.argv) > 2 else None)

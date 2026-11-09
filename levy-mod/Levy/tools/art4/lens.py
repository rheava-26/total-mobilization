"""Spectral Lens (vanity accessory): a pale ghost-glass lens in a dark goggle rim with two short straps.

2x2 half-res grid, 16 x 10 cells -> 32 x 20 px. Every cell is typed by hand below; nothing is dithered by a script.
Palette = the approved Spectator Potion sprite's own hexes, so the two read as one family:
  O #282c54 outline (the item's darkest blue), a #44507a rim shade, b #6870a2 rim mid, h #aab2d8 rim light,
  g #aab2d8 glass shade, c #ccd3ee glass, d #e2e7f9 glass light, e #f4f6ff glass pale, w #ffffff glint,
  t #343648 strap mid, u #565a6c strap light, v #82869c strap catch-light,
  y #b0e2ee and z #cec4ee: single ghost-tint cells (pale cyan, pale lilac) sprinkled in the glass as the spectral noise.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib4 import G, check

PAL = dict(O='#282c54', a='#44507a', b='#6870a2', h='#aab2d8', g='#aab2d8', c='#ccd3ee', d='#e2e7f9', e='#f4f6ff', w='#ffffff',
           t='#343648', u='#565a6c', v='#82869c', y='#b0e2ee', z='#cec4ee')

ART = [
    ".....OOOOOO.....",
    "....OhhbbbbO....",
    "...OhhwedcbaO...",
    "OOOOhweeddcaOOOO",
    "OuvOheddccgaOutO",
    "OttOhddccygaOttO",
    "OOOObcczgggaOOOO",
    "...ObbcgggaaO...",
    "....ObbbaaaO....",
    ".....OOOOOO.....",
]
check(ART, 16, 'lens')

g = G(16, 10, PAL)
g.art(ART)
im = g.render()
HERE = os.path.dirname(os.path.abspath(__file__))
MOD = os.path.abspath(os.path.join(HERE, '..', '..'))
im.save(os.path.join(MOD, 'Content/Items/SpectralLens.png'))
os.makedirs(os.path.join(HERE, 'png'), exist_ok=True)
im.resize((im.width * 4, im.height * 4), 0).save(os.path.join(HERE, 'png', 'SpectralLens_preview4x.png'))
print(im.size)

"""Levy art pass 4 shared helpers.

Everything is hand-placed ASCII on a HALF-resolution cell grid and rendered doubled (strict 2x2).
No generated dithering: every shading cell and noise cell is typed into the ASCII by hand.
Palette hexes are copied from vanilla reference sprites (see palette.py) and are named in each script.
"""
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SCRATCH = '/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad'
REFS = SCRATCH + '/vanilla-refs'
OUT = SCRATCH + '/art4'
PNG = os.path.join(HERE, 'png')          # staging copies of every new sprite (small, ours)
MOD = os.path.abspath(os.path.join(HERE, '..', '..'))   # levy-mod/Levy
os.makedirs(OUT, exist_ok=True)
os.makedirs(PNG, exist_ok=True)


def hx(s):
    s = s.lstrip('#')
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


class G:
    def __init__(s, w, h, pal):
        s.w, s.h = w, h
        s.pal = {k: hx(v) for k, v in pal.items()}
        s.c = [['.'] * w for _ in range(h)]

    def put(s, x, y, ch):
        if 0 <= x < s.w and 0 <= y < s.h:
            s.c[y][x] = ch

    def art(s, rows, x0=0, y0=0):
        """Stamp ASCII. '.' keeps what is underneath; ' ' too; '_' erases."""
        for j, row in enumerate(rows):
            for i, ch in enumerate(row):
                if ch in '. ':
                    continue
                s.put(x0 + i, y0 + j, '.' if ch == '_' else ch)

    def rect(s, x0, y0, x1, y1, ch):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                s.put(x, y, ch)

    def render(s):
        im = Image.new('RGBA', (s.w * 2, s.h * 2), (0, 0, 0, 0))
        px = im.load()
        for y in range(s.h):
            for x in range(s.w):
                ch = s.c[y][x]
                if ch == '.':
                    continue
                col = s.pal[ch]
                for dy in (0, 1):
                    for dx in (0, 1):
                        px[x * 2 + dx, y * 2 + dy] = col
        return im


def check(rows, w, name=''):
    for i, r in enumerate(rows):
        assert len(r) == w, '%s row %d len %d != %d: %r' % (name, i, len(r), w, r)


def gutters(im, tw, th, pad=2, fill=False):
    """Lay a continuous (tw*16 x th*16) image out on tModLoader's tile sheet: every 16px tile is
    followed by `pad` transparent pixels (the 2px CoordinatePadding), final size tw*18 x th*18.
    fill=True repeats the tile's own last row/column into the gutter (harmless, avoids edge bleed)."""
    out = Image.new('RGBA', (tw * (16 + pad), th * (16 + pad)), (0, 0, 0, 0))
    for j in range(th):
        for i in range(tw):
            tile = im.crop((i * 16, j * 16, i * 16 + 16, j * 16 + 16))
            out.paste(tile, (i * (16 + pad), j * (16 + pad)))
    return out


def save(im, name, dest=None):
    """Write to staging dir (and to the mod path if dest given, relative to levy-mod/Levy)."""
    im.save(os.path.join(PNG, name + '.png'))
    if dest:
        p = os.path.join(MOD, dest)
        im.save(p)
    return im

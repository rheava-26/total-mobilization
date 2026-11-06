"""Levy 2.0 art pass 3. Hand-drawn ASCII sprites on vanilla's 2x2 pixel grid.

Everything is authored on a half-resolution cell grid and rendered doubled, so every
true pixel pair is identical, like vanilla. Colours come from per-sprite palettes; the
`mess` pass then breaks up the clean bands (boundary dithering plus small clumps) so
shading reads like hand-placed vanilla pixels instead of a smooth bevel.
"""
import os
import random
import sys
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
SCRATCH = '/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad'
REFS = SCRATCH + '/vanilla-refs'
OUT = SCRATCH + '/art3'
PNG = os.path.join(HERE, 'png')
os.makedirs(PNG, exist_ok=True)
os.makedirs(OUT, exist_ok=True)


def rgb(r, g, b):
    return (r, g, b, 255)


class G:
    """Half-res cell grid of palette characters."""

    def __init__(s, w, h, pal):
        s.w, s.h, s.pal = w, h, dict(pal)
        s.c = [['.'] * w for _ in range(h)]

    def put(s, x, y, ch):
        if 0 <= x < s.w and 0 <= y < s.h:
            s.c[y][x] = ch

    def get(s, x, y):
        if 0 <= x < s.w and 0 <= y < s.h:
            return s.c[y][x]
        return '.'

    def art(s, rows, x0=0, y0=0, solid=False):
        """Stamp ASCII rows. '.' is transparent (skipped), ' ' is always skipped."""
        for j, row in enumerate(rows):
            for i, ch in enumerate(row):
                if ch == ' ' or (ch == '.' and not solid):
                    continue
                s.put(x0 + i, y0 + j, ch)

    def rect(s, x0, y0, x1, y1, ch):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                s.put(x, y, ch)

    def hline(s, x0, x1, y, ch):
        for x in range(x0, x1 + 1):
            s.put(x, y, ch)

    def vline(s, x, y0, y1, ch):
        for y in range(y0, y1 + 1):
            s.put(x, y, ch)

    def render(s, scale=2):
        im = Image.new('RGBA', (s.w * scale, s.h * scale), (0, 0, 0, 0))
        px = im.load()
        for y in range(s.h):
            for x in range(s.w):
                ch = s.c[y][x]
                if ch == '.':
                    continue
                col = s.pal[ch]
                for dy in range(scale):
                    for dx in range(scale):
                        px[x * scale + dx, y * scale + dy] = col
        return im


def check(rows, w, name=''):
    for i, r in enumerate(rows):
        assert len(r) == w, '%s row %d len %d != %d: %r' % (name, i, len(r), w, r)


def mess(g, groups, seed=1, edge=0.34, clump=0.07, tl=0.62):
    """Break up banding. groups: strings of palette chars ordered dark -> light.
    edge  : chance a boundary cell adopts its neighbour's step (dithering-ish noise)
    clump : density of 2-cell blobs shifted one step (lighter if top-left biased)."""
    rnd = random.Random(seed)
    where = {}
    for gi, grp in enumerate(groups):
        for i, ch in enumerate(grp):
            where[ch] = (gi, i)
    snap = [row[:] for row in g.c]
    for y in range(g.h):
        for x in range(g.w):
            ch = snap[y][x]
            if ch not in where:
                continue
            gi, i = where[ch]
            nb = []
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if 0 <= x + dx < g.w and 0 <= y + dy < g.h:
                    n = snap[y + dy][x + dx]
                    if n in where and where[n][0] == gi and where[n][1] != i:
                        nb.append(n)
            if nb and rnd.random() < edge:
                g.c[y][x] = rnd.choice(nb)
    cells = [(x, y) for y in range(g.h) for x in range(g.w) if g.c[y][x] in where]
    rnd.shuffle(cells)
    n = int(len(cells) * clump)
    for (x, y) in cells[:n]:
        ch = g.c[y][x]
        gi, i = where[ch]
        grp = groups[gi]
        # interior ramp steps only: never touch the darkest (outline) step
        if i == 0:
            continue
        d = 1 if rnd.random() < tl else -1
        j = i + d
        if j < 1 or j >= len(grp):
            continue
        for (xx, yy) in ((x, y), (x + rnd.choice((1, 0)), y + (0 if rnd.random() < .5 else 1))):
            c2 = g.get(xx, yy)
            if c2 in where and where[c2][0] == gi and where[c2][1] > 0:
                k = max(1, min(len(grp) - 1, where[c2][1] + d))
                g.put(xx, yy, grp[k])


def auto_outline(g, groups, only_first_cell=False):
    """Fill transparent cells touching a sprite cell with that neighbour group's darkest char."""
    dark = {}
    for grp in groups:
        for ch in grp:
            dark[ch] = grp[0]
    snap = [row[:] for row in g.c]
    for y in range(g.h):
        for x in range(g.w):
            if snap[y][x] != '.':
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                xx, yy = x + dx, y + dy
                if 0 <= xx < g.w and 0 <= yy < g.h and snap[yy][xx] in dark:
                    g.c[y][x] = dark[snap[yy][xx]]
                    break


def font(sz):
    for p in ('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',
              '/usr/share/fonts/dejavu/DejaVuSans.ttf'):
        if os.path.exists(p):
            return ImageFont.truetype(p, sz)
    return ImageFont.load_default()


BG = (58, 66, 120)


def ref(name):
    return Image.open(REFS + '/' + name + '.png').convert('RGBA')


def compare(path, title, new, refs, scale=8, tile=None):
    """new: list of (label, Image); refs: list of vanilla names. Same scale, side by side."""
    items = [(l, i, False) for l, i in new] + [('vanilla: ' + r.replace('%27', "'"), ref(r), True) for r in refs]
    big = [(l, i.resize((i.width * scale, i.height * scale), Image.NEAREST), v) for l, i, v in items]
    pad = 14
    cw = max(i.width for _, i, _ in big)
    ch = max(i.height for _, i, _ in big)
    # wrap at ~1700px
    cols = max(1, min(len(big), 1700 // (cw + pad)))
    rows = (len(big) + cols - 1) // cols
    f, ft = font(15), font(22)
    W = cols * (cw + pad) + pad
    H = 44 + rows * (ch + pad + 26) + pad
    out = Image.new('RGB', (W, H), (30, 33, 60))
    d = ImageDraw.Draw(out)
    d.text((pad, 8), title, fill=(240, 240, 255), font=ft)
    for k, (l, i, v) in enumerate(big):
        r, c = divmod(k, cols)
        x = pad + c * (cw + pad)
        y = 44 + pad + r * (ch + pad + 26)
        d.rectangle([x, y, x + cw - 1, y + ch - 1], fill=(70, 78, 134) if not v else (58, 66, 120),
                    outline=(255, 210, 90) if not v else (110, 120, 190))
        out.paste(i, (x + (cw - i.width) // 2, y + (ch - i.height) // 2), i)
        d.text((x + 2, y + ch + 4), l, fill=(255, 235, 170) if not v else (225, 228, 250), font=f)
    out.save(path)


def save(im, name):
    im.save(os.path.join(PNG, name + '.png'))
    return im

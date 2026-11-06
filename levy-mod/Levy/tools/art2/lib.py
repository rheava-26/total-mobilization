# Tiny pixel-art toolkit for the Levy 2.0 concept art. No anti-aliasing anywhere.
from PIL import Image, ImageDraw, ImageFont
import os


def shade(c, f):
    return tuple(max(0, min(255, int(v * f))) for v in c[:3]) + (255,)


def mix(a, b, t):
    return tuple(int(a[i] * (1 - t) + b[i] * t) for i in range(3)) + (255,)


def rgb(r, g, b):
    return (r, g, b, 255)


class Sprite:
    def __init__(s, w, h):
        s.w, s.h = w, h
        s.im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        s.d = ImageDraw.Draw(s.im)

    def px(s, x, y, c):
        if 0 <= x < s.w and 0 <= y < s.h:
            s.im.putpixel((x, y), c)

    def get(s, x, y):
        if 0 <= x < s.w and 0 <= y < s.h:
            return s.im.getpixel((x, y))
        return (0, 0, 0, 0)

    def rect(s, x0, y0, x1, y1, c):
        s.d.rectangle([x0, y0, x1, y1], fill=c)

    def line(s, x0, y0, x1, y1, c, w=1):
        s.d.line([x0, y0, x1, y1], fill=c, width=w)

    def ell(s, x0, y0, x1, y1, c):
        s.d.ellipse([x0, y0, x1, y1], fill=c)

    def poly(s, pts, c):
        s.d.polygon(pts, fill=c)

    def circle(s, cx, cy, r, c):
        for y in range(cy - r, cy + r + 1):
            for x in range(cx - r, cx + r + 1):
                if (x - cx) ** 2 + (y - cy) ** 2 <= r * r + r * 0.6:
                    s.px(x, y, c)

    def clear(s, x, y):
        s.px(x, y, (0, 0, 0, 0))

    def paste(s, o, x, y):
        s.im.alpha_composite(o.im, (x, y))


def bevel(sp, hi=1.32, lo=0.74):
    src = sp.im.copy()
    P = src.load()

    def op(x, y):
        return 0 <= x < sp.w and 0 <= y < sp.h and P[x, y][3] > 0
    for y in range(sp.h):
        for x in range(sp.w):
            c = P[x, y]
            if c[3] == 0:
                continue
            if not op(x, y - 1) or not op(x - 1, y):
                sp.im.putpixel((x, y), shade(c, hi))
            elif not op(x, y + 1) or not op(x + 1, y):
                sp.im.putpixel((x, y), shade(c, lo))


def outline(sp, f=0.28, tint=None):
    """1px outline in a deep tint of the neighbouring colour (not black)."""
    src = sp.im.copy()
    P = src.load()
    for y in range(sp.h):
        for x in range(sp.w):
            if P[x, y][3] > 0:
                continue
            ns = [P[x + dx, y + dy] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))
                  if 0 <= x + dx < sp.w and 0 <= y + dy < sp.h and P[x + dx, y + dy][3] > 0]
            if not ns:
                continue
            n = len(ns)
            c = tuple(sum(q[i] for q in ns) // n for i in range(3))
            c = tuple(int(v * f) for v in c)
            if tint:
                c = tuple(min(255, int(c[i] * 0.75 + tint[i] * 0.25)) for i in range(3))
            sp.im.putpixel((x, y), c + (255,))


def finish(sp, do_bevel=True, f=0.28, tint=None):
    if do_bevel:
        bevel(sp)
    outline(sp, f, tint)
    return sp


def font(sz):
    for p in ('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',
              '/usr/share/fonts/dejavu/DejaVuSans.ttf'):
        if os.path.exists(p):
            return ImageFont.truetype(p, sz)
    try:
        return ImageFont.load_default(sz)
    except Exception:
        return ImageFont.load_default()


BG = (58, 66, 120)


def sheet(entries, scale=8, title=None, cols=None, pad=14, cell=None, bg=BG):
    """entries: list of (label, PIL.Image RGBA). Returns an RGB contact sheet."""
    big = [(l, i.resize((i.width * scale, i.height * scale), Image.NEAREST)) for l, i in entries]
    cw = cell[0] if cell else max(i.width for _, i in big)
    ch = cell[1] if cell else max(i.height for _, i in big)
    cols = cols or len(big)
    rows = (len(big) + cols - 1) // cols
    f = font(15)
    ft = font(22)
    th = 40 if title else 0
    W = cols * (cw + pad) + pad
    H = th + rows * (ch + pad + 26) + pad
    out = Image.new('RGB', (W, H), (30, 33, 60))
    d = ImageDraw.Draw(out)
    if title:
        d.text((pad, 8), title, fill=(240, 240, 255), font=ft)
    for k, (l, i) in enumerate(big):
        r, c = divmod(k, cols)
        x = pad + c * (cw + pad)
        y = th + pad + r * (ch + pad + 26)
        d.rectangle([x, y, x + cw - 1, y + ch - 1], fill=bg, outline=(110, 120, 190))
        out.paste(i, (x + (cw - i.width) // 2, y + (ch - i.height) // 2), i)
        d.text((x + 2, y + ch + 4), l, fill=(225, 228, 250), font=f)
    return out

"""view.py out.png scale file1 file2 ... : montage of sprites on a neutral backdrop (for eyeballing)."""
import sys
from PIL import Image, ImageDraw
sys.path.insert(0, __file__.rsplit('/', 1)[0])
from palette import load, REFS


def get(p):
    import os
    if os.path.exists(p):
        return Image.open(p).convert('RGBA')
    return load(p)


def main():
    out, sc = sys.argv[1], int(sys.argv[2])
    ims = [get(p) for p in sys.argv[3:]]
    pad = 8
    W = sum(i.width * sc + pad for i in ims) + pad
    H = max(i.height * sc for i in ims) + 2 * pad
    if W > 2400:  # wrap
        rows, cur, w = [], [], pad
        for i in ims:
            if w + i.width * sc + pad > 2400 and cur:
                rows.append(cur); cur = []; w = pad
            cur.append(i); w += i.width * sc + pad
        rows.append(cur)
    else:
        rows = [ims]
    Hs = [max(i.height * sc for i in r) + pad for r in rows]
    sheet = Image.new('RGBA', (min(W, 2400), sum(Hs) + pad), (66, 72, 84, 255))
    y = pad
    for r, h in zip(rows, Hs):
        x = pad
        for i in r:
            sheet.alpha_composite(i.resize((i.width * sc, i.height * sc), Image.NEAREST), (x, y))
            x += i.width * sc + pad
        y += h
    sheet.save(out)


main()

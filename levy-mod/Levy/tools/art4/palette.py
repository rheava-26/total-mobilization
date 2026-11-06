"""Palette extraction helper for the art4 pass.

usage: python3 -I palette.py <png> [<png> ...] [-n 12]
Prints the opaque colours of each reference sprite sorted dark -> light with their pixel share,
and groups them into an outline tone (darkest, high share on the border), a shading ramp
(by luminance) and a highlight (lightest). Vanilla refs live OUTSIDE the repo (scratchpad) and
are never committed.
"""
import sys
from collections import Counter
from PIL import Image

REFS = '/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/vanilla-refs'


def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def load(path):
    if '/' not in path:
        path = REFS + '/' + path
    if not path.endswith('.png'):
        path += '.png'
    return Image.open(path).convert('RGBA')


def colours(path, n=12, merge=10):
    """Most common opaque colours, merging near-identical ones (JPEG-ish noise / AA)."""
    im = load(path)
    cnt = Counter()
    for y in range(im.height):
        for x in range(im.width):
            p = im.getpixel((x, y))
            if p[3] > 200:
                cnt[p[:3]] += 1
    out = []
    for c, k in cnt.most_common():
        for o in out:
            if sum(abs(c[i] - o[0][i]) for i in range(3)) <= merge:
                o[1] += k
                break
        else:
            out.append([c, k])
    out.sort(key=lambda t: -t[1])
    out = out[:n]
    tot = sum(k for _, k in out) or 1
    out.sort(key=lambda t: lum(t[0]))
    return [(c, k / tot) for c, k in out]


def hexs(c):
    return '#%02x%02x%02x' % tuple(c)


def ramp(path, n=6):
    """(outline, [ramp dark->light], highlight) for a sprite."""
    cs = colours(path, n + 2)
    cols = [c for c, _ in cs]
    return cols[0], cols[1:-1], cols[-1]


if __name__ == '__main__':
    args = sys.argv[1:]
    n = 12
    if '-n' in args:
        i = args.index('-n')
        n = int(args[i + 1])
        del args[i:i + 2]
    for a in args:
        print('==', a)
        for c, s in colours(a, n):
            print('  %s  %3d,%3d,%3d  %4.1f%%' % (hexs(c), c[0], c[1], c[2], s * 100))

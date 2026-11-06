"""Style study: which vanilla refs sit on a 2x2 grid, and how many ramp steps / outline tones they use.

For every PNG in the refs folder: size, whether 2x2 blocks (at offset 0,0 or 1,1) are uniform
(grid score 0..1 = share of fully-opaque 2x2 blocks that are a single colour), count of distinct
colours, and share of silhouette-edge cells that are the darkest outline tone.
Prints a table sorted by grid score, then summary medians. Also writes the contact sheet used for
eyeballing curve / falloff / rim-light habits.
"""
import glob
import os
import statistics
import sys
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from palette import REFS


def grid_score(im):
    best = 0
    for oy in (0, 1):
        for ox in (0, 1):
            ok = tot = 0
            for y in range(oy, im.height - 1, 2):
                for x in range(ox, im.width - 1, 2):
                    b = [im.getpixel((x + dx, y + dy)) for dy in (0, 1) for dx in (0, 1)]
                    if all(p[3] > 200 for p in b):
                        tot += 1
                        ok += all(p == b[0] for p in b)
            if tot:
                best = max(best, ok / tot)
    return best


def main():
    rows = []
    for f in sorted(glob.glob(REFS + '/*.png')):
        n = os.path.basename(f)[:-4]
        if 'placed' in n or 'old' in n:
            continue
        try:
            im = Image.open(f).convert('RGBA')
        except Exception:
            continue
        cols = set(im.getpixel((x, y))[:3] for y in range(im.height) for x in range(im.width) if im.getpixel((x, y))[3] > 200)
        rows.append((grid_score(im), n, im.size, len(cols)))
    rows.sort()
    for g, n, s, c in rows:
        print('%.2f %-34s %-9s %2d colours' % (g, n, s, c))
    gs = [r[0] for r in rows]
    print('n=%d median grid score %.2f ; share >=0.8: %.0f%%' % (len(rows), statistics.median(gs), 100 * sum(g >= .8 for g in gs) / len(gs)))
    print('median distinct colours %d' % statistics.median(r[3] for r in rows))


main()

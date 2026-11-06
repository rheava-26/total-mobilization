"""Eve-approved potions (art2 commit 13e4b6f). Seeker v2 is exported untouched; the Spectator
(spectator_v1) keeps its silhouette, neck and palette but its glints/dots are shuffled so it is
not a carbon copy of the Builder Potion. The art2 PNGs are 24x30 (one empty 2px column each side)."""
import os
import random
import subprocess
from lib3 import *

REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..'))
SRC = 'levy-mod/Levy/tools/art2/png/'


def fetch(name):
    data = subprocess.check_output(['git', '-C', REPO, 'show', 'origin/art2:' + SRC + name])
    p = os.path.join(SCRATCH, 'src_' + name)
    open(p, 'wb').write(data)
    return Image.open(p).convert('RGBA')


def lum(c):
    return (c[0] * 3 + c[1] * 6 + c[2]) / 10


def shuffled_spectator(im):
    im = im.copy()
    P = im.load()
    cell = lambda c, r: P[c * 2, r * 2]
    setc = lambda c, r, col: [P.__setitem__((c * 2 + dx, r * 2 + dy), col) for dx in (0, 1) for dy in (0, 1)]
    rnd = random.Random(21)
    inner = [(c, r) for r in range(8, 14) for c in range(3, 10)]   # leave the left highlight stripe alone
    glints = [p for p in inner if lum(cell(*p)) > 236]
    mids = [p for p in inner if 150 < lum(cell(*p)) < 215]
    for g in glints:
        far = [m for m in mids if abs(m[0] - g[0]) + abs(m[1] - g[1]) >= 3]
        m = rnd.choice(far)
        a, b = cell(*g), cell(*m)
        setc(g[0], g[1], b)
        setc(m[0], m[1], a)
        mids.remove(m)
    # two fresh pale dots near the lower right, one near the top, like loose bubbles
    for (c, r, k) in ((8, 12, 236), (5, 9, 248)):
        setc(c, r, (k, k + 2 if k < 253 else 255, 255, 255))
    return im


def main():
    seeker = fetch('seeker_v2.png')
    spec = shuffled_spectator(fetch('spectator_v1.png'))
    save(seeker, 'seeker_potion')
    save(spec, 'spectator_potion')
    return spec, seeker


if __name__ == '__main__':
    spec, seeker = main()
    compare(OUT + '/cmp_potions.png', 'Approved potions (Spectator shuffled, Seeker v2) vs vanilla (8x)',
            [('Spectator', spec), ('Seeker', seeker)],
            ['Builder_Potion', 'Magic_Power_Potion', 'Obsidian_Skin_Potion', 'Invisibility_Potion'])

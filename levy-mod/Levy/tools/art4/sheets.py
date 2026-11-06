"""Comparison sheets (OLD | NEW | vanilla refs at 8x), overview.png, and the in-world context mock.

Old sprites come from the base commit (git) or from the art3 concept PNGs; vanilla refs come from the scratchpad
(never committed). Output: scratchpad/art4/cmp_*.png, overview.png, mock_world.png.
"""
import io
import os
import subprocess
import sys
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from palette import load
from lib4 import PNG, OUT, SCRATCH, MOD

REPO = os.path.abspath(os.path.join(MOD, '..', '..'))
BASE = 'origin/worktree-levy-stardust'
A3 = SCRATCH + '/art3png/'
BG = (66, 72, 84, 255)


def old_git(path):
    p = subprocess.run(['git', '-C', REPO, 'show', '%s:levy-mod/Levy/%s' % (BASE, path)], capture_output=True)
    return Image.open(io.BytesIO(p.stdout)).convert('RGBA') if p.returncode == 0 else None


def ungut(im, tw, th):
    out = Image.new('RGBA', (tw * 16, th * 16), (0, 0, 0, 0))
    for j in range(th):
        for i in range(tw):
            out.paste(im.crop((i * 18, j * 18, i * 18 + 16, j * 18 + 16)), (i * 16, j * 16))
    return out


def new(name):
    return Image.open(os.path.join(PNG, name + '.png')).convert('RGBA')


def a3(name):
    return Image.open(A3 + name + '.png').convert('RGBA')


def vref(n):
    return load(n)


# key: (title, old, new, refs, tile-grid or None)
def specs():
    T = 'Content/Tiles/'
    I = 'Content/Items/'
    B = I + 'Books/'
    S = []
    S.append(('BarracksTile', old_git(T + 'BarracksTile.png'), new('BarracksTile'), ['Bed_(placed)', 'Hay_(placed)', 'Wooden_Door_(placed)', 'Red_Banner_(placed)'], (3, 3)))
    S.append(('BarracksItem', old_git(I + 'Placeables/BarracksItem.png'), new('BarracksItem'), ['Tiki_Torch', 'Bed', 'Spear_Rack'], None))
    S.append(('ArmoryTile', old_git(T + 'ArmoryTile.png'), new('ArmoryTile'), ['Weapon_Rack_(placed)', 'Mannequin_(placed)', 'Spear_Rack_(placed)', 'Bookcase_(placed)'], (2, 3)))
    S.append(('ArmoryItem', old_git(I + 'Placeables/ArmoryItem.png'), new('ArmoryItem'), ['Weapon_Rack', 'Mannequin', 'Bookcase', 'Spear_Rack'], None))
    S.append(('LogisticalCenterTile', old_git(T + 'LogisticalCenterTile.png'), new('LogisticalCenterTile'), ['Barrel', 'Hay', 'Spear_Rack_(placed)', 'Weapon_Rack_(placed)'], (3, 2)))
    S.append(('LogisticalCenterItem', old_git(I + 'Placeables/LogisticalCenterItem.png'), new('LogisticalCenterItem'), ['Barrel', 'Keg', 'Hay'], None))
    S.append(('PatrolBannerTile', old_git(T + 'PatrolBannerTile.png'), new('PatrolBannerTile'), ['Red_Banner_(placed)', 'Blue_Banner_(placed)', 'Green_Banner_(placed)', 'Yellow_Banner_(placed)'], (1, 3)))
    S.append(('PatrolBannerItem', old_git(I + 'Placeables/PatrolBannerItem.png'), new('PatrolBannerItem'), ['Red_Banner', 'Green_Banner', 'Blue_Banner'], None))
    refs = {'GuideToPlunder': 'Water_Bolt', 'GuideToFortification': 'Lunar_Flare', 'GuideToTheArcaneArts': 'Cursed_Flames',
            'GuideToExpertise': 'Crystal_Storm', 'GuideToDrill': 'Book_of_Skulls', 'GuideToEngineering': 'Spell_Tome',
            'GuideToDiscipline': 'Demon_Scythe', 'GuideToFirepower': 'Golden_Shower', 'GuideToHealthcare': 'Razorblade_Typhoon',
            'GuideToConscription': 'Spell_Tome'}
    for k, r in refs.items():
        S.append((k, old_git(B + k + '.png'), new(k), [r, 'Spell_Tome', 'Book_of_Skulls'], None))
    S.append(('FieldManual', old_git(I + 'FieldManual.png'), new('FieldManual'), ['Spell_Tome', 'Book_of_Skulls', 'Tome_of_Infinite_Wisdom'], None))
    S.append(('GuideToAutonomy', a3('guide_to_autonomy'), new('GuideToAutonomy'), ['Spell_Tome', 'Slime_Staff', 'Gel'], None))
    S.append(('WarHorn', old_git(I + 'WarHorn.png'), new('WarHorn'), ['Dog_Whistle', 'Bell', 'Golden_Shower'], None))
    S.append(('CommandRadio', old_git(I + 'CommandRadio.png'), new('CommandRadio'), ['Weather_Radio', 'Cell_Phone', 'Radar'], None))
    S.append(('MovieCameraItem', a3('static_camera_item'), new('MovieCameraItem'), ['Film_Projector', 'Radio_Thing', 'Binoculars'], None))
    S.append(('MovieCameraTile', a3('static_camera_tile'), new('MovieCameraTile'), ['Film_Projector_(placed)', 'Radio_Thing_(placed)'], None))
    S.append(('LevyRod', a3('sentry_rod'), new('LevyRod'), ['Ballista_Rod', 'Flameburst_Rod', 'Explosive_Trap_Rod'], None))
    S.append(('LevyCane', a3('sentry_cane'), new('LevyCane'), ['Ballista_Cane', 'Flameburst_Cane', 'Explosive_Trap_Cane'], None))
    S.append(('LevyStaff', a3('sentry_staff'), new('LevyStaff'), ['Ballista_Staff', 'Flameburst_Staff', 'Explosive_Trap_Staff'], None))
    S.append(('GoPro', a3('gopro_a_cam'), new('GoPro'), ['Cell_Phone', 'Radar', 'Binoculars'], None))
    S.append(('GoProIcon', a3('gopro_carry_icon'), new('GoProIcon'), ['Cell_Phone', 'Radar'], None))
    return S


def sheet(title, old, nw, refs, grid, sc=8):
    if grid and old is not None:
        old = ungut(old, *grid)
    if grid:
        nw = ungut(nw, *grid)
    cells = [('OLD', old), ('NEW', nw)] + [(r, vref(r)) for r in refs]
    h = max(i.height for _, i in cells if i is not None)
    sc = min(sc, 3000 // max(1, sum(i.width for _, i in cells if i is not None) + 4 * len(cells)))
    pad = 14
    W = sum((i.width if i is not None else 8) * sc + pad for _, i in cells) + pad
    H = h * sc + 2 * pad + 14
    s = Image.new('RGBA', (W, H), BG)
    d = ImageDraw.Draw(s)
    x = pad
    for lab, im in cells:
        if im is not None:
            s.alpha_composite(im.resize((im.width * sc, im.height * sc), Image.NEAREST), (x, pad + 14))
        d.text((x, 4), lab[:28], fill=(235, 235, 235, 255))
        x += (im.width if im is not None else 8) * sc + pad
    d.text((W - 200, H - 14), title, fill=(180, 180, 190, 255))
    s.save('%s/cmp_%s.png' % (OUT, title))
    return nw if grid is None else nw


def overview(S):
    cols, sc = 6, 3
    cells = []
    for t, old, nw, refs, grid in S:
        o = ungut(old, *grid) if (grid and old is not None) else old
        n = ungut(nw, *grid) if grid else nw
        cells.append((t, o, n))
    cw, ch = 2 * 100 + 30, 112 + 20
    rows = (len(cells) + 2) // 3
    cw = 3 * (2 * 40 * 3 // 1)
    W, Hh = 3 * 300, rows * 190 + 10
    s = Image.new('RGBA', (W, Hh), BG)
    d = ImageDraw.Draw(s)
    for i, (t, o, n) in enumerate(cells):
        x0, y0 = (i % 3) * 300 + 6, (i // 3) * 190 + 6
        for k, im in enumerate((o, n)):
            if im is None:
                continue
            sc2 = max(1, min(3, 130 // max(im.width, im.height)))
            sc2 = max(1, min(sc2, 6))
            s.alpha_composite(im.resize((im.width * sc2, im.height * sc2), Image.NEAREST), (x0 + k * 145, y0 + 16))
        d.text((x0, y0), '%s   (old | new)' % t, fill=(235, 235, 235, 255))
    s.save(OUT + '/overview.png')


def crop(name, box):
    return vref(name).crop(box)


def mock():
    T = 16
    W, H = 38, 13
    sc = 3
    w = Image.new('RGBA', (W * T, H * T), (132, 170, 248, 255))
    dirt = crop('Dirt_Block_(placed)', (16, 16, 32, 32))
    grass = crop('Grass_(top)', (16, 0, 32, 16))
    wall = crop('Wood_Wall_(placed)', (16, 16, 32, 32))
    dwall = crop('Dirt_Wall_(placed)', (16, 16, 32, 32))
    for x in range(W):
        w.alpha_composite(grass, (x * T, 10 * T))
        for y in (11, 12):
            w.alpha_composite(dirt, (x * T, y * T))
    # underground back wall strip
    # house: wood wall interior, wood frame from a darkened wall crop
    frame = wall.point(lambda v: min(255, int(v * 1.0)))
    for x in range(14, 36):
        for y in range(4, 10):
            w.alpha_composite(wall, (x * T, y * T))
    fr = Image.new('RGBA', (T, T), (0, 0, 0, 0))
    for x in range(13, 37):
        w.alpha_composite(dirt.point(lambda v: int(v * 0.8)) if False else dirt, (x * T, 3 * T))   # roof line (dirt block stand-in)
    for y in range(3, 10):
        w.alpha_composite(dirt, (13 * T, y * T))
        w.alpha_composite(dirt, (36 * T, y * T))

    def put(im, tx, ty, wt, ht):
        """bottom-left anchor in tiles; `im` may be a vanilla placed image with its own size"""
        w.alpha_composite(im, (tx * T, (ty + ht) * T - im.height))

    def put_new(name, tx, ty, tw, th, grid=True):
        im = new(name)
        if grid:
            im = ungut(im, tw, th)
        w.alpha_composite(im, (tx * T, ty * T))

    put_new('BarracksTile', 3, 7, 3, 3)
    put_new('ArmoryTile', 15, 7, 2, 3)
    put(vref('Bookcase_(placed)'), 18, 6, 3, 4)
    put_new('PatrolBannerTile', 22, 4, 1, 3)
    put(vref('Red_Banner_(placed)'), 24, 4, 1, 3)
    put(vref('Weapon_Rack_(placed)'), 25, 4, 3, 3)
    put(vref('Wooden_Table_(placed)'), 22, 8, 3, 2)
    put(vref('Wooden_Chair_(placed)'), 26, 8, 1, 2)
    put_new('LogisticalCenterTile', 28, 8, 3, 2)
    put_new('MovieCameraTilePadded', 32, 7, 2, 3)
    put(vref('Mannequin_(placed)'), 34, 7, 2, 3)
    put(vref('Wooden_Door_(placed)'), 13, 7, 2, 3) if False else None
    # a vanilla Work Bench + Tinkerer's Workshop outside next to the barracks for scale
    put(vref('Work_Bench_(placed)'), 7, 9, 2, 1)
    put(vref("Tinkerer's_Workshop_(placed)"), 9, 8, 3, 2)
    w = w.resize((W * T * sc, H * T * sc), Image.NEAREST)
    d = ImageDraw.Draw(w)
    d.text((6, 6), 'Levy tiles in a vanilla-style yard: Barracks | house: Armory, vanilla Bookcase, Patrol Banner + vanilla Red Banner, Weapon Rack, Table, Logistical Center, camera, Mannequin',
           fill=(20, 20, 30, 255))
    w.save(OUT + '/mock_world.png')


def main():
    S = specs()
    for t, old, nw, refs, grid in S:
        sheet(t, old, nw, refs, grid)
    overview(S)
    mock()


if __name__ == '__main__':
    main()

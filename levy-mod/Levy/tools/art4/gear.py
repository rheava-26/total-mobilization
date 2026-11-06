"""Hand-held / worn items: Command Radio, War Horn, GoPro (+ carried icon), movie camera item, sentry staves.

2x2 half-res grid. Palettes (all hexes copied from vanilla refs, see palette.py):
  Command Radio : art2 variant-B olive body (Eve-approved) + Cell Phone / Weather Radio steel greys (#2a2f38 #454d5c #616b7e
                  #7e8aa0 #b2b6ba), Radar screen green (#1d5e13 / art2 #78c86e), Red Banner red #e13434.
  War Horn      : Book of Skulls / Spell Tome ivory (#dfdddb #c7b8ad #bfb191 #a39373 #675f54 #47413a #25201b) + Golden Shower brass
                  (#feca50 #c46609) + Spell Tome gold (#cf8b2a #a8663c).
  GoPro         : Cell Phone body/lens ramp (#151c46 #27368f #4353b6 #5663ae #b5bde9, shell #2a2f38 #454d5c #616b7e #7e8aa0 #b2b6ba).
  Movie camera  : Film Projector (#1a1f2f #3c2c50 #444b5f #5b637b #80839d #969cae #c9cccb #973b5c #f6976f) + Weapon Rack wood
                  (#423022 #563e2c #634732 #78553c #896245 #976b4b) + brass (#feca50 #cf8b2a #a8663c).
  Sentry staves : Slime Staff shaft browns (#291d14 #402e21 #75523a #a67a5a #cb9c73) + Ballista gold ferrule (#cf8b2a #feca50)
                  + Red Banner roof (#791834 #8a1e3d #c82d36 #e13434) + window (#f4cf40 #f9e18b).
"""
from lib4 import G, check, save

# ------------------------------------------------------------------ Command Radio 28x26 (14x13)
RADIO_PAL = dict(O='#12140b', D='#2d351d', o='#3e4828', b='#586438', l='#748449', L='#a1b364',
                 k='#2a2f38', K='#454d5c', w='#616b7e', S='#78c86e', s='#1d5e13', r='#e13434', g='#b2b6ba', G='#7e8aa0')
RADIO = [
    "..........g...",
    "..........k...",
    "..........K...",
    "....OOOOOOO...",
    "...OLLLLllbO..",
    "..wKLlDDDDboO.",
    "..KkLlbbbbboO.",
    "..KkLlDDDDboO.",
    "..wKLkkkkkboO.",
    "...OlkSSskboO.",
    "...OlrbggbboO.",
    "...OoooooDDDO.",
    "....OOOOOOOO..",
]


def radio():
    check(RADIO, 14, 'radio')
    g = G(14, 13, RADIO_PAL)
    g.art(RADIO)
    return g.render()


# ------------------------------------------------------------------ War Horn 30x26 (15x13)
HORN_PAL = dict(O='#25201b', i='#dfdddb', j='#c7b8ad', h='#bfb191', t='#a39373', u='#675f54', v='#47413a',
                B='#feca50', b='#cf8b2a', c='#a8663c', m='#1b1713', n='#3c3731')
HORN = [
    "..........OOO..",
    ".........OjhhO.",
    "........OjmmmtO",
    "........OhmnntO",
    ".......OhBbbbbO",
    "......OjhtutvO.",
    ".....OiihttuvO.",
    "....OiihhttuO..",
    "...OBbbbbcO....",
    "..OihtutvO.....",
    ".OBbtuvvO......",
    ".OcvuvO........",
    "..OOO..........",
]


def horn():
    check(HORN, 15, 'horn')
    g = G(15, 13, HORN_PAL)
    g.art(HORN)
    return g.render()


# ------------------------------------------------------------------ GoPro 32x24 (16x12) + carried icon 16x16 (8x8)
CAM_PAL = dict(O='#151c46', k='#2a2f38', K='#454d5c', w='#616b7e', W='#7e8aa0', g='#b2b6ba',
               n='#27368f', m='#4353b6', l='#5663ae', q='#b5bde9', r='#e13434', y='#cf8b2a', Y='#feca50')
GOPRO = [
    "....OOOOO.......",
    "...OWrrrkO......",
    "..OOWWwwwOOOOO..",
    ".OWggWwwwKKKKkO.",
    ".OWgOOOOOkKKKkO.",
    ".OWOnmmlOOKyKkO.",
    ".OWOnqmmnOKKKkO.",
    ".OwOnmmnnOKKKkO.",
    ".OwOOnnnOOKkkkO.",
    ".OKwwwKKKKKkkkO.",
    ".OKKKKKKkkkkkkO.",
    "..OOOOOOOOOOOO..",
]


def gopro():
    check(GOPRO, 16, 'gopro')
    g = G(16, 12, CAM_PAL)
    g.art(GOPRO)
    return g.render()


ICON = [
    ".OOOOOO.",
    "OWWKKKkO",
    "OWOOOKkO",
    "OWOnmKrO",
    "OWOnnKkO",
    "OKOOOKkO",
    "OKKKKkkO",
    ".OOOOOO.",
]


def icon():
    check(ICON, 8, 'icon')
    g = G(8, 8, CAM_PAL)
    g.art(ICON)
    return g.render()


# ------------------------------------------------------------------ movie camera item 36x30 (18x15)
FILM_PAL = dict(O='#1a1f2f', k='#3c2c50', K='#444b5f', x='#5b637b', w='#80839d', W='#969cae', g='#c9cccb', p='#973b5c', P='#f6976f',
                a='#976b4b', b='#78553c', c='#634732', d='#563e2c', e='#423022', B='#feca50', y='#cf8b2a', z='#a8663c',
                n='#27368f', m='#4353b6', q='#b5bde9')
REEL = [
    "..OOO..",
    ".OWwxO.",
    "OWwOxxO",
    "OwOPOxO",
    ".OxxxO.",
    "..OOO..",
]
MOVIE_BODY = [
    ".OaaaabbbbbbbbOOOO",
    ".OabbbbbbbbbbcOWWO",
    ".OabbbbyyybbbcOWnO",
    "BOabbbbbbbbbbcOWmO",
    "BBabbbbbbbbbbcOWqO",
    ".OcccccccccccdOOOO",
    "..Od........dO....",
    ".Od..........dO...",
    ".d............d...",
]


def movie_item():
    # normalise ragged rows by padding / trimming to width 18
    check(MOVIE_BODY, 18, 'movie')
    g = G(18, 15, FILM_PAL)
    g.art(REEL, 1, 0)
    g.art(REEL, 9, 0)
    g.art(MOVIE_BODY, 0, 6)
    return g.render()


# ------------------------------------------------------------------ sentry staves
STAFF_PAL = dict(O='#291d14', d='#402e21', h='#75523a', a='#a67a5a', A='#cb9c73', y='#cf8b2a', Y='#feca50',
                 R='#791834', r='#8a1e3d', q='#c82d36', Q='#e13434', W='#75523a', w='#a67a5a', V='#f4cf40', U='#f9e18b', t='#402e21')


def shaft(g, x0, y0, x1, y1, bands=()):
    """Diagonal shaft from bottom-left (x0,y0) to top-right (x1,y1), 45 degrees. 1 light cell + 1 dark cell per row
    (vanilla staff shafts are two tones, no outline). `bands` lists step indices that get gold wraps."""
    n = y0 - y1
    for i in range(n + 1):
        x, y = x0 + i, y0 - i
        g.put(x, y, 'h')
        g.put(x + 1, y, 'd')
        if i % 3 == 1:
            g.put(x, y, 'a')
        if i in bands:
            g.put(x, y, 'Y')
            g.put(x + 1, y, 'y')
    # tiny foot cap
    g.put(x0 - 1, y0 + 1, 'y')
    g.put(x0, y0 + 1, 'Y')


ROD_HEAD = [
    "..Q..",
    ".QqR.",
    "QqrRR",
    ".WVd.",
    ".Wwd.",
]
CANE_HEAD = [
    "...OO...",
    "..OQqO..",
    ".OQqrRO.",
    "OQqqrRRO",
    ".OWVwdO.",
    ".OYYYyO.",
    ".Od..dO.",
    ".O....O.",
]
STAFF_HEAD = [
    "...Y....",
    "..OQqO..",
    ".OQqrRO.",
    "OQqqrRRO",
    ".OVWwdO.",
    ".OUWwdO.",
    ".OYYYyO.",
    ".OWVwdO.",
    ".Od..tO.",
    ".O....O.",
]


def staff(w, head, tipdx, y0, bands):
    g = G(w, w, STAFF_PAL)
    hw, hh = len(head[0]), len(head)
    hx, hy = w - hw, 0
    tx, ty = hx + tipdx, hy + hh - 1
    n = y0 - ty
    shaft(g, tx - n, y0, tx, ty, bands)
    g.art(head, hx, hy)
    return g.render()


def rod():
    return staff(16, ROD_HEAD, 2, 14, (3,))


def cane():
    return staff(18, CANE_HEAD, 3, 16, (3, 6))


def staff3():
    return staff(20, STAFF_HEAD, 3, 18, (3, 6, 9))



# ------------------------------------------------------------------ movie camera tile 16x24 cells (32x48) on a wooden tripod
CAMT_PAL = dict(FILM_PAL, Y='#feca50', B='#cf8b2a', e='#423022')
LENS = [
    ".OYBO.",
    "OYmnBO",
    "YmqnnB",
    "BnnnnB",
    "OBnnBO",
    ".OBBO.",
]
CAM_BODY = [
    ".OOOOOOOOOOOOOO.",
    ".OaaaabbbbbbbcO.",
    ".OabbbbbbbbbbcO.",
    "BOabbbbbbbbbbcO.",
    "BBabbbbbbbbbccO.",
    ".OabbbbbbbbbccO.",
    ".ObbbbbbbbbbccO.",
    ".OcbbbbbbbbccdO.",
    ".OcccccccccccdO.",
    ".OOOOOOOOOOOOOO.",
]


def camera_tile():
    check(CAM_BODY, 16, 'cambody')
    g = G(16, 24, CAMT_PAL)
    g.art(REEL, 0, 0)
    g.art(REEL, 9, 0)
    g.art(CAM_BODY, 0, 6)
    g.art(LENS, 5, 8)
    g.art(["Y"], 3, 8); g.art(["yy"], 10, 8)   # brass fittings (hand placed)
    # tripod: hub, left / right / back legs (wood, light left)
    g.art(["OccO"], 6, 16)
    for i, (xl, xr) in enumerate([(6, 9), (5, 10), (5, 10), (4, 11), (3, 12), (3, 12), (2, 13)]):
        y = 17 + i
        g.put(xl, y, 'a'); g.put(xl + 1, y, 'd')
        g.put(xr, y, 'c'); g.put(xr + 1, y, 'e') if xr + 1 < 16 else None
    for y in range(17, 22):
        g.put(7, y, 'c'); g.put(8, y, 'd')
    g.put(1, 23, 'e'); g.put(2, 23, 'e'); g.put(13, 23, 'e'); g.put(14, 23, 'e')
    return g.render()


def camera_tile_padded():
    from PIL import Image
    im = camera_tile()
    out = Image.new('RGBA', (36, 54), (0, 0, 0, 0))
    for j in range(3):
        for i in range(2):
            out.paste(im.crop((i * 16, j * 16, i * 16 + 16, j * 16 + 16)), (i * 18, j * 18))
    return out


def main():
    save(radio(), 'CommandRadio', 'Content/Items/CommandRadio.png')
    save(horn(), 'WarHorn', 'Content/Items/WarHorn.png')
    save(gopro(), 'GoPro')
    save(icon(), 'GoProIcon')
    save(movie_item(), 'MovieCameraItem')
    save(camera_tile(), 'MovieCameraTile')
    save(camera_tile_padded(), 'MovieCameraTilePadded')
    import os
    from lib4 import MOD
    os.makedirs(os.path.join(MOD, 'Content/Items/OOA'), exist_ok=True)
    save(rod(), 'LevyRod', 'Content/Items/OOA/LevyRod.png')
    save(cane(), 'LevyCane', 'Content/Items/OOA/LevyCane.png')
    save(staff3(), 'LevyStaff', 'Content/Items/OOA/LevyStaff.png')


if __name__ == '__main__':
    main()

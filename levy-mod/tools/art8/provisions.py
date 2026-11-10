"""Provisions art pass: hand-placed ASCII on a half-resolution cell grid, rendered strict 2x2 (same method as art4).
usage: python3 -I provisions.py   -> writes the item PNGs into Content/Items/Provisions and a 4x preview sheet.
Each sprite has ONE outline colour, the darkest shade of its own ramp. Noise/shading cells are typed by hand."""
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
MOD = os.path.abspath(os.path.join(HERE, '..', '..', 'Levy'))
OUT = os.path.join(MOD, 'Content', 'Items', 'Provisions')
PREVIEW = os.path.join(HERE, 'provisions_preview_4x.png')
os.makedirs(OUT, exist_ok=True)


def hx(s):
    s = s.lstrip('#')
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


def render(rows, pal):
    w = len(rows[0])
    h = len(rows)
    im = Image.new('RGBA', (w * 2, h * 2), (0, 0, 0, 0))
    px = im.load()
    for y, row in enumerate(rows):
        assert len(row) == w, (len(row), w, y, row)
        for x, ch in enumerate(row):
            if ch == '.':
                continue
            c = hx(pal[ch])
            for dy in (0, 1):
                for dx in (0, 1):
                    px[x * 2 + dx, y * 2 + dy] = c
    return im


S = {}

# FIZZ POP: a green soda bottle with a red cap and a yellow label (20x30)
S['FizzPop'] = (dict(O='0d2b1c', d='1c6b3d', m='2fa356', l='6fdc72', h='c9ff9c', w='f2ffe0', C='d8402a', c='8f1f1a', k='ff8a63', y='f2d45c', Y='b8902a'), [
    "...OOOO...",
    "...OkCO...",
    "..OOCcOO..",
    "...OdmO...",
    "...OdlO...",
    "..OOdmlO..",
    ".OdmmlhmdO",
    ".OdlhllmdO",
    ".OYyyyyYYO",
    ".OyywyyyYO",
    ".OYYyyYYYO",
    ".OdmlwlmdO",
    ".OdmmllmdO",
    ".OddmmmddO",
    "..OOOOOOO.",
])

# FIZZ TANK: a steel soda cylinder with a brass valve and a tap
S['FizzTank'] = (dict(O='0b1d33', d='1d4f8a', m='2f86d6', l='6cc4f5', h='d3f3ff', b='c59a2e', B='7d5a14', g='f0d060', r='d8402a', R='8f1f1a', w='f2ffff'), [
    ".....OOOO......",
    "....ObgbBO.....",
    "....OBbBBO.....",
    "..OOOOBBOOOO...",
    ".OdmmlhhlmmdO..",
    ".OdmllhwllmdO..",
    "OOdmlhwllmmdOO.",
    "ObOdmlhllmmdOBO",
    "OBBdmlllmmddOBO",
    "OOOrrrrrrrrrOOO",
    ".OdRrwrrrrrRdO.",
    ".OdRRRRRRRRRdO.",
    ".OdmlhllmmmddO.",
    ".OdmlhwlmmmddO.",
    ".OdmllhlmmmddO.",
    ".OddmlllmmdddO.",
    ".OdddmmmmdddO..",
    ".OOOOOOOOOOOO..",
])

# MAGMA SHOT: a smoked-glass shot of lava
S['MagmaShot'] = (dict(O='2a0c06', g='b7a6ad', G='6e5e69', r='d9341a', R='8e1b10', o='ff7a1c', y='ffd24a', w='fff0b0'), [
    "..OOOOOOOO..",
    ".OgGgggggGgO",
    ".OgroyyoorGO",
    ".OGroyyoorGO",
    ".OgRrooorRGO",
    "..OGrroorGO.",
    "..OgRrrrRGO.",
    "..OGRrorRGO.",
    "..OGgRRRgGO.",
    "..OGGGGGGGO.",
    "...OOOOOOO..",
])

# MAGMA CRUCIBLE: an iron pot full of lava with two handles
S['MagmaCrucible'] = (dict(O='1a0f10', i='4a4146', I='7a6d73', j='b2a5a8', r='d9341a', R='8e1b10', o='ff7a1c', y='ffd24a', w='fff0b0'), [
    "...OOOOOOOOOOOOO...",
    "..OjjIIIIIIIIIIIO..",
    ".OIOOooyyyyooOOIiO.",
    "OIIOoyyywwyyyooOiiO",
    "OjIOrooyyyyooroOiiO",
    ".OIOrRrooooorRrOiO.",
    "..OIOOrRRRRROOiiO..",
    "..OjIIIIIIIIIIiiO..",
    "..OjIIiIIIIIiIiiO..",
    "..OIIIIIIIIIIIiiO..",
    "..OIIiIIIIIIIiiiO..",
    "...OiiiIIIIIiiiO...",
    "...OOiiiiiiiiiOO...",
    "..OIIOOOOOOOOOIIO..",
    "..OiiO.......OiiO..",
    "..OOOO.......OOOO..",
])

# FISH FRY PLATTER: a white plate with two fried fish, a lemon wedge and parsley
def stamp(base, pat, x0, y0):
    base = [list(r) for r in base]
    for j, row in enumerate(pat):
        for i, ch in enumerate(row):
            if ch != '.':
                base[y0 + j][x0 + i] = ch
    return [''.join(r) for r in base]

_plate = [
    "....OOOOOOOOOOOOOOOOOO....",
    "..OOqqqppppppppppppppPOO..",
    ".OqpppppppppppppppppppPPO.",
    "OqppppppppppppppppppppppPO",
    "OppppppppppppppppppppppPPO",
    "OppppppppppppppppppppppPPO",
    "OppppppppppppppppppppppPPO",
    ".OpppppppppppppppppppPPPO.",
    ".OPPpppppppppppppppPPPPO..",
    "..OOPPPPPPPPPPPPPPPPOO....",
    "....OOOOOOOOOOOOOOOO......",
]
_plate = [r.ljust(26, '.')[:26] for r in _plate]
_fish = [
    "..FFFFFFF...F",
    ".FgggggffFFFF",
    "FfeggfffffFF.",
    ".FfffffffFFFF",
    "..FFFFFFF...F",
]
_lemon = [".LLL.", "LlLlL", "LLlLL", ".LLL."]
_leaf = ["vV.", "VvV", ".vV"]
_p = stamp(_plate, _fish, 2, 2)
_p = stamp(_p, _fish, 9, 5)
_p = stamp(_p, _lemon, 19, 2)
_p = stamp(_p, _leaf, 22, 6)
S['FishFryPlatter'] = (dict(O='2b1608', p='ddd8cc', P='9a948a', q='f4f0e6', f='e0a23a', F='a8691f', g='f7d37a', e='3a2410', L='f5e050', l='b8a020', v='3f8a35', V='27591f'), _p)

# FRUIT BASKET: a wicker basket with apples, a banana, grapes and an orange
S['FruitBasket'] = (dict(O='2a1608', w='c8903f', W='8a5a22', x='5e3a14', a='d03030', A='8c1a1e', q='ff8a7a', y='f2d24a', Y='b88a1f', p='8a3cc0', P='55207f', n='c8a0e8', o='f08a28', N='b0560f', v='3f9a35', V='27591f'), [
    ".....OOOOOOOOOOOO....",
    "....OwwWOOOOOOOWWO...",
    "...OwOO...vV...OOWO..",
    "...OW..OOaaOOyy..OW..",
    "..OW..OaqaAAOyYyO.WO.",
    "..OW.OaqaaAAOoYyYO.WO",
    "..OW.OaaaAAAOoNYyO.WO",
    "..OOOOOOAAOOOnPPOOOOO",
    ".OwwwWwwWwwWwPnPwwWO.",
    ".OWxWwWWxWwWWwPWWxWO.",
    ".OwWwwWxWwwWxwwWwwWO.",
    ".OWxWwWWwWxWwWWxWwWO.",
    "..OwWxWwWWwWxWwWWwO..",
    "..OWwWWxWwWWwWxWWWO..",
    "...OWxWWWxWWWWxWWO...",
    "....OOOOOOOOOOOOO....",
])

# BURGER BOX: a red take-away box with a yellow handle and a burger on the front
S['BurgerBox'] = (dict(O='3a0c0c', r='cc3030', R='8a1f20', k='f06a5a', y='f6d24a', Y='b8901f', t='e0a85a', T='a8702a', b='6a3a1c', g='5aa83a', c='f0c030', w='fff4d0'), [
    "....OOOOOOOOOO....",
    "...OyYOOOOOOYyO...",
    "..OyOO........OyO.",
    ".OOOOOOOOOOOOOOOO.",
    ".OkrrrrrrrrrrrrRO.",
    ".OrrrrrttttrrrrRO.",
    ".OrrrrtTtwTTrrrRO.",
    ".OrrrrOgggggOrrRO.",
    ".OrrrrOcccccOrrRO.",
    ".OrrrrbbbbbbbrrRO.",
    ".OrrrrTTtTTtTrrRO.",
    ".OrrrrOOOOOOOrrRO.",
    ".OrrrrrrrrrrrrrRO.",
    ".OrrrOOrrrrOOrrRO.",
    ".OrrrrrOOOOrrrrRO.",
    ".ORRRRRRRRRRRRRRO.",
    "..OOOOOOOOOOOOOO..",
])

# GRAND FEAST: a roast bird on a silver platter with fruit and greens
_bird = [
    ".....OOOOOOOOOO.....",
    "...OOGGggggggGGOO...",
    "..OGGggbbbbbbggGGO..",
    ".OGggbbBbbbbBbbggGO.",
    ".OgbbbbbbBBbbbbbbgO.",
    "OgbbBbbbbbbbbbBbbbgO",
    "OgbbbbBBbbbbBBbbbbgO",
    ".OBbbbbbbbbbbbbbbBO.",
    "..OOBBBBBBBBBBBBOO..",
]
_leg = [
    ".OOO.",
    "OwwbO",
    "OwbbBO"[:5],
    ".ObBO",
    "..OO.",
]
_pl = [
    "OOOOOOOOOOOOOOOOOOOOOOOOOOOOOO",
    "OjjjjjjjjjjjjjjjjjjjjjjjjjjjjO",
    "OIaAIvVIyyIjjjjjjIaAIvVIyyIjIO",
    ".OIIIIIIIIIIIIIIIIIIIIIIIIIIO.",
    "..OiIIIIIIIIIIIIIIIIIIIIIIiO..",
    "...OOiiiiiiiiiiiiiiiiiiiiOO...",
    ".....OOOOOOOOOOOOOOOOOOOO.....",
]
_f = [r.ljust(30, '.')[:30] for r in ["." * 30] * 8] + [r.ljust(30, '.')[:30] for r in _pl]
_f = stamp(_f, _bird, 5, 0)
_f = stamp(_f, _leg, 1, 3)
_f = stamp(_f, [r[::-1].replace('b', 'b') for r in _leg], 24, 3)
S['GrandFeast'] = (dict(O='24100a', b='b8742a', B='7a4614', g='e8a64a', G='f4cf7c', w='f0ead8', i='6a6e78', I='9ca0aa', j='d8dce6', a='d03030', A='8c1a1e', v='3f9a35', V='27591f', y='f2d24a'), _f)


def main():
    ims = {}
    for name, (pal, rows) in S.items():
        w0 = max(len(r) for r in rows)
        bad = [i for i, r in enumerate(rows) if len(r) != w0]
        if bad:
            print('WIDTH MISMATCH', name, [(i, len(rows[i])) for i in bad], 'expected', w0)
            rows = [r.ljust(w0, '.')[:w0] for r in rows]
        ims[name] = render(rows, pal)
        ims[name].save(os.path.join(OUT, name + '.png'))
        print(name, ims[name].size)
    order = ['FizzPop', 'FizzTank', 'MagmaShot', 'MagmaCrucible', 'FishFryPlatter', 'FruitBasket', 'BurgerBox', 'GrandFeast']
    pad = 16
    scale = 4
    rows = [order[:4], order[4:]]
    rw = [sum(ims[n].width * scale + pad for n in r) + pad for r in rows]
    rh = [max(ims[n].height * scale for n in r) + pad for r in rows]
    sheet = Image.new('RGBA', (max(rw), sum(rh) + pad), (58, 66, 88, 255))
    y = pad
    for r, h in zip(rows, rh):
        x = pad
        for n in r:
            im = ims[n].resize((ims[n].width * scale, ims[n].height * scale), Image.NEAREST)
            sheet.paste(im, (x, y + (h - pad - im.height) // 2), im)
            x += im.width + pad
        y += h
    sheet.save(PREVIEW)
    print('preview', PREVIEW)


if __name__ == '__main__':
    main()

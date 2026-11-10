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

# FIZZ POP: a blue soda can with a silver top and a yellow label (20x28)
S['FizzPop'] = (dict(O='0b1d33', d='1d4f8a', m='2f86d6', l='6cc4f5', h='d3f3ff', j='e4ecf2', I='aab6c4', i='6a7686', y='f2d45c', Y='b8902a', w='f6fcff'), [
    ".OOOOOOOO.",
    "OjIjjjjIiO",
    "OOIiOOiIOO",
    "OdmmlhlmdO",
    "OdmlhllmmO",
    "OYyyywyyYO",
    "OYyywwyyYO",
    "OYyyyyywYO",
    "OdmlhllmdO",
    "OdmlllmmdO",
    "OddmlmmmdO",
    "OdddmmmddO",
    "OiIIiIIiiO",
    ".OOOOOOOO.",
])

# FIZZ TANK (Case of Fizz): a cardboard case with a row of blue cans standing in it (40x26)
S['FizzTank'] = (dict(O='2a1608', c='c8a064', k='e6c88c', C='8a6030', b='2f86d6', B='1d4f8a', w='f6fcff', j='e4ecf2', I='aab6c4', i='6a7686', d='1d4f8a', m='2f86d6', l='6cc4f5'), [
    ".OOOOO.OOOOO.OOOOO..",
    "OjIjiOOjIjiOOjIjiO..",
    "OdmldOOdmldOOdmldO..",
    "OdmlmOOdmlmOOdmlmO..",
    "OkkkkkkkkkkkkkkkkkkO",
    "OccccccccccccccccccO",
    "OcBBBBBBBBBBBBBBBBcO",
    "OcbwbwwbwwbwwbwwbbcO",
    "OcBBBBBBBBBBBBBBBBcO",
    "OcccccccccccccccccCO",
    "OcccCcccccccccCcccCO",
    "OCCCCCCCCCCCCCCCCCCO",
    "OOOOOOOOOOOOOOOOOOOO",
])

# MAGMA SHOT (Iceberg Shot): a frosted shot glass of ice-cold vodka (24x22)
S['MagmaShot'] = (dict(O='10243a', e='d8f0ff', f='9fd0ee', F='5f98c8', v='aedaf2', w='ffffff'), [
    "OOOOOOOOOOOO",
    "OwewwwwwewwO",
    "OefvvwvvvfFO",
    "OeFvwvvvvfFO",
    ".OfvvvvvvFO.",
    ".OFvvwvvvFO.",
    "..OFvvvvFO..",
    "..OFvvvvFO..",
    "..OFFFFFFO..",
    "..OFFFFFFO..",
    "...OOOOOO...",
])

# MAGMA CRUCIBLE (Iceberg Tray): a frosted bottle and two shots on a silver tray (38x30)
S['MagmaCrucible'] = (dict(O='10243a', e='d8f0ff', f='9fd0ee', F='5f98c8', v='aedaf2', w='ffffff', k='5a7a9a', c='2a3f58', j='d8dce6', I='9ca0aa', i='6a6e78'), [
    "........OOO........",
    "........OkO........",
    "........OcO........",
    ".......OfeFO.......",
    ".......OfeFO.......",
    "......OOfeFOO......",
    ".....OfvvvvvFO.....",
    ".OOO.OfvwvvvFO.OOO.",
    "OwveOOfvvvvvFOOwveO",
    "OFvFOOfvvwvvFOOFvFO",
    ".OFO.OFFFFFFFO.OFO.",
    "OjjjjjjjjjjjjjjjjjO",
    "OIIIIIIIIIIIIIIIIIO",
    ".OiiiiiiiiiiiiiiiO.",
    "..OOOOOOOOOOOOOOO..",
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

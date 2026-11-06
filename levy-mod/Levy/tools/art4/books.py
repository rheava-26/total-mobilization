"""Guide books (24x28), Field Manual (24x28) and Guide to Autonomy (30x32).

One tilted-tome silhouette shared by the whole set (vanilla spell-tome shape: diamond cover with
gold trim, cream page edge along the lower right, darkest outline). Each guide has its own cover
ramp, borrowed from a different vanilla spell tome, and a 5x5 emblem. Noise cells are typed in.
Palette sources (vanilla refs, see palette.py):
  Plunder        Water Bolt            Fortification  Lunar Flare
  Arcane Arts    Cursed Flames         Expertise      Crystal Storm
  Drill          Book of Skulls        Engineering    Cell Phone blue + Spell Tome brass
  Discipline     Demon Scythe          Firepower      Golden Shower
  Healthcare     Razorblade Typhoon    Conscription   Spell Tome
  Field Manual   Slime Staff browns / Lunar Flare khaki    Autonomy  Slime Staff browns + Gel/Slime Banner blue
Pages on every book: Spell Tome / Water Bolt page cream #dfdddb with Book of Skulls shade #c7b8ad.
"""
from lib4 import G, check, save

TEMPLATE = [
    ".....OO.....",
    "....OTTO....",
    "...OTccbTO..",
    "..OTcccbbTO.",
    ".OTccbbbbTO.",
    "OTccbbbbbbTO",
    "OTcbbbbbbbPO",
    "OTcbbbbbbbPO",
    "OTbbbbbbddPO",
    ".OTbbbbddPQO",
    "..OTbbdPQO..",
    "...OTdPQO...",
    "....OPQO....",
    ".....OO.....",
]
check(TEMPLATE, 12, 'template')

PAGES = dict(P='#dfdddb', Q='#c7b8ad', S='#564f4a')


def P(**k):
    d = dict(PAGES)
    d.update(k)
    return d


GLYPH = {
    'coin':   [".xxx.", "xyyzx", "xyzzx", "xzzzx", ".xxx."],
    'tower':  [".x.x.", ".xxx.", ".xyx.", ".xxx.", "xxxxx"],
    'star':   ["..x..", ".xyx.", "xxxxx", ".xxz.", ".x.z."],
    'target': [".xxx.", "x...x", "x.y.x", "x...z", ".xzz."],
    'chev':   ["..x..", ".x.x.", "x...x", "..y..", ".y.y."],
    'gear':   ["x.x.x", ".xxz.", "xx.zx", ".xzz.", "x.z.x"],
    'sword':  ["..x..", "..x..", "..x..", ".yyy.", "..y.."],
    'flame':  ["..x..", ".xx..", ".xyx.", "xxyyx", ".xzz."],
    'cross':  ["..x..", "..x..", "xxxxx", "..xz.", "..zz."],
    'helmet': [".xxx.", "xyxxx", "xxxxz", "zzzzz", ".z.z."],
    'compass': ["..x..", ".xyx.", "xyzyx", ".xyx.", "..x.."],
}

BOOKS = {
    # name: (palette, glyph, [(x,y,ch) hand noise])
    'GuideToPlunder': (P(O='#360404', d='#751717', b='#881d1d', c='#9b2c2c', T='#d58c38', x='#d58c38', y='#feca50', z='#a8663c'),
                       'coin', [(5, 3, 'c'), (7, 7, 'c'), (3, 8, 'd'), (8, 6, 'd')]),
    'GuideToFortification': (P(O='#172624', d='#185d42', b='#21a08d', c='#22dd97', T='#a28859', x='#d8bf8f', y='#172624', z='#a28859'),
                             'tower', [(4, 3, 'c'), (6, 8, 'd'), (3, 7, 'd'), (7, 4, 'd')]),
    'GuideToTheArcaneArts': (P(O='#1f032a', d='#5a1775', b='#691d88', c='#7b2c9b', T='#cf2aa4', x='#d538ad', y='#dfdbde', z='#300940'),
                             'star', [(3, 6, 'c'), (7, 8, 'd'), (4, 4, 'b'), (8, 7, 'd')]),
    'GuideToExpertise': (P(O='#31031f', d='#751750', b='#881d5e', c='#9b2c6f', T='#cf2a34', x='#49c3ba', y='#70f8c9', z='#44092d'),
                         'target', [(4, 4, 'b'), (3, 7, 'd'), (7, 6, 'c'), (6, 8, 'd')]),
    'GuideToDrill': (P(O='#1b1713', d='#3c3731', b='#47413a', c='#564f4a', T='#a39373', x='#dfdddb', y='#a39373', z='#25201b'),
                     'chev', [(3, 5, 'c'), (8, 8, 'd'), (5, 3, 'c'), (7, 7, 'd')]),
    'GuideToEngineering': (P(O='#151c46', d='#27368f', b='#4353b6', c='#5663ae', T='#cf8b2a', x='#cf8b2a', y='#feca50', z='#7a5c0a'),
                           'gear', [(4, 4, 'c'), (7, 8, 'd'), (3, 6, 'c'), (8, 7, 'd')]),
    'GuideToDiscipline': (P(O='#16134b', d='#3d3a7c', b='#3f3c86', c='#574e9c', T='#878787', x='#dfdbde', y='#cf8b2a', z='#878787'),
                          'sword', [(3, 5, 'c'), (7, 8, 'd'), (5, 4, 'c'), (8, 6, 'd')]),
    'GuideToFirepower': (P(O='#320405', d='#721b1d', b='#942e1f', c='#b13f1d', T='#c46609', x='#c46609', y='#feca50', z='#b13f1d'),
                         'flame', [(4, 4, 'c'), (3, 7, 'd'), (8, 6, 'd'), (6, 3, 'c')]),
    'GuideToHealthcare': (P(O='#0a4428', d='#20e090', b='#30f8ab', c='#70f8c9', T='#a1d86b', x='#dfdbde', y='#ed5c5c', z='#c7adc2'),
                          'cross', [(4, 5, 'c'), (7, 8, 'd'), (3, 7, 'd'), (8, 7, 'd')]),
    'GuideToConscription': (P(O='#2c0c03', d='#934933', b='#a8663c', c='#bb6a45', T='#cf8b2a', x='#889198', y='#bcc7cf', z='#564f4a'),
                            'helmet', [(4, 3, 'c'), (7, 7, 'd'), (3, 6, 'c'), (8, 8, 'd')]),
    'FieldManual': (P(O='#291d14', d='#493613', b='#75523a', c='#a67a5a', T='#cb9c73', x='#d8bf8f', y='#ed5c5c', z='#493613'),
                    'compass', [(4, 4, 'c'), (7, 7, 'd'), (3, 6, 'd'), (8, 6, 'c')]),
}


def build(name):
    pal, glyph, noise = BOOKS[name]
    g = G(12, 14, pal)
    g.art(TEMPLATE)
    g.art(GLYPH[glyph], 3, 4)
    for (x, y, ch) in noise:
        if g.c[y][x] in 'bcd':
            g.put(x, y, ch)
    return g.render()


def autonomy():
    pal = P(O='#291d14', d='#402e21', b='#75523a', c='#a67a5a', T='#cb9c73',
            x='#2f54bf', y='#5698ff', z='#253a84', w='#dfdddb', k='#16134b')
    g = G(15, 16, pal)
    g.art(TEMPLATE, 1, 1)
    for (x, y, ch) in [(5, 5, 'c'), (4, 8, 'c'), (9, 9, 'd'), (8, 11, 'd'), (3, 9, 'd'), (10, 7, 'c')]:
        if g.c[y][x] in 'bcd':
            g.put(x, y, ch)
    # a small blue slime sitting on the cover (6 wide), eyes dark, highlight top-left
    g.art([".zzzz.",
           "zyyxxz",
           "zykxkz",
           "zxxxxz",
           ".zzzz."], 4, 6)
    return g.render()


def main():
    for n in BOOKS:
        dest = 'Content/Items/Books/%s.png' % n if n != 'FieldManual' else 'Content/Items/FieldManual.png'
        im = build(n)
        assert im.size == (24, 28)
        save(im, n, dest)
    im = autonomy()
    assert im.size == (30, 32)
    save(im, 'GuideToAutonomy')   # not in Content yet (item does not exist in this base)


if __name__ == '__main__':
    main()

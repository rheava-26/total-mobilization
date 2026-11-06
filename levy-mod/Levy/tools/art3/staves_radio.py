"""Levy sentry staves (watchtower theme, 3 tiers) + Command Radio (walkie-talkie)."""
from gear import PAL, BROWN, GOLD, STRAP, BLK, SCREEN, RED, outline_all
from lib3 import *

SP = dict(PAL)
SP.update(BROWN)

ROD = [
    "...yy...",
    "..yZzy..",
    ".xyyyyyx",
    "..DlmD..",
    "..DEED..",
    ".CCCCCC.",
    "..C..C..",
    "..C..C..",
]
CANE = [
    "....yy....",
    "...yZzy...",
    "..yZzzzy..",
    ".xyyyyyyx.",
    "..DElmED..",
    "..DEllED..",
    "..DEDEED..",
    "..DDEDED..",
    ".CDDDDDDC.",
    "..C.DD.C..",
    "..C....C..",
    "..C....C..",
]
STAFF = [
    ".....Czzx...",
    ".....C......",
    "....yZzy....",
    "...yZzzzy...",
    ".xyyyyyyyyx.",
    "..42lmml24..",
    "..32444423..",
    "..32433423..",
    "..32344323..",
    ".jkkkkkkkkj.",
    "..C......C..",
    "..C..jj..C..",
    "..C......C..",
    "..C......C..",
]


def staff(head, size, x0, ferrule=0, seed=1, glow=(), y0=0):
    g = G(size, size, SP)
    for r in head:
        pass
    check(head, len(head[0]), 'head')
    g.art(head, x0, y0)
    hh = len(head)
    # 45 degree two-wide handle from the left leg down to the bottom-left corner
    sx, sy = x0 + 1, y0 + hh
    n = 0
    while sy < size and sx >= 0:
        g.put(sx, sy, 'D' if n % 2 == 0 else 'E')
        g.put(sx + 1, sy, 'C')
        sx -= 1
        sy += 1
        n += 1
    for k in range(ferrule):
        g.put(k, size - 1 - k, 'k')
        g.put(k + 1, size - 1 - k, 'j')
    mess(g, ['BCDEF', '?yzZY', '?12345', '?jklm'], seed=seed, edge=.33, clump=.07)
    for x, y, c in glow:
        g.put(x + x0, y, c)
    auto_outline(g, ['BCDEF', 'xyzZY', '012345', 'hjklm'])
    return g.render()


# ------------------------------------------------------------------ radio
RPAL = dict(PAL)
RPAL.update({'B': rgb(36, 42, 20), 'C': rgb(66, 78, 34), 'D': rgb(100, 116, 52), 'E': rgb(138, 156, 74), 'F': rgb(182, 200, 112)})
RADIO = [
    "FEEEED",
    "E0000D",
    "EDDDDC",
    "E0000D",
    "EDDDDC",
    "D1GggC",
    "D1gggC",
    "ERD55C",
    "CCCCBB",
]


def radio():
    g = G(14, 13, RPAL)
    g.art(RADIO, 4, 3)
    mess(g, ['BCDEF'], seed=9, edge=.3, clump=.07)
    # push-to-talk bar on the left, antenna on the top right
    g.rect(2, 5, 3, 7, '3')
    g.vline(2, 5, 7, '4')
    g.art(["R", "4", "3"], 8, 0)
    g.art(["34"], 8, 2) if False else None
    auto_outline(g, ['BCDEF', '012345', 'rRQ', 'gG'])
    return g.render()


def build():
    out = {}
    out['sentry_rod'] = staff(ROD, 16, 6, 0, 2)
    out['sentry_cane'] = staff(CANE, 18, 4, 1, 4)
    out['sentry_staff'] = staff(STAFF, 20, 3, 2, 6, y0=1)
    out['command_radio'] = radio()
    for k, v in out.items():
        save(v, k)
    return out


if __name__ == '__main__':
    o = build()
    compare(OUT + '/cmp_staves.png', 'Levy sentry staves vs DD2 staves (8x)',
            [('Levy Rod', o['sentry_rod']), ('Levy Cane', o['sentry_cane']), ('Levy Staff', o['sentry_staff'])],
            ['Flameburst_Rod', 'Flameburst_Cane', 'Flameburst_Staff', 'Lightning_Aura_Rod', 'Lightning_Aura_Cane', 'Lightning_Aura_Staff'], scale=6)
    compare(OUT + '/cmp_radio.png', 'Command Radio vs vanilla gadgets (8x)',
            [('Command Radio 28x26', o['command_radio'])],
            ['Weather_Radio', 'Cell_Phone', 'Tally_Counter', 'GPS'], scale=8)

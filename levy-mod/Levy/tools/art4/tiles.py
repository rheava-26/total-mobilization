"""Placeable tiles + their inventory items, flat and front-facing, strict 2x2 grid.

Palette sources (vanilla refs, see palette.py):
  Barracks      : Weapon Rack placed wood (#423022 #563e2c #634732 #78553c #896245 #976b4b #b2815c #ca9877), outline #291e15
                  (all vanilla wood furniture), Red Banner (#8a1e3d #ae242e #c82d36 #e13434 #f4cf40) for pennant + eave band.
  Armory        : Bookcase placed (#291e15 outline, #604438 #785840 #906850 #a87858 #b08460) + Spear Rack steel
                  (#282e39 #595e68, blade lights #889198 #bcc7cf) + Red Banner red / gold for the shield.
  Logistical    : Bookcase/Weapon Rack wood ramps, Spear Rack hoop steel, Weapon Rack tan (#b2815c #ca9877 #896245) for sacks,
                  Red Banner (#c82d36 #8a1e3d) for the stencil.
  Patrol Banner : transcribed cell-for-cell from vanilla Red Banner (placed): same edge ramp #5b142e #791834 #8a1e3d, cloth ramp
                  #ae242e #c82d36 #e13434, gold #da7b36 #f4cf40 #f9e18b, hanger #535853. Only the emblem is ours.
Vanilla habits applied: chamfered silhouettes (corner cells cut), 1-cell outline, rim highlight on the top-left, 2-cell dark
band bottom-right, curved light falloff on round things (barrel, sacks, tent shoulders).
"""
from lib4 import G, check, gutters, save

# ------------------------------------------------------------------ Barracks tent 24x24 cells -> 54x54
TENT_PAL = dict(O='#291e15', a='#ca9877', b='#b2815c', c='#896245', d='#634732', S='#563e2c', D='#291e15', E='#423022',
                h='#976b4b', p='#78553c', q='#c82d36', R='#8a1e3d', Q='#e13434', Y='#f4cf40')


def trow(pad, left, opening='', right=''):
    r = '.' * pad + 'O' + left + opening + right + 'O' + '.' * pad
    assert len(r) == 24, (len(r), r)
    return r


TENT = [
    "...........pqqqq........",
    "...........pqYqR........",
    "...........pqR..........",
    "...........p............",
    "...........OO...........",
    "..........OabO..........",
    ".........OaabbO.........",
    ".........OaabcO.........",
    "........OaabbcdO........",
    ".......OaaabbcddO.......",
    trow(6, 'aaabbbbbcd'),
    trow(6, 'aabb', 'DD', 'bccd'),
    trow(5, 'aabb', 'DEED', 'bccd'),
    trow(4, 'aabb', 'DEEEED', 'bccd'),
    trow(3, 'aabb', 'DEEEEEED', 'bccd'),
    trow(3, 'aaab', 'DEEEEEED', 'bbcd'),
    trow(2, 'aaabb', 'DEEEEEED', 'bbccd'),
    trow(1, 'QQqqqR', 'DEEEEEED', 'qqRRRR'),
    trow(1, 'aabbab', 'DEEEEEED', 'bbcbcd'),
    trow(1, 'aaabbb', 'DEEEEEED', 'bccbcd'),
    trow(1, 'abbbab', 'DEEEEEED', 'bbcccd'),
    trow(1, 'aabbbb', 'DEhqqqhD', 'bcbccd'),
    trow(1, 'bbbbcb', 'DhhhhhhD', 'ccdcdd'),
    ".OOOOOOOOOOOOOOOOOOOOOO.",
]
check(TENT, 24, 'tent')


def barracks():
    g = G(24, 24, TENT_PAL)
    g.art(TENT)
    for (x, y) in [(6, 18), (6, 19), (6, 20), (17, 18), (17, 19), (17, 20), (9, 8), (13, 9)]:
        g.put(x, y, 'S')
    # the lit cot inside the doorway: pillow + blanket (hand placed)
    g.put(10, 21, 'a')
    return g.render()


# ------------------------------------------------------------------ Armory cabinet 16x24 cells -> 36x54
ARM_PAL = dict(O='#291e15', a='#b08460', b='#a87858', c='#906850', d='#785840', g='#976b4b', E='#403028', F='#291e15',
               w='#bcc7cf', x='#889198', X='#595e68', y='#cf8b2a', R='#c82d36', r='#8a1e3d', Y='#f4cf40')
ARMORY = [
    ".OOOOOOOOOOOOOO.",
    "OaaabaaabbaabbcO",
    "ObbbbbbbbbbbbcdO",
    "ObbFFFFFFFFFFcdO",
    "ObbEwEEwEEwEEcdO",
    "ObbxwXxwXxwXEcdO",
    "ObbEwXEwXEwXEcdO",
    "ObbEyEEyEEyEEcdO",
    "ObbEgEEgEEgEEcdO",
    "ObbEgEEgEEgEEcdO",
    "ObbEgEEgEEgEEcdO",
    "OaaaaaabaaaaaabO",
    "OcccddcccdcdddcO",
    "ObbEyyyEEEEEEcdO",
    "ObbyRRrEEwxEEcdO",
    "ObbyRYrEEwxXEcdO",
    "ObbyRRrEExXXEcdO",
    "ObbEyyyEEXEEEcdO",
    "OaaaaaabaaaaaabO",
    "OcccddcccdcdddcO",
    "OaaabbbbOabbbcdO",
    "OXXXXXXXyXXXXXXO",
    "ObbbcbbcObcccddO",
    ".OOOOOOOOOOOOOO.",
]
check(ARMORY, 16, 'armory')


def armory():
    g = G(16, 24, ARM_PAL)
    g.art(ARMORY)
    return g.render()


# ------------------------------------------------------------------ Logistical Center 24x16 cells -> 54x36
LOG_PAL = dict(O='#291e15', a='#b08460', b='#a87858', c='#906850', d='#785840', H='#595e68', h='#889198',
               A='#ca9877', B='#b2815c', C='#896245', T='#634732', R='#c82d36', r='#8a1e3d')
CRATE = [
    ".OOOOOOO.",
    "OaaaaaabO",
    "OabbbbbcO",
    "OdddddddO",
    "OabRRRbcO",
    "OabbrrbcO",
    "OdddddddO",
    "OcccccccO",
    ".OOOOOOO.",
]
BARREL = [
    "..OOOO..",
    ".OaaabO.",
    "OaabbbcO",
    "OHhHHHHO",
    "OabbbbcO",
    "OabbbbcO",
    "OaabbbcO",
    "OHhHHHHO",
    ".OabbcO.",
    "..OOOO..",
]
SACK = [
    "...OTO...",
    "..OAAAO..",
    ".OAABBBO.",
    "OAABBBCCO",
    "OABBBCCCO",
    "OABBCCCCO",
    ".OCCCCCO.",
    "..OOOOO..",
]
SACK_S = [
    "..OTO..",
    ".OAABO.",
    "OAABBCO",
    "OABBCCO",
    ".OCCCO.",
    "..OOO..",
]


def logistical():
    g = G(24, 16, LOG_PAL)
    g.art(CRATE, 0, 0)         # stacked crates, left
    g.art(CRATE, 0, 8)
    g.art(BARREL, 9, 6)
    g.art(SACK, 16, 8)
    g.art(SACK_S, 18, 2)
    for (x, y) in [(3, 2), (6, 10), (11, 7), (13, 12), (19, 11), (21, 13)]:
        g.put(x, y, 'T' if g.c[y][x] in 'bBC' else g.c[y][x])
    return g.render()


# ------------------------------------------------------------------ Patrol Banner 8x24 cells -> 18x54 (vanilla Red Banner layout)
BAN_PAL = dict(k='#535853', e='#5b142e', E='#791834', F='#8a1e3d', a='#ae242e', c='#c82d36', d='#e13434',
               G='#da7b36', Y='#f4cf40', Z='#f9e18b')
BANNER = [
    "kecceeck",
    ".edddde.",
    ".eaccae.",
    ".eaaaae.",
    ".ecaace.",
    ".ecccce.",
    ".eacGGE.",
    ".eGZYcE.",   # emblem (ours): rank chevrons, gold on the vanilla red cloth
    ".EcZYcE.",
    ".EZcYcE.",
    ".EccccE.",
    ".EcZYcE.",
    ".EZcYcE.",
    ".EacccE.",
    ".FccccF.",
    ".FdccdF.",
    ".FddddF.",
    ".FcddcF.",
    ".FdccdF.",
    ".FcddcF.",
    "..FccF..",
    "...FF...",
    "........",
    "........",
]
BANNER[6] = ".eacccE."
BANNER[7] = ".ecZYce."
BANNER[8] = ".EZccYE."
BANNER[9] = ".EcccccE"[:8]
check(BANNER, 8, 'banner')


def banner():
    g = G(8, 24, BAN_PAL)
    g.art(BANNER)
    return g.render()


# ------------------------------------------------------------------ inventory items 30x30 (15x15 cells)
TENT_ITEM = [
    ".......pqqq....",
    ".......pqYq....",
    ".......p.......",
    "......OOO......",
    ".....OabdO.....",
    "....OaabcdO....",
    "...OaabbccdO...",
    "..OaabDEDbcdO..",
    ".OaabDEEEDccdO.",
    "OaabbDEEEDbccdO",
    "OaaabDEEEDbccdO",
    "OQQqqDEEEDqRRRO",
    "OaabbDEhEDbcddO",
    "ObbbcDhhhDcdcdO",
    "OOOOOOOOOOOOOOO",
]
ARM_ITEM = [
    "..OOOOOOOOOOO..",
    ".OaaaaabaaabcO.",
    ".ObFFFFFFFFFcO.",
    ".ObEwEEwEEwEcO.",
    ".ObxwXxwXxwXcO.",
    ".ObEgEEgEEgEcO.",
    ".ObEgEEgEEgEcO.",
    ".OaaaaaaaaaaaO.",
    ".OccdcccdcccdO.",
    ".ObERRREExxEcO.",
    ".ObERYREExXEcO.",
    ".OaaaaaaaaaaaO.",
    ".OaabbbOabbbcO.",
    ".OXXXXXyXXXXXO.",
    "..OOOOOOOOOOO..",
]
CRATE_S = [
    ".OOOOO.",
    "OaaaabO",
    "OabbbcO",
    "OdddddO",
    "OabRrcO",
    "OcccccO",
    ".OOOOO.",
]
BARREL_S = [
    ".OOOOO.",
    "OaaabcO",
    "OHhHHHO",
    "OabbbcO",
    "OabbbcO",
    "OHhHHHO",
    "OaabbcO",
    "OcbbccO",
    ".OOOOO.",
]
BAN_ITEM = [
    "kecdek",
    "edddde",
    "eaccae",
    "eaaaae",
    "ecZYce",
    "eZccYe",
    "ecccce",
    "ecZYce",
    "eZccYe",
    "FccccF",
    "FdccdF",
    "FcddcF",
    ".FccF.",
    "..FF..",
]


def tent_item():
    check(TENT_ITEM, 15, 'tent_item')
    g = G(15, 15, TENT_PAL)
    g.art(TENT_ITEM)
    return g.render()


def arm_item():
    check(ARM_ITEM, 15, 'arm_item')
    g = G(15, 15, ARM_PAL)
    g.art(ARM_ITEM)
    return g.render()


def log_item():
    g = G(15, 15, LOG_PAL)
    g.art(CRATE_S, 0, 8)
    g.art(BARREL_S, 7, 6)
    g.art(CRATE_S, 1, 1)
    return g.render()


def ban_item():
    check(BAN_ITEM, 6, 'ban_item')
    g = G(15, 15, BAN_PAL)
    g.art(BAN_ITEM, 4, 0)
    return g.render()


def main():
    save(gutters(barracks(), 3, 3), 'BarracksTile', 'Content/Tiles/BarracksTile.png')
    save(gutters(armory(), 2, 3), 'ArmoryTile', 'Content/Tiles/ArmoryTile.png')
    save(gutters(logistical(), 3, 2), 'LogisticalCenterTile', 'Content/Tiles/LogisticalCenterTile.png')
    save(gutters(banner(), 1, 3), 'PatrolBannerTile', 'Content/Tiles/PatrolBannerTile.png')
    save(tent_item(), 'BarracksItem', 'Content/Items/Placeables/BarracksItem.png')
    save(arm_item(), 'ArmoryItem', 'Content/Items/Placeables/ArmoryItem.png')
    save(log_item(), 'LogisticalCenterItem', 'Content/Items/Placeables/LogisticalCenterItem.png')
    save(ban_item(), 'PatrolBannerItem', 'Content/Items/Placeables/PatrolBannerItem.png')


if __name__ == '__main__':
    main()

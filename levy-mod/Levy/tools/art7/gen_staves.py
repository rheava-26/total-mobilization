#!/usr/bin/env python3
"""Levy sentry staves: vanilla Ballista Rod/Cane/Staff shaft kept, ballista head swapped for a house.
usage: gen_staves.py <ref_dir> <out_dir>   (vanilla art is 2x-scaled pixel art; we work on the logical grid)
ref_dir holds Ballista_Rod/Cane/Staff.png (fetched from terraria.wiki.gg, never committed)."""
import sys
from PIL import Image

PAL = {'A':(41,43,50),'B':(61,24,26),'C':(63,66,74),'D':(188,199,207),'E':(136,145,152),'F':(88,99,107),
 'G':(162,55,85),'H':(237,92,92),'I':(255,157,127),'J':(123,29,40),'K':(22,28,32),'L':(255,252,202),
 'M':(127,64,53),'N':(98,51,26),'O':(224,174,115),'P':(181,123,92),'Q':(216,245,252),
 'g':(226,170,60),'h':(150,100,30)}

ROD = ["...BBB...",
       "..BIIHB..",
       ".BIHHGJB.",
       "BIHHGGJJB",
       ".KOPPMNK.",
       ".KOLPMNK.",
       ".KOPKKNK.",
       ".KPPKKNK.",
       ".KFEFEFK."]
CANE = ["....BBB....",
        "...BIHJB...",
        "..BIHHGJB..",
        ".BIHHHGGJB.",
        "BIHHHGGGJJB",
        "KghgghgghgK",
        ".KOPPPMMNK.",
        ".KOLLPMMNK.",
        ".KOLLPMKKK.",
        ".KOPPPMKNK.",
        ".KFEFEFEFK."]
STAFF = ["....KBB..",
         "....KHHB.",
         "....KJJ..",
         "....K....",
         "AD.DDD.EA",
         "AD.DDD.EA",
         "ADDDDDEEA",
         "ADEEEEEFA",
         "ADEEQQEFA",
         "ADEEQQEFA",
         "ADEFEEEFA",
         "ADEKKKEFA",
         "ADEKKKEFA",
         "AAAAAAAAA"]
# explicit shaft extension up the vanilla diagonal into the head: {grid row: (x0, "chars")}
EXT_ROD = {9:(4,"BPB"), 8:(5,"BPB")}
EXT_CANE = {11:(5,"KDK"), 10:(6,"KCK")}
EXT_STAFF = {11:(6,"KDK"), 10:(7,"KCK")}

SPECS = {
 'Rod':  dict(n=15, canvas=16, keep=lambda x,y: y>=10 and x<=5, head=(6,0), art=ROD, ext=EXT_ROD),
 'Cane': dict(n=18, canvas=18, keep=lambda x,y: y>=12 and x<=6, head=(7,1), art=CANE, ext=EXT_CANE),
 'Staff':dict(n=19, canvas=20, keep=lambda x,y: y>=12 and x<=6, head=(9,0), art=STAFF, ext=EXT_STAFF),
}

def build(name, sp, ref):
    v = Image.open(f'{ref}/Ballista_{name}.png').convert('RGBA')
    C = sp['canvas']; oy = C - sp['n']  # bottom-aligned, left-aligned
    g = [[None]*C for _ in range(C)]
    for y in range(sp['n']):
        for x in range(sp['n']):
            p = v.getpixel((x*2, y*2))
            if p[3] and sp['keep'](x, y): g[y+oy][x] = p[:3]
    for ry, (rx, chs) in sp['ext'].items():
        for i, ch in enumerate(chs): g[ry+oy][rx+i] = PAL[ch]
    hx, hy = sp['head']
    for j, row in enumerate(sp['art']):
        for i, ch in enumerate(row):
            if ch != '.': g[hy+oy+j][hx+i] = PAL[ch]
    im = Image.new('RGBA', (C*2, C*2), (0,0,0,0))
    for y in range(C):
        for x in range(C):
            if g[y][x]:
                for dy in range(2):
                    for dx in range(2): im.putpixel((x*2+dx, y*2+dy), g[y][x]+(255,))
    return im

if __name__ == '__main__':
    ref, out = sys.argv[1], sys.argv[2]
    for name, sp in SPECS.items():
        im = build(name, sp, ref); im.save(f'{out}/Levy{name}.png'); print(name, im.size)

from PIL import Image
S='/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/art6'
MOD='/home/user/total-mobilization/.claude/worktrees/agent-ac7499c4444e64e83/levy-mod/Levy/Content/Items/OOA/'
P={'O':'2a1a12','H':'ff8a78','R':'e0343a','r':'a82a3c','d':'6e1a2a',
 'W':'d2a470','w':'94603c','b':'5e3a24','Y':'f6c844','y':'c8902c','k':'4a301f',
 'h':'b88458','m':'86583a','G':'f0b030','g':'c08820',
 'S':'b4bac2','s':'8a909a','t':'5c626c','u':'3a3e48','F':'e0343a','f':'8a1e30','Z':'ffd0c8',
 'D':'2a2c34','L':'c8e8f0','p':'d8394a','q':'7a1428'}
def hx(s): return tuple(int(s[i:i+2],16) for i in (0,2,4))+(255,)
def render(rows):
    n=len(rows)
    for r in rows: assert len(r)==n,(len(r),n,r)
    im=Image.new('RGBA',(n*2,n*2),(0,0,0,0));px=im.load()
    for y,row in enumerate(rows):
        for x,ch in enumerate(row):
            if ch=='.':continue
            for dy in(0,1):
                for dx in(0,1):px[x*2+dx,y*2+dy]=hx(P[ch])
    return im
ROD=[
"................",
"..........H.....",
".........HRr....",
"........HRRRr...",
".......HRRRRrd..",
"......HRRRRRrdd.",
".......WWYWwb...",
".......WWYWwb...",
".......WDDWbb...",
".......kkkkkk...",
"......hmk.......",
".....hmk........",
"....hmk.........",
"...hmk..........",
"..GgGk..........",
".OgGOk..........",
]
CANE=[
"..................",
"...........uu.....",
"...........Su.....",
".........ZHSu.....",
"........HRRRrd....",
".......HRRRRrd....",
"......HRRRRRrrd...",
".....HRRRRRRRrdd..",
"......GGGGGGgg....",
"......WWYWWwwb....",
"......WWYWWwwb....",
"......WWwDDwbb....",
"......WWwDDwbb....",
"......kkkkkkkk....",
".....hmk..........",
"....hmk...........",
"...hmk............",
"..GgGk............",
]
STAFF=[
"....................",
"...........kFFf.....",
"...........kFFFf....",
"...........kFf......",
"...........k........",
"........S.S.S.S.....",
"........SSSSSSt.....",
"........SsSSSst.....",
"........SSDSsst.....",
"........SSDSsst.....",
"........SsSSstt.....",
"........SSSSstt.....",
"........SSDDstt.....",
"........sSDDstt.....",
".......SSSSsstuu....",
"......hmttttttu.....",
".....hmk............",
"....hmk.............",
"..pHmk..............",
"..qpqk..............",
]
if __name__=='__main__':
    for n,a in (('Rod',ROD),('Cane',CANE),('Staff',STAFF)):
        im=render(a); im.save(f'{S}/work/Levy{n}.png'); print(n,im.size)
    names=['Ballista_Rod','Ballista_Cane','Ballista_Staff']
    sheet=Image.new('RGBA',(1360,680),(60,60,70,255)); x=10
    for n in names:
        a=Image.open(f'{S}/ref/{n}.png').convert('RGBA');a=a.resize((a.width*8,a.height*8),0);sheet.alpha_composite(a,(x,10));x+=a.width+10
    x=10
    for n in ('Rod','Cane','Staff'):
        b=Image.open(f'{S}/work/Levy{n}.png').convert('RGBA');b=b.resize((b.width*8,b.height*8),0);sheet.alpha_composite(b,(x,340));x+=b.width+10
    sheet.save(f'{S}/work/sheet.png')

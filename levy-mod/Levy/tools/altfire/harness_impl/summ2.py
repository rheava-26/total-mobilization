import csv,os,sys
S='/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/alt5/'
NEW=['AegisBlade','BrimstoneSword','BrinyBaron','LionHeart','TheDarkMaster','Exoblade','DeathsAscension','MantisClaws','StygianShield','GalileoGladius','MirrorBlade','WulfrumScrewdriver','GildedProboscis','Photoviscerator','SurgeDriver','Lightspeed','Violence']
def load(sc,mode):
    f=S+f'{sc}_{mode}.tsv'; d={}; fam={}; notes={}
    if not os.path.exists(f): return d,fam,notes
    for r in csv.DictReader(open(f),delimiter='\t'):
        if r.get('kind')!='weapon': continue
        n=r['internal']
        if n in d: continue  # first row = 15-tile trial
        try: d[n]=int(r['damageToTarget'])
        except: continue
        fam[n]=r['classifier']; notes[n]=r.get('notes','')
    return d,fam,notes
for sc in ['boss','pack']:
    L,fam,_=load(sc,'left'); R,_,_=load(sc,'random'); P,_,pn=load(sc,'policy')
    print(f'\n### {sc} (15-tile trial)'); print('| weapon | family | left | random | policy | rnd/left | pol/left |'); print('|---|---|---:|---:|---:|---:|---:|')
    rl=[];pl=[]
    for n in sorted(L,key=lambda n:(n in NEW,n)):
        l,r,p=L.get(n),R.get(n),P.get(n)
        f=lambda a,b:(f'{a/b:.2f}' if a is not None and b else 'n/a')
        print(f'| {n}{" *" if n in NEW else ""} | {fam[n].split("/")[0]} | {l} | {r} | {p} | {f(r,l)} | {f(p,l)} |')
        if l and r is not None and p is not None: rl.append(r/l); pl.append(p/l)
    if rl: print(f'\nmean rnd/left {sum(rl)/len(rl):.2f}  pol/left {sum(pl)/len(pl):.2f} n={len(rl)}')

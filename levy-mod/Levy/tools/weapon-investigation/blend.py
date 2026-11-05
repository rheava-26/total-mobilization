import math, sys, csv
rows = list(csv.DictReader(open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/tiers.tsv"), delimiter="\t"))
RP = {0:0,1:1,2:2,3:4.5,4:7,5:9.5,6:11.5,7:12.5,8:13.5,9:16,10:17,11:18}
def rarityP(r):
    r=int(r)
    if r<=-1: return 12
    return RP.get(r,19)
def valueP(v):
    v=max(1,float(v)); return max(0,min(18,(math.log10(v)-2.5)*4.5))
def defense(P):
    for lim,d in ((2,8),(5,14),(7,24),(9,34),(11,40),(13,46)):
        if P<lim: return d
    return 55
W_R,W_RP,W_V = 0.60,0.30,0.10
EXP=0.75
for r in rows:
    r['R']=float(r['route']); r['rare']=int(r['rare']); r['val']=int(r['value']); r['score']=float(r['score'])
    r['perHit']=float(r['perHit']); r['dpsAdj']=float(r['dpsAdj']); r['hps']=float(r['hitsPerSec'])
    custom = (r['rare']>11 or r['rare']<-1) and r['mod']!='CalamityMod'
    r['RP']=rarityP(r['rare']) if not custom else None; r['VP']=valueP(r['val'])
    if r['R']<0: r['R']=r['RP'] if r['RP'] is not None else r['VP']
    r['P']=(W_R*r['R']+W_RP*r['RP']+W_V*r['VP']) if not custom else (0.85*r['R']+0.15*r['VP'])
    D=defense(r['P'])
    r['D']=D
    eff=max(1.0,r['perHit']-EXP*D)/max(1.0,r['perHit'])
    r['eff']=eff
    r['effDps']=r['dpsAdj']*eff
    r['rank']=12*r['P']+4*math.log(1+r['effDps']/10)
    r['cls']=r['class']
    r['full']=r['mod']+'/'+r['name']
STAGES=[("pre-boss",0.99),("post-EoC",2.99),("post-Skeletron",5.99),("early HM (post-WoF)",7.99),("post-mech",11.99),("post-Plantera",12.99)]
CLASSES=["melee","ranged","magic","summon","none"]
out=[]
def line(r,key): return "%-34s R=%5.2f rare=%2d val=%8d  cur=%7.1f  P*=%5.2f eff=%.2f effDps=%7.1f rank=%6.1f  [%s]"%(r['full'][:34],r['R'],r['rare'],r['val'],r['score'],r['P'],r['eff'],r['effDps'],r['rank'],r['routeSrc'][:30])
only=sys.argv[1:]
for name,lim in STAGES:
    avail=[r for r in rows if r['R']<=lim and r['family'] not in ('Unsupported',)]
    out.append("\n######## STAGE %s  (%d weapons available) ########"%(name,len(avail)))
    for c in CLASSES:
        pool=[r for r in avail if r['cls']==c]
        if not pool: continue
        byc=sorted(pool,key=lambda r:-r['score'])[:10]; byb=sorted(pool,key=lambda r:-r['rank'])[:10]
        out.append("\n-- class %s: CURRENT top10 (Score)"%c)
        for r in byc: out.append("   "+line(r,'s'))
        out.append("-- class %s: BLENDED top10 (rank)"%c)
        for r in byb: out.append("   "+line(r,'b'))
    out.append("\n-- ALL CLASSES: CURRENT top10"); [out.append("   "+line(r,'s')) for r in sorted(avail,key=lambda r:-r['score'])[:10]]
    out.append("-- ALL CLASSES: BLENDED top10"); [out.append("   "+line(r,'b')) for r in sorted(avail,key=lambda r:-r['rank'])[:10]]
open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/rank_tables.txt","w").write("\n".join(out))
import pickle; pickle.dump(rows,open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/rows.pkl","wb"))
print(len(rows))

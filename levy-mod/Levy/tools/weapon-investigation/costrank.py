import math, collections
exec(open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/blend2.py").read().split('if __name__')[0])
for r in rows:
    r['leaf']=float(r['leafCost']); r['leaf']=0.0 if abs(r['leaf']-r['val'])<1 else r['leaf']; r['cost']=r['val']+r['leaf']   # Eve: sell value PLUS total (recursive) material cost
    r['fin']=final(r)[0]
STAGES=[("pre-boss",0.99),("post-EoC",2.99),("post-Skeletron",5.99),("early HM (post-WoF)",7.99),("post-mech",11.99),("post-Plantera",12.99)]
CLASSES=["melee","ranged","magic","summon","none"]
def line(r): return "%-30s cost=%9d (val %8d + mats %8d) tier=%5.2f rare=%2d cur=%7.1f tierRank=%6.1f effDps=%6.1f"%(r['full'][:30],r['cost'],r['val'],r['leaf'],r['R'],r['rare'],r['score'],r['fin'],r['effDps'])
out=[]
for name,lim in STAGES:
    av=[r for r in rows if r['R']<=lim and not r['isTool']=='True']
    out.append("\n######## STAGE %s (%d weapons reachable by tier <= %.2f) ########"%(name,len(av),lim))
    for c in CLASSES:
        pool=[r for r in av if r['cls']==c]
        if not pool: continue
        out.append("\n-- class %s: COST-primary top10 (value + total material cost)"%c); out += ["   "+line(r) for r in sorted(pool,key=lambda r:-r['cost'])[:10]]
        out.append("-- class %s: TIER-primary top10 (blend)"%c); out += ["   "+line(r) for r in sorted(pool,key=lambda r:-r['fin'])[:10]]
        out.append("-- class %s: CURRENT Score top10"%c); out += ["   "+line(r) for r in sorted(pool,key=lambda r:-r['score'])[:10]]
open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/cost_tables.txt","w").write("\n".join(out))
by={r['full']:r for r in rows}
print("== cost-primary key facts ==")
for n in ["Vanilla/Minishark","Vanilla/Megashark","Vanilla/ClockworkAssaultRifle","Vanilla/MythrilSword","Vanilla/ChlorophyteSaber","Vanilla/ChlorophyteClaymore","Vanilla/ZombieArm","Vanilla/TerraBlade","Vanilla/Xenopopper","Vanilla/LaserMachinegun","Vanilla/InfluxWaver","Vanilla/RazorbladeTyphoon","Vanilla/TheHorsemansBlade","Vanilla/Muramasa","Vanilla/BladeofGrass","Vanilla/WoodenSword","CalamityMod/WulfrumKnife","CalamityMod/MycelialClaws","CalamityMod/Needler","CalamityMod/SpeedBlaster"]:
    r=by.get(n);
    if r: print("%-32s cost=%9d val=%8d mats=%8d tier=%5.2f cur=%7.1f effDps=%7.1f"%(n,r['cost'],r['val'],r['leaf'],r['R'],r['score'],r['effDps']))
# early-HM armory 9: cost vs current vs tier
exec(open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/mk.py").read().split("def W")[0])
names=["Vanilla/"+n for n in V]+["CalamityMod/"+n for n in C]+["ThoriumMod/"+n for n in T]
pool=[by[n] for n in names if n in by]
for lab,key in (("current Score","score"),("cost-primary","cost"),("tier-primary blend","fin")):
    o=sorted(pool,key=lambda r:-r[key]); print("%-20s top9: %s | Minishark #%d, Wulfrum #%d"%(lab,", ".join(r['name'] for r in o[:9]),1+[r['name'] for r in o].index('Minishark'),1+[r['name'] for r in o].index('WulfrumKnife')))
# cost key sanity: biggest cost / effDps outliers
cand=[r for r in rows if r['cls'] in ('melee','ranged','magic')]
print("\ncost-primary absurdly HIGH (top 12 by cost among weapons with effDps < 5):")
for r in sorted([x for x in cand if x['effDps']<5],key=lambda r:-r['cost'])[:12]: print("   "+line(r))
print("cost-primary absurdly LOW (cost < 20000 but effDps > 150):")
for r in sorted([x for x in cand if x['cost']<20000 and x['effDps']>150],key=lambda r:r['cost'])[:12]: print("   "+line(r))
import statistics
def sp(a,b):
    def rk(v):
        s=sorted(range(len(v)),key=lambda i:v[i]); r=[0]*len(v)
        for k,i in enumerate(s): r[i]=k
        return r
    ra,rb=rk(a),rk(b); n=len(a); ma=sum(ra)/n; mb=sum(rb)/n
    return sum((x-ma)*(y-mb) for x,y in zip(ra,rb))/math.sqrt(sum((x-ma)**2 for x in ra)*sum((y-mb)**2 for y in rb))
rs=[r for r in cand if not r['routeSrc'].startswith('sig')]
print("\nSpearman vs route tier: value %.2f, cost(value+mats) %.2f ; vs effDps: value %.2f, cost %.2f, current score %.2f"%(sp([r['R'] for r in rs],[r['val'] for r in rs]),sp([r['R'] for r in rs],[r['cost'] for r in rs]),sp([r['effDps'] for r in rs],[r['val'] for r in rs]),sp([r['effDps'] for r in rs],[r['cost'] for r in rs]),sp([r['effDps'] for r in rs],[r['score'] for r in rs])))

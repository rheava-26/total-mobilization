import pickle, math, sys, re
exec(open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/blend.py").read().split("STAGES=")[0])
# route-confidence cap: a recipe/sig route may not outrun the item's own rarity/value signal by more than 4 stages (stops one unrouted modded ingredient poisoning a whole tree)
for r in rows:
    own = max(r['RP'] if r['RP'] is not None else 0, r['VP'])
    if r['routeSrc'].startswith('recipe') or r['routeSrc'].startswith('sig'):
        r['R']=min(r['R'], own+4)
    custom = (r['rare']>11 or r['rare']<-1) and r['mod']!='CalamityMod'
    r['P']=(W_R*r['R']+W_RP*r['RP']+W_V*r['VP']) if not custom else (0.85*r['R']+0.15*r['VP'])
    D=defense(r['P']); r['D']=D
    r['eff']=max(1.0,r['perHit']-EXP*D)/max(1.0,r['perHit']); r['effDps']=r['dpsAdj']*r['eff']
    r['rank']=12*r['P']+4*math.log(1+r['effDps']/10)
by={r['full']:r for r in rows}
def L(n):
    r=by.get(n);
    return "%-30s R=%5.2f rare=%2d val=%8d  cur=%7.1f  P*=%5.2f D=%2d eff=%.2f effDps=%7.1f rank=%6.1f [%s]"%(n[:30],r['R'],r['rare'],r['val'],r['score'],r['P'],r['D'],r['eff'],r['effDps'],r['rank'],r['routeSrc'][:28]) if r else n+" (missing)"
out=[]
out.append("== KEY ITEMS ==")
for n in ["Vanilla/Minishark","Vanilla/Musket","Vanilla/ClockworkAssaultRifle","Vanilla/PhoenixBlaster","Vanilla/Boomstick","Vanilla/Megashark","Vanilla/Muramasa","Vanilla/NightsEdge","Vanilla/BladeofGrass","Vanilla/Starfury","Vanilla/CobaltSword","Vanilla/MythrilSword","Vanilla/TerraBlade","Vanilla/Zenith","CalamityMod/WulfrumKnife","CalamityMod/WulfrumBlunderbuss","CalamityMod/Kylie","CalamityMod/ShinobiBlade","CalamityMod/Archerfish","CalamityMod/MycelialClaws","CalamityMod/BurntSienna","CalamityMod/SpeedBlaster","CalamityMod/Needler","CalamityMod/Avalanche","CalamityMod/TitaniumRailgun","CalamityMod/MythrilKnife","ThoriumMod/HitScanner","ThoriumMod/MythrilStaff","ThoriumMod/CobaltStaff","ThoriumMod/ThoriumWand","ThoriumMod/CobaltRevolver","ThoriumMod/TommyGun","ThoriumMod/PalladiumSMG","Vanilla/FlowerofFire","Vanilla/DemonScythe","Vanilla/SpaceGun","Vanilla/WaterBolt"]:
    out.append(L(n))
STAGES=[("pre-boss",0.99),("post-EoC",2.99),("post-Skeletron",5.99),("early HM (post-WoF)",7.99),("post-mech",11.99),("post-Plantera",12.99)]
CLASSES=["melee","ranged","magic","summon","none"]
def line(r): return "%-32s R=%5.2f rare=%2d val=%8d cur=%7.1f P*=%5.2f eff=%.2f rank=%6.1f [%s]"%(r['full'][:32],r['R'],r['rare'],r['val'],r['score'],r['P'],r['eff'],r['rank'],r['routeSrc'][:24])
tabs=[]
for name,lim in STAGES:
    avail=[r for r in rows if r['R']<=lim]
    tabs.append("\n######## STAGE %s  (%d weapons with route tier <= %.2f) ########"%(name,len(avail),lim))
    for c in CLASSES:
        pool=[r for r in avail if r['cls']==c]
        if not pool: continue
        tabs.append("\n-- class %s: CURRENT top10 (Score)"%c); tabs += ["   "+line(r) for r in sorted(pool,key=lambda r:-r['score'])[:10]]
        tabs.append("-- class %s: BLENDED top10 (rank)"%c); tabs += ["   "+line(r) for r in sorted(pool,key=lambda r:-r['rank'])[:10]]
    tabs.append("\n-- ALL: CURRENT top10"); tabs += ["   "+line(r) for r in sorted(avail,key=lambda r:-r['score'])[:10]]
    tabs.append("-- ALL: BLENDED top10"); tabs += ["   "+line(r) for r in sorted(avail,key=lambda r:-r['rank'])[:10]]
open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/rank_tables.txt","w").write("\n".join(tabs))
# --- armory simulation: the scenario-1 stock, 9 levies, best-first by each metric ---
stock=[]
exec(open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/mk.py").read().split("def W")[0])
names=["Vanilla/"+n for n in V]+["CalamityMod/"+n for n in C]+["ThoriumMod/"+n for n in T]
pool=[by[n] for n in names if n in by]
def pick(pool,key,k=9):
    return [r['name'] for r in sorted(pool,key=lambda r:-r[key])[:k]]
out.append("\n== ARMORY (early-HM stock of %d weapons), 9 levies best-first =="%len(pool))
out.append("CURRENT score order : "+", ".join(pick(pool,'score')))
out.append("BLENDED rank order  : "+", ".join(pick(pool,'rank')))
out.append("\nMinishark rank position among %d: current #%d, blended #%d"%(len(pool),1+[r['name'] for r in sorted(pool,key=lambda r:-r['score'])].index('Minishark'),1+[r['name'] for r in sorted(pool,key=lambda r:-r['rank'])].index('Minishark')))
# class preference: a class-C levy takes class C's best if its rank is within DELTA of the overall best, else the overall best
def classpref(pool,cls,delta):
    best=max(pool,key=lambda r:r['rank']); cb=[r for r in pool if r['cls']==cls]
    if not cb: return best['name']
    c=max(cb,key=lambda r:r['rank'])
    return c['name'] if c['rank']>=best['rank']-delta else best['name']+" (overall)"
for delta in (0,12,18,30):
    out.append("class-pref delta=%d pts (%.1f stages): "%(delta,delta/12)+"; ".join("%s->%s"%(c,classpref(pool,c,delta)) for c in ("melee","ranged","magic","summon","none")))
open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/analysis.txt","w").write("\n".join(out))
print("\n".join(out))
# sanity: weapons whose rank percentile differs hugely from score percentile
import statistics
sc=sorted(r['score'] for r in rows); rk=sorted(r['rank'] for r in rows)
def pct(lst,v):
    import bisect; return bisect.bisect_left(lst,v)/len(lst)
fails=sorted(rows,key=lambda r:-(pct(rk,r['rank'])-pct(sc,r['score'])))
print("\n== BIGGEST RISERS (rank pctile - score pctile) =="); [print("  "+L(r['full'])) for r in fails[:14]]
print("\n== BIGGEST FALLERS =="); [print("  "+L(r['full'])) for r in fails[-14:]]

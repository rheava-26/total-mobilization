import statistics, collections
exec(open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/blend2.py").read().split('if __name__')[0])
STAGES=[("pre-boss",0.99),("post-EoC",2.99),("post-Skeletron",5.99),("early HM",7.99),("post-mech",11.99),("post-Plantera",12.99)]
print("composition of the top-9 (9 levies, best-first, no variety) by class")
for name,lim in STAGES:
    av=[r for r in rows if r['R']<=lim]
    for key,lab in (('score','current'),('rank','blended')):
        top=sorted(av,key=lambda r:-(r[key] if key=='score' else final(r)[0]))[:9]
        print("  %-14s %-8s %s"%(name,lab,dict(collections.Counter(r['cls'] for r in top))))
# mana sustainability
ms=[r for r in rows if r['cls']=='magic' and int(r['mana'])>0 and r['R']<=7.99]
fs=[]
for r in ms:
    mana=int(r['mana']); hps=r['hps']
    fs.append(min(1.0, 6.0/(mana*max(0.01,hps))))
print("\nmana-using magic weapons up to early HM: n=%d, sustained-fire factor (combat regen 6 mana/s over cast cost*casts/s): median %.2f, p25 %.2f, share below 0.5: %.0f%%"%(len(ms),statistics.median(fs),sorted(fs)[len(fs)//4],100*sum(1 for f in fs if f<0.5)/len(fs)))
for n in ("Vanilla/WaterBolt","Vanilla/FlowerofFire","Vanilla/DemonScythe","ThoriumMod/MythrilStaff","ThoriumMod/CobaltStaff","CalamityMod/TheCauldron","Vanilla/CrystalStorm"):
    r={x['full']:x for x in rows}.get(n)
    if r: print("   %-28s mana=%s hps=%.2f sustained=%.2f cur=%.0f"%(n,r['mana'],r['hps'],min(1.0,6.0/(int(r['mana'])*max(0.01,r['hps']))) if int(r['mana'])>0 else 1.0,r['score']))
heal=[r for r in rows if r['mod']=='ThoriumMod' and r['family']=='Magic' and r['cls']=='none' and int(r['dmg'])<=0]
print("\nThorium healer/support wands (damage<=0) scored by the heal path: n=%d, scores %s"%(len(heal),collections.Counter(round(r['score']) for r in heal).most_common(5)))

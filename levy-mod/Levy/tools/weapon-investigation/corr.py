import math, collections
exec(open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/blend2.py").read().split('if __name__')[0])
def rank(v):
    s=sorted(range(len(v)),key=lambda i:v[i]); r=[0]*len(v)
    for k,i in enumerate(s): r[i]=k
    return r
def sp(a,b):
    ra,rb=rank(a),rank(b); n=len(a); ma=sum(ra)/n; mb=sum(rb)/n
    c=sum((x-ma)*(y-mb) for x,y in zip(ra,rb)); return c/math.sqrt(sum((x-ma)**2 for x in ra)*sum((y-mb)**2 for y in rb))
for m in ('Vanilla','CalamityMod','ThoriumMod'):
    rs=[r for r in rows if r['mod']==m and not r['routeSrc'].startswith('sig')]
    print(m,len(rs),"spearman route~value %.2f  route~rarity %.2f  route~score %.2f  route~effDps %.2f"%(sp([r['R'] for r in rs],[r['val'] for r in rs]),sp([r['R'] for r in rs],[r['rare'] for r in rs]),sp([r['R'] for r in rs],[r['score'] for r in rs]),sp([r['R'] for r in rs],[r['effDps'] for r in rs])))
by={r['full']:r for r in rows}
exec(open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/mk.py").read().split("def W")[0])
names=["Vanilla/"+n for n in V]+["CalamityMod/"+n for n in C]+["ThoriumMod/"+n for n in T]
pool=[by[n] for n in names if n in by]
print("value-only top9 in armory:", ", ".join("%s(%dg)"%(r['name'],r['val']//10000) for r in sorted(pool,key=lambda r:-r['val'])[:9]))
print("rarity-only top:", ", ".join("%s(r%d)"%(r['name'],r['rare']) for r in sorted(pool,key=lambda r:-(r['rare'] if r['rare']<12 else 0))[:9]))
cand=[r for r in rows if 7<=r['R']<=12]
print("\nvalue misleads UP (high value, low effDps):")
for r in sorted(cand,key=lambda r:-(r['val']/max(1,r['effDps'])))[:8]: print("    %-30s val=%8d effDps=%6.1f cur=%6.1f"%(r['full'][:30],r['val'],r['effDps'],r['score']))
print("value misleads DOWN (low value, high effDps):")
for r in sorted(cand,key=lambda r:(r['val']/max(1,r['effDps'])))[:8]: print("    %-30s val=%8d effDps=%6.1f cur=%6.1f"%(r['full'][:30],r['val'],r['effDps'],r['score']))
for m in ('CalamityMod','ThoriumMod'):
    c=collections.Counter(r['val'] for r in rows if r['mod']==m); print(m,"most common values:",c.most_common(6))

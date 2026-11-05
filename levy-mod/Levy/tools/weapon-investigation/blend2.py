import math
src=open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/analyze.py").read()
exec(src.split("by={r['full']")[0])   # rows with cap applied
# --- credible-tier floor: P may not exceed (highest stage whose weak-quartile damage the weapon matches) + 1.5 ---
supported=[r for r in rows if r['cls'] in ('melee','ranged','magic') ]
import bisect
def q25(vals):
    v=sorted(vals); return v[len(v)//4] if v else 0
buckets={}
for r in supported:
    b=int(r['P'])   # integer stage of the (uncapped) blended progression
    buckets.setdefault(b,[]).append(r['effDps'])
stageDps={b:q25(v) for b,v in buckets.items() if len(v)>=5}
ks=sorted(stageDps)
def credible(effDps):
    c=0
    for b in ks:
        if effDps>=stageDps[b]*0.8: c=b
    return c
def final(r,W=12,K=4,floor=True,slack=1.5):
    P=r['P']
    if floor and r['cls'] in ('melee','ranged','magic','none','summon'):
        P=min(P, credible(r['effDps'])+slack)
    return W*P+K*math.log(1+r['effDps']/10), P
if __name__=="__main__":
    print("stage q25 effDps:",{b:round(stageDps[b]) for b in ks})
    # armory
    exec(open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/mk.py").read().split("def W")[0])
    by={r['full']:r for r in rows}
    names=["Vanilla/"+n for n in V]+["CalamityMod/"+n for n in C]+["ThoriumMod/"+n for n in T]
    pool=[by[n] for n in names if n in by]
    for (W,K,fl) in ((12,4,False),(12,4,True),(10,5,True),(8,6,True),(6,8,True)):
        sc=sorted(pool,key=lambda r:-final(r,W,K,fl)[0])
        print("W=%d K=%d floor=%s -> %s"%(W,K,fl,", ".join(r['name'] for r in sc[:9])),"| Minishark #%d"%(1+[r['name'] for r in sc].index('Minishark')),"Wulfrum #%d"%(1+[r['name'] for r in sc].index('WulfrumKnife')))

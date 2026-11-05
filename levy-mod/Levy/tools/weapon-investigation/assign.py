import re, collections, sys, math
exec(open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/costrank.py").read().split('STAGES=')[0])
def norm(s): return re.sub(r'[^a-z0-9]','',s.lower())
byn={}
for r in rows: byn.setdefault(norm(r['name']),r)
txt=open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/s3.txt").read()
def scen_block(name):
    m=re.search(r"## SCENARIO %s\n(.*?)(?=\n## |\Z)"%re.escape(name),txt,re.S); return m.group(1)
def mixlist(order,counts):
    left=dict(counts); res=[]
    cyc=["ranged","magic","summon","none","melee"] if order=="meleelast" else ["melee","ranged","magic","summon","none"]
    if order=="interleave":
        while sum(left.values())>0:
            for c in cyc:
                if left.get(c,0)>0: res.append(c); left[c]-=1
    else:
        for c in cyc:
            while left.get(c,0)>0: res.append(c); left[c]-=1
    return res
def parse_current(block):
    first=block.split("tally:")[0]
    out=[]
    for l in first.splitlines():
        m=re.match(r"\s+soldier (\d+): weapon=(.*?) side=(.*?) armorClass=(\w+)",l)
        if not m: continue
        w=m.group(2)
        mm=re.match(r"(.*?)\[(\w+)/(\w+) sc=(\d+)\]",w)
        out.append(dict(id=int(m.group(1)),armor=m.group(4),weapon=(mm.group(1).split('/')[-1] if mm else '-'),wcls=(mm.group(3) if mm else None),score=(int(mm.group(4)) if mm else 0),side=m.group(3)[:28]))
    return out
def parse_stock(block):
    st=[]
    for l in block.splitlines():
        m=re.match(r"\s{4}(\S.*?) \| (\w+)/(\w+) \| (\d+) (\d+) (\d+) (\d+) (\d+) \| ([\d.]+)",l)
        if m: st.append(dict(name=m.group(1).split('/')[-1],cls=m.group(3),score=float(m.group(9)),value=int(m.group(8))))
    return st
def cost_of(name):
    r=byn.get(norm(name)); return r['cost'] if r else 0
def assign(souls,weapons,key):
    """souls: list of (id, armorClass); weapons: list of dicts. Global: class-matched first (best weapon to the worst-equipped soldier of that class), then flex/leftovers; variety: second copy of a type costs 25%."""
    W=sorted(weapons,key=lambda w:-key(w)); held={}; used=set(); typecount=collections.Counter()
    S=[dict(id=i,armor=a,w=None) for i,a in souls]
    def eq(s): return key(s['w']) if s['w'] else -1
    # phase A: class-matched. weapon best-first -> the worst-equipped soldier of its class
    for w in W:
        cands=[s for s in S if s['armor']==w['cls'] and s['w'] is None]
        if not cands: continue
        cands[0]['w']=w; used.add(id(w))
    # phase B: still-unarmed soldiers (class empty, or no armor) take the best remaining weapon, worst-equipped soldiers first; 'none' armor soldiers go last so they never starve a classed levy
    for pri in (lambda s:s['armor']!='none', lambda s:s['armor']=='none'):
        for s in [x for x in S if x['w'] is None and pri(x)]:
            rest=[w for w in W if id(w) not in used]
            if not rest: break
            pick=rest[0]
            s['w']=pick; used.add(id(pick))
    return S,[w for w in W if id(w) not in used]
def summarize(rows_):
    by=collections.defaultdict(list)
    for r in rows_: by[r['armor']].append(r)
    parts=[]
    for c in ("melee","ranged","magic","summon","none"):
        if c in by:
            sc=[r['score'] for r in by[c]]
            parts.append("%s n=%d min=%d mean=%d"%(c,len(sc),min(sc),sum(sc)/len(sc)))
    return "; ".join(parts)
out=[]
cases=[("H_eveStock_9_interleave",9,"interleave",dict(melee=4,ranged=2,magic=2,summon=1)),("H_eveStock_9_meleelast",9,"meleelast",dict(melee=4,ranged=2,magic=2,summon=1)),
       ("H_eveStock_20_interleave",20,"interleave",dict(melee=7,ranged=5,magic=4,summon=2,none=2)),("H_eveStock_20_meleelast",20,"meleelast",dict(melee=7,ranged=5,magic=4,summon=2,none=2))]
for name,n,order,counts in cases:
    block=scen_block(name)
    cur=parse_current(block)
    stock=parse_stock(block)
    for l in block.split("tally:")[0].splitlines():   # sidearms held after the real run are shelf items too: the proposal pools them with everything else
        mm=re.search(r"side=(.*?)\[(\w+)/(\w+) sc=(\d+)\] armorClass",l)
        if mm: stock.append(dict(name=mm.group(1).split('/')[-1],cls=mm.group(3),score=float(mm.group(4)),value=0))
    # current-run weapons from soldiers are included in the stock table already
    mix=mixlist(order,counts)
    souls=[(100+i,c) for i,c in enumerate(mix)]
    out.append("\n===== %s: %d levies %s, classes in enlist order: %s"%(name,n,counts,",".join(m[0] for m in mix)))
    out.append("CURRENT (real ArmoryTE.IssueGear, soldier-by-soldier): "+summarize(cur))
    mism=sum(1 for r in cur if r['armor']!='none' and r['wcls'] and r['wcls']!=r['armor']); empty=sum(1 for r in cur if r['weapon']=='-')
    out.append("   class-mismatched primaries: %d, unarmed: %d, worst armed score: %s"%(mism,empty,min([r['score'] for r in cur if r['weapon']!='-'] or [0])))
    for key,lab in ((lambda w:w['score'],"score"),(lambda w:cost_of(w['name']),"cost")):
        res,left=assign(souls,stock,key)
        rr=[dict(id=s['id'],armor=s['armor'],weapon=(s['w']['name'] if s['w'] else '-'),wcls=(s['w']['cls'] if s['w'] else None),score=(int(s['w']['score']) if s['w'] else 0)) for s in res]
        mm=sum(1 for r in rr if r['armor']!='none' and r['wcls'] and r['wcls']!=r['armor']); em=sum(1 for r in rr if r['weapon']=='-')
        out.append("PROPOSED global (class-matched, worst-equipped first, key=%s): "%lab+summarize(rr))
        out.append("   class-mismatched primaries: %d, unarmed: %d, worst armed score: %s, leftover weapons: %s"%(mm,em,min([r['score'] for r in rr if r['weapon']!='-'] or [0]),", ".join(w['name'] for w in left)[:100]))
    out.append("   per levy (armor | current weapon sc | proposed(score-key) weapon sc):")
    res,left=assign(souls,stock,lambda w:w['score'])
    resd={s['id']:s for s in res}
    for r in cur:
        s=resd[r['id']]
        out.append("     %3d %-7s | %-24s %5d | %-24s %5d"%(r['id'],r['armor'],r['weapon'][:24],r['score'],(s['w']['name'] if s['w'] else '-')[:24],(int(s['w']['score']) if s['w'] else 0)))
open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/assignment.txt","w").write("\n".join(out))
print("\n".join(out))

import gzip, json, glob, collections, statistics, sys
def rows(p):
    out=[]
    try:
        with gzip.open(p,'rb') as g:
            for l in g:
                try: out.append(json.loads(l))
                except Exception: pass
    except Exception: pass
    return out
files = glob.glob('/home/user/lab/hmbattery2/recordings/**/*.levyrec', recursive=True)
eve = glob.glob('/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/eve_recs/*.levyrec')
tot = collections.Counter(); dmgsum = collections.Counter()
by = collections.defaultdict(list)
nrec=0
for grp, fl in (('bat',files),('eve',eve)):
    for p in fl:
        r = rows(p)
        if not r: continue
        nrec+=1
        h = next((x for x in r if x.get('k')=='hdr'),{})
        names = next((x for x in reversed(r) if x.get('k')=='names'),{})
        npcn = names.get('npcs',{}); prn = names.get('projectiles',{})
        mode = h.get('mode')
        for x in r:
            if x.get('k')=='e' and x.get('e')=='lhit':
                key=(grp,x['src'],x['by'],npcn.get(str(x['npc']),x['npc']),prn.get(str(x['proj']),x['proj']) if x['proj']>=0 else '-')
                by[key].append(x['dmg'])
print('records',nrec)
agg=collections.defaultdict(lambda:[0,0])
for k,v in by.items():
    agg[(k[0],k[1],k[2])][0]+=len(v); agg[(k[0],k[1],k[2])][1]+=sum(v)
for k,v in sorted(agg.items()): print(k,v[0],v[1],round(v[1]/v[0],1))
print()
rowsout=[]
for k,v in by.items():
    rowsout.append((k,len(v),statistics.median(v),round(statistics.mean(v),1),max(v),min(v)))
rowsout.sort(key=lambda t:-t[1]*t[3])
for t in rowsout[:70]: print(t)

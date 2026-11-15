import gzip, json, glob, collections, statistics
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
eve = sorted(glob.glob('/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/eve_recs/*.levyrec'))
cmd = collections.defaultdict(list); lev = collections.defaultdict(list)
defs = collections.defaultdict(list)
for grp, fl in (('bat',files),('eve',eve)):
    for p in fl:
        r = rows(p)
        names = next((x for x in reversed(r) if x.get('k')=='names'),{})
        npcn = names.get('npcs',{}); prn = names.get('projectiles',{})
        h = next((x for x in r if x.get('k')=='hdr'),{})
        for x in r:
            if x.get('k')!='e': continue
            if x['e']=='hit':
                key=(grp, npcn.get(str(x['npc']),x['npc']) if x['npc']>=0 else '-', prn.get(str(x['proj']),x['proj']) if x['proj']>=0 else '-', x['other'])
                cmd[key].append(x['dmg'])
            if x['e']=='lhit':
                src='body' if x['src']=='body' else x['src']
                key=(grp, npcn.get(str(x['npc']),x['npc']) if x['npc']>=0 else '-', prn.get(str(x['proj']),x['proj']) if x['proj']>=0 else '-', x['src'])
                lev[key].append(x['dmg'])
        c=h.get('cmd',{})
        if c: defs[grp].append((c.get('defense'), c.get('life')))
print({g:len(v) for g,v in defs.items()})
print('--- commander hits')
for k,v in sorted(cmd.items(), key=lambda t:-len(t[1]))[:40]:
    print(k,len(v),statistics.median(v),round(statistics.mean(v),1),max(v))
print('--- levy hits, same (grp,npc,proj)')
lv2 = collections.defaultdict(list)
for k,v in lev.items(): lv2[(k[0],k[1],k[2])]+=v
for k,v in cmd.items(): pass
cm2 = collections.defaultdict(list)
for k,v in cmd.items(): cm2[(k[0],k[1],k[2])]+=v
print('npc/proj, cmd n/med/mean, levy n/med/mean')
for k in sorted(cm2, key=lambda k:-len(cm2[k])):
    if k in lv2:
        a=cm2[k]; b=lv2[k]
        print(k, (len(a),statistics.median(a),round(statistics.mean(a),1)), (len(b),statistics.median(b),round(statistics.mean(b),1)))

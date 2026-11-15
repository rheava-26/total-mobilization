import gzip, json, glob, collections
p='/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/eve_recs/2026-11-14_16-24_Anahita_won.levyrec'
rows=[]
for l in gzip.open(p,'rb'):
    try: rows.append(json.loads(l))
    except Exception: pass
ev=[x for x in rows if x.get('k')=='e' and x['e']=='lhit' and x['src']=='other']
print(len(ev))
byid=collections.defaultdict(list)
for x in ev: byid[x['id']].append(x)
for i,(k,v) in enumerate(sorted(byid.items(), key=lambda t:-len(t[1]))[:3]):
    print('levy',k,len(v),'first t',v[0]['t'],'last',v[-1]['t'],'sum',sum(a['dmg'] for a in v))
    print([ (a['t'],a['dmg'],a['life'],a['npc'],a['proj'],a['x'],a['y']) for a in v[:12]])
# time buckets
b=collections.Counter(); bd=collections.Counter()
for x in ev: b[x['t']//600]+=1; bd[x['t']//600]+=x['dmg']
print(sorted(b.items())); print(sorted(bd.items()))
names=next((x for x in rows if x.get('k')=='names'),{})
# boss position vs hit?
hdr=rows[0]; print(hdr.get('boss'), hdr['meta'])
end=[x for x in rows if x.get('k')=='end'][0]; print({k:end[k] for k in end if k in('result','durationSec','armyAlive','armyTotal')})

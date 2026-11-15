import gzip, json, glob, collections
files = glob.glob('/home/user/lab/hmbattery2/recordings/**/*.levyrec', recursive=True)
eve = sorted(glob.glob('/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/eve_recs/*.levyrec'))
for grp, fl in (('bat',files),('eve',eve)):
    c=collections.Counter(); tot=0; perrec=collections.Counter(); kills=0
    gaps=collections.Counter()
    for p in fl:
        last={}
        try:
            for l in gzip.open(p,'rb'):
                try: x=json.loads(l)
                except Exception: continue
                if x.get('k')=='e' and x['e']=='lhit' and x['src']=='other':
                    c[x['dmg']]+=1; tot+=1; perrec[p.split('/')[-1]]+=1; kills+=x['kill']
                    if x['id'] in last: gaps[min(x['t']-last[x['id']],200)]+=1
                    last[x['id']]=x['t']
        except Exception: pass
    print(grp,tot,'kills',kills); print(c.most_common(15)); print('gap ticks',sorted(gaps.items(), key=lambda t:-t[1])[:10]); print(perrec.most_common(4))

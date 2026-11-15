import gzip, json, glob, collections
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
cnt = collections.defaultdict(lambda: collections.Counter())
cmdb = collections.defaultdict(lambda: collections.Counter())
for grp, fl in (('bat',files),('eve',eve)):
    for p in fl:
        r = rows(p)
        names = next((x for x in reversed(r) if x.get('k')=='names'),{})
        bn = names.get('buffs',{})
        for x in r:
            if x.get('k')=='e' and x.get('e')=='lbuf':
                for b in x['b']:
                    cnt[grp][bn.get(str(b[0]), b[0])]+=1
            if x.get('k')=='e' and x.get('e')=='buffs':
                for b in x.get('b',x.get('buffs',[])):
                    try: cmdb[grp][bn.get(str(b[0]), b[0])]+=1
                    except Exception: pass
for g in cnt:
    print('LEVY', g, cnt[g].most_common(80))
for g in cmdb:
    print('CMD', g, cmdb[g].most_common(80))
# show a sample 'buffs' event
for p in eve[:1]:
    for x in rows(p):
        if x.get('k')=='e' and x.get('e')=='buffs':
            print(x); break

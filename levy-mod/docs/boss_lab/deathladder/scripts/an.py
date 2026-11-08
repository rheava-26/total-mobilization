import csv, collections, re, sys, statistics as st
D='/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/dl/out/'
f=sys.argv[1] if len(sys.argv)>1 else 'bulk'
rows=list(csv.DictReader(open(D+f+'.tsv'),delimiter='\t'))
dl={r['key']:r for r in csv.DictReader(open(D+f+'.tsv.dl.tsv'),delimiter='\t')}
hit={}
for l in open(D+f+'.tsv.hit.tsv'):
    k,_,v=l.rstrip('\n').partition('\t'); hit[k]=v
def cell(k):
    m=re.search(r'\|dl(\w+)$',k); c=m.group(1)
    return ('Rev1w-' if '|mrev|' in k else '')+c
def fl(x):
    try: return float(x)
    except: return 0.0
g=collections.OrderedDict()
for r in rows: g.setdefault(cell(r['key']),[]).append(r)
def hsplit(v):
    h=b=t=p=0; vs=0; sf=0; other={}
    for tok in v.split(' '):
        m=re.match(r'(.*)=(\d+)/(\d+)$',tok)
        if not m: continue
        n,d=m.group(1),int(m.group(2))
        if n.endswith('/13'): h+=d
        elif n.endswith('/14'): b+=d
        elif n.endswith('/15'): t+=d
        elif n.startswith('proj:'):
            p+=d; other[n]=other.get(n,0)+d
    return h,b,t,p,other
out=[]
for c,rs in g.items():
    n=len(rs); wins=[r for r in rs if r['result']=='WIN']
    kt=[fl(r['sec']) for r in wins]
    left=[fl(r['bossLeft']) for r in rs if r['result']!='WIN']
    fd=[fl(r['firstDeathSec']) for r in rs if r['firstDeathSec'] not in('-','')]
    dth=[fl(r['deaths']) for r in rs]; nn=rs[0]['n']
    hh=bb=tt=pp=0; oth={}
    cls=collections.Counter(); wep=collections.Counter(); dot=0; tot=0; end=[]
    for r in rs:
        h,b,t,p,o=hsplit(hit.get(r['key'],''))
        hh+=h;bb+=b;tt+=t;pp+=p
        for k,v in o.items(): oth[k]=oth.get(k,0)+v
        d=dl[r['key']]
        for tok in d['classDmg'].split(' '):
            a,_,v=tok.partition('='); cls[a]+=int(v or 0)
        for tok in d['weaponDmg'].split(' '):
            a,_,v=tok.partition('=')
            if a: wep[a]+=int(v or 0)
        mx=fl(r['bossMax']); lf=fl(r['bossLeft']) if r['result']!='WIN' else 0
        dot+=max(0,mx-lf-fl(r['dmgBoss'])-fl(r['dmgUnattr'])); tot+=mx-lf
        by=d['deathBy']
    tk=hh+bb+tt+pp
    print('== %s  n_levies=%s  wins %d/%d'%(c,nn,len(wins),n))
    print('  kill s (wins): %s ; boss left (losses) k: %s'%(['%.0f'%x for x in kt],['%.1f'%(x/1000) for x in left]))
    print('  first levy death s: %s ; deaths: %s/%s'%(['%.1f'%x for x in fd],[int(x) for x in dth],nn))
    print('  hits taken dmg: head %d body %d tail %d proj %d  (head%% %.0f body%% %.0f tail%% %.0f proj%% %.1f)'%(hh,bb,tt,pp,100*hh/max(tk,1),100*bb/max(tk,1),100*tt/max(tk,1),100*pp/max(tk,1)))
    print('  proj by name:',sorted(oth.items(),key=lambda x:-x[1])[:4])
    tc=sum(cls.values())
    print('  class dmg share (P pierce,X explosive,D DoT-weapon):',{k:'%.0f%%'%(100*v/tc) for k,v in cls.most_common()}, ' uncredited(DoT/etc) share of boss dmg %.0f%%'%(100*dot/max(tot,1)))
    print('  top weapons:',[(k,v) for k,v in wep.most_common(6)])
    kills=collections.Counter()
    for r in rs:
        for tok in dl[r['key']]['deathBy'].split(' '):
            a,_,v=tok.partition('=')
            if a: kills[a]+=int(v)
    print('  killers:',dict(kills))

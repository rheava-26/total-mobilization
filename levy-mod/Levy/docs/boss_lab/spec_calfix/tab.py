import sys,csv,collections
# usage: tab.py file.tsv [...]  -> per (boss,mode,n,cmd) aggregate
cols=['result','seed','boss','mode','n','tgtLevyPct','tgtPlayerPct','addTgtLevyPct','addTgtPlayerPct','deaths','sec','bossLeft','bossMax']
for f in sys.argv[1:]:
    rows=list(csv.DictReader(open(f),delimiter='\t'))
    print("==",f,len(rows))
    g=collections.OrderedDict()
    for r in rows: g.setdefault((r['boss'],r['mode'],r['n'],r.get('key','').split('|c')[-1] if '|c' in r.get('key','') else 'near'),[]).append(r)
    for k,v in g.items():
        def m(c): 
            xs=[float(x[c]) for x in v if x.get(c) not in (None,'','-')]
            return sum(xs)/len(xs) if xs else float('nan')
        res=collections.Counter(x['result'] for x in v)
        print(k,len(v),dict(res),"P%%=%.0f L%%=%.0f addP=%.0f addL=%.0f deaths=%.1f sec=%.0f"%(m('tgtPlayerPct'),m('tgtLevyPct'),m('addTgtPlayerPct'),m('addTgtLevyPct'),m('deaths'),m('sec')), [x['tgtPlayerPct'] for x in v])

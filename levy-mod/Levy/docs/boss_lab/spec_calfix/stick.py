import sys,re,collections
# per fight key: samples (every 30 ticks) with tgt=player, and samples with boss within 6 tiles of commander; longest run of consecutive near-commander samples
for f in sys.argv[1:]:
    d=collections.defaultdict(list)
    for l in open(f):
        c=l.rstrip('\n').split('\t')
        if len(c)<10: continue
        m=re.search(r'cmdDist=([\d.]+)t',l); t=re.search(r'tgt=\d+\((\w+)\)',l); 
        if not m or not t: continue
        d[c[0]].append((float(m.group(1)),t.group(1)))
    for k,v in d.items():
        n=len(v); P=sum(1 for x in v if x[1]=='player'); nc=sum(1 for x in v if x[0]<6)
        run=best=0
        for x in v:
            run=run+1 if x[0]<6 else 0; best=max(best,run)
        print(f.split('/')[-1],k.split('|')[1:3],"samples=%d tgtPlayer=%d (%.0f%%) bossWithin6tOfCmd=%d (%.0f%%) longestRun=%d samples (%ds)"%(n,P,100*P/n,nc,100*nc/n,best,best*30//60))

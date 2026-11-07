import csv,re,sys
def load(f):
    rows=list(csv.reader(open(f),delimiter='\t')); h=rows[0]; ix={k:i for i,k in enumerate(h)}
    d={}
    for r in rows[1:]:
        if len(r)<len(h)-3: continue
        m=re.search(r'cwCalamityMod/(\w+)',r[0]); 
        if not m: continue
        d[m.group(1)]=float(r[ix['dmgBoss']])/float(r[ix['sec']])/int(r[ix['n']])
    return d
B=load(sys.argv[1]); A=load(sys.argv[2])
for k in sorted(A,key=lambda k:A[k]-B.get(k,0)):
    a,b=A[k],B.get(k,0)
    print(f"{k:28s} before={b:9.2f} after={a:9.2f}  {'REGRESSION' if a<0.7*b and b>1 else ''}")

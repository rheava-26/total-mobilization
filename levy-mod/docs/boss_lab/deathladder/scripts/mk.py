import sys
cap=int(sys.argv[2]); seeds=[int(x) for x in sys.argv[3].split(',')]; cells=sys.argv[4].split(',')
rows=[]
for c in cells:
    mode='mdeath'; tok=''
    if c.startswith('R'): mode='mrev'; c=c[1:]
    ok='dlok=1' if c.endswith('ok') else ''
    t=c[0]
    for s in seeds:
        rows.append("fight\teow\t%s\t%d\t%d\tcmd=near\tn=cp\tgear=mod\tgrade=lv\tl3=1\torder=follow\tarena=a3\tdl=%s%s\n"%(mode,s,cap,t,('\t'+ok) if ok else ''))
open('/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/dl/specs/%s.tsv'%sys.argv[1],'w').write(''.join(rows))

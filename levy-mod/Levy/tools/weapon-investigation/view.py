import sys,re
t=open("/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/cost_tables.txt").read()
stages=re.split(r"\n######## STAGE ",t)
want=sys.argv[1]; kind=sys.argv[2] if len(sys.argv)>2 else "COST-primary"
for st in stages:
    if not st.startswith(want): continue
    print("STAGE",st.splitlines()[0])
    cur=None
    for l in st.splitlines()[1:]:
        if l.startswith("-- class"): cur=("COST" in l and kind.startswith("COST")) or ("TIER" in l and kind.startswith("TIER")) or ("CURRENT" in l and kind.startswith("CUR"));
        if cur: print(l[:150])

#!/usr/bin/env python3
"""q.py FOLDED  callees <substr> [depth]   -> samples under frames matching substr, broken down by next frames (aggregated, any occurrence)
   q.py FOLDED  top [n]                     -> exclusive top
   q.py FOLDED  path <substr>               -> most common callers chain of matching frames
"""
import sys, collections, re
f = sys.argv[1]; cmd = sys.argv[2]
rows = []
for l in open(f):
    st, c = l.rsplit(" ", 1); rows.append((st.split(";"), int(c)))
total = sum(c for _, c in rows)
def short(s): return s if len(s) < 140 else s[:140]
def skip(fr):  # noise frames from hooks chains
    return fr.startswith("dynamicClass.") or fr.startswith("Terraria.ModLoader.Core") or "SyncProxy" in fr
if cmd == "callees":
    sub = sys.argv[3]; depth = int(sys.argv[4]) if len(sys.argv) > 4 else 1
    agg = collections.Counter(); tot = 0
    for st, c in rows:
        idx = [i for i, x in enumerate(st) if sub in x]
        if not idx: continue
        i = idx[-1] if "--last" in sys.argv else idx[0]
        tot += c
        rest = [x for x in st[i + 1:] if not skip(x)]
        key = " > ".join(short(x.split("(")[0]) for x in rest[:depth]) or "<self>"
        agg[key] += c
    print(f"{sub}: {tot} samples ({100*tot/total:.1f}% of {total})")
    for k, v in agg.most_common(int(sys.argv[5]) if len(sys.argv) > 5 else 25): print(f"  {100*v/total:5.1f}% {v:6d}  {k}")
elif cmd == "top":
    agg = collections.Counter()
    for st, c in rows: agg[st[-1]] += c
    for k, v in agg.most_common(int(sys.argv[3]) if len(sys.argv) > 3 else 40): print(f"{100*v/total:5.1f}% {v:6d} {short(k)}")
elif cmd == "callers":
    sub = sys.argv[3]; agg = collections.Counter(); tot = 0
    for st, c in rows:
        idx = [i for i, x in enumerate(st) if sub in x]
        if not idx: continue
        tot += c
        pre = [x for x in st[:idx[0]] if not skip(x)]
        agg[" < ".join(short(x.split("(")[0]) for x in reversed(pre[-3:]))] += c
    print(f"{sub}: {tot} ({100*tot/total:.1f}%)")
    for k, v in agg.most_common(12): print(f"  {100*v/total:5.1f}% {k}")

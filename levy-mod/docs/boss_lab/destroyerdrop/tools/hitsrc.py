#!/usr/bin/env python3
"""hitsrc.py RUN_DIR CELL : damage taken by levies per fight by source (contact:<npc> / proj:<name>), from the run's out.tsv.hit.tsv; mean damage and hits per fight, top sources."""
import glob, re, sys, collections
run, cell = sys.argv[1], sys.argv[2]
HIT = re.compile(r"(contact|proj):(.*?)(?:/(-?\d+))?=(\d+)/(\d+)")
dmg = collections.Counter(); hits = collections.Counter(); n = 0
for f in glob.glob(run + '/jobs/*/try*/out.tsv.hit.tsv'):
    for l in open(f, errors='replace'):
        k, _, v = l.rstrip('\n').partition('\t')
        if not re.search(r"\|t" + cell + r"(\||$)", k): continue
        n += 1
        for kind, nm, ident, d, h in HIT.findall(v):
            dmg[(kind, nm)] += int(d); hits[(kind, nm)] += int(h)
print(f"{run} cell {cell}: {n} fights")
for (kind, nm), d in dmg.most_common(14):
    print(f"  {kind:7s} {nm:40s} dmg/fight {d / n:8.0f}  hits/fight {hits[(kind, nm)] / n:6.1f}  per hit {d / max(1, hits[(kind, nm)]):6.0f}")

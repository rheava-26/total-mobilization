#!/usr/bin/env python3
"""parity.py LABEL=RUN_DIR ...: pools the .parity.tsv files of live-pose parity runs (LEVY_HITPARITY_LIVE=1) per NPC: sampled positions, positions where a player is hurt, and the rule in force's
mismatches (levy hurt where a player is not / not hurt where a player is / other slot or multiplier). Run with LEVY_CONTACTPARITY=0 the rule in force is the old rectangle."""
import csv, glob, sys
from collections import defaultdict
print("| arm | npc | samples | positions | player hurt at | levy-only | player-only | slot/mult |\n|---|---|---|---|---|---|---|---|")
for arg in sys.argv[1:]:
    label, run = arg.split("=", 1); t = defaultdict(lambda: [0] * 6)
    for p in glob.glob(run + "/jobs/*/try*/out.tsv.parity.tsv"):
        for r in csv.DictReader(open(p), delimiter="\t"):
            a = t[r["npc"]]
            for i, k in enumerate(["samples", "positions", "playerHurts", "ruleLevyOnly", "ruleLevyMissed", "ruleSlotMult"]): a[i] += int(r[k])
    tot = [0] * 6
    for n, a in sorted(t.items(), key=lambda kv: -kv[1][1]):
        if a[1] < 20000: continue
        print(f"| {label} | {n} | " + " | ".join(map(str, a[:3] + a[3:])) + " |")
    for a in t.values():
        for i in range(6): tot[i] += a[i]
    print(f"| {label} | ALL | " + " | ".join(map(str, tot)) + " |")

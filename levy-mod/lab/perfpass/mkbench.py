#!/usr/bin/env python3
"""mkbench.py  -- writes the perf-pass benchmark specs (raw harness fight lines) into specs/, with absolute army paths for this checkout.
bench.tsv : the BENCHMARK. Her real armies (40 regulars, her gear) in three busy endgame scenes, the army kept alive (dr=0.9: every hit on a levy cut by 90%)
            so the population and the work stay the same for the whole measured span: Exo Mechs (Ares first, her 11-21 17:19 army), Providence (projectile storm,
            her 11-21 12:58 Polterghast army), Polterghast (the same army). 40 s each, Master+Death, arena big, commander far, order Free Rein.
fights.tsv: her fight scenarios as they really go (no dr): the same three scenes, 60 s cap, the army can die."""
import os
H = os.path.dirname(os.path.abspath(__file__))
A = lambda n: os.path.join(H, "armies", n)
SCENES = [("exoares", "eve_exo.json"), ("prov", "eve_polter.json"), ("polter", "eve_polter.json")]
def line(boss, army, seed, ticks, extra):
    return "\t".join(["fight", boss, "mdeath", str(seed), str(ticks), "cmd=far", "order=free", "arena=big", "lv=" + A(army)] + extra)
os.makedirs(os.path.join(H, "specs"), exist_ok=True)
with open(os.path.join(H, "specs", "bench.tsv"), "w") as f:
    for boss, army in SCENES:
        for seed in (1, 2): f.write(line(boss, army, seed, 40 * 60, ["dr=0.9", "meta=bench_" + boss]) + "\n")
with open(os.path.join(H, "specs", "fights.tsv"), "w") as f:
    for boss, army in SCENES:
        for seed in (1, 2): f.write(line(boss, army, seed, 60 * 60, ["meta=fight_" + boss]) + "\n")
with open(os.path.join(H, "specs", "smoke.tsv"), "w") as f:
    for boss, army in SCENES: f.write(line(boss, army, 1, 15 * 60, ["dr=0.9", "meta=smoke_" + boss]) + "\n")

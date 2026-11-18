#!/usr/bin/env python3
"""mkgc.py: the gear-cost lab specs (run mkarmy.py gearcost first). gc_cal.tsv: Calamity loaded, her 40 levies in vanilla-swapped gear and in her own gear; gc_van.tsv: no Calamity, the vanilla-swapped 40.
Run each with LEVY_GEARCOST=1 LEVY_CROWD=1, one at a time; lab/lagsweep/gearcost.py RUN [RUN ...] prints the per-levy table."""
from pathlib import Path
L = Path(__file__).resolve().parent
def line(army, seed, meta): return "\t".join(["fight", "solarp", "mdeath", str(seed), "1800", "cmd=near", "order=follow", "arena=a3", f"lv={L}/{army}.json", "pw=30", f"meta={meta}"])
cal = ["# gear-cost lab, Calamity loaded: her 40 levies in vanilla-swapped gear and in her own gear"] + [line(a, s, m) for a, m in [("eve40v", "cal_vgear"), ("eve40", "cal_evegear")] for s in (1, 2)]
van = ["# mods: vanilla", "# gear-cost lab, no Calamity: the same 40 levies in vanilla-swapped gear"] + [line("eve40v", s, "van_vgear") for s in (1, 2)]
(L / "gc_cal.tsv").write_text("\n".join(cal) + "\n"); (L / "gc_van.tsv").write_text("\n".join(van) + "\n")
print("ok")

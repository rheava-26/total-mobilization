#!/usr/bin/env python3
"""sweeplist.py: the LEVY_ARMYSWEEP_ONLY list for the lagsweep cost sweep: every weapon of the army sweep's list (Eve's army) plus the weapon and ammo pairs her 11-17 perf log flagged
(Chlorophyte Bullet, Sprouting Arrow split, Astral Probe rounds via Starspawn Helix Staff, Cosmic Tentacle via Apathanull, Plaguenade bees, Dreadmine, Pulse Turret, Nebula Arcanum subshots)."""
import re
from pathlib import Path
src = (Path(__file__).resolve().parents[2] / "Levy" / "Common" / "Testing" / "IdentityWorldCheck.ArmySweep.cs").read_text()
names = re.findall(r'"([^"]+)"', re.search(r"ArmyList =\s*\{(.*?)\};", src, re.S).group(1))
extra = ["Realm Ravager@Chlorophyte Bullet", "Vernal Bolter@Sprouting Arrow", "Malevolence@Sprouting Arrow", "Stake Launcher@Stake", "Nebula Arcanum", "Sniper Rifle@Musket Ball", "Sniper Rifle@Chlorophyte Bullet"]
print(";".join([n for n in names if n not in ("Sniper Rifle", "Vernal Bolter")] + extra))

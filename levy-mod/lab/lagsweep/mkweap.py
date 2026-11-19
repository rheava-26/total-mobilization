#!/usr/bin/env python3
"""mkweap.py: the lag4 weapon experiment's armies (writes next to this script) and its spec.
  eve45_w_<slug>.json   eve45 (her 45 levies, her armor, accessories and stations) with every levy holding one weapon and no sidearm, for each of the heaviest weapons of the per-weapon cost sweep
                        (LAG_FINDINGS section 3: Plaguenade, Terra Blade, Apathanull, System Bane, The Syringe, Radiant Star, Shard of Antumbra, Havoc's Breath).
  l4_weapons.tsv        one Solar Pillar fight per army and seed (pw=45, 1800 ticks, dr=0.95 so the 45 levies stay alive and engaged), her own army as the control, ns as given.
Usage: mkweap.py [SEEDS]   (default 2)"""
import json, sys
from pathlib import Path
HERE = Path(__file__).resolve().parent
base = json.load(open(HERE / "eve45.json"))
# (slug, item, class letter, ammo)
WEAP = [("plaguenade", "CalamityMod/Plaguenade", "T", None), ("terrablade", "Terra Blade", "E", None), ("apathanull", "CalamityMod/Apathanull", "M", None),
        ("systembane", "CalamityMod/SystemBane", "T", None), ("syringe", "CalamityMod/TheSyringe", "T", None), ("radiantstar", "CalamityMod/RadiantStar", "T", None),
        ("antumbra", "CalamityMod/ShardofAntumbra", "T", None), ("havoc", "CalamityMod/HavocsBreath", "R", "Gel")]
for slug, item, cls, ammo in WEAP:
    lv = []
    for l in base["levies"]:
        l = dict(l); l["weapon"] = item; l["weaponName"] = item.split("/")[-1]; l["cls"] = cls; l.pop("side", None)
        if ammo: l["ammo"] = ammo
        else: l.pop("ammo", None)
        lv.append(l)
    d = dict(base); d["levies"] = lv; d["note"] = f"eve45 with every levy holding {item} and no sidearm (lag4 weapon experiment)"
    (HERE / f"eve45_w_{slug}.json").write_text(json.dumps(d, indent=1))
seeds = int(sys.argv[1]) if len(sys.argv) > 1 else 2
rows = []
for s in range(1, seeds + 1):
    for slug, path in [("her_army", HERE / "eve45.json")] + [(w[0], HERE / f"eve45_w_{w[0]}.json") for w in WEAP]:
        rows.append("\t".join(["fight", "solarp", "mdeath", str(s), "1800", "cmd=near", "order=follow", "arena=a3", f"lv={path}", "pw=45", "dr=0.95", f"meta={slug}"]))
(HERE / "l4_weapons.tsv").write_text("\n".join(rows) + "\n")
print(len(WEAP), "armies,", len(rows), "fights")

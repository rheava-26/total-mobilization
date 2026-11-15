#!/usr/bin/env python3
"""Rigorous Destroyer / Aquatic Scourge battery (Master+Death). ONE fixed baseline, only the RNG seed varies; then one variable at a time.
  hm_battery.py OUT_DIR [--bosses destroyer,aqua] [--pair mdeath] [--base-fights 40] [--cell-fights 20] [--cells baseline,n12,n24,n36,nowings,flat]

Baseline (per boss): the on-tier army of mkgear.make_army(boss, 'on', seed=1, n=34) written ONCE and reused by every fight (same weapons, ammo, armor, accessories, 400 HP,
8 healing potions, full buffs, postwof stations), arena a3 (a3@ocean for Aquatic Scourge: the ocean strip so Calamity does not enrage it), cmd=near, order=follow, the spectator
stand-in commander, cap 300 s (Aquatic Scourge 420 s). Cells change exactly one thing against it:
  n12 / n24 / n36   army size (mkgear.make_army with that n, same recipe, seed 1)
  nowings           the baseline army with Angel Wings removed from every levy (Cloud in a Bottle stays)
  flat              the baseline army in the same arena without the platform rows (token a3flat)
meta=hmb_<boss>_<cell>_<pair> on every fight line; hm_bcollect.py maps recordings back through it. Also writes GEAR.md (the baseline loadout, per class)."""
import argparse, json, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE)); sys.path.insert(0, str(HERE.parents[1] / "docs" / "boss_lab" / "battery_2026-11" / "scripts"))
import hm_mk, mkgear, yaml

ap = argparse.ArgumentParser()
ap.add_argument("out"); ap.add_argument("--bosses", default="destroyer,aqua"); ap.add_argument("--pair", default="mdeath")
ap.add_argument("--base-fights", type=int, default=40); ap.add_argument("--cell-fights", type=int, default=20); ap.add_argument("--cells", default="baseline,n12,n24,n36,nowings,flat")
ap.add_argument("--n", type=int, default=34)
a = ap.parse_args()
out = Path(a.out); (out / "armies").mkdir(parents=True, exist_ok=True)
LAB = HERE.parent / "presets"
pot = yaml.safe_load((LAB / "potions.yaml").read_text())["best"]; sta = yaml.safe_load((LAB / "stations.yaml").read_text())["postwof"]
CAP = {"destroyer": 300, "aqua": 420}
lines = []; gear = ["# Baseline loadout (Destroyer / Aquatic Scourge battery)\n", f"{a.n} levies, 400 HP (15 life crystals), 200 mana, 8 x Greater Healing Potion, buffs: {', '.join(pot['buffs'])}. Stations: {', '.join(sta['stations'])}{' + honey' if sta['honey'] else ''}.\n"]
for boss in a.bosses.split(","):
    base = mkgear.make_army(boss, "on", 1, n=a.n)
    def dump(name, army, acc_filter=None):
        lv = army["levies"]
        if acc_filter:
            lv = [dict(l, acc=[x for x in l["acc"] if acc_filter(x)]) for l in lv]
        f = out / "armies" / f"{boss}_{name}.json"
        f.write_text(json.dumps(dict(accSlots=5, honey=bool(sta["honey"]), stations=sta["stations"], stationLevel=2, buffs=pot["buffs"], levies=lv)))
        return f
    arena0 = hm_mk.BOSSES[boss][2]
    cells = {"baseline": (dump("baseline", base), arena0, a.n), "nowings": (dump("nowings", base, lambda x: x != "Angel Wings"), arena0, a.n),
             "flat": (dump("flat", base), arena0.replace("a3", "a3flat", 1), a.n)}
    for n in (12, 24, 36): cells[f"n{n}"] = (dump(f"n{n}", mkgear.make_army(boss, "on", 1, n=n)), arena0, n)
    gear.append(f"\n## {hm_mk.BOSSES[boss][4]} (route tier {base['T']:.2f}, arena {arena0})\n")
    for cls, nm in zip("RMES", ("Ranged", "Magic", "Melee", "Summoner")):
        ls = [l for l in base["levies"] if l["cls"] == cls]
        if not ls: continue
        gear.append(f"- {nm} x{len(ls)}: armor {' / '.join(ls[0]['armor'])}; accessories {', '.join(ls[0]['acc'])}\n")
        for l in ls: gear.append(f"  - {l['weapon']}" + (f" with {l['ammo']}" if l["ammo"] else "") + "\n")
    for cell in a.cells.split(","):
        f, arena, n = cells[cell]
        nf = a.base_fights if cell == "baseline" else a.cell_fights
        for sd in range(1, nf + 1):
            tok = ["cmd=near", "order=follow", f"arena={arena}", f"lv={f}", f"meta=hmb_{boss}_{cell}_{a.pair}"] + hm_mk.BOSSES[boss][3]
            lines.append("\t".join(["fight", boss, a.pair, str(sd), str(CAP[boss] * 60)] + tok))
(out / "spec.tsv").write_text("\n".join(lines) + "\n"); (out / "GEAR.md").write_text("".join(gear))
print(len(lines), "fights ->", out / "spec.tsv")

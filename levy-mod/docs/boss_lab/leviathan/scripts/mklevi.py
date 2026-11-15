#!/usr/bin/env python3
"""Writes the armies and a raw levylab fight spec (.tsv) for the Leviathan / Anahita lab (and the regression bosses).
  mklevi.py OUT_DIR --bosses levi --pairs rev,mdeath --kits mid,on --seeds 1-3 --cap 300 [--arena a3@ocean]
Same army recipe as the 2026-11 battery (30 levies: 10 ranged, 10 magic, 5 melee, 5 summoner, 400 HP, 8 healing potions, full buffs, postwof stations, cmd=near, order=follow), just a post-Plantera
progression for the Leviathan (Boss Checklist 12.8): on = everything obtainable before it, mid = one tier below. pairs: rev = Expert + Revengeance, mdeath = Master + Death."""
import argparse, json, sys
from pathlib import Path
BAT = Path(__file__).resolve().parents[2] / "battery_2026-11" / "scripts"
sys.path.insert(0, str(BAT))
import mkgear
mkgear.PROG.update(levi=12.8, fish=14.0, destroyer=10.0, ds=2.0, eow=3.0)
import yaml
ap = argparse.ArgumentParser()
ap.add_argument("out"); ap.add_argument("--bosses", default="levi"); ap.add_argument("--pairs", default="rev,mdeath"); ap.add_argument("--kits", default="mid,on")
ap.add_argument("--seeds", default="1-3"); ap.add_argument("--cap", type=int, default=300); ap.add_argument("--n", type=int, default=30); ap.add_argument("--arena", default="")
ap.add_argument("--stations", default="postwof"); ap.add_argument("--tok", action="append", default=[], help="extra harness token (all fights)")
a = ap.parse_args()
ARENA = {"levi": "a3@ocean", "fish": "a3@ocean", "destroyer": "a3", "ds": "a3", "eow": "a3"}
out = Path(a.out); (out / "armies").mkdir(parents=True, exist_ok=True)
lo, _, hi = a.seeds.partition("-"); seeds = list(range(int(lo), int(hi or lo) + 1))
LAB = Path(__file__).resolve().parents[4] / "lab" / "presets"
pot = yaml.safe_load((LAB / "potions.yaml").read_text())["best"]; sta = yaml.safe_load((LAB / "stations.yaml").read_text())[a.stations]
lines = []
for boss in a.bosses.split(","):
    for kit in a.kits.split(","):
        for sd in seeds:
            army = mkgear.make_army(boss, kit, sd, n=a.n)
            f = out / "armies" / f"{boss}_{kit}_{sd}.json"
            f.write_text(json.dumps(dict(accSlots=5, honey=bool(sta["honey"]), stations=sta["stations"], stationLevel=2, buffs=pot["buffs"], levies=army["levies"])))
            for pair in a.pairs.split(","):
                tok = ["cmd=near", "order=follow", f"arena={a.arena or ARENA.get(boss, 'a3')}", f"lv={f}", f"meta={boss}_{kit}_{pair}_T{army['T']:.2f}"] + a.tok
                lines.append("\t".join(["fight", boss, pair, str(sd), str(a.cap * 60)] + tok))
(out / "spec.tsv").write_text("\n".join(lines) + "\n")
print(len(lines), "fights ->", out / "spec.tsv")

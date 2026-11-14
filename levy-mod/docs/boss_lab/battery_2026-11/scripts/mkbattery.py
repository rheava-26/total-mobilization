#!/usr/bin/env python3
"""Writes the army files and a raw levylab fight spec (.tsv) for a slice of the battery.
  mkbattery.py OUT_DIR --bosses twins,prime --pairs rev,mdeath --kits mid,on --imm shared --seeds 1-3 --cap 300 [--n 30]
pairs: rev = Expert + Revengeance, mdeath = Master + Death.  imm: shared (the product) | local | perlevy | static.
Each fight line carries meta=<boss>_<kit>_<pair>_<imm> so analyze.py can map rows back."""
import argparse, json, sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import mkgear

ap = argparse.ArgumentParser()
ap.add_argument("out"); ap.add_argument("--bosses", default=",".join(mkgear.PROG)); ap.add_argument("--pairs", default="rev,mdeath"); ap.add_argument("--kits", default="mid,on")
ap.add_argument("--imm", default="shared"); ap.add_argument("--seeds", default="1-3"); ap.add_argument("--cap", type=int, default=300); ap.add_argument("--n", type=int, default=30)
ap.add_argument("--tok", action="append", default=["plant:spec=0"], help="boss:token extra harness token for one boss (default: plant:spec=0)"); ap.add_argument("--stations", default="postwof"); ap.add_argument("--buffs", default="")
a = ap.parse_args()
out = Path(a.out); (out / "armies").mkdir(parents=True, exist_ok=True)
lo, _, hi = a.seeds.partition("-"); seeds = list(range(int(lo), int(hi or lo) + 1))
import yaml
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
                for imm in a.imm.split(","):
                    tok = ["cmd=near", "order=follow", "arena=a3", f"lv={f}", f"imm={imm}", f"meta={boss}_{kit}_{pair}_{imm}_T{army['T']:.2f}"]
                    tok += [t.split(":", 1)[1] for t in a.tok if t.split(":", 1)[0] == boss]
                    lines.append("\t".join(["fight", boss, pair, str(sd), str(a.cap * 60)] + tok))
(out / "spec.tsv").write_text("\n".join(lines) + "\n")
print(len(lines), "fights ->", out / "spec.tsv")

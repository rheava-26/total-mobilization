#!/usr/bin/env python3
"""Writes armies and a raw levylab fight spec (.tsv) for the headless replay recordings (hmreplays).
  hm_mk.py OUT_DIR --bosses destroyer,aqua --pairs rev,mdeath --seeds 1-3 [--mid-seeds 4] [--cap 300]

Army recipe = the 2026-11 boss battery's (gear route table + mkgear.py: 34% ranged, 34% magic, 16% melee, rest summoners, 400 HP, 8 healing potions,
full buffs, postwof stations, cmd=near, order=follow). Differences: the levy count is the REAL campaign count for that boss (LevyTierMath.Own with the
shipped defaults: 14 at the start, 28 after Wall of Flesh, 34 at the end of Hardmode, 40 after Moon Lord; a boss beaten is worth its tier's weight x factor;
Calamity bosses are worth the tier mean), and the kit is `on` (every item obtainable before the boss) for --seeds and `mid` (one tier below) for --mid-seeds.
pairs: rev = Expert + Revengeance, mdeath = Master + Death. One fight line per (boss, pair, seed); meta=<slug>_<kit>_<pair>_n<N>_T<tier>."""
import argparse, json, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
BAT = HERE.parents[1] / "docs" / "boss_lab" / "battery_2026-11" / "scripts"
sys.path.insert(0, str(BAT))
import mkgear
import yaml

# slug -> (harness boss token, progression (Boss Checklist, same scale as item_tiers.tsv), arena, extra harness tokens, title)
BOSSES = {
    "destroyer": ("destroyer", 10.0, "a3", [], "The Destroyer"),
    "aqua": ("aqua", 9.5, "a3@ocean", [], "Aquatic Scourge"),
    "wof": ("wof", 7.0, "a3uw", [], "Wall of Flesh"),
    "qslime": ("qslime", 8.0, "a3", [], "Queen Slime"),
    "twins": ("twins", 9.0, "a3", [], "The Twins"),
    "prime": ("prime", 11.0, "a3", [], "Skeletron Prime"),
    "brim": ("brim", 10.5, "a3@crag", [], "Brimstone Elemental"),
    "cryo": ("cryo", 8.5, "a3", [], "Cryogen"),
    "cal": ("cal", 11.7, "a3", [], "Calamitas Clone"),
    "plant": ("plant", 12.0, "a3e@jungle", ["spec=0"], "Plantera"),
    "levi": ("levi", 12.8, "a3@ocean", [], "Leviathan and Anahita"),
    "aureus": ("aureus", 12.81, "a3", [], "Astrum Aureus"),
    "golem": ("golem", 13.0, "a3e@temple", [], "Golem"),
    "fish": ("fish", 14.0, "a3@ocean", [], "Duke Fishron"),
    "pbg": ("pbg", 14.5, "a3@jungle", [], "The Plaguebringer Goliath"),
    "eol": ("eol", 15.0, "a3", [], "Empress of Light"),
    "cultist": ("cultist", 15.5, "a3", [], "Lunatic Cultist"),
    "ravager": ("ravager", 16.5, "a3", [], "Ravager"),
    "ml": ("ml", 18.0, "a3", [], "Moon Lord"),
    "crab": ("crab", 2.7, "a3", [], "Crabulon"),
    "hive": ("hive", 3.98, "a3", [], "Hive Mind"),
    "perf": ("perf", 3.99, "a3", [], "The Perforators"),
    "deer": ("deer", 6.0, "a3", ["zone=snow"], "Deerclops"),
}
CAPS = {"aqua": 420, "ml": 420, "levi": 360, "pbg": 360, "plant": 360, "ravager": 360, "golem": 360}   # game seconds; default --cap. A fight ends early on a win or a wipe
mkgear.PROG.update({k: v[1] for k, v in BOSSES.items()})

# ---- real levy count (LevyTierMath.Own with the shipped defaults) ----
START, PRE, HM, POST = 14, 28, 34, 40
PRE_W = {"kslime": 1, "eye": 2, "eowbrain": 2, "qb": 1, "skel": 2, "deer": 1, "wof": 7}                      # sums to 16
HM_W = {"qslime": 1, "twins": 2, "destroyer": 2, "prime": 2, "plant": 3, "golem": 3, "eol": 1, "fish": 1, "cultist": 2, "ml": 3}  # sums to 20
# bosses beaten BEFORE the fight, by campaign order (Boss Checklist progression). tier 0 = pre-hardmode, 1 = hardmode. weight 0 = a modded boss (worth its tier's mean).
ORDER = [("kslime", 1.0, 0), ("ds", 1.6, 0), ("eye", 2.0, 0), ("crab", 2.7, 0), ("eowbrain", 3.0, 0), ("hive", 3.98, 0), ("perf", 3.99, 0), ("qb", 4.0, 0), ("skel", 5.0, 0),
         ("deer", 6.0, 0), ("sg", 6.7, 0), ("wof", 7.0, 0), ("qslime", 8.0, 1), ("cryo", 8.5, 1), ("twins", 9.0, 1), ("aqua", 9.5, 1), ("destroyer", 10.0, 1), ("brim", 10.5, 1),
         ("prime", 11.0, 1), ("cal", 11.7, 1), ("plant", 12.0, 1), ("levi", 12.8, 1), ("aureus", 12.81, 1), ("golem", 13.0, 1), ("fish", 14.0, 1), ("pbg", 14.5, 1), ("eol", 15.0, 1),
         ("cultist", 15.5, 1), ("ravager", 16.5, 1), ("ml", 18.0, 1)]


def levy_count(prog):
    pre_mean, hm_mean = sum(PRE_W.values()) / len(PRE_W), sum(HM_W.values()) / len(HM_W)
    f_pre, f_hm = (PRE - START) / sum(PRE_W.values()), (HM - PRE) / sum(HM_W.values())
    v = float(START)
    for name, p, tier in ORDER:
        if p >= prog - 1e-9: continue
        if tier == 0: v += PRE_W.get(name, pre_mean) * f_pre
        else: v += HM_W.get(name, hm_mean) * f_hm
    cap = PRE if prog <= 7.0 else HM   # the stage cap: the world is pre-Hardmode until Wall of Flesh falls, Hardmode until Moon Lord
    return max(START, min(int(v + 0.5 + 1e-3), cap, 60))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out"); ap.add_argument("--bosses", default=",".join(BOSSES)); ap.add_argument("--pairs", default="rev,mdeath")
    ap.add_argument("--seeds", default="1-3"); ap.add_argument("--mid-seeds", default="4"); ap.add_argument("--cap", type=int, default=0, help="fight cap in game seconds for every boss (default: CAPS per boss, else 300)")
    ap.add_argument("--n", type=int, default=0, help="override the levy count"); ap.add_argument("--stations", default="postwof"); ap.add_argument("--tok", action="append", default=[], help="extra harness token on every fight")
    a = ap.parse_args()
    out = Path(a.out); (out / "armies").mkdir(parents=True, exist_ok=True)
    LAB = HERE.parent / "presets"
    pot = yaml.safe_load((LAB / "potions.yaml").read_text())["best"]; sta = yaml.safe_load((LAB / "stations.yaml").read_text())[a.stations]

    def seeds(s):
        if not s: return []
        lo, _, hi = s.partition("-"); return list(range(int(lo), int(hi or lo) + 1))
    plan = [("on", sd) for sd in seeds(a.seeds)] + [("mid", sd) for sd in seeds(a.mid_seeds)]
    lines = []
    for slug in a.bosses.split(","):
        tok0, prog, arena, extra, title = BOSSES[slug]
        n = a.n or levy_count(prog)
        for kit, sd in plan:
            army = mkgear.make_army(slug, kit, sd, n=n)
            f = out / "armies" / f"{slug}_{kit}_{sd}.json"
            f.write_text(json.dumps(dict(accSlots=5, honey=bool(sta["honey"]), stations=sta["stations"], stationLevel=2, buffs=pot["buffs"], levies=army["levies"])))
            for pair in a.pairs.split(","):
                tok = ["cmd=near", "order=follow", f"arena={arena}", f"lv={f}", f"meta={slug}_{kit}_{pair}_n{n}_T{army['T']:.2f}"] + extra + a.tok
                lines.append("\t".join(["fight", tok0, pair, str(sd), str((a.cap or CAPS.get(slug, 300)) * 60)] + tok))
        print(f"{slug}: {n} levies, T on={mkgear.threshold(slug, 'on'):.2f}", file=sys.stderr)
    (out / "spec.tsv").write_text("\n".join(lines) + "\n")
    print(len(lines), "fights ->", out / "spec.tsv")


if __name__ == "__main__":
    main()

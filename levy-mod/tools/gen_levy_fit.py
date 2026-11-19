#!/usr/bin/env python3
"""gen_levy_fit.py: builds Levy/Common/Weapons/LevyFit.tsv, the measured levy-vs-player table the armory's Balanced ranking reads (WeaponRank, LevyFit.cs).

Sources (levy vs a stand-in player, same weapon, same dummy, 600 ticks):
  docs/ARMYSWEEP_DPS.tsv   her army's weapons (levy_dps_idle = levy damage per second)
  docs/ENDGAME_SWEEP.tsv   the post-Moon Lord tiers (levy_dmg_idle_600t / 10 = damage per second)
Rows kept: OK, WEAK, BROKEN (status_after). Left out: NOREF (the stand-in did no damage, so there is no verdict) and rows whose note says a later fix covers them ("COVERED BY", "NOT MEASURABLE").
Vanilla rows are written with their internal item names (the sweeps print display names).
Usage: python3 tools/gen_levy_fit.py   (from levy-mod/)
"""
import csv, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "Levy", "Common", "Weapons", "LevyFit.tsv")

# display name -> internal name (ItemID.Search), for every vanilla weapon the sweeps measured
VANILLA = {
    "Terra Blade": "TerraBlade", "Morning Star": "MaceWhip", "Influx Waver": "InfluxWaver", "Dark Harvest": "ScytheWhip",
    "The Eye of Cthulhu": "TheEyeOfCthulhu", "Christmas Tree Sword": "ChristmasTreeSword", "Death Sickle": "DeathSickle",
    "Sniper Rifle": "SniperRifle", "Desert Tiger Staff": "StormTigerStaff", "Frostbrand": "Frostbrand", "Deadly Sphere Staff": "DeadlySphereStaff",
    "Staff of Earth": "StaffofEarth", "Xenopopper": "Xenopopper", "Meowmere": "Meowmere", "Star Wrath": "StarWrath", "Terrarian": "Terrarian",
    "Celebration Mk2": "Celeb2", "S.D.M.G.": "SDMG", "Last Prism": "LastPrism", "Lunar Flare": "LunarFlareBook",
    "Lunar Portal Staff": "MoonlordTurretStaff", "Rainbow Crystal Staff": "RainbowCrystalStaff",
}

def rows(path, dps_of):
    with open(os.path.join(ROOT, "docs", path), newline="") as f:
        for r in csv.DictReader(f, delimiter="\t"):
            st = r["status_after"].strip()
            note = " ".join(r.get(k, "") or "" for k in ("note", "cause_note", "sweep_cause"))
            if st not in ("OK", "WEAK", "BROKEN") or "COVERED BY" in note or "NOT MEASURABLE" in note:
                continue
            mod, _, name = r["weapon"].partition(":")
            if mod == "Terraria":
                if name not in VANILLA: sys.exit(f"no internal name for vanilla weapon {name!r}: add it to VANILLA")
                name = VANILLA[name]
            yield mod, name, st, float(r["ratio_after"]), dps_of(r), path.split("_")[0].lower()

out = {}
for row in rows("ARMYSWEEP_DPS.tsv", lambda r: float(r["levy_dps_idle"])):
    out[(row[0], row[1])] = row
for row in rows("ENDGAME_SWEEP.tsv", lambda r: float(r["levy_dmg_idle_600t"]) / 10.0):
    out.setdefault((row[0], row[1]), row)   # the army sweep's row wins: it was measured on her own setup

with open(OUT, "w", newline="") as f:
    f.write("mod\titem\tstatus\tratio\tlevy_dps\tsource\n")
    for k in sorted(out):
        mod, name, st, ratio, dps, src = out[k]
        f.write(f"{mod}\t{name}\t{st}\t{ratio:.2f}\t{dps:.0f}\t{src}\n")
print(f"wrote {len(out)} rows to {OUT}")

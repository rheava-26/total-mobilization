#!/usr/bin/env python3
"""Writes the labcompare raw fight specs (levylab raw .tsv) into specs/ (pre-hardmode) and the hardmode wrappers into <lab>/work.
  mkspecs.py [--lab /home/user/lab/labcompare]

Two arms for every set:
  matched  = the current Levy (3.1.8) with every ENEMY-SIDE rule that changed since the baseline switched back where a switch exists:
             boss life scaling (LEVY_BOSSHP=legacy: +2% per engaged command point, cap 80%, the 2.0.x rule) and the hit-parity work of 3.0
             (LEVY_PLAYERHIT=0, LEVY_PROJPARITY=0, LEVY_ARENABOX=0: hostile hits back to the earlier intake, no difficulty multiplier on shots,
             no Calamity arena boxes). For pre-hardmode also the 2.0.64 contact parity (LEVY_CONTACTPARITY=0, LEVY_CONTACTSLOTS=0) and no
             single-player owner emulation (LEVY_CR_SP=0), which lab 1 did not have. So the boss is as close to the old one as switches allow
             and the difference is the levies (AI, weapons, movement, fear, gear use).
  shipped  = the current Levy exactly as it ships (every rule at its default); pre-hardmode runs with the single-player owner emulation on (what a real game runs).
Pre-hardmode cells = boss-lab lab 1 (Levy 2.0.13) main matrix: gear=mod (the C# tier roll; LEVY_CR_EXCLUDE empty, as in lab 1, so the duds UrchinMace and
SaharaSlicers stay in), Levy grade, the lab-1 levy counts written out (n=cp follows newer tier math), cmd=far, arena surf (the harness default), 300 s cap,
seed parity picks the order (odd free, even sd) as in lab 1.
Hardmode = hmbattery2 (Levy 2.0.66): hm_mk.py (broad sweep, 184 fights) and hm_battery.py (Destroyer baseline, 40 fights), unchanged since then."""
import argparse
from pathlib import Path

OUT = Path(__file__).resolve().parent / "specs"
CELLS = [("kslime", "exp", 10), ("kslime", "rev", 10), ("ds", "exp", 11), ("eye", "exp", 11), ("eye", "rev", 11), ("eye", "death", 11),
         ("eow", "exp", 13), ("brain", "exp", 13), ("brain", "rev", 13), ("qb", "exp", 15), ("qb", "rev", 15), ("skel", "exp", 16)]
ENEMY_OLD = {"LEVY_BOSSHP": "legacy", "LEVY_PLAYERHIT": "0", "LEVY_PROJPARITY": "0", "LEVY_ARENABOX": "0"}
ENV = {
    "prehm_matched": {"LEVY_CR_SP": "0", "LEVY_CR_EXCLUDE": "", **ENEMY_OLD, "LEVY_CONTACTPARITY": "0", "LEVY_CONTACTSLOTS": "0"},
    "prehm_shipped": {"LEVY_CR_SP": "1", "LEVY_CR_EXCLUDE": ""},
    "hm_matched": dict(ENEMY_OLD), "hm_shipped": {},
}


def head(title, env):
    return [f"# {title}"] + [f"# env: {k}={v}" for k, v in env.items()]


def prehm(name, seeds, cells=CELLS):
    lines = head(f"labcompare {name}: pre-hardmode vs boss-lab lab 1 (2.0.13); cmd=far, surf, gear=mod, lab-1 counts, 300 s cap", ENV[name])
    for b, m, n in cells:
        for s in seeds:
            lines.append("\t".join(["fight", b, m, str(s), "18000", "gear=mod", f"n={n}", "cmd=far"]))
    (OUT / f"{name}.tsv").write_text("\n".join(lines) + "\n")
    print(name, sum(1 for l in lines if l.startswith("fight")), "fights")


def hm(lab, group, arm):
    src = lab / "work" / group / "spec.tsv"
    if not src.exists(): print("missing", src, "(run hm_mk.py / hm_battery.py first)"); return
    name = f"hm_{arm}"
    lines = head(f"labcompare {group} {arm}: hardmode vs hmbattery2 (2.0.66), armies and fight lines unchanged", ENV[name]) + src.read_text().splitlines()
    (lab / "work" / group / f"spec_{arm}.tsv").write_text("\n".join(lines) + "\n")
    print(group, arm, sum(1 for l in lines if l.startswith("fight")), "fights")


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("--lab", default="/home/user/lab/labcompare"); a = ap.parse_args()
    OUT.mkdir(exist_ok=True)
    for f in OUT.glob("prehm_sp*.tsv"): f.unlink()
    prehm("prehm_matched", range(1, 11)); prehm("prehm_shipped", range(1, 11))
    for g in ("bat", "hmall"):
        for arm in ("matched", "shipped"): hm(Path(a.lab), g, arm)

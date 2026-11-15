#!/usr/bin/env python3
"""OLD vs NEW tables for the hardmode boss battery and broad sweep (hmreplays = old, hmbattery2 = new).
  hm_compare.py [--old /home/user/lab/hmreplays] [--new /home/user/lab/hmbattery2] [--part battery|sweep|all]
Reads <lab>/battery/fights.csv and <lab>/recordings/fights.csv; the damage split is read back from each fight's levylab run dir (hm_hits.py).
Damage figures are per fight, means: contact = what levies took from boss bodies / segments / adds touching them, projectile = what they took from hostile projectiles."""
import argparse, csv, statistics, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from hm_hits import num, pct, wilson, fisher, battery_damage, sweep_damage
from hm_mk import BOSSES

ap = argparse.ArgumentParser()
ap.add_argument("--old", default="/home/user/lab/hmreplays"); ap.add_argument("--new", default="/home/user/lab/hmbattery2"); ap.add_argument("--part", default="all")
a = ap.parse_args()
OK = ("win", "loss", "timeout", "despawn")


def load(path):
    p = Path(path)
    return [r for r in csv.DictReader(open(p)) if r["result"].lower() in OK] if p.exists() else []


def mean(xs):
    xs = [x for x in xs if x is not None]
    return statistics.mean(xs) if xs else float("nan")


def stats(rows, dmg, key):
    """rows: fights of one cell. -> dict of win rate, kill time, deaths, contact, proj"""
    w = [r for r in rows if r["result"].lower() == "win"]; lo, hi = wilson(len(w), len(rows)); ks = [num(r["sec"]) for r in w if num(r["sec"]) is not None]
    ds = [dmg.get(key(r)) for r in rows]; ds = [d for d in ds if d]
    return dict(n=len(rows), w=len(w), lo=lo, hi=hi, kill=(statistics.median(ks), pct(ks, .1), pct(ks, .9)) if ks else None, deaths=mean([num(r["deaths"]) for r in rows]),
                contact=mean([d["contact"] for d in ds]), proj=mean([d["proj"] for d in ds]), projN=mean([d["projN"] for d in ds]),
                head=mean([d["head"] for d in ds]), body=mean([d["body"] for d in ds]), tail=mean([d["tail"] for d in ds]),
                lock=mean([num(r.get("tgtLevyPct")) for r in rows]), spite=mean([num(r.get("tgtSpitePct")) for r in rows]), cmdr=mean([num(r.get("tgtPlayerPct")) for r in rows]))


def wr(s): return f"{s['w']}/{s['n']} = {100 * s['w'] / s['n']:.0f}% ({100 * s['lo']:.0f}-{100 * s['hi']:.0f})" if s["n"] else "-"
def kt(s, ci=True): return (f"{s['kill'][0]:.0f} ({s['kill'][1]:.0f}-{s['kill'][2]:.0f})" if ci else f"{s['kill'][0]:.0f}") if s["kill"] else "-"
def f0(x): return "-" if x != x else f"{x:.0f}"
def f1(x): return "-" if x != x else f"{x:.1f}"


NAME = {"baseline": "baseline (34 levies)", "n12": "12 levies", "n24": "24 levies", "n36": "36 levies", "nowings": "no wings", "flat": "flat arena"}
PAIR = {"rev": "Exp+Rev", "mdeath": "Master+Death"}

if a.part in ("battery", "all"):
    old, new = load(Path(a.old) / "battery" / "fights.csv"), load(Path(a.new) / "battery" / "fights.csv")
    dmg_o = battery_damage(sorted({r["run"] for r in old})); dmg_n = battery_damage(sorted({r["run"] for r in new}))
    ko = lambda r: (r["boss"], r["cell"], int(r["seed"]))
    print("## Battery, Master+Death: OLD (hmreplays, Spectator veto on projectiles) vs NEW (hmbattery2)\n")
    print("| boss | cell | OLD win rate (95% CI) | NEW win rate (95% CI) | Fisher p | OLD kill s median (p10-p90) | NEW kill s median (p10-p90) | OLD levy deaths/fight | NEW levy deaths/fight |")
    print("|---|---|---|---|---|---|---|---|---|")
    S = {}
    for boss in ("destroyer", "aqua"):
        for cell in NAME:
            mo = [r for r in old if r["boss"] == boss and r["cell"] == cell]; mn = [r for r in new if r["boss"] == boss and r["cell"] == cell]
            if not mo and not mn: continue
            so, sn = stats(mo, dmg_o, ko), stats(mn, dmg_n, ko); S[(boss, cell)] = (so, sn)
            p = fisher(so["w"], so["n"] - so["w"], sn["w"], sn["n"] - sn["w"]) if so["n"] and sn["n"] else float("nan")
            print(f"| {boss} | {NAME[cell]} | {wr(so)} | {wr(sn)} | {p:.3f} | {kt(so)} | {kt(sn)} | {f1(so['deaths'])} | {f1(sn['deaths'])} |")
    print("\n## Battery damage taken by levies per fight (mean), OLD -> NEW\n")
    print("| boss | cell | contact OLD | contact NEW | projectile OLD (hits) | projectile NEW (hits) | NEW head / body / tail | NEW boss target: levy lock % | spite % | commander % | OLD target field (lock + spite merged) % |")
    print("|---|---|---|---|---|---|---|---|---|---|---|")
    for (boss, cell), (so, sn) in S.items():
        print(f"| {boss} | {NAME[cell]} | {f0(so['contact'])} | {f0(sn['contact'])} | {f0(so['proj'])} ({f0(so['projN'])}) | {f0(sn['proj'])} ({f0(sn['projN'])}) | {f0(sn['head'])} / {f0(sn['body'])} / {f0(sn['tail'])} | {f0(sn['lock'])} | {f0(sn['spite'])} | {f0(sn['cmdr'])} | {f0(so['lock'])} |")
    print()

if a.part in ("sweep", "all"):
    old, new = load(Path(a.old) / "recordings" / "fights.csv"), load(Path(a.new) / "recordings" / "fights.csv")
    dmg_o = sweep_damage(sorted({r["run"] for r in old})); dmg_n = sweep_damage(sorted({r["run"] for r in new}))
    ko = lambda r: (r["slug"], r["pair"], r["kit"], int(r["seed"]))
    print("## Broad sweep: OLD vs NEW (3 on-tier seeds + 1 one-tier-below seed per boss and mode)\n")
    print("| boss | levies | mode | OLD W / L / other (n) | NEW W / L / other (n) | OLD kill s | NEW kill s | OLD deaths/fight | NEW deaths/fight | contact OLD -> NEW | projectile OLD -> NEW (hits NEW) | NEW boss target: lock / spite / cmdr % |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|")
    tot_o = tot_n = 0
    for slug in BOSSES:
        for pair in ("rev", "mdeath"):
            mo = [r for r in old if r["slug"] == slug and r["pair"] == pair]; mn = [r for r in new if r["slug"] == slug and r["pair"] == pair]
            if not mo and not mn: continue
            so, sn = stats(mo, dmg_o, ko), stats(mn, dmg_n, ko); tot_o += len(mo); tot_n += len(mn)
            def wl(rs, s): return f"{s['w']} / {sum(1 for r in rs if r['result'].lower() == 'loss')} / {sum(1 for r in rs if r['result'].lower() in ('timeout', 'despawn'))} ({s['n']})" if rs else "-"
            lv = (mn or mo)[0]["levies"]
            print(f"| {BOSSES[slug][4]} | {lv} | {PAIR[pair]} | {wl(mo, so)} | {wl(mn, sn)} | {kt(so, False)} | {kt(sn, False)} | {f1(so['deaths'])} | {f1(sn['deaths'])} | {f0(so['contact'])} -> {f0(sn['contact'])} | {f0(so['proj'])} -> {f0(sn['proj'])} ({f0(sn['projN'])}) | {f0(sn['lock'])} / {f0(sn['spite'])} / {f0(sn['cmdr'])} |")
    print(f"\nfights: OLD {tot_o}, NEW {tot_n}")

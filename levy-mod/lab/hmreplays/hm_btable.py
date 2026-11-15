#!/usr/bin/env python3
"""Battery table: hm_btable.py [<HM_LAB>/battery/fights.csv]
per boss x cell: fights, win rate (Wilson 95% interval), kill time median / p10 / p90 of the wins, levy deaths per fight (mean), damage TAKEN by levies per fight by source
(head / body / tail / projectile / adds, mean), mean tgtLevy% (the recorder's target field; it reads levy for the targeting lock, an NPC target >= 300 and the spite target alike)."""
import os, csv, glob, json, math, re, statistics, sys
from pathlib import Path

rows = [r for r in csv.DictReader(open(sys.argv[1] if len(sys.argv) > 1 else os.environ.get("HM_LAB", "/home/user/lab/hmbattery2") + "/battery/fights.csv")) if r["result"] in ("WIN", "LOSS", "TIMEOUT", "DESPAWN")]
HIT_RE = re.compile(r"(contact|proj):(.*?)(?:/(-?\d+))?=(\d+)/(\d+)")


def hits_by_source(rows):
    """damage TAKEN by levies per fight from the run's out.tsv.hit.tsv (contact:<npc>/<type>=damage/hits, proj:<name>=damage/hits): head / body / tail / projectile / other contact."""
    out = {}
    for run in {r["run"] for r in rows}:
        cells = {c["id"]: c["name"] for c in json.loads((Path(run) / "cells.json").read_text())["cells"]}
        for hf in glob.glob(str(Path(run) / "jobs" / "*" / "try*" / "out.tsv.hit.tsv")):
            for line in open(hf, errors="replace"):
                k, _, v = line.rstrip("\n").partition("\t"); p = k.split("|")
                m = re.search(r"\|t(c\d+)\|", k)
                if not m or m[1] not in cells: continue
                mm = re.search(r"meta=hmb_(\w+?)_(\w+)_(?:rev|mdeath)\b", cells[m[1]])
                if not mm: continue
                d = dict(head=0, body=0, tail=0, proj=0, other=0)
                for kind, nm, ident, dmg, n in HIT_RE.findall(v):
                    dmg = int(dmg)
                    if kind == "proj": d["proj"] += dmg
                    elif ident == "134" or nm.endswith("Head"): d["head"] += dmg
                    elif ident == "135" or nm.endswith("Body"): d["body"] += dmg
                    elif ident == "136" or nm.endswith("Tail"): d["tail"] += dmg
                    else: d["other"] += dmg
                out[(mm[1], mm[2], p[4])] = d
    return out


ORDER = ["baseline", "n12", "n24", "n36", "nowings", "flat"]
NAME = {"baseline": "baseline (34 levies, wings, platforms)", "n12": "12 levies", "n24": "24 levies", "n36": "36 levies", "nowings": "no wings", "flat": "flat arena (no platforms)"}


def num(x):
    try: return float(x)
    except (TypeError, ValueError): return None


def pct(xs, p):
    xs = sorted(xs); k = (len(xs) - 1) * p; lo, hi = math.floor(k), math.ceil(k)
    return xs[lo] + (xs[hi] - xs[lo]) * (k - lo)


def wilson(w, n, z=1.96):
    if n == 0: return (0, 0)
    p = w / n; d = 1 + z * z / n; c = p + z * z / (2 * n); h = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n))
    return ((c - h) / d, (c + h) / d)


def mean(rs, k):
    v = [num(r.get(k)) for r in rs if num(r.get(k)) is not None]
    return statistics.mean(v) if v else float("nan")


HB = hits_by_source(rows)
for r in rows:
    d = HB.get((r["boss"], r["cell"], str(r["seed"])), {})
    for k in ("head", "body", "tail", "proj", "other"): r["h_" + k] = d.get(k, "")
print("| boss | cell | fights | win rate (95% CI) | kill s median (p10-p90) | levy deaths/fight | dmg taken: head | body | tail | projectile | other contact | boss target = levy % |")
print("|---|---|---|---|---|---|---|---|---|---|---|---|")
for boss in ("destroyer", "aqua"):
    for cell in ORDER:
        m = [r for r in rows if r["boss"] == boss and r["cell"] == cell]
        if not m: continue
        w = [r for r in m if r["result"] == "WIN"]; lo, hi = wilson(len(w), len(m)); ks = [num(r["sec"]) for r in w if num(r["sec"]) is not None]
        kt = f"{statistics.median(ks):.0f} ({pct(ks, .1):.0f}-{pct(ks, .9):.0f})" if ks else "-"
        print(f"| {boss} | {NAME[cell]} | {len(m)} | {len(w)}/{len(m)} = {100 * len(w) / len(m):.0f}% ({100 * lo:.0f}-{100 * hi:.0f}) | {kt} | {mean(m, 'deaths'):.1f} | {mean(m, 'h_head'):.0f} | {mean(m, 'h_body'):.0f} | {mean(m, 'h_tail'):.0f} | {mean(m, 'h_proj'):.0f} | {mean(m, 'h_other'):.0f} | {mean(m, 'tgtLevyPct'):.0f} |")

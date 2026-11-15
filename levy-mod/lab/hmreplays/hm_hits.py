#!/usr/bin/env python3
"""Shared helpers for the hmreplays / hmbattery2 tables: stats (Wilson interval, percentile, Fisher exact) and the per-fight DAMAGE TAKEN split read back from a levylab run dir.

damage taken by levies per fight, from the run's results.csv (contact = takenHead + takenBody + takenTail + takenOtherContact; projectile = takenProj / takenProjHits) and,
for the worm split, from the run's out.tsv.hit.tsv lines (contact:<npc>/<type>=damage/hits, proj:<name>=damage/hits)."""
import csv, glob, json, math, re
from pathlib import Path

HIT_RE = re.compile(r"(contact|proj):(.*?)(?:/(-?\d+))?=(\d+)/(\d+)")
SWEEP_META = re.compile(r"meta=(\w+?)_(on|mid)_(rev|mdeath)_n(\d+)_T([\d.]+)")
BATTERY_META = re.compile(r"meta=hmb_(\w+?)_(\w+)_(rev|mdeath)\b")


def num(x):
    try: return float(x)
    except (TypeError, ValueError): return None


def pct(xs, p):
    xs = sorted(xs); k = (len(xs) - 1) * p; lo, hi = math.floor(k), math.ceil(k)
    return xs[lo] + (xs[hi] - xs[lo]) * (k - lo)


def wilson(w, n, z=1.96):
    if n == 0: return (0.0, 0.0)
    p = w / n; d = 1 + z * z / n; c = p + z * z / (2 * n); h = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n))
    return ((c - h) / d, (c + h) / d)


def fisher(a, b, c, d):
    """two-sided Fisher exact p for the 2x2 table [[a, b], [c, d]] (wins/losses of arm 1, wins/losses of arm 2)."""
    n1, n2, k = a + b, c + d, a + c
    def pm(x): return math.comb(n1, x) * math.comb(n2, k - x) / math.comb(n1 + n2, k)
    p0 = pm(a)
    return min(1.0, sum(pm(x) for x in range(max(0, k - n2), min(n1, k) + 1) if pm(x) <= p0 * (1 + 1e-9)))


def cell_names(run):
    return {c["id"]: c["name"] for c in json.loads((Path(run) / "cells.json").read_text())["cells"]}


def _split(v):
    d = dict(head=0, body=0, tail=0, proj=0, other=0)
    for kind, nm, ident, dmg, n in HIT_RE.findall(v):
        dmg = int(dmg)
        if kind == "proj": d["proj"] += dmg
        elif ident == "134" or nm.endswith("Head"): d["head"] += dmg
        elif ident == "135" or nm.endswith("Body"): d["body"] += dmg
        elif ident == "136" or nm.endswith("Tail"): d["tail"] += dmg
        else: d["other"] += dmg
    return d


def run_damage(run):
    """-> {(cellname, seed): dict(contact, proj, projN, head, body, tail)} for every fight of a run dir, from results.csv (+ hit.tsv for the worm split)."""
    run = Path(run); names = cell_names(run); out = {}
    rc = run / "results.csv"
    if rc.exists():
        for r in csv.DictReader(open(rc)):
            nm = names.get(r["cell"])
            if nm is None: continue
            g = lambda k: num(r.get(k)) or 0.0
            out[(r["cell"], int(r["seed"]))] = dict(contact=g("takenHead") + g("takenBody") + g("takenTail") + g("takenOtherContact"), proj=g("takenProj"), projN=g("takenProjHits"), head=0, body=0, tail=0)
    for hf in glob.glob(str(run / "jobs" / "*" / "try*" / "out.tsv.hit.tsv")):
        for line in open(hf, errors="replace"):
            k, _, v = line.rstrip("\n").partition("\t"); p = k.split("|")
            m = re.search(r"\|t(c\d+)\|", k)
            if not m or len(p) < 5: continue
            key = (m[1], int(p[4]))
            if key in out:
                s = _split(v); out[key].update(head=s["head"], body=s["body"], tail=s["tail"])
    return {(names[c], s): d for (c, s), d in out.items()}


def sweep_damage(runs):
    """{(slug, pair, kit, seed): damage dict} over run dirs of hm_mk.py sweeps (the last run to contain a fight wins)."""
    out = {}
    for run in runs:
        for (nm, seed), d in run_damage(run).items():
            m = SWEEP_META.search(nm)
            if m: out[(m[1], m[3], m[2], seed)] = d
    return out


def battery_damage(runs):
    """{(boss, cell, seed): damage dict} over run dirs of hm_battery.py runs."""
    out = {}
    for run in runs:
        for (nm, seed), d in run_damage(run).items():
            m = BATTERY_META.search(nm)
            if m: out[(m[1], m[2], seed)] = d
    return out

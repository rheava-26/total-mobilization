#!/usr/bin/env python3
"""OLD rows for the pre-hardmode cells: boss-lab lab 1 (Levy 2.0.12/2.0.13).
The authority is lab 1's own main table (levy-mod/Levy/docs/boss_lab/main_table.txt, the `surf/far` lines): it lists, per cell, every fight it counted as
seed:<result letter><seconds>. Each of those is matched to its raw row in levy-mod/Levy/docs/boss_lab/raw/*.tsv (same boss, mode, n, seed, cmd=far, surface,
result and whole seconds; lab 1 counted a few 600 s-cap rows that ended before 300 s), so the metrics come from the exact fights lab 1 reported.
  old_prehm.py            -> per-cell summary (and any fight that could not be matched)
  import old_prehm; old_prehm.rows() -> {(boss, mode, n): [row dict, ...]}"""
import csv, re
from pathlib import Path

BL = Path(__file__).resolve().parents[2] / "Levy" / "docs" / "boss_lab"
RES = {"W": "WIN", "L": "LOSS", "T": "TIMEOUT", "D": "DESPAWN"}
# run order of lab 1 (RESULTS.md sections 0 and 8). When several raw rows fit a main-table fight (a 300 s timeout fits every rerun), the latest run is
# taken, as lab 1's tab.py kept the last row of a key. Checked: the deaths per fight then equal the main table's column.
ORDER = ["smoke1", "smoke2", "dbg_eye", "e0", "s1_main", "b1b", "b2_eye", "b4_ug", "b3_clone", "b3_dps", "b3_hpscale0", "b2_brain", "b5_brain", "b1a_far", "b1_far13",
         "b2_eye13", "near13", "death_more", "wormsx", "worms_fix", "eye_phase", "eye_phase_hs0", "e16v19", "big1", "big2", "big3", "anom_ds", "anom2", "brain_new", "diag_brain"]


def _raw():
    out = []
    for p in sorted((BL / "raw").glob("*.tsv")):
        if ".dbg" in p.name or ".hit" in p.name: continue
        for r in csv.DictReader(open(p, errors="replace"), delimiter="\t"):
            if r.get("kind") == "fight": r["_file"] = p.stem; out.append(r)
    return out


def rows():
    raw = _raw(); pat = re.compile(r"^fight\|(\w+)\|(\w+)\|(free|sd)\|(\d+)\|(\d+)(?:\|n(\d+))?\|gmod\|grlv\|cfar$")
    idx = {}
    for r in raw:
        m = pat.match(r["key"])
        if m: idx.setdefault((m[1], m[2], int(m[6] or 10), int(m[4])), []).append(r)
    out = {}; missing = []
    for line in open(BL / "main_table.txt"):
        f = line.split()
        if len(f) < 5 or f[3] != "surf/far": continue
        boss, mode, n = f[0], f[1], int(f[4].split("=")[1])
        for tok in line.rstrip().split("|")[-1].strip().split(","):
            sd, rs = tok.split(":"); res, sec = RES[rs[0]], float(rs[1:])
            cand = [r for r in idx.get((boss, mode, n, int(sd)), []) if r["result"] == res and abs(float(r["sec"]) - sec) < 0.51]
            cand.sort(key=lambda r: ORDER.index(r["_file"]) if r["_file"] in ORDER else len(ORDER))
            if cand: out.setdefault((boss, mode, n), []).append(cand[-1])
            else: missing.append((boss, mode, n, sd, rs))
    return out, missing


if __name__ == "__main__":
    o, miss = rows()
    for (b, m, n), v in sorted(o.items()):
        print(f"{b:7} {m:6} n={n:2} fights={len(v):2} wins={sum(r['result'] == 'WIN' for r in v):2} deaths={sum(float(r['deaths']) for r in v) / len(v):4.1f}{' '.join(r['result'][0] + r['seed'] for r in v)}  files={sorted({r['_file'] for r in v})}")
    print("unmatched:", miss)

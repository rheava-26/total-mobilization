#!/usr/bin/env python3
"""summ.py RUN_DIR...: one line per fight from the harness out.tsv (target mix, enraged seconds, result)."""
import csv, glob, re, sys
for run in sys.argv[1:]:
    rows = []
    for f in sorted(glob.glob(run + "/jobs/*/try*/out.tsv")):
        for r in csv.DictReader(open(f, errors="replace"), delimiter="\t"):
            if r.get("result") in (None, "", "result"): continue
            rows.append(r)
    rows.sort(key=lambda r: (r["key"], int(r["seed"])))
    for r in rows:
        k = r["key"]; a = re.search(r"\|a([^|]*)\|", k); c = "far" if "|cfar" in k else "near"
        print(run.rstrip("/").split("/")[-1], r["boss"], r["mode"], "arena=" + (a.group(1) if a else "?"), "cmd=" + c, "seed", r["seed"], r["result"], "sec", r["sec"], "deaths", r["deaths"],
              "tgtLevy%", r["tgtLevyPct"], "tgtPlayer%", r["tgtPlayerPct"], "tgtNone%", r["tgtNonePct"], "enragedSec", r["enragedSec"], "bossLeft", r["bossLeft"], "/", r["bossMax"])

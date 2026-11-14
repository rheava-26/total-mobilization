#!/usr/bin/env python3
"""enr.py RUN_DIR [...]: summarise HARNESS-ENR probe lines per fight (arena, boss), by target kind."""
import glob, gzip, re, sys, collections
rows = collections.OrderedDict()
for run in sys.argv[1:]:
    for f in sorted(glob.glob(run + "/jobs/*/try*/server.log.gz")):
        for l in gzip.open(f, "rt", errors="replace"):
            if "HARNESS-ENR" not in l: continue
            m = re.search(r"key=(\S+) t=(\d+) boss=(\w+) tgt=(\d+) kind=(\S+) enr=(\d) .*?tgtTile=(\d+),(\d+) zones\[([^\]]*)\](?: cond\[([^\]]*)\])?", l)
            if not m: continue
            key, t, boss, tgt, kind, enr, tx, ty, z, cond = m.groups()
            arena = re.search(r"\|a([^|]*)\|", key)
            seed = key.split("|")[4]
            k = (boss, arena.group(1) if arena else "?", seed, run.rstrip("/").split("/")[-1])
            d = rows.setdefault(k, collections.Counter())
            d["n"] += 1; d["kind_" + kind] += 1; d["enr_" + kind] += int(enr)
            d["last"] = (t, tx, ty, z, cond)
            if "levy" == kind: d["lastlevy"] = (tx, ty, z, cond)
for k, d in rows.items():
    print(k, "samples", d["n"], "| cmd", d["kind_cmd"], "enr", d["enr_cmd"], "| levy", d["kind_levy"], "enr", d["enr_levy"], "| other", {x: v for x, v in d.items() if x.startswith("kind_") and x not in ("kind_cmd", "kind_levy")})
    if d.get("lastlevy"): print("    levy dummy last:", d["lastlevy"])
    elif d.get("last"): print("    last:", d["last"])

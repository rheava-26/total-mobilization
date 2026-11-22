#!/usr/bin/env python3
"""Cut for wall time: the hardmode matched sweep stopped after 74 fights (the first nine bosses complete, Plantera seed 1); the remaining bosses run on
their on-tier seeds 1-2 only. Writes <spec dir>/spec_matched_b.tsv = the env header of spec_matched.tsv + every fight line of seeds 1-2 that the first
run did not finish.
  mkrest.py WORK_GROUP_DIR FIRST_RUN_DIR"""
import csv, glob, sys
from pathlib import Path

grp, run = Path(sys.argv[1]), Path(sys.argv[2])
done = set()
for p in glob.glob(str(run / "jobs/*/try*/out.tsv")):
    for r in csv.DictReader(open(p, errors="replace"), delimiter="\t"):
        if r.get("kind") == "fight": done.add((r["boss"], r["mode"], r["seed"]))
lines = (grp / "spec_matched.tsv").read_text().splitlines()
hdr = [l for l in lines if l.startswith("#")] + ["# cut for time: the bosses the first matched run did not reach, on-tier seeds 1-2 only"]
body = [l for l in lines if l.startswith("fight") and l.split("\t")[3] in ("1", "2") and tuple(l.split("\t")[1:4]) not in done]
(grp / "spec_matched_b.tsv").write_text("\n".join(hdr + body) + "\n")
print(len(done), "done;", len(body), "fights ->", grp / "spec_matched_b.tsv")

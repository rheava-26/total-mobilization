#!/usr/bin/env python3
"""Builds the replay site from /home/user/lab/hmreplays/recordings (see hm_collect.py).
  hm_site.py [--rec /home/user/lab/hmreplays/recordings] [--sets /home/user/lab/hmreplays/sets] [--site /home/user/lab/hmreplays/site] [--bosses a,b] [--hz 15] [--batches 55]

make_site.py reads a set folder's index.txt notes only for names that contain '.jsonl', so each set folder holds symlinks <name>.jsonl.gz -> the .levyrec
(a gzip JSON-lines file, which make_site opens by its .gz suffix) plus an index.txt with one note per fight taken from fights.csv. One set per boss."""
import argparse, csv, os, shutil, subprocess, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from hm_mk import BOSSES

ap = argparse.ArgumentParser()
ap.add_argument("--rec", default="/home/user/lab/hmreplays/recordings"); ap.add_argument("--sets", default="/home/user/lab/hmreplays/sets"); ap.add_argument("--site", default="/home/user/lab/hmreplays/site")
ap.add_argument("--bosses", default=""); ap.add_argument("--hz", default="15"); ap.add_argument("--batches", default="0")
a = ap.parse_args()
rec, sets, site = Path(a.rec), Path(a.sets), Path(a.site)
PAIR = {"rev": "Expert+Rev", "mdeath": "Master+Death"}
ORDER = list(BOSSES)
want = a.bosses.split(",") if a.bosses else ORDER
rows = list(csv.DictReader(open(rec / "fights.csv")))
shutil.rmtree(sets, ignore_errors=True); sets.mkdir(parents=True)
args = []
for slug in want:
    mine = [r for r in rows if r["slug"] == slug]
    if not mine: continue
    d = sets / slug; d.mkdir()
    lines = []
    mine.sort(key=lambda r: (r["pair"] != "mdeath", r["kit"] != "on", int(r["seed"])))   # Master+Death first (what Eve plays), on-tier kit before mid
    for r in mine:
        src = (rec / r["file"]).resolve(); name = Path(r["file"]).stem + ".jsonl.gz"
        os.symlink(src, d / name)
        res = r["result"].upper() if r["result"] != "despawn" else "DESPAWN"
        try: dead = int(r["deaths"])
        except ValueError: dead = None
        fd = r["firstDeathSec"]
        txt = f"{PAIR[r['pair']]}, {r['levies']} levies, {'on-tier' if r['kit'] == 'on' else 'one tier below'} gear, seed {r['seed']}. "
        if res == "WIN": txt += f"Won in {float(r['sec']):.0f} s"
        elif res == "TIMEOUT": txt += f"Timed out at {float(r['sec']):.0f} s"
        else: txt += f"{res.capitalize()} after {float(r['sec']):.0f} s"
        txt += f", {dead} of {r['levies']} levies died" + (f" (first at {float(fd):.0f} s)." if fd not in ("", None) and dead else ".")
        lines += [name, "    " + txt]
    (d / "index.txt").write_text("\n".join(lines) + "\n")
    title = BOSSES[slug][4]
    modes = sorted({PAIR[r["pair"]] for r in mine}, key=lambda m: m != "Master+Death")
    args.append(f"{title.replace(' ', '_')}_(lab,_{'_and_'.join(modes)})={d}")
cmd = [sys.executable, str(HERE.parent.parent / "Levy" / "tools" / "fightviewer" / "make_site.py"), str(site)] + args + ["--hz", a.hz]
if a.batches != "0": cmd += ["--batches", a.batches]
print(" ".join(cmd[:4]), "... %d sets" % len(args)); sys.exit(subprocess.call(cmd))

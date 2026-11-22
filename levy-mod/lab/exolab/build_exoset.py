#!/usr/bin/env python3
"""Build the curated "Your army vs the Exo Mechs (lab)" replay set: make_site over a folder of picked recordings, then OUT/{set.json, data/*.json}
(the layout of scratchpad/labset_v1: one set to merge into the published page's manifest). Not published here.
  build_exoset.py PICKS_DIR OUT_DIR WORK_DIR
PICKS_DIR holds the chosen .jsonl.gz recordings and an index.txt (one "file<TAB>note" line each, the note shown on the page)."""
import json, os, shutil, subprocess, sys

FV = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Levy", "tools", "fightviewer")
picks, out, work = sys.argv[1:4]
site = os.path.join(work, "site")
shutil.rmtree(site, ignore_errors=True)
subprocess.run([sys.executable, os.path.join(FV, "make_site.py"), site, "exolab=" + picks, "--hz", "15"], check=True)
man = json.load(open(os.path.join(site, "data", "manifest.json")))
assert len(man["sets"]) == 1
st = man["sets"][0]
st["id"] = "exolab"
st["title"] = "Your army vs the Exo Mechs (lab)"
st["intro"] = ("Your 40 regulars from your Nov 21 Exo Mechs fight, fighting alone on Levy 3.1.10 (Calamity, Master+Death) in an enormous open arena: "
               "a 1200-tile asphalt floor, three platform rows, open sky. You stand 150 tiles away as an invisible Spectator. "
               "Draedon picks Ares, Thanatos or Artemis and Apollo first; the rest of the fight is Calamity's own.")
for f in st["fights"]:
    if f.get("result") == "despawn" and f.get("dur", 0) >= 595: f["result"] = "timeout"
st = {"id": st["id"], "title": st["title"], "intro": st["intro"], **{k: v for k, v in st.items() if k not in ("id", "title", "intro")}}
shutil.rmtree(out, ignore_errors=True)
os.makedirs(os.path.join(out, "data"))
for f in st["fights"]:
    shutil.copyfile(os.path.join(site, f["file"]), os.path.join(out, f["file"]))
json.dump(st, open(os.path.join(out, "set.json"), "w", encoding="utf-8"), separators=(",", ":"))
man["sets"] = [st]
json.dump(man, open(os.path.join(work, "preview_manifest.json"), "w"), separators=(",", ":"))
data = sum(f["bytes"] for f in st["fights"])
print("set %s: %d fights, data %.2f MB, set.json %.1f KB" % (st["id"], len(st["fights"]), data / 1048576, os.path.getsize(os.path.join(out, "set.json")) / 1024))

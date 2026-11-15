#!/usr/bin/env python3
"""Per-boss summary of the recorded fights: hm_table.py [/home/user/lab/hmreplays/recordings/fights.csv]
fights, wins / losses / timeouts per mode (Expert+Rev, Master+Death), median kill time of the wins, mean levy deaths per fight, mean levies."""
import csv, statistics, sys
from pathlib import Path
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from hm_mk import BOSSES

rows = list(csv.DictReader(open(sys.argv[1] if len(sys.argv) > 1 else "/home/user/lab/hmreplays/recordings/fights.csv")))
rows = [r for r in rows if r["result"] not in ("error", "nores")]
PAIR = {"rev": "Exp+Rev", "mdeath": "Master+Death"}


def f(x):
    try: return float(x)
    except (TypeError, ValueError): return None


print("| boss | levies | mode | fights | wins | losses | timeouts/despawns | median kill s (wins) | levy deaths per fight (mean) |")
print("|---|---|---|---|---|---|---|---|---|")
tot = 0
for slug in BOSSES:
    for pair in ("rev", "mdeath"):
        m = [r for r in rows if r["slug"] == slug and r["pair"] == pair]
        if not m: continue
        w = [r for r in m if r["result"] == "win"]; l = [r for r in m if r["result"] == "loss"]; o = [r for r in m if r["result"] not in ("win", "loss")]
        ks = [f(r["sec"]) for r in w if f(r["sec"]) is not None]; dd = [f(r["deaths"]) for r in m if f(r["deaths"]) is not None]
        tot += len(m)
        print(f"| {BOSSES[slug][4]} | {m[0]['levies']} | {PAIR[pair]} | {len(m)} | {len(w)} | {len(l)} | {len(o)} | {statistics.median(ks):.0f} | {statistics.mean(dd):.1f} |" if ks else
              f"| {BOSSES[slug][4]} | {m[0]['levies']} | {PAIR[pair]} | {len(m)} | {len(w)} | {len(l)} | {len(o)} | - | {statistics.mean(dd):.1f} |")
print(f"\n{tot} fights")

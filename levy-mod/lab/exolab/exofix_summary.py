"""Before/after summary of a reduced exolab run: python3 summ.py EXO_COLLECT_DIR RUN_DIR OUTDIR"""
import sys, json, gzip, re, statistics, collections, importlib.util
from pathlib import Path
spec = importlib.util.spec_from_file_location("exo_collect", sys.argv[1] + "/exo_collect.py")
ec = importlib.util.module_from_spec(spec); spec.loader.exec_module(ec)
run, out = Path(sys.argv[2]), Path(sys.argv[3])
out.mkdir(parents=True, exist_ok=True)
fj = out / "fights.json"
if fj.exists(): fj.unlink()
ec.collect([run], out / "recs", str(fj))
fights = json.load(open(fj))
BEAMS = ("Exothermal Laser", "Blenderbeam", "Ohio Beam", "Thanatos", "Laser")

def phases(s):
    return [(p.split("@")[0], float(p.split("@")[1])) for p in s.split() if "@" in p]

def first_drop(f):
    """seconds from the first mech's spawn to the other two arriving (its 70% threshold)"""
    ph = phases(f["exo"].get("phases", ""))
    solo = next((t for l, t in ph if l in ("A", "T", "W")), None)
    nxt = next((t for l, t in ph if solo is not None and t > solo and len(l) >= 2), None)
    return (nxt - solo) if solo is not None and nxt is not None else None

def scale_ratio(f):
    """life / lifeMax of the first mech's root at the moment its lifeMax stops climbing (the first 6 s): 1.0 = the scaling held"""
    p = out / "recs" / f.get("rec", "x")
    if not p.exists(): return None
    best = None
    with gzip.open(p, "rt") as fh:
        n = 0
        for l in fh:
            d = json.loads(l)
            if d.get("k") != "f": continue
            for b in d.get("b", []):
                if b[18] in (-1, b[1]) and b[7] > 1_000_000 and b[13] == 0:   # a root of a mech (not a part)
                    if best is None or b[7] > best[0] or (b[7] == best[0] and d["t"] < best[2] + 90):
                        if best is None or b[7] > best[0]: best = (b[7], b[6], d["t"])
            n += 1
            if n > 400: break
    return None if best is None else best[1] / best[0]

rows = []
for f in sorted(fights, key=lambda f: (f["opener"], f["seed"])):
    rf = f.get("recFacts", {}); causes = collections.Counter(rf.get("causes", {}))
    dmg = sum(float(f["exo"].get(k, 0) or 0) for k in ("A_dmg", "T_dmg", "W_dmg")) or float(rf.get("end", {}).get("dmgBoss") or 0)
    gauss = causes.get("Gauss Explosion", 0)
    beams = sum(v for k, v in causes.items() if any(b in k for b in ("Exothermal Laser", "Blenderbeam", "Ohio Beam")) or ("Thanatos" in k and "Beam" in k) or k.endswith("Deathray"))
    rows.append(dict(opener=f["opener"], seed=f["seed"], result=f["result"], sec=float(f["exo"].get("sec", 0)), dmgBoss=rf.get("end", {}).get("dmgBoss"), dmg=dmg,
                     worst2s=rf.get("mass", [0])[0], worstBy=rf.get("mass", [0, ""])[1], gauss=gauss, beams=beams, deaths=f["exo"].get("deaths"),
                     to70=first_drop(f), closed=f["exo"].get("thanClosedHits"), open=f["exo"].get("thanOpenHits"), idleOpen=f["exo"].get("lvNoTgtWhileOpen%"),
                     scale=f["exo"].get("hpScalePct") or rf.get("end", {}).get("hpScalePct"), ratio=scale_ratio(f), phases=f["exo"].get("phases"), kills=f["exo"].get("killOrder")))
for r in rows: print(json.dumps(r))
json.dump(rows, open(out / "summary.json", "w"), indent=1)

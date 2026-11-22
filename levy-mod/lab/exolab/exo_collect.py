#!/usr/bin/env python3
"""Exo Mechs lab: collect a finished levylab run into fights.json + gzip recordings, and print the tables.
  exo_collect.py RUN [RUN ...] --dest RECDIR [--json OUT.json]     gzip every recording to RECDIR/<army>_<opener>_s<seed>_<result>.jsonl.gz, append index.txt, write fights.json
  exo_collect.py --tables FIGHTS.json                                 print the RESULTS tables
One fight = the harness rows (out.tsv.exo.tsv: kill order, phases, per-mech damage, targets, levy activity, ms per tick; out.tsv.exo_events.tsv),
the kit row (results.csv: deaths, first death, weapon damage, damage taken) and the recording (format 13, found by its cellkey)."""
import argparse, csv, glob, gzip, json, os, re, shutil, statistics
from collections import Counter, defaultdict
from pathlib import Path

OPEN = {"exoares": "Ares first", "exothan": "Thanatos first", "exotwins": "Artemis and Apollo first"}
ARMY = {"eve": "Her army (as recorded, 3.1.8 gear)", "eve_matched": "Her army, class-matched armour", "eve_noscale": "Her army, no boss-life scaling", "opt": "Optimal post-Yharon army"}


def rows(path):
    if not os.path.exists(path): return []
    with open(path, errors="replace") as f: return list(csv.DictReader(f, delimiter="\t"))


def rec_facts(path):
    """From a recording: what dealt each levy's killing hit (projectile name, else the NPC), the largest number of levy deaths inside any 2 s and its cause,
    the mechs' target kinds over every frame (0 none, 1 player, 2 levy, 3 spite) and the recorder's end record."""
    names, hits, deaths, tk, end, firstOf = {}, {}, [], Counter(), {}, {}
    with open(path, errors="replace") as fh:
        for l in fh:
            try: r = json.loads(l)
            except Exception: continue
            k = r.get("k")
            if k == "names": names = r
            elif k == "e" and r.get("e") == "lhit": hits[r["id"]] = r
            elif k == "e" and r.get("e") == "ldeath": deaths.append((r, hits.get(r["id"], {})))
            elif k == "e" and r.get("e") == "boss": firstOf.setdefault(r.get("name"), r.get("npc"))
            elif k == "f":
                for b in r.get("b", []):
                    if len(b) > 14: tk[(b[0], b[14])] += 1
            elif k == "nt": pass
            elif k == "end": end = r
    pn, nn = names.get("projectiles", {}), names.get("npcs", {})
    mech = {t for n, t in firstOf.items() if n in ("XF-09 Ares", "XM-05 Thanatos", "XS-01 Artemis", "XS-03 Apollo")}  # the first NPC of each name (Thanatos's segments share his name)
    tkm = Counter()
    for (ty, kd), v in tk.items():
        if ty in mech: tkm[kd] += v
    def cause(d, h):
        if h.get("proj", -1) not in (-1, None) and str(h.get("proj")) in pn: return pn[str(h["proj"])]
        if d.get("npc", -1) not in (-1, None): return nn.get(str(d["npc"]), "npc " + str(d["npc"]))
        return {"other": "no source (debuff or unattributed)", "proj": "unnamed projectile"}.get(h.get("src"), h.get("by", "?"))
    causes = [(d["t"], cause(d, h)) for d, h in deaths]
    best = (0, "", 0)
    for i, (t, c) in enumerate(causes):
        win = [x for x in causes if t <= x[0] < t + 120]
        if len(win) > best[0]: best = (len(win), Counter(x[1] for x in win).most_common(1)[0][0], t / 60.0)
    return dict(causes=Counter(c for _, c in causes), mass=best, mechTgt=dict(tkm), end={k: end.get(k) for k in ("reason", "result", "outcome", "target", "hpScalePct", "levyDeaths", "dmgBoss")})



def collect(runs, dest, out_json):
    dest = Path(dest); dest.mkdir(parents=True, exist_ok=True)
    fights = json.load(open(out_json)) if out_json and os.path.exists(out_json) else []
    have = {(f["run"], f["key"]) for f in fights}
    idx_lines = []
    for run in runs:
        run = Path(run); rn = run.name
        cells = {c["id"]: c for c in json.load(open(run / "cells.json"))["cells"]}
        kit = {(r["cell"], r["seed"]): r for r in csv.DictReader(open(run / "results.csv"))} if (run / "results.csv").exists() else {}
        exo, ev = {}, defaultdict(list)
        for t in sorted(glob.glob(str(run / "jobs/*/try*/out.tsv.exo.tsv"))):
            for r in rows(t): exo[r["key"]] = r
        for t in sorted(glob.glob(str(run / "jobs/*/try*/out.tsv.exo_events.tsv"))):
            for r in rows(t): ev[r["key"]].append((float(r["sec"]), r["event"], r["detail"]))
        recs = {}
        for p in glob.glob(str(run / "rec/*/*.jsonl")):
            try:
                with open(p, errors="replace") as f: h = json.loads(f.readline())
                recs[h.get("meta", {}).get("cellkey")] = p
            except Exception: pass
        for key, r in exo.items():
            if (rn, key) in have: continue
            m = re.search(r"\|t(c\d+)(?:\||$)", key); cid = m[1] if m else None
            name = cells.get(cid, {}).get("name", "")
            mm = re.search(r"meta=exo_(\S+)_(exo\w+)", name)
            army, opener = (mm[1], mm[2]) if mm else ("?", r["boss"])
            k = kit.get((cid, r["seed"]), {})
            res = r["result"].lower()
            f = dict(run=rn, key=key, army=army, opener=opener, seed=int(r["seed"]), result=res, exo=r, kit=k, events=ev.get(key, []))
            src = recs.get(key)
            if src:
                try: f["recFacts"] = rec_facts(src)
                except Exception as ex: f["recFacts"] = {"error": str(ex)}
                fn = f"{army}_{opener}_s{r['seed']}_{res}.jsonl.gz"
                with open(src, "rb") as a, gzip.open(dest / fn, "wb") as b: shutil.copyfileobj(a, b)
                f["rec"] = fn
                kills = r["killOrder"] if r["killOrder"] != "-" else "none killed"
                idx_lines.append(f"{fn}\t{ARMY.get(army, army)}, {OPEN.get(opener, opener)}, seed {r['seed']}: {res} at {r['sec']} s; mechs killed: {kills}; phases {r['phases']}; levy deaths {r['deaths']}/{r['n']}")
            fights.append(f)
    if idx_lines:
        with open(dest / "index.txt", "a") as ix: ix.write("\n".join(idx_lines) + "\n")
    if out_json: json.dump(fights, open(out_json, "w"), indent=0)
    print(len(fights), "fights;", len(idx_lines), "recordings written to", dest)


def fnum(x):
    try: return float(x)
    except Exception: return None


def med(xs):
    xs = [x for x in xs if x is not None]
    return statistics.median(xs) if xs else None


def fmt(x, nd=0):
    return "-" if x is None else (f"{x:,.{nd}f}")


def parse_phase_stats(s):
    out = {}
    for part in s.split():
        lab, rest = part.split(":", 1)
        d = {}
        for kv in rest.split(","):
            m = re.match(r"([a-zA-Z]+)([\d.\-]+)", kv.rstrip("%s"))
            if m: d[m[1]] = fnum(m[2])
        d["sec"] = fnum(rest.split("s,")[0])
        out[lab] = d
    return out


def tables(fjson):
    F = json.load(open(fjson))
    cells = defaultdict(list)
    for f in F: cells[(f["army"], f["opener"])].append(f)
    order = [a for a in ARMY if any(k[0] == a for k in cells)]
    print("## Results by cell\n")
    print("| army | Draedon's first mech | fights | wins | fight length s, median (range) | mechs killed per fight | kill order seen | furthest phase | levy deaths, median | first levy death s | pool left in losses, median |")
    print("|---|---|---|---|---|---|---|---|---|---|---|")
    for a in order:
        for o in OPEN:
            fs = cells.get((a, o));
            if not fs: continue
            w = sum(f["result"] == "win" for f in fs)
            secs = [fnum(f["exo"]["sec"]) for f in fs]
            kills = [0 if f["exo"]["killOrder"] == "-" else len(f["exo"]["killOrder"].split(";")) for f in fs]
            ko = Counter(";".join(x.split("@")[0] for x in f["exo"]["killOrder"].split(";")) for f in fs if f["exo"]["killOrder"] != "-")
            far = Counter(furthest(f) for f in fs)
            left = []
            for f in fs:
                if f["result"] == "win": continue
                e = f["exo"]; tot = sum(max(0, fnum(e[p + "_max"]) or 0) for p in "ATW"); lf = sum(max(0, fnum(e[p + "_left"]) or 0) for p in "ATW")
                left.append(100 * lf / tot if tot else None)
            fd = [fnum(f["kit"].get("firstDeathSec")) for f in fs]
            print(f"| {ARMY.get(a, a)} | {OPEN[o]} | {len(fs)} | **{w}/{len(fs)}** | {fmt(med(secs))} ({fmt(min(secs))}-{fmt(max(secs))}) | {statistics.mean(kills):.1f} | {', '.join(f'{k.replace(chr(59), ' > ')} x{v}' for k, v in ko.most_common(3)) or '-'} | {', '.join(f'{k} x{v}' for k, v in far.most_common(3))} | {fmt(med([fnum(f['exo']['deaths']) for f in fs]))}/{fs[0]['exo']['n']} | {fmt(med(fd), 1)} | {fmt(med(left))}% |")
    print("\n## Army damage per mech (DPS while that mech could be hurt, median per fight) and how long each mech was open\n")
    print("| army | first mech | Ares DPS | Ares open s | Thanatos DPS | Thanatos open s | Twins DPS | Twins open s | whole-fight army DPS | Thanatos hits on closed armour |")
    print("|---|---|---|---|---|---|---|---|---|---|")
    for a in order:
        for o in OPEN:
            fs = cells.get((a, o))
            if not fs: continue
            def m(col): return med([fnum(f["exo"][col]) for f in fs])
            tot = med([sum((fnum(f["exo"][p + "_dmg"]) or 0) for p in "ATW") / max(1, fnum(f["exo"]["sec"]) - (fnum(f["exo"]["firstMechSec"]) or 0)) for f in fs])
            closed = [100 * fnum(f["exo"]["thanClosedHits"]) / max(1, fnum(f["exo"]["thanClosedHits"]) + fnum(f["exo"]["thanOpenHits"])) for f in fs if (fnum(f["exo"]["thanClosedHits"]) or 0) + (fnum(f["exo"]["thanOpenHits"]) or 0) > 0]
            print(f"| {ARMY.get(a, a)} | {OPEN[o]} | {fmt(m('A_dps'))} | {fmt(m('A_openSec'))} | {fmt(m('T_dps'))} | {fmt(m('T_openSec'))} | {fmt(m('W_dps'))} | {fmt(m('W_openSec'))} | {fmt(tot)} | {fmt(med(closed))}% |")
    print("\n## Who the mechs and Draedon went for, and what the levies were doing (share of samples, median per fight)\n")
    print("| army | first mech | mechs on a levy | Draedon on a levy | Draedon from the army, tiles | levies on a mech they can hurt | on an immune mech | on Draedon | no target while a mech is open | not engaged while a mech is open | >100 tiles from any open mech | levies off the arena (fights) | ms per tick |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    for a in order:
        for o in OPEN:
            fs = cells.get((a, o))
            if not fs: continue
            def tg(f):
                vs = []
                for p in "ATW":
                    t = f["exo"][p + "_tgt(none/cmd/levy/spite)" if p == "A" else p + "_tgt"]
                    if t != "-": vs.append(fnum(t.split("/")[2]) + fnum(t.split("/")[3]))
                return statistics.mean(vs) if vs else None
            def m(col): return med([fnum(f["exo"][col]) for f in fs])
            dl = med([fnum(f["exo"]["draedonTgt(levy/cmd/none)"].split("/")[0]) for f in fs if f["exo"]["draedonTgt(levy/cmd/none)"] != "-"])
            off = sum(int(f["exo"]["offArenaLevies"]) > 0 for f in fs)
            print(f"| {ARMY.get(a, a)} | {OPEN[o]} | {fmt(med([tg(f) for f in fs]))}% | {fmt(dl)}% | {fmt(m('draedonDistArmyTiles'))} | {fmt(m('lvTgtOpen%'))}% | {fmt(m('lvTgtImmune%'))}% | {fmt(m('lvTgtDraedon%'))}% | {fmt(m('lvNoTgtWhileOpen%'))}% | {fmt(m('lvIdle%'))}% | {fmt(m('lvFar100%'))}% | {off}/{len(fs)} | {fmt(m('msPerTick'), 1)} |")
    print("\n## By phase (all fights of an army pooled): army DPS, idle levies, levies on an immune mech, ms per tick\n")
    print("Phase labels: one letter per mech present (A Ares, T Thanatos, W the twins); upper case = can be hurt, lower case = passive and immune (Draedon's interlude for that mech).\n")
    print("| army | phase | fights that reached it | seconds in it (median) | army DPS (median) | levies with no target or not engaged % | levies targeting an immune mech % | engaged % | ms per tick |")
    print("|---|---|---|---|---|---|---|---|---|")
    for a in order:
        ph = defaultdict(list)
        for o in OPEN:
            for f in cells.get((a, o), []):
                for lab, d in parse_phase_stats(f["exo"]["phaseStats"]).items(): ph[lab].append(d)
        for lab, ds in sorted(ph.items(), key=lambda kv: -len(kv[1])):
            if lab in ("pre", "-") or len(ds) < 2: continue
            print(f"| {ARMY.get(a, a)} | {lab} | {len(ds)} | {fmt(med([d.get('sec') for d in ds]), 1)} | {fmt(med([d.get('dps') for d in ds]))} | {fmt(med([d.get('idle') for d in ds]))} | {fmt(med([d.get('immT') for d in ds]))} | {fmt(med([d.get('eng') for d in ds]))} | {fmt(med([d.get('ms') for d in ds]), 1)} |")
    print("\n## What killed levies (the killing hit in the recordings, all fights of an army pooled) and the worst 2 seconds\n")
    for a in order:
        c = Counter(); mass = []
        for o in OPEN:
            for f in cells.get((a, o), []):
                rf = f.get("recFacts") or {}
                for k, v in (rf.get("causes") or {}).items(): c[k] += v
                if rf.get("mass"): mass.append(rf["mass"])
        tot = sum(c.values())
        if tot:
            mc = Counter(m[1] for m in mass if m[0] >= 8)
            print(f"- {ARMY.get(a, a)} ({tot} deaths): " + ", ".join(f"{k} {100 * v / tot:.0f}%" for k, v in c.most_common(8))
                  + f". Worst 2 s per fight, median {med([m[0] for m in mass]):.0f} levies (max {max(m[0] for m in mass)}); fights where 8+ died inside 2 s: {sum(m[0] >= 8 for m in mass)}/{len(mass)}, by " + (", ".join(f"{k} x{v}" for k, v in mc.most_common(3)) or "-"))
    print("\n## What killed levies, harness attribution (last attacker NPC; env = no NPC)\n")
    for a in order:
        c = Counter()
        for o in OPEN:
            for f in cells.get((a, o), []):
                for kv in f["exo"]["deathBy"].split():
                    k, _, v = kv.rpartition("="); c[k.split("/", 1)[-1] or "no attacker recorded"] += int(v)
        tot = sum(c.values())
        print(f"- {ARMY.get(a, a)} ({tot} deaths): " + ", ".join(f"{k.replace('_', ' ') or 'unknown'} {100 * v / tot:.0f}%" for k, v in c.most_common(7)))
    print("\n## Top weapons by damage to the mechs (summed over the army's fights; per levy carrying it)\n")
    for a in order:
        w = Counter(); carriers = Counter()
        for o in OPEN:
            for f in cells.get((a, o), []):
                for kv in (f["kit"].get("weaponDmg") or "").split():
                    k, _, v = kv.rpartition("="); w[k] += float(v)
        print(f"- {ARMY.get(a, a)}: " + ", ".join(f"{k.replace('C:', '')} {v / 1e6:.2f}M" for k, v in w.most_common(10)))


PHASES = {1: "1 first mech alone", 2: "2 other two arrive", 3: "3 all three", 4: "4 one berserk, two leave", 5: "5 one down, two fight", 6: "6 one berserk, one leaves", 7: "7 last mech"}


def phase_of(label, deaths):
    """Calamity's phase number (AresBody's own comments) from a label (A/T/W present, upper case = can be hurt) and the mechs already killed."""
    if label in ("pre", "-", ""): return 0
    up = sum(ch.isupper() for ch in label); n = len(label)
    if deaths >= 2: return 7
    if deaths == 1: return 5 if up >= 2 else 6
    if n == 1: return 1
    if up == 3: return 3
    if up == 2: return 2
    return 4


def phase_seq(f):
    """[(sec, phase number)] from the phase labels and the kill times; brief flickers (under 1 s) are kept, the furthest is what counts."""
    e = f["exo"]
    kills = [] if e["killOrder"] == "-" else sorted(float(x.split("@")[1]) for x in e["killOrder"].split(";"))
    out = []
    for p in e["phases"].split():
        lab, _, t = p.rpartition("@"); t = float(t)
        d = sum(k <= t + 0.05 for k in kills)
        out.append((t, phase_of(lab, d)))
    return out


def furthest(f):
    if f["result"] == "win": return "won"
    ph = max([p for _, p in phase_seq(f)] or [0])
    return "phase " + PHASES.get(ph, str(ph))


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("runs", nargs="*"); ap.add_argument("--dest"); ap.add_argument("--json"); ap.add_argument("--tables")
    a = ap.parse_args()
    if a.tables: tables(a.tables)
    else: collect(a.runs, a.dest, a.json)

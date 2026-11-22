#!/usr/bin/env python3
"""labcompare: OLD vs NEW tables, and the recordings copied out as gzip.
  compare.py tables  [--prehm-matched RUN] [--prehm-shipped RUN] [--bat-matched RUN] ... [--hm-shipped RUN]   -> markdown tables on stdout
  compare.py finish  --dest DIR [--drop-raw] RUN [RUN ...]                    -> <DIR>/<run>/<boss>_<mode>_<cell>_s<seed>_<result>.jsonl.gz + index.txt, <run>/tgt.json

OLD data:
  pre-hardmode = boss-lab lab 1 (Levy 2.0.12/2.0.13; old_prehm.py reads the exact fights lab 1's main table counted)
  hardmode     = hmbattery2 (Levy 2.0.66): levy-mod/lab/hmbattery2/sweep_fights.csv and battery_fights.csv
Metrics (same definition on both sides of every row):
  win % with a Wilson 95% interval; Fisher exact p of the two win counts; kill time = median of the wins; deaths = levy deaths per fight (mean);
  army DPS = damage the army dealt to the boss per fight-second (pre-HM and battery: the harness dmgBoss; sweep: boss life removed per second,
  (bossMax - bossLeft) / sec, because the OLD sweep rows carry no dmgBoss); boss on levy % = share of samples the boss targets a levy
  (pre-HM: the harness per-tick sample; hardmode: the recording's boss-target field, as hmbattery2 read it).
  Pre-HM: a TIMEOUT whose boss moved more than 100,000 px is a boss that FLED (Skeletron's leave state; lab 2 anomaly A1); never a win."""
import argparse, csv, gzip, json, math, re, shutil, statistics, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE)); sys.path.insert(0, str(HERE.parents[1] / "Levy" / "tools" / "fightviewer"))
import old_prehm

BOSS = {"kslime": "King Slime", "ds": "Desert Scourge", "eye": "Eye of Cthulhu", "eow": "Eater of Worlds", "brain": "Brain of Cthulhu", "qb": "Queen Bee", "skel": "Skeletron",
        "destroyer": "The Destroyer", "aqua": "Aquatic Scourge", "wof": "Wall of Flesh", "qslime": "Queen Slime", "twins": "The Twins", "prime": "Skeletron Prime",
        "brim": "Brimstone Elemental", "cryo": "Cryogen", "cal": "Calamitas Clone", "plant": "Plantera", "levi": "Leviathan and Anahita", "aureus": "Astrum Aureus",
        "golem": "Golem", "fish": "Duke Fishron", "pbg": "The Plaguebringer Goliath", "eol": "Empress of Light", "cultist": "Lunatic Cultist", "ravager": "Ravager",
        "ml": "Moon Lord", "crab": "Crabulon", "hive": "Hive Mind", "perf": "The Perforators", "deer": "Deerclops"}
MODE = {"exp": "Expert", "rev": "Rev", "death": "Death", "mdeath": "Master+Death"}
PAIR = {"rev": "Expert+Rev", "mdeath": "Master+Death"}
SWEEP_ORDER = ["crab", "hive", "perf", "deer", "wof", "qslime", "cryo", "twins", "aqua", "destroyer", "brim", "prime", "cal", "plant", "levi", "aureus", "golem", "fish", "pbg", "eol", "cultist", "ravager", "ml"]


def f(x, d=float("nan")):
    try: return float(x)
    except Exception: return d


def wilson(w, n, z=1.96):
    if n == 0: return (float("nan"),) * 2
    p = w / n; den = 1 + z * z / n; c = (p + z * z / (2 * n)) / den; h = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / den
    return max(0.0, c - h), min(1.0, c + h)


def fisher(a, b, c, d):
    """two-sided Fisher exact p for [[a, b], [c, d]]."""
    n = a + b + c + d; r1 = a + b; c1 = a + c
    def pr(x): return math.comb(r1, x) * math.comb(n - r1, c1 - x) / math.comb(n, c1)
    p0 = pr(a); lo, hi = max(0, c1 - (n - r1)), min(r1, c1)
    return min(1.0, sum(pr(x) for x in range(lo, hi + 1) if pr(x) <= p0 * (1 + 1e-9)))


def summ(rows):
    """rows: dicts with result (win|loss|timeout|fled|despawn), sec, deaths, dps, tgt."""
    n = len(rows); w = [r for r in rows if r["result"] == "win"]
    lo, hi = wilson(len(w), n)
    tt = [r["sec"] for r in w]
    other = {}
    for r in rows:
        if r["result"] != "win": other[r["result"]] = other.get(r["result"], 0) + 1
    def mean(k):
        xs = [r[k] for r in rows if not math.isnan(r[k])]
        return sum(xs) / len(xs) if xs else float("nan")
    left = [r["left"] for r in rows if r["result"] != "win" and not math.isnan(r.get("left", float("nan")))]
    return dict(n=n, w=len(w), lo=lo, hi=hi, ttk=statistics.median(tt) if tt else float("nan"), ttk_lo=min(tt) if tt else float("nan"), ttk_hi=max(tt) if tt else float("nan"),
                deaths=mean("deaths"), dps=mean("dps"), tgt=mean("tgt"), other=other, left=statistics.median(left) if left else float("nan"))


def fmt_win(s):
    if not s["n"]: return "-"
    return f"{s['w']}/{s['n']} = {100 * s['w'] / s['n']:.0f}% ({100 * s['lo']:.0f}-{100 * s['hi']:.0f})"


def fmt_other(s):
    t = ", ".join(f"{v} {k}" for k, v in sorted(s["other"].items())) or "-"
    return t + (f"; boss left {s['left']:.0f}%" if s["other"] and not math.isnan(s["left"]) else "")


def left_pct(r, cap=float("inf")):
    """boss life left as % of its max (non-wins). Pre-HM passes cap=50000: lab 1's early Desert Scourge rows summed every segment into bossMax."""
    bm, bl = f(r.get("bossMax")), f(r.get("bossLeft"))
    return 100 * bl / bm if bm and bm > 0 and bm < cap and not math.isnan(bl) else float("nan")


def g(x, nd=0):
    return "-" if x is None or (isinstance(x, float) and math.isnan(x)) else f"{x:.{nd}f}"


# ------------------------------------------------------------------ NEW run readers
def run_rows(run):
    """-> list of (cell dict, out.tsv row) for a levylab run dir (last try wins for a fight)."""
    run = Path(run); cells = {c["id"]: c for c in json.loads((run / "cells.json").read_text())["cells"]}
    by = {}
    for p in sorted(run.glob("jobs/*/try*/out.tsv")):
        for r in csv.DictReader(open(p, errors="replace"), delimiter="\t"):
            m = re.search(r"\|t(c\d+)(?:\||$)", r["key"])
            if m and r.get("kind") == "fight": by[(m[1], int(r["seed"]))] = (cells[m[1]], r)
    return [by[k] for k in sorted(by, key=lambda k: (int(k[0][1:]), k[1]))]


def classify(r):
    res = r["result"].lower()
    if res == "timeout" and f(r.get("bossMoved"), 0) > 100000: return "fled"
    return res


def prehm_row(r):
    return dict(result=classify(r), sec=f(r["sec"]), deaths=f(r["deaths"]), dps=f(r.get("dmgPerSec")), tgt=f(r.get("tgtLevyPct")), seed=int(r["seed"]), left=left_pct(r, 50000))


_tgt_cache = {}
def rec_tgt(run, cid, seed):
    """boss-target-on-levy % from the fight's recording (make_site.summarize 'pl'), as hmbattery2's collector read it.
    `finish` stores these in <run>/tgt.json before the raw recordings are removed; that file is read first."""
    k = (str(run), cid, seed)
    if k in _tgt_cache: return _tgt_cache[k]
    tj = Path(run) / "tgt.json"
    if tj.exists():
        d = json.loads(tj.read_text())
        if f"{cid}:{seed}" in d: _tgt_cache[k] = f(d[f"{cid}:{seed}"]); return _tgt_cache[k]
    import make_site
    best = None
    for p in Path(run).glob("rec/*/*.jsonl"):
        try: hdr = json.loads(open(p).readline())
        except Exception: continue
        ck = hdr.get("meta", {}).get("cellkey", "")
        if re.search(rf"\|t{cid}(?:\||$)", ck) and int(hdr["meta"].get("seed", 0)) == seed and (best is None or p.stat().st_size > best.stat().st_size): best = p
    v = float("nan")
    if best is not None:
        try: v = f(make_site.summarize(make_site.load(str(best))).get("pl"))
        except Exception as e: print("summarize failed", best, e, file=sys.stderr)
    _tgt_cache[k] = v
    return v


def hm_new(runs):
    """runs: one run dir, or several joined by commas (the matched sweep was split by the wall-time cut)."""
    out = {}
    for run, c, r in [(rd, c, r) for rd in str(runs).split(",") for c, r in run_rows(rd)]:
        m = re.search(r"meta=(\w+?)_(on|mid)_(rev|mdeath)_n(\d+)_T", c["name"])
        if not m: continue
        slug, kit, pair = m[1], m[2], m[3]; sec = f(r["sec"]); bm, bl = f(r["bossMax"], 0), f(r["bossLeft"], 0)
        out.setdefault((slug, pair), []).append(dict(result=r["result"].lower(), sec=sec, deaths=f(r["deaths"]), dps=(bm - bl) / sec if sec > 0 else float("nan"),
                                                     tgt=rec_tgt(run, re.search(r"\|t(c\d+)", r["key"])[1], int(r["seed"])), seed=int(r["seed"]), kit=kit,
                                                     left=left_pct(r)))
    return out


def bat_new(run):
    rows = []
    for c, r in run_rows(run):
        if "meta=hmb_destroyer_baseline" not in c["name"]: continue
        sec = f(r["sec"])
        rows.append(dict(result=r["result"].lower(), sec=sec, deaths=f(r["deaths"]), dps=f(r["dmgBoss"]) / sec if sec > 0 else float("nan"),
                         tgt=rec_tgt(run, re.search(r"\|t(c\d+)", r["key"])[1], int(r["seed"])), seed=int(r["seed"]), left=left_pct(r)))
    return rows


# ------------------------------------------------------------------ OLD readers
def hm_old():
    out = {}
    for r in csv.DictReader(open(HERE.parent / "hmbattery2" / "sweep_fights.csv")):
        sec = f(r["sec"]); bm, bl = f(r["bossMax"], 0), f(r["bossLeft"], 0)
        out.setdefault((r["slug"], r["pair"]), []).append(dict(result=r["result"].lower(), sec=sec, deaths=f(r["deaths"]), dps=(bm - bl) / sec if sec > 0 else float("nan"),
                                                               tgt=f(r["tgtLevyPct"]), seed=int(r["seed"]), kit=r["kit"], left=left_pct(r)))
    return out


def bat_old():
    rows = []
    for r in csv.DictReader(open(HERE.parent / "hmbattery2" / "battery_fights.csv")):
        if r["cell"] != "baseline" or r["boss"] != "destroyer": continue
        sec = f(r["sec"])
        rows.append(dict(result=r["result"].lower(), sec=sec, deaths=f(r["deaths"]), dps=f(r["dmgBoss"]) / sec if sec > 0 else float("nan"), tgt=f(r["tgtLevyPct"]), seed=int(r["seed"]), left=left_pct(r)))
    return rows


# ------------------------------------------------------------------ tables
LEVIES = {"wof": 28, "deer": 28, "crab": 19, "hive": 22, "perf": 24}
ARMS = ("matched", "shipped")


def rng(s): return "-" if math.isnan(s["ttk"]) else f"{s['ttk']:.0f} ({s['ttk_lo']:.0f}-{s['ttk_hi']:.0f})"


def pval(so, sn): return fisher(so["w"], so["n"] - so["w"], sn["w"], sn["n"] - sn["w"]) if so["n"] and sn["n"] else float("nan")


def emit(title, rows, old_label):
    """rows: list of (boss, mode, levies, old rows, {arm: new rows})."""
    arms = [a for a in ARMS if any(r[4].get(a) for r in rows)]
    print(f"\n### {title}: win rate and kill time\n")
    h = ["boss", "mode", "levies", f"{old_label} win rate (95% CI)"]
    for a in arms: h += [f"NEW {a} win rate (95% CI)", f"p vs OLD"]
    h += [f"{old_label} kill s"] + [f"NEW {a} kill s" for a in arms]
    print("| " + " | ".join(h) + " |\n|" + "---|" * len(h))
    for boss, mode, n, o, nw in rows:
        so = summ(o); c = [boss, mode, str(n), fmt_win(so)]
        for a in arms:
            sn = summ(nw.get(a, [])); c += [fmt_win(sn), g(pval(so, sn), 2) if sn["n"] else "-"]
        c += [rng(so)] + [rng(summ(nw.get(a, []))) for a in arms]
        print("| " + " | ".join(c) + " |")
    print(f"\n### {title}: deaths, army DPS, boss target\n")
    h = ["boss", "mode", f"{old_label} deaths/fight"] + [f"NEW {a} deaths/fight" for a in arms] + [f"{old_label} army DPS"] + [f"NEW {a} army DPS" for a in arms]
    h += [f"{old_label} boss on levy %"] + [f"NEW {a} boss on levy %" for a in arms] + [f"non-wins {old_label}"] + [f"non-wins NEW {a}" for a in arms]
    print("| " + " | ".join(h) + " |\n|" + "---|" * len(h))
    for boss, mode, n, o, nw in rows:
        so = summ(o); sns = [summ(nw.get(a, [])) for a in arms]
        c = [boss, mode, g(so["deaths"], 1)] + [g(x["deaths"], 1) for x in sns] + [g(so["dps"])] + [g(x["dps"]) for x in sns]
        c += [g(so["tgt"])] + [g(x["tgt"]) for x in sns] + [fmt_other(so)] + [fmt_other(x) for x in sns]
        print("| " + " | ".join(c) + " |")


def prehm_rows(runs):
    old, _ = old_prehm.rows(); new = {a: {} for a in runs}
    for a, run in runs.items():
        for c, r in run_rows(run): new[a].setdefault((r["boss"], r["mode"], int(r["n"])), []).append(prehm_row(r))
    rows, tot = [], ([], {a: [] for a in runs})
    for k in __import__("mkspecs").CELLS:
        o = [prehm_row(r) for r in old.get(k, [])]; nw = {a: new[a].get(k, []) for a in runs}
        if not any(nw.values()): continue
        tot[0].extend(o); [tot[1][a].extend(v) for a, v in nw.items()]
        rows.append((BOSS[k[0]], MODE[k[1]], k[2], o, nw))
    rows.append(("**all cells**", "-", "-", tot[0], tot[1]))
    return rows, old, new


def prehm_tables(runs):
    rows, old, new = prehm_rows(runs)
    emit("Pre-hardmode vs boss-lab lab 1 (Levy 2.0.13)", rows, "OLD")
    print("\nSame seeds only (the seeds lab 1 ran; the armies are identical seed for seed; lab 1 ran some seeds twice):\n")
    arms = [a for a in ARMS if a in runs]
    print("| boss | mode | seeds | OLD wins | " + " | ".join(f"NEW {a} wins" for a in arms) + " | OLD deaths | " + " | ".join(f"NEW {a} deaths" for a in arms) + " |\n|" + "---|" * (5 + 2 * len(arms)))
    for k in __import__("mkspecs").CELLS:
        o = [prehm_row(r) for r in old.get(k, [])]
        if not o or not any(new[a].get(k) for a in arms): continue
        seeds = sorted({r["seed"] for r in o}); ps = {a: [r for r in new[a].get(k, []) if r["seed"] in seeds] for a in arms}
        print(f"| {BOSS[k[0]]} | {MODE[k[1]]} | {','.join(map(str, seeds))} | {sum(r['result'] == 'win' for r in o)}/{len(o)} | " + " | ".join(f"{sum(r['result'] == 'win' for r in ps[a])}/{len(ps[a])}" for a in arms)
              + f" | {g(summ(o)['deaths'], 1)} | " + " | ".join(g(summ(ps[a])["deaths"], 1) for a in arms) + " |")


def hm_tables(runs):
    old = hm_old(); new = {a: hm_new(r) for a, r in runs.items()}
    rows, tot = [], {p: ([], {a: [] for a in runs}) for p in PAIR}
    for slug in SWEEP_ORDER:
        for pair in PAIR:
            nw = {a: new[a].get((slug, pair), []) for a in runs}
            if not any(nw.values()): continue
            o = old.get((slug, pair), []); tot[pair][0].extend(o); [tot[pair][1][a].extend(v) for a, v in nw.items()]
            rows.append((BOSS[slug], PAIR[pair], LEVIES.get(slug, 34), o, nw))
    for pair in PAIR: rows.append(("**all bosses**", PAIR[pair], "-", tot[pair][0], tot[pair][1]))
    if "matched" in runs:   # OLD is cut to the seeds the matched arm ran for that boss (all 4, or 1-2 after the wall-time cut), so each row compares the same armies
        mrows, mt = [], {p: ([], []) for p in PAIR}
        for b, m, n, o, nw in rows[:-len(PAIR)]:
            sd = {r["seed"] for r in nw.get("matched", [])}; oo = [r for r in o if r["seed"] in sd]
            pk = next(k for k, v in PAIR.items() if v == m); mt[pk][0].extend(oo); mt[pk][1].extend(nw.get("matched", []))
            mrows.append((b + ("" if len(sd) == 4 else f" (seeds {','.join(map(str, sorted(sd)))})"), m, n, oo, {"matched": nw.get("matched", [])}))
        for pk, pv in PAIR.items(): mrows.append(("**all bosses**", pv, "-", mt[pk][0], {"matched": mt[pk][1]}))
        emit("Hardmode broad sweep vs hmbattery2 (Levy 2.0.66), matched arm; 3 on-tier seeds + 1 one-tier-below seed per boss and mode (the bosses after Plantera: on-tier seeds 1-2)", mrows, "OLD")
    if "shipped" in runs:   # the shipped arm ran fewer seeds: every column here is cut to the seeds it ran, so the three are the same armies
        srows = []
        for b, m, n, o, nw in rows:
            sd = {r["seed"] for r in nw.get("shipped", [])}
            srows.append((b, m, n, [r for r in o if r["seed"] in sd], {a: [r for r in nw.get(a, []) if r["seed"] in sd] for a in runs}))
        emit("Hardmode broad sweep, the seeds the shipped arm ran (on-tier seeds 1-2), OLD vs matched vs shipped", srows, "OLD")


def bat_tables(runs):
    emit("Destroyer battery baseline vs hmbattery2 (Levy 2.0.66); Master+Death, 34 levies, one fixed army, seeds 1-40",
         [("The Destroyer", "Master+Death", 34, bat_old(), {a: bat_new(r) for a, r in runs.items()})], "OLD")


# ------------------------------------------------------------------ recordings
INDEX_HEAD = ("labcompare recordings (FightRecorder format 12, gzip; Levy 3.1.8, Calamity 2.2.2, tML 2026.10). One folder per run:\n"
              "  prehm_matched / prehm_shipped = pre-hardmode cells vs boss-lab lab 1 (cmd=far, surface arena, gear=mod; matched = old enemy rules, shipped = as shipped)\n"
              "  bat_matched / bat_shipped     = Destroyer battery baseline, Master+Death, one fixed 34-levy army (vs hmbattery2)\n"
              "  hm_matched / hm_shipped       = hardmode broad sweep vs hmbattery2 (_on_ = on-tier kit, _mid_ = one tier below; rev = Expert+Rev, mdeath = Master+Death)\n"
              "Columns: file, boss, mode, seed, result, fight seconds, levy deaths.\n\n")


def rec_name(c, r):
    mm = re.search(r"meta=(\w+?)_(on|mid)_(rev|mdeath)_n(\d+)", c["name"]); mb = re.search(r"meta=hmb_(\w+?)_(\w+?)_(mdeath)", c["name"])
    if mm: return f"{mm[1]}_{mm[3]}_{mm[2]}_n{mm[4]}", r["result"].lower()
    if mb: return f"{mb[1]}_{mb[3]}_battery-{mb[2]}_n{r['n']}", r["result"].lower()
    return f"{r['boss']}_{r['mode']}_n{r['n']}_cmdfar", classify(r)


def finish(dest, run, drop_raw=False):
    """for one run: cache the boss-target values (tgt.json), gzip every recording into <dest>/<run name>/, write its index part, rebuild <dest>/index.txt;
    with drop_raw the raw .jsonl recordings are deleted afterwards (the gzip copies are the keepers)."""
    import make_site
    dest = Path(dest); run = Path(run); sub = dest / run.name; sub.mkdir(parents=True, exist_ok=True)
    rows = {(re.search(r"\|t(c\d+)", r["key"])[1], int(r["seed"])): (c, r) for c, r in run_rows(run)}
    best = {}
    for p in sorted(run.glob("rec/*/*.jsonl")):
        try: hdr = json.loads(open(p).readline())
        except Exception: continue
        m = re.search(r"\|t(c\d+)(?:\||$)", hdr.get("meta", {}).get("cellkey", ""))
        if not m: continue
        k = (m[1], int(hdr["meta"].get("seed", 0)))
        if k not in best or p.stat().st_size > best[k].stat().st_size: best[k] = p
    tj = run / "tgt.json"; tgt = json.loads(tj.read_text()) if tj.exists() else {}
    part = []
    for k, p in sorted(best.items(), key=lambda kv: (int(kv[0][0][1:]), kv[0][1])):
        if k not in rows: continue
        c, r = rows[k]; nm, res = rec_name(c, r)
        if f"{k[0]}:{k[1]}" not in tgt:
            try: tgt[f"{k[0]}:{k[1]}"] = make_site.summarize(make_site.load(str(p))).get("pl")
            except Exception as e: print("summarize failed", p, e, file=sys.stderr)
        fn = f"{nm}_s{r['seed']}_{res}.jsonl.gz"
        with open(p, "rb") as src, gzip.open(sub / fn, "wb", compresslevel=6) as z: shutil.copyfileobj(src, z)
        part.append(f"{run.name}/{fn}\t{BOSS.get(r['boss'], r['boss'])}\t{MODE.get(r['mode'], r['mode'])}\tseed {r['seed']}\t{res}\t{f(r['sec']):.0f} s\t{r['deaths']}/{r['n']} levy deaths")
    tj.write_text(json.dumps(tgt))
    (sub / "index.part").write_text("\n".join(part) + "\n")
    parts = [q.read_text().rstrip("\n") for q in sorted(dest.glob("*/index.part"))]
    (dest / "index.txt").write_text(INDEX_HEAD + "\n".join(x for x in parts if x) + "\n")
    if drop_raw:
        for q in run.glob("rec/*/*.jsonl"): q.unlink()
    print(len(part), "recordings ->", sub)


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); sp = ap.add_subparsers(dest="cmd", required=True)
    t = sp.add_parser("tables")
    for k in ("prehm", "bat", "hm"):
        for arm in ARMS: t.add_argument(f"--{k}-{arm}")
    r = sp.add_parser("finish"); r.add_argument("--dest", required=True); r.add_argument("--drop-raw", action="store_true"); r.add_argument("runs", nargs="+")
    a = ap.parse_args()
    if a.cmd == "tables":
        for k, fn in (("prehm", prehm_tables), ("bat", bat_tables), ("hm", hm_tables)):
            runs = {arm: getattr(a, f"{k}_{arm}") for arm in ARMS if getattr(a, f"{k}_{arm}")}
            if runs: fn(runs)
    else:
        for run in a.runs: finish(a.dest, run, a.drop_raw)

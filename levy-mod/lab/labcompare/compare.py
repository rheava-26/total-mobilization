#!/usr/bin/env python3
"""labcompare: OLD vs NEW tables, and the recordings copied out as gzip.
  compare.py tables  --prehm RUN [--prehm-sp1 RUN] --hm RUN --bat RUN        -> markdown tables on stdout
  compare.py recs    --dest DIR RUN [RUN ...]                                -> <DIR>/<set>/<boss>_<mode>_<cell>_s<seed>_<result>.jsonl.gz + index.txt

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
    return dict(n=n, w=len(w), lo=lo, hi=hi, ttk=statistics.median(tt) if tt else float("nan"), ttk_lo=min(tt) if tt else float("nan"), ttk_hi=max(tt) if tt else float("nan"),
                deaths=mean("deaths"), dps=mean("dps"), tgt=mean("tgt"), other=other)


def fmt_win(s):
    if not s["n"]: return "-"
    return f"{s['w']}/{s['n']} = {100 * s['w'] / s['n']:.0f}% ({100 * s['lo']:.0f}-{100 * s['hi']:.0f})"


def fmt_other(s):
    return ", ".join(f"{v} {k}" for k, v in sorted(s["other"].items())) or "-"


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
    return dict(result=classify(r), sec=f(r["sec"]), deaths=f(r["deaths"]), dps=f(r.get("dmgPerSec")), tgt=f(r.get("tgtLevyPct")), seed=int(r["seed"]))


_tgt_cache = {}
def rec_tgt(run, cid, seed):
    """boss-target-on-levy % from the fight's recording (make_site.summarize 'pl'), as hmbattery2's collector read it."""
    k = (str(run), cid, seed)
    if k in _tgt_cache: return _tgt_cache[k]
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


def hm_new(run):
    out = {}
    for c, r in run_rows(run):
        m = re.search(r"meta=(\w+?)_(on|mid)_(rev|mdeath)_n(\d+)_T", c["name"])
        if not m: continue
        slug, kit, pair = m[1], m[2], m[3]; sec = f(r["sec"]); bm, bl = f(r["bossMax"], 0), f(r["bossLeft"], 0)
        out.setdefault((slug, pair), []).append(dict(result=r["result"].lower(), sec=sec, deaths=f(r["deaths"]), dps=(bm - bl) / sec if sec > 0 else float("nan"),
                                                     tgt=rec_tgt(run, re.search(r"\|t(c\d+)", r["key"])[1], int(r["seed"])), seed=int(r["seed"]), kit=kit,
                                                     contact=sum(f(r.get(k), 0) for k in ("takenBoss",)), proj=float("nan")))
    return out


def bat_new(run):
    rows = []
    for c, r in run_rows(run):
        if "meta=hmb_destroyer_baseline" not in c["name"]: continue
        sec = f(r["sec"])
        rows.append(dict(result=r["result"].lower(), sec=sec, deaths=f(r["deaths"]), dps=f(r["dmgBoss"]) / sec if sec > 0 else float("nan"),
                         tgt=rec_tgt(run, re.search(r"\|t(c\d+)", r["key"])[1], int(r["seed"])), seed=int(r["seed"])))
    return rows


# ------------------------------------------------------------------ OLD readers
def hm_old():
    out = {}
    for r in csv.DictReader(open(HERE.parent / "hmbattery2" / "sweep_fights.csv")):
        sec = f(r["sec"]); bm, bl = f(r["bossMax"], 0), f(r["bossLeft"], 0)
        out.setdefault((r["slug"], r["pair"]), []).append(dict(result=r["result"].lower(), sec=sec, deaths=f(r["deaths"]), dps=(bm - bl) / sec if sec > 0 else float("nan"),
                                                               tgt=f(r["tgtLevyPct"]), seed=int(r["seed"]), kit=r["kit"]))
    return out


def bat_old():
    rows = []
    for r in csv.DictReader(open(HERE.parent / "hmbattery2" / "battery_fights.csv")):
        if r["cell"] != "baseline" or r["boss"] != "destroyer": continue
        sec = f(r["sec"])
        rows.append(dict(result=r["result"].lower(), sec=sec, deaths=f(r["deaths"]), dps=f(r["dmgBoss"]) / sec if sec > 0 else float("nan"), tgt=f(r["tgtLevyPct"]), seed=int(r["seed"])))
    return rows


# ------------------------------------------------------------------ tables
HDR = "| boss | mode | levies | OLD win rate (95% CI) | NEW win rate (95% CI) | Fisher p | OLD kill s median (range) | NEW kill s median (range) | OLD deaths/fight | NEW deaths/fight | OLD army DPS | NEW army DPS | OLD boss on levy % | NEW boss on levy % | OLD other results | NEW other results |"
SEP = "|" + "---|" * 16


def line(boss, mode, n, so, sn):
    p = fisher(so["w"], so["n"] - so["w"], sn["w"], sn["n"] - sn["w"]) if so["n"] and sn["n"] else float("nan")
    rng = lambda s: "-" if math.isnan(s["ttk"]) else f"{s['ttk']:.0f} ({s['ttk_lo']:.0f}-{s['ttk_hi']:.0f})"
    return f"| {boss} | {mode} | {n} | {fmt_win(so)} | {fmt_win(sn)} | {g(p, 2)} | {rng(so)} | {rng(sn)} | {g(so['deaths'], 1)} | {g(sn['deaths'], 1)} | {g(so['dps'])} | {g(sn['dps'])} | {g(so['tgt'])} | {g(sn['tgt'])} | {fmt_other(so)} | {fmt_other(sn)} |"


def prehm_table(run, title):
    old, _ = old_prehm.rows(); new = {}
    for c, r in run_rows(run): new.setdefault((r["boss"], r["mode"], int(r["n"])), []).append(prehm_row(r))
    print(f"\n### {title}\n\n{HDR}\n{SEP}")
    tot = {"o": [], "n": []}
    for k in [(b, m, n) for b, m, n in __import__("mkspecs").CELLS]:
        o = [prehm_row(r) for r in old.get(k, [])]; nw = new.get(k, [])
        if not nw: continue
        tot["o"] += o; tot["n"] += nw
        print(line(BOSS[k[0]], MODE[k[1]], k[2], summ(o), summ(nw)))
    print(line("**all cells**", "-", "-", summ(tot["o"]), summ(tot["n"])))
    # paired: only the seeds lab 1 ran
    print(f"\nSame seeds only (lab 1's seeds; the armies are identical seed for seed):\n\n| boss | mode | seeds | OLD wins | NEW wins | OLD deaths | NEW deaths |\n|---|---|---|---|---|---|---|")
    for k in [(b, m, n) for b, m, n in __import__("mkspecs").CELLS]:
        o = [prehm_row(r) for r in old.get(k, [])]; nw = new.get(k, [])
        if not nw or not o: continue
        seeds = sorted({r["seed"] for r in o}); np_ = [r for r in nw if r["seed"] in seeds]
        print(f"| {BOSS[k[0]]} | {MODE[k[1]]} | {','.join(map(str, seeds))} | {sum(r['result'] == 'win' for r in o)}/{len(o)} | {sum(r['result'] == 'win' for r in np_)}/{len(np_)} | {g(summ(o)['deaths'], 1)} | {g(summ(np_)['deaths'], 1)} |")


def hm_table(run):
    old, new = hm_old(), hm_new(run)
    print(f"\n### Hardmode broad sweep: hmbattery2 (2.0.66) vs NEW\n\n{HDR}\n{SEP}")
    tot = {p: {"o": [], "n": []} for p in PAIR}
    for slug in SWEEP_ORDER:
        for pair in PAIR:
            o, nw = old.get((slug, pair), []), new.get((slug, pair), [])
            if not nw: continue
            tot[pair]["o"] += o; tot[pair]["n"] += nw
            n = re.search(r"n(\d+)", "n34")[1]
            print(line(BOSS[slug], PAIR[pair], "", summ(o), summ(nw)).replace("|  |", f"| {LEVIES.get(slug, 34)} |", 1))
    for pair in PAIR: print(line(f"**all bosses**", PAIR[pair], "-", summ(tot[pair]["o"]), summ(tot[pair]["n"])))


LEVIES = {"wof": 28, "deer": 28, "crab": 19, "hive": 22, "perf": 24}


def bat_table(run):
    so, sn = summ(bat_old()), summ(bat_new(run))
    print(f"\n### Destroyer battery baseline, Master+Death, 34 levies, one fixed army (hmbattery2 vs NEW)\n\n{HDR}\n{SEP}")
    print(line("The Destroyer", "Master+Death", 34, so, sn))


# ------------------------------------------------------------------ recordings
def recs(dest, runs):
    dest = Path(dest); dest.mkdir(parents=True, exist_ok=True); idx = []
    for run in map(Path, runs):
        rows = {(re.search(r"\|t(c\d+)", r["key"])[1], int(r["seed"])): (c, r) for c, r in run_rows(run)}
        sub = dest / run.name; sub.mkdir(exist_ok=True)
        best = {}
        for p in sorted(run.glob("rec/*/*.jsonl")):
            try: hdr = json.loads(open(p).readline())
            except Exception: continue
            m = re.search(r"\|t(c\d+)(?:\||$)", hdr.get("meta", {}).get("cellkey", ""))
            if not m: continue
            k = (m[1], int(hdr["meta"].get("seed", 0)))
            if k not in best or p.stat().st_size > best[k].stat().st_size: best[k] = p
        for k, p in sorted(best.items(), key=lambda kv: (int(kv[0][0][1:]), kv[0][1])):
            if k not in rows: continue
            c, r = rows[k]; mm = re.search(r"meta=(\w+?)_(on|mid)_(rev|mdeath)_n(\d+)", c["name"]); mb = re.search(r"meta=hmb_(\w+?)_(\w+?)_(mdeath)", c["name"])
            if mm: nm = f"{mm[1]}_{mm[3]}_{mm[2]}_n{mm[4]}"
            elif mb: nm = f"{mb[1]}_{mb[3]}_battery-{mb[2]}"
            else: nm = f"{r['boss']}_{r['mode']}_n{r['n']}_cmdfar"
            res = classify(r) if not mm and not mb else r["result"].lower()
            fn = f"{nm}_s{r['seed']}_{res}.jsonl.gz"
            with open(p, "rb") as src, gzip.open(sub / fn, "wb", compresslevel=9) as z: shutil.copyfileobj(src, z)
            idx.append(f"{run.name}/{fn}\t{BOSS.get(r['boss'], r['boss'])}\t{r['mode']}\tseed {r['seed']}\t{res}\t{f(r['sec']):.0f} s\t{r['deaths']}/{r['n']} levy deaths")
    (dest / "index.txt").write_text("labcompare recordings (FightRecorder format 12, gzip). Columns: file, boss, mode, seed, result, fight seconds, levy deaths.\n"
                                    "prehm_sp0 = pre-hardmode cells vs boss-lab lab 1 (cmd=far, surface arena, gear=mod), prehm_sp1 = the same with single-player owner emulation on,\n"
                                    "hm = hardmode broad sweep vs hmbattery2 (_on_ = on-tier kit, _mid_ = one tier below), bat = Destroyer battery baseline (one fixed army).\n\n" + "\n".join(idx) + "\n")
    print(len(idx), "recordings ->", dest)


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); sp = ap.add_subparsers(dest="cmd", required=True)
    t = sp.add_parser("tables"); t.add_argument("--prehm"); t.add_argument("--prehm-sp1"); t.add_argument("--hm"); t.add_argument("--bat")
    r = sp.add_parser("recs"); r.add_argument("--dest", required=True); r.add_argument("runs", nargs="+")
    a = ap.parse_args()
    if a.cmd == "tables":
        if a.prehm: prehm_table(a.prehm, "Pre-hardmode: boss-lab lab 1 (2.0.13) vs NEW, matched setup (no single-player owner emulation)")
        if a.prehm_sp1: prehm_table(a.prehm_sp1, "Pre-hardmode: lab 1 vs NEW with single-player owner emulation on (extra)")
        if a.bat: bat_table(a.bat)
        if a.hm: hm_table(a.hm)
    else:
        recs(a.dest, a.runs)

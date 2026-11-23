#!/usr/bin/env python3
"""bossai lab tables: summarize.py ARM=RUN_DIR [ARM=RUN_DIR ...]
Reads every out.tsv.bossai.tsv and out.tsv.exo.tsv under each run dir (levylab run dirs) and prints markdown tables:
per arm x opener medians (fight length, levy deaths, worst 2 s, mechs killed, pool left), target behaviour (lock switches per minute, shared target,
distance to target, Thanatos under the floor, Ares enraged, what Ares's arms read), projectile outcomes (made / touched a levy, pooled) and the killing hits."""
import sys, glob, statistics as S, collections, re

def med(xs):
    xs = [x for x in xs if x is not None]
    return S.median(xs) if xs else None

def fmt(v, d=1):
    if v is None: return "-"
    return f"{v:.{d}f}" if isinstance(v, float) else str(v)

def load(run):
    rows = []
    for p in glob.glob(f"{run}/jobs/*/try*/out.tsv.bossai.tsv"):
        lines = open(p).read().splitlines()
        for l in lines[1:]:
            c = l.split("\t")
            if len(c) < 8: continue
            m = {}
            for tok in c[7].split(" "):
                if "=" in tok:
                    k, v = tok.split("=", 1); m[k] = v
            rows.append(dict(key=c[0], boss=c[1], seed=c[2], result=c[3], sec=float(c[4]), deaths=int(c[5]), n=int(c[6]), m=m))
    exo = {}
    for p in glob.glob(f"{run}/jobs/*/try*/out.tsv.exo.tsv"):
        lines = open(p).read().splitlines()
        hdr = lines[0].split("\t")
        for l in lines[1:]:
            d = dict(zip(hdr, l.split("\t"))); exo[d["key"]] = d
    for r in rows: r["exo"] = exo.get(r["key"], {})
    return rows

def num(m, k):
    v = m.get(k)
    try: return float(v)
    except (TypeError, ValueError): return None

OPEN = {"exoares": "Ares first", "exothan": "Thanatos first", "exotwins": "twins first"}

def main():
    arms = [a.split("=", 1) for a in sys.argv[1:]]
    data = {name: load(run) for name, run in arms}
    print("### Fights\n")
    print("| arm | opener | fights | wins | length s | levy deaths | worst 2 s | mechs killed | pool left % (losses) | kill order |")
    print("|---|---|---|---|---|---|---|---|---|---|")
    for name, rows in data.items():
        for b in OPEN:
            rs = [r for r in rows if r["boss"] == b]
            if not rs: continue
            wins = sum(1 for r in rs if r["result"] == "WIN")
            killed, left, orders = [], [], []
            for r in rs:
                e = r["exo"]; ko = e.get("killOrder", "-")
                killed.append(0 if ko in ("-", "") else len(ko.split(";")))
                orders.append(ko if ko not in ("", "-") else "none")
                try:
                    mx = sum(max(0, float(e[f"{k}_max"])) for k in "ATW"); lf = sum(max(0, float(e[f"{k}_left"])) for k in "ATW")
                    if r["result"] != "WIN" and mx > 0: left.append(100 * lf / mx)
                except (KeyError, ValueError): pass
            print(f"| {name} | {OPEN[b]} | {len(rs)} | {wins} | {fmt(med([r['sec'] for r in rs]))} | {fmt(med([r['deaths'] for r in rs]))} | {fmt(med([num(r['m'], 'worst2s') for r in rs]), 0)} | {fmt(med(killed))} | {fmt(med(left), 0)} | {', '.join(orders)} |")
    print("\n### How the mechs hunt (medians per fight)\n")
    print("| arm | opener | lock switches/min Ares / Thanatos / Artemis / Apollo | mechs sharing one target % | tiles to own target Ares / Thanatos / Apollo | Thanatos under the floor % | Ares enraged % | Ares's arms read (empty / commander / levy) | arm aim point, tiles from nearest levy |")
    print("|---|---|---|---|---|---|---|---|---|")
    for name, rows in data.items():
        for b in OPEN:
            rs = [r for r in rows if r["boss"] == b]
            if not rs: continue
            sw = " / ".join(fmt(med([num(r["m"], f"{k}.switchPerMin") for r in rs])) for k in ("ares", "than", "artemis", "apollo"))
            td = " / ".join(fmt(med([num(r["m"], f"{k}.tgtDistTiles") for r in rs]), 0) for k in ("ares", "than", "apollo"))
            ar = [0, 0, 0]
            for r in rs:
                v = r["m"].get("armReads(empty/cmd/levy/other)")
                if v: a = [int(x) for x in v.split("/")]; ar = [ar[0] + a[0], ar[1] + a[1], ar[2] + a[2]]
            tot = sum(ar)
            arms_s = "-" if tot == 0 else f"{100*ar[0]/tot:.0f}% / {100*ar[1]/tot:.0f}% / {100*ar[2]/tot:.0f}%"
            print(f"| {name} | {OPEN[b]} | {sw} | {fmt(med([num(r['m'], 'sharedTarget%') for r in rs]), 0)} | {td} | {fmt(med([num(r['m'], 'thanUnder%') for r in rs]), 0)} | {fmt(med([num(r['m'], 'aresEnraged%') for r in rs]), 0)} | {arms_s} | {fmt(med([num(r['m'], 'armAimFromNearestLevyTiles') for r in rs]), 0)} |")
    print("\n### Mech projectiles, pooled over the arm's fights (made / touched at least one levy / levy hits)\n")
    for name, rows in data.items():
        agg = collections.defaultdict(lambda: [0, 0, 0])
        for r in rows:
            for k, v in r["m"].items():
                if k.startswith("proj."):
                    a = [int(x) for x in v.split("/")]; t = agg[k[5:]]
                    for i in range(3): t[i] += a[i]
        items = sorted(agg.items(), key=lambda kv: -kv[1][0])
        print(f"- **{name}**: " + ", ".join(f"{k} {a[0]}/{a[1]} ({(100*a[1]/a[0]) if a[0] else 0:.0f}%)" for k, a in items if a[0] >= 3))
    print("\n### Killing hits, pooled (share of deaths)\n")
    for name, rows in data.items():
        agg = collections.Counter()
        for r in rows:
            for k, v in r["m"].items():
                if k.startswith("killedBy."): agg[k[9:]] += int(v)
        tot = sum(agg.values())
        print(f"- **{name}** ({tot} deaths): " + ", ".join(f"{k} {100*v/tot:.0f}%" for k, v in agg.most_common(9)))
    print("\n### Feature counters (summed)\n")
    for name, rows in data.items():
        s = [0] * 7
        for r in rows:
            v = r["m"].get("feature(armCalls/armLent/armEmpty/ventCalls/ventPlaced/homingCalls/homingPlaced)")
            if v:
                a = [int(x) for x in v.split("/")]; s = [x + y for x, y in zip(s, a)]
        print(f"- {name}: arm calls {s[0]}, lent {s[1]}, read the empty slot {s[2]}; vent calls {s[3]}, stand-ins placed {s[4]}; homing calls {s[5]}, placed {s[6]}")
    print("\n### ms per tick (median per fight, from the exo table)\n")
    for name, rows in data.items():
        v = med([float(r["exo"]["msPerTick"]) for r in rows if r["exo"].get("msPerTick", "-") not in ("-", "")])
        print(f"- {name}: {fmt(v, 2)}")

main()

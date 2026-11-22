"""Before/after table of reduced exolab runs: exofix_compare.py BASEDIR TAG [TAG ...]  (BASEDIR/TAG/summary.json from exofix_summary.py)"""
import json, sys, statistics
base = sys.argv.pop(1).rstrip("/") + "/"
def med(x):
    x = [v for v in x if v is not None]
    return statistics.median(x) if x else None
print("run | opener | fights | fight s (median) | hit damage dealt M (sum) | dealt per s k (median) | worst 2 s (median, max) | Gauss deaths (sum) | beam deaths (sum) | first mech to 70% s (median) | life/max at full scaling (median) | Thanatos hits on open % (pooled)")
for tag in sys.argv[1:]:
    rows = json.load(open(base + tag + "/summary.json"))
    for op in ("exoares", "exothan", "exotwins", "all"):
        rs = [r for r in rows if op == "all" or r["opener"] == op]
        dm = sum((r["dmgBoss"] or 0) for r in rs) / 1e6
        dps = med([(r["dmgBoss"] or 0) / r["sec"] / 1e3 for r in rs])
        o = sum(int(r["open"] or 0) for r in rs); c = sum(int(r["closed"] or 0) for r in rs)
        print(f"{tag} | {op} | {len(rs)} | {med([r['sec'] for r in rs]):.0f} | {dm:.1f} | {dps:.0f} | {med([r['worst2s'] for r in rs]):.0f}, {max(r['worst2s'] for r in rs)} | {sum(r['gauss'] for r in rs)} | {sum(r['beams'] for r in rs)} | "
              f"{('-' if op == 'all' else (str([r['to70'] for r in rs]).replace('None', 'never')))} |{med([r['ratio'] for r in rs]):.2f} | {100 * o / max(1, o + c):.0f}")

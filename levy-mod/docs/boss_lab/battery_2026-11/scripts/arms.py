import csv, statistics as st, collections, sys
rows = list(csv.DictReader(open(sys.argv[1])))
ORDER = "qslime cryo twins aqua destroyer brim prime cal plant fish eol".split()
print("| boss | arm | wins | kill s (median) | army DPS (median) | deaths (median) | landed hits | static-blocked | cross-levy blocked | detour us/tick | alt us/tick |")
print("|---|---|---|---|---|---|---|---|---|---|---|")
tot = collections.defaultdict(lambda: collections.Counter())
for b in ORDER:
    for imm in ("shared", "local", "static", "perlevy"):
        rs = [r for r in rows if r["boss"] == b and r["kit"] == "on" and r["pair"] == "mdeath" and r["imm"] == imm]
        if not rs: continue
        f = lambda k: [float(r[k]) for r in rs if r[k] not in ("", "-")]
        w = sum(r["result"] == "WIN" for r in rs)
        m = lambda k: f"{st.median(f(k)):.0f}" if f(k) else "-"
        s = lambda k: sum(int(r[k] or 0) for r in rs)
        print(f"| {b} | {imm} | {w}/{len(rs)} | {m('ttk')} | {m('armyDps')} | {m('deaths')} | {s('all_landed')} | {s('all_blkStatic')} | {s('all_crossBlocked')} | {m('detourUsPerTick')} | {m('altUsPerTick')} |")
        for r in rs:
            tot[imm]["dps"] += float(r["armyDps"] or 0); tot[imm]["n"] += 1
print()
for imm, c in tot.items(): print(imm, "mean army DPS over", c["n"], "fights:", round(c["dps"] / c["n"]))

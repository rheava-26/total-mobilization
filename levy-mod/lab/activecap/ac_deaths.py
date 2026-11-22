#!/usr/bin/env python3
"""ac_deaths.py REC.jsonl [...] -- why does a capped army lose more soldiers? From an event recording (format 12):
per fight, the soldier bodies in the first frame (the starting field) vs bodies that arrived later (replacements under the active army size),
when each arrived and when it died (ldeath events), the deaths per second, and the deaths per 100 soldier-seconds in the field.
Equal deaths per second with fewer soldiers = the same enemy fire shared by fewer; replacements dying soon after they arrive = an arrival problem."""
import sys, json, statistics

def one(path):
    frames, deaths, hits, end = [], {}, {}, None
    for line in open(path):
        d = json.loads(line)
        k = d.get("k")
        if k == "f": frames.append((d["t"], [row[0] for row in d.get("l", [])]))
        elif k == "e" and d.get("e") == "ldeath": deaths.setdefault(d["id"], d["t"])
        elif k == "e" and d.get("e") == "lhit": hits.setdefault(d["id"], []).append(d["dmg"])
        elif k == "end": end = d
    if not frames: return None
    T = end["ticks"] if end else frames[-1][0]
    first = {}
    for t, ids in frames:
        for i in ids: first.setdefault(i, t)
    start = set(frames[0][1])
    for i, t in deaths.items(): first.setdefault(i, t)
    soldier_ticks = 0; prev_t = frames[0][0]
    for t, ids in frames: soldier_ticks += len(ids) * (t - prev_t); prev_t = t
    orig = [i for i in first if i in start]; repl = [i for i in first if i not in start]
    life = lambda i: (deaths[i] - first[i]) / 60.0
    rd = [life(i) for i in repl if i in deaths]
    od = [life(i) for i in orig if i in deaths]
    return dict(file=path.split("/")[-1], secs=T / 60.0, start=len(start), repl=len(repl), deaths=len(deaths), dps=len(deaths) / (T / 60.0),
                per100=100.0 * len(deaths) / max(1e-9, soldier_ticks / 60.0), orig_dead=len(od), repl_dead=len(rd),
                orig_life=statistics.median(od) if od else float("nan"), repl_life=statistics.median(rd) if rd else float("nan"),
                repl_5s=sum(1 for x in rd if x <= 5.0), avg_field=soldier_ticks / max(1, T))

print("| recording | s | field at start | replacements | deaths | deaths/s | deaths per 100 soldier-s | starting field dead (median s alive) | replacements dead (median s from arrival) | replacements dead within 5 s | mean field |")
print("|---|---|---|---|---|---|---|---|---|---|---|")
for p in sys.argv[1:]:
    r = one(p)
    if r: print(f"| {r['file']} | {r['secs']:.1f} | {r['start']} | {r['repl']} | {r['deaths']} | {r['dps']:.2f} | {r['per100']:.1f} | {r['orig_dead']} ({r['orig_life']:.1f}) | {r['repl_dead']} ({r['repl_life']:.1f}) | {r['repl_5s']} | {r['avg_field']:.1f} |")

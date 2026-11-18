#!/usr/bin/env python3
"""eve_log.py LEVYPERF.jsonl  -- what scales in the laggy windows of a player's perf log (lagsweep, 2026-11-17).

Windows: laggy = focused, fps < 20, levies present; smooth = focused, fps > 50, 20+ levies. Everything is per game update (ms per tick) unless it says otherwise.
Levy's own buckets ("lv") are corrected for the analyzer bug fixed on the lagsweep branch: while a recorded fight's profiling was on they read 11 times too high
(a window is taken as affected when its "ai" bucket is more than twice the SoldierNPC update time it is part of)."""
import json, sys, statistics as S, collections
import numpy as np

path = sys.argv[1]
W = []
for line in open(path):
    try: d = json.loads(line)
    except Exception: continue
    if d.get("k") != "w" or d.get("nu", 0) < 5: continue
    nu = d["nu"]; top = {(c, n): (ms, cnt) for c, n, ms, cnt in d["top"]}
    def ms(c, n): return top.get((c, n), (0, 0))[0] / nu
    def calls(c, n): return top.get((c, n), (0, 0))[1] / nu
    w = dict(t=d["t"][11:19], foc=d["foc"], fps=d["fps"], ups=d["ups"], upd=d["upd"], draw=d["draw"], npcph=d["us"][0], projph=d["us"][1],
             npc=d["cnt"]["npc"], proj=d["cnt"]["proj"], dust=d["cnt"]["dust"], gore=d["cnt"]["gore"], levy=d["cnt"]["levy"], minion=d["cnt"]["minion"],
             sold=ms("nt", "Levy/SoldierNPC"), army=ms("army", "all"), armyn=calls("army", "all"), dnpclevy=ms("dnpc", "Levy"),
             globcal=ms("glob", "CalamityMod"), globcaln=calls("glob", "CalamityMod"), globlevy=ms("glob", "Levy"), globnox=ms("glob", "NoxusBoss"),
             npcvan=ms("npc", "Terraria"), projvan=ms("proj", "Terraria"), projcal=ms("proj", "CalamityMod"),
             ddust=d["ds"][2], dgore=d["ds"][3], dnpc=d["ds"][0], dproj=d["ds"][1], dui=d["ds"][4], boss=d["ctx"]["boss"] or d["ctx"]["far"],
             lv=dict(d["lv"]), pt={n: (top[(c, n)][0] / nu, top[(c, n)][1] / nu) for (c, n) in top if c == "pt"})
    w["rest"] = w["upd"] - w["npcph"] - w["projph"]
    ai = w["lv"].get("ai", 0); w["lvfix"] = 11.0 if (w["sold"] > 0 and ai > 2 * w["sold"]) else 1.0
    W.append(w)

lag = [w for w in W if w["foc"] and w["fps"] < 20 and w["levy"] > 0]
smo = [w for w in W if w["foc"] and w["fps"] > 50 and w["levy"] >= 20]
def m(ws, k): return S.mean(w[k] for w in ws) if ws else float("nan")
def lvm(ws, k): return S.mean(w["lv"].get(k, 0) / w["lvfix"] for w in ws) if ws else float("nan")
print(f"# {len(W)} windows; laggy {len(lag)}, smooth {len(smo)}\n")

print("## 1. The update crosses the frame budget: fps against ms per update (focused windows with levies)")
bins = [(0, 8), (8, 10), (10, 12), (12, 14), (14, 15.5), (15.5, 17), (17, 99)]
print("| ms per update | windows | median fps | median updates/s | median draw ms per frame |\n|---|---|---|---|---|")
foc = [w for w in W if w["foc"] and w["levy"] > 0]
for lo, hi in bins:
    b = [w for w in foc if lo <= w["upd"] < hi]
    if b: print(f"| {lo}-{hi} | {len(b)} | {S.median(w['fps'] for w in b):.1f} | {S.median(w['ups'] for w in b):.1f} | {S.median(w['draw'] for w in b):.1f} |")

print("\n## 2. Where an update goes (ms per tick, mean)")
rows = [("whole update", "upd"), ("NPC phase", "npcph"), ("  levy bodies (SoldierNPC update)", "sold"), ("  base-game NPCs", "npcvan"), ("projectile phase", "projph"), ("  army shots", "army"),
        ("  other base-game shots", "projvan"), ("  other Calamity shots", "projcal"), ("global AI hooks: Calamity", "globcal"), ("global AI hooks: Levy", "globlevy"), ("global AI hooks: NoxusBoss", "globnox"),
        ("rest of the update (players, dust, gore, world)", "rest"), ("draw per frame", "draw"), ("  levy bodies drawn per frame", "dnpclevy"), ("  dust drawn per frame", "ddust")]
print("| | laggy | smooth | ratio |\n|---|---|---|---|")
for n, k in rows: a, b = m(lag, k), m(smo, k); print(f"| {n} | {a:.2f} | {b:.2f} | {a / b if b else float('nan'):.1f} |")
print("\n## 3. Counts (mean)\n| | laggy | smooth | ratio |\n|---|---|---|---|")
for k in ["levy", "npc", "proj", "armyn", "minion", "dust", "gore", "globcaln"]:
    a, b = m(lag, k), m(smo, k); print(f"| {k} | {a:.0f} | {b:.0f} | {a / b:.1f} |")

print("\n## 4. Per unit: does each thing cost more, or are there more of them?\n| | laggy | smooth |\n|---|---|---|")
def per(ws, num, den, scale=1000): return S.mean(w[num] / w[den] * scale for w in ws if w[den] > 0)
print(f"| SoldierNPC update per levy, us | {per(lag, 'sold', 'levy'):.0f} | {per(smo, 'sold', 'levy'):.0f} |")
print(f"| army shot update per shot alive, us | {per(lag, 'army', 'armyn'):.1f} | {per(smo, 'army', 'armyn'):.1f} |")
print(f"| Calamity global hook per call, us | {per(lag, 'globcal', 'globcaln'):.2f} | {per(smo, 'globcal', 'globcaln'):.2f} |")
print(f"| army shots alive per levy | {per(lag, 'armyn', 'levy', 1):.1f} | {per(smo, 'armyn', 'levy', 1):.1f} |")

print("\n## 5. Levy's own buckets (ms per tick, corrected for the x11 analyzer bug)\n| bucket | laggy | smooth | ratio |\n|---|---|---|---|")
for k in ["ai", "stats", "g.reset", "g.misc", "g.equips", "g.sets", "g.slot", "dmg", "combat", "move"]:
    a, b = lvm(lag, k), lvm(smo, k); print(f"| {k} | {a:.2f} | {b:.2f} | {a / b if b else float('nan'):.1f} |")
print(f"\nwindows with the x11 bug: laggy {sum(w['lvfix'] > 1 for w in lag)}/{len(lag)}, smooth {sum(w['lvfix'] > 1 for w in smo)}/{len(smo)}")
eng = [w for w in smo if w["lvfix"] > 1]
print(f"smooth windows inside a recorded fight (the same profiling as the laggy ones): {len(eng)}; stats {lvm(eng, 'stats'):.2f} ai {lvm(eng, 'ai'):.2f} levies {m(eng, 'levy'):.0f} army shots {m(eng, 'armyn'):.0f}")

print("\n## 6. Least squares over every focused window with levies: ms per update explained by counts")
X = np.array([[1, w["levy"], w["armyn"], max(0, w["npc"] - w["levy"]), w["dust"] / 1000, w["gore"] / 100] for w in foc]); y = np.array([w["upd"] for w in foc])
c, *_ = np.linalg.lstsq(X, y, rcond=None); r2 = 1 - ((y - X @ c) ** 2).sum() / ((y - y.mean()) ** 2).sum()
for n, v in zip(["constant", "per levy", "per army shot alive", "per other NPC", "per 1000 dust", "per 100 gore"], c): print(f"- {n}: {v:+.4f} ms")
print(f"- R^2 = {r2:.2f} over {len(foc)} windows")
for k, nm in [("sold", "SoldierNPC"), ("army", "army shots")]:
    y2 = np.array([w[k] for w in foc]); c2, *_ = np.linalg.lstsq(X, y2, rcond=None); r22 = 1 - ((y2 - X @ c2) ** 2).sum() / ((y2 - y2.mean()) ** 2).sum()
    print(f"- {nm}: " + ", ".join(f"{n} {v:+.4f}" for n, v in zip(["const", "levy", "shot", "npc", "kdust", "hgore"], c2)) + f" (R^2 {r22:.2f})")

print("\n## 7. Projectile types in the laggy windows (ms per tick, shots alive, us per shot), against the same type in smooth windows")
agg = collections.defaultdict(lambda: [0.0, 0.0, 0]); aggs = collections.defaultdict(lambda: [0.0, 0.0, 0])
for w in lag:
    for n, (a, b) in w["pt"].items(): g = agg[n]; g[0] += a; g[1] += b; g[2] += 1
for w in smo:
    for n, (a, b) in w["pt"].items(): g = aggs[n]; g[0] += a; g[1] += b; g[2] += 1
print("| type | ms/tick (mean over laggy windows) | shots alive | us per shot | smooth: us per shot (windows) |\n|---|---|---|---|---|")
for n, (a, b, k) in sorted(agg.items(), key=lambda kv: -kv[1][0])[:16]:
    s = aggs.get(n); ss = f"{s[0] / s[1] * 1000:.1f} ({s[2]})" if s and s[1] > 0 else "-"
    print(f"| {n} | {a / len(lag):.2f} | {b / len(lag):.0f} | {a / b * 1000 if b else 0:.1f} | {ss} |")

print("\n## 8. Hostile NPCs: the non-levy NPC count above the calm baseline")
calm = [w for w in W if w["foc"] and w["levy"] > 0 and w["armyn"] < 60]
base = S.median(w["npc"] - w["levy"] for w in calm) if calm else 0
print(f"calm baseline of non-levy NPCs (town NPCs, drones): {base:.0f}; laggy windows: hostiles above it, median {S.median(max(0, w['npc'] - w['levy'] - base) for w in lag):.0f}, range {min(max(0, w['npc'] - w['levy'] - base) for w in lag):.0f}-{max(max(0, w['npc'] - w['levy'] - base) for w in lag):.0f}")

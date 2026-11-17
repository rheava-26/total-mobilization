#!/usr/bin/env python3
"""Self-check of the offline fight rules and readers (python3 selfcheck.py; prints PASS / FAIL lines and exits 1 on a failure).

Builds small recordings in memory: the death-or-fled rule, a Lunatic Cultist file with Celestial Pillars standing after her death (the win must come out as won and
the pillars must not make a second fight), pillars standing from the start under several later bosses, the format 11 commander projectile key through
make_site's converter and recview's reader, and a format 10 damage row of 10 numbers plus a future row of 12.
"""
import gzip, json, os, sys, tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "..", "Levy", "tools", "fightviewer"))
import recrules as R
import recview

fails = 0


def chk(ok, what):
    global fails
    print(("PASS " if ok else "FAIL ") + what)
    if not ok:
        fails += 1


CULT, SOLAR, VORTEX, NEBULA, STAR, EOC = 439, 517, 422, 507, 493, 4


def brow(t, wid, x=1000, life=100, part=0):
    return [t, wid, x, 500, 0, 0, life, 1000, 0, 0, 0, 0, 0, part, 1, -1, 40, 40]


def frame(t, bosses, dead=False, c=None):
    f = {"k": "f", "t": t, "p": [900, 500, 0, 0, 1, 8192 if dead else 1, 0 if dead else 400, 200, 0, 0, 0, 0, -1, 0, 0, 0], "b": bosses, "l": []}
    if c:
        f["c"] = c
    return f


def rec(frames, events, extra_end=None, names=None):
    rows = [{"k": "hdr", "format": 11, "auto": 1, "boss": "x", "hz": 30, "meta": {}}]
    rows += frames + events
    rows.append({"k": "names", "items": {}, "npcs": names or {str(CULT): "Lunatic Cultist", str(EOC): "Eye of Cthulhu", str(SOLAR): "Solar Pillar"}, "projectiles": {"85": "Magic Missile"}, "buffs": {}, "soldiers": {}, "prefixes": {}})
    rows.append(dict({"k": "end", "reason": "boss gone", "ticks": frames[-1]["t"], "result": "ended", "outcome": "none", "cmdDeaths": 0}, **(extra_end or {})))
    return rows


# ---- the rule
chk(R.decide("killed", 3, False, 5000, 4000) == "won", "a kill is won")
chk(R.decide("despawn", 1, True, 1000, 940) == "lost", "boss left with the commander down: lost")
chk(R.decide("despawn", 1, False, 1800, 1000) == "lost", "boss left 13 s after she fell: lost")
chk(R.decide("despawn", 1, False, 1000 + R.LOST_WINDOW, 1000) == "lost" and R.decide("despawn", 1, False, 1001 + R.LOST_WINDOW, 1000) == "fled", "the window is 60 s")
chk(R.decide("despawn", 0, False, 5000, None) == "fled", "boss left, she never fell: fled")
chk(R.decide("despawn", 1, False, 1000, 5000) == "fled", "she fell only after it left: fled")
chk(R.decide("none", 1, False, -1, 500) == "lost" and R.decide("none", 0, False, -1, None) == "ended", "no boss end: lost after a fall, else ended")

# whole files read from events and the frame dead flag
fr = [frame(t, [brow(EOC, 7)], dead=(t >= 1000 and t < 1200)) for t in range(0, 1400, 2)]
ev_lost = [{"k": "e", "t": 1000, "e": "cdeath"}, {"k": "e", "t": 1100, "e": "bdespawn", "npc": EOC, "name": "Eye of Cthulhu", "id": 7, "final": 1}]
chk(R.classify(rec(fr, ev_lost))["result"] == "lost", "file: despawn while the commander is down reads lost")
ev_fled = [{"k": "e", "t": 1100, "e": "bdespawn", "npc": EOC, "name": "Eye of Cthulhu", "id": 7, "final": 1}]
fr_alive = [frame(t, [brow(EOC, 7)]) for t in range(0, 1400, 2)]
chk(R.classify(rec(fr_alive, ev_fled))["result"] == "fled", "file: despawn with her alive and no fall reads fled")
ev_old = [{"k": "e", "t": 100, "e": "cdeath"}, {"k": "e", "t": 9000, "e": "bdespawn", "npc": EOC, "name": "Eye of Cthulhu", "id": 7, "final": 1}]
chk(R.classify(rec([frame(t, [brow(EOC, 7)]) for t in range(0, 9100, 2)], ev_old))["result"] == "fled", "file: a fall long before the boss left does not make it a loss")

# ---- the Cultist file: she dies at 3000, four pillars appear and stand for another 20 minutes, a different boss shows up at 50000
frames = []
for t in range(0, 3000, 2):
    frames.append(frame(t, [brow(CULT, 63, life=500)]))
for t in range(3000, 60000, 20):
    b = [brow(SOLAR, 115), brow(VORTEX, 116), brow(NEBULA, 117), brow(STAR, 118)]
    if t >= 50000:
        b.append(brow(EOC, 7))
    frames.append(frame(t, b))
frames.sort(key=lambda f: f["t"])
events = [{"k": "e", "t": 0, "e": "boss", "npc": CULT, "name": "Lunatic Cultist", "id": 63}, {"k": "e", "t": 3000, "e": "boss", "npc": SOLAR, "name": "Solar Pillar", "id": 115},
          {"k": "e", "t": 3000, "e": "bdeath", "npc": CULT, "name": "Lunatic Cultist", "id": 63, "life": 0, "max": 1000, "final": 0},
          {"k": "e", "t": 4000, "e": "cdeath"}]
rows = rec(frames, events, {"result": "lost", "ticks": 60000})
eps = R.episodes(rows)
chk(len(eps) == 2 and not eps[0].get("whole"), "the Cultist file with pillars splits into two fights (%d)" % len(eps))
chk(eps[0]["name"] == "Lunatic Cultist" and eps[0]["result"] == "won" and eps[0]["outcome"] == "killed", "the Cultist fight is won, named for her alone (%s, %s)" % (eps[0]["name"], eps[0]["result"]))
chk(eps[0]["cmdDeaths"] == 0 and eps[1]["name"] == "Eye of Cthulhu", "her later death (after the fight) is not in it; the next boss is its own fight")
last = eps[0]["rows"][-1]
chk(last["k"] == "end" and last["result"] == "won" and last["bossName"] == "Lunatic Cultist" and last["reclassified"] == 1, "the new end record says won, bossName Lunatic Cultist")
chk(eps[0]["rows"][0].get("split", {}).get("episode") == 1 and eps[0]["rows"][1]["k"] != "e", "the episode names where it came from")
# each fight carries the time it began; a cut fight is not cut again; the split file is named for that minute
rows[0]["date"] = "2026-11-16T22:11:33"
eps_d = R.episodes(rows)
chk(eps_d[0]["rows"][0]["date"] == "2026-11-16T22:11:33" and eps_d[1]["rows"][0]["date"] > "2026-11-16T22:24" and eps_d[1]["rows"][0]["split"]["srcDate"] == "2026-11-16T22:11:33",
    "each fight's header date is when it began (%s, %s)" % (eps_d[0]["rows"][0]["date"], eps_d[1]["rows"][0]["date"]))
chk(R.file_name("cultist_pillars_2211.levyrec", eps_d[1]).startswith(eps_d[1]["rows"][0]["date"][:10] + "_" + eps_d[1]["rows"][0]["date"][11:16].replace(":", "-") + "_Eye_of_Cthulhu_"),
    "a split file is named for the minute its fight began (%s)" % R.file_name("cultist_pillars_2211.levyrec", eps_d[1]))
again = R.episodes(eps_d[1]["rows"])
chk(len(again) == 1 and again[0].get("whole"), "a fight already cut out comes back whole")
# pillars only: no fight at all
only = rec([frame(t, [brow(SOLAR, 115)]) for t in range(0, 4000, 20)], [{"k": "e", "t": 0, "e": "boss", "npc": SOLAR, "name": "Solar Pillar", "id": 115}])
chk(R.episode_spans(only) == [], "pillars standing alone are no fight")
# no event NPCs: left whole
chk(len(R.episodes(rec(fr_alive, ev_fled))) == 1 and R.episodes(rec(fr_alive, ev_fled))[0].get("whole"), "a recording with no event NPCs is returned whole")

# ---- make_site: result for real play, c key kept compact, data loads
import make_site as ms
s = ms.summarize(rec(fr, ev_lost))
chk(s["result"] == "loss" and s["rule"] == "lost" and s["cmdDeaths"] == 1, "make_site: a boss that left after her fall is a loss, not 'despawn' (%s)" % s["result"])
chk(ms.main_boss(frame(0, [brow(SOLAR, 115), brow(EOC, 7)]))[0] == EOC and ms.main_boss(frame(0, [brow(SOLAR, 115)])) is None, "make_site: a pillar is never the main boss")
s2 = ms.summarize(rec(fr_alive, ev_fled))
chk(s2["result"] == "fled", "make_site: boss left, commander alive: fled (%s)" % s2["result"])
cfr = [frame(t, [brow(EOC, 7)], c=[[85, 1000.4 + t, 480.6, 12.34, -1.0, 16, 16], [85, 1100, 400, 0, 0, 16, 16]]) for t in range(0, 40, 2)]
blob = json.loads(ms.dump(ms.convert(rec(cfr, []), 1)))
c0 = blob["frames"][3].get("c")
chk(c0 is not None and len(c0) == 2 and all(isinstance(v, int) for v in (c0[0][0], c0[0][1], c0[0][2], c0[0][5], c0[0][6])) and c0[0][3] == 12.3, "make_site keeps the commander projectiles as compact rows (%s)" % (c0,))
ms.MAX_C_ROWS = 1
blob1 = json.loads(ms.dump(ms.convert(rec(cfr, []), 1)))
chk(len(blob1["frames"][3]["c"]) == 1, "make_site thins to --cmd-cap rows per sample")
ms.MAX_C_ROWS = 0
blob0 = json.loads(ms.dump(ms.convert(rec(cfr, []), 1)))
chk("c" not in blob0["frames"][3], "make_site drops the layer at cap 0")
ms.MAX_C_ROWS = 120
chk("c" not in json.loads(ms.dump(ms.convert(rec(fr_alive, ev_fled), 1)))["frames"][3], "a format 10 file has no c key in its data")

# ---- recview: dmg rows of 7 (format 8/9), 10 (format 10) and 12 (a later one)
d = [{"k": "e", "t": 60, "e": "dmg", "d": [["cmd", "rogue", "item", 1, 1, 500, 3], ["cmd", "rogue", "proj", 85, 1, 700, 4, 757, 1, 2], ["levy", "magic", "proj", 9, 0, 50, 1, 0, 0, 0, 99, 100]]}]
rows = rec(cfr, d)
with tempfile.TemporaryDirectory() as td:
    p = os.path.join(td, "t.levyrec")
    with gzip.open(p, "wt") as g:
        for r in rows:
            g.write(json.dumps(r) + "\n")
    try:
        a = recview.analyse(recview.Rec(p))
        tot = sum(x["damage"] for x in a["damage"])
        chk(tot == 1250, "recview reads dmg rows of 7, 10 and 12 numbers (total %s)" % tot)
        chk(a["commanderProjectiles"]["format11"] and a["commanderProjectiles"]["mostRows"] == 2, "recview counts the commander projectile key")
    except Exception as e:
        chk(False, "recview analyse raised %s: %s" % (type(e).__name__, e))

print("fail=%d" % fails)
sys.exit(1 if fails else 0)

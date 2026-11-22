#!/usr/bin/env python3
"""Self-check of the offline fight rules and readers (python3 selfcheck.py; prints PASS / FAIL lines and exits 1 on a failure).

Builds small recordings in memory: the death-or-fled rule, a Lunatic Cultist file with Celestial Pillars standing after her death (the win must come out as won and
the pillars must not make a second fight), pillars standing from the start under several later bosses, the format 11 commander projectile key through
make_site's converter and recview's reader, and a format 10 damage row of 10 numbers plus a future row of 12.
Format 12: the event result rule piece by piece and on whole files (main event by priority, army gone, cut file, caps), the C# constants read from
EventRecorder.cs against recrules (priority, grace, cap, fallback, miniboss hand list), the importance table, make_site's event row and data file,
an event never split, and the anomaly scan (idle, stuck, one-hit death, damage spike, weapon outlier) on a small built recording.
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

# ================================================================ format 12: event recordings
# ---- the rule, piece by piece (EventRecorder.DecideRecording, EventResult, DecideInvasion, DecideAcid)
chk(R.decide_recording("army gone", "won") == "lost", "event: the army gone is lost, whatever the event did")
chk(R.decide_recording("world unloaded", "won") == "interrupted" and R.decide_recording(None, "won") == "interrupted", "event: the world closing or a cut file is interrupted")
chk(R.decide_recording("time limit", None) == "survived" and R.decide_recording("size limit", None) == "survived", "event: the hour cap and the size cap are survived")
chk(R.decide_recording("turned off", "won") == "ended" and R.decide_recording("event over", None) == "ended", "event: the setting off is ended; an event over with no result is ended")
chk(R.decide_recording("event over", "won") == "won" and R.decide_recording("event over", "survived") == "survived", "event: otherwise the main event's own result")
chk(R.invasion_result(0) == "won" and R.invasion_result(-3) == "won" and R.invasion_result(40) == "ended", "invasion: won when its size ran out, ended when taken away")
chk(R.moon_result(15) == "won" and R.moon_result(14) == "survived" and R.moon_result(20) == "won", "moons: wave 15 is won, dawn earlier is survived")
chk(R.dd2_result(True, False) == "won" and R.dd2_result(False, True) == "lost" and R.dd2_result(False, False) == "ended", "Old One's Army: won / lost / ended")
chk(R.acid_result(40, 1) == "won" and R.acid_result(40, 4) == "won" and R.acid_result(40, 5) == "survived" and R.acid_result(200, 20) == "won" and R.acid_result(200, 21) == "survived", "Acid Rain: won at 10 % of the start or less (and at most 2)")
chk(R.normal_median({1: 0, 2: 0}, {1: 100, 2: 200}) == 0 and R.normal_median({1: 0, 2: 0, 3: 0, 4: 1}, {1: 100, 2: 200, 3: 300, 4: 9000}) == 200, "fallback: the median of the ordinary types, none with fewer than 3")
chk(R.fallback_mini(1200, 200) and not R.fallback_mini(1199, 200) and not R.fallback_mini(900, 100) and not R.fallback_mini(5000, 0), "fallback: 6 times the median and at least 1000 life")
chk(R.base_importance(517) == 2 and R.base_importance(551) == 2 and R.base_importance(325) == 1 and R.base_importance(26) == 0 and R.base_importance(26, True) == 2, "importance: pillars and Betsy 2, the hand list 1, boss flag 2, else 0")

# ---- the C# side says the same (EventRecorder.cs): priority, timings, fallback numbers, the miniboss hand list
CS = os.path.join(HERE, "..", "..", "Levy", "Common", "Recording", "EventRecorder.cs")
NPCIDS = {"GoblinSummoner": 471, "PirateShip": 491, "PirateShipCannon": 492, "MartianSaucer": 392, "MartianSaucerTurret": 393, "MartianSaucerCannon": 394, "MartianSaucerCore": 395, "MourningWood": 325,
          "Pumpking": 327, "PumpkingBlade": 328, "Everscream": 344, "SantaNK1": 346, "IceQueen": 345, "Mothron": 477, "DD2DarkMageT1": 564, "DD2DarkMageT3": 565, "DD2OgreT2": 576, "DD2OgreT3": 577, "BloodNautilus": 618}
try:
    import re as _re
    cs = open(CS, encoding="utf-8").read()
    pri = _re.findall(r'"(\w+)"', _re.search(r"Priority = \{([^}]*)\}", cs).group(1))
    chk(pri == R.EVENT_PRIORITY, "C# and Python name the main event in the same order (%s)" % pri)
    chk(_re.search(r"ArmyGoneTicks = 90 \* 60", cs) and R.ARMY_GONE_TICKS == 5400 and _re.search(r"MaxTicks = 60 \* 60 \* 60", cs) and R.EVENT_MAX_TICKS == 216000, "C# and Python: 90 s army grace, 60 minute cap")
    m = _re.search(r"FallbackRatio = (\d+), FallbackMinLife = (\d+), FallbackMinTypes = (\d+)", cs)
    chk(m and (int(m.group(1)), int(m.group(2)), int(m.group(3))) == (R.FALLBACK_RATIO, R.FALLBACK_MIN_LIFE, R.FALLBACK_MIN_TYPES), "C# and Python: the same fallback numbers")
    hand = _re.search(r"MinibossTypes = new\(\)\s*\{([^}]*)\}", cs).group(1)
    ids = {NPCIDS.get(n, -1) for n in _re.findall(r"NPCID\.(\w+)", hand)}
    chk(ids == set(R.MINIBOSS_TYPES), "C# and Python: the same miniboss hand list (%d types)" % len(ids))
    chk(_re.search(r"FinalMoonWave = 15", cs) and R.FINAL_MOON_WAVE == 15, "C# and Python: the moons' final wave is 15")
except Exception as e:
    chk(False, "reading EventRecorder.cs raised %s: %s" % (type(e).__name__, e))


def erec(events, reason="event over", end_extra=None, hdr_extra=None, frames=None):
    h = {"k": "hdr", "format": 12, "kind": "event", "event": "bloodmoon", "eventName": "Blood Moon", "events": ["bloodmoon"], "hz": 5, "meta": {}, "wave": 0,
         "roster": {"618": {"name": "Dreadnautilus", "imp": 1}}, "stateNames": "Idle,Follow,Patrol,Engage"}
    h.update(hdr_extra or {})
    fr = frames or [{"k": "f", "t": t, "p": [800, 400, 0, 0, 1, 1, 400, 200, 0, 0, 0, 0, -1, 0, 0, 0], "l": [[5, 820, 400, 0, 0, 1, 300, 3, 0, 0, 400, 18, 40]],
                     "a": [[618, 3, 900, 400, 0, 0, 5000, 5000, 2, 5, 60, 60], [26, 4, 950, 400, 0, 0, 50, 50, 0, -1, 20, 30]]} for t in range(0, 3600, 12)]
    rows = [h] + fr + events + [{"k": "nt", "type": 26, "name": "Goblin Peon", "imp": 0}, {"k": "names", "items": {}, "npcs": {"618": "Dreadnautilus", "26": "Goblin Peon"}, "projectiles": {}, "buffs": {}, "soldiers": {"5": "Pvt. Five"}, "prefixes": {}}]
    if reason is not None:
        e = {"k": "end", "kind": "event", "reason": reason, "ticks": 3600, "result": "x"}
        e.update(end_extra or {})
        rows.append(e)
    return rows


ev_goblin = [{"k": "e", "t": 600, "e": "evstart", "ev": "goblinarmy", "name": "Goblin Army"}, {"k": "e", "t": 1200, "e": "evend", "ev": "bloodmoon", "result": "survived"},
             {"k": "e", "t": 3000, "e": "evend", "ev": "goblinarmy", "result": "won"}, {"k": "e", "t": 2000, "e": "ekill", "npc": 618, "name": "Dreadnautilus", "imp": 1, "id": 3, "x": 900, "y": 400},
             {"k": "e", "t": 3000, "e": "kc", "n": 40, "mini": 1, "army": 1}]
c = R.classify_event(erec(ev_goblin))
chk(c["main"] == "goblinarmy" and c["result"] == "won" and c["results"] == {"bloodmoon": "survived", "goblinarmy": "won"}, "a Blood Moon joined by a Goblin Army: the invasion is the main event, its win is the result (%s %s)" % (c["main"], c["result"]))
chk(R.classify_event(erec(ev_goblin, reason="army gone"))["result"] == "lost", "the same file ended by the army gone reads lost")
chk(R.classify_event(erec(ev_goblin, reason=None))["result"] == "interrupted", "a cut event file (no end record) reads interrupted")
chk(R.classify_event(erec([], reason="time limit"))["result"] == "survived", "an event stopped by the hour cap reads survived")
chk(R.classify_event(erec([{"k": "e", "t": 30, "e": "wave", "n": 15, "ev": "pumpkinmoon"}], hdr_extra={"event": "pumpkinmoon", "events": ["pumpkinmoon"]}))["waveMax"] == 15, "the wave events give waveMax")
imp = R.importance_table(erec([]) + [{"k": "nt", "type": 26, "name": "Goblin Peon", "imp": 1}])
chk(imp.get(618) == 1 and imp.get(26) == 1, "importance table: the header roster, and a later nt line wins (%s)" % imp)
chk(R.is_event_recording(erec([])) and not R.is_event_recording(rec(fr_alive, ev_fled)), "an event recording is told by hdr.kind")

# ---- make_site: an event is its own row, never split, carries nt and the event header
se = ms.summarize(erec(ev_goblin, end_extra={"kills": 40, "minibossKills": 1}))
chk(se["kind"] == "event" and se["boss"] == "Goblin Army" and se["result"] == "win" and se["kills"] == 40 and se["miniKills"] == 1 and se["levies"] == 1, "make_site: an event row (kind, name, result, kills, miniboss kills, levies) %s" % {k: se[k] for k in ("kind", "boss", "result", "kills")})
chk(ms.summarize(erec([], reason="time limit"))["result"] == "survived", "make_site: survived stays survived")
blob = json.loads(ms.dump(ms.convert(erec(ev_goblin), 5)))
chk(blob["hdr"].get("kind") == "event" and blob["hdr"].get("roster") and blob.get("nt", {}).get("26") == 0 and len(blob["frames"]) >= 50, "make_site: the data file keeps kind, roster and the nt table")
with tempfile.TemporaryDirectory() as td:
    p = os.path.join(td, "event_bloodmoon_x_won.levyrec")
    with gzip.open(p, "wt") as g:
        for r in erec(ev_goblin) + [{"k": "f", "t": 4000, "p": [1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, -1, 0, 0, 0], "b": [brow(SOLAR, 115)]}]:
            g.write(json.dumps(r) + "\n")
    chk(len(ms.episode_list(p)) == 1 and ms.episode_list(p)[0][0] == "", "make_site: an event recording is never split into fights, even with a pillar in it")
    try:
        a = recview.analyse(recview.Rec(p))
        chk(a.get("event", {}).get("main") == "goblinarmy" and "Dreadnautilus" in a["event"]["minibosses"], "recview: the summary reads the event and its highlighted enemies")
    except Exception as e:
        chk(False, "recview analyse of an event raised %s: %s" % (type(e).__name__, e))

# ---- the anomaly scan finds what it is built to find
def lrow(t, lid, x, life=400, st=1):
    return [lid, x, 400, 0, 0, 1, life, st, 0, 0, 400, 18, 40]
sfr = []
for t in range(0, 60 * 40, 30):
    sfr.append({"k": "f", "t": t, "p": [1000 + t, 400, 0, 0, 1, 1, 400, 200, 0, 0, 0, 0, -1, 0, 0, 0], "b": [brow(EOC, 7, x=1100)],
                "l": [lrow(t, 1, 1050), lrow(t, 2, 1060 + (t % 200), st=3)]})
sev = [{"k": "e", "t": 900, "e": "lhit", "id": 2, "dmg": 400, "src": "body", "by": "boss", "npc": EOC, "proj": -1, "life": 0, "max": 400, "x": 1060, "y": 400, "kill": 1}]
for k in range(8):
    sev.append({"k": "e", "t": 60 * (k + 1), "e": "dmg", "d": [["levy", "melee", "proj", 9, 1, 60 if k != 5 else 5000, 1, 0, 0, 0], ["levy", "magic", "proj", 10, 1, 50, 1, 0, 0, 0],
                                                          ["levy", "ranged", "proj", 11, 1, 40, 1, 0, 0, 0], ["levy", "summon", "proj", 12, 1, 55, 1, 0, 0, 0], ["levy", "melee", "proj", 13, 1, 900, 1, 0, 0, 0]]})
srows = rec(sfr, sev)
srows[0]["stateNames"] = "Idle,Follow,Patrol,Engage"
with tempfile.TemporaryDirectory() as td:
    p = os.path.join(td, "s.levyrec")
    with gzip.open(p, "wt") as g:
        for r in srows:
            g.write(json.dumps(r) + "\n")
    found = recview.scan(recview.Rec(p))
    kinds = {d["kind"] for d in found}
    chk("idle" in kinds and any(d["kind"] == "idle" and d.get("levy") == 1 for d in found) and not any(d["kind"] == "idle" and d.get("levy") == 2 for d in found), "scan: a levy following with the boss in range for 40 s is idle; the engaging one is not")
    chk(any(d["kind"] == "onehit" and d.get("levy") == 2 for d in found), "scan: a death from one hit of full life")
    chk(any(d["kind"] == "spike" for d in found) and any(d["kind"] == "weapon" for d in found), "scan: a one-second damage spike and a weapon far above the army's median (%s)" % sorted(kinds))
    chk(any(d["kind"] == "stuck" and d.get("levy") == 1 for d in found), "scan: a levy standing still while the commander walks 40 tiles is stuck")

# ---- the kill rule (recwin): a boss that vanishes with no death hook is a boss that LEFT, whatever life it had
DOG = 796
cd_ev = {"k": "e", "t": 9334, "e": "cdeath"}
false_win = {"k": "e", "t": 9384, "e": "bdeath", "npc": DOG, "name": "The Devourer of Gods", "id": 190, "life": 14542, "max": 1836000, "final": 1, "ht": 9351, "hd": 2792, "hs": "other"}
fw = R.classify(rec([frame(t, [brow(DOG, 190)]) for t in range(0, 9500, 20)], [cd_ev, false_win], {"outcome": "killed", "result": "won"}, {str(DOG): "The Devourer of Gods"}))
chk(fw["outcome"] == "despawn" and fw["result"] == "lost", "old DoG false win (bdeath life 14542 after a cdeath, no kh) reads boss left, lost: %s" % fw)
real_win = {"k": "e", "t": 10153, "e": "bdeath", "npc": 795, "name": "The Devourer of Gods", "id": 69, "life": 1, "max": 1836000, "final": 1, "ht": 10153, "hd": 10, "hs": "other"}
rw = R.classify(rec([frame(t, [brow(795, 69)]) for t in range(0, 10200, 20)], [real_win], {"outcome": "killed", "result": "won"}, {"795": "The Devourer of Gods"}))
chk(rw["outcome"] == "killed" and rw["result"] == "won", "old DoG real win (life 1, no commander death) stays won: %s" % rw)
rw2 = R.classify(rec([frame(t, [brow(795, 69)]) for t in range(0, 10200, 20)], [cd_ev, dict(real_win, hd=10)], {}, {"795": "The Devourer of Gods"}))
chk(rw2["outcome"] == "killed", "life 1 after a commander death is a death animation, still a kill")
hk = R.classify(rec([frame(t, [brow(DOG, 190)]) for t in range(0, 9500, 20)], [cd_ev, dict(false_win, kh=1)], {}, {str(DOG): "The Devourer of Gods"}))
chk(hk["outcome"] == "killed", "a bdeath with kh 1 is believed whatever the life")
chk(not R.end_is_kill(false_win, [9334]) and R.end_is_kill(false_win, []) and R.suspect_end(false_win, []) and not R.suspect_end(false_win, [9334]), "end_is_kill / suspect_end on the DoG event")
chk(R.end_is_kill(dict(false_win, life=0), [9334]), "life 0 is always a kill")
# the real recordings, when they are on disk (LEVY_EVE_RECS = folder of her 2026-11-20 files)
eve = os.environ.get("LEVY_EVE_RECS", "")
for nm, want in (("2026-11-20_15-19_The_Devourer_of_Gods_won.jsonl.gz", "lost"), ("2026-11-20_15-29_The_Devourer_of_Gods_won.jsonl.gz", "won")):
    fp = os.path.join(eve, nm) if eve else ""
    if fp and os.path.exists(fp):
        with gzip.open(fp, "rt") as g:
            rws = [json.loads(l) for l in g if '"k":"f"' not in l[:12]]
        got = R.classify(rws)
        chk(got["result"] == want, "real recording %s reads %s (got %s, %s)" % (nm[:16], want, got["result"], got["outcome"]))
    else:
        print("SKIP real recording %s (set LEVY_EVE_RECS)" % nm[:16])

# ---- make_site: the "Hide broken or very old fights" rules (the page's hideWhy is the same rule; tested headlessly against this one)
import copy as _copy
hman = {"current": "3.1.10", "eras": [dict(e) for e in ms.ERAS], "sets": [{"id": "s", "fights": [
    {"id": "a", "ver": "2.0.13"}, {"id": "b", "ver": "2.0.63", "verAbout": True}, {"id": "c", "ver": "2.0.67"}, {"id": "d", "ver": "2.0.68"},
    {"id": "e", "ver": "3.0.6"}, {"id": "f", "ver": "3.1.8", "src": "x.levyrec"}, {"id": "g"}]}]}
hr = lambda m: {f["id"]: (ms.hidden_reason(f, m) or ("",))[0] for f in m["sets"][0]["fights"]}
chk(hr(hman) == {"a": "old", "b": "old", "c": "old", "d": "", "e": "", "f": "", "g": ""}, "hide: <= 2.0.67 very old, later or unversioned shown, outdated alone not hidden %s" % hr(hman))
m2 = _copy.deepcopy(hman); m2["eras"].append({"upTo": "3.0.9", "note": "n", "broken": True})
chk(hr(m2)["e"] == "broken" and hr(m2)["f"] == "", "hide: a broken era after the cut-off hides its fights only")
m3 = _copy.deepcopy(hman); m3["hide"] = {"oldUpTo": "2.0.13"}
chk(hr(m3)["a"] == "old" and hr(m3)["b"] == "broken" and hr(m3)["c"] == "broken", "hide: an earlier cut-off leaves the broken eras to hide the rest %s" % hr(m3))
m4 = _copy.deepcopy(hman); m4["eras"] = [{k: v for k, v in e.items() if k != "broken"} for e in m4["eras"]]
_old = ms.BROKEN[:]
ms.BROKEN[:] = [(r"^x\.levyrec$", "why")]
n4 = ms.stamp(m4)
ms.BROKEN[:] = _old
chk(m4["hide"]["oldUpTo"] == ms.HIDE["oldUpTo"] and all(e.get("broken") for e in m4["eras"]) and m4["sets"][0]["fights"][5].get("broken") == "why" and n4 == {"broken": 1, "old": 3, "shown": 3},
    "hide: stamp writes the cut-off, the default eras' flags and a per-fight reason %s" % n4)
m5 = _copy.deepcopy(m4); m5["hide"]["oldUpTo"] = "2.0.0"; ms.stamp(m5)
chk(m5["hide"]["oldUpTo"] == "2.0.0", "hide: stamp keeps a cut-off already in the manifest")
with tempfile.TemporaryDirectory() as td:
    ip = os.path.join(td, "index.txt")
    open(ip, "w").write("a--b_s1_win.levyrec\n    Won in 5 s.\n")
    chk((ms.parse_index(ip).get("a--b_s1_win.levyrec") or {}).get("note") == "Won in 5 s.", "make_site: index.txt notes work for .levyrec names")

print("fail=%d" % fails)
sys.exit(1 if fails else 0)

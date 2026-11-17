#!/usr/bin/env python3
"""The recorder's fight rules, applied to recordings already on disk. Python 3, standard library only.

Shared by recview.py (--episodes, --split) and the site builders (fightviewer/make_site.py, curate_site.py), so an old file reads the same
way a format 11 recording is written. The C# originals are FightRecorder.DecideResult / LostWindowTicks / EventNpcTypes; the format doc is the
comment at the top of FightRecFormat.cs.

RESULT RULE  (won | lost | fled | ended)
  won     the last boss was killed (bdeath).
  lost    a boss left alive (bdespawn) and the COMMANDER's fall is why: she was dead at that moment, or her latest cdeath is within
          LOST_WINDOW ticks (60 s) before it. A boss with no living player to target despawns, so her death reads as the boss leaving.
  fled    a boss left alive while the commander was alive and not recently fallen: dawn, distance, a timer. The army is not part of the
          rule: levies dead, alive or targeted change nothing, only the commander counts.
  ended   no boss end (the 20 minute limit, the recorder stopped, no boss) with no commander death; with a death and a boss seen: lost.

EVENT NPCs are not bosses: the four Celestial Pillars, the Martian Saucer and its parts, the Flying Dutchman and its cannons and the
Pumpkin Moon / Frost Moon wave bosses never start, extend or merge into a boss fight. Recordings made before format 11 kept the pillars in the
fight, so a Lunatic Cultist win became "Cultist, Solar Pillar, ... lost" and a pillar standing from the start glued every later boss into one
file. episodes() cuts such a file where no real boss stood for longer than the recorder's own grace and reads each part by the rule above.
"""
import copy, datetime, re

TICKS = 60
LOST_WINDOW = 60 * TICKS          # FightRecorder.LostWindowTicks
AUTO_GRACE = 300                  # FightRecorder.AutoGraceTicks: an automatic recording ends this long after the last boss is gone
LEAD = 60                         # ticks kept before an episode's first boss frame (the real recorder starts a little before the boss is in view)
DEAD_FLAG = 8192                  # frame p[5]: the commander is dead

# NPC ids (Terraria): pillars Solar 517, Vortex 422, Nebula 507, Stardust 493; Martian Saucer 392 turret 393 cannon 394 core 395; Flying Dutchman 491 and its cannon 492;
# Mourning Wood 325, Pumpking 327 and its blade 328, Everscream 344, Ice Queen 345, Santa-NK1 346
EVENT_NPC_TYPES = frozenset([517, 422, 507, 493, 392, 393, 394, 395, 491, 492, 325, 327, 328, 344, 345, 346])


def is_event_npc(t):
    return t in EVENT_NPC_TYPES


def decide(outcome, cmd_deaths, dead_at_end, end_tick, last_death_tick, bosses_seen=1):
    """FightRecorder.DecideResult. outcome: killed | despawn | none."""
    if outcome == "killed":
        return "won"
    if outcome == "despawn":
        fell = cmd_deaths > 0 and last_death_tick is not None and last_death_tick >= 0 and -10 <= end_tick - last_death_tick <= LOST_WINDOW
        return "lost" if (dead_at_end or fell) else "fled"
    return "lost" if cmd_deaths > 0 and bosses_seen > 0 else "ended"


def _frames(rows):
    return [r for r in rows if r.get("k") == "f"]


def _events(rows):
    return [r for r in rows if r.get("k") == "e"]


def dead_at(frames, tick):
    """True when the commander's last frame at or before tick has the dead flag."""
    last = None
    for f in frames:
        if f["t"] > tick:
            break
        last = f
    return bool(last and len(last.get("p", [])) > 5 and int(last["p"][5]) & DEAD_FLAG)


def final_end(events):
    """The event naming the end of a fight: among bdeath / bdespawn events (event NPCs left out), the last one that was a kill, else the last one
    (FightRecorder.FinalEndIndex). None when there is none. Events carry final 0 or 1; a file that kept the pillars alive wrote final 0 for the Cultist."""
    ends = [e for e in events if e.get("e") in ("bdeath", "bdespawn") and not is_event_npc(e.get("npc", -1))]
    if not ends:
        return None
    flagged = [e for e in ends if e.get("final", 1) != 0]
    pool = flagged or ends
    kills = [e for e in pool if e["e"] == "bdeath"]
    return (kills or pool)[-1]


def classify(rows, t0=None, t1=None):
    """Result of a whole recording (or the ticks t0..t1 of it) by the rule. Returns dict(result, outcome, endTick, cmdDeaths, deadAtEnd, lastDeath)."""
    ev = _events(rows)
    fr = _frames(rows)
    if t0 is not None:
        ev = [e for e in ev if t0 <= e["t"] <= t1]
        fr = [f for f in fr if t0 <= f["t"] <= t1]
    deaths = [e["t"] for e in ev if e.get("e") == "cdeath"]
    fin = final_end(ev)
    seen = any(e.get("e") == "boss" and not is_event_npc(e.get("npc", -1)) for e in ev) or fin is not None or any(b[13] == 0 and not is_event_npc(b[0]) for f in fr for b in f.get("b", []))
    if fin is None:
        outcome, end_tick = "none", None
    else:
        outcome, end_tick = ("killed" if fin["e"] == "bdeath" else "despawn"), fin["t"]
    if outcome == "despawn":
        # deaths after the boss was gone do not count; the rule looks at the latest fall at or before its end
        before = [d for d in deaths if d <= end_tick + 10]
    else:
        before = deaths
    last = before[-1] if before else None
    dead = outcome == "despawn" and dead_at(fr, end_tick)
    result = decide(outcome, len(before) if outcome == "despawn" else len(deaths), dead, end_tick if end_tick is not None else -1, last, 1 if seen else 0)
    return {"result": result, "outcome": outcome, "endTick": end_tick, "cmdDeaths": len(deaths), "deadAtEnd": dead, "lastDeath": last, "bossSeen": seen}


# ------------------------------------------------------------------ episodes
def has_event_npcs(rows):
    for f in _frames(rows):
        for b in f.get("b", []):
            if is_event_npc(b[0]):
                return True
    return any(e.get("e") in ("boss", "bdeath", "bdespawn") and is_event_npc(e.get("npc", -1)) for e in _events(rows))


def _present(f):
    return any(b[13] == 0 and not is_event_npc(b[0]) for b in f.get("b", []))


def episode_spans(rows, grace=AUTO_GRACE):
    """[(start_tick, end_tick)] of the fights in the recording: runs of frames with a real main boss (event NPCs ignored), joined when the gap is no longer than grace.
    A fight runs from LEAD ticks before its first boss frame to grace ticks after its last (what the live recorder would have kept)."""
    fr = _frames(rows)
    if not fr:
        return []
    last_t = fr[-1]["t"]
    runs = []
    for f in fr:
        if not _present(f):
            continue
        if runs and f["t"] - runs[-1][1] <= grace:
            runs[-1][1] = f["t"]
        else:
            runs.append([f["t"], f["t"]])
    out, prev_end = [], -1
    for a, b in runs:
        s = max(fr[0]["t"], a - LEAD, prev_end + 1)
        e = min(last_t, b + grace)
        out.append((s, e))
        prev_end = e
    return out


def episodes(rows, force=False):
    """Split a recording into its fights. Only a recording that contains event NPCs (pillars and the like) is split; any other comes back whole as one
    episode (rows untouched, end.result re-read by the rule). Each episode is a dict(rows, name, result, ...) whose rows form a complete recording of
    its own (ticks re-based to 0, one synthesized end record carrying the result, hdr.split saying where it came from)."""
    hdr = next((r for r in rows if r.get("k") == "hdr"), {})
    if not (force or has_event_npcs(rows)) or (hdr.get("split") and not force):   # an episode already cut out stays whole
        return [{"rows": rows, "whole": True, **classify(rows)}]
    spans = episode_spans(rows)
    if not spans:
        return [{"rows": rows, "whole": True, **classify(rows)}]
    out = []
    for n, (s, e) in enumerate(spans, 1):
        out.append(_cut(rows, s, e, n, len(spans)))
    return out


ARENA_NEAR = 60                   # tiles: an arena record this close to the action stays with a split fight
CMD_NEAR = 100                    # tiles: the commander counts as in the fight only this close to the main boss (not on her way there, not respawned at base)


def action_points(rows):
    """[(x, y)] in tiles where the fight happened: the main boss in every frame it stood (event NPCs left out), and the living commander in
    those frames when she was within CMD_NEAR tiles of it."""
    pts = []
    for f in _frames(rows):
        mb = next((b for b in f.get("b", []) if b[13] == 0 and not is_event_npc(b[0])), None)
        if mb is None:
            continue
        bx, by = mb[2] / 16.0, mb[3] / 16.0
        pts.append((bx, by))
        p = f.get("p") or []
        if len(p) > 5 and not int(p[5]) & DEAD_FLAG:
            cx, cy = p[0] / 16.0, p[1] / 16.0
            if abs(cx - bx) <= CMD_NEAR and abs(cy - by) <= CMD_NEAR:
                pts.append((cx, cy))
    return pts


def fight_arenas(rows, near=ARENA_NEAR):
    """The arena (terrain) records that touch the fight: those overlapping or within `near` tiles of an action point. A recording split from a
    long one carries every arena of its source (older recorders captured terrain only where the recording began), so a fight held far from
    there keeps none, and the page says no terrain was recorded instead of framing a map from somewhere else."""
    arenas = [r for r in rows if r.get("k") == "arena"]
    pts = action_points(rows)
    if not pts:
        return arenas
    pts = pts[::max(1, len(pts) // 4000)]
    keep = []
    for a in arenas:
        x0, y0 = a.get("x0", 0) - near, a.get("y0", 0) - near
        x1, y1 = a.get("x0", 0) + a.get("w", 0) + near, a.get("y0", 0) + a.get("h", 0) + near
        if any(x0 <= x <= x1 and y0 <= y <= y1 for x, y in pts):
            keep.append(a)
    return keep


def _shift(o, base, keys=("t",)):
    o = dict(o)
    for k in keys:
        if k in o and isinstance(o[k], (int, float)):
            o[k] = o[k] - base
    return o


def _cut(rows, s, e, n, total):
    hdr = next((r for r in rows if r.get("k") == "hdr"), {})
    old_end = next((r for r in reversed(rows) if r.get("k") == "end"), {})
    fr = [f for f in _frames(rows) if s <= f["t"] <= e]
    ev = [x for x in _events(rows) if s <= x["t"] <= e]
    # names of the main bosses that stood in this span, in order of appearance (event NPCs left out)
    names_map = {}
    for r in rows:
        if r.get("k") == "names":
            names_map = r.get("npcs", {})
    seen_types, order = set(), []
    for f in fr:
        for b in f.get("b", []):
            if b[13] == 0 and not is_event_npc(b[0]) and b[0] not in seen_types:
                seen_types.add(b[0]); order.append(b[0])
    nm = [names_map.get(str(t), "npc %d" % t) for t in order]
    # the Twins read as one name, as the recorder names them
    if "Retinazer" in nm and "Spazmatism" in nm:
        nm = ["The Twins"] + [x for x in nm if x not in ("Retinazer", "Spazmatism")]
    name = nm[0] if len(nm) == 1 else (", ".join(nm[:-1]) + " and " + nm[-1] if nm else (hdr.get("boss") or "Fight").replace("_", " "))
    c = classify(rows, s, e)
    last_span = n == total
    reason = old_end.get("reason", "boss gone") if last_span else "boss gone"
    dm_boss = dm_all = 0
    for x in ev:
        if x.get("e") == "dmg":
            for d in x.get("d", []):
                if len(d) >= 7:
                    dm_all += d[5]
                    if d[4]:
                        dm_boss += d[5]
    end = {k: v for k, v in old_end.items()}
    end.update({"k": "end", "reason": reason, "ticks": e - s, "frames": len(fr), "events": len(ev), "outcome": c["outcome"], "result": c["result"],
                "durationSec": round((e - s) / TICKS, 1), "bossName": name, "bosses": len(order), "cmdDeaths": c["cmdDeaths"],
                "levyDeaths": sum(1 for x in ev if x.get("e") == "ldeath"), "dmgBoss": dm_boss, "dmgAll": dm_all, "reclassified": 1})
    new_hdr = dict(hdr)
    new_hdr["boss"] = re.sub(r"[^A-Za-z0-9]", "_", name)
    meta = dict(hdr.get("meta") or {})
    meta["bossName"] = name
    new_hdr["meta"] = meta
    new_hdr["split"] = {"episode": n, "of": total, "t0": s, "t1": e}
    if hdr.get("date"):                       # the episode's own start time, so each fight shows (and sorts by) when it began
        try:
            d0 = datetime.datetime.fromisoformat(hdr["date"])
            new_hdr["date"] = (d0 + datetime.timedelta(seconds=round(s / TICKS))).isoformat(timespec="seconds")
            new_hdr["split"]["srcDate"] = hdr["date"]
        except (TypeError, ValueError):
            pass
    out = [new_hdr]
    # only the terrain this fight touched (the source carries every arena of the whole recording)
    near = set(id(a) for a in fight_arenas([r for r in rows if r.get("k") == "arena"] + fr))
    for r in rows:
        k = r.get("k")
        if k in ("cam", "names") or (k == "arena" and id(r) in near):
            out.append(r)
    out.extend(_shift(f, s) for f in fr)
    out.extend(_shift(x, s) for x in ev)
    out.append(end)
    # order: hdr, arena/cam/names (kept as they were), frames and events interleaved by tick, end
    head = [r for r in out if r.get("k") in ("hdr", "arena", "cam")]
    body = sorted([r for r in out if r.get("k") in ("f", "e")], key=lambda r: (r["t"], 0 if r["k"] == "f" else 1))
    tail = [r for r in out if r.get("k") in ("names", "end")]
    return {"rows": head + body + tail, "whole": False, "name": name, "start": s, "end": e, "episode": n, "of": total, **c}


def file_name(src_name, ep):
    """YYYY-MM-DD_HH-MM_Boss_result[_epN].levyrec for an episode: the stamp is the minute the fight began (from its header), else the source's stamp; _epN marks which fight of the source it was."""
    m = re.match(r"^(\d{4}-\d{2}-\d{2}_\d{2}-\d{2})_", src_name)
    date = ((ep.get("rows") or [{}])[0].get("date") or "") if ep.get("rows") else ""
    dm = re.match(r"^(\d{4}-\d{2}-\d{2})T(\d{2}):(\d{2})", date)
    # the episode's own start minute when the header has it (a source named without a stamp gets one), else the source's stamp
    stamp = ("%s_%s-%s" % dm.groups()) if dm else (m.group(1) if m else "split")
    safe = re.sub(r"[^A-Za-z0-9]", "_", ep.get("name") or "Fight")
    return "%s_%s_%s%s.levyrec" % (stamp, safe, ep["result"], "" if ep.get("of", 1) == 1 else "_ep%d" % ep["episode"])

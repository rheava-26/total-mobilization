#!/usr/bin/env python3
"""Build the hosted Levy fight-replay site.

usage: make_site.py <outdir> <set-name>=<dir> [<set-name>=<dir> ...] [--hz 15] [--keep]

  make_site.py /tmp/replay-site lab1=replays/notable lab2=replays/notable2 worm=replays/worm5 options=replays/options lab3=replays/lab3

For every set directory it reads the recordings (*.levyrec, *.jsonl or *.jsonl.gz, FightRecorder format 1-13; a format 13 recording's NAME_part2.levyrec ... are joined to NAME.levyrec), the optional index.txt
("why it's notable" notes) and each recording's header meta, and writes

  make_site.py <outdir> <sets...> --auto <dir> [--auto <dir2> ...] [--per-cell 2] [--max-auto 110] [--batches 55] [--cache scan.json]
  adds an even spread of fights from every recording under the --auto folders (one set per boss, notes generated from the recording)
  and, with --batches MB, writes <outdir>/b01, b02 ... each a complete publishable page (see batches.txt for the order).

  <outdir>/index.html            copy of site/index.html (the page, no data inside)
  <outdir>/data/manifest.json    the fight list: one row per fight with boss, mode, levies, arena, result, targeting %, hits, deaths, note
  <outdir>/data/<id>.json        one plain-JSON file per fight (frames downsampled to --hz, coordinates rounded, levy and boss rows delta-coded), loaded lazily by the page

Adding a set is just adding another <set-name>=<dir> argument and re-running. Sets show in the order given.
Set names lab1 lab2 lab3 worm options get nice titles; any other name is shown with underscores as spaces.
The whole <outdir> is what gets published (static files, relative URLs only). Old data/*.json is removed first so renamed fights leave no strays.
"""
import argparse, glob, gzip, json, os, re, shutil, sys

HERE = os.path.dirname(os.path.abspath(__file__))
# the recorder's fight rules (result word, event NPCs, splitting a recording into its fights) live with the offline tool
sys.path.insert(0, os.path.join(HERE, "..", "..", "..", "tools", "recview"))
try:
    import recrules
except ImportError:      # the page still builds without it; results then come from the old frame-based reading
    recrules = None
import reccodec          # format 13: compact frame rows and part files (tools/recview/reccodec.py)
TITLES = {"eve": "Eve's fights", "rec2": "Recorder test", "lab1": "Lab 1", "lab2": "Lab 2", "lab3": "Lab 3", "worm": "Worm round", "worm5": "Worm round", "options": "Options", "vert1": "Movement lab", "moblab": "Mobility gear lab", "adds": "Boss minions", "wormdodge": "Worm dodge lab", "wormbody": "Worm body lab", "eowrev": "Eater of Worlds: Rev vs Death", "deathladder": "Death EoW gear ladder", "paladin": "Paladin's Shield lab"}
MODES = {"exp": "Expert", "rev": "Rev", "death": "Death", "revoff": "Rev, rage off", "mdeath": "Master+Death"}
ARENAS = {"surf": "Surface", "ug": "Underground"}
FLED_PX = 50000          # a boss that ends this far from where it began has left the fight
MAX_FILE = 14 * 1048576  # hard limit is 15 MB per data file
MAX_TOTAL = 55 * 1048576  # hard limit is 60 MB for the site (curate_site.py works to its own, larger --max-mb)
MAX_C_ROWS = 120         # format 11 key c (the commander's own projectiles): rows kept per sample; 0 drops the layer. --cmd-cap changes it


# ---------------------------------------------------------------- versions
# Mechanics eras: a fight recorded on Levy <= upTo (and older than CURRENT) is outdated for that reason. The lead appends eras and updates CURRENT in
# data/manifest.json (no data rebuild); these are the defaults a build writes.
ERAS = [
    {"upTo": "2.0.63", "note": "Levies were hit by bosses where you would not be."},
    {"upTo": "2.0.65", "note": "Boss shots never hit levies when the commander had a Spectator potion."},
    {"upTo": "2.0.67", "note": "Boss shots did about 1 damage to levies; touching a boss did 3 to 7 times too much."},
]
CURRENT = "2.0.68 (not yet released)"
# recording format -> (oldest, newest) Levy version known to write it; used only when a recording carries no version
FORMAT_VERS = {1: ("2.0.0", "2.0.12"), 2: ("2.0.0", "2.0.12"), 3: ("2.0.13", "2.0.19"), 4: ("2.0.13", "2.0.19"), 5: ("2.0.20", "2.0.20"),
               6: ("2.0.21", "2.0.25"), 7: ("2.0.26", "2.0.40"), 8: ("2.0.41", "2.0.63"), 9: ("2.0.64", "2.0.99"), 10: ("3.0.0", "3.0.9"), 11: ("3.0.10", "3.9.99"), 12: ("3.0.10", "3.9.99"), 13: ("3.1.9", "3.9.99")}
EVENT_MAX_FACTOR = 60    # an event recording (format 12, 5 Hz, up to an hour) may be thinned to one sample every 12 s to fit a data file
EVENT_HZ = 1             # the default rate of an event's data file (the page interpolates between samples)


def version_of(hdr):
    """(version string, about?) for a recording header: the Levy entry of the mods list (format 8+), else hdr.levy, else the newest version
    the format number allows, flagged 'about'."""
    for m in hdr.get("mods") or []:
        if isinstance(m, (list, tuple)) and len(m) >= 2 and str(m[0]).lower() == "levy":
            return str(m[1]), False
    if hdr.get("levy"):
        return str(hdr["levy"]), False
    rng = FORMAT_VERS.get(hdr.get("format", 1), ("2.0.0", "2.0.12"))
    return rng[1], True


# ---------------------------------------------------------------- reading
def load_one(path):
    rows = []
    for line in reccodec.read_lines(path):
        try:
            rows.append(json.loads(line))
        except ValueError:
            pass
    return rows


def load(path):
    """Every record of a recording, its frame rows decoded (format 13) and its part files (NAME_part2.levyrec ...) joined back into one fight."""
    return reccodec.join_parts([load_one(p) for p in reccodec.parts_of(path)])


def is_later_part(path):
    return reccodec.part_of(path)[1] >= 2


def episode_list(path):
    """[(suffix, rows)] for one recording: the whole file, or, when it holds event NPCs (Celestial Pillars and the like that older recorders kept
    in the fight), its fights apart, suffix -ep1, -ep2 ... (see tools/recview/recrules.py)."""
    rows = load(path)
    if recrules is None or not any(r.get("k") == "f" for r in rows) or recrules.is_event_recording(rows):
        return [("", rows)]
    eps = recrules.episodes(rows)
    if len(eps) == 1 and eps[0].get("whole"):
        return [("", rows)]
    return [("-ep%d" % e["episode"], e["rows"]) for e in eps]


def parse_index(path):
    """index.txt -> {filename: {"note": str, "result": str|None}}. A line naming files ('a.jsonl.gz + b.jsonl.gz') is
    followed by indented lines: the first is the note, a later one may carry 'WIN' / 'LOSS' / 'TIMEOUT'."""
    out = {}
    if not os.path.isfile(path):
        return out
    cur = None
    for raw in open(path, encoding="utf-8"):
        line = raw.rstrip("\n")
        if not line.strip():
            continue
        if not line[0].isspace() and ".jsonl" in line:
            names = [n.strip() for n in line.split("+") if n.strip()]
            cur = {"names": names, "note": None, "result": None}
            for n in names:
                out[n] = cur
        elif cur is not None and line[0].isspace():
            t = line.strip()
            if cur["note"] is None:
                cur["note"] = t
            m = re.search(r"\b(WIN|LOSS|TIMEOUT)\b", t)
            if m and cur["result"] is None and t is not cur["note"]:
                cur["result"] = m.group(1).lower()
    return out


# ---------------------------------------------------------------- summary
def tkind(b):
    if len(b) > 14:
        return b[14]
    return 1 if 0 <= b[12] < 255 else 0


def main_boss(f):
    """The row the fight follows in a sample: of the bosses (not parts) the one with the largest life pool, a pool's owner first (format 13 field 18 realLife: a worm's head, not a segment; Ares, not
    an arm), the lowest life on a tie (a worm's live head before segments frozen at full life). Draedon's own 16,000 never beats a mech. Event NPCs (Celestial Pillars and the like) are never the main boss.
    The page's mainBoss() is the same rule."""
    bs = [b for b in (f.get("b") or []) if not (recrules and recrules.is_event_npc(b[0]))]
    if not bs:
        return None
    heads = [b for b in bs if not b[13]] or bs
    own = [b for b in heads if len(b) <= 18 or b[18] < 0 or b[18] == b[1]] or heads
    return max(own, key=lambda b: (b[7], -b[6]))


def summarize_event(rows, hdr):
    """Format 12 event recording: the manifest row of the Events section (event name, duration, result, kills, levy deaths, miniboss kills)."""
    c = recrules.classify_event(rows)
    ids = set()
    for f in rows:
        if f.get("k") == "f":
            for l in f.get("l") or []:
                ids.add(l[0])
    ver, about = version_of(hdr)
    res = {"won": "win", "lost": "loss"}.get(c["result"], c["result"])
    return {"ver": ver, "verAbout": about, "date": hdr.get("date"), "kind": "event", "event": c["main"], "boss": c["name"], "mode": hdr.get("mode") or "?",
            "arena": ", ".join(recrules.event_name(x) for x in c["seen"]), "levies": len(ids), "result": res, "rule": c["result"], "dur": c["dur"],
            "kills": c["kills"], "miniKills": c["minibossKills"], "deaths": c["levyDeaths"], "waveMax": c["waveMax"],
            "hits": sum(1 for r in rows if r.get("k") == "e" and r.get("e") == "lhit"), "dmg": 0, "pl": 0, "pp": 0, "ps": 0, "hasT": False, "bossEnd": None,
            "format": hdr.get("format", 12), "seed": None, "gear": None, "cmdDeaths": sum(1 for r in rows if r.get("k") == "e" and r.get("e") == "cdeath")}


def pretty_team(s):
    s = str(s or "").replace("_", " ").strip()
    return s[:1].upper() + s[1:] if s else "?"


def pvp_summary(rows, hdr):
    """PvP lab fight (meta.pvp == "1"): who won. Same rule as the page (index.html, pvpBuild).
    idsA / idsB hold only the first three soldier ids of each side; later levies are reinforcements with new ids. A levy's side:
    its id is a seed id; else the army-name prefix learnt from the seeds ("Opt." = the optimal army), when the seed sides differ in it;
    else the seed side it first stood nearest to. The winner is the side with levies left standing at the end (both or neither: draw)."""
    meta = hdr.get("meta") or {}
    if str(meta.get("pvp")) != "1":
        return None
    ids = lambda t: [int(x) for x in str(t or "").split(",") if x.strip().lstrip("-").isdigit()]
    A, B = ids(meta.get("idsA")), ids(meta.get("idsB"))
    names = (next((r for r in rows if r.get("k") == "names"), None) or {}).get("soldiers") or {}
    is_opt = lambda i: str(names.get(str(i)) or "").lower().startswith("opt.")
    first = {}
    for f in rows:
        if f.get("k") == "f":
            for l in f.get("l") or []:
                first.setdefault(l[0], l[1])
    dead = {e.get("id") for e in rows if e.get("k") == "e" and e.get("e") == "ldeath"}

    def mean(a):
        v = [first[i] for i in a if i in first]
        return sum(v) / len(v) if v else None

    def maj(a):
        k = [is_opt(i) for i in a if str(i) in names]
        return -1 if not k else (1 if sum(k) * 2 > len(k) else 0 if sum(k) * 2 < len(k) else -1)
    cA, cB, kA, kB = mean(A), mean(B), maj(A), maj(B)
    by_name = kA >= 0 and kB >= 0 and kA != kB
    alive = [0, 0]
    for i in first:
        if i in dead:
            continue
        if i in A:
            sd = 0
        elif i in B:
            sd = 1
        elif by_name and str(i) in names:
            sd = 0 if int(is_opt(i)) == kA else 1
        elif cA is not None and cB is not None:
            sd = 0 if abs(first[i] - cA) <= abs(first[i] - cB) else 1
        else:
            sd = 0
        alive[sd] += 1
    win = "a" if alive[0] and not alive[1] else "b" if alive[1] and not alive[0] else "draw"
    return {"a": meta.get("teamA") or "A", "b": meta.get("teamB") or "B", "win": win, "aliveA": alive[0], "aliveB": alive[1],
            "cut": not any(r.get("k") == "end" for r in rows)}


def summarize(rows):
    hdr = next((r for r in rows if r.get("k") == "hdr"), {})
    if recrules is not None and recrules.is_event_recording(hdr):
        return summarize_event(rows, hdr)
    meta = hdr.get("meta") or {}
    frames = [r for r in rows if r.get("k") == "f"]
    ev = [r for r in rows if r.get("k") == "e"]
    tally = [0, 0, 0, 0]   # kinds 0 none, 1 commander, 2 levy, 3 spite (format 9)
    ids, bx0, far = set(), None, 0.0
    for f in frames:
        for l in f.get("l") or []:
            ids.add(l[0])
        mb = main_boss(f)
        if mb is None:
            continue
        tally[tkind(mb)] += 1
        if bx0 is None:
            bx0 = (mb[2], mb[3])
        far = max(far, abs(mb[2] - bx0[0]), abs(mb[3] - bx0[1]))
    n = max(1, sum(tally))
    t0 = frames[0]["t"] if frames else 0
    t1 = frames[-1]["t"] if frames else 0
    dur = (t1 - t0) / 60.0
    lhit = [e for e in ev if e.get("e") == "lhit"]
    deaths = sum(1 for e in ev if e.get("e") == "ldeath")
    m = re.search(r"_n(\d+)", str(meta.get("gear") or ""))
    n_levies = int(m.group(1)) if m else len(ids)
    gone = len(frames) >= 3 and all(main_boss(f) is None for f in frames[-3:])   # the boss left the world before the recording ended
    life = None
    for f in reversed(frames):
        mb = main_boss(f)
        if mb is not None:
            life = mb[6] / max(1, mb[7])
            break
    # format 4 records the boss's end explicitly (bdeath: it died, bdespawn: it left alive); older files are inferred from the frames
    bend = [e for e in ev if e.get("e") in ("bdeath", "bdespawn")]
    fin = ([e for e in bend if e.get("final", 1) != 0] or bend or [None])[-1]
    # recordings made before the worm-kill fix logged a worm that died to a body-segment blow as bdespawn: a leave with <=1% life and a hit within ~1 s is a kill.
    # Only for files with no kh (the death-hook field, written since 2026-11-20) and when the commander had not just fallen: a boss that Calamity removed after her death is a boss that left, whatever its life.
    cdeaths = [e["t"] for e in ev if e.get("e") == "cdeath"]
    if fin is not None and fin["e"] == "bdespawn" and "kh" not in fin and fin.get("max", 0) > 0 and fin.get("life", 1) <= 0.01 * fin["max"] and fin.get("ht") is not None and fin["t"] - fin["ht"] <= 60 \
            and not any(fin["t"] - 3600 <= d <= fin["t"] + 10 for d in cdeaths):
        fin = dict(fin, e="bdeath", inferred=True)
    # the other direction: an old bdeath that no death hook and no lethal blow explains, right after the commander's fall, is a boss that left (recrules.end_is_kill)
    if fin is not None and fin["e"] == "bdeath" and recrules is not None and not recrules.end_is_kill(fin, cdeaths):
        fin = dict(fin, e="bdespawn", rederived=True)
    if fin is not None and fin["e"] == "bdeath":
        result = "win"
    elif fin is not None and n_levies and deaths >= n_levies:
        result = "loss"
    elif fin is not None:
        result = "despawn"
    elif far > FLED_PX and not gone:
        result = "fled"
    elif n_levies and deaths >= n_levies:
        result = "loss"
    elif gone or (life is not None and life <= 0.02):
        result = "win"
    elif dur >= 295:
        result = "timeout"
    else:
        result = "ended"
    # a recording of real play (the automatic recorder, format 8+) is read by the recorder's result rule: the commander's fall makes a boss that leaves a LOSS, not "the boss left"
    cmd_deaths = sum(1 for e in ev if e.get("e") == "cdeath")
    rule = None
    if recrules is not None and hdr.get("auto") in (1, 2) and any(e.get("e") in ("bdeath", "bdespawn", "boss") for e in ev):
        rule = recrules.classify(rows)
        result = {"won": "win", "lost": "loss", "fled": "fled"}.get(rule["result"], "ended")
    mode = MODES.get(meta.get("mode")) or (hdr.get("mode") or "?").replace("Expert+Revengeance", "Rev")
    arena = ARENAS.get(meta.get("arena"), meta.get("arena") or "?")
    # a hand-made recording (/levyrec start <name>) carries its file name in hdr.boss: name the fight after the boss it actually met, or "Other fights" when none
    named = [e.get("name") for e in ev if e.get("e") == "boss" and e.get("name")]
    boss = meta.get("bossName") or (named[0] if named and not meta else None) or ("Other fights" if not meta else (hdr.get("boss") or "?").replace("_", " "))
    ver, about = version_of(hdr)
    pvp = pvp_summary(rows, hdr)
    if pvp:
        boss = pretty_team(pvp["a"]) + " vs " + pretty_team(pvp["b"])
    return {
        "pvp": pvp,
        "ver": ver, "verAbout": about, "date": hdr.get("date"),
        "boss": boss, "mode": mode, "arena": arena, "levies": max(n_levies, len(ids)), "result": result,
        "pl": round(100.0 * tally[2] / n, 1), "pp": round(100.0 * tally[1] / n, 1), "ps": round(100.0 * tally[3] / n, 1),
        "hits": len(lhit), "dmg": sum(e.get("dmg", 0) for e in lhit), "deaths": deaths,
        "dur": round(dur, 1), "hasT": any(len(b) > 14 for f in frames for b in (f.get("b") or [])[:1]),
        "bossEnd": ({"kind": "killed" if fin["e"] == "bdeath" else "despawn", "t": round(fin["t"] / 60.0, 1)} if fin else None),
        "format": hdr.get("format", 1), "seed": meta.get("seed"), "gear": meta.get("gear"),
        "cmdDeaths": cmd_deaths, "rule": rule["result"] if rule else None,
    }


# ---------------------------------------------------------------- shrinking
def r2(v):
    if isinstance(v, float):
        v = round(v, 2)
        return int(v) if v == int(v) else v
    return v


def ri(v):
    return int(round(v)) if isinstance(v, (int, float)) else v


def r1(v):
    if isinstance(v, float):
        v = round(v, 1)
        return int(v) if v == int(v) else v
    return v


def row(a, ints=(), ones=()):
    return [ri(v) if i in ints else r1(v) if i in ones else r2(v) for i, v in enumerate(a)]


def expand_hitboxes(frames):
    """Format 7 frame key s: the first row of an NPC slot (or a changed one) is full [type,id,x,y,w,h,realLife,flags], later rows are [id,x,y].
    Expand every row to the full form, walking ALL frames in order (the short rows lean on a full row that may sit in a frame the downsampler drops)."""
    defs = {}
    for f in frames:
        s = f.get("s")
        if not s:
            continue
        out = []
        for r in s:
            if len(r) >= 8:
                defs[r[1]] = r
                out.append(r)
            else:
                d = defs.get(r[0])
                if d is not None:
                    out.append([d[0], r[0], r[1], r[2], d[4], d[5], d[6], d[7]])
        f["s"] = out


def shrink_frame(f):
    o = {"t": f["t"], "p": row(f["p"], ints=(0, 1, 13, 14))}
    if f.get("l"):
        o["l"] = [row(l, ints=(1, 2)) for l in f["l"]]
    if f.get("b"):
        o["b"] = [row(b, ints=(2, 3), ones=(8, 9, 10, 11)) for b in f["b"]]
    if f.get("a"):
        o["a"] = [row(a, ints=(2, 3)) for a in f["a"]]
    if f.get("s"):
        o["s"] = [[int(round(v)) for v in r] for r in f["s"]]
    if f.get("j"):
        o["j"] = [row(j[:8], ints=(1, 2)) for j in f["j"]]      # format 13 adds the slot (field 8): the page has no use for it
    if f.get("c") and MAX_C_ROWS > 0:
        # the commander's own projectiles: [type,x,y,vx,vy,w,h], positions and sizes as integers, velocity to a tenth; the nearest-listed MAX_C_ROWS stay
        o["c"] = [row(c[:7], ints=(0, 1, 2, 5, 6), ones=(3, 4)) for c in f["c"][:MAX_C_ROWS]]
    return o


def enc_rows(rows, last, idx, ipos):
    """Delta-code rows keyed by r[idx] against the previous row of the same key (the page's decRows is the inverse).
    first sight / odd length: the row itself. unchanged: the bare key. else [-key-1, bitmask of changed fields, values...];
    fields in ipos (integer positions) are stored as differences."""
    out = []
    for r in rows:
        key = r[idx]
        prev = last.get(key)
        last[key] = r
        if key < 0 and idx == 0:
            # format 8: officers, militia and drones carry negative ids; a bare row starting with a negative number would read as a delta row
            out.append({"r": r})
            continue
        if prev is None or len(prev) != len(r) or key < 0:
            out.append(r)
            continue
        ch = [i for i in range(len(r)) if i != idx and r[i] != prev[i]]
        if not ch:
            out.append(key)
            continue
        if any(i in ipos and not (isinstance(r[i], int) and isinstance(prev[i], int)) for i in ch):
            out.append(r)
            continue
        out.append([-key - 1, sum(1 << i for i in ch)] + [r[i] - prev[i] if i in ipos else r[i] for i in ch])
    return out


def shrink_event(e):
    return {k: (r1(v) if isinstance(v, float) else v) for k, v in e.items() if k != "k"}


def convert(rows, factor):
    hdr = next((r for r in rows if r.get("k") == "hdr"), {})
    frames = [r for r in rows if r.get("k") == "f"]
    expand_hitboxes(frames)
    keep = frames[::factor]
    if frames and keep[-1] is not frames[-1]:
        keep.append(frames[-1])
    hz = hdr.get("hz", 30)
    out = {
        "v": 4,
        "hdr": {k: hdr[k] for k in ("format", "meta", "mode", "boss", "stateNames", "levy", "date", "split", "kind", "event", "eventName", "events", "roster", "wave", "worldW", "worldH") if k in hdr},
        "frames": [],
        "events": [shrink_event(e) for e in rows if e.get("k") == "e"],
    }
    lastL, lastB, lastA, lastS = {}, {}, {}, {}
    for f in keep:
        o = shrink_frame(f)
        if "l" in o:
            o["l"] = enc_rows(o["l"], lastL, 0, (1, 2))
        if "b" in o:
            o["b"] = enc_rows(o["b"], lastB, 1, (2, 3))
        if "a" in o:
            o["a"] = enc_rows(o["a"], lastA, 1, (2, 3))
        if "s" in o:
            o["s"] = enc_rows(o["s"], lastS, 1, (2, 3))
        out["frames"].append(o)
    out["hdr"]["hz"] = max(1, hz // factor)
    for k in ("names", "end", "cam"):
        r = next((x for x in rows if x.get("k") == k), None)
        if r:
            out[k] = {kk: vv for kk, vv in r.items() if kk != "k"}
    # format 12: enemy importance by type (0 normal, 1 miniboss, 2 boss; the last nt line of a type wins)
    nt = {}
    for r in rows:
        if r.get("k") == "nt":
            nt[str(r["type"])] = r.get("imp", 0)
            out.setdefault("names", {}).setdefault("npcs", {}).setdefault(str(r["type"]), r.get("name"))
    if nt:
        out["nt"] = nt
    # terrain: format 4 writes several arena records (start area, growth strips, camera view); the page merges them. Older files have one.
    # A fight split from a longer recording keeps only the arenas near its own action (files split before recrules did this carry them all).
    ar_rows = recrules.fight_arenas(rows) if (recrules is not None and hdr.get("split")) else [r for r in rows if r.get("k") == "arena"]
    arenas = [{kk: vv for kk, vv in r.items() if kk != "k"} for r in ar_rows]
    if arenas:
        out["arenas"] = arenas
    return out


def dump(o):
    return json.dumps(o, separators=(",", ":")).encode("utf-8")


# ---------------------------------------------------------------- build
def slug(s):
    return re.sub(r"[^A-Za-z0-9]+", "-", s).strip("-").lower()


def walk_recordings(dirs):
    out = []
    for d in dirs:
        for root, _dn, fn in os.walk(d):
            for f in fn:
                if f.endswith((".jsonl", ".jsonl.gz", ".levyrec")) and not is_later_part(f):   # a part file is read with its first part
                    out.append(os.path.join(root, f))
    return sorted(out)


def prescan(paths, cache_path):
    """summaries of many recordings, cached on disk (path + size + mtime) so a re-run is fast."""
    cache = {}
    if cache_path and os.path.isfile(cache_path):
        try:
            cache = json.load(open(cache_path))
        except ValueError:
            cache = {}
    res, dirty = {}, 0
    for i, p in enumerate(paths):
        st = os.stat(p)
        key = "%s|%d|%d" % (p, sum(os.path.getsize(x) for x in reccodec.parts_of(p)), int(st.st_mtime))
        if key in cache:
            res[p] = cache[key]
            continue
        rows = load(p)
        if any(r.get("k") == "f" for r in rows):
            res[p] = cache[key] = summarize(rows)
            res[p]["bytes_raw"] = st.st_size
        del rows
        dirty += 1
        if dirty % 40 == 0:
            print("  scanned %d / %d" % (i + 1, len(paths)), file=sys.stderr)
    if cache_path and dirty:
        json.dump(cache, open(cache_path, "w"), separators=(",", ":"))
    return res


def dkey(s):
    return (s["boss"], s["mode"], s.get("seed"), s.get("gear"), round(s["dur"]))


def pick_auto(sums, seen_keys, per_cell, max_n):
    """one pass over every candidate: drop duplicates and tiny/odd files, group by boss x mode x result, then take the cells
    round-robin (first pick of every cell, then the second, ...) so the spread is even and the cap cuts the deepest picks."""
    cells, dup = {}, set(seen_keys)
    for p, s in sorted(sums.items(), key=lambda kv: (kv[1]["bytes_raw"], kv[0])):
        if s["dur"] < 15 or s["boss"] in ("?", ""):
            continue
        k = dkey(s)
        if k in dup:
            continue
        dup.add(k)
        cells.setdefault((s["boss"], s["mode"], s["result"]), []).append((p, s))
    order = sorted(cells)
    chosen = []
    for rnd in range(per_cell):
        for c in order:
            lst = cells[c]
            # prefer a seed not yet taken in this cell
            taken = {x[1].get("seed") for x in chosen if (x[1]["boss"], x[1]["mode"], x[1]["result"]) == c}
            cand = [x for x in lst if x not in chosen]
            fresh = [x for x in cand if x[1].get("seed") not in taken]
            pickx = (fresh or cand or [None])[0]
            if pickx is not None:
                chosen.append(pickx)
            if len(chosen) >= max_n:
                return chosen
    return chosen


RES_TXT = {"win": "boss killed", "loss": "the fight was lost", "timeout": "time ran out", "fled": "the boss left with the commander alive", "despawn": "the boss left", "ended": "recording ended",
           "survived": "the event ran out", "interrupted": "the world closed"}


def event_note(s):
    """One line for an event recording: result, length, waves, kills, levy deaths."""
    word = {"win": "Won", "loss": "The army was wiped", "survived": "Survived", "ended": "Ended", "interrupted": "The world closed"}.get(s["result"], s["result"])
    bits = ["%s after %d min %02d s." % (word, int(s["dur"]) // 60, int(s["dur"]) % 60)]
    if s.get("waveMax"):
        bits.append("Reached wave %d." % s["waveMax"])
    bits.append("%d enemies killed, %d of them minibosses or bosses." % (s.get("kills", 0), s.get("miniKills", 0)))
    bits.append("%d levy deaths." % s.get("deaths", 0))
    return " ".join(bits)


def auto_note(s):
    if s.get("kind") == "event":
        return event_note(s)
    bits = ["%s, %d levies, %s%s." % (s["mode"], s["levies"], s["arena"].lower(), (", seed " + str(s["seed"])) if s.get("seed") else "")]
    bits.append("%s after %.0f s." % (RES_TXT.get(s["result"], s["result"]).capitalize(), s["dur"]))
    if s["levies"]:
        bits.append("%d of %d levies fell." % (min(s["deaths"], s["levies"]), s["levies"]))
    if s.get("hasT"):
        bits.append("Boss aimed at levies %.0f%% of the time." % s["pl"])
    return " ".join(bits)


def main():
    global MAX_C_ROWS
    ap = argparse.ArgumentParser(description="Build the hosted Levy fight-replay site.")
    ap.add_argument("outdir")
    ap.add_argument("sets", nargs="*", metavar="name=dir")
    ap.add_argument("--hz", type=int, default=15, help="sample rate of the data files (recordings are 30 Hz); default 15")
    ap.add_argument("--auto", action="append", default=[], metavar="dir", help="add a spread of fights from every recording under this folder (repeatable): one set per boss, up to --per-cell per boss x mode x result")
    ap.add_argument("--per-cell", type=int, default=2)
    ap.add_argument("--max-auto", type=int, default=110, help="cap on the number of auto-picked fights")
    ap.add_argument("--cmd-cap", type=int, default=MAX_C_ROWS, help="most commander projectiles kept per sample (format 11 key c); 0 drops the layer to save space")
    ap.add_argument("--cache", default=None, help="file that caches the summaries of the --auto candidates")
    ap.add_argument("--batches", type=float, default=0, metavar="MB", help="split the site into publishable batches of at most MB megabytes of data (and 250 files): <outdir>/b01, b02, ...; each holds index.html, the manifest of everything up to that batch and only its own data files, so publishing them in order to the same URL builds the full page")
    a = ap.parse_args()
    MAX_C_ROWS = max(0, a.cmd_cap)

    out = os.path.abspath(a.outdir)
    shutil.rmtree(out, ignore_errors=True)
    os.makedirs(out, exist_ok=True)

    manifest_sets, seen, all_fights, seen_keys = [], set(), [], set()
    blobs = {}   # fight id -> encoded bytes
    paths = {}   # fight id -> recording path (re-read if the budget forces a coarser rate)
    by_set = {}

    def add_fight(setname, setid, settitle, path, fights, note=None, auto=False, s=None):
        for suffix, rows in episode_list(path):
            add_episode(setname, setid, settitle, path, fights, note, auto, suffix, rows)

    def add_episode(setname, setid, settitle, path, fights, note, auto, suffix, rows):
        fn = os.path.basename(path)
        if not any(r.get("k") == "f" for r in rows):
            print("skip (no frames):", fn, file=sys.stderr)
            return
        s = summarize(rows)
        hdr = next((r for r in rows if r.get("k") == "hdr"), {})
        hz0 = hdr.get("hz", 30)
        event = hdr.get("kind") == "event"
        factor = max(1, -(-hz0 // (min(a.hz, EVENT_HZ) if event else a.hz)))
        while True:
            blob = dump(convert(rows, factor))
            if len(blob) <= MAX_FILE or factor >= (EVENT_MAX_FACTOR if event else 8):
                break
            factor = factor + 1 if not event else factor * 2
        del rows
        base = re.sub(r"\.(jsonl(\.gz)?|levyrec)$", "", fn) + suffix
        fid = slug(setname) + "__" + slug(base)
        n = 2
        while fid in seen:
            fid = "%s__%s-%d" % (slug(setname), slug(base), n)
            n += 1
        seen.add(fid)
        text = None
        if auto:
            text = auto_note(s)
        elif note:
            text = note.get("note")
            if note["names"][0] != fn:       # 'a + b' entries: the note and result describe the first file
                text = "Another run of the same cell as: " + (text or "")
            elif note["result"] and note["result"] != s["result"]:
                print("note: %s computed %s, index says %s" % (fn, s["result"], note["result"]), file=sys.stderr)
                text = (text or "") + " (Viewer verdict for this recording: %s.)" % s["result"]
        blobs[fid] = blob
        paths[fid] = (path, factor, hz0, suffix)
        s.update({"id": fid, "file": "data/%s.json" % fid, "bytes": len(blob), "hz": max(1, hz0 // factor), "note": text, "src": fn + suffix})
        fights.append(s)
        seen_keys.add(dkey(s))

    for spec in a.sets:
        if "=" not in spec:
            sys.exit("set must look like name=dir, got: " + spec)
        name, d = spec.split("=", 1)
        if not os.path.isdir(d):
            print("skip set %s: %s is not a directory" % (name, d), file=sys.stderr)
            continue
        idx = parse_index(os.path.join(d, "index.txt"))
        files = sorted(f for f in os.listdir(d) if f.endswith((".jsonl", ".jsonl.gz", ".levyrec")) and not is_later_part(f))
        ordered = [n for n in sorted(idx, key=lambda n: list(idx).index(n)) if n in files]
        ordered += [f for f in files if f not in ordered]
        fights = []
        for fn in ordered:
            add_fight(name, slug(name), TITLES.get(name.lower(), name.replace("_", " ")), os.path.join(d, fn), fights, note=idx.get(fn))
        if fights:
            manifest_sets.append({"id": slug(name), "title": TITLES.get(name.lower(), name.replace("_", " ")), "fights": fights})
            all_fights += fights
        print("%-10s %3d fights" % (name, len(fights)), file=sys.stderr)

    if a.auto:
        cands = walk_recordings(a.auto)
        print("auto: scanning %d recordings" % len(cands), file=sys.stderr)
        sums = prescan(cands, a.cache)
        chosen = pick_auto(sums, seen_keys, a.per_cell, a.max_auto)
        print("auto: picked %d fights" % len(chosen), file=sys.stderr)
        chosen.sort(key=lambda x: (x[1]["boss"], x[1]["mode"], x[1]["result"], x[1].get("seed") or ""))
        bysets = {}
        for p, s0 in chosen:
            bysets.setdefault(s0["boss"], []).append(p)
        for boss in sorted(bysets):
            fights = []
            for p in bysets[boss]:
                add_fight("more " + boss, "more-" + slug(boss), boss + " (more)", p, fights, auto=True)
            if fights:
                manifest_sets.append({"id": "more-" + slug(boss), "title": boss + " (more)", "fights": fights})
                all_fights += fights
            print("more %-24s %3d fights" % (boss, len(fights)), file=sys.stderr)

    # event recordings (format 12) get their own section at the top of the list, whatever set they came from
    evs = [f for st in manifest_sets for f in st["fights"] if f.get("kind") == "event"]
    if evs:
        for st in manifest_sets:
            st["fights"] = [f for f in st["fights"] if f.get("kind") != "event"]
        manifest_sets[:] = [{"id": "events", "title": "Events", "kind": "event", "intro": "Whole events: every levy, militia outpost and enemy, from the start of the event to its end.",
                             "fights": sorted(evs, key=lambda f: f.get("date") or "", reverse=True)}] + [st for st in manifest_sets if st["fights"]]
    fixed = os.path.getsize(os.path.join(HERE, "site", "index.html")) + 400 * len(all_fights)
    if not a.batches:
        # keep the site under the host's size limit: thin the biggest recordings one step at a time (15 Hz -> 10 -> 7.5 Hz)
        while sum(len(b) for b in blobs.values()) + fixed > MAX_TOTAL:
            cand = [f for f in all_fights if paths[f["id"]][1] < 4]
            if not cand:
                break
            f = max(cand, key=lambda f: f["bytes"])
            path, factor, hz0, suffix = paths[f["id"]]
            blob = dump(convert(dict(episode_list(path))[suffix], factor + 1))
            blobs[f["id"]] = blob
            paths[f["id"]] = (path, factor + 1, hz0, suffix)
            f["bytes"] = len(blob)
            f["hz"] = max(1, hz0 // (factor + 1))
            print("thinned %s to %d Hz (site was over budget)" % (f["src"], f["hz"]), file=sys.stderr)

    # batches: pack the fights in page order; batch k carries the manifest of everything up to k, so each publish is a complete, working page
    if a.batches:
        limit = int(a.batches * 1048576)
        batch_of, cur_b, cur_bytes, cur_n = {}, 1, 0, 0
        for f in all_fights:
            if cur_n and (cur_bytes + f["bytes"] > limit or cur_n >= 250):
                cur_b += 1; cur_bytes = 0; cur_n = 0
            batch_of[f["id"]] = cur_b; cur_bytes += f["bytes"]; cur_n += 1
        nb = cur_b
    else:
        batch_of, nb = {f["id"]: 0 for f in all_fights}, 0

    first = [f for f in all_fights if batch_of[f["id"]] <= 1]
    wins = [f for f in first if f["result"] == "win" and f["levies"] and f["hasT"]]
    wins.sort(key=lambda f: (-(f["deaths"] / f["levies"] if f["deaths"] < f["levies"] else 0), f["dur"]))
    default = wins[0]["id"] if wins else (first[0]["id"] if first else None)

    def write_site(root, upto):
        data = os.path.join(root, "data")
        os.makedirs(data, exist_ok=True)
        total, count = 0, 0
        for fid, blob in blobs.items():
            if (batch_of[fid] == upto) if nb else True:
                with open(os.path.join(data, fid + ".json"), "wb") as fh:
                    fh.write(blob)
                total += len(blob); count += 1
        sets = []
        for st in manifest_sets:
            fl = [f for f in st["fights"] if (not nb) or batch_of[f["id"]] <= upto]
            if fl:
                sets.append({"id": st["id"], "title": st["title"], "fights": fl})
        with open(os.path.join(data, "manifest.json"), "w", encoding="utf-8") as f:
            json.dump({"version": 2, "default": default, "current": CURRENT, "eras": ERAS, "sets": sets}, f, separators=(",", ":"))
        shutil.copyfile(os.path.join(HERE, "site", "index.html"), os.path.join(root, "index.html"))
        size = total + os.path.getsize(os.path.join(data, "manifest.json")) + os.path.getsize(os.path.join(root, "index.html"))
        return count + 2, size, sum(len(s["fights"]) for s in sets)

    if not nb:
        n_files, size, n_f = write_site(out, 0)
        print("wrote %s: %d fights, %d files, %.2f MB (largest data file %.2f MB)" % (out, n_f, n_files, size / 1048576, max([f["bytes"] for f in all_fights] or [0]) / 1048576), file=sys.stderr)
        if size > MAX_TOTAL:
            print("WARNING: site is over %d MB even after thinning; the host limit is 60 MB. Use --batches 55." % (MAX_TOTAL // 1048576), file=sys.stderr)
    else:
        lines = []
        for b in range(1, nb + 1):
            root = os.path.join(out, "b%02d" % b)
            n_files, size, n_f = write_site(root, b)
            lines.append("b%02d: %d files, %.1f MB, manifest lists %d fights (%d new)" % (b, n_files, size / 1048576, n_f, sum(1 for f in all_fights if batch_of[f["id"]] == b)))
        open(os.path.join(out, "batches.txt"), "w").write("\n".join(lines) + "\nPublish in order to the same URL: each batch adds its data files and replaces index.html and data/manifest.json.\n")
        print("\n".join(lines), file=sys.stderr)


if __name__ == "__main__":
    main()

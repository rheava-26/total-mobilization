#!/usr/bin/env python3
"""Build the hosted Levy fight-replay site.

usage: make_site.py <outdir> <set-name>=<dir> [<set-name>=<dir> ...] [--hz 15] [--keep]

  make_site.py /tmp/replay-site lab1=replays/notable lab2=replays/notable2 worm=replays/worm5 options=replays/options lab3=replays/lab3

For every set directory it reads the recordings (*.jsonl or *.jsonl.gz, FightRecorder format 1-4), the optional index.txt
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
TITLES = {"eve": "Eve's fights", "rec2": "Recorder test", "lab1": "Lab 1", "lab2": "Lab 2", "lab3": "Lab 3", "worm": "Worm round", "worm5": "Worm round", "options": "Options", "vert1": "Movement lab", "moblab": "Mobility gear lab"}
MODES = {"exp": "Expert", "rev": "Rev", "death": "Death", "revoff": "Rev, rage off", "mdeath": "Master+Death"}
ARENAS = {"surf": "Surface", "ug": "Underground"}
FLED_PX = 50000          # a boss that ends this far from where it began has left the fight
MAX_FILE = 14 * 1048576  # hard limit is 15 MB per data file
MAX_TOTAL = 55 * 1048576  # hard limit is 60 MB for the site


# ---------------------------------------------------------------- reading
def load(path):
    op = gzip.open if path.endswith(".gz") else open
    rows = []
    with op(path, "rt", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                try:
                    rows.append(json.loads(line))
                except ValueError:
                    pass
    return rows


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
    bs = f.get("b") or []
    for b in bs:
        if not b[13]:
            return b
    return bs[0] if bs else None


def summarize(rows):
    hdr = next((r for r in rows if r.get("k") == "hdr"), {})
    meta = hdr.get("meta") or {}
    frames = [r for r in rows if r.get("k") == "f"]
    ev = [r for r in rows if r.get("k") == "e"]
    tally = [0, 0, 0]
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
    # recordings made before the worm-kill fix logged a worm that died to a body-segment blow as bdespawn: a leave with <=1% life and a hit within ~1 s is a kill
    if fin is not None and fin["e"] == "bdespawn" and fin.get("max", 0) > 0 and fin.get("life", 1) <= 0.01 * fin["max"] and fin.get("ht") is not None and fin["t"] - fin["ht"] <= 60:
        fin = dict(fin, e="bdeath", inferred=True)
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
    mode = MODES.get(meta.get("mode")) or (hdr.get("mode") or "?").replace("Expert+Revengeance", "Rev")
    arena = ARENAS.get(meta.get("arena"), meta.get("arena") or "?")
    # a hand-made recording (/levyrec start <name>) carries its file name in hdr.boss: name the fight after the boss it actually met, or "Other fights" when none
    named = [e.get("name") for e in ev if e.get("e") == "boss" and e.get("name")]
    boss = meta.get("bossName") or (named[0] if named and not meta else None) or ("Other fights" if not meta else (hdr.get("boss") or "?").replace("_", " "))
    return {
        "boss": boss, "mode": mode, "arena": arena, "levies": max(n_levies, len(ids)), "result": result,
        "pl": round(100.0 * tally[2] / n, 1), "pp": round(100.0 * tally[1] / n, 1),
        "hits": len(lhit), "dmg": sum(e.get("dmg", 0) for e in lhit), "deaths": deaths,
        "dur": round(dur, 1), "hasT": any(len(b) > 14 for f in frames for b in (f.get("b") or [])[:1]),
        "bossEnd": ({"kind": "killed" if fin["e"] == "bdeath" else "despawn", "t": round(fin["t"] / 60.0, 1)} if fin else None),
        "format": hdr.get("format", 1), "seed": meta.get("seed"), "gear": meta.get("gear"),
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
        o["j"] = [row(j, ints=(1, 2)) for j in f["j"]]
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
        "hdr": {k: hdr[k] for k in ("format", "meta", "mode", "boss", "stateNames", "levy", "date") if k in hdr},
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
    # terrain: format 4 writes several arena records (start area, growth strips, camera view); the page merges them. Older files have one.
    arenas = [{kk: vv for kk, vv in r.items() if kk != "k"} for r in rows if r.get("k") == "arena"]
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
                if f.endswith(".jsonl") or f.endswith(".jsonl.gz"):
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
        key = "%s|%d|%d" % (p, st.st_size, int(st.st_mtime))
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


RES_TXT = {"win": "boss killed", "loss": "every levy fell", "timeout": "time ran out", "fled": "the boss flew off", "despawn": "the boss left", "ended": "recording ended"}


def auto_note(s):
    bits = ["%s, %d levies, %s%s." % (s["mode"], s["levies"], s["arena"].lower(), (", seed " + str(s["seed"])) if s.get("seed") else "")]
    bits.append("%s after %.0f s." % (RES_TXT.get(s["result"], s["result"]).capitalize(), s["dur"]))
    if s["levies"]:
        bits.append("%d of %d levies fell." % (min(s["deaths"], s["levies"]), s["levies"]))
    if s.get("hasT"):
        bits.append("Boss aimed at levies %.0f%% of the time." % s["pl"])
    return " ".join(bits)


def main():
    ap = argparse.ArgumentParser(description="Build the hosted Levy fight-replay site.")
    ap.add_argument("outdir")
    ap.add_argument("sets", nargs="*", metavar="name=dir")
    ap.add_argument("--hz", type=int, default=15, help="sample rate of the data files (recordings are 30 Hz); default 15")
    ap.add_argument("--auto", action="append", default=[], metavar="dir", help="add a spread of fights from every recording under this folder (repeatable): one set per boss, up to --per-cell per boss x mode x result")
    ap.add_argument("--per-cell", type=int, default=2)
    ap.add_argument("--max-auto", type=int, default=110, help="cap on the number of auto-picked fights")
    ap.add_argument("--cache", default=None, help="file that caches the summaries of the --auto candidates")
    ap.add_argument("--batches", type=float, default=0, metavar="MB", help="split the site into publishable batches of at most MB megabytes of data (and 250 files): <outdir>/b01, b02, ...; each holds index.html, the manifest of everything up to that batch and only its own data files, so publishing them in order to the same URL builds the full page")
    a = ap.parse_args()

    out = os.path.abspath(a.outdir)
    shutil.rmtree(out, ignore_errors=True)
    os.makedirs(out, exist_ok=True)

    manifest_sets, seen, all_fights, seen_keys = [], set(), [], set()
    blobs = {}   # fight id -> encoded bytes
    paths = {}   # fight id -> recording path (re-read if the budget forces a coarser rate)
    by_set = {}

    def add_fight(setname, setid, settitle, path, fights, note=None, auto=False, s=None):
        fn = os.path.basename(path)
        rows = load(path)
        if not any(r.get("k") == "f" for r in rows):
            print("skip (no frames):", fn, file=sys.stderr)
            return
        s = summarize(rows)
        hdr = next((r for r in rows if r.get("k") == "hdr"), {})
        hz0 = hdr.get("hz", 30)
        factor = max(1, -(-hz0 // a.hz))
        while True:
            blob = dump(convert(rows, factor))
            if len(blob) <= MAX_FILE or factor >= 8:
                break
            factor += 1
        del rows
        base = re.sub(r"\.jsonl(\.gz)?$", "", fn)
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
        paths[fid] = (path, factor, hz0)
        s.update({"id": fid, "file": "data/%s.json" % fid, "bytes": len(blob), "hz": max(1, hz0 // factor), "note": text, "src": fn})
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
        files = sorted(f for f in os.listdir(d) if f.endswith(".jsonl") or f.endswith(".jsonl.gz"))
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

    fixed = os.path.getsize(os.path.join(HERE, "site", "index.html")) + 400 * len(all_fights)
    if not a.batches:
        # keep the site under the host's size limit: thin the biggest recordings one step at a time (15 Hz -> 10 -> 7.5 Hz)
        while sum(len(b) for b in blobs.values()) + fixed > MAX_TOTAL:
            cand = [f for f in all_fights if paths[f["id"]][1] < 4]
            if not cand:
                break
            f = max(cand, key=lambda f: f["bytes"])
            path, factor, hz0 = paths[f["id"]]
            blob = dump(convert(load(path), factor + 1))
            blobs[f["id"]] = blob
            paths[f["id"]] = (path, factor + 1, hz0)
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
            json.dump({"version": 1, "default": default, "sets": sets}, f, separators=(",", ":"))
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

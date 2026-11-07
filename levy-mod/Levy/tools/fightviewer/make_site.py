#!/usr/bin/env python3
"""Build the hosted Levy fight-replay site.

usage: make_site.py <outdir> <set-name>=<dir> [<set-name>=<dir> ...] [--hz 15] [--keep]

  make_site.py /tmp/replay-site lab1=replays/notable lab2=replays/notable2 worm=replays/worm5 options=replays/options lab3=replays/lab3

For every set directory it reads the recordings (*.jsonl or *.jsonl.gz, FightRecorder format 1-4), the optional index.txt
("why it's notable" notes) and each recording's header meta, and writes

  <outdir>/index.html            copy of site/index.html (the page, no data inside)
  <outdir>/data/manifest.json    the fight list: one row per fight with boss, mode, levies, arena, result, targeting %, hits, deaths, note
  <outdir>/data/<id>.json        one plain-JSON file per fight (frames downsampled to --hz, coordinates rounded, levy and boss rows delta-coded), loaded lazily by the page

Adding a set is just adding another <set-name>=<dir> argument and re-running. Sets show in the order given.
Set names lab1 lab2 lab3 worm options get nice titles; any other name is shown with underscores as spaces.
The whole <outdir> is what gets published (static files, relative URLs only). Old data/*.json is removed first so renamed fights leave no strays.
"""
import argparse, glob, gzip, json, os, re, shutil, sys

HERE = os.path.dirname(os.path.abspath(__file__))
TITLES = {"rec2": "Recorder test", "lab1": "Lab 1", "lab2": "Lab 2", "lab3": "Lab 3", "worm": "Worm round", "worm5": "Worm round", "options": "Options"}
MODES = {"exp": "Expert", "rev": "Rev", "death": "Death", "revoff": "Rev, rage off"}
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
    boss = meta.get("bossName") or (hdr.get("boss") or "?").replace("_", " ")
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


def shrink_frame(f):
    o = {"t": f["t"], "p": row(f["p"], ints=(0, 1, 13, 14))}
    if f.get("l"):
        o["l"] = [row(l, ints=(1, 2)) for l in f["l"]]
    if f.get("b"):
        o["b"] = [row(b, ints=(2, 3), ones=(8, 9, 10, 11)) for b in f["b"]]
    if f.get("a"):
        o["a"] = [row(a, ints=(2, 3)) for a in f["a"]]
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
    keep = frames[::factor]
    if frames and keep[-1] is not frames[-1]:
        keep.append(frames[-1])
    hz = hdr.get("hz", 30)
    out = {
        "v": 3,
        "hdr": {k: hdr[k] for k in ("format", "meta", "mode", "boss", "stateNames", "levy", "date") if k in hdr},
        "frames": [],
        "events": [shrink_event(e) for e in rows if e.get("k") == "e"],
    }
    lastL, lastB, lastA = {}, {}, {}
    for f in keep:
        o = shrink_frame(f)
        if "l" in o:
            o["l"] = enc_rows(o["l"], lastL, 0, (1, 2))
        if "b" in o:
            o["b"] = enc_rows(o["b"], lastB, 1, (2, 3))
        if "a" in o:
            o["a"] = enc_rows(o["a"], lastA, 1, (2, 3))
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


def main():
    ap = argparse.ArgumentParser(description="Build the hosted Levy fight-replay site.")
    ap.add_argument("outdir")
    ap.add_argument("sets", nargs="+", metavar="name=dir")
    ap.add_argument("--hz", type=int, default=15, help="sample rate of the data files (recordings are 30 Hz); default 15")
    a = ap.parse_args()

    out = os.path.abspath(a.outdir)
    data = os.path.join(out, "data")
    os.makedirs(data, exist_ok=True)
    for old in glob.glob(os.path.join(data, "*.json")):
        os.remove(old)

    manifest_sets, seen, all_fights = [], set(), []
    blobs = {}   # fight id -> encoded bytes
    paths = {}   # fight id -> recording path (re-read if the budget forces a coarser rate)
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
            rows = load(os.path.join(d, fn))
            if not any(r.get("k") == "f" for r in rows):
                print("skip (no frames):", fn, file=sys.stderr)
                continue
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
            fid = slug(name) + "__" + slug(base)
            n = 2
            while fid in seen:
                fid = "%s__%s-%d" % (slug(name), slug(base), n)
                n += 1
            seen.add(fid)
            note = idx.get(fn)
            text = (note or {}).get("note")
            if note and note["names"][0] != fn:       # 'a + b' entries: the note and result describe the first file
                text = "Another run of the same cell as: " + (text or "")
            elif note and note["result"] and note["result"] != s["result"]:
                print("note: %s computed %s, index says %s" % (fn, s["result"], note["result"]), file=sys.stderr)
                text = (text or "") + " (Viewer verdict for this recording: %s.)" % s["result"]
            blobs[fid] = blob
            paths[fid] = (os.path.join(d, fn), factor, hz0)
            s.update({"id": fid, "file": "data/%s.json" % fid, "bytes": len(blob), "hz": max(1, hz0 // factor),
                      "note": text, "src": fn})
            fights.append(s)
        if fights:
            manifest_sets.append({"id": slug(name), "title": TITLES.get(name.lower(), name.replace("_", " ")), "fights": fights})
            all_fights += fights
        print("%-10s %3d fights" % (name, len(fights)), file=sys.stderr)

    # keep the site under the host's size limit: thin the biggest recordings one step at a time (15 Hz -> 10 -> 7.5 Hz)
    fixed = os.path.getsize(os.path.join(HERE, "site", "index.html")) + 400 * len(all_fights)
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
    total = 0
    for fid, blob in blobs.items():
        with open(os.path.join(data, fid + ".json"), "wb") as fh:
            fh.write(blob)
        total += len(blob)
    nfiles = len(blobs)

    # default: the closest win (most levies dead without losing them all), shortest on ties
    wins = [f for f in all_fights if f["result"] == "win" and f["levies"] and f["hasT"]]
    wins.sort(key=lambda f: (-(f["deaths"] / f["levies"] if f["deaths"] < f["levies"] else 0), f["dur"]))
    default = wins[0]["id"] if wins else (all_fights[0]["id"] if all_fights else None)
    with open(os.path.join(data, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump({"version": 1, "default": default, "sets": manifest_sets}, f, separators=(",", ":"))
    shutil.copyfile(os.path.join(HERE, "site", "index.html"), os.path.join(out, "index.html"))
    size = total + os.path.getsize(os.path.join(data, "manifest.json")) + os.path.getsize(os.path.join(out, "index.html"))
    print("wrote %s: %d fights, %d files, %.2f MB (largest data file %.2f MB)" % (
        out, nfiles, nfiles + 2, size / 1048576, max([f["bytes"] for f in all_fights] or [0]) / 1048576), file=sys.stderr)
    if size > MAX_TOTAL:
        print("WARNING: site is over %d MB even after thinning; the host limit is 60 MB. Publish fewer fights." % (MAX_TOTAL // 1048576), file=sys.stderr)


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Build one self-contained HTML page with several fight recordings embedded.

usage: make_gallery.py <out.html> <rec files...> [--hz 30|15] [--budget-mb 12] [--title "..."]

Each .jsonl recording is gzip-compressed and embedded as base64 next to a small precomputed summary (for the fight list).
The page is viewer.html with the data spliced in; it needs no network (a pinned cdnjs pako is only fetched by browsers that lack DecompressionStream).
If the page would exceed the budget the recordings are downsampled to 15 Hz (every other sample; events are kept), then projectile rows are thinned.
"""
import argparse, base64, gzip, json, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))


def load(path):
    rows = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                try:
                    rows.append(json.loads(line))
                except ValueError:
                    pass
    return rows


def tk_of(b):
    if len(b) > 14:
        return b[14]
    return 1 if 0 <= b[12] < 255 else 0


def summarize(rows):
    hdr = next((r for r in rows if r.get("k") == "hdr"), {})
    meta = hdr.get("meta") or {}
    frames = [r for r in rows if r.get("k") == "f"]
    ev = [r for r in rows if r.get("k") == "e"]
    tally = {0: 0, 1: 0, 2: 0}
    has_t = False
    for f in frames:
        bs = f.get("b") or []
        main = next((b for b in bs if not b[13]), bs[0] if bs else None)
        if main is None:
            continue
        if len(main) > 14:
            has_t = True
        tally[tk_of(main)] += 1
    n = max(1, sum(tally.values()))
    t0 = frames[0]["t"] if frames else 0
    t1 = frames[-1]["t"] if frames else 0
    lhit = [e for e in ev if e.get("e") == "lhit"]
    boss = meta.get("bossName") or (hdr.get("boss") or "?").replace("_", " ")
    return {
        "boss": boss, "dur": (t1 - t0) / 60.0, "pl": 100.0 * tally[2] / n, "pp": 100.0 * tally[1] / n, "pn": 100.0 * tally[0] / n,
        "hits": len(lhit), "dmg": sum(e.get("dmg", 0) for e in lhit), "deaths": sum(1 for e in ev if e.get("e") == "ldeath"),
        "meta": meta, "v": hdr.get("format", 1), "hasT": has_t, "frames": len(frames),
    }


def expand_s(r, defs):
    """Format 7 hitbox rows: a row of 3 ([id,x,y]) leans on the last full row of that id, which may sit in a frame the downsampler drops; write full rows."""
    s = r.get("s")
    if not s:
        return r
    o = []
    for x in s:
        if len(x) >= 8:
            defs[x[1]] = x
            o.append(x)
        elif x[0] in defs:
            d = defs[x[0]]
            o.append([d[0], x[0], x[1], x[2], d[4], d[5], d[6], d[7]])
    r = dict(r)
    r["s"] = o
    return r


def downsample(rows, factor=2, thin_proj=False):
    out, i, sdefs = [], 0, {}
    for r in rows:
        if r.get("k") == "f":
            r = expand_s(r, sdefs)
            i += 1
            if (i - 1) % factor:
                continue
            if thin_proj and r.get("j") and len(r["j"]) > 40:
                r = dict(r)
                r["j"] = r["j"][::2]
        elif r.get("k") == "hdr":
            r = dict(r)
            r["hz"] = (r.get("hz", 30) // factor) if factor > 1 else r.get("hz", 30)
            r["downsampled"] = factor
        out.append(r)
    return out


def dumps(rows):
    return "\n".join(json.dumps(r, separators=(",", ":")) for r in rows) + "\n"


def build(files, hz, budget, title):
    items = []
    for p in files:
        rows = load(p)
        if not any(r.get("k") == "f" for r in rows):
            print("skip (no frames):", p, file=sys.stderr)
            continue
        items.append((p, rows, summarize(rows)))
    tpl = open(os.path.join(HERE, "viewer.html"), encoding="utf-8").read()
    marker = "/*__GALLERY__*/null"
    if marker not in tpl:
        sys.exit("viewer.html has no gallery marker")
    factor = 2 if hz == 15 else 1
    thin = False
    while True:
        gal = []
        for p, rows, s in items:
            use = downsample(rows, factor, thin) if (factor > 1 or thin) else rows
            raw = dumps(use).encode()
            z = gzip.compress(raw, 9, mtime=0)
            name = os.path.splitext(os.path.basename(p))[0]
            gal.append({"title": s["boss"], "file": name, "sum": s, "z": base64.b64encode(z).decode("ascii"), "rawBytes": len(raw)})
        blob = json.dumps(gal, separators=(",", ":"))
        html = tpl.replace(marker, blob.replace("</", "<\\/"), 1)
        if title:
            html = html.replace("<title>Levy Fight Replay</title>", "<title>%s</title>" % title, 1)
        size = len(html.encode("utf-8"))
        print("%d fights, %s Hz%s, %.2f MB" % (len(gal), 60 // 2 // factor if factor else 30, ", thinned projectiles" if thin else "", size / 1048576), file=sys.stderr)
        if size <= budget:
            return html, gal
        if factor == 1:
            factor = 2
        elif not thin:
            thin = True
        else:
            sys.exit("cannot fit the budget even downsampled; embed fewer recordings")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out")
    ap.add_argument("recs", nargs="+")
    ap.add_argument("--hz", type=int, default=30, choices=(15, 30))
    ap.add_argument("--budget-mb", type=float, default=12.0)
    ap.add_argument("--title", default="")
    a = ap.parse_args()
    html, gal = build(a.recs, a.hz, int(a.budget_mb * 1048576), a.title)
    with open(a.out, "w", encoding="utf-8") as f:
        f.write(html)
    print("wrote %s (%.2f MB, %d recordings)" % (a.out, os.path.getsize(a.out) / 1048576, len(gal)), file=sys.stderr)


if __name__ == "__main__":
    main()

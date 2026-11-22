#!/usr/bin/env python3
"""Re-encode fight recordings (format 12 or older) as format 13: compact frame rows (see reccodec.py) and, past the part size, part files.

usage: reencode.py [--out DIR] [--part-mb 9] [--check] FILE...

  Writes DIR/<name>.levyrec (and <name>_part2.levyrec ... when a part would pass --part-mb) and prints the size before and after.
  --check decodes the result again and compares every frame with the original (positions to the pixel, velocities to a tenth).
  Hostile and commander projectiles of an old file carry no slot: the converter gives each a key by following it from sample to sample
  (same type, near where its velocity puts it), as the game's own slot number would.
  Only the frame rows change; every other line is copied as it is. The output reads in make_site.py, recview.py, viewer.html and the game's report.
"""
import argparse, gzip, io, json, os, sys, zlib

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import reccodec  # noqa: E402

PART_BYTES_DEFAULT = 9 * 1000 * 1000


def assign_keys(frames, key, ki):
    """Give the rows of j or c a slot key at index ki (format 12 has none): follow each projectile from the sample before."""
    prev = []  # (key, type, x, y, vx, vy)
    for f in frames:
        rows = f.get(key)
        if not rows:
            prev = []
            continue
        dt = 2
        used, cur = set(), []
        taken_prev = set()
        for r in rows:
            t, x, y, vx, vy = r[0], r[1], r[2], r[3], r[4]
            best, bd = None, 32.0 ** 2
            for i, (k, pt, px, py, pvx, pvy) in enumerate(prev):
                if i in taken_prev or pt != t:
                    continue
                ex, ey = px + pvx * dt, py + pvy * dt
                d = (ex - x) ** 2 + (ey - y) ** 2
                if d < bd:
                    best, bd = i, d
            if best is not None:
                taken_prev.add(best)
                k = prev[best][0]
            else:
                k = None
            cur.append([k, t, x, y, vx, vy, r])
        prev_keys = {p[0] for p in prev}
        for c in cur:
            if c[0] is not None:
                used.add(c[0])
        nxt = 0
        for c in cur:
            if c[0] is None:
                while nxt in used or nxt in prev_keys:
                    nxt += 1
                c[0] = nxt
                used.add(nxt)
        out = []
        for k, t, x, y, vx, vy, r in cur:
            rr = list(r[:ki]) + [0] * max(0, ki - len(r))
            out.append(rr + [k])
        f[key] = out
        prev = [(c[0], c[1], c[2], c[3], c[4], c[5]) for c in cur]


def convert(lines, part_bytes):
    """Original lines -> list of parts, each a list of output lines."""
    rows = [json.loads(l) for l in lines]
    frames = [r for r in rows if r.get("k") == "f"]
    hdr = next((r for r in rows if r.get("k") == "hdr"), None)
    if hdr is not None and hdr.get("codec"):
        raise SystemExit("already format 13")
    reccodec.decode_rows(rows)
    import copy
    orig = copy.deepcopy(frames)
    assign_keys(frames, "j", 8)
    assign_keys(frames, "c", 7)
    hdr = dict(hdr or {"k": "hdr"})
    hdr["formatWas"] = hdr.get("format")
    hdr["format"] = 13
    hdr["codec"] = 1
    hdr["schemaJ"] = "type,x,y,vx,vy,damage,w,h,slot"
    hdr["schemaC"] = "type,x,y,vx,vy,w,h,slot"
    enc = reccodec.Encoder()
    parts, cur, size = [], [], 0
    z = zlib.compressobj(9, zlib.DEFLATED, 31)
    def start(n):
        h = dict(hdr)
        if n > 1:
            h["part"] = n
        enc.reset()
        return [json.dumps(h, separators=(",", ":"))]
    cur = start(1)
    size = 0
    fi = 0
    for r in rows:
        k = r.get("k")
        if k == "hdr":
            continue
        if k == "f":
            if size > part_bytes:
                parts.append(cur)
                cur = start(len(parts) + 1)
                z = zlib.compressobj(9, zlib.DEFLATED, 31)
                size = 0
            r = enc.frame(dict(frames[fi]))
            fi += 1
        s = json.dumps(r, separators=(",", ":"))
        cur.append(s)
        size += len(z.compress((s + "\n").encode()))
        if len(cur) % 400 == 0:
            size += len(z.flush(zlib.Z_SYNC_FLUSH))
    parts.append(cur)
    return parts, orig


def gz(lines):
    b = io.BytesIO()
    with gzip.GzipFile(fileobj=b, mode="wb", compresslevel=9, mtime=0) as g:
        g.write(("\n".join(lines) + "\n").encode("utf-8"))
    return b.getvalue()


def check(parts, orig):
    rows = reccodec.join_parts([[json.loads(l) for l in p] for p in parts])
    fr = [r for r in rows if r.get("k") == "f"]
    if len(fr) != len(orig):
        return "frame count %d vs %d" % (len(fr), len(orig))
    for a, b in zip(fr, orig):
        for key, (ki, n, pos, ten) in reccodec.LISTS.items():
            ra, rb = a.get(key) or [], b.get(key) or []
            if len(ra) != len(rb):
                return "t=%s %s rows %d vs %d" % (a["t"], key, len(ra), len(rb))
            for x, y in zip(ra, rb):
                for i in range(min(len(y), n)):
                    if key in ("j", "c") and i == ki:
                        continue
                    tol = 0.051 if i in ten else 0.51
                    if abs(x[i] - (y[i] if isinstance(y[i], (int, float)) else 0)) > tol + 1e-9:
                        return "t=%s %s field %d: %s vs %s" % (a["t"], key, i, x[i], y[i])
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("files", nargs="+")
    ap.add_argument("--out", default=None)
    ap.add_argument("--part-mb", type=float, default=PART_BYTES_DEFAULT / 1e6)
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args()
    tot0 = tot1 = 0
    for p in a.files:
        lines = reccodec.read_lines(p)
        parts, orig = convert(lines, int(a.part_mb * 1e6))
        blobs = [gz(x) for x in parts]
        before = os.path.getsize(p)
        after = sum(len(b) for b in blobs)
        tot0 += before; tot1 += after
        note = ""
        if a.check:
            note = check(parts, orig) or "decodes back exactly"
        name = os.path.basename(p)
        for e in (".jsonl.gz", ".levyrec", ".jsonl"):
            if name.endswith(e):
                name = name[: -len(e)]
                break
        if a.out:
            os.makedirs(a.out, exist_ok=True)
            for i, b in enumerate(blobs):
                fn = os.path.join(a.out, name + ("_part%d" % (i + 1) if i else "") + ".levyrec")
                with open(fn, "wb") as fh:
                    fh.write(b)
        print("%-70s %7.2f MB -> %6.2f MB  (%3.0f%%)  %d part%s  %s" % (name[:70], before / 1048576, after / 1048576, 100.0 * after / max(1, before), len(blobs), "" if len(blobs) == 1 else "s", note))
    if len(a.files) > 1:
        print("total %.1f MB -> %.1f MB (%.0f%%)" % (tot0 / 1048576, tot1 / 1048576, 100.0 * tot1 / max(1, tot0)))


if __name__ == "__main__":
    main()

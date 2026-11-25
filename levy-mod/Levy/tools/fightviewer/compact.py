#!/usr/bin/env python3
"""Compact replay data (page format v5): still plain JSON, about a third smaller than v4. Lossless; the page (site/index.html expandCompact) turns it back into v4 before use.

  python3 compact.py <site dir>      rewrites every data/*.json that is still v4 in place and updates the manifest's "bytes"

What changes (everything else is left as it was):
  events    the list of objects with repeated keys becomes one table per kind: {"k":kind,"c":[[column,mode,table]...],"t":[time deltas],"r":[[values...]]}
            column mode 0 plain, 1 integer delta from the column's previous value, 2 index into a table of strings; a missing key is null (trailing nulls cut)
  j, c, m   (projectile rows of a frame) a row that continues one of the previous frame's rows is [ref, dx, dy] or [ref, dx, dy, mask, values...]: ref = index of that row in
            the previous frame, dx dy = position minus (the ref's position + its velocity x the frame step), mask = the other fields that differ (value for each set bit).
            A row with no match is [[row]]. A frame without the list forgets the previous one.
"""
import glob, json, math, os, sys

LISTS = ("j", "c", "m")
SLOT = {"j": 8, "c": 8, "m": 7}


def _rnd(x):
    return math.floor(x + 0.5)


def code_list(key, frames):
    prev, pt = None, None
    for f in frames:
        rows = f.get(key)
        if not rows:
            prev = None
            if key in f and not rows:
                pass
            continue
        dt = (f["t"] - pt) if pt is not None else 0
        out = []
        bytype = {}
        slotmap = {}
        if prev is not None:
            si = SLOT[key]
            for i, r in enumerate(prev):
                bytype.setdefault(r[0], []).append(i)
                if len(r) > si and r[si] >= 0:
                    slotmap[(r[0], r[si])] = i
        for r in rows:
            ref = None
            si = SLOT[key]
            if prev is not None:
                if len(r) > si and r[si] >= 0:
                    ref = slotmap.get((r[0], r[si]))
                else:
                    best = None
                    for i in bytype.get(r[0], ()):
                        p = prev[i]
                        if len(p) != len(r):
                            continue
                        dx = r[1] - _rnd(p[1] + p[3] * dt); dy = r[2] - _rnd(p[2] + p[4] * dt)
                        d = abs(dx) + abs(dy)
                        if d > 300:
                            continue
                        sc = d + 1000 * sum(1 for k in range(len(r)) if k not in (1, 2) and r[k] != p[k])
                        if best is None or sc < best[0]:
                            best = (sc, i)
                    if best:
                        ref = best[1]
            if ref is None or len(prev[ref]) != len(r):
                out.append([r])
                continue
            p = prev[ref]
            dx = r[1] - _rnd(p[1] + p[3] * dt); dy = r[2] - _rnd(p[2] + p[4] * dt)
            mask, vals = 0, []
            for k in range(len(r)):
                if k in (1, 2):
                    continue
                if r[k] != p[k]:
                    mask |= 1 << k
                    vals.append(r[k])
            out.append([ref, dx, dy] if not mask else [ref, dx, dy, mask] + vals)
        prev, pt = rows, f["t"]
        f[key] = out
    return frames


def expand_list(key, frames):
    prev, pt = None, None
    for f in frames:
        rows = f.get(key)
        if not rows:
            prev = None
            continue
        dt = (f["t"] - pt) if pt is not None else 0
        out = []
        for e in rows:
            if isinstance(e[0], list):
                out.append(e[0]); continue
            p = prev[e[0]]
            r = list(p)
            r[1] = e[1] + _rnd(p[1] + p[3] * dt); r[2] = e[2] + _rnd(p[2] + p[4] * dt)
            if len(e) > 3:
                m, vi, k = e[3], 4, 0
                while m:
                    if m & 1:
                        r[k] = e[vi]; vi += 1
                    m >>= 1; k += 1
            out.append(r)
        f[key] = out
        prev, pt = out, f["t"]
    return frames


# ---- l, b, a, s: keyed rows (levy id / NPC slot). v4 stores them as differences from the key's last row; v5 stores positions as the miss against where the last row's velocity said it would be.
KEYED = {"l": (0, 1, 2, 3, 4), "b": (1, 2, 3, 4, 5), "a": (1, 2, 3, 4, 5), "s": (1, 2, 3, None, None)}   # key field, x, y, vx, vy
V4POS = {"l": (1, 2), "b": (2, 3), "a": (2, 3), "s": (2, 3)}


def dec_v4(rows, last, idx, ipos):
    """Inverse of make_site.enc_rows (the page's decRows)."""
    out = []
    for r in rows:
        if isinstance(r, int):
            prev = last.get(r)
            if prev is None:
                continue
            row = list(prev)
        elif not isinstance(r, list):
            row = r["r"]
        elif r[0] < 0:
            prev = last.get(-r[0] - 1)
            if prev is None:
                continue
            row = list(prev); m, k, i = r[1], 2, 0
            while m:
                if m & 1:
                    row[i] = prev[i] + r[k] if i in ipos else r[k]; k += 1
                m >>= 1; i += 1
        else:
            row = r
        last[row[idx]] = row; out.append(row)
    return out


def code_keyed(key, frames):
    ki, xi, yi, vxi, vyi = KEYED[key]
    last, lt = {}, {}
    dlast = {}
    for f in frames:
        rows = f.get(key)
        if rows is None:
            continue
        full = dec_v4(rows, dlast, ki, V4POS[key])
        out = []
        for r in full:
            k = r[ki]; p = last.get(k)
            if p is None or len(p) != len(r):
                out.append([r]); last[k] = r; lt[k] = f["t"]; continue
            dt = f["t"] - lt[k]
            if vxi is None:
                px, py = p[xi], p[yi]
            else:
                px, py = _rnd(p[xi] + p[vxi] * dt), _rnd(p[yi] + p[vyi] * dt)
            dx, dy = r[xi] - px, r[yi] - py
            mask, vals = 0, []
            for i in range(len(r)):
                if i in (ki, xi, yi):
                    continue
                if r[i] != p[i]:
                    mask |= 1 << i; vals.append(r[i])
            if not mask and dx == 0 and dy == 0:
                out.append(k)
            else:
                out.append([k, dx, dy] if not mask else [k, dx, dy, mask] + vals)
            last[k] = r; lt[k] = f["t"]
        f[key] = out


def expand_keyed(key, frames):
    ki, xi, yi, vxi, vyi = KEYED[key]
    last, lt = {}, {}
    for f in frames:
        rows = f.get(key)
        if rows is None:
            continue
        out = []
        for e in rows:
            if isinstance(e, int) and not isinstance(e, bool):
                k = e; p = last.get(k)
                if p is None:
                    continue
                dt = f["t"] - lt[k]
                r = list(p)
                if vxi is None: pass
                else: r[xi] = _rnd(p[xi] + p[vxi] * dt); r[yi] = _rnd(p[yi] + p[vyi] * dt)
            elif isinstance(e[0], list):
                r = e[0]; k = r[ki]
            else:
                k = e[0]; p = last.get(k)
                if p is None:
                    continue
                dt = f["t"] - lt[k]
                r = list(p)
                if vxi is None:
                    r[xi] = p[xi] + e[1]; r[yi] = p[yi] + e[2]
                else:
                    r[xi] = e[1] + _rnd(p[xi] + p[vxi] * dt); r[yi] = e[2] + _rnd(p[yi] + p[vyi] * dt)
                if len(e) > 3:
                    m, vi, i = e[3], 4, 0
                    while m:
                        if m & 1:
                            r[i] = e[vi]; vi += 1
                        m >>= 1; i += 1
            last[k] = r; lt[k] = f["t"]; out.append(r)
        f[key] = out


def code_events(events):
    kinds, order = {}, []
    for e in events:
        k = e["e"]
        if k not in kinds:
            kinds[k] = []; order.append(k)
        kinds[k].append(e)
    out = []
    for k in order:
        evs = kinds[k]
        cols = []
        for e in evs:
            for kk in e:
                if kk not in ("t", "e") and kk not in cols:
                    cols.append(kk)
        if any(v is None for e in evs for v in e.values()):
            return None
        data = [[e.get(c) for c in cols] for e in evs]     # missing -> None
        spec = []
        for ci, c in enumerate(cols):
            vals = [row[ci] for row in data if row[ci] is not None]
            mode, table = 0, None
            if vals and all(isinstance(v, str) for v in vals):
                t = sorted(set(vals))
                if len(t) <= 60:
                    mode, table = 2, t
                    idx = {v: i for i, v in enumerate(t)}
                    for row in data:
                        if row[ci] is not None:
                            row[ci] = idx[row[ci]]
            elif vals and all(isinstance(v, int) and not isinstance(v, bool) for v in vals):
                last, deltas, plain, dl = 0, [], 0, 0
                for row in data:
                    v = row[ci]
                    if v is None:
                        continue
                    plain += len(str(v)); dl += len(str(v - last)); last = v
                if dl < plain:
                    mode = 1
                    last = 0
                    for row in data:
                        v = row[ci]
                        if v is not None:
                            row[ci] = v - last; last = v
            spec.append([c, mode] + ([table] if table is not None else []))
        for row in data:
            while row and row[-1] is None:
                row.pop()
        ts, last = [], 0
        for e in evs:
            ts.append(e["t"] - last); last = e["t"]
        out.append({"k": k, "c": spec, "t": ts, "r": data})
    return out


def expand_events(tabs):
    res = []
    for tb in tabs:
        spec = tb["c"]; last = [0] * len(spec); t = 0
        for i, row in enumerate(tb["r"]):
            t += tb["t"][i]
            e = {"t": t, "e": tb["k"]}
            for ci, s in enumerate(spec):
                v = row[ci] if ci < len(row) else None
                if v is None:
                    continue
                if s[1] == 1:
                    v = last[ci] + v; last[ci] = v
                elif s[1] == 2:
                    v = s[2][v]
                e[s[0]] = v
            res.append(e)
    return res


def compact(o):
    """v4 -> v5 in place on a copy-free basis (returns o)."""
    if o.get("v", 0) >= 5:
        return o
    for k in LISTS:
        code_list(k, o["frames"])
    for k in KEYED:
        code_keyed(k, o["frames"])
    ev = code_events(o.get("events") or [])
    if ev is not None:
        o["events"] = ev
        o["evc"] = 1
    o["v"] = 5
    return o


def expand(o):
    if o.get("v", 0) < 5:
        return o
    for k in LISTS:
        expand_list(k, o["frames"])
    for k in KEYED:
        expand_keyed(k, o["frames"])
    if o.get("evc"):
        o["events"] = expand_events(o["events"])
        del o["evc"]
    o["v"] = 4; o["full"] = 1   # l, b, a, s are now full rows (v4 had them delta-coded): the page skips its own decoding
    return o


def main():
    root = sys.argv[1]
    man_p = os.path.join(root, "data", "manifest.json")
    man = json.load(open(man_p, encoding="utf-8"))
    sizes = {}
    before = after = 0
    for p in sorted(glob.glob(os.path.join(root, "data", "*.json"))):
        if p == man_p:
            continue
        raw = open(p, "rb").read()
        o = json.loads(raw)
        if o.get("v", 0) >= 5:
            after += len(raw); before += len(raw); continue
        compact(o)
        blob = json.dumps(o, separators=(",", ":")).encode("utf-8")
        open(p, "wb").write(blob)
        before += len(raw); after += len(blob)
        sizes[os.path.splitext(os.path.basename(p))[0]] = len(blob)
    for s in man["sets"]:
        for f in s["fights"]:
            if f["id"] in sizes:
                f["bytes"] = sizes[f["id"]]
    json.dump(man, open(man_p, "w", encoding="utf-8"), separators=(",", ":"))
    print("data files: %.1f MiB -> %.1f MiB" % (before / 1048576, after / 1048576))


if __name__ == "__main__":
    main()

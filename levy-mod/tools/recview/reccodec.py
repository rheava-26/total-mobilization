#!/usr/bin/env python3
"""Format 13 frame rows: the compact row coding of fight recordings, and joining part files. Python 3, standard library only.

The C# original is FightRecFormat.RowCodec (the writer) and FightRecFormat.RowDecoder (the in-game reader); the JS one is in fightviewer/viewer.html.
A recording whose header has "codec": 1 writes the frame keys b, a, l, j and c this way (p, s and every other record are as in format 12):

  every number is stored as an integer in its field's unit: positions, sizes, life, ids in whole pixels / points, velocities (and boss ai0..ai3) in tenths.
  an entry of a row list is one of
    K                  the entity whose key is K: every field is its prediction
    [K, mask, d, ...]  the entity K: the fields whose bit is set in mask are prediction + d (in field order), the others their prediction
    [[full row]]       a new entity, or one that was not in the previous sample: the full row in natural units, exactly as in format 12
  the key is the id field (b and a: the NPC slot, field 1; l: the levy id, field 0; j and c: the projectile slot, appended as the last field).
  prediction: x and y move by the velocity ((v before + v now + 5) // 10 pixels over the 2-tick sample, v in tenths) or by the last step, whichever was closer on the sample
  before (a fresh entity starts with the velocity); every other field stays as it was.
  an entity is predicted only from the sample right before: one that is absent from a sample is forgotten, and comes back as a full row.
  a hdr line (the start of a part file) forgets everything.

PART FILES: a recording that would pass the part size (about 9 MB) goes on in NAME_part2.levyrec, NAME_part3.levyrec ...; each part starts with the header
(with "part": n) and full rows, so a part reads on its own. join_parts() puts a recording's parts back together (one hdr, every line in order).
"""
import gzip, json, os, re

# list key -> (key field index, number of fields, position field indices, tenth-unit field indices)
LISTS = {
    "b": (1, 19, (2, 3), (4, 5, 8, 9, 10, 11)),
    "a": (1, 12, (2, 3), (4, 5)),
    "l": (0, 13, (1, 2), (3, 4)),
    "j": (8, 9, (1, 2), (3, 4)),
    "c": (7, 8, (1, 2), (3, 4)),
}
PART_RE = re.compile(r"^(.*)_part(\d+)(\.levyrec|\.jsonl\.gz|\.jsonl)$")


def _vstep(a, b):
    """Pixels moved over one sample (2 ticks) by a body whose velocity went from a to b tenths of a pixel per tick."""
    return (a + b + 5) // 10


def _pred_pos(pq, q, st, pi, p, ten):
    """Prediction of position field p (pi 0 = x, 1 = y) from the previous row pq, the state st (step, use velocity) and this row's velocity (already decoded in q)."""
    if st[1]:
        return pq[p] + _vstep(pq[ten[pi]], q[ten[pi]])
    return pq[p] + st[0][pi]


def _next_state(pq, q, st, pos, ten):
    """After a row is known: its step, and which predictor the next sample uses (the one that would have been closer this time)."""
    es = ev = 0
    for pi, p in enumerate(pos):
        es += abs(q[p] - pq[p] - st[0][pi])
        ev += abs(q[p] - pq[p] - _vstep(pq[ten[pi]], q[ten[pi]]))
    return ([q[p] - pq[p] for p in pos], ev <= es)


FRESH = ([0, 0], True)


def _q(v, ten):
    if not isinstance(v, (int, float)) or v != v or v in (float("inf"), float("-inf")):
        return 0
    x = v * 10 if ten else v
    return int(x + 0.5) if x >= 0 else -int(-x + 0.5)


def _nat(q, ten):
    if not ten:
        return q
    return q // 10 if q % 10 == 0 else q / 10.0


class Decoder:
    """Turns format 13 coded rows back into full rows (natural units). One per recording; feed it every line in order."""

    def __init__(self):
        self.st = {k: {} for k in LISTS}
        self.on = False

    def header(self, hdr):
        self.on = bool(hdr.get("codec"))
        for k in self.st:
            self.st[k] = {}

    def frame(self, f):
        if not self.on:
            return f
        for key, (ki, n, pos, ten) in LISTS.items():
            old = self.st[key]
            new = {}
            rows = f.get(key)
            if rows:
                out = []
                for e in rows:
                    if isinstance(e, list) and e and isinstance(e[0], list):
                        r = e[0]
                        q = [_q(r[i], i in ten) for i in range(len(r))]
                        k = q[ki]
                        new[k] = (q, FRESH)
                    else:
                        if isinstance(e, list):
                            k, mask, vals = e[0], e[1], e[2:]
                        else:
                            k, mask, vals = e, 0, ()
                        prev = old.get(k)
                        if prev is None:
                            continue  # a coded row with nothing before it: a reader that started mid-file
                        pq, st = prev
                        q = list(pq)
                        d = {}
                        vi, i, m = 0, 0, mask
                        while m:
                            if m & 1:
                                d[i] = vals[vi] if vi < len(vals) else 0
                                vi += 1
                            m >>= 1
                            i += 1
                        for i, dv in d.items():
                            if i not in pos:
                                q[i] += dv
                        for pi, p in enumerate(pos):
                            q[p] = _pred_pos(pq, q, st, pi, p, ten) + d.get(p, 0)
                        new[k] = (q, _next_state(pq, q, st, pos, ten))
                    out.append([_nat(v, i in ten) for i, v in enumerate(new[k][0])])
                f[key] = out
            self.st[key] = new
        return f


class Encoder:
    """Format 12 shaped frames -> format 13 coded frames (the converter's half; the game's writer is FightRecFormat.RowCodec)."""

    def __init__(self):
        self.st = {k: {} for k in LISTS}

    def reset(self):
        for k in self.st:
            self.st[k] = {}

    def frame(self, f):
        for key, (ki, n, pos, ten) in LISTS.items():
            old = self.st[key]
            new = {}
            rows = f.get(key)
            if rows:
                out = []
                for r in rows:
                    q = [_q(r[i], i in ten) for i in range(len(r))]
                    k = q[ki]
                    prev = old.get(k)
                    if prev is None or k in new or len(prev[0]) != len(q):
                        out.append([[_nat(v, i in ten) for i, v in enumerate(q)]])
                        new[k] = (q, FRESH)
                        continue
                    pq, st = prev
                    pred = list(pq)
                    for pi, p in enumerate(pos):
                        pred[p] = _pred_pos(pq, q, st, pi, p, ten)
                    mask, vals = 0, []
                    for i in range(len(q)):
                        if i != ki and q[i] != pred[i]:
                            mask |= 1 << i
                            vals.append(q[i] - pred[i])
                    out.append(k if mask == 0 else [k, mask] + vals)
                    new[k] = (q, _next_state(pq, q, st, pos, ten))
                f[key] = out
            self.st[key] = new
        return f


def decode_rows(rows):
    """Decode every format 13 frame of a list of parsed records in place (format 12 and older pass through). Returns rows."""
    d = Decoder()
    for r in rows:
        k = r.get("k")
        if k == "hdr":
            d.header(r)
        elif k == "f" and d.on:
            d.frame(r)
    return rows


def part_of(path):
    """(base path, part number) for NAME_partN.levyrec; (path, 1) for the first part or a single file."""
    m = PART_RE.match(path)
    if m:
        return m.group(1) + m.group(3), int(m.group(2))
    return path, 1


def parts_of(path):
    """Every part file of the recording that path belongs to, in order (just [path] for a single file)."""
    base, _n = part_of(path)
    stem, ext = None, None
    for e in (".levyrec", ".jsonl.gz", ".jsonl"):
        if base.endswith(e):
            stem, ext = base[: -len(e)], e
            break
    if stem is None:
        return [path]
    out = [base] if os.path.isfile(base) else []
    n = 2
    while os.path.isfile("%s_part%d%s" % (stem, n, ext)):
        out.append("%s_part%d%s" % (stem, n, ext))
        n += 1
    return out or [path]


def read_lines(path):
    """Raw JSON lines of one file (gzip sniffed by its magic bytes); a cut gzip stream yields what it holds."""
    with open(path, "rb") as fh:
        gz = fh.read(2) == b"\x1f\x8b"
    out = []
    try:
        with (gzip.open(path, "rt", encoding="utf-8") if gz else open(path, "r", encoding="utf-8")) as fh:
            for line in fh:
                line = line.strip()
                if line:
                    out.append(line)
    except (EOFError, OSError):
        pass
    return out


def join_parts(row_lists):
    """Parsed records of a recording's parts (in order) -> one list: the first header, the later parts' lines after it, every frame decoded."""
    out = []
    for i, rows in enumerate(row_lists):
        decode_rows(rows)
        for r in rows:
            if i > 0 and r.get("k") == "hdr":
                continue
            out.append(r)
    return out

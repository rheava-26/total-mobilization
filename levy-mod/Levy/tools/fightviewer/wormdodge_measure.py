#!/usr/bin/env python3
"""Worm-boss dodge measurement over FightRecorder (format 6) recordings.

For every "close pass" (a worm HEAD comes within CLOSE px of a body: the commander or a levy) it measures what the body did in the
WIN seconds before the closest approach, in the head's frame:
  along   displacement along the head's heading at closest approach (+ = same way the head is going)
  perp    displacement perpendicular to it, signed so + = toward the side the head is TURNING TOWARD ("with the turn"), - = away from the turn
  toward  displacement toward the head's position at the window start (+ = closing on the head)
  t50     seconds before closest approach at which the body had made half of its window displacement (small = late commit)
  late    share of the window path length travelled in the last 0.4 s
  rise    pixels climbed (positive up) at the highest point of the window, vs. the start
  hit     the body took damage from the worm within +-0.5 s of closest approach (head part / body part)
Usage: wormdodge_measure.py [--csv out.csv] GROUP=glob [GROUP=glob ...]
Only reads recordings. Run with python3 -I.
"""
import glob, gzip, json, math, sys, statistics as st

CLOSE = 128.0          # px = 8 tiles
WIN = 1.5              # s
HEAD_TYPES = {792, 13, 134, 7}                 # worm HEAD npc types: Desert Scourge, Eater of Worlds, The Destroyer, Giant Devourer
WORM_PARTS = {791, 793, 14, 15, 135, 136, 8, 9, 10}


def load(path):
    op = gzip.open if path.endswith('.gz') else open
    out = []
    with op(path, 'rt', encoding='utf-8') as f:
        for ln in f:
            ln = ln.strip()
            if ln:
                try: out.append(json.loads(ln))
                except ValueError: pass
    return out


def unit(x, y):
    n = math.hypot(x, y)
    return (x / n, y / n) if n > 1e-6 else (0.0, 0.0)


def passes_of(rows, group, fname):
    frames = [r for r in rows if r.get('k') == 'f']
    if len(frames) < 10: return []
    ts = [r['t'] for r in frames]
    dt = (ts[1] - ts[0]) / 60.0 or 1 / 30.0
    hz_n = int(round(WIN / dt))
    hpos = {}
    for i, r in enumerate(frames):
        for b in r.get('b', []):
            if b[0] in HEAD_TYPES: hpos.setdefault(b[1], {})[i] = (b[2], b[3])
    if not hpos: return []
    bodies = {}
    for i, r in enumerate(frames):
        p = r.get('p')
        if p and not (int(p[5]) & 8192): bodies.setdefault('P', {})[i] = (p[0], p[1], int(p[5]), p[2], p[3])
        for l in r.get('l', []): bodies.setdefault(('L', int(l[0])), {})[i] = (l[1], l[2], int(l[5]), l[3], l[4])
    hits = {}
    for e in rows:
        if e.get('k') != 'e': continue
        if e.get('e') == 'hit' and e.get('npc') in (HEAD_TYPES | WORM_PARTS): hits.setdefault('P', []).append((e['t'], e['npc']))
        if e.get('e') == 'lhit' and e.get('by') == 'boss' and e.get('npc') in (HEAD_TYPES | WORM_PARTS):
            hits.setdefault(('L', int(e['id'])), []).append((e['t'], e['npc']))
    tgt = []
    for r in frames:
        m = None
        for b in r.get('b', []):
            if b[0] in HEAD_TYPES and len(b) > 15: m = (b[14], int(b[15])); break
        tgt.append(m)
    out = []
    for bk, tr in bodies.items():
        if len(tr) < hz_n + 2: continue
        for hid, hp in hpos.items():
            d = {i: math.hypot(tr[i][0] - hp[i][0], tr[i][1] - hp[i][1]) for i in tr if i in hp}
            ks = sorted(d)
            last_pass = -10 ** 9
            j = 0
            while j < len(ks):
                i = ks[j]
                if d[i] >= CLOSE: j += 1; continue
                run = [i]; j += 1
                while j < len(ks) and ks[j] - run[-1] <= 2 and d[ks[j]] < CLOSE: run.append(ks[j]); j += 1
                ic = min(run, key=lambda q: d[q])
                if ic - last_pass < 2.0 / dt: continue
                i0 = ic - hz_n
                if i0 < 0 or i0 not in tr or i0 not in hp: continue
                if any((q not in tr or q not in hp) for q in range(i0, ic + 1, 1)): continue
                last_pass = ic
                rec = measure(tr, hp, i0, ic, dt)
                if rec is None: continue
                tc = ts[ic]
                hh = [n for (t, n) in hits.get(bk, []) if abs(t - tc) <= 30]
                rec['hit_head'] = int(any(n in HEAD_TYPES for n in hh))
                rec['hit_body'] = int(any(n in WORM_PARTS for n in hh))
                rec['hit'] = int(bool(hh))
                rec['targeted'] = int(tgt[ic] is not None and ((bk == 'P' and tgt[ic][0] == 1) or (bk != 'P' and tgt[ic][0] == 2 and tgt[ic][1] == bk[1])))
                rec['who'] = 'cmd' if bk == 'P' else 'levy'
                rec['group'] = group; rec['file'] = fname; rec['t_s'] = round(tc / 60.0, 1)
                out.append(rec)
    return out


def measure(tr, hp, i0, ic, dt):
    a = max(i0, ic - int(0.5 / dt)); b = max(i0, ic - int(0.2 / dt))
    if a == b: return None
    ux, uy = unit(hp[b][0] - hp[a][0], hp[b][1] - hp[a][1])
    if ux == 0 and uy == 0: return None
    c0 = max(i0, ic - int(0.9 / dt)); c1 = ic - int(0.3 / dt)
    if c1 <= c0: return None
    v0 = unit(hp[c0 + 1][0] - hp[c0][0], hp[c0 + 1][1] - hp[c0][1]) if (c0 + 1) in hp else (ux, uy)
    v0 = unit(hp[c0 + 2][0] - hp[c0][0], hp[c0 + 2][1] - hp[c0][1]) if (c0 + 2) in hp else v0
    v1 = unit(hp[ic][0] - hp[c1][0], hp[ic][1] - hp[c1][1])
    omega = v0[0] * v1[1] - v0[1] * v1[0]
    sg = 1.0 if omega >= 0 else -1.0
    nx, ny = -uy, ux
    bx0, by0 = tr[i0][0], tr[i0][1]; bx1, by1 = tr[ic][0], tr[ic][1]
    Dx, Dy = bx1 - bx0, by1 - by0
    hx0, hy0 = hp[i0]
    tx, ty = unit(hx0 - bx0, hy0 - by0)
    along = Dx * ux + Dy * uy
    perp = (Dx * nx + Dy * ny) * sg
    toward = Dx * tx + Dy * ty
    tot = math.hypot(Dx, Dy)
    pl = 0.0; prev = (bx0, by0); qlate = ic - int(0.4 / dt); pl_late = 0.0
    for q in range(i0, ic + 1):
        pl += math.hypot(tr[q][0] - prev[0], tr[q][1] - prev[1]); prev = (tr[q][0], tr[q][1])
        if q <= qlate: pl_late = pl
    late = (pl - pl_late) / pl if pl > 1 else 0.0
    t50 = None
    if tot > 8:
        for q in range(i0, ic + 1):
            if math.hypot(tr[q][0] - bx0, tr[q][1] - by0) >= 0.5 * tot: t50 = (ic - q) * dt; break
    rise = max(by0 - tr[q][1] for q in range(i0, ic + 1))
    rel = (bx1 - hp[ic][0]) * nx + (by1 - hp[ic][1]) * ny
    hspeed = math.hypot(hp[ic][0] - hp[c1][0], hp[ic][1] - hp[c1][1]) / max(1e-6, (ic - c1) * dt) / 60.0   # px per tick
    # commit impulse: the biggest velocity change between consecutive samples in the window (jump, double jump, dash, wing/hook kick)
    best = None
    for q in range(i0 + 1, ic + 1):
        dvx = tr[q][3] - tr[q - 1][3]; dvy = tr[q][4] - tr[q - 1][4]
        m = math.hypot(dvx, dvy)
        if best is None or m > best[0]: best = (m, q, dvx, dvy)
    cm, cq, dvx, dvy = best
    rp0 = (bx0 - hx0) * nx + (by0 - hy0) * ny          # lateral offset from the head's final heading line at window start
    side = 1.0 if (bx1 - hp[ic][0]) * nx + (by1 - hp[ic][1]) * ny >= 0 else -1.0
    # mean relative body velocity vs head heading over the last 0.5 s: cosine and lateral-outward speed
    cs = []; lat = []
    for q in range(max(i0, ic - int(0.5 / dt)), ic + 1):
        bv = unit(tr[q][3], tr[q][4]); sp = math.hypot(tr[q][3], tr[q][4])
        if sp > 0.5:
            cs.append(bv[0] * ux + bv[1] * uy)
        lat.append((tr[q][3] * nx + tr[q][4] * ny) * side)
    # horizontal rule: head's horizontal heading just before closest approach vs the body's horizontal motion (+ = WITH the head's horizontal direction)
    hsx = 1.0 if (hp[b][0] - hp[a][0]) >= 0 else -1.0
    hx_with = Dx * hsx
    cvx_with = dvx * hsx
    lastvx = sum(tr[q][3] for q in range(max(i0, ic - int(0.5 / dt)), ic + 1)) / max(1, len(range(max(i0, ic - int(0.5 / dt)), ic + 1))) * hsx
    return dict(hx_with=round(hx_with, 1), cvx_with=round(cvx_with, 1), lastvx_with=round(lastvx, 2), c_mag=round(cm, 1) if cm >= 3 else 0, c_t=round((ic - cq) * dt, 2) if cm >= 3 else '',
                c_dist=round(math.hypot(tr[cq][0] - hp[cq][0], tr[cq][1] - hp[cq][1])) if cm >= 3 else '', hold=round(sum(math.hypot(tr[q][3], tr[q][4]) for q in range(i0, max(i0 + 1, ic - int(0.6 / dt)))) / max(1, len(range(i0, max(i0 + 1, ic - int(0.6 / dt))))), 1),
                c_along=round(dvx * ux + dvy * uy, 1), c_out=round((dvx * nx + dvy * ny) * side, 1), c_up=round(-dvy, 1),
                cos_head=round(sum(cs) / len(cs), 2) if cs else '', lat_out=round(sum(lat) / len(lat), 2),
                dmin=round(math.hypot(bx1 - hp[ic][0], by1 - hp[ic][1]), 1), d0=round(math.hypot(bx0 - hx0, by0 - hy0), 1),
                hspeed=round(hspeed, 1), along=round(along, 1), perp=round(perp, 1), toward=round(toward, 1),
                disp=round(tot, 1), path=round(pl, 1), t50=(round(t50, 2) if t50 is not None else ''), late=round(late, 2),
                rise=round(rise, 1), side_with_turn=round(rel * sg, 1), omega=round(omega, 2))


def med(v):
    v = [x for x in v if x != '' and x is not None]
    return round(st.median(v), 2) if v else float('nan')


def mean(v):
    v = [x for x in v if x != '' and x is not None]
    return round(sum(v) / len(v), 2) if v else float('nan')


def summarize(recs, title):
    print('\n== %s  (n=%d passes)' % (title, len(recs)))
    if not recs: return
    pr = lambda k: med([r[k] for r in recs])
    n = len(recs)
    print('  dmin med %s | disp med %s | path med %s | head px/tick med %s' % (pr('dmin'), pr('disp'), pr('path'), pr('hspeed')))
    print('  along med %s | perp(with-turn +) med %s mean %s | toward-head med %s mean %s' % (pr('along'), pr('perp'), mean([r['perp'] for r in recs]), pr('toward'), mean([r['toward'] for r in recs])))
    print('  WITH the turn (perp>20px) %.0f%% | AWAY from turn (perp<-20) %.0f%% | toward head (>20) %.0f%% | away from head (<-20) %.0f%%' % (
        100 * sum(r['perp'] > 20 for r in recs) / n, 100 * sum(r['perp'] < -20 for r in recs) / n,
        100 * sum(r['toward'] > 20 for r in recs) / n, 100 * sum(r['toward'] < -20 for r in recs) / n))
    print('  ends on turn side %.0f%% | t50 med %s s | late-share med %s | rise med %s px | still (disp<24px) %.0f%%' % (
        100 * sum(r['side_with_turn'] > 0 for r in recs) / n, pr('t50'), pr('late'), pr('rise'), 100 * sum(r['disp'] < 24 for r in recs) / n))
    cr = [r for r in recs if r['c_mag']]
    print('  commit impulse: found %.0f%% | time before closest med %s s | dv along-head med %s | dv outward(from head line) med %s | dv up med %s | |dv| med %s' % (
        100 * len(cr) / n, med([r['c_t'] for r in cr]), med([r['c_along'] for r in cr]), med([r['c_out'] for r in cr]), med([r['c_up'] for r in cr]), med([r['c_mag'] for r in cr])))
    print('  HORIZONTAL vs head vx: window dx AGAINST head %.0f%% / WITH %.0f%% (|dx|>20px) | commit dvx against %.0f%% / with %.0f%% (|dv|>2) | last-0.5s mean vx against %.0f%% / with %.0f%% | median dx_with %s' % (
        100 * sum(r['hx_with'] < -20 for r in recs) / n, 100 * sum(r['hx_with'] > 20 for r in recs) / n,
        100 * sum(r['cvx_with'] < -2 for r in cr) / max(1, len(cr)), 100 * sum(r['cvx_with'] > 2 for r in cr) / max(1, len(cr)),
        100 * sum(r['lastvx_with'] < -0.5 for r in recs) / n, 100 * sum(r['lastvx_with'] > 0.5 for r in recs) / n, pr('hx_with')))
    print('  head-to-body distance at the commit: med %s px | body speed px/tick in the first 0.9 s of the window (hold/approach): med %s' % (med([r['c_dist'] for r in cr]), pr('hold')))
    print('  last 0.5s: cos(body velocity, head heading) mean %s (+1 = running WITH the head) | lateral-outward speed px/tick mean %s' % (mean([r['cos_head'] for r in recs]), mean([r['lat_out'] for r in recs])))
    print('  HIT within 0.5s of closest approach: %.0f%% (head %.0f%%, body part %.0f%%) | dmin<40px: %.0f%%' % (
        100 * sum(r['hit'] for r in recs) / n, 100 * sum(r['hit_head'] for r in recs) / n,
        100 * sum(r['hit_body'] for r in recs) / n, 100 * sum(r['dmin'] < 40 for r in recs) / n))


def main(argv):
    csv_out = None; specs = []; i = 0
    while i < len(argv):
        if argv[i] == '--csv': csv_out = argv[i + 1]; i += 2
        else: specs.append(argv[i]); i += 1
    allr = []
    for sp in specs:
        g, pat = sp.split('=', 1)
        for fn in sorted(glob.glob(pat)):
            try: rows = load(fn)
            except Exception as ex: print('skip', fn, ex); continue
            allr += passes_of(rows, g, fn.split('/')[-1])
    groups = []
    for r in allr:
        if (r['group'], r['who']) not in groups: groups.append((r['group'], r['who']))
    for g, w in groups:
        rs = [r for r in allr if r['group'] == g and r['who'] == w]
        summarize(rs, '%s / %s (all passes)' % (g, w))
        summarize([r for r in rs if r['targeted']], '%s / %s (head targets this body)' % (g, w))
    if csv_out and allr:
        keys = list(allr[0].keys())
        with open(csv_out, 'w') as f:
            f.write(','.join(keys) + '\n')
            for r in allr: f.write(','.join(str(r[k]) for k in keys) + '\n')


if __name__ == '__main__':
    main(sys.argv[1:])

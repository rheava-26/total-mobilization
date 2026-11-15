#!/usr/bin/env python3
"""bossleft.py NAME=results.csv ... : win rate (Wilson CI) plus the continuous measure 'boss life left / max' (0 for a win): mean, SE, median; and Fisher / Mann-Whitney vs the first arm."""
import csv, math, statistics as st, sys


def wilson(w, n, z=1.96):
    p = w / n; d = 1 + z * z / n; c = p + z * z / (2 * n); h = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)); return (c - h) / d, (c + h) / d


def fisher(a, b, c, d):
    n1, n2, k = a + b, c + d, a + c
    pm = lambda x: math.comb(n1, x) * math.comb(n2, k - x) / math.comb(n1 + n2, k)
    p0 = pm(a)
    return min(1.0, sum(pm(x) for x in range(max(0, k - n2), min(n1, k) + 1) if pm(x) <= p0 * (1 + 1e-9)))


def mwu(x, y):
    """two-sided Mann-Whitney U p (normal approximation with tie correction)."""
    n1, n2 = len(x), len(y); allv = sorted([(v, 0) for v in x] + [(v, 1) for v in y])
    ranks = [0.0] * len(allv); i = 0; tie = 0
    while i < len(allv):
        j = i
        while j + 1 < len(allv) and allv[j + 1][0] == allv[i][0]: j += 1
        r = (i + j) / 2 + 1
        for k in range(i, j + 1): ranks[k] = r
        t = j - i + 1; tie += t ** 3 - t; i = j + 1
    r1 = sum(r for r, (v, g) in zip(ranks, allv) if g == 0); u = r1 - n1 * (n1 + 1) / 2
    mu = n1 * n2 / 2; N = n1 + n2; sd = math.sqrt(n1 * n2 / 12 * ((N + 1) - tie / (N * (N - 1))))
    z = (u - mu) / sd if sd else 0
    return math.erfc(abs(z) / math.sqrt(2))


arms = []
for s in sys.argv[1:]:
    name, paths = s.split('=', 1)
    rs = []
    for p in paths.split('+'):
        rs += [r for r in csv.DictReader(open(p)) if r['result'] in ('WIN', 'LOSS')]
    arms.append((name, rs))
base = None
for name, rs in arms:
    n = len(rs); w = sum(r['result'] == 'WIN' for r in rs); lo, hi = wilson(w, n)
    fr = [float(r['bossLeft']) / float(r['bossMax']) for r in rs]
    dm = [float(r['dmgBoss']) for r in rs]
    line = f"{name:10s} n={n:3d} wins={w:3d} ({100*w/n:4.1f}%, {100*lo:.0f}-{100*hi:.0f})  bossLeft mean {100*st.mean(fr):4.1f}% (se {100*st.pstdev(fr)/n**.5:.1f}) median {100*st.median(fr):4.1f}%  dmgBoss mean {st.mean(dm)/1000:.0f}k (se {st.pstdev(dm)/n**.5/1000:.1f})"
    if base is None: base = (w, n, fr, dm)
    else: line += f"  | vs {arms[0][0]}: Fisher p={fisher(w, n - w, base[0], base[1] - base[0]):.3f}  MWU(bossLeft) p={mwu(fr, base[2]):.3f}"
    print(line)

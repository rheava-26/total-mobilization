#!/usr/bin/env python3
"""pccmp.py PRE=results.csv CUR=results.csv : Skeletron Prime (cell c0) and Calamitas Clone (cell c1) Master+Death, pre-slots build vs current build:
wins, levy deaths, contact damage taken per fight (head+body+tail+other contact), projectile damage, kill time of the wins; Mann-Whitney p of contact damage and deaths."""
import csv, math, statistics as st, sys


def fisher(a, b, c, d):
    n1, n2, k = a + b, c + d, a + c
    pm = lambda x: math.comb(n1, x) * math.comb(n2, k - x) / math.comb(n1 + n2, k)
    p0 = pm(a)
    return min(1.0, sum(pm(x) for x in range(max(0, k - n2), min(n1, k) + 1) if pm(x) <= p0 * (1 + 1e-9)))


def mwu(x, y):
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
    return math.erfc(abs((u - mu) / sd) / math.sqrt(2)) if sd else 1.0


def f(r, c):
    try: return float(r[c])
    except (TypeError, ValueError): return 0.0


def load(p):
    return [r for r in csv.DictReader(open(p)) if r['result'] in ('WIN', 'LOSS')]


pre = load(sys.argv[1].split('=', 1)[1]); cur = load(sys.argv[2].split('=', 1)[1])
contact = lambda r: f(r, 'takenHead') + f(r, 'takenBody') + f(r, 'takenTail') + f(r, 'takenOtherContact')
for cell, name in (('c0', 'Skeletron Prime'), ('c1', 'Calamitas Clone')):
    a = [r for r in pre if r['cell'] == cell]; b = [r for r in cur if r['cell'] == cell]
    wa = sum(r['result'] == 'WIN' for r in a); wb = sum(r['result'] == 'WIN' for r in b)
    ca, cb = [contact(r) for r in a], [contact(r) for r in b]; da, db = [f(r, 'deaths') for r in a], [f(r, 'deaths') for r in b]
    print(f"{name:16s} pre n={len(a)} wins={wa} contact {st.mean(ca):6.0f} (se {st.pstdev(ca)/len(a)**.5:4.0f}) deaths {st.mean(da):4.1f} proj {st.mean(f(r,'takenProj') for r in a):5.0f}  |  cur n={len(b)} wins={wb} contact {st.mean(cb):6.0f} (se {st.pstdev(cb)/len(b)**.5:4.0f}) deaths {st.mean(db):4.1f} proj {st.mean(f(r,'takenProj') for r in b):5.0f}"
          f"  | Fisher(wins) p={fisher(wa, len(a)-wa, wb, len(b)-wb):.2f} MWU(contact) p={mwu(ca, cb):.2f} MWU(deaths) p={mwu(da, db):.2f}")

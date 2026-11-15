#!/usr/bin/env python3
"""analyze2.py ARM=DIR [ARM=DIR ...]: per arm, per-fight metrics from the recordings, mean +- SE over fights.
metrics: levy grounded |vx| (px/tick), fraction of levy-frames inside a body/head hitbox, contact hits taken per fight (head/body/tail), damage per hit, first death, levy-seconds alive."""
import glob, gzip, json, os, statistics as st, sys
from analyze import fight


def mse(xs):
    xs = [x for x in xs if x is not None]
    return f"{st.mean(xs):8.3f} +-{st.pstdev(xs) / max(1, len(xs)) ** .5:6.3f}" if xs else "-"


arms = []
for spec in sys.argv[1:]:
    name, d = spec.split('=', 1)
    fs = sorted(glob.glob(os.path.join(d, '**', '*.levyrec'), recursive=True) + glob.glob(os.path.join(d, '**', '*.jsonl'), recursive=True))
    arms.append((name, [fight(f) for f in fs]))
rows = [
    ("grounded |vx| px/tick", lambda r: r['spd_g']),
    ("all |vx| px/tick", lambda r: r['spd']),
    ("contact frames /1000 levy-frames (any segment)", lambda r: 1000 * sum(r['contact'].values()) / max(1, r['lframes'])),
    ("contact frames /1000 (body 135)", lambda r: 1000 * r['contact'].get(135, 0) / max(1, r['lframes'])),
    ("contact frames /1000 (head 134)", lambda r: 1000 * r['contact'].get(134, 0) / max(1, r['lframes'])),
    ("boss contact hits/fight (head)", lambda r: sum(v for (s, n), v in r['hits'].items() if n == 134)),
    ("boss contact hits/fight (body)", lambda r: sum(v for (s, n), v in r['hits'].items() if n == 135 and s == 'body')),
    ("hits per 1000 contact frames", lambda r: 1000 * sum(v for (s, n), v in r['hits'].items() if s == 'body') / max(1, sum(r['contact'].values()))),
    ("flying fraction", lambda r: r['fly']),
    ("aerial fraction", lambda r: r['aer']),
    ("levy-frames per fight", lambda r: r['lframes']),
    ("deaths", lambda r: r['deaths']),
]
print(f"{'':50s}" + "".join(f"{n:>22s}" for n, _ in arms))
for label, f in rows:
    print(f"{label:50s}" + "".join(f"{mse([f(r) for r in rs]):>22s}" for _, rs in arms))

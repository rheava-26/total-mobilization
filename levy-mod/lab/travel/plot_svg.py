#!/usr/bin/env python3
"""plot_svg.py <out.svg> <title> <run_dir> [<run_dir> ...]   -- paths of every levy of one or more travel-lab runs (stacked panels), terrain from surface.csv when present."""
import csv, os, sys, collections

COL = {'none': '#6b7280', 'boots': '#d97706', 'wings': '#2563eb', 'hook': '#059669'}

def load(run):
    tr = collections.defaultdict(list)
    p = os.path.join(run, 'out', 'trace.csv')
    for r in csv.DictReader(open(p)):
        tr[(r['name'], r['gear'])].append((float(r['x']), float(r['elevation'])))
    surf = None
    sp = os.path.join(run, 'out', 'surface.csv')
    ev = open(os.path.join(run, 'out', 'events.txt')).read()
    x0 = 0
    for line in ev.splitlines():
        if line.startswith('world mode: X0='):
            x0 = int(line.split('X0=')[1].split()[0])
    if os.path.exists(sp):
        surf = {int(r['x']): int(r['surfaceRow']) for r in csv.DictReader(open(sp))}
    return tr, surf, x0

def main():
    out, title, runs = sys.argv[1], sys.argv[2], sys.argv[3:]
    W, H, PAD = 1000, 260, 30
    parts = []
    y = 0
    for run in runs:
        tr, surf, x0 = load(run)
        xs = [p[0] for pts in tr.values() for p in pts]
        if not xs: continue
        lo, hi = min(xs) - 10, max(xs) + 10
        if surf:  # terrain: rows -> elevation = 150 - row (the trace uses the same 150 reference)
            lo = min(lo, -10)
        els = [p[1] for pts in tr.values() for p in pts]
        elo, ehi = min(els) - 8, max(els) + 8
        sx = (W - 2 * PAD) / max(1.0, hi - lo); sy = (H - 2 * PAD) / max(1.0, ehi - elo)
        def X(v): return PAD + (v - lo) * sx
        def Y(v): return y + H - PAD - (v - elo) * sy
        parts.append(f'<text x="{PAD}" y="{y + 16}" font-size="13" font-family="monospace">{os.path.basename(run)}</text>')
        if surf:
            pts = []
            for off in range(int(lo), int(hi)):
                row = surf.get(x0 + off, -1)
                if row > 0: pts.append(f'{X(off):.1f},{Y(150 - row):.1f}')
            parts.append(f'<polyline fill="none" stroke="#9ca3af" stroke-width="1" points="{" ".join(pts)}"/>')
        for (name, gear), pts in sorted(tr.items()):
            d = ' '.join(f'{X(a):.1f},{Y(b):.1f}' for a, b in pts)
            parts.append(f'<polyline fill="none" stroke="{COL.get(gear, "#000")}" stroke-width="1" stroke-opacity="0.55" points="{d}"/>')
            a, b = pts[-1]
            parts.append(f'<circle cx="{X(a):.1f}" cy="{Y(b):.1f}" r="2.5" fill="{COL.get(gear, "#000")}"/>')
        y += H
    svg = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{y + 40}" viewBox="0 0 {W} {y + 40}"><rect width="100%" height="100%" fill="white"/>',
           f'<text x="{PAD}" y="{y + 28}" font-size="14" font-family="monospace" font-weight="bold">{title}   (grey = none, orange = boots, blue = wings, green = hook; dot = where the levy ended)</text>'] + parts + ['</svg>']
    open(out, 'w').write('\n'.join(svg))

main()

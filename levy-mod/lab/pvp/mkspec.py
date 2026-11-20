"""Writes the PvP lab spec: every matchup, both side assignments, N seeds. Raw calrev `fight pvp` lines for levylab (one cell per matchup and side).
  mkspec.py OUT.tsv [--seeds 1-5] [--cap 240] [--pairs eve:optimal,eve:optimal_ranged,eve_prov:optimal_prov,eve_prov:optimal_prov_ranged]
Army A stands on the left (team 1, spawned first), army B on the right (team 2). Each pair runs as X-vs-Y and Y-vs-X, so a side bias cancels. Order Search & Destroy (both armies hunt),
arena a3, Master + Death, commander at the arena centre (invulnerable, Spectator buff)."""
import argparse
from pathlib import Path
H = Path(__file__).resolve().parent
FILES = {'eve': H.parent / 'battery' / 'eve_army.json', 'optimal': H.parent / 'battery' / 'optimal_army.json', 'optimal_ranged': H.parent / 'battery' / 'optimal_ranged_army.json',
         'eve_prov': H / 'eve_prov_army.json', 'optimal_prov': H / 'optimal_prov_army.json', 'optimal_prov_ranged': H / 'optimal_prov_ranged_army.json'}
ap = argparse.ArgumentParser(); ap.add_argument('out'); ap.add_argument('--seeds', default='1-5'); ap.add_argument('--cap', type=int, default=240)
ap.add_argument('--pairs', default='eve:optimal,eve:optimal_ranged,eve_prov:optimal_prov,eve_prov:optimal_prov_ranged'); ap.add_argument('--one-side', action='store_true')
a = ap.parse_args()
lo, _, hi = a.seeds.partition('-'); seeds = range(int(lo), int(hi or lo) + 1)
lines = []
for pair in a.pairs.split(','):
    x, y = pair.split(':')
    for ta, tb in ([(x, y)] if a.one_side or x == y else [(x, y), (y, x)]):
        for sd in seeds:
            lines.append('\t'.join(['fight', 'pvp', 'mdeath', str(sd), str(a.cap * 60), 'order=sd', 'arena=a3', 'cmd=near', f'lv={FILES[ta]}', f'lvb={FILES[tb]}', f'ta={ta}', f'tb={tb}']))
Path(a.out).write_text('\n'.join(lines) + '\n'); print(len(lines), 'fights')

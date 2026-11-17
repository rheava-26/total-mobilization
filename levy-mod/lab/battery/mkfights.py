"""Writes the battery fight spec: every army x boss x seed, Master+Death, arena a3, commander near (the harness's invulnerable Spectator stand-in, idle), order follow.
  mkfights.py OUT.tsv ARMY[,ARMY...] [--seeds 1-3] [--bosses ravager,eol]
Ravager cap 360 s, Empress of Light 300 s (the harness runs every fight at night)."""
import argparse
from pathlib import Path
H = Path(__file__).resolve().parent
ap = argparse.ArgumentParser(); ap.add_argument('out'); ap.add_argument('armies'); ap.add_argument('--seeds', default='1-3'); ap.add_argument('--bosses', default='ravager,eol')
a = ap.parse_args()
lo, _, hi = a.seeds.partition('-'); seeds = range(int(lo), int(hi or lo) + 1)
CAP = {'ravager': 360, 'eol': 300}
lines = []
for army in a.armies.split(','):
    for boss in a.bosses.split(','):
        for sd in seeds:
            lines.append('\t'.join(['fight', boss, 'mdeath', str(sd), str(CAP[boss] * 60), 'cmd=near', 'order=follow', 'arena=a3', f'lv={H / (army + "_army.json")}', f'meta=bat_{army}_{boss}']))
Path(a.out).write_text('\n'.join(lines) + '\n'); print(len(lines), 'fights')

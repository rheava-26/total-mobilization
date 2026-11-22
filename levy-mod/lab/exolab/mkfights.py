"""Writes an Exo Mechs fight spec (raw harness lines for levylab): every army x opener x seed, Master+Death, arena big, the commander an idle immune Spectator
150 tiles left of the army (cmd=far), army order Free Rein (what her army was on in her 17:19 fight).
  mkfights.py OUT.tsv ARMY[,ARMY...] [--seeds 1-5] [--openers exoares,exothan,exotwins] [--cap 600] [--tokens hpscale=0 --tag _noscale]
ARMY = a file stem in armies/ (eve, eve_matched, optimal). Openers: exoares = Ares first, exothan = Thanatos first, exotwins = Artemis and Apollo first."""
import argparse
from pathlib import Path

H = Path(__file__).resolve().parent
ap = argparse.ArgumentParser(); ap.add_argument('out'); ap.add_argument('armies'); ap.add_argument('--seeds', default='1-5')
ap.add_argument('--openers', default='exoares,exothan,exotwins'); ap.add_argument('--cap', type=int, default=600); ap.add_argument('--order', default='free'); ap.add_argument('--tokens', default='', help='extra harness tokens, comma list (hpscale=0)'); ap.add_argument('--tag', default='', help='suffix of the meta cell name')
a = ap.parse_args()
lo, _, hi = a.seeds.partition('-'); seeds = range(int(lo), int(hi or lo) + 1)
lines = []
for sd in seeds:  # seed-major so a cut run still has every cell
    for army in a.armies.split(','):
        for op in a.openers.split(','):
            lines.append('\t'.join(['fight', op, 'mdeath', str(sd), str(a.cap * 60), 'cmd=far', f'order={a.order}', 'arena=big', f'lv={H / "armies" / (army + "_army.json")}', f'meta=exo_{army}{a.tag}_{op}'] + [t for t in a.tokens.split(',') if t]))
Path(a.out).write_text('\n'.join(lines) + '\n'); print(len(lines), 'fights ->', a.out)

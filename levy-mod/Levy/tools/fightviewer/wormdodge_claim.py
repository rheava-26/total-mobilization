#!/usr/bin/env python3
"""Eve's claim test: 'every time I get hit I turn into the boss; when I turn against it and jump I dodge it.'
For each close pass (head within CLOSE px of the body at closest approach) classify the last 1.0 s before closest approach:
  direction: mean body vx x sign(head vx) -> WITH (> +1 px/tick), AGAINST (< -1), STILL
  jump: an upward velocity kick (>= 4 px/tick) or, for the commander, the jump flag
and tabulate hit rate (the worm damaged the body within +-0.5 s of closest approach) per class.
Usage: wormdodge_claim.py CLOSE_PX GROUP=glob ..."""
import sys, glob, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import wormdodge_measure as M


def tab(recs, title):
    print('\n== %s (n=%d, hit %.0f%%)' % (title, len(recs), 100 * sum(r['hit'] for r in recs) / max(1, len(recs))))
    def row(name, f):
        rs = [r for r in recs if f(r)]
        print('   %-28s n=%3d  hit %5.1f%%' % (name, len(rs), 100 * sum(r['hit'] for r in rs) / len(rs) if rs else float('nan')))
    wi = lambda r: r['vs1'] > 1.0
    ag = lambda r: r['vs1'] < -1.0
    st = lambda r: not wi(r) and not ag(r)
    jp = lambda r: r['ups1'] >= 1 or r['jflag']
    row('WITH head vx', wi); row('AGAINST head vx', ag); row('still', st)
    row('jumped', jp); row('no jump', lambda r: not jp(r))
    row('AGAINST + jump', lambda r: ag(r) and jp(r)); row('AGAINST, no jump', lambda r: ag(r) and not jp(r))
    row('WITH + jump', lambda r: wi(r) and jp(r)); row('WITH, no jump', lambda r: wi(r) and not jp(r))
    ms = [r['mstart'] for r in recs if r['mstart'] != '']
    hs = [r['mstart'] for r in recs if r['mstart'] != '' and r['hit']]
    cs = [r['mstart'] for r in recs if r['mstart'] != '' and not r['hit']]
    print('   move started (s before closest approach) median: all %s | hit passes %s | clean passes %s' % (M.med(ms), M.med(hs), M.med(cs)))


def main(argv):
    M.CLOSE = float(argv[0]); allr = []
    for sp in argv[1:]:
        g, pat = sp.split('=', 1)
        for fn in sorted(glob.glob(pat)):
            allr += M.passes_of(M.load(fn), g, fn.split('/')[-1])
    groups = []
    for r in allr:
        if (r['group'], r['who']) not in groups: groups.append((r['group'], r['who']))
    for g, w in groups:
        tab([r for r in allr if r['group'] == g and r['who'] == w], '%s / %s  (head within %d px)' % (g, w, M.CLOSE))
    for w in ('cmd', 'levy'):
        pool = [r for r in allr if r['who'] == w and r['group'].startswith('eve')]
        if pool: tab(pool, 'ALL eve groups / %s' % w)


if __name__ == '__main__':
    main(sys.argv[1:])

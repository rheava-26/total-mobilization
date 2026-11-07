#!/usr/bin/env python3
"""Per-cell table for the worm-dodge lab: wins, kill time, levy deaths, levy hits from the worm HEAD vs BODY parts (from the recordings' lhit events).
Usage: wormdodge_table.py OUTDIR RECDIR TAG[,TAG...]   (OUTDIR has <tag>_<spec>.tsv from the calrev harness, RECDIR has <tag>_<spec>/*.jsonl)"""
import csv, glob, json, os, sys, statistics as st

HEAD = {792, 13, 134, 7}
PARTS = {791, 793, 14, 15, 135, 136, 8, 9, 10}


def hits(fn):
    h = b = 0
    for ln in open(fn):
        if '"lhit"' not in ln: continue
        try: e = json.loads(ln)
        except ValueError: continue
        if e.get('by') != 'boss': continue
        if e['npc'] in HEAD: h += 1
        elif e['npc'] in PARTS: b += 1
    return h, b


def x_is_aux(f): return f.endswith(('.gap.tsv', '.hit.tsv', '.l3.tsv'))


def main(out, rec, tags):
    print('| arm | cell | wins | kill s (wins) | boss left % (losses) | levy deaths | levy hits head | levy hits body |')
    print('|---|---|---|---|---|---|---|---|')
    for tag in tags:
        for f in sorted(x for x in glob.glob('%s/%s_*.tsv' % (out, tag)) if x.count('.') == 1 or x.endswith('_exp.tsv') or True):
            if x_is_aux(f): continue
            spec = os.path.basename(f)[len(tag) + 1:-4]
            rows = list(csv.DictReader(open(f), delimiter='\t'))
            if not rows: continue
            wins = [r for r in rows if r['result'] == 'WIN']
            kt = st.mean(float(r['sec']) for r in wins) if wins else float('nan')
            left = [100.0 * float(r['bossLeft']) / float(r['bossMax']) for r in rows if r['result'] != 'WIN']
            dths = st.mean(float(r['deaths']) for r in rows)
            hh = bb = 0; n = 0
            for rf in glob.glob('%s/%s_%s/*.jsonl' % (rec, tag, spec)):
                a, c = hits(rf); hh += a; bb += c; n += 1
            print('| %s | %s | %d/%d | %.0f | %.0f | %.1f | %.0f | %.0f |' % (tag, spec, len(wins), len(rows), kt, st.mean(left) if left else float('nan'), dths, hh / max(1, n), bb / max(1, n)))


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2], sys.argv[3].split(','))

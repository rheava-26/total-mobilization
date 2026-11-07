#!/usr/bin/env python3
"""Worm body lab table: wins, levy deaths, levy hits by worm HEAD vs BODY per fight, and body hits that came within 1 s after a jump (an upward velocity kick) or a dodge dash.
Usage: wormbody_table.py OUTDIR RECDIR ARM[,ARM...]   (OUTDIR/<arm>_<cell>.tsv from the calrev harness, RECDIR/<arm>_<cell>/*.jsonl). Run with python3 -I."""
import csv, glob, json, os, sys, collections

HEAD = {792, 13, 134, 7}
PARTS = {791, 793, 14, 15, 135, 136, 8, 9, 10}
WIN = 60  # ticks = 1 s


def fight(fn):
    lv = collections.defaultdict(list)   # id -> [(t, vx, vy)]
    hits = []
    for ln in open(fn):
        if '"k":"f"' in ln:
            try: e = json.loads(ln)
            except ValueError: continue
            for l in e.get('l', []): lv[int(l[0])].append((e['t'], l[3], l[4]))
        elif '"lhit"' in ln:
            try: e = json.loads(ln)
            except ValueError: continue
            if e.get('by') == 'boss': hits.append(e)
    h = b = ba = 0
    for e in hits:
        if e['npc'] in HEAD: h += 1; continue
        if e['npc'] not in PARTS: continue
        b += 1
        t = e['t']
        fr = [x for x in lv.get(int(e['id']), []) if t - WIN <= x[0] <= t]
        if any(x[2] < -4.0 for x in fr) or any(abs(x[1]) > 10.0 for x in fr): ba += 1
    return h, b, ba


def main(out, rec, arms):
    print('| arm | cell | wins | levy deaths | head hits | body hits | body hits <=1 s after jump/dash | boss left % |')
    print('|---|---|---|---|---|---|---|---|')
    for arm in arms:
        for f in sorted(glob.glob('%s/%s_*.tsv' % (out, arm))):
            if f.endswith(('.gap.tsv', '.hit.tsv', '.l3.tsv')): continue
            cell = os.path.basename(f)[len(arm) + 1:-4]
            rows = [r for r in csv.DictReader(open(f), delimiter='\t') if r['kind'] == 'fight']
            if not rows: continue
            n = len(rows); w = sum(1 for r in rows if r['result'] == 'WIN')
            dths = sum(float(r['deaths']) for r in rows) / n
            left = sum(float(r['bossLeft']) / max(1, float(r['bossMax'])) for r in rows) / n
            H = B = BA = 0
            for fn in glob.glob('%s/%s_%s/*.jsonl' % (rec, arm, cell)):
                h, b, ba = fight(fn); H += h; B += b; BA += ba
            nf = max(1, len(glob.glob('%s/%s_%s/*.jsonl' % (rec, arm, cell))))
            print('| %s | %s | %d/%d | %.1f | %.0f | %.0f | %.0f | %.0f |' % (arm, cell, w, n, dths, H / nf, B / nf, BA / nf, 100 * left))


main(sys.argv[1], sys.argv[2], sys.argv[3].split(','))

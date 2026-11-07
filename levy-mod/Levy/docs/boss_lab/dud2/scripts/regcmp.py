import sys, re, csv
# usage: regcmp.py reg_out.tsv ref  -> weapon, reference held dps (previous builder, single lab pool), now, ratio
ref = {}
for l in open(sys.argv[2]):
    w, p, h = l.rstrip('\n').split('\t'); ref[w] = (float(p), float(h))
rows = list(csv.reader(open(sys.argv[1]), delimiter='\t'))
h = rows[0]; ix = {k: i for i, k in enumerate(h)}
now = {}
for r in rows[1:]:
    if len(r) < len(h) - 3: continue
    m = re.search(r'cw(CalamityMod/\w+)', r[0])
    if not m: continue
    w = m.group(1).split('/')[1]
    now.setdefault(w, []).append(float(r[ix['dmgBoss']]) / float(r[ix['sec']]) / int(r[ix['n']]))
bad = 0
print(f"{'weapon':32s} {'plain_ref':>10s} {'held_ref':>10s} {'now':>10s}  verdict")
for w, v in now.items():
    p, hh = ref[w]; n = sum(v) / len(v)
    best = max(p, hh)
    verdict = ''
    if best > 20 and n < 0.5 * best: verdict = 'DOWN >50% (check seeds)'; bad += 1
    elif best > 20 and n < 0.75 * best: verdict = 'down 25-50% (noise?)'
    print(f"{w:32s} {p:10.1f} {hh:10.1f} {n:10.1f}  {verdict}")
print('flagged:', bad)

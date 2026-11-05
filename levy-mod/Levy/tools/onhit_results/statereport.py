import csv, sys, collections, re
# usage: statereport.py file.onhit.tsv   -> commander ModPlayer fields changed by soldier fire / hits (not changed in the idle controls)
rows = list(csv.DictReader(open(sys.argv[1]), delimiter='\t'))
neg = set(r['name'] for r in rows if r['kind'] == 'STATE' and r['weapon'].startswith('(no weapon)'))
fire = collections.defaultdict(set); hit = collections.defaultdict(set); ex = {}
for r in rows:
    if r['kind'] != 'STATE' or r['weapon'].startswith('(no weapon)'): continue
    n = r['name']
    if n in neg or re.search(r'statMana|statLife|lostLife|lifeRecovery|buffCount|Frame|frame|Timer|timer|color|Rainbow|rotation|Rotation', n): continue
    m = re.search(r'onFireTick=(\d+) afterHit=(\d+) other=(\d+)', r['stack'])
    f, h, o = map(int, m.groups())
    if f > 0: fire[n].add(r['weapon'])
    if h > 0: hit[n].add(r['weapon'])
    ex.setdefault(n, (r['weapon'], r['source']))
print('FIELDS CHANGED ON THE FIRE TICK (Shoot-time path), not in idle controls:')
for n, w in sorted(fire.items(), key=lambda x: -len(x[1])):
    print('  %-62s weapons=%3d e.g. %s %s' % (n, len(w), ex[n][0], ex[n][1]))
print('FIELDS CHANGED WITHIN 2 TICKS AFTER A SOLDIER HIT (owner-on-hit path), not in idle controls:')
for n, w in sorted(hit.items(), key=lambda x: -len(x[1])):
    print('  %-62s weapons=%3d e.g. %s %s' % (n, len(w), ex[n][0], ex[n][1]))

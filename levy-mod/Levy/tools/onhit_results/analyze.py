import csv, sys, collections, re
# usage: analyze.py file.onhit.tsv [--verbose]
f = sys.argv[1]
rows = list(csv.DictReader(open(f), delimiter='\t'))
by = collections.OrderedDict()
for r in rows:
    by.setdefault(r['trial'], []).append(r)
neg = None
for k, v in by.items():
    if k.startswith('(no weapon)') and 'stage0' in k:
        neg = v; break
negstate = set(r['name'] for r in neg if r['kind'] == 'STATE') if neg else set()
print('trials', len(by))
tot = collections.Counter()
atcmd = collections.OrderedDict()
statew = collections.OrderedDict()
hookw = collections.OrderedDict()
traj = collections.OrderedDict()
fired = 0
for k, v in by.items():
    t = [r for r in v if r['kind'] == 'TRIAL']
    if not t: continue
    m = dict(x.split('=') for x in t[0]['source'].split(';'))
    if '(no weapon)' in k: continue
    tot['weapons'] += 1
    if int(m['soldierProj']) > 0 or int(m['projHits']) > 0: tot['fired'] += 1
    if int(m['taggedHits']) > 0: tot['hit'] += 1
    c = []
    for r in v:
        if r['loc'] == 'CMD' and r['kind'] in ('PROJ', 'DUST', 'NPC', 'ITEM', 'COMBATTEXT', 'PARTICLE'):
            c.append(r)
        if r['kind'] == 'CMDHOOK': hookw.setdefault(k, []).append(r)
        if r['kind'] == 'PROJ-TRAJ' and r['source'].startswith('reachedCmd'): traj.setdefault(k, []).append(r)
        if r['kind'] == 'STATE' and r['name'] not in negstate and not re.search(r'statMana|statLife|lostLife|lifeRecovery|buffCount', r['name']): statew.setdefault(k, []).append(r)
    if c: atcmd[k] = c
print(dict(tot))
print('weapons with something spawning within 12 tiles of the COMMANDER:', len(atcmd))
print('weapons whose commander ModPlayer hooks fired:', len(hookw))
print('weapons with a projectile that flew to within 12 tiles of the commander:', len(traj))
print('weapons changing commander ModPlayer state not changed in the idle control:', len(statew))
verbose = '--verbose' in sys.argv
print()
for k, c in atcmd.items():
    print('AT-CMD', k)
    for r in c:
        print('    %-10s %-40s afterHit=%s n=%s src=%s | %s' % (r['kind'], r['name'][:40], r['afterHit'], r['count'], r['source'][:70], r['stack'][:200]))
print()
for k, c in traj.items():
    print('TRAJ', k, [(r['name'], r['source']) for r in c])
print()
for k, c in hookw.items(): print('HOOK', k, [(r['name'], r['tagged'], r['count']) for r in c])
print()
for k, c in statew.items():
    print('STATE', k)
    for r in c[:12]:
        print('    %-70s %s n=%s %s' % (r['name'], r['source'], r['count'], r['stack']))

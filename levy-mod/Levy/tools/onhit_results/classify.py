import csv, sys, collections
# usage: classify.py file.onhit.tsv [--list]
rows = list(csv.DictReader(open(sys.argv[1]), delimiter='\t'))

def cls(stack):
    s = stack
    if s.startswith('ThoriumMod.ThoriumPlayer.PostUpdateEquips') or 'PlayerLoader.PostUpdateEquips' in s.split(' < ')[0:3].__str__(): return 'PLAYER-UPDATE(noise)'
    if 'OnHitNPCWithProj' in s or 'OnHitNPC' in s.split(' < ')[0]: return 'HIT'
    if 'ItemLoader.Shoot' in s or 'TryRealShoot' in s or 'ModifyShootStats' in s or 'ItemLoader.UseItem' in s: return 'SHOOT-TIME'
    if 'OnKill' in s or 'Projectile.Kill' in s: return 'KILL'
    if '.AI <' in s or s.split(' < ')[0].endswith('.AI'): return 'AI-TICK'
    if 'PostAI' in s or 'PreAI' in s: return 'AI-TICK'
    return 'OTHER'

ev = collections.Counter(); wp = collections.defaultdict(set); tot = set(); kinds = collections.Counter()
detail = collections.defaultdict(list)
for r in rows:
    w = r['weapon']
    if w.startswith('(no weapon)'): continue
    tot.add(w)
    if r['kind'] in ('PROJ', 'DUST', 'NPC', 'ITEM', 'COMBATTEXT', 'PARTICLE') and r['loc'] == 'CMD':
        c = cls(r['stack'])
        if c.startswith('PLAYER-UPDATE'): continue
        key = (c, r['kind'])
        ev[key] += int(r['count']); wp[key].add(w); wp[(c, '*')].add(w); wp[('*', '*')].add(w)
        detail[(c)].append((w, r['kind'], r['name'], r['count'], r['stack'][:140]))
print('weapons in file:', len(tot))
print('weapons with >=1 spawn within 12 tiles of commander:', len(wp[('*', '*')]))
for c in ['HIT', 'KILL', 'AI-TICK', 'SHOOT-TIME', 'OTHER']:
    print('  %-11s weapons=%3d' % (c, len(wp[(c, '*')])), ' '.join('%s:%d(w%d)' % (k[1], v, len(wp[k])) for k, v in sorted(ev.items()) if k[0] == c))
tr = set(); hook = set()
for r in rows:
    if r['weapon'].startswith('(no weapon)'): continue
    if r['kind'] == 'PROJ-TRAJ' and r['source'].startswith('reachedCmd'): tr.add(r['weapon'])
    if r['kind'] == 'CMDHOOK' and '[commander]' in r['name']: hook.add(r['weapon'])
print('weapons with a projectile that flew to within 12 tiles of commander:', len(tr))
print('weapons where commander ModPlayer hooks fired:', len(hook))
dm = collections.defaultdict(int)
for r in rows:
    if r['kind'] == 'TRIAL':
        m = dict(x.split('=') for x in r['source'].split(';'))
        dm[r['weapon']] += int(m['dmg'])
print('total damage dealt over listed weapons:', sum(dm.values()), ' weapons that damaged:', sum(1 for v in dm.values() if v > 0))
if '--list' in sys.argv:
    for c, l in detail.items():
        print('==', c)
        for x in l: print('  ', x)

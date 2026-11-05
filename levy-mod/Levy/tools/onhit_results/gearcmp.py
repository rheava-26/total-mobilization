import csv, sys, collections, re
# usage: gearcmp.py file.onhit.tsv  -> for each gear entry, events within 12 tiles of the commander that appear with a firing soldier but not in the gear's idle control
rows = list(csv.DictReader(open(sys.argv[1]), delimiter='\t'))
EV = ('PROJ', 'DUST', 'NPC', 'ITEM', 'COMBATTEXT', 'PARTICLE')
by = collections.OrderedDict()
for r in rows: by.setdefault((r['gear'], r['weapon']), []).append(r)
gears = collections.OrderedDict()
for (g, w) in by: gears.setdefault(g, []).append(w)
noisy = 0; hookrows = []; flagged = []
total = 0
for g, ws in gears.items():
    idle = [r for r in by.get((g, '(no weapon)'), []) if r['kind'] in EV and r['loc'] == 'CMD']
    idlekeys = set((r['kind'], r['name'], r['stack'][:80]) for r in idle)
    total += 1
    for w in ws:
        if w.startswith('(no weapon)'): continue
        t = by[(g, w)]
        new = [r for r in t if r['kind'] in EV and r['loc'] == 'CMD' and (r['kind'], r['name'], r['stack'][:80]) not in idlekeys and not r['stack'].startswith('ThoriumMod.ThoriumPlayer.PostUpdateEquips')]
        hooks = [r for r in t if r['kind'] == 'CMDHOOK' and 'commander' in r['name']]
        for r in hooks: hookrows.append((g, w, r['name'], r['tagged'], r['count']))
        if new: flagged.append((g, w, new))
print('gear entries:', total)
print('gear entries with a firing-soldier-only event at the commander:', len(set(f[0] for f in flagged)))
print('commander ModPlayer hook invocations from soldier fire:', len(hookrows), hookrows[:10])
for g, w, new in flagged[:60]:
    print(g, '|', w)
    for r in new[:3]: print('     ', r['kind'], r['name'][:30], 'n=' + r['count'], r['stack'][:170])
idle_with_events = [(g) for g in gears if any(r['kind'] in EV and r['loc'] == 'CMD' for r in by.get((g, '(no weapon)'), []))]
print('gear entries that spawn something at the commander while idle (no weapon):', len(idle_with_events), idle_with_events[:20])

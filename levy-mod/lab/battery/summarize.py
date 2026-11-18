"""Per-army summary from collect.py's fights.json: damage by weapon (share), death waves, what killed levies, engagement.
  summarize.py FIGHTS.json"""
import json, sys, collections, statistics
F = json.load(open(sys.argv[1]))
by = collections.defaultdict(list)
for f in F: by[(f['army'], f['boss'])].append(f)
for (army, boss), fs in sorted(by.items()):
    wd = collections.Counter(); secs = sum(f['sec'] for f in fs)
    for f in fs:
        for kv in f['weaponDmg'].split():
            k, _, v = kv.rpartition('='); wd[k.split('@')[0]] += int(v)
    tot = sum(wd.values()) or 1
    print(f'== {army} vs {boss}: {sum(f["result"] == "WIN" for f in fs)}/{len(fs)} wins, {secs:.0f} s fought')
    print('   weapons (share of boss damage): ' + ', '.join(f'{k.replace("CalamityMod/", "").replace("C:", "")} {100 * v / tot:.0f}%' for k, v in wd.most_common(10)))
    killers = collections.Counter(); waves = []
    for f in fs:
        rc = f.get('rec', {})
        for t, i, npc, src in rc.get('deaths', []): killers[src] += 1
        ts = sorted(t / 60 for t, *_ in rc.get('deaths', []))
        # biggest wave: most deaths inside any 1 s window
        best = max((sum(1 for u in ts if s <= u < s + 1.0), s) for s in ts) if ts else (0, 0)
        waves.append(f'{best[0]} at {best[1]:.1f}s')
        for src, v in rc.get('topHitSrc', [])[:2]: pass
    print('   deaths by source: ' + ', '.join(f'{k} {v}' for k, v in killers.most_common()) + ' | biggest 1-second death wave per fight: ' + '; '.join(waves))
    print('   not engaged %: ' + ', '.join(str(f.get('rec', {}).get('notEngagedPct')) for f in fs) + ' | >50 tiles from boss %: ' + ', '.join(str(f.get('rec', {}).get('farOver50tilesPct')) for f in fs)
          + ' | crowded %: ' + ', '.join(str(f.get('rec', {}).get('crowdedPct')) for f in fs) + ' | mean dist to boss: ' + ', '.join(str(f.get('rec', {}).get('meanDistToBoss')) for f in fs))
    print('   top hit sources: ' + ' | '.join(f"{a}={b}" for f in fs[:1] for a, b in f.get('rec', {}).get('topHitSrc', [])[:3]))

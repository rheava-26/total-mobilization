import csv, sys, re
# prints a comma list of weapon names that had an at-commander spawn / trajectory / hook in the given onhit tsv
rows = list(csv.DictReader(open(sys.argv[1]), delimiter='\t'))
names = set()
for r in rows:
    w = r['weapon']
    if w.startswith('(no weapon)'): continue
    if r['loc'] == 'CMD' and r['kind'] in ('PROJ', 'DUST', 'NPC', 'ITEM', 'COMBATTEXT', 'PARTICLE') and not r['stack'].startswith('ThoriumMod.ThoriumPlayer.PostUpdateEquips'): names.add(w)
    if r['kind'] == 'PROJ-TRAJ' and r['source'].startswith('reachedCmd'): names.add(w)
    if r['kind'] == 'CMDHOOK': names.add(w)
print(','.join(sorted(names)))

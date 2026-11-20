"""Gives an army file her levies' real names, by levy id, from the recording it was rebuilt from (the names table: rank and name, e.g. "Sgt. Alice").
  addnames.py ARMY.json REC.levyrec
The recordings carry no temperament, so only names are added. A levy without a name keeps the lab's default ("<army> <class> <n>")."""
import gzip, json, sys
army_f, rec = sys.argv[1:3]
names = {}
for line in gzip.open(rec, 'rt', errors='replace'):
    if '"k":"names"' in line:
        names = json.loads(line).get('soldiers', {}); break
a = json.load(open(army_f))
n = 0
for lv in a['levies']:
    nm = names.get(str(lv['id']))
    if nm: lv['name'] = nm; n += 1
json.dump(a, open(army_f, 'w'), indent=1)
print(army_f, n, 'of', len(a['levies']), 'named')

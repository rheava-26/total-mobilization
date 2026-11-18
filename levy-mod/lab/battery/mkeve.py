"""Reconstruct Eve's levy army from a .levyrec (format 10/11): the lgear baseline of every regular levy (id > 0).
  mkeve.py REC CALNAMES.json OUT.json
Item names: Calamity display names map to CalamityMod/<Internal> through the en-US localization; anything else is a vanilla display name.
Prefixes: vanilla ids (< 85) are written as the id, modded ones by name ("Name@Prefix")."""
import gzip, json, sys
rec, calf, outf = sys.argv[1:4]
cal = json.load(open(calf))
names = {}; lgear = {}; firstl = {}
for line in gzip.open(rec, 'rt', errors='replace'):
    try: o = json.loads(line)
    except Exception: continue
    if o['k'] == 'names': names = o
    elif o['k'] == 'e' and o['e'] == 'lgear': lgear.setdefault(o['id'], []).append((o['t'], o['g']))
    elif o['k'] == 'f':
        for l in o.get('l', []):
            if l[0] not in firstl: firstl[l[0]] = l
it = names['items']; px = names.get('prefixes', {})
VANILLA_PREFIX_COUNT = 85
CLS = {}  # weapon damage class, filled from item_tiers.tsv where known
for line in open('/home/user/total-mobilization/.claude/worktrees/agent-a018a87be4cc0046e/levy-mod/docs/boss_lab/battery_2026-11/item_tiers.tsv'):
    f = line.rstrip('\n').split('\t')
    if len(f) > 18 and f[0] in ('Vanilla', 'CalamityMod'): CLS[f[18]] = f[3]

def iname(t):
    dn = it.get(str(t), str(t))
    return ('CalamityMod/' + cal[dn]) if dn in cal else dn

def full(t, pre):
    n = iname(t)
    if pre:
        n += '@' + (str(pre) if pre < VANILLA_PREFIX_COUNT else px.get(str(pre), str(pre)))
    return n

def cls_of(t):
    c = CLS.get(it.get(str(t), ''), '')
    return {'melee': 'E', 'ranged': 'R', 'magic': 'M', 'summon': 'S'}.get(c, 'T' if 'Rogue' in c else 'R')

levies = []; notes = []
for lid in sorted(k for k in lgear if k > 0):
    t, g = lgear[lid][-1]   # the last baseline (gear at the end of the fight)
    s = {x[0]: x for x in g}
    lv = {'id': lid}
    w = s.get(0)
    if not w: notes.append(f'levy {lid}: no weapon'); continue
    lv['weapon'] = full(w[1], w[2]); lv['cls'] = cls_of(w[1]); lv['weaponName'] = it.get(str(w[1]))
    if 1 in s: lv['side'] = full(s[1][1], s[1][2])
    lv['armor'] = [iname(s[k][1]) for k in (2, 3, 4) if k in s]
    mob = [full(s[k][1], s[k][2]) for k in (13, 14) if k in s]
    acc = [full(s[k][1], s[k][2]) for k in range(5, 11) if k in s]
    lv['acc'] = mob + acc        # wings and boots first: the harness puts them in their own slots and counts only the rest against accSlots
    if 11 in s: lv['ammo'] = iname(s[11][1])
    if 12 in s: lv['hook'] = iname(s[12][1])
    life = firstl.get(lid, [0] * 11)[10]
    lv['lc'] = 15; lv['lf'] = max(0, int(round((life - 400) / 5))) if life >= 400 else 0; lv['recordedLifeMax'] = life
    lv['mc'] = 9; lv['potions'] = 8; lv['potion'] = 'Greater Healing Potion'
    levies.append(lv)
army = {'source': rec.split('/')[-1], 'note': 'Eve\'s 34 regular levies (ids 1-34) as worn at the end of the fight; officers and militia are not levies and are left out.',
        'accSlots': 2, 'honey': True, 'stations': ['campfire', 'heartlantern', 'starinabottle', 'sunflower', 'bast', 'sharpening', 'wartable', 'bewitching', 'ammobox', 'crystalball'],
        'stationLevel': 2, 'buffs': [], 'levies': levies, 'notes': notes}
json.dump(army, open(outf, 'w'), indent=1)
print(len(levies), 'levies', notes)

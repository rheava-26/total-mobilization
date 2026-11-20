"""Champions against regulars: N Champions and 3N Levies with the same weapons and armour, at the pre-Moon Lord and post-Providence tiers.
  mkchamp.py      -> champ<N>_<tier>.json and reg<3N>_<tier>.json for N = 3, 5; tier old (battery optimal_ranged) and prov (optimal_prov_ranged)
Weapons: both sides cycle through the tier's fight-tested optimal weapons in the same order (one of each, then again), so the mix matches.
Champions: grade Champion, 6 accessory slots (Eve's world: the Champion's 5 plus 1 milestone; the army file sets champSlots), the wings and boots slots, and
6 accessories: the regulars' pair plus Avenger Emblem, Frozen Shield, Charm of Myths and Star Veil. Regulars: grade Levy, their 2 accessories. Same HP, potions, hook, stations."""
import json
from pathlib import Path
H = Path(__file__).resolve().parent
TIERS = {'old': H.parent / 'battery' / 'optimal_ranged_army.json', 'prov': H / 'optimal_prov_ranged_army.json'}
EXTRA = ['Avenger Emblem@Menacing', 'Frozen Shield@Menacing', 'Charm of Myths@Menacing', 'Star Veil@Menacing']
for tier, f in TIERS.items():
    src = json.loads(f.read_text())
    kinds, seen = [], set()
    for lv in src['levies']:
        if lv['weapon'] not in seen: seen.add(lv['weapon']); kinds.append(lv)
    for n in (3, 5):
        for grade, count in (('C', n), ('L', 3 * n)):
            levies = []
            for i in range(count):
                b = dict(kinds[i % len(kinds)]); b['id'] = i + 1; b.pop('name', None)
                if grade == 'C': b['grade'] = 'C'; b['acc'] = b['acc'] + EXTRA
                levies.append(b)
            army = dict(src, levies=levies, champSlots=6, note=f'{"Champions" if grade == "C" else "Regulars"} x{count}, {tier} tier (mkchamp.py)')
            out = H / f'{"champ" if grade == "C" else "reg"}{count}_{tier}.json'
            out.write_text(json.dumps(army, indent=1)); print(out.name, len(levies))

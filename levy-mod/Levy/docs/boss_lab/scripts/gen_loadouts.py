import csv, re, sys
# builds docs/boss_lab/LOADOUTS.md from the harness tier dump (out/tiers.md) + Levy's weapon scores (scratchpad tiers_new.tsv)
S = '/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/tiers_new.tsv'
score = {}
for r in csv.DictReader(open(S), delimiter='\t'):
    score[(r['mod'], r['name'])] = (float(r['score']), r['route'], r['routeSrc'])
ACQ = {
    'StormSurge': 'Sea Prism/Navystone crafting (Sunken Sea), route 1.61',
    'Barinade': 'Desert Scourge drop (wiki: one of 5 weapons, 25-33% each)',
    'SandstreamScepter': 'Desert Scourge drop', 'SaharaSlicers': 'Desert Scourge drop', 'BrittleStarStaff': 'Desert Scourge drop',
    'AquamarineStaff': 'Navystone/Sea Prism crafting, route 1.61', 'SeashineSword': 'Navystone crafting, route 1.61', 'UrchinMace': 'Sea Remains crafting, route 1.6',
    'WulfrumProsthesis': 'Wulfrum enemies (surface), route 0.5', 'WulfrumController': 'Wulfrum enemies (surface), route 0.5', 'WulfrumScrewdriver': 'Wulfrum Metal crafting',
    'Fungicide': 'Crabulon drop (wiki: one of 6, 25-33%)', 'HyphaeRod': 'Crabulon drop', 'MycelialClaws': 'Crabulon drop', 'PuffShroom': 'Crabulon drop',
    'TeardropCleaver': 'Eye of Cthulhu drop (route 2.0)', 'DeathstareRod': 'Eye of Cthulhu drop (route 2.0)',
    'Shadethrower': 'Hive Mind drop (wiki: one of 3-4)', 'ShaderainStaff': 'Hive Mind drop', 'DankStaff': 'Hive Mind drop',
    'Eviscerator': 'The Perforators drop', 'BloodBath': 'The Perforators drop', 'SausageMaker': 'The Perforators drop', 'Aorta': 'The Perforators drop', 'FleshOfInfidelity': 'The Perforators drop',
    'Galeforce': 'Aerialite Bar crafting (route 4.5)', 'Tradewinds': 'Aerialite Bar crafting', 'WindBlade': 'Aerialite Bar crafting',
}
txt = open('/home/user/lab/out/tiers.md').read()
BOSS_TIER = [('King Slime, Desert Scourge', 0), ('Eye of Cthulhu', 2), ('Eater of Worlds, Brain of Cthulhu', 3), ('Queen Bee', 4), ('Skeletron', 5)]
out = ['# Boss lab loadouts: what each class gets per boss and why', '',
       'Generated from the harness tier data block (`CombatHarness.CalRevLab.cs`, `CrTiers()`) by `gen_loadouts.py`; the table below is what the harness prints at boot (`LEVY_CR_TIERDOC`).',
       'Tier = what is obtainable BEFORE that boss in Boss Checklist order with Calamity: King Slime 1, Desert Scourge 1.6, Eye 2, Crabulon 2.7, Eater/Brain 3, Hive Mind 3.98, Perforators 3.99, Queen Bee 4, Skeletron 5.',
       'Every Calamity name was verified with `ModContent.TryFind` at boot (`613 lookups OK, 0 unknown`). Unknown names would be logged and dropped, never fatal.', '',
       '## Which tier each boss gets', '']
for b, t in BOSS_TIER: out.append(f'- {b}: tier {t}')
out += ['', '## How a levy is rolled', '',
        '- Same seed gives the same army in every mode (boss and mode are not in the roll; the tier is).',
        '- Class split of N levies: 30% ranger (R), 30% mage (M), 20% melee (E), 20% summoner (S).',
        '- Weapon: weighted pick from the tier pool (vanilla options weight 1 each, the tier\'s best Calamity items weight 2-3). Duplicates allowed (crafted or farmed). Armor: uniform over the class\'s sets (class-specific Victide/Aerospec helmets). One accessory slot per Levy-grade levy (`grade=lv`, the default for `gear=mod`; the old `gear=van` rolls 35% Man-at-Arms with 3 slots at no command-point cost). Life crystals: tiers 0-2 roll 0-5, tiers 3+ have 5 (200 hp). Healing potions as before.',
        '- Weapon "why" columns: `score` = Levy `WeaponClassifier.Score` (from the gear-audit table), `acquisition` = wiki drop table (curl `?action=raw`) or Levy route source. Items with `unknown:rarity` route evidence were not used.', '']
cur = None
for line in txt.split('\n'):
    if line.startswith('## tier'):
        out.append(line.replace('## tier', '## Tier')); continue
    if line.startswith('why:') or line.startswith('life crystals'):
        out.append('- ' + line); continue
    m = re.match(r'- ([RMES]) weapons: (.*)', line)
    if m:
        cls = m.group(1)
        out.append(f'- **{cls} weapons**')
        for it in re.findall(r'([\w/\' ]+)\[w(\d)(?:; ammo ([^\]]*))?\]', m.group(2)):
            name, w, ammo = it; name = name.strip(' ,')
            short = name.replace('CalamityMod/', '')
            if name.startswith('CalamityMod/'):
                sc = score.get(('CalamityMod', short))
                out.append(f'  - {short} (w{w}{"; ammo " + ammo if ammo else ""}): score {sc[0]:.0f}; {ACQ.get(short, "")}' if sc else f'  - {short} (w{w}): {ACQ.get(short, "")}')
            else:
                out.append(f'  - {name} (w{w}{"; ammo " + ammo if ammo else ""}): vanilla')
        continue
    m = re.match(r'- ([RMES]) armor sets: (.*)', line)
    if m:
        out.append(f'- **{m.group(1)} armor**: ' + ', '.join(sorted(set(m.group(2).replace('CalamityMod/', '').split(' / ')))))
        continue
    if line.startswith('- accessory pool'):
        out.append('- **accessory pool** (one slot): ' + line.split(': ', 1)[1].replace('CalamityMod/', '')); out.append('')
open(sys.argv[1], 'w').write('\n'.join(out) + '\n')

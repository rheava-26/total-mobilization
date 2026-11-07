import re
fam = {}
for l in open('/home/user/lab/scratche/spa_server.log', errors='ignore'):
    m = re.search(r'weapon CalamityMod/(\w+): family=(\w+)', l)
    if m: fam[m.group(1)] = m.group(2)
melee = {'Broadsword', 'Shortsword', 'Spear', 'Yoyo', 'Flail', 'Whip'}
names = [n for n, f in fam.items() if f in melee] + ['Basher', 'UrchinMace', 'BallOFugu', 'YateveoBloom', 'SaharaSlicers', 'CosmicShiv', 'SubmarineShocker']
names = list(dict.fromkeys(names))
lines = [f"fight\tkslime\texp\t1\t1800\tgear=mod\tn=4\tdr=1\tbhp=40\tcmd=far\tclone=E\tcw=CalamityMod/{n}" for n in names]
open('/home/user/lab/specs_e/ks_melee.tsv', 'w').write('\n'.join(lines) + '\n')
print(len(lines))

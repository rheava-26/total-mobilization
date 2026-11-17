"""Calamity display name -> internal name map from the en-US localization (read-only reference source)."""
import glob, json, re, sys
D = '/home/user/tml-save-2026.10/ModSources/CalamityMod/Localization/en-US/'
out = {}
for fn in glob.glob(D + 'Mods.CalamityMod.Items.*.hjson'):
    key = None
    for line in open(fn, encoding='utf-8'):
        m = re.match(r'^(\w+): \{', line)
        if m: key = m.group(1); continue
        m = re.match(r'^\tDisplayName: (.+?)\s*$', line)
        if m and key:
            dn = m.group(1).strip().strip('"')
            out.setdefault(dn, key)
json.dump(out, open(sys.argv[1], 'w'), indent=0, sort_keys=True)
print(len(out), 'names')

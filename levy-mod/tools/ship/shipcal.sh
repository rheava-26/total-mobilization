#!/usr/bin/env bash
# Build Levy, Mechanization and Warbands on tModLoader 2026.10 into the PRIVATE Calamity ship install and boot it headless (no world).
#   shipcal.sh [--no-build]
# $SHIPCAL = a copy of tML 2026.10 with its own save/ (Mods: CalamityMod, CalamityModMusic, SerousCommonLib, MagicStorage, ThoriumMod
# + our three). It is never shared with a lab or another builder; the three tmods are built straight into it, third-party tmods are copied in.
# Enabled: CalamityModMusic, CalamityMod, Levy, Mechanization, Warbands (override: CAL_MODS="a,b,c").
# Reports: exc=N failN=N, the Levy and Mechanization version lines, the Calamity compat line count.
set -u
. "$(dirname "${BASH_SOURCE[0]}")/env.sh"
CAL_MODS=${CAL_MODS:-CalamityModMusic,CalamityMod,Levy,Mechanization,Warbands}
ensure_install "$TML10" "$SHIPCAL" || exit 1
mkdir -p "$SHIPCAL/save/Mods"
sync_thirdparty "$SAVE10/Mods" "$SHIPCAL/save/Mods" || exit 1

if [ "${1:-}" != "--no-build" ]; then
  echo "== build on tML 2026.10 (into $SHIPCAL/save/Mods) =="
  for m in "${SHIP_MODS[@]}"; do
    "$SHIP_DIR/build_mod.sh" -t 10 -i "$SHIPCAL" -s "$SHIPCAL/save" "$m" || { echo "BUILD FAILED: $m"; exit 1; }
  done
fi

echo "== boot ($CAL_MODS) =="
"$SHIP_DIR/tmlboot.sh" -i "$SHIPCAL" -s "$SHIPCAL/save" -m "$CAL_MODS" -p 7774 -q 10 -t 300 || echo "WARNING: boot did not reach the world menu (see $SHIPCAL/boot.out)"
L="$SHIPCAL/tModLoader-Logs/server.log"
[ -f "$L" ] || { echo "NO LOG"; exit 1; }
EXC=$(grep -c -E -i 'xception' "$L")
FAILN=$(grep -c -E 'fail=[1-9]|FAILURE|!!!' "$L")
COMPAT=$(grep -c -F 'Calamity targeting compat: CalamityTargeting hook installed' "$L")
echo "CALAMITY BOOT: exc=$EXC failN=$FAILN compat=$COMPAT"
grep -E 'Adding Content: (Levy|Mechanization|Warbands)' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | sed 's/^/  /'
grep -E 'Levy Calamity targeting compat' "$L" | sed -E 's/^\[[^]]*\] //' | head -2 | sed 's/^/  /'
grep -E 'fail=[1-9]|FAILURE|!!!' "$L" | cut -c1-220 | head -10 | sed 's/^/  FAIL: /'
grep -E -i 'xception' "$L" | cut -c1-220 | head -6 | sed 's/^/  EXC: /'

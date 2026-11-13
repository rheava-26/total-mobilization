#!/usr/bin/env bash
# The ship pipeline: build Levy, Mechanization and Warbands on the shipping toolchain (tML 2026.08), boot a vanilla headless
# server with all three enabled, then the Calamity build/boot (shipcal.sh) and the world checks (shipworld.sh).
#   shipboth.sh [--no-cal] [--no-world]
# 1. build on tML 2026.08 into $SAVE08/Mods (needs ThoriumMod + MagicStorage + SerousCommonLib there: setup_toolchain.sh)
# 2. copy (never link) the three tmods to $VERIFY_SAVE/Mods and boot the PRIVATE vanilla install $SHIPNORM against it. No world:
#    the boot stops at the world menu after "Finalizing Content" + the load-time self-checks.
# 3. shipcal.sh   4. shipworld.sh
# Boot report: exceptions (must be 0), the self-check summary lines, and every fail=[1-9] / FAILURE / !!! line.
set -u
. "$(dirname "${BASH_SOURCE[0]}")/env.sh"
T0=$SECONDS; NOCAL=0; NOWORLD=0
for a in "$@"; do case $a in --no-cal) NOCAL=1;; --no-world) NOWORLD=1;; esac; done

echo "######## 1/4 BUILD (tML 2026.08) ########"
for m in "${SHIP_MODS[@]}"; do
  "$SHIP_DIR/build_mod.sh" -t 08 "$m" || { echo "BUILD FAILED: $m"; exit 1; }
done
grep -H '^version' "$LEVY_SRC/build.txt" "$MECH_SRC/build.txt" "$WARB_SRC/build.txt" | sed -E 's#.*/([A-Za-z]+)/build.txt:#  \1 #'

echo "######## 2/4 VANILLA BOOT ########"
ensure_install "$TML08" "$SHIPNORM" || exit 1
mkdir -p "$VERIFY_SAVE/Mods"
sync_tmods "$SAVE08/Mods" "$VERIFY_SAVE/Mods" "${SHIP_MODS[@]}" || exit 1
"$SHIP_DIR/tmlboot.sh" -i "$SHIPNORM" -s "$VERIFY_SAVE" -m "Levy,Mechanization,Warbands" -p 7775 -q 12 -t 300 || echo "WARNING: boot did not reach the world menu (see $SHIPNORM/boot.out)"
L="$SHIPNORM/tModLoader-Logs/server.log"
if [ -f "$L" ]; then
  EXC=$(grep -c -E -i 'xception' "$L")
  echo "VANILLA BOOT: exc=$EXC"
  grep -E 'Adding Content: (Levy|Mechanization|Warbands)' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | sed 's/^/  /'
  echo "-- self-check summary lines:"
  grep -E 'self-check' "$L" | grep -E 'pass=|fail=|checks OK|checks ok|FAILURE| OK' | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-200 | sed 's/^/  /'
  echo "-- fail=[1-9] / FAILURE / !!! lines:"
  grep -E 'fail=[1-9]|FAILURE|!!!' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-220 | sed 's/^/  /'
  echo "-- exception lines:"
  grep -E -i 'xception' "$L" | cut -c1-220 | head -8 | sed 's/^/  /'
else echo "NO LOG at $L"; fi

[ $NOCAL = 1 ] || { echo "######## 3/4 CALAMITY (shipcal) ########"; "$SHIP_DIR/shipcal.sh"; }
[ $NOWORLD = 1 ] || { echo "######## 4/4 WORLD (shipworld) ########"; "$SHIP_DIR/shipworld.sh"; }
echo "######## shipboth done in $((SECONDS - T0))s ########"

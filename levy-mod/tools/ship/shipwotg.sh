#!/usr/bin/env bash
# OPTIONAL third ship configuration "wotg": Calamity + Wrath of the Gods + Luminance + Fargo's + Luiafk + Levy/Mechanization/Warbands on tML 2026.10.
#   shipwotg.sh [--no-build] [--world]        (WOTG_SOULS=1 adds Fargo's Souls)
# Never touches the vanilla and Calamity halves: it has its own private install $WOTG_HOME (tML copy + save/).
# Mods in $WOTG_HOME/save/Mods: everything from $SAVE10/Mods (Calamity set, copied) + the extra tmods in env.sh WOTG_TMODS (copied from $MODCACHE) + our three (built).
# Enabled (override WOTG_MODS="a,b,c"): CalamityModMusic,CalamityMod,Daybreak,Luminance,SubworldLibrary,NoxusBoss,Fargowiltas,miningcracks_take_on_luiafk,Levy,Mechanization,Warbands
# Reports: exceptions, FAIL lines (env.sh fail_lines), the Levy/Mechanization/Warbands load lines, the compat line. --world also runs the world checks (shipworld.sh with SHIPWORLD_HALVES=wot).
# Private-lab use: WOTG_HOME=/path WOTG_LAB=/path PORT_WOTG=8130 PORT_WWOT=8131 shipwotg.sh --world
set -u
. "$(dirname "${BASH_SOURCE[0]}")/env.sh"
DEF=CalamityModMusic,CalamityMod,Daybreak,Luminance,SubworldLibrary,NoxusBoss,Fargowiltas,miningcracks_take_on_luiafk
[ "${WOTG_SOULS:-0}" = 1 ] && DEF+=,FargowiltasSouls
WOTG_MODS=${WOTG_MODS:-$DEF,Levy,Mechanization,Warbands}
NOBUILD=0; WORLD=0
for a in "$@"; do case $a in --no-build) NOBUILD=1;; --world) WORLD=1;; esac; done

ensure_install "$TML10" "$WOTG_HOME" || exit 1
mkdir -p "$WOTG_HOME/save/Mods"
sync_thirdparty "$SAVE10/Mods" "$WOTG_HOME/save/Mods" || exit 1
EXTRA=("${WOTG_TMODS[@]}")
[ "${WOTG_SOULS:-0}" = 1 ] && EXTRA+=("FargowiltasSouls:$WOTG_SOULS_TMOD")
for e in "${EXTRA[@]}"; do
  f="${e#*:}"; [ -f "$f" ] || { echo "ERROR: $f missing (DepotDownloader into $MODCACHE: see env.sh WOTG_TMODS)"; exit 1; }
  copy_fresh "$f" "$WOTG_HOME/save/Mods/${e%%:*}.tmod"
done

if [ $NOBUILD = 0 ]; then
  echo "== build on tML 2026.10 (into $WOTG_HOME/save/Mods) =="
  for m in "${SHIP_MODS[@]}"; do
    "$SHIP_DIR/build_mod.sh" -t 10 -i "$WOTG_HOME" -s "$WOTG_HOME/save" "$m" || { echo "BUILD FAILED: $m"; exit 1; }
  done
fi

echo "== boot ($WOTG_MODS) =="
"$SHIP_DIR/tmlboot.sh" -i "$WOTG_HOME" -s "$WOTG_HOME/save" -m "$WOTG_MODS" -p "$PORT_WOTG" -q 12 -t 420 || echo "WARNING: boot did not reach the world menu (see $WOTG_HOME/boot.out)"
L="$WOTG_HOME/tModLoader-Logs/server.log"
[ -f "$L" ] || { echo "NO LOG"; exit 1; }
EXC=$(grep -c -E -i 'xception' "$L")
FAILN=$(fail_lines "$L" | wc -l)
COMPAT=$(grep -c -F 'Calamity targeting compat: CalamityTargeting hook installed' "$L")
echo "WOTG BOOT: exc=$EXC failN=$FAILN compat=$COMPAT"
grep -E 'Adding Content: (NoxusBoss|Luminance|Fargowiltas|miningcracks_take_on_luiafk|FargowiltasSouls|Levy|Mechanization|Warbands)' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | sed 's/^/  /'
grep -E 'Levy Calamity targeting compat' "$L" | sed -E 's/^\[[^]]*\] //' | head -2 | sed 's/^/  /'
fail_lines "$L" | cut -c1-220 | head -20 | sed 's/^/  FAIL: /'
grep -E -i 'xception' "$L" | cut -c1-220 | head -12 | sed 's/^/  EXC: /'
if [ $WORLD = 1 ]; then SHIPWORLD_HALVES=wot LAB="$WOTG_LAB" "$SHIP_DIR/shipworld.sh"; fi

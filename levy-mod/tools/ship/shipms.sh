#!/usr/bin/env bash
# OPTIONAL ship configuration "ms" (off by default; shipboth.sh runs it with SHIPMS=1): Calamity + Magic Storage + Levy/Mechanization/Warbands on tML 2026.10, headless, no world.
#   shipms.sh [--no-build]
# The Magic Storage half of Levy runs only when Magic Storage is loaded, so neither ship half covers it. Its boot self-check ("Levy Magic Storage self-check: N checks OK") builds a real in-memory network
# and runs Magic Storage's own craft code against an armory: the Armory Heart, orders, and crafting with armory racks ([MsCraft]).
# Own private install $MS_HOME (tML copy + save/); Mods: everything in $SAVE10/Mods (Calamity, MagicStorage, SerousCommonLib, copied) + our three (built).
# Enabled (override MS_MODS="a,b,c"): CalamityModMusic,CalamityMod,SerousCommonLib,MagicStorage,Levy,Mechanization,Warbands
# Private-lab use: MS_HOME=/path PORT_MS=8299 shipms.sh
set -u
. "$(dirname "${BASH_SOURCE[0]}")/env.sh"
MS_MODS=${MS_MODS:-CalamityModMusic,CalamityMod,SerousCommonLib,MagicStorage,Levy,Mechanization,Warbands}
NOBUILD=0
for a in "$@"; do case $a in --no-build) NOBUILD=1;; esac; done

ensure_install "$TML10" "$MS_HOME" || exit 1
mkdir -p "$MS_HOME/save/Mods"
sync_thirdparty "$SAVE10/Mods" "$MS_HOME/save/Mods" || exit 1
[ -f "$MS_HOME/save/Mods/MagicStorage.tmod" ] || { echo "ERROR: MagicStorage.tmod missing from $SAVE10/Mods (setup_toolchain.sh builds it)"; exit 1; }

if [ $NOBUILD = 0 ]; then
  echo "== build on tML 2026.10 (into $MS_HOME/save/Mods) =="
  for m in "${SHIP_MODS[@]}"; do
    "$SHIP_DIR/build_mod.sh" -t 10 -i "$MS_HOME" -s "$MS_HOME/save" "$m" || { echo "BUILD FAILED: $m"; exit 1; }
  done
fi

echo "== boot ($MS_MODS) =="
"$SHIP_DIR/tmlboot.sh" -i "$MS_HOME" -s "$MS_HOME/save" -m "$MS_MODS" -p "$PORT_MS" -q 12 -t 420 || echo "WARNING: boot did not reach the world menu (see $MS_HOME/boot.out)"
L="$MS_HOME/tModLoader-Logs/server.log"
[ -f "$L" ] || { echo "NO LOG"; exit 1; }
EXC=$(grep -c -E -i "$EXC_RE" "$L")
FAILN=$(fail_lines "$L" | wc -l)
MSOK=$(grep -c -E 'Levy Magic Storage self-check: [0-9]+ checks OK' "$L")
echo "MS BOOT: exc=$EXC failN=$FAILN magicstorage-check-ok=$MSOK"
grep -E 'Adding Content: (MagicStorage|Levy|Mechanization|Warbands)' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | sed 's/^/  /'
grep -E 'Levy Magic Storage self-check|Levy/MagicStorage compat|\[MsCraft\]' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-400 | sed 's/^/  /'
fail_lines "$L" | cut -c1-220 | head -20 | sed 's/^/  FAIL: /'
grep -E -i "$EXC_RE" "$L" | cut -c1-220 | head -12 | sed 's/^/  EXC: /'
RC=0; [ "$MSOK" -ge 1 ] && [ "$EXC" = 0 ] || RC=1

# In a world: the Crafting Interface's own Craft entry (CraftingGUI.Craft) after Magic Storage's refresh code, on a real Storage Heart tile ([MsWorldCraft]). Needs the ship world (shipworld.sh makes it).
W="$LAB/world/ShipWorld.wld"
if [ -f "$W" ]; then
  copy_fresh "$W" "$MS_HOME/MsWorld.wld"
  echo "== world craft ($MS_MODS, ShipWorld copy) =="
  "$SHIP_DIR/tmlboot.sh" -i "$MS_HOME" -s "$MS_HOME/save" -m "$MS_MODS" -w "$MS_HOME/MsWorld.wld" -p "$PORT_MSW" -e "LEVY_MSCRAFT_WORLD=1 LEVY_MSCRAFT_EXIT=1 ${MSW_ENV:-}" \
      -x '\[MsWorldCraft\] self-check pass=' -q 5 -t 600 || echo "WARNING: world boot ended early (see $MS_HOME/boot.out)"
  L="$MS_HOME/tModLoader-Logs/server.log"
  WP=$(grep -c -F '[MsWorldCraft] PASS ' "$L"); WF=$(grep -c -F '[MsWorldCraft] FAIL ' "$L"); WEXC=$(grep -c -E -i "$EXC_RE" "$L")
  echo "MS WORLD CRAFT: PASS=$WP FAIL=$WF exc=$WEXC"
  grep -E '\[MsWorldCraft\] (FAIL|OBSERVED|self-check)' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-400 | sed 's/^/  /'
  grep -E -i "$EXC_RE" "$L" | cut -c1-220 | head -8 | sed 's/^/  EXC: /'
  [ "$WP" -ge 1 ] && [ "$WF" = 0 ] || RC=1
else echo "MS WORLD CRAFT: skipped (no $W; run shipworld.sh first)"; fi
exit $RC

#!/usr/bin/env bash
# The Redemption check (OPTIONAL configuration, not part of shipboth.sh): Levy + Mod of Redemption and its five libraries on tML 2026.08, one headless boot with a world.
#   shiprede.sh [sweep]
#     default  : the world check (IdentityWorld LEVY_REDE_ALONE=1: the damage class issuable, a sample of weapons per route against the stand-in player) and the BossBugs part "redemption"
#     sweep    : the army sweep of every weapon of the mod (LEVY_ARMYSWEEP_MODS=Redemption): the "army row" table, reported not failed
# The tmods come from $MODCACHE/redemption/<tML version>/Redemption.tmod and $MODCACHE/redemption/deps/<workshop id>/<version>/<Lib>.tmod (DepotDownloader, see setup_toolchain.sh).
# Needs the shipworld's world ($LAB/world/ShipWorld.wld, made by shipworld.sh) and Levy built on 2026.08 into $SAVE08/Mods. Only code identifiers of the mod are logged.
set -u
. "$(dirname "${BASH_SOURCE[0]}")/env.sh"
MODE=${1:-check}
PORT=${PORT_REDE:-7779}
RED=$MODCACHE/redemption
LIBS=("deps/2760520795/2026.6/ParticleLibrary" "deps/2776422822/2026.6/CollisionLib" "deps/2785100219/2026.6/SubworldLibrary" "deps/2790924965/2026.3/StructureHelper" "deps/2988092558/2026.3/BetterDialogue")
[ -f "$RED/2026.6/Redemption.tmod" ] || { echo "no $RED/2026.6/Redemption.tmod (the mod is not cached)"; exit 1; }
[ -f "$LAB/world/ShipWorld.wld" ] || { echo "no $LAB/world/ShipWorld.wld: run shipworld.sh once"; exit 1; }
"$SHIP_DIR/build_mod.sh" -t 08 Levy || exit 1
RUN=$LAB/run-rede; rm -rf "$RUN"; mkdir -p "$RUN/save/Mods"
ensure_install "$TML08" "$RUN/tml" || exit 1
copy_fresh "$RED/2026.6/Redemption.tmod" "$RUN/save/Mods/Redemption.tmod"
for l in "${LIBS[@]}"; do copy_fresh "$RED/$l.tmod" "$RUN/save/Mods/$(basename "$l").tmod" || exit 1; done
copy_fresh "$SAVE08/Mods/Levy.tmod" "$RUN/save/Mods/Levy.tmod"
copy_fresh "$LAB/world/ShipWorld.wld" "$RUN/ShipWorld.wld"
ORDER="ParticleLibrary,CollisionLib,SubworldLibrary,StructureHelper,BetterDialogue,Redemption,Levy"
if [ "$MODE" = sweep ]; then
  ENVS="LEVY_SELFCHECK_WORLD=1 LEVY_MECHWORLD=0 LEVY_SUMMONWORLD=0 LEVY_ARMYSWEEP_ALONE=1 LEVY_ARMYSWEEP_MODS=Redemption"; PAT='army sweep: [0-9]+ weapons'
else
  ENVS="LEVY_SELFCHECK_WORLD=1 LEVY_MECHWORLD=0 LEVY_SUMMONWORLD=0 LEVY_REDE_ALONE=1"; PAT='\[IdentityWorld\] self-check pass='
fi
"$SHIP_DIR/tmlboot.sh" -i "$RUN/tml" -s "$RUN/save" -m "$ORDER" -w "$RUN/ShipWorld.wld" -p "$PORT" -e "$ENVS" -x "$PAT" -q 8 -t 1700
L="$RUN/tml/tModLoader-Logs/server.log"
if [ "$MODE" = sweep ]; then
  grep -F 'army sweep:' "$L" | sed -E 's/^[^]]*\] \[[^]]*\] //' | cut -c1-200
else
  grep -F '[IdentityWorld] ' "$L" | grep -E 'PASS|FAIL' | grep -F 'Redemption' | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-220
  grep -F '[IdentityWorld] self-check' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //'
  # the BossBugs part (chained by the summon world check): hold-fire states, the custom damage class, projectiles left behind
  "$SHIP_DIR/tmlboot.sh" -i "$RUN/tml" -s "$RUN/save" -m "$ORDER" -w "$RUN/ShipWorld.wld" -p "$PORT" -e "LEVY_SELFCHECK_WORLD=1 LEVY_MECHWORLD=0 LEVY_BOSSBUGS_ONLY=1 LEVY_BOSSBUGS_PART=redemption" -x '\[BossBugs\] self-check pass=' -q 8 -t 1700
  grep -F '[BossBugs] ' "$L" | grep -E 'FAIL|self-check' | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-220
fi
cp -f "$L" "$LAB/rede-$MODE.log" 2>/dev/null; rm -rf "$RUN"

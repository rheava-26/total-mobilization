#!/usr/bin/env bash
# quickcheck.sh REPO_DIR PART [MODS]   : build Levy from REPO_DIR on tML 2026.10 into the PRIVATE ship install, boot the ShipWorld copy with only the BossBugs world check (parts: calamity|shots|ml|wof|mlkill|killedge|all)
# prints the BossBugs lines and every exception line. Uses one tML process at a time.
set -u
REPO_DIR=$1; PART=${2:-all}
export REPO=$REPO_DIR
. "$(dirname "${BASH_SOURCE[0]}")/shipenv.sh"
. "$REPO_DIR/levy-mod/tools/ship/env.sh"
SHIP_DIR="$REPO_DIR/levy-mod/tools/ship"
CAL_MODS=${3:-CalamityModMusic,CalamityMod,Levy}
ensure_install "$TML10" "$SHIPCAL" || exit 1
mkdir -p "$SHIPCAL/save/Mods"
sync_thirdparty "$SAVE10/Mods" "$SHIPCAL/save/Mods" || exit 1
rm -f "$SHIPCAL/save/Mods/Levy.tmod"
"$SHIP_DIR/build_mod.sh" -t 10 -i "$SHIPCAL" -s "$SHIPCAL/save" Levy | tail -3; [ -f "$SHIPCAL/save/Mods/Levy.tmod" ] || { echo BUILD FAILED; exit 1; }
RUN=$LAB/qc-run; rm -rf "$RUN"; mkdir -p "$RUN"
copy_fresh "$LAB/world/ShipWorld.wld" "$RUN/ShipWorld.wld"
"$SHIP_DIR/tmlboot.sh" -i "$SHIPCAL" -s "$SHIPCAL/save" -m "$CAL_MODS" -w "$RUN/ShipWorld.wld" -p "$PORT_WCAL" \
   -e "LEVY_SELFCHECK_WORLD=1 LEVY_BOSSBUGS_ONLY=1 LEVY_BOSSBUGS_PART=$PART" -x '\[BossBugs\] self-check pass=' -q 6 -t 600
L="$SHIPCAL/tModLoader-Logs/server.log"
cp "$L" "$RUN/server.log" 2>/dev/null
grep -F '[BossBugs]' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-300
echo "--- exceptions:"; grep -c -E -i 'xception' "$L"; grep -E -i 'xception' "$L" | cut -c1-200 | head -8

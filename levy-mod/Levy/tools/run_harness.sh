#!/usr/bin/env bash
# Runs the Levy combat harness on a scratch copy of the test world. Measurement only.
# Usage:  tools/run_harness.sh [-o out.tsv] [-f filter] [-m ModName] [-t runTicks] [-s k/n] [-r 10,25,40] [-c] [-T timeoutSec] [-p port]
#   -f  comma list of substrings of item name/internal name (quick subset)     -m  mod whose weapons are tested (default ThoriumMod)
#   -c  skip the vanilla controls     -s  shard k/n     -r  ranges in tiles     -t  run ticks per range (default 360)
# Build the mod first:  /home/user/tools/tmlbuild.sh <worktree>/levy-mod/Levy
# NOTE: enabled.json is shared and rewritten, so run ONE harness at a time (shards share the same Mods dir config, so
# start them all within the same script call: see tools/run_harness_sharded.sh).
set -u
OUT=""; FILTER=""; MOD="ThoriumMod"; TICKS=""; SHARD=""; RANGES=""; SKIPC=""; TMO=14400; PORT=7777; KEEPCFG=""
while getopts "o:f:m:t:s:r:cT:p:k" o; do case $o in
  o) OUT=$OPTARG;; f) FILTER=$OPTARG;; m) MOD=$OPTARG;; t) TICKS=$OPTARG;; s) SHARD=$OPTARG;; r) RANGES=$OPTARG;; c) SKIPC=1;; T) TMO=$OPTARG;; p) PORT=$OPTARG;; k) KEEPCFG=1;;
esac; done
TML=/home/user/tml-bin/tml
DATA=/root/.local/share/Terraria/tModLoader
# LEVY_TML_SAVE=<dir>: use a PRIVATE tModLoader save dir (Mods/, Worlds/ under it; passed as -tmlsavedirectory) so concurrent builders never share a Mods dir.
# Needs <dir>/Mods/{Levy,<mod>}.tmod and a copy of LevyTest.wld/.twld in <dir>/Worlds. Mechanization is not enabled in this mode.
SAVEARG=""; MODS_EXTRA=",\"Mechanization\""
if [ -n "${LEVY_TML_SAVE:-}" ]; then DATA=$LEVY_TML_SAVE; SAVEARG="-tmlsavedirectory $LEVY_TML_SAVE"; MODS_EXTRA=""; mkdir -p "$DATA/Worlds"; [ -f "$DATA/Worlds/LevyTest.wld" ] || cp /root/.local/share/Terraria/tModLoader/Worlds/LevyTest.wld /root/.local/share/Terraria/tModLoader/Worlds/LevyTest.twld "$DATA/Worlds/"; fi
WORLDS=$DATA/Worlds
ENABLED=$DATA/Mods/enabled.json
RUN=$(date +%s)_$$
SCR=${HARNESS_SCRATCH:-/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/harness}
mkdir -p "$SCR"
[ -z "$OUT" ] && OUT=$SCR/run_$RUN.tsv
WNAME=HarnessScratch_$RUN
cp "$WORLDS/LevyTest.wld" "$WORLDS/$WNAME.wld" && cp "$WORLDS/LevyTest.twld" "$WORLDS/$WNAME.twld" || { echo "cannot copy test world"; exit 1; }
if [ -z "$KEEPCFG" ]; then echo "[\"Levy\"$MODS_EXTRA,\"$MOD\"]" > "$ENABLED"; fi
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH
export LEVY_HARNESS=1 LEVY_HARNESS_OUT="$OUT" LEVY_HARNESS_MOD="$MOD"
[ -n "$FILTER" ] && export LEVY_HARNESS_FILTER="$FILTER"
[ -n "$TICKS" ] && export LEVY_HARNESS_TICKS="$TICKS"
[ -n "$SHARD" ] && export LEVY_HARNESS_SHARD="$SHARD"
[ -n "$RANGES" ] && export LEVY_HARNESS_RANGES="$RANGES"
[ -n "$SKIPC" ] && export LEVY_HARNESS_SKIPCONTROLS=1
LOG=$SCR/server_$RUN.log
cd "$TML" || exit 1
echo "world copy: $WORLDS/$WNAME.wld   log: $LOG   out: $OUT"
timeout "$TMO" dotnet tModLoader.dll -server -nosteam $SAVEARG -port "$PORT" -world "$WORLDS/$WNAME.wld" > "$LOG" 2>&1 < /dev/null
RC=$?
if [ -z "$KEEPCFG" ]; then echo "[\"Levy\"$MODS_EXTRA]" > "$ENABLED"; fi
rm -f "$WORLDS/$WNAME".wld* "$WORLDS/$WNAME".twld*
echo "server exit=$RC (124 = timed out)"
echo "enabled.json: $(cat "$ENABLED")"
echo "LOG: $LOG"
echo "TSV: $OUT"
[ -f "$OUT" ] && echo "rows: $(($(wc -l < "$OUT") - 1))"
exit 0

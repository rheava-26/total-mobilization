#!/usr/bin/env bash
# Runs the Levy combat harness on a scratch copy of the test world. Measurement only.
# Usage:  tools/run_harness.sh [-N] [-o out.tsv] [-f filter] [-m ModName] [-t runTicks] [-s k/n] [-r 4,7,15,25,40] [-C classes] [-A armor,pieces] [-c] [-T timeoutSec] [-p port] [-k]
#   -N  use the NEW toolchain (tModLoader 2026.10, save dir /home/user/tml-save-2026.10); default is the old one (/home/user/tml-bin/tml, shared Mods dir)
#   -f  comma list of substrings of item name/internal name (quick subset)     -m  mod whose weapons are tested (default ThoriumMod)
#   -C  comma list of damage-class groups to keep (rogue,melee,ranged,magic,summon,throwing,other)
#   -A  comma list of ModItem internal names (in the tested mod) worn by BOTH the soldier and the player baseline
#   -c  skip the vanilla controls     -s  shard k/n     -r  ranges in tiles     -t  run ticks per range (default 360)
#   -k  keep enabled.json as it is (the sharded wrapper sets it once)
# Build the mod first:  old: /home/user/tools/tmlbuild.sh <worktree>/levy-mod/Levy     new (-N): /home/user/tools/tmlbuild10.sh <worktree>/levy-mod/Levy
# Enabled list written: old = Levy,Mechanization,<mod>;  new = <mod>[+CalamityModMusic first when the mod is CalamityMod],Levy,Mechanization. The previous enabled.json is restored afterwards.
# NOTE: enabled.json is shared per toolchain, so run ONE harness at a time (shards: see tools/run_harness_sharded.sh).
set -u
OUT=""; FILTER=""; MOD="ThoriumMod"; TICKS=""; SHARD=""; RANGES=""; SKIPC=""; TMO=14400; PORT=7777; KEEPCFG=""; NEW=""; CLASSES=""; ARMOR=""; SETUP=""
while getopts "o:f:m:t:s:r:cT:p:kNC:A:u:" o; do case $o in
  o) OUT=$OPTARG;; f) FILTER=$OPTARG;; m) MOD=$OPTARG;; t) TICKS=$OPTARG;; s) SHARD=$OPTARG;; r) RANGES=$OPTARG;; c) SKIPC=1;; T) TMO=$OPTARG;; p) PORT=$OPTARG;; k) KEEPCFG=1;; N) NEW=1;; C) CLASSES=$OPTARG;; A) ARMOR=$OPTARG;; u) SETUP=$OPTARG;;
esac; done
if [ -n "$NEW" ]; then
  TML=${HARNESS_TML:-/home/user/tml-bin/tml-2026.10}   # override both for a PRIVATE install + save dir (no collisions with other agents)
  DATA=${HARNESS_DATA:-/home/user/tml-save-2026.10}
  EXTRA=(-tmlsavedirectory "$DATA")
  if [ "$MOD" = "CalamityMod" ]; then LIST="[\"CalamityModMusic\",\"CalamityMod\",\"Levy\",\"Mechanization\"]"; else LIST="[\"$MOD\",\"Levy\",\"Mechanization\"]"; fi
  LOGF=$TML/tModLoader-Logs/server.log
else
  TML=${HARNESS_TML:-/home/user/tml-bin/tml}
  DATA=${HARNESS_DATA:-/root/.local/share/Terraria/tModLoader}
  EXTRA=(); [ -n "${HARNESS_DATA:-}" ] && EXTRA=(-tmlsavedirectory "$DATA")   # private save dir for the old toolchain
  LIST="[\"Levy\",\"Mechanization\",\"$MOD\"]"
  LOGF=$TML/tModLoader-Logs/server.log
fi
# LEVY_TML_SAVE=<dir>: use a PRIVATE tModLoader save dir (Mods/, Worlds/ under it; passed as -tmlsavedirectory) so concurrent builders never share a Mods dir.
# Needs <dir>/Mods/{Levy,<mod>}.tmod (and Mechanization is NOT enabled in this mode). The test world is copied in if missing.
if [ -n "${LEVY_TML_SAVE:-}" ]; then
  DATA=$LEVY_TML_SAVE; EXTRA=(-tmlsavedirectory "$DATA"); mkdir -p "$DATA/Worlds" "$DATA/Mods"
  LIST=${LIST/,\"Mechanization\"/}
fi
WORLDS=$DATA/Worlds
SRCWORLDS=/root/.local/share/Terraria/tModLoader/Worlds   # the one test world lives here for both toolchains
ENABLED=$DATA/Mods/enabled.json
RUN=$(date +%s)_$$
SCR=${HARNESS_SCRATCH:-/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/harness}
mkdir -p "$SCR" "$WORLDS"
[ -z "$OUT" ] && OUT=$SCR/run_$RUN.tsv
WNAME=HarnessScratch_$RUN
cp "$SRCWORLDS/LevyTest.wld" "$WORLDS/$WNAME.wld" && cp "$SRCWORLDS/LevyTest.twld" "$WORLDS/$WNAME.twld" || { echo "cannot copy test world"; exit 1; }
PREV=$(cat "$ENABLED" 2>/dev/null || echo '["Levy","Mechanization"]')
if [ -z "$KEEPCFG" ]; then echo "$LIST" > "$ENABLED"; fi
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH
export LEVY_HARNESS=1 LEVY_HARNESS_OUT="$OUT" LEVY_HARNESS_MOD="$MOD"
[ -n "$FILTER" ] && export LEVY_HARNESS_FILTER="$FILTER"
[ -n "$TICKS" ] && export LEVY_HARNESS_TICKS="$TICKS"
[ -n "$SHARD" ] && export LEVY_HARNESS_SHARD="$SHARD"
[ -n "$RANGES" ] && export LEVY_HARNESS_RANGES="$RANGES"
[ -n "${SETUP:-}" ] && export LEVY_HARNESS_SETUP="$SETUP"
[ -n "$CLASSES" ] && export LEVY_HARNESS_CLASS="$CLASSES"
[ -n "$ARMOR" ] && export LEVY_HARNESS_ARMOR="$ARMOR"
[ -n "$SKIPC" ] && export LEVY_HARNESS_SKIPCONTROLS=1
LOG=$SCR/server_$RUN.log
cd "$TML" || exit 1
echo "world copy: $WORLDS/$WNAME.wld   log: $LOG   out: $OUT   toolchain: ${NEW:+NEW}${NEW:-OLD}   enabled: $LIST"
timeout "$TMO" dotnet tModLoader.dll -server -nosteam "${EXTRA[@]}" -port "$PORT" -world "$WORLDS/$WNAME.wld" > "$LOG" 2>&1 < /dev/null
RC=$?
if [ -z "$KEEPCFG" ]; then printf '%s' "$PREV" > "$ENABLED"; fi
rm -f "$WORLDS/$WNAME".wld* "$WORLDS/$WNAME".twld*
echo "server exit=$RC (124 = timed out)"
echo "enabled.json: $(cat "$ENABLED" | tr -d '\n ')"
echo "LOG: $LOG   (tML log: $LOGF)"
echo "TSV: $OUT"
[ -f "$OUT" ] && echo "rows: $(($(wc -l < "$OUT") - 1))"
exit 0

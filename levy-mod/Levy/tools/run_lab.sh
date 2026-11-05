#!/usr/bin/env bash
# Runs the OWNER LAB (Common/Testing/OwnerLab.cs; feasibility study, measurement only) in a PRIVATE tModLoader save dir.
# Usage: tools/run_lab.sh -s '<spec>' [-o out.tsv] [-m ThoriumMod|none] [-p port] [-T timeoutSec] [-S saveDir]
#   spec: ';'-separated  kind|item|N|ticks|flags   (see OwnerLab.cs header)
# Never touches the shared Mods dir. Build first with:  dotnet tModLoader.dll -server -nosteam -tmlsavedirectory $SAVE -build <Levy dir>
set -u
SPEC=""; OUT=""; MOD="ThoriumMod"; PORT=7821; TMO=1800; SAVE=/home/user/tml-save-owner
while getopts "s:o:m:p:T:S:" o; do case $o in s) SPEC=$OPTARG;; o) OUT=$OPTARG;; m) MOD=$OPTARG;; p) PORT=$OPTARG;; T) TMO=$OPTARG;; S) SAVE=$OPTARG;; esac; done
TML=/home/user/tml-bin/tml
SRC=/root/.local/share/Terraria/tModLoader/Worlds
SCR=${LAB_SCRATCH:-/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/lab}
mkdir -p "$SCR" "$SAVE/Worlds" "$SAVE/Mods"
RUN=$(date +%s)_$$
[ -z "$OUT" ] && OUT=$SCR/lab_$RUN.tsv
W=LabScratch_$RUN
cp "$SRC/LevyTest.wld" "$SAVE/Worlds/$W.wld" && cp "$SRC/LevyTest.twld" "$SAVE/Worlds/$W.twld" || { echo "cannot copy world"; exit 1; }
if [ "$MOD" = none ]; then echo '["Levy"]' > "$SAVE/Mods/enabled.json"; else echo "[\"Levy\",\"$MOD\"]" > "$SAVE/Mods/enabled.json"; fi
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH
export LEVY_HARNESS=1 LEVY_HARNESS_OUT="$OUT" LEVY_HARNESS_MOD="$MOD" LEVY_HARNESS_LAB="$SPEC" LEVY_HARNESS_TICKS=120
LOG=$SCR/server_$RUN.log
cd "$TML" || exit 1
echo "log: $LOG  out: $OUT"
timeout "$TMO" dotnet tModLoader.dll -server -nosteam -tmlsavedirectory "$SAVE" -port "$PORT" -world "$SAVE/Worlds/$W.wld" > "$LOG" 2>&1 < /dev/null
RC=$?
rm -f "$SAVE/Worlds/$W".wld* "$SAVE/Worlds/$W".twld*
echo "server exit=$RC (124 = timed out, 7 = lab watchdog HANG)"
[ -f "$OUT" ] && echo "rows: $(($(wc -l < "$OUT") - 1))"
[ -f "$OUT.HANG" ] && cat "$OUT.HANG"
exit 0

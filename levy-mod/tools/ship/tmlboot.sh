#!/usr/bin/env bash
# Boot a tModLoader headless dedicated server, wait for a condition, stop it. The log lands in <install>/tModLoader-Logs/server.log
# and the console in <install>/boot.out. Always use a PRIVATE install (see env.sh) so nothing else overwrites the log.
#
#   tmlboot.sh -i INSTALL -s SAVE [-m "Mod1,Mod2"] [-w WORLD.wld] [-p PORT] [-e "VAR=1 VAR2=2"]
#              [-x REGEX] [-q SECONDS] [-t TIMEOUT] [-- extra tML args]
#     -m  write SAVE/Mods/enabled.json with these mods (load order = list order); omit to keep the existing file
#     -w  load this world (default: no world, boot stops at the world menu)
#     -x  wait until REGEX appears in the log or console (default: the mod load finished and the world menu is up)
#     -q  after that, wait until the log has been still for this long (default 8 s; boot self-checks run on after the load)
#     -t  give up after this many seconds (default 300)
# exit: 0 condition met, 1 timed out, 2 server exited early (mod load failure; see boot.out / server.log)
set -u
. "$(dirname "${BASH_SOURCE[0]}")/env.sh"
INSTALL=; SAVE=; MODS=; WORLD=; PORT=7777; ENVS=; PAT=; QUIET=8; TMO=300
while getopts "i:s:m:w:p:e:x:q:t:" o; do case $o in
  i) INSTALL=$OPTARG;; s) SAVE=$OPTARG;; m) MODS=$OPTARG;; w) WORLD=$OPTARG;; p) PORT=$OPTARG;;
  e) ENVS=$OPTARG;; x) PAT=$OPTARG;; q) QUIET=$OPTARG;; t) TMO=$OPTARG;; *) exit 64;; esac; done
shift $((OPTIND-1))
[ -f "$INSTALL/tModLoader.dll" ] || { echo "tmlboot: $INSTALL is not a tML install"; exit 64; }
mkdir -p "$SAVE/Mods"
if [ -n "$MODS" ]; then IFS=, read -r -a ML <<< "$MODS"; write_enabled "$SAVE/Mods" "${ML[@]}"; fi

use_dotnet8
LOG="$INSTALL/tModLoader-Logs/server.log"; OUT="$INSTALL/boot.out"
rm -rf "$INSTALL/tModLoader-Logs"; : > "$OUT"
FIFO="$(mktemp -u "${TMPDIR:-/tmp}/tmlboot.XXXXXX")"; mkfifo "$FIFO"
ARGS=(-server -nosteam -tmlsavedirectory "$SAVE" -port "$PORT" -maxplayers 1)
[ -n "$WORLD" ] && ARGS+=(-world "$WORLD")
cd "$INSTALL" || exit 64
# the fifo is held open by a sleeper so the server never sees EOF on stdin; "exit" is written to it at the end
( sleep $((TMO+60)) > "$FIFO" ) & HOLD=$!; disown $HOLD
# shellcheck disable=SC2086
env $ENVS setsid dotnet tModLoader.dll "${ARGS[@]}" "$@" < "$FIFO" > "$OUT" 2>&1 &
SRV=$!; disown $SRV
cleanup() { kill -9 -- -"$SRV" 2>/dev/null; kill -9 "$SRV" "$HOLD" 2>/dev/null; rm -f "$FIFO"; }
trap cleanup EXIT

seen() { # $1 = regex
  grep -q -E -e "$1" "$OUT" 2>/dev/null || { [ -f "$LOG" ] && grep -q -E -e "$1" "$LOG" 2>/dev/null; }
}
rc=1; t0=$SECONDS
while [ $((SECONDS - t0)) -lt "$TMO" ]; do
  if ! kill -0 "$SRV" 2>/dev/null; then rc=2; break; fi
  if [ -n "$PAT" ]; then seen "$PAT" && { rc=0; break; }
  elif [ -z "$WORLD" ]; then seen 'Mod Load Completed' && grep -q -E 'New World|Choose World' "$OUT" && { rc=0; break; }
  else seen 'Server started|Listening on port' && { rc=0; break; }; fi
  sleep 1
done
if [ $rc = 0 ] && [ "$QUIET" -gt 0 ]; then
  last=-1; still=0
  while [ $((SECONDS - t0)) -lt "$TMO" ] && kill -0 "$SRV" 2>/dev/null; do
    sz=$(cat "$LOG" "$OUT" 2>/dev/null | wc -c)
    if [ "$sz" = "$last" ]; then still=$((still+1)); else still=0; last=$sz; fi
    [ "$still" -ge "$QUIET" ] && break
    sleep 1
  done
fi
echo "tmlboot: rc=$rc after $((SECONDS - t0))s (install=$INSTALL)"
kill -0 "$SRV" 2>/dev/null && { echo exit > "$FIFO" & sleep 6; }
exit $rc

#!/usr/bin/env bash
# usage: qr.sh <slot r|s> <port> <specname...>   runs /home/user/lab/specs2/<name>.tsv -> out2/<name>.tsv in order, retrying (the harness skips finished keys) up to 4 times if the server dies.
# Recordings go to /home/user/lab/rec2/<name>/ ; specs named spreadoff* run with LEVY_SPREAD=0.
SLOT=$1; PORT=$2; shift 2
H=/home/user/lab
for N in "$@"; do
  export LEVY_REC_DIR=$H/rec2/$N; mkdir -p "$LEVY_REC_DIR"
  case "$N" in spreadoff*) export LEVY_SPREAD=0;; *) unset LEVY_SPREAD;; esac
  WANT=$(grep -c . $H/specs2/$N.tsv)
  for TRY in 1 2 3 4; do
    HAVE=$(( $(cat $H/out2/$N.tsv 2>/dev/null | wc -l) - 1 )); [ "$HAVE" -ge "$WANT" ] && break
    SLOT=$SLOT bash /home/user/total-mobilization/.claude/worktrees/agent-a99f9c13d1a134264/levy-mod/Levy/docs/boss_lab/scripts/runr.sh $H/specs2/$N.tsv $H/out2/$N.tsv 9000 $PORT >> $H/out2/$N.log 2>&1
  done
done

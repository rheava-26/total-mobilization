#!/usr/bin/env bash
# usage: rune.sh <spec.tsv> <out.tsv> [timeoutSec] [port]   -- PRIVATE tmle/savee (Levy.tmod built there by builde.sh)
SPEC=${1:?spec}; OUT=${2:?out}; TMO=${3:-3600}; PORT=${4:-7795}
H=/home/user/lab
W=/home/user/total-mobilization/.claude/worktrees/agent-ae27987db61f1c9b4
export LEVY_HARNESS_SCENARIO=${LEVY_HARNESS_SCENARIO:-calrev} LEVY_CR_SPEC="$SPEC" LEVY_CR_OUT="$OUT"
export HARNESS_TML=$H/tmle HARNESS_DATA=$H/savee HARNESS_SCRATCH=$H/scratche
unset LEVY_TML_SAVE
mkdir -p $HARNESS_SCRATCH
cd $W
bash $W/levy-mod/Levy/tools/run_harness.sh -N -m CalamityMod -p "$PORT" -T "$TMO" -o $H/scratche/harness_dummy.tsv
ls -t $H/scratche/server_*.log 2>/dev/null | tail -n +4 | xargs -r rm -f

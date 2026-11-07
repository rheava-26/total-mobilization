#!/usr/bin/env bash
# usage: runx.sh <spec.tsv> <out.tsv> [timeoutSec] [port]
SPEC=${1:?spec}; OUT=${2:?out}; TMO=${3:-3600}; PORT=${4:-7796}
H=/home/user/lab
export LEVY_HARNESS_SCENARIO=${LEVY_HARNESS_SCENARIO:-calrev} LEVY_CR_SPEC="$SPEC" LEVY_CR_OUT="$OUT" LEVY_CR_SP=${LEVY_CR_SP:-1}
export HARNESS_TML=$H/tmldud${SLOT:-} HARNESS_DATA=$H/savedud${SLOT:-} HARNESS_SCRATCH=$H/scratchdud${SLOT:-}
unset LEVY_TML_SAVE
mkdir -p $HARNESS_SCRATCH
cd /home/user/total-mobilization/.claude/worktrees/agent-a23640dd34068e3b6
bash /home/user/total-mobilization/.claude/worktrees/agent-a23640dd34068e3b6/levy-mod/Levy/tools/run_harness.sh -N -m CalamityMod -p "$PORT" -T "$TMO" -o $H/scratchdud${SLOT:-}/harness_dummy${SLOT:-}.tsv
ls -t $H/scratchdud${SLOT:-}/server_*.log 2>/dev/null | tail -n +4 | xargs -r rm -f

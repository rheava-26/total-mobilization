#!/usr/bin/env bash
# usage: run.sh <spec name>  -> out/<spec>.tsv (+ .pl.tsv), rec/<spec>/
N=$1; H=/home/user/lab; P=/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/pl
export LEVY_FEARMODEL=events LEVY_SURV=formation
export LEVY_REC_DIR=$P/rec/$N; mkdir -p $LEVY_REC_DIR
export LEVY_HARNESS_SCENARIO=calrev LEVY_CR_SPEC=$P/specs/$N.tsv LEVY_CR_OUT=$P/out/$N.tsv LEVY_CR_SP=1
export LEVY_CR_EXCLUDE=CalamityMod/UrchinMace,CalamityMod/SaharaSlicers
export HARNESS_TML=$H/tmlpd HARNESS_DATA=$H/savepd HARNESS_SCRATCH=$H/scratchpd
unset LEVY_TML_SAVE
mkdir -p $HARNESS_SCRATCH
cd /home/user/total-mobilization/.claude/worktrees/agent-aeeafc0c42316c22d
WANT=$(grep -c . $P/specs/$N.tsv)
for TRY in 1 2 3 4 5 6; do
  HAVE=$(( $(cat $LEVY_CR_OUT 2>/dev/null | wc -l) - 1 )); [ "$HAVE" -ge "$WANT" ] && break
  bash levy-mod/Levy/tools/run_harness.sh -N -m CalamityMod -p 7861 -T 9000 -o $H/scratchpd/harness_dummy.tsv >> $P/out/$N.log 2>&1
done

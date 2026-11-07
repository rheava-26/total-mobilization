#!/usr/bin/env bash
# usage: runo.sh <slot o|o2> <spec.tsv> <out.tsv> [timeoutSec] [port]   -- PRIVATE tmlo/saveo (slot o) or tmlo2/saveo2 (slot o2). Env passes through (LEVY_REC_DIR, LEVY_CR_*, LEVY_FEARMODEL, LEVY_SURV ...)
SLOT=${1:?slot}; SPEC=${2:?spec}; OUT=${3:?out}; TMO=${4:-9000}; PORT=${5:-7821}
H=/home/user/lab
W=/home/user/total-mobilization/.claude/worktrees/agent-ad11f62609ea192e9
export LEVY_HARNESS_SCENARIO=${LEVY_HARNESS_SCENARIO:-calrev} LEVY_CR_SPEC="$SPEC" LEVY_CR_OUT="$OUT"
export LEVY_CR_EXCLUDE=${LEVY_CR_EXCLUDE-CalamityMod/UrchinMace,CalamityMod/SaharaSlicers}
export LEVY_CR_SP=${LEVY_CR_SP-1}
export HARNESS_TML=$H/tml$SLOT HARNESS_DATA=$H/save$SLOT HARNESS_SCRATCH=$H/scratch$SLOT
unset LEVY_TML_SAVE
mkdir -p $HARNESS_SCRATCH
cd $W
bash $W/levy-mod/Levy/tools/run_harness.sh -N -m CalamityMod -p "$PORT" -T "$TMO" -o $H/scratch$SLOT/harness_dummy.tsv
ls -t $H/scratch$SLOT/server_*.log 2>/dev/null | tail -n +4 | xargs -r rm -f

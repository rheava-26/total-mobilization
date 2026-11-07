#!/usr/bin/env bash
# usage: runr.sh <spec.tsv> <out.tsv> [timeoutSec] [port]   -- PRIVATE tmlr/saver (lab 2). Env passes through: LEVY_REC_DIR, LEVY_SPREAD, LEVY_CR_EXCLUDE ...
# Calamity weapons that deal 0 damage from levies (dud-weapons branch) are excluded from the loadouts unless LEVY_CR_EXCLUDE is set otherwise.
SPEC=${1:?spec}; OUT=${2:?out}; TMO=${3:-9000}; PORT=${4:-7801}
H=/home/user/lab; SLOT=${SLOT:-r}   # SLOT=r (tmlr/saver) or s (tmls/saves, a copy for the 2nd server)
W=${LEVY_WT:-/home/user/total-mobilization/.claude/worktrees/agent-a99f9c13d1a134264}
export LEVY_HARNESS_SCENARIO=${LEVY_HARNESS_SCENARIO:-calrev} LEVY_CR_SPEC="$SPEC" LEVY_CR_OUT="$OUT"
export LEVY_CR_EXCLUDE=${LEVY_CR_EXCLUDE-CalamityMod/UrchinMace,CalamityMod/SaharaSlicers}
export HARNESS_TML=$H/tml$SLOT HARNESS_DATA=$H/save$SLOT HARNESS_SCRATCH=$H/scratch$SLOT
unset LEVY_TML_SAVE
mkdir -p $HARNESS_SCRATCH
cd $W
bash $W/levy-mod/Levy/tools/run_harness.sh -N -m CalamityMod -p "$PORT" -T "$TMO" -o $H/scratch$SLOT/harness_dummy.tsv
ls -t $H/scratch$SLOT/server_*.log 2>/dev/null | tail -n +4 | xargs -r rm -f

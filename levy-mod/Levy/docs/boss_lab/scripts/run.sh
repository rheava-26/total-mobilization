#!/usr/bin/env bash
# usage: run.sh <slot 1|2> <spec.tsv> <out.tsv> [timeoutSec] [port]
# Runs a calrev spec on private install tml<slot>/save<slot> (Levy.tmod copied from saveb first). Extra LEVY_CR_* env vars pass through.
SLOT=${1:?slot}; SPEC=${2:?spec}; OUT=${3:?out}; TMO=${4:-5400}; PORT=${5:-$((7790+SLOT))}
H=/home/user/lab
cp $H/saveb/Mods/Levy.tmod $H/save$SLOT/Mods/Levy.tmod
export LEVY_HARNESS_SCENARIO=${LEVY_HARNESS_SCENARIO:-calrev} LEVY_CR_SPEC="$SPEC" LEVY_CR_OUT="$OUT"
export HARNESS_TML=$H/tml$SLOT HARNESS_DATA=$H/save$SLOT HARNESS_SCRATCH=$H/scratch$SLOT
unset LEVY_TML_SAVE
mkdir -p $HARNESS_SCRATCH
cd /home/user/total-mobilization/.claude/worktrees/agent-a7e7652babfd3ac91
bash /home/user/total-mobilization/.claude/worktrees/agent-a7e7652babfd3ac91/levy-mod/Levy/tools/run_harness.sh -N -m CalamityMod -p "$PORT" -T "$TMO" -o $H/scratch$SLOT/harness_dummy.tsv
# keep only the newest server log for this slot
ls -t $H/scratch$SLOT/server_*.log 2>/dev/null | tail -n +4 | xargs -r rm -f

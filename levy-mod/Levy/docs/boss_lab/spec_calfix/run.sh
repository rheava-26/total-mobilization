#!/usr/bin/env bash
# usage: run.sh <tmod> <spec> <out> <port>
TMOD=$1; SPEC=$2; OUT=$3; PORT=${4:-7811}
H=/home/user/lab
cp "$TMOD" $H/savec/Mods/Levy.tmod
export LEVY_HARNESS_SCENARIO=calrev LEVY_CR_SPEC="$SPEC" LEVY_CR_OUT="$OUT"
export HARNESS_TML=$H/tmlc HARNESS_DATA=$H/savec HARNESS_SCRATCH=$H/c/scr
unset LEVY_TML_SAVE
cd /home/user/total-mobilization/.claude/worktrees/agent-aa2345f7eca371c32
bash levy-mod/Levy/tools/run_harness.sh -N -m CalamityMod -p "$PORT" -T 9000 -o $H/c/scr/dummy.tsv

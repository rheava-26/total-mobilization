#!/usr/bin/env bash
S=/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/alt5
REPO=/home/user/total-mobilization/.claude/worktrees/agent-a70a1826e20857be8/levy-mod/Levy
TML=/home/user/tml-bin/tml-alt4x; DATA=/home/user/tml-save-alt4x
export HARNESS_TML=$TML HARNESS_DATA=$DATA LEVY_TML_SAVE=$DATA HARNESS_SCRATCH=$S LEVY_HARNESS_SCENARIO=gate
rm -f $S/gate.tsv.gate.txt
$REPO/tools/run_harness.sh -N -m ThoriumMod -c -T 1500 -p 7794 -o $S/gate.tsv > $S/gate.out 2>&1
cat $S/gate.tsv.gate.txt
tail -3 $S/gate.out

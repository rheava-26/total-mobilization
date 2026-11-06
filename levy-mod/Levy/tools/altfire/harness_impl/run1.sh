#!/usr/bin/env bash
# usage: run1.sh <lane 1|2> <scenario boss|pack> <mode left|random|policy> [filter-file]
LANE=$1; SC=$2; MODE=$3
S=/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/alt5
FILT=${4:-$S/filter.txt}
REPO=/home/user/total-mobilization/.claude/worktrees/agent-a70a1826e20857be8/levy-mod/Levy
if [ "$LANE" = 1 ]; then TML=/home/user/tml-bin/tml-alt; DATA=/home/user/tml-save-alt; PORT=7791; elif [ "$LANE" = 3 ]; then TML=/home/user/tml-bin/tml-alt3x; DATA=/home/user/tml-save-alt3x; PORT=7793; else TML=/home/user/tml-bin/tml-alt2; DATA=/home/user/tml-save-alt2; PORT=7792; fi
if [ "$SC" = boss ]; then CROWD=1; TOUGH=1; else CROWD=6; TOUGH=0; fi
export HARNESS_TML=$TML HARNESS_DATA=$DATA LEVY_TML_SAVE=$DATA HARNESS_SCRATCH=$S LEVY_HARNESS_NOBASE=1 LEVY_ALT_MODE=$MODE LEVY_HARNESS_CROWD=$CROWD LEVY_ALT_TOUGH=$TOUGH
if [ "$MODE" != left ]; then export LEVY_HARNESS_NOWALK=1; fi
OUT=$S/${SC}_${MODE}${5:-}.tsv
$REPO/tools/run_harness.sh -N -m CalamityMod -c -f "$(cat $FILT)" -r 15 -t 420 -p $PORT -o $OUT > $S/${SC}_${MODE}${5:-}.out 2>&1
tail -4 $S/${SC}_${MODE}${5:-}.out

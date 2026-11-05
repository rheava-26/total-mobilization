#!/usr/bin/env bash
# usage: runalt.sh <mode left|random|policy> <outname> <filter> <port> [crowd] [tough] [ticks] [mod] [table]
MODE=$1; OUTN=$2; FILT=$3; PORT=$4; CROWD=${5:-1}; TOUGH=${6:--1}; TICKS=${7:-420}; MOD=${8:-CalamityMod}
REPO=/home/user/total-mobilization/.claude/worktrees/agent-a3c84ab0363cb6bae/levy-mod/Levy
TABLE=${9:-$REPO/tools/altfire/calamity_altfire_items.tsv}
SCR=/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/altharness
mkdir -p "$SCR"
export HARNESS_TML=/home/user/tml-bin/tml-alt3 HARNESS_DATA=/home/user/tml-save-alt3 HARNESS_SCRATCH=$SCR
export LEVY_HARNESS_NOBASE=1 LEVY_ALT_MODE=$MODE LEVY_ALT_TABLE=$TABLE LEVY_HARNESS_CROWD=$CROWD LEVY_ALT_TOUGH=$TOUGH
[ -z "$MOD" ] && MOD=CalamityMod
bash $REPO/tools/run_harness.sh -N -m "$MOD" -c -f "$FILT" -r 15 -t "$TICKS" -p "$PORT" -o "$SCR/$OUTN.tsv" > "$SCR/$OUTN.out" 2>&1
tail -6 "$SCR/$OUTN.out"

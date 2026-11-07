#!/usr/bin/env bash
# usage: go.sh <name> <port> <trace 0|1> <slot ""|2> [holdforce] [extra env as K=V]
# runs spec <name>.tsv -> <name>_out.tsv in the dud2 scratch dir (call detached)
D=/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/dud2
N=${1:?name}; PORT=${2:-7796}
export LEVY_DUD_TRACE=${3:-0}
S=${4:-}
[ "$S" = "-" ] && S=""
export SLOT=$S
[ -n "${5:-}" ] && [ "${5:-}" != "-" ] && export LEVY_HOLD_FORCE=$5
export LEVY_CR_SP=${LEVY_CR_SP:-1}
[ -n "${6:-}" ] && export "$6"
[ -z "${NOCOPY:-}" ] && cp /home/user/lab/savedud/Mods/Levy.tmod /home/user/lab/savedud$S/Mods/Levy.tmod 2>/dev/null
cd $D
./runx.sh $D/$N.tsv $D/${N}_out.tsv 5400 $PORT > $D/$N.log 2>&1
cp /home/user/lab/tmldud$S/tModLoader-Logs/server.log $D/${N}_server.log 2>/dev/null
echo done > $D/$N.done

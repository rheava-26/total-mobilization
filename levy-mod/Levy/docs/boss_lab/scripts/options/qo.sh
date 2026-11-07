#!/usr/bin/env bash
# usage: qo.sh <slot o|o2> <port> <specname...>   runs specs_o/<name>.tsv -> out_o/<name>.tsv in order, retrying (finished keys are skipped) up to 4 times if the server dies.
SLOT=$1; PORT=$2; shift 2
H=/home/user/lab
for N in "$@"; do
  WANT=$(grep -c . $H/specs_o/$N.tsv)
  for TRY in 1 2 3 4; do
    HAVE=$(( $(cat $H/out_o/$N.tsv 2>/dev/null | wc -l) - 1 )); [ "$HAVE" -ge "$WANT" ] && break
    bash $H/runo.sh $SLOT $H/specs_o/$N.tsv $H/out_o/$N.tsv 9000 $PORT >> $H/out_o/$N.log 2>&1
  done
done
echo queue-done $SLOT > $H/out_o/queue_$SLOT.done

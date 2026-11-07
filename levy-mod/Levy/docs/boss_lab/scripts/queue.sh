#!/usr/bin/env bash
# usage: queue.sh <slot> <specname> [specname...]   runs /home/user/lab/specs/<name>.tsv -> out/<name>.tsv one after another on that slot
SLOT=$1; shift
export LEVY_REC_DIR=${LEVY_REC_DIR:-/home/user/lab/rec}
for N in "$@"; do
  /home/user/lab/run.sh $SLOT /home/user/lab/specs/$N.tsv /home/user/lab/out/$N.tsv 9000 > /home/user/lab/out/$N.log 2>&1
done

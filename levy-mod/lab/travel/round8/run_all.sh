#!/usr/bin/env bash
# run_all.sh TMOD TAG [SETS...]   runs the job sets one after another, 3 at a time
T=$1; TAG=$2; shift 2
SETS=${@:-"far near farclimb nearclimb mixed climb"}
for S in $SETS; do
  python3 $(dirname $0)/jobs.py $T $TAG $S | xargs -P3 -I{} bash -c "{}" > ${R8:-/home/user/lab/tl8/r8}/log_${TAG}_$S.txt 2>&1
  echo "set $S done $(date +%T)" >> ${R8:-/home/user/lab/tl8/r8}/progress.txt
done
echo "ALL $T $TAG done $(date +%T)" >> ${R8:-/home/user/lab/tl8/r8}/progress.txt

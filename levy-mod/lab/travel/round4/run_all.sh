#!/usr/bin/env bash
# run_all.sh TMOD TAG [SETS...]   runs the job sets one after another, 3 at a time
T=$1; TAG=$2; shift 2
SETS=${@:-"far near farclimb nearclimb mixed climb"}
for S in $SETS; do
  python3 /home/user/lab/t4/jobs.py $T $TAG $S | xargs -P3 -I{} bash -c "{}" > /home/user/lab/t4/log_${TAG}_$S.txt 2>&1
  echo "set $S done $(date +%T)" >> /home/user/lab/t4/progress.txt
done
echo "ALL $T $TAG done $(date +%T)" >> /home/user/lab/t4/progress.txt

#!/usr/bin/env bash
# runq.sh TMODNAME TAG SET [ENV...]   one set, 2 at a time
T=$1; TAG=$2; S=$3; shift 3
python3 /home/user/lab/t5/jobs.py $T $TAG $S | sed "s#^env #env $* #" | xargs -P2 -I{} bash -c "{}" > /home/user/lab/t5/log_${TAG}_$S.txt 2>&1
echo "set $S $TAG done $(date +%T)" >> /home/user/lab/t5/progress.txt

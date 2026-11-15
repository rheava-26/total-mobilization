#!/usr/bin/env bash
# arm.sh NAME SPEC [extra levylab args]   (env passes through)
L=${DD_LAB:-/home/user/lab/destroyerdrop}; R=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../../.." && pwd)
n=$1; spec=$2; shift 2
mkdir -p $L/work/$n
cd $R && $L/lab.sh levy-mod/lab/levylab run $spec --jobs ${JOBS:-2} --per-proc 8 --rec 99 --out $L/work/$n/run --quiet "$@" > $L/work/$n/log.txt 2>&1
echo "arm $n done $(date +%T)" >> $L/progress.txt

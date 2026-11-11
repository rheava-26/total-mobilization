#!/usr/bin/env bash
# reps.sh TMOD PREFIX N [ENV...] : N repetitions of the mixed chasm leg, 3 at a time; prints hook/wings arrivals
B=$1; P=$2; N=$3; shift 3
for i in $(seq 1 $N); do echo "$i"; done | xargs -P3 -I{} bash /home/user/lab/t7/rgjob2.sh $B ${P}_{} "$@" > /home/user/lab/t7/log_${P}.txt 2>&1
grep -h "{" /home/user/lab/t7/log_${P}.txt | sort -V

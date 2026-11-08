#!/usr/bin/env bash
# zt.sh TMOD TAG leg...   (zoo legs)
T=$1; TAG=$2; shift 2
for k in "$@"; do f=$((20+150*k)); t=$((f+150)); echo "${TAG}_zoo_Z$k zoo LevyTest map $f $t 0 6000"; done | xargs -P3 -L1 bash -c '/home/user/lab/t2/r1.sh '$T' $0 $1 $2 $3 $4 $5 $6 $7'

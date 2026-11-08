#!/usr/bin/env bash
# batch.sh <tmod> <tag> [parallel] ; runs the standard job list
S=${TRAVEL_HOME:-$HOME/travel-lab}
TMOD=$1; TAG=$2; P=${3:-3}; cd $S
add() { echo "$1 $2 $3 $4 $5 $6"; }
{
add ${TAG}_walk_map_full walk map 0 920 12000
add ${TAG}_walk_follow_full walk follow 0 920 12000
for k in 0 1 2 3 4 5; do f=$((20+150*k)); t=$((f+150)); add ${TAG}_walk_map_L$k walk map $f $t 6000; done
for k in 0 1 2 3 4 5; do f=$((20+150*k)); t=$((f+150)); add ${TAG}_zoo_map_Z$k zoo map $f $t 6000; done
for k in 0 1 2 3 4 5; do f=$((20+150*k)); t=$((f+150)); add ${TAG}_zoo_follow_Z$k zoo follow $f $t 6000; done
} | xargs -P$P -L1 bash -c "COURSE=\$1 $S/run.sh $TMOD \$0 \$2 \$3 \$4 mixed 15 \$5"

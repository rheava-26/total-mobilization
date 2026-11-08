#!/usr/bin/env bash
# batchW.sh <tmod> <tag> [parallel] [foes]  -- real generated worlds: long (800) and medium (150) map orders, plus follow
S=${TRAVEL_HOME:-$HOME/travel-lab}
TMOD=$1; TAG=$2; P=${3:-3}; FOES=${4:-0}; cd $S
add() { echo "$1 $2 $3 $4 $5 $6 $7"; }
{
for w in M1 M2; do
  for x in 700 1700 2700 3700 4700; do
    add ${TAG}_${w}_x${x}_long $w $x map 0 800 9000
    add ${TAG}_${w}_x${x}_fol $w $x follow 0 800 9000
    add ${TAG}_${w}_x${x}_med1 $w $x map 0 150 4000
    add ${TAG}_${w}_x${x}_med2 $w $((x+300)) map 0 150 4000
    add ${TAG}_${w}_x${x}_med3 $w $((x+600)) map 0 -150 4000
  done
done
} | xargs -P$P -L1 bash -c "FOES=$FOES WORLD=\$1 COURSE=world X0=\$2 $S/run.sh $TMOD \$0 \$3 \$4 \$5 mixed 15 \$6"

#!/usr/bin/env bash
# r1.sh TMODNAME RUNNAME COURSE WORLD MODE FROM TO [X0] [CAP]   (env: GEAR N CMDFAR HOLD CMDTRACK MIX ...; R8 = scratch root, default /home/user/lab/tl8/r8)
# R8/tmod/TMODNAME.tmod is the build; TRAVEL_HOME (default R8/../th) holds save/Worlds with LevyTest, M1, M2, W3..W6; results land in R8/res/RUNNAME.csv (+ .ev.gz)
R8=${R8:-/home/user/lab/tl8/r8}
export TRAVEL_HOME=${TRAVEL_HOME:-$(dirname $R8)/th} LEVY_TRAVEL_CMDFAR=${CMDFAR:-20} TML_DIR=${TML_DIR:-$(dirname $R8)/tml}
mkdir -p $TRAVEL_HOME/tmod
cp $R8/tmod/$1.tmod $TRAVEL_HOME/tmod/$1.tmod
PORT=$((20000 + ($$ * 7 + RANDOM) % 30000))
COURSE=$3 WORLD=$4 X0=${8:-0} bash $(dirname $0)/../run.sh $TRAVEL_HOME/tmod/$1.tmod $2 $5 $6 $7 ${GEAR:-mixed} ${N:-15} ${9:-6000} $PORT
D=$TRAVEL_HOME/runs/$2
mkdir -p $R8/res
if [ -f $D/out/levies.csv ]; then
  cp $D/out/levies.csv $R8/res/$2.csv
  gzip -c $D/out/events.txt > $R8/res/$2.ev.gz
fi
rm -rf $D

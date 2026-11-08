#!/usr/bin/env bash
# r1.sh TMODNAME RUNNAME COURSE WORLD MODE FROM TO [X0] [CAP]
export TRAVEL_HOME=/home/user/lab/t2/th LEVY_TRAVEL_CMDFAR=${CMDFAR:-20}
cp /home/user/lab/t2/tmod/$1.tmod $TRAVEL_HOME/tmod/$1.tmod
PORT=$((20000 + ($$ * 7 + RANDOM) % 30000))
COURSE=$3 WORLD=$4 X0=${8:-0} $TRAVEL_HOME/run.sh $TRAVEL_HOME/tmod/$1.tmod $2 $5 $6 $7 ${GEAR:-mixed} ${N:-15} ${9:-6000} $PORT

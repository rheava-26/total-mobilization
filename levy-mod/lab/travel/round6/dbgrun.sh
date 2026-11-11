#!/usr/bin/env bash
# dbgrun.sh TMOD NAME DBG [ENV=VAL ...] : the mixed army on the corruption chasm leg, DBG lines of one levy kept in /home/user/lab/t6/dbg/NAME.txt
cd /home/user/lab/t6
B=$1; NAME=$2; DBG=$3; shift 3
cp tmod/$B.tmod th/tmod/$B.tmod
env "$@" TRAVEL_HOME=/home/user/lab/t6/th LEVY_TRAVEL_DBG=$DBG LEVY_TRAVEL_CMDFAR=20 LEVY_TRAVEL_NOHOP=1 COURSE=corrupt WORLD=LevyTest X0=0 GEAR=mixed N=15 TMO=300 bash th/run.sh th/tmod/$B.tmod $NAME map 320 470 mixed 15 1400 $((24200 + RANDOM % 500)) > /dev/null 2>&1
grep DBG th/runs/$NAME/out/events.txt | cut -c1-200 > dbg/$NAME.txt
wc -l dbg/$NAME.txt

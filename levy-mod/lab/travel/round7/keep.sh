#!/usr/bin/env bash
# keep.sh TMOD NAME [ENV=VAL ...] : the chasm leg once (use alone: copies the newest tML server log to th/logs/NAME.log)
B=$1; NAME=$2; shift 2
export "$@"
cp /home/user/lab/t7/tmod/$B.tmod /home/user/lab/t7/th/tmod/$B.tmod
export TRAVEL_HOME=/home/user/lab/t7/th LEVY_TRAVEL_CMDFAR=20 LEVY_TRAVEL_NOHOP=1 COURSE=${COURSE:-corrupt} WORLD=LevyTest X0=0
mkdir -p /home/user/lab/t7/th/logs
bash /home/user/lab/t7/th/run.sh /home/user/lab/t7/th/tmod/$B.tmod $NAME map ${FROM:-320} ${TO:-470} ${GEAR:-mixed} ${N:-15} ${CAP:-6000} $((24200 + RANDOM % 5000)) | tail -1
cp /home/user/lab/t7/tmlA/tModLoader-Logs/$(ls -t /home/user/lab/t7/tmlA/tModLoader-Logs | grep '^server[0-9]*.log$' | head -1) /home/user/lab/t7/th/logs/$NAME.log

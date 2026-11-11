#!/usr/bin/env bash
cd /home/user/lab/t7/th
for a in "33333 W3 7911" "44444 W4 7912" "55555 W5 7913" "66666 W6 7914"; do
  set -- $a
  if [ -f save/Worlds/$2.wld ]; then continue; fi
  mkdir -p gen/save/Worlds; rm -f gen/save/Worlds/$2.*
  TRAVEL_HOME=/home/user/lab/t7/th ./gen.sh 2 $1 $2 $3 &
  GP=$!
  for i in $(seq 1 100); do
    sleep 5
    if [ -f gen/$2.log ] && grep -q "Server started" gen/$2.log && [ -f gen/save/Worlds/$2.twld ]; then break; fi
  done
  sleep 3
  pkill -f "worldname $2" ; sleep 2
  wait $GP 2>/dev/null
  cp gen/save/Worlds/$2.wld gen/save/Worlds/$2.twld save/Worlds/ && echo "$2 done"
done
echo ALLGEN

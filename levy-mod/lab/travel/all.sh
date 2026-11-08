#!/usr/bin/env bash
S=${TRAVEL_HOME:-$HOME/travel-lab}
cd $S
T=${1:-$S/tmod/levy.tmod}
P=${2:-}
LEGACY=1 ./batch.sh $T ${P}Lg 3 > batch${P}Lg.log 2>&1
LEGACY=0 ./batch.sh $T ${P}Nw 3 > batch${P}Nw.log 2>&1
LEGACY=1 ./batchW.sh $T ${P}WLg 3 > batch${P}WLg.log 2>&1
LEGACY=0 ./batchW.sh $T ${P}WNw 3 > batch${P}WNw.log 2>&1
echo done > all${P}.done

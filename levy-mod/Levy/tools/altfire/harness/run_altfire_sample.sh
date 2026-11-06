#!/usr/bin/env bash
# usage: lanes.sh <lane 3|4> <scenario boss|pack> <mode...>
LANE=$1; SC=$2; shift 2
S=/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad
F="Hydra,SepticSkewer,CorinthPrime,BlossomFlux,Vesuvius,VeeringWind,Leviatitan,PlasmaRifle,PlasmaCaster,FrigidflashBolt,SeasSearing,Animosity,NullificationPistol,FaceMelter,ShortCircuit,SpeedBlaster"
if [ "$SC" = boss ]; then CROWD=1; TOUGH=1; else CROWD=6; TOUGH=0; fi
for M in "$@"; do
  if [ "$LANE" = 3 ]; then bash $S/runalt.sh $M ${SC}_$M "$F" 779$LANE $CROWD $TOUGH 420; else bash $S/runalt4.sh $M ${SC}_$M "$F" 779$LANE $CROWD $TOUGH 420; fi
done

#!/usr/bin/env bash
S=/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad
cd $S; T=$1; TAG=$2
{
echo "${TAG}_C0_holes corrupt 20 170"
echo "${TAG}_C1_cave corrupt 170 320"
echo "${TAG}_C2_chasm corrupt 320 470"
echo "${TAG}_C3_hole corrupt 470 620"
echo "${TAG}_Cfull corrupt 20 620"
} | xargs -P3 -L1 bash -c "COURSE=\$1 $S/run.sh $T \$0 map \$2 \$3 mixed 15 7000"
echo done > c_$TAG.done

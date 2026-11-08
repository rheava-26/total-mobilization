#!/usr/bin/env bash
# runall.sh TMODNAME TAG [P] [SET]  -- all round-2 sets. SET = all|worlds|zoo|cor|walk
T=$1; TAG=$2; P=${3:-3}; SET=${4:-all}
{
if [ $SET = all ] || [ $SET = worlds ] || [ $SET = med ] || [ $SET = lf ]; then
for w in M1 M2; do for x in 700 1700 2700 3700 4700; do
  [ $SET = med ] || echo "${TAG}_${w}_x${x}_long world $w map 0 800 $x 9000"
  [ $SET = med ] || echo "${TAG}_${w}_x${x}_fol world $w follow 0 800 $x 9000"
  [ $SET = lf ] || echo "${TAG}_${w}_x${x}_med1 world $w map 0 150 $x 4000"
  [ $SET = lf ] || echo "${TAG}_${w}_x${x}_med2 world $w map 0 150 $((x+300)) 4000"
  [ $SET = lf ] || echo "${TAG}_${w}_x${x}_med3 world $w map 0 -150 $((x+600)) 4000"
done; done
fi
if [ $SET = all ] || [ $SET = zoo ]; then
for k in 0 1 2 3 4 5; do f=$((20+150*k)); t=$((f+150));
  echo "${TAG}_zoo_map_Z$k zoo LevyTest map $f $t 0 6000"
  echo "${TAG}_zoo_follow_Z$k zoo LevyTest follow $f $t 0 6000"
done
fi
if [ $SET = all ] || [ $SET = cor ]; then
for k in 0 1 2 3 4; do f=$((20+150*k)); t=$((f+150)); echo "${TAG}_cor_map_C$k corrupt LevyTest map $f $t 0 6000"; done
fi
if [ $SET = all ] || [ $SET = walk ]; then
for k in 0 1 2 3 4 5; do f=$((20+150*k)); t=$((f+150)); echo "${TAG}_walk_map_L$k walk LevyTest map $f $t 0 6000"; done
echo "${TAG}_walk_map_full walk LevyTest map 0 920 0 12000"
echo "${TAG}_walk_follow_full walk LevyTest follow 0 920 0 12000"
fi
} | xargs -P$P -L1 bash -c '/home/user/lab/t2/r1.sh '$T' $0 $1 $2 $3 $4 $5 $6 $7'

#!/usr/bin/env bash
# cost.sh: idle-machine planner cost, base vs n7, same four runs each, one at a time
cd /home/user/lab/t2
for T in base n7; do
  ./r1.sh $T K_${T}_long world M1 map 0 800 700 9000
  ./r1.sh $T K_${T}_med world M2 map 0 150 2700 4000
  ./r1.sh $T K_${T}_c0 corrupt LevyTest map 20 170 0 6000
  ./r1.sh $T K_${T}_c2 corrupt LevyTest map 320 470 0 6000
  ./r1.sh $T K_${T}_walk walk LevyTest map 0 920 0 12000
done
for T in base n7; do
  for r in long med c0 c2 walk; do
    echo -n "$T $r: "; grep -h "^pathfinder" th/runs/K_${T}_$r/out/events.txt | sed -E 's/.*pathfinder: ([^;]*); planner: (.*) ; wall ms for run ([0-9]+).*/local[\1] planner[\2] wall=\3/'
  done
done

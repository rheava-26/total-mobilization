#!/usr/bin/env bash
cd /home/user/lab/t5
B="LEVY_AIRLAVA=0 LEVY_HOOKMORE=0 LEVY_WINGCTL=0"
./runq.sh a1 b5 lava5 $B
./runq.sh a1 a5 lava5
./runq.sh a1 b0 lava $B
./runq.sh a1 a0 lava
echo CHAIN1 done >> progress.txt

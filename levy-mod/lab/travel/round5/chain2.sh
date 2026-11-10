#!/usr/bin/env bash
cd /home/user/lab/t5
B="LEVY_AIRLAVA=0 LEVY_HOOKMORE=0 LEVY_WINGCTL=0"
./runq.sh a9 rb reg $B
./runq.sh a9 ra reg
echo CHAIN2 done >> progress.txt

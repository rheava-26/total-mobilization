#!/usr/bin/env bash
# full.sh : before (h1) then after (c11) on the real-world and regression sets
cd /home/user/lab/t4
./run_all.sh h1 b1 hnear hfar hmed mixednh
./run_all.sh $1 $2 hnear hfar hmed mixednh
echo FULLDONE >> progress.txt

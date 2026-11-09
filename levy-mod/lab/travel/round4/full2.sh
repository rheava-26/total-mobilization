#!/usr/bin/env bash
cd /home/user/lab/t4
./run_all.sh h1 b1 far near
./run_all.sh c15 a3 far near hfar hnear hmed mixednh wall zoohook
echo FULL2DONE >> progress.txt

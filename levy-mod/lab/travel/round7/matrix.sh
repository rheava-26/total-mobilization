#!/usr/bin/env bash
# matrix.sh TMOD OUTFILE "TAG ENV..." lines on stdin ; runs 3 at a time, appends result lines
B=$1; OUT=$2
xargs -P3 -L1 bash -c 'bash /home/user/lab/t7/rgjob2.sh '$B' "$@"' _ >> $OUT 2>&1

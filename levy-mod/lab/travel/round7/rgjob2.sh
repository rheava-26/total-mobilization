#!/usr/bin/env bash
# rgjob2.sh TMOD TAG [ENV=VAL...] : the corruption chasm leg once, hook arrivals
cd /home/user/lab/t7
B=$1; T=$2; shift 2
env "$@" CMDFAR=20 LEVY_TRAVEL_NOHOP=1 bash /home/user/lab/t7/r1.sh $B $T corrupt LevyTest map 320 470 0 6000 | tail -1
python3 -I - $T <<'PY'
import csv, collections, sys
f = f'/home/user/lab/t7/res/{sys.argv[1]}.csv'
c = collections.Counter(); n = collections.Counter()
for r in csv.DictReader(open(f)):
    g = r['gear'].split(':')[0]; n[g] += 1; c[g] += r['arrived'] == '1'
print(sys.argv[1], {g: f'{c[g]}/{n[g]}' for g in n})
PY

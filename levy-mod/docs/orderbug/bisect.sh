#!/usr/bin/env bash
# usage: bisect.sh <outfile> <rev:name>...   ports the orderobey harness onto each rev, runs fight + calm (packet path, radio, mixed gear), keeps one summary block per rev
cd /home/user/lab/ob
OUT="$1"; shift; : > $OUT
L=tml/tModLoader-Logs/server.log
for pair in "$@"; do
  rev="${pair%%:*}"; name="${pair##*:}"
  echo "##### $name ($rev)" >> $OUT
  if ! ./port.sh $rev $name > build_$name.log 2>&1; then echo "BUILD FAILED" >> $OUT; tail -3 build_$name.log >> $OUT; continue; fi
  for round in fight calm; do
    LEVY_OO_GEAR=mixed LEVY_OO_BOSSFLAG=1 LEVY_OO_BOSSDMG=40 ./run.sh $name.tmod bis_${name}_$round radio $round 420 > /dev/null 2>&1
    echo "--- $name $round" >> $OUT
    grep -a "ORDEROBEY: \(RESULT\)" $L | sed -E 's/^.*ORDEROBEY: //' >> $OUT
    grep -a "ORDEROBEY: STEP" $L | sed -E 's/^.*ORDEROBEY: //' | grep -v "fail=0$" | tr '\n' ';' >> $OUT; echo >> $OUT
    grep -a "ORDEROBEY: .*FAIL" $L | sed -E 's/^.*ORDEROBEY: //' | awk '{print $1,$2}' | sort | uniq -c | tr '\n' ';' >> $OUT; echo >> $OUT
    grep -a "ORDEROBEY: .*FAIL" $L | grep -c "REC-IGNORED" | sed 's/^/REC-IGNORED lines: /' >> $OUT
    grep -a "Exception" $L | grep -v "lost conn" | sort | uniq -c | head -3 >> $OUT
  done
done
echo DONE >> $OUT

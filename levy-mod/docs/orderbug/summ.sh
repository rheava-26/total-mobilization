#!/usr/bin/env bash
# summarize last server.log
L=/home/user/lab/ob/tml/tModLoader-Logs/server.log
grep -a "HARNESS-ORDEROBEY: \(STEP\|RESULT\|pre-order\)" $L | sed -E 's/^\[[0-9:.]+\] \[[^]]*\] \[Levy\]: HARNESS-ORDEROBEY: //' | cut -c1-330
grep -a "Exception" $L | grep -v "lost conn" | sort | uniq -c | head -5

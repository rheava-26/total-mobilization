#!/usr/bin/env bash
# usage: tn.sh <serverlog> : last DUDTRACE line per weapon
grep "DUDTRACE CalamityMod\|DUDTRACE proj" "$1" | awk '{k=$0; sub(/^.*DUDTRACE /,"",k); split(k,a," "); last[a[1]]=k} END{for (x in last) print last[x]}' | cut -c1-520 | sort

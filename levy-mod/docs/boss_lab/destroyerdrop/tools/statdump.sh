#!/usr/bin/env bash
L=${DD_LAB:-/home/user/lab/destroyerdrop}; S=$L/work/specs
export JOBS=1
LEVY_STATDUMP=1 $L/arm.sh sd_on $S/statdump.tsv --rev ctl-diag
LEVY_STATDUMP=1 LEVY_MOBSLOTS=0 $L/arm.sh sd_off $S/statdump.tsv --rev ctl-diag

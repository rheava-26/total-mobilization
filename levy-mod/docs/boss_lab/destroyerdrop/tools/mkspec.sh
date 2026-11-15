#!/usr/bin/env bash
# mkspec.sh ARMY(name) FROM TO > spec
a=$1; f=${2:-1}; t=${3:-40}
for s in $(seq $f $t); do printf 'fight\tdestroyer\tmdeath\t%s\t18000\tcmd=near\torder=follow\tarena=a3\tlv=${DD_LAB:-/home/user/lab/destroyerdrop}/work/armies/destroyer_%s.json\tmeta=hmb_destroyer_%s_mdeath\n' $s $a $a; done

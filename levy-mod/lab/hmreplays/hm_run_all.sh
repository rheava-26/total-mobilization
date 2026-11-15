#!/usr/bin/env bash
# Runs the whole hardmode boss battery + broad sweep for one lab root, one group after another (2 tML server processes at a time).
#   HM_LAB=/home/user/lab/hmbattery2 hm_run_all.sh        # the lab root holds kitsrc/ (tml, save/Mods, world/LevyTest.wld) and lab.sh (it sets the LEVYLAB_* env and execs its arguments)
# Groups: bat (Destroyer: baseline 40 + n12/n24/n36/nowings/flat 20 each; Aquatic Scourge: baseline 20), then the sweep in three groups (3 on-tier seeds + 1 one-tier-below seed per boss x mode).
# Every fight is recorded; each group's recordings are collected into $HM_LAB/recordings (sweep) or $HM_LAB/battery (battery) as gzip .levyrec + fights.csv.
# SKIP_BAT=1 skips the battery group.
H=${HM_LAB:-/home/user/lab/hmbattery2}; export HM_LAB=$H
R=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd); T=$R/levy-mod/lab/hmreplays
cd "$R"
log() { echo "$1 $(date +%T)" >> "$H/progress.txt"; }
run() { # $1 = group dir, $2 = collector script
  "$H/lab.sh" levy-mod/lab/levylab run "$1/spec.tsv" --jobs 2 --per-proc 8 --rec 99 --out "$1/run" --quiet > "$1/log.txt" 2>&1
  python3 "$T/$2" "$1/run" > "$1/collect.log" 2>&1
}
if [ "${SKIP_BAT:-0}" != 1 ]; then
  mkdir -p "$H/work/bat/d" "$H/work/bat/a"
  python3 "$T/hm_battery.py" "$H/work/bat/d" --bosses destroyer > "$H/work/bat/mk.log" 2>&1
  python3 "$T/hm_battery.py" "$H/work/bat/a" --bosses aqua --cells baseline --base-fights 20 >> "$H/work/bat/mk.log" 2>&1
  cat "$H/work/bat/d/spec.tsv" "$H/work/bat/a/spec.tsv" > "$H/work/bat/spec.tsv"
  cat "$H/work/bat/d/GEAR.md" > "$H/work/bat/GEAR.md"; tail -n +3 "$H/work/bat/a/GEAR.md" >> "$H/work/bat/GEAR.md"
  run "$H/work/bat" hm_bcollect.py; log "bat done"
fi
sweep() { # $1 = group name, $2 = comma list of bosses
  mkdir -p "$H/work/$1"
  python3 "$T/hm_mk.py" "$H/work/$1" --bosses "$2" --seeds 1-3 --mid-seeds 4 > "$H/work/$1/mk.log" 2>&1
  run "$H/work/$1" hm_collect.py; log "$1 done"
}
sweep gA destroyer,aqua,twins,prime,brim,cryo,cal,qslime
sweep gB plant,levi,aureus,golem,fish,pbg,deer,wof
sweep gD crab,hive,perf,eol,cultist,ravager,ml
log ALLDONE

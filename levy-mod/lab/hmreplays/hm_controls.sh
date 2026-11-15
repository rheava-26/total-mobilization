#!/usr/bin/env bash
# Attribution controls for a Destroyer Master+Death baseline shift (same 40 fights, same army, same seeds as the battery baseline), one change at a time:
#   noveto    this build, LEVY_SPECSHOTS=0    (the Spectator commander vetoes hostile shots again, the pre-2.0.66 behaviour)
#   oldbuild  the hmreplays tip ($OLD_REV: code of the old run, before the Wings/Boots slots and the shot fix) with its own harness: does the OLD run reproduce on this box and world?
#   hitbox    Levy 2.0.64 (1a0e3ed): bosshitbox merged (levies take contact damage as a player would), veto on, no Wings/Boots slots
#   preslots  79d68d9: everything merged up to the Wings/Boots slots (adds fieldfix: thorns, recorder format 9), veto on
#   HM_LAB=/home/user/lab/hmbattery2 hm_controls.sh [names...]     results land in $HM_LAB/work/ctl_<name>/run
H=${HM_LAB:-/home/user/lab/hmbattery2}; export HM_LAB=$H
R=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd); T=$R/levy-mod/lab/hmreplays
OLD_REV=${OLD_REV:-320e80c}
cd "$R"
[ -f "$H/work/ctl/spec.tsv" ] || { mkdir -p "$H/work/ctl"; python3 "$T/hm_battery.py" "$H/work/ctl" --bosses destroyer --cells baseline --base-fights 40 > /dev/null; }
one() { # $1 = name, rest = extra levylab args; env comes from the caller
  n=$1; shift
  mkdir -p "$H/work/ctl_$n"
  "$H/lab.sh" levy-mod/lab/levylab run "$H/work/ctl/spec.tsv" --jobs 2 --per-proc 8 --out "$H/work/ctl_$n/run" --quiet "$@" > "$H/work/ctl_$n/log.txt" 2>&1
  echo "ctl $n done $(date +%T)" >> "$H/progress.txt"
}
for n in ${@:-noveto oldbuild}; do
  case $n in
    noveto) LEVY_SPECSHOTS=0 one noveto;;
    oldbuild) one oldbuild --rev "$OLD_REV";;
    hitbox) one hitbox --rev 1a0e3ed;;
    preslots) one preslots --rev 79d68d9;;
  esac
done

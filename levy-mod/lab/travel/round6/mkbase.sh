#!/usr/bin/env bash
# mkbase.sh : build 'base' = HEAD gameplay + the current lab harness (Travel.cs) only
W=/home/user/total-mobilization/.claude/worktrees/agent-a80a6a7571708b50c
B=/home/user/lab/t6/basesrc
rm -rf $B; mkdir -p $B
cp -r $W/levy-mod/Levy $B/Levy
rm -rf $B/Levy/bin $B/Levy/obj
cd $W
for f in $(git diff --name-only e7ff2af | grep '^levy-mod/Levy/' ); do
  rel=${f#levy-mod/Levy/}
  case "$rel" in
    Common/Testing/CombatHarness.Travel.cs) ;;
    *) git show e7ff2af:$f > $B/Levy/$rel ;;
  esac
done
rm -f $B/Levy/Content/NPCs/SoldierNPC.FlightTalk.cs
printf 'namespace Levy.Content.NPCs { public partial class SoldierNPC { internal static long StatArcStarts, StatArcFuelOut; internal static int FlightShouts, FlightLines; internal static long FlyUnjams; } }\n' > $B/Levy/Content/NPCs/SoldierNPC.BaseStubs.cs
cd /home/user/lab/t6 && bash build.sh $B/Levy base

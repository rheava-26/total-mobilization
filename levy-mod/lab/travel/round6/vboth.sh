#!/usr/bin/env bash
# vboth.sh : build Levy + Mechanization + Warbands from the travel5 worktree with a private install copy (tmlV) and boot headless once.
W=/home/user/total-mobilization/.claude/worktrees/agent-a80a6a7571708b50c
S=/home/user/lab/t6/vsave
T=/home/user/lab/t6/tmlV
if [ ! -d $T ]; then cp -r /home/user/lab/t6/tmlA $T; fi
rm -rf $S; cp -r /home/user/tml-save-hotfix $S
rm -rf $S/ModSources/*
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 LEVY_SELFCHECK_WORLD=1
for M in "$W/levy-mod/Levy" "$W/mechanization-mod/Mechanization" "$W/warbands-mod/Warbands"; do
  N=$(basename $M)
  rm -rf $S/ModSources/$N; cp -r $M $S/ModSources/$N; rm -rf $S/ModSources/$N/bin $S/ModSources/$N/obj
  echo "== build $N"
  (cd $T && timeout 900 dotnet tModLoader.dll -server -nosteam -tmlsavedirectory $S -build $S/ModSources/$N 2>&1 | grep -v "warning CS1701" | grep -E " error |Build error|built|Compilation" | head -8)
done
ls -la $S/Mods/*.tmod
python3 -I -c "import json;p='$S/Mods/enabled.json';json.dump(['Levy','Mechanization','Warbands'],open(p,'w'))"
rm -f $T/tModLoader-Logs/server.log
(cd $T && timeout -k 10 300 dotnet tModLoader.dll -server -nosteam -tmlsavedirectory $S < /dev/null > $S/boot.out 2>&1; echo "boot exit=$?")
L=$T/tModLoader-Logs/server.log
echo "exc=$(grep -cE 'xception|TypeLoad|MissingMethod|malformed' $L)"
grep -E 'xception|TypeLoad|MissingMethod|malformed' $L | grep -v "proc/" | head -8 | cut -c1-300
echo "fail=0 lines: $(grep -cE 'fail=0' $L)"
grep -E 'fail=[1-9]|FAILURE|FAILED|FAIL ' $L | grep -v 'fail=0' | head -8 | cut -c1-300
grep -hE 'self-check' $L | grep -iE "mobil|hop|offscreen|travel|flight|movement|route" | sed 's/.*\]: //' | head -12 | cut -c1-200
grep -m1 -oE 'Levy \(Levy\) v[0-9.]+' $L
grep -c "self-check: OK\|self-check OK" $L

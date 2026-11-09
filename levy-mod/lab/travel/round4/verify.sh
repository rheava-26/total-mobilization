#!/usr/bin/env bash
# verify.sh : real build with the shipping tML + headless boot of a server, log in /home/user/lab/t4/vsave/boot.log
SRC=/home/user/total-mobilization/.claude/worktrees/agent-a9b19f7a5402e7c89/levy-mod/Levy
S=/home/user/lab/t4/vsave
rm -rf $S; cp -r /home/user/tml-save-hotfix $S
rm -rf $S/ModSources/Levy; cp -r $SRC $S/ModSources/Levy; rm -rf $S/ModSources/Levy/bin $S/ModSources/Levy/obj
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
cd /home/user/tml-bin/tml
timeout 1500 dotnet tModLoader.dll -server -build $S/ModSources/Levy -tmlsavedirectory $S 2>&1 | grep -v "warning CS1701" | grep -E " error |Build error|built" | head
ls -la $S/Mods/Levy.tmod
cat $S/Mods/enabled.json
timeout -k 10 240 dotnet tModLoader.dll -server -nosteam -tmlsavedirectory $S < /dev/null > $S/boot.log 2>&1
echo "boot exit=$?"
grep -aiE "xception|TypeLoad|MissingMethod|malformed" $S/boot.log | grep -v "proc/" | head -8
grep -aE "fail=|SELFCHECK|self-check|Finished loading" $S/boot.log | tail -15

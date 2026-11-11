#!/usr/bin/env bash
# vbase.sh : boot the OLD gameplay (basesrc) with the same toolchain, to see which self-check failures were there before
S=/home/user/lab/t6/vsave2
T=/home/user/lab/t6/tmlV
rm -rf $S; cp -r /home/user/tml-save-hotfix $S
rm -rf $S/ModSources/*
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 LEVY_SELFCHECK_WORLD=1
cp -r /home/user/lab/t6/basesrc/Levy $S/ModSources/Levy; rm -rf $S/ModSources/Levy/bin $S/ModSources/Levy/obj
(cd $T && timeout 900 dotnet tModLoader.dll -server -nosteam -tmlsavedirectory $S -build $S/ModSources/Levy 2>&1 | grep -E "Compilation finished|error CS" | head -3)
python3 -I -c "import json;json.dump(['Levy'],open('$S/Mods/enabled.json','w'))"
rm -f $T/tModLoader-Logs/server.log
(cd $T && timeout -k 10 300 dotnet tModLoader.dll -server -nosteam -tmlsavedirectory $S < /dev/null > $S/boot.out 2>&1; echo "boot exit=$?")
L=$T/tModLoader-Logs/server.log
echo "exc=$(grep -cE 'xception|TypeLoad|MissingMethod|malformed' $L)"
grep -E 'fail=[1-9]|FAILURE|FAILED|FAIL ' $L | grep -v 'fail=0' | head -5 | cut -c1-200

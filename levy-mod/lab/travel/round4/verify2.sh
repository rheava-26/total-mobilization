#!/usr/bin/env bash
# verify2.sh : boot the real build with a world so the load-time self-checks run
S=/home/user/lab/t4/vsave
mkdir -p $S/Worlds
cp /home/user/lab/t4/th/save/Worlds/LevyTest.wld $S/Worlds/T.wld
cp /home/user/lab/t4/th/save/Worlds/LevyTest.twld $S/Worlds/T.twld
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
cd /home/user/tml-bin/tml
timeout -k 10 120 dotnet tModLoader.dll -server -nosteam -tmlsavedirectory $S -world $S/Worlds/T.wld -port 7999 < /dev/null > $S/boot2.log 2>&1
echo "boot exit=$?"
echo "exceptions: $(grep -aciE 'exception' $S/boot2.log)"
grep -aiE "self-check|fail=[0-9]|check.*(pass|fail)" $S/boot2.log | head -20 | cut -c1-220

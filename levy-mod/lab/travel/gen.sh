#!/usr/bin/env bash
# gen.sh <size 1-3> <seed> <name> <port>
S=${TRAVEL_HOME:-$HOME/travel-lab}
mkdir -p $S/gen/save/Mods $S/gen/save/Worlds
cp $S/save/Mods/Levy.tmod $S/gen/save/Mods/
echo '["Levy"]' > $S/gen/save/Mods/enabled.json
cp -r $S/save/ModSources $S/gen/save/ 2>/dev/null
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH
cd /home/user/tml-bin/tml
timeout 420 dotnet tModLoader.dll -server -nosteam -tmlsavedirectory $S/gen/save -port $4 -autocreate $1 -seed $2 -worldname $3 -world $S/gen/save/Worlds/$3.wld -difficulty 0 > $S/gen/$3.log 2>&1 < /dev/null

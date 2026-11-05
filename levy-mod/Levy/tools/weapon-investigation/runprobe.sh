#!/bin/bash
# usage: runprobe.sh out.txt catalogmods scenfile
SAVE=/home/user/tml-save-wci; TML=/home/user/tml-bin/tml-wci
echo '["CalamityModMusic","CalamityMod","ThoriumMod","Levy"]' > $SAVE/Mods/enabled.json
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH
export LEVY_PICKPROBE_TIERS="${4:-}" LEVY_PICKPROBE=1 LEVY_PICKPROBE_OUT="$1" LEVY_PICKPROBE_CATALOG="$2" LEVY_PICKPROBE_SCEN="$3"
cd $TML && timeout 900 dotnet tModLoader.dll -server -nosteam -tmlsavedirectory $SAVE -port 7791 -world $SAVE/Worlds/PP.wld > ${1}.log 2>&1 < /dev/null
echo exit=$?

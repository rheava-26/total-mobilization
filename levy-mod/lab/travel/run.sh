#!/usr/bin/env bash
# run.sh <tmod> <name> <mode> <from> <to> [gear] [n] [cap] [port]
S=${TRAVEL_HOME:-$HOME/travel-lab}
TMOD=$1; NAME=$2; MODE=$3; FROM=$4; TO=$5; GEAR=${6:-mixed}; N=${7:-15}; CAP=${8:-12000}; PORT=${9:-$((7800 + RANDOM % 150))}
R=$S/runs/$NAME; rm -rf $R; mkdir -p $R/save/Mods $R/save/Worlds $R/out
cp $TMOD $R/save/Mods/Levy.tmod; echo '["Levy"]' > $R/save/Mods/enabled.json
cp -r $S/save/ModSources $R/save/
W=${WORLD:-LevyTest}; cp $S/save/Worlds/$W.wld $R/save/Worlds/T.wld; cp $S/save/Worlds/$W.twld $R/save/Worlds/T.twld
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH
export LEVY_TRAVEL_LEGACY=${LEGACY:-0}
export LEVY_TRAVEL_COURSE=${COURSE:-walk} LEVY_TRAVEL_X0=${X0:-0} LEVY_TRAVEL_FOES=${FOES:-0} LEVY_TRAVEL_HOLD=${HOLD:-0} LEVY_TRAVEL_CMDTRACK=${CMDTRACK:-0}
export LEVY_HARNESS=1 LEVY_HARNESS_OUT=$R/harness.tsv LEVY_HARNESS_SCENARIO=travel LEVY_TRAVEL_MODE=$MODE LEVY_TRAVEL_FROM=$FROM LEVY_TRAVEL_TO=$TO LEVY_TRAVEL_GEAR=$GEAR LEVY_TRAVEL_N=$N LEVY_TRAVEL_CAP=$CAP LEVY_TRAVEL_OUT=$R/out
export LEVY_TRAVEL_MIX=${MIX:-} LEVY_TRAVEL_NOHOUSE=${NOHOUSE:-0}
cd ${TML_DIR:-/home/user/tml-bin/tml}
timeout ${TMO:-900} dotnet tModLoader.dll -server -nosteam -tmlsavedirectory $R/save -port $PORT -world $R/save/Worlds/T.wld > $R/server.log 2>&1 < /dev/null
rm -rf $R/save
echo "$NAME exit=$? $(grep -c 'HARNESS-TRAVEL: RESULT' $R/server.log) result; $(grep 'HARNESS-TRAVEL: RESULT' $R/server.log | head -1)"

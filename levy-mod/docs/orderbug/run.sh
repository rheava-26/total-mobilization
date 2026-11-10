#!/usr/bin/env bash
# usage: run.sh <tmod> <label> <mode> <round> [timeout] [extra env...]
set -u
TMOD="$1"; L="$2"; MODE="$3"; ROUND="$4"; TO="${5:-420}"
B=/home/user/lab/ob; S=$B/runs/$L; PORT=$((7800 + RANDOM % 150))
[ "${KEEP:-0}" = 1 ] || rm -rf "$S"; mkdir -p $S/save/Mods $S/save/Worlds $S/tml/tModLoader-Logs
for f in $B/tml/*; do b=$(basename "$f"); [ "$b" = tModLoader-Logs ] && continue; ln -sfn "$f" $S/tml/$b; done
cp "$TMOD" $S/save/Mods/Levy.tmod
for f in CalamityMod CalamityModMusic MagicStorage Mechanization SerousCommonLib ThoriumMod; do ln -sfn /home/user/lab/mods-shared/$f.tmod $S/save/Mods/$f.tmod; done
echo "${ENABLED:-[\"Levy\",\"Mechanization\"]}" > $S/save/Mods/enabled.json
[ "${KEEP:-0}" = 1 ] || cp /root/.local/share/Terraria/tModLoader/Worlds/LevyTest.wld $S/save/Worlds/W.wld
[ "${KEEP:-0}" = 1 ] || cp /root/.local/share/Terraria/tModLoader/Worlds/LevyTest.twld $S/save/Worlds/W.twld
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH
export LEVY_HARNESS=1 LEVY_HARNESS_OUT=$S/h.tsv LEVY_HARNESS_SCENARIO=orderobey LEVY_OO_MODE=$MODE LEVY_OO_ROUND=$ROUND LEVY_ORDERSCHECK=0
cd $S/tml
timeout -k 10 $TO dotnet tModLoader.dll -server -nosteam -tmlsavedirectory $S/save -port $PORT -world $S/save/Worlds/W.wld < /dev/null > $S/server.log 2>&1
echo "exit=$? label=$L"
grep -aE "HARNESS-ORDEROBEY: (RESULT|STEP|roster|boss|killed|respawn|reloaded|---|EXC)" $S/server.log | cut -c1-400
grep -aiE "xception" $S/server.log | grep -v "proc/" | head -5

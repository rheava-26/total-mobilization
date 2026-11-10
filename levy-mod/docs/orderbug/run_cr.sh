#!/usr/bin/env bash
# usage: run_cr.sh <tmod> <label> <boss> <mode> <cap> <extra spec fields (tab separated, optional)>   env: LEVY_OO_MODE, ENABLED
set -u
TMOD="$1"; L="$2"; BOSS="$3"; MODE="$4"; CAP="$5"; EXTRA="${6:-}"
B=/home/user/lab/ob; S=$B/runs/$L; PORT=$((7800 + RANDOM % 150))
rm -rf "$S"; mkdir -p $S/save/Mods $S/save/Worlds $S/tml/tModLoader-Logs $S/out
for f in $B/tml/*; do b=$(basename "$f"); [ "$b" = tModLoader-Logs ] && continue; ln -sfn "$f" $S/tml/$b; done
cp "$TMOD" $S/save/Mods/Levy.tmod
for f in CalamityMod CalamityModMusic MagicStorage Mechanization SerousCommonLib ThoriumMod; do ln -sfn /home/user/lab/mods-shared/$f.tmod $S/save/Mods/$f.tmod; done
echo "${ENABLED:-[\"CalamityModMusic\",\"CalamityMod\",\"Levy\",\"Mechanization\"]}" > $S/save/Mods/enabled.json
cp /root/.local/share/Terraria/tModLoader/Worlds/LevyTest.wld $S/save/Worlds/W.wld
cp /root/.local/share/Terraria/tModLoader/Worlds/LevyTest.twld $S/save/Worlds/W.twld
printf "fight\t$BOSS\t$MODE\t1\t$CAP\tcmd=near\tn=cp\tgear=mod\tgrade=lv\tl3=1\torder=follow\tarena=a3\tlv=/home/user/lab/ob/${ARMY:-army1.json}\ttag=op$EXTRA\n" > $S/spec.tsv
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH
export LEVY_HARNESS=1 LEVY_HARNESS_OUT=$S/out/h.tsv LEVY_HARNESS_MOD=CalamityMod LEVY_HARNESS_SCENARIO=calrev LEVY_CR_SPEC=$S/spec.tsv LEVY_CR_OUT=$S/out/o.tsv LEVY_CR_SP=1
export LEVY_CR_ORDERPROBE=1 LEVY_FEARMODEL=events LEVY_SURV=formation LEVY_ORDERSCHECK=0
cd $S/tml
timeout -k 10 ${TO:-900} dotnet tModLoader.dll -server -nosteam -tmlsavedirectory $S/save -port $PORT -world $S/save/Worlds/W.wld < /dev/null > $S/server.log 2>&1
echo "exit=$? label=$L"

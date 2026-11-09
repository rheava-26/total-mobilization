#!/usr/bin/env bash
# build.sh <src Levy dir> <outname>  -> /home/user/lab/t4/tmod/<outname>.tmod
SRC=$1; OUT=$2; SAVE=/home/user/lab/t4/bsave_$OUT
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
rm -rf $SAVE; mkdir -p $SAVE/ModSources $SAVE/Mods; cp /home/user/tml-save-hotfix/Mods/{ThoriumMod,SerousCommonLib,MagicStorage}.tmod $SAVE/Mods/; cp -r $SRC $SAVE/ModSources/Levy; rm -rf $SAVE/ModSources/Levy/docs $SAVE/ModSources/Levy/bin $SAVE/ModSources/Levy/obj
cd /home/user/lab/t4/tmlA && timeout 1500 dotnet tModLoader.dll -server -nosteam -tmlsavedirectory $SAVE -build $SAVE/ModSources/Levy 2>&1 | grep -v "warning CS1701" | grep -E " error |Build error|built" | head -30
mkdir -p /home/user/lab/t4/tmod; cp $SAVE/Mods/Levy.tmod /home/user/lab/t4/tmod/$OUT.tmod && ls -la /home/user/lab/t4/tmod/$OUT.tmod

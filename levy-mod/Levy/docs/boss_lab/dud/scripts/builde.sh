#!/usr/bin/env bash
# usage: builde.sh <abs Levy mod dir> -> builds with private tmle/savee
set -u
SRC="${1:?mod dir}"
SAVE=/home/user/lab/savee; TML=/home/user/lab/tmle
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
rm -rf "$SAVE/ModSources/Levy"; mkdir -p "$SAVE/ModSources"
cp -r "$SRC" "$SAVE/ModSources/Levy"; rm -rf "$SAVE/ModSources/Levy/bin" "$SAVE/ModSources/Levy/obj"
cd "$TML" && timeout 1500 dotnet tModLoader.dll -server -nosteam -tmlsavedirectory "$SAVE" -build "$SAVE/ModSources/Levy" 2>&1 | grep -v "warning CS1701" | grep -E " error |Build error|Compiling|built" | head -40
ls -la "$SAVE/Mods/Levy.tmod"

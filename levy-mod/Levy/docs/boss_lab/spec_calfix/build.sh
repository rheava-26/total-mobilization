#!/usr/bin/env bash
# usage: build.sh <abs Levy mod dir> <out tmod copy>   private tmlc/savec build
SRC="${1:?mod dir}"; OUT="${2:?out}"
SAVE=/home/user/lab/savec; TML=/home/user/lab/tmlc
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
rm -rf "$SAVE/ModSources/Levy"; mkdir -p "$SAVE/ModSources"
cp -r "$SRC" "$SAVE/ModSources/Levy"; rm -rf "$SAVE/ModSources/Levy/bin" "$SAVE/ModSources/Levy/obj"
cd "$TML" && timeout 1500 dotnet tModLoader.dll -server -nosteam -tmlsavedirectory "$SAVE" -build "$SAVE/ModSources/Levy" 2>&1 | grep -v "warning CS1701" | grep -E " error |Build error|built" | head -40
cp "$SAVE/Mods/Levy.tmod" "$OUT" && ls -la "$OUT"

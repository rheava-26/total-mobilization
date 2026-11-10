#!/usr/bin/env bash
# usage: build.sh <abs Levy mod dir> <out.tmod>
set -u
SRC="${1:?mod dir}"; OUT="${2:?out}"
B=/home/user/lab/ob; SAVE=$B/save; TML=$B/tml
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
rm -rf "$SAVE/ModSources/Levy"; mkdir -p "$SAVE/ModSources"
cp -r "$SRC" "$SAVE/ModSources/Levy"; rm -rf "$SAVE/ModSources/Levy/bin" "$SAVE/ModSources/Levy/obj" "$SAVE/ModSources/Levy/docs"
rm -f "$SAVE/Mods/Levy.tmod"
cd "$TML" && timeout 1500 dotnet tModLoader.dll -server -nosteam -tmlsavedirectory "$SAVE" -build "$SAVE/ModSources/Levy" > $B/last_build.log 2>&1
grep -v "warning CS" $B/last_build.log | grep -E " error |Build error|built" | head -20
[ -f "$SAVE/Mods/Levy.tmod" ] && cp "$SAVE/Mods/Levy.tmod" "$OUT" && echo "OK $OUT"

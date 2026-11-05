#!/usr/bin/env bash
# Build Levy with the NEW toolchain into a PRIVATE install + save dir (never touches the shared Mods dirs).  Usage: onhit_build.sh <levy mod dir>
set -u
SAVE=${HARNESS_DATA:-/home/user/tml-save-onhit-10}; TML=${HARNESS_TML:-/home/user/tml-onhit/tml}
SRC=$(cd "${1:?mod dir}" && pwd); NAME=$(basename "$SRC")
rm -rf "$SAVE/ModSources/$NAME"; mkdir -p "$SAVE/ModSources"; cp -r "$SRC" "$SAVE/ModSources/$NAME"; rm -rf "$SAVE/ModSources/$NAME/bin" "$SAVE/ModSources/$NAME/obj"
cp /home/user/tml-save-2026.10/ModSources/tModLoader.targets "$SAVE/ModSources/" 2>/dev/null
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
cd "$TML" && timeout 1500 dotnet tModLoader.dll -server -nosteam -tmlsavedirectory "$SAVE" -build "$SAVE/ModSources/$NAME" 2>&1 | grep -v "warning CS1701"
echo "build exit=${PIPESTATUS[0]}"; ls -la "$SAVE"/Mods/Levy.tmod

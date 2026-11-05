#!/usr/bin/env bash
# Builds Levy into a PRIVATE tModLoader save dir (never the shared Mods dir).
# Usage: tools/lab_build.sh [-N] [-S saveDir]     -N = new toolchain (tML 2026.10; needed with Calamity), default save dir then /home/user/tml-save-owner2-cal
# Needs <saveDir>/Mods/ThoriumMod.tmod (and CalamityMod*.tmod for -N Calamity runs) and <saveDir>/ModSources/tModLoader.targets (created if missing).
set -u
NEW=""; SAVE=""
while getopts "NS:" o; do case $o in N) NEW=1;; S) SAVE=$OPTARG;; esac; done
[ -z "$SAVE" ] && { if [ -n "$NEW" ]; then SAVE=/home/user/tml-save-owner2-cal; else SAVE=/home/user/tml-save-owner2; fi; }
HERE=$(cd "$(dirname "$0")/.." && pwd)
TML=/home/user/tml-bin/tml; [ -n "$NEW" ] && TML=/home/user/tml-bin/tml-2026.10
mkdir -p "$SAVE/Mods" "$SAVE/ModSources"
[ -f "$SAVE/ModSources/tModLoader.targets" ] || printf '<Project ToolsVersion="14.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">\n  <Import Project="%s/tMLMod.targets" />\n</Project>' "$TML" > "$SAVE/ModSources/tModLoader.targets"
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
SRC="$HERE"
if [ -n "$NEW" ]; then
  # the new toolchain builds from <save>/ModSources/<Name> (the csproj imports ..\tModLoader.targets)
  rm -rf "$SAVE/ModSources/Levy"; cp -r "$HERE" "$SAVE/ModSources/Levy"; rm -rf "$SAVE/ModSources/Levy/bin" "$SAVE/ModSources/Levy/obj" "$SAVE/ModSources/Levy/tools/owner_lab2_data"
  SRC="$SAVE/ModSources/Levy"
fi
cd "$TML" && timeout 1500 dotnet tModLoader.dll -server -nosteam -tmlsavedirectory "$SAVE" -build "$SRC" 2>&1 | grep -vE "warning CS(1701|0649|0169|0414)"
echo "build exit=${PIPESTATUS[0]}"; ls -la "$SAVE/Mods/Levy.tmod"

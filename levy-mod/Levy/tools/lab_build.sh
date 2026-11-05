#!/usr/bin/env bash
# Builds Levy into a PRIVATE tModLoader save dir (never the shared Mods dir).
# Usage: tools/lab_build.sh [-N] [-S saveDir]     -N = new toolchain (tML 2026.10; needed with Calamity)
# Needs <saveDir>/Mods/ThoriumMod.tmod (and CalamityMod*.tmod for -N Calamity runs) and <saveDir>/ModSources/tModLoader.targets.
set -u
NEW=""; SAVE=/home/user/tml-save-owner2
while getopts "NS:" o; do case $o in N) NEW=1;; S) SAVE=$OPTARG;; esac; done
HERE=$(cd "$(dirname "$0")/.." && pwd)
TML=/home/user/tml-bin/tml; [ -n "$NEW" ] && TML=/home/user/tml-bin/tml-2026.10
mkdir -p "$SAVE/Mods" "$SAVE/ModSources"
[ -f "$SAVE/ModSources/tModLoader.targets" ] || printf '<Project ToolsVersion="14.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">\n  <Import Project="%s/tMLMod.targets" />\n</Project>' "$TML" > "$SAVE/ModSources/tModLoader.targets"
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH
cd "$TML" && timeout 900 dotnet tModLoader.dll -server -nosteam -tmlsavedirectory "$SAVE" -build "$HERE" 2>&1 | grep -vE "warning CS(1701|0649|0169|0414)"
echo "build exit=${PIPESTATUS[0]}"; ls -la "$SAVE/Mods/Levy.tmod"

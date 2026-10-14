#!/usr/bin/env bash
# Build a .tmod with tModLoader's own headless build. Usage: tmlbuild.sh <mod dir>
set -u
MOD="${1:-/home/user/total-mobilization/levy}"
export DOTNET_ROOT=/home/user/dotnet; export PATH=/home/user/dotnet:$PATH
cd /home/user/tml-bin/tml && timeout 900 dotnet tModLoader.dll -server -build "$MOD" 2>&1 | grep -v "warning CS1701" 
RC=${PIPESTATUS[0]}
echo "tmlbuild exit=$RC"; ls -la ~/.local/share/Terraria/tModLoader/Mods/*.tmod 2>/dev/null
exit $RC

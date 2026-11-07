#!/usr/bin/env bash
# shipping-toolchain build + boot (no Calamity) of the options-lab Levy, private save dir /home/user/lab/shipo/save
set -u
W=/home/user/total-mobilization/.claude/worktrees/agent-ad11f62609ea192e9/levy-mod/Levy
S=/home/user/lab/shipo/save
rm -rf $S/ModSources/Levy; mkdir -p $S/ModSources; cp -r $W $S/ModSources/Levy; rm -rf $S/ModSources/Levy/bin $S/ModSources/Levy/obj $S/ModSources/Levy/docs
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH
cd /home/user/tml-bin/tml
timeout 900 dotnet tModLoader.dll -server -build $S/ModSources/Levy -tmlsavedirectory $S 2>&1 | grep -v "warning CS1701" | grep -E " error |Build error|built|Compiling" | head
ls -la $S/Mods/Levy.tmod
if [ "${1:-}" = boot ]; then
  timeout -k 10 240 dotnet tModLoader.dll -server -nosteam -tmlsavedirectory $S < /dev/null > /home/user/lab/shipo/boot.log 2>&1
  echo "boot exit=$?"
  echo "bad lines:"; grep -E "xception|TypeLoad|MissingMethod|malformed" /home/user/lab/shipo/boot.log | grep -v "proc/" | head
  grep -iE "fail=" /home/user/lab/shipo/boot.log | head -5
fi

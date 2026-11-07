#!/usr/bin/env bash
# Shipping-toolchain check (no Calamity): build Levy against a COPY of tml-save-hotfix, boot headless, scan the log.
set -u
W=/home/user/total-mobilization/.claude/worktrees/agent-a23640dd34068e3b6/levy-mod/Levy
S=/home/user/lab/savehfdud
export DOTNET_ROOT=/home/user/dotnet PATH=/home/user/dotnet:$PATH
rm -rf "$S"; cp -r /home/user/tml-save-hotfix "$S"
cd /home/user/tml-bin/tml || exit 1
echo "--- build"
timeout 900 dotnet tModLoader.dll -server -build "$W" -tmlsavedirectory "$S" 2>&1 | grep -v 'warning CS1701' | grep -E 'error|Build|built|Compiling' | head -10
ls -la "$S/Mods/Levy.tmod"
cat "$S/Mods/enabled.json"; echo
echo "--- boot"
LEVY_MODSHOT_AUDIT=1 timeout -k 10 240 dotnet tModLoader.dll -server -nosteam -tmlsavedirectory "$S" < /dev/null > "$S/boot.out" 2>&1
echo "boot exit=$? (124 = reached the world prompt)"
L=/home/user/tml-bin/tml/tModLoader-Logs/server.log
echo "exception-ish lines (excluding proc/): $(grep -iE 'xception|TypeLoad|MissingMethod|malformed' "$L" | grep -vc 'proc/')"
grep -iE 'xception|TypeLoad|MissingMethod|malformed' "$L" | grep -v 'proc/' | head -10 | cut -c1-250
echo "--- self checks"
grep -iE 'self ?check|fail=' "$L" | cut -c1-200 | head -40
echo "--- audit"
grep -E 'MODSHOT-AUDIT' "$L" | cut -c1-260 | head -80
cp "$L" /tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/dud2/shipcheck_server.log

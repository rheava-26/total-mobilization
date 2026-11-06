#!/usr/bin/env bash
# Turns the Magic Storage compat ON for a build: adds MagicStorage to build.txt's weakReferences and drops the marker file
# that makes Levy.csproj define MAGICSTORAGE (which compiles Common/Compat/MagicStorage/{MsDriver,ArmoryHeart}.cs).
# After this, building Levy needs MagicStorage.tmod (and its SerousCommonLib.tmod) in the Mods folder, like the Thorium weak reference does.
# Usage: enable_magicstorage.sh [on|off]   (run from anywhere)
set -eu
HERE="$(cd "$(dirname "$0")/../Levy" && pwd)"
MARK="$HERE/Common/Compat/MagicStorage/.enabled"
if [ "${1:-on}" = "off" ]; then
  rm -f "$MARK"
  sed -i -E 's/^(weakReferences = .*), ?MagicStorage/\1/' "$HERE/build.txt"
  echo "Magic Storage compat OFF"
else
  touch "$MARK"
  grep -q 'MagicStorage' "$HERE/build.txt" || sed -i -E 's/^(weakReferences = .*)$/\1, MagicStorage/' "$HERE/build.txt"
  grep '^weakReferences' "$HERE/build.txt"
  echo "Magic Storage compat ON"
fi

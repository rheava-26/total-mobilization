#!/usr/bin/env bash
# Build a mod into a tModLoader save's Mods/ folder.
#   build_mod.sh [-t 08|10] [-i INSTALL] [-s SAVE] <mod dir | Levy | Mechanization | Warbands>
#     -t 08 (default): tModLoader 2026.08 at $TML08, save $SAVE08.   -t 10: 2026.10 at $TML10, save $SAVE10.
#     -i / -s override the install and the save dir (the ship scripts build straight into their private ship save).
# Two build modes, chosen automatically:
#   tML mode  (Levy, Mechanization, Warbands, CalamityModMusic): `tModLoader -server -build <dir>`; the mod dir name is the mod name.
#   csproj mode (the .csproj uses Krafs.Publicizer: Calamity, MagicStorage, SerousCommonLib): `dotnet build` with the .NET 10 SDK;
#     the dir must sit in <save>/ModSources/ next to tModLoader.targets (setup_toolchain.sh arranges that).
# Weak references (Levy: ThoriumMod, MagicStorage) must already be in <save>/Mods or tML refuses with TML002.
set -u
. "$(dirname "${BASH_SOURCE[0]}")/env.sh"
TC=08; INSTALL=; SAVE=
while getopts "t:i:s:" o; do case $o in t) TC=$OPTARG;; i) INSTALL=$OPTARG;; s) SAVE=$OPTARG;; *) exit 64;; esac; done
shift $((OPTIND-1))
MOD="$(mod_src "${1:?usage: build_mod.sh [-t 08|10] [-i INSTALL] [-s SAVE] <mod dir|name>}")"
[ -d "$MOD" ] || { echo "no such mod dir: $MOD"; exit 64; }
if [ "$TC" = 10 ]; then : "${INSTALL:=$TML10}" "${SAVE:=$SAVE10}"; else : "${INSTALL:=$TML08}" "${SAVE:=$SAVE08}"; fi
mkdir -p "$SAVE/Mods"

if ls "$MOD"/*.csproj >/dev/null 2>&1 && grep -q -l Publicizer "$MOD"/*.csproj 2>/dev/null; then
  use_dotnet10
  # the generated tModLoader.targets points at the install the save was set up with; rewrite it for this install
  printf '<Project ToolsVersion="14.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">\n  <Import Project="%s/tMLMod.targets" />\n</Project>\n' "$INSTALL" > "$SAVE/ModSources/tModLoader.targets"
  cd "$MOD" && timeout 1800 dotnet build -c Release "-p:ExtraBuildModFlags=-tmlsavedirectory $SAVE" 2>&1 | grep -v -E "warning (CS|CA|NU|MSB|IDE|tMOD)|: warning " | tail -${TAIL:-12}
  RC=${PIPESTATUS[0]}
else
  use_dotnet8
  cd "$INSTALL" && timeout 900 dotnet tModLoader.dll -server -tmlsavedirectory "$SAVE" -build "$MOD" 2>&1 | grep -v "warning CS1701"
  RC=${PIPESTATUS[0]}
fi
NAME="$(basename "$MOD")"
if [ "$RC" = 0 ]; then ls -la "$SAVE/Mods/$NAME.tmod" 2>/dev/null; fi
echo "build_mod $NAME (tML 2026.$TC) exit=$RC"
exit $RC

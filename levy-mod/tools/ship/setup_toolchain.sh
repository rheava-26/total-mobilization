#!/usr/bin/env bash
# Rebuild the whole Levy / Mechanization / Warbands ship toolchain from scratch. Non-interactive and idempotent: every step is skipped
# when its result already exists (FORCE=1 redoes the builds). Safe to use as a cloud-environment setup script.
#
#   setup_toolchain.sh            install everything
#   setup_toolchain.sh --check    only report what is missing (exit 1 if anything is)
#
# Installs (all outside the repo; nothing third-party is ever committed):
#   /home/user/dotnet                  .NET 8 runtime                      <- https://dot.net/v1/dotnet-install.sh  (channel 8.0)
#   /home/user/dotnet10                .NET 10 SDK + the .NET 8 runtime    <- same script (channel 10.0)
#   /home/user/tml-bin/tml             tModLoader v2026.08.3.0 (shipping)  <- GitHub release asset tModLoader.zip
#   /home/user/tml-bin/tml-2026.10     tModLoader v2026.10.2.3 (Calamity)  <- GitHub release asset tModLoader.zip
#   /home/user/tml-save-2026.10/ModSources/{CalamityMod,CalamityModMusic,SerousCommonLib,MagicStorage}   public sources, pinned (below)
#   .../Mods/{CalamityMod,CalamityModMusic,SerousCommonLib,MagicStorage}.tmod   built here (Calamity needs the .NET 10 SDK)
#   ThoriumMod.tmod                    NOT open source and not downloadable here: see THORIUM below.
# Then: private ship installs /home/user/shipnorm (tML 2026.08) and /home/user/shipcal (tML 2026.10), each a COPY with its own save/.
#
# THORIUM: Levy weak-references ThoriumMod and compiles against it, so `tModLoader -build Levy` stops with TML002 ("Could not find
# ThoriumMod.tmod in your subscribed Workshop mods nor the Mods folder") until the published ThoriumMod.tmod (Steam Workshop 2909886416,
# 1.7.2.8) is in the Mods folders. The Workshop's file_url is empty for tModLoader items (SteamPipe only), so supply the file one of three ways:
#   THORIUM_TMOD=/path/ThoriumMod.tmod   or   copy it to /home/user/mods-cache/ThoriumMod.tmod   or   THORIUM_TMOD_URL=https://...
# The script still exits 0 without it (a failing setup script would stop a cloud session from starting) and says so loudly.
set -u
T_START=$SECONDS
. "$(dirname "${BASH_SOURCE[0]}")/env.sh"
CHECK=0; [ "${1:-}" = "--check" ] && CHECK=1
FORCE=${FORCE:-0}
MISSING=()

TML08_TAG=v2026.08.3.0
TML10_TAG=v2026.10.2.3
TML_URL="https://github.com/tModLoader/tModLoader/releases/download"
# public sources, pinned: the commit / tag the 2.2.2 + 2.1 Calamity builds and MagicStorage 0.7.0.11 / SerousCommonLib 1.0.7 came from
CAL_URL=https://github.com/CalamityTeam/CalamityModPublic.git;        CAL_REF=1a8cebd27ec5615316b78f71973446b5528d2b78   # branch 1.4.4, build.txt version 2.2.2
CALM_URL=https://github.com/CalamityTeam/CalamityModMusicPublic.git;  CALM_REF=cb64906735ad6ac58d55bd7fed2fc729925619dd # branch 1.4.4, build.txt version 2.1
MS_URL=https://github.com/blushiemagic/MagicStorage.git;              MS_REF=v0.7.0.11
SCL_URL=https://github.com/absoluteAquarian/SerousCommonLib.git;      SCL_REF=v1.0.7

say() { printf '\n[%3ds] == %s\n' $((SECONDS - T_START)) "$*"; }
need() { MISSING+=("$1"); echo "   MISSING: $1"; }
DL=$U/dl; mkdir -p "$DL"

# ---------------------------------------------------------------- .NET
dotnet_install_script() {
  [ -f "$DL/dotnet-install.sh" ] || curl -fsSL -o "$DL/dotnet-install.sh" https://dot.net/v1/dotnet-install.sh
}
say ".NET 8 runtime -> $DOTNET8"
if ls "$DOTNET8"/shared/Microsoft.NETCore.App/8.* >/dev/null 2>&1; then echo "   have it"
elif [ $CHECK = 1 ]; then need ".NET 8 runtime"
else dotnet_install_script && bash "$DL/dotnet-install.sh" --channel 8.0 --runtime dotnet --install-dir "$DOTNET8" | tail -2; fi

say ".NET 10 SDK (+ .NET 8 runtime) -> $DOTNET10"
if ls "$DOTNET10"/sdk/10.* >/dev/null 2>&1 && ls "$DOTNET10"/shared/Microsoft.NETCore.App/8.* >/dev/null 2>&1; then echo "   have it"
elif [ $CHECK = 1 ]; then need ".NET 10 SDK"
else
  dotnet_install_script
  ls "$DOTNET10"/sdk/10.* >/dev/null 2>&1 || bash "$DL/dotnet-install.sh" --channel 10.0 --install-dir "$DOTNET10" | tail -2
  # tML (net8) is launched by `dotnet` from this root during csproj builds, so the 8.0 runtime must live here too
  ls "$DOTNET10"/shared/Microsoft.NETCore.App/8.* >/dev/null 2>&1 || bash "$DL/dotnet-install.sh" --channel 8.0 --runtime dotnet --install-dir "$DOTNET10" | tail -2
fi

# ---------------------------------------------------------------- tModLoader installs
install_tml() { # $1 = tag, $2 = dest
  say "tModLoader $1 -> $2"
  if [ -f "$2/tModLoader.dll" ]; then echo "   have it"; return; fi
  if [ $CHECK = 1 ]; then need "tModLoader $1"; return; fi
  local zip="$DL/tml-$1.zip" tmp="$2.partial"
  [ -s "$zip" ] || curl -fsSL -o "$zip" "$TML_URL/$1/tModLoader.zip"
  rm -rf "$tmp"; mkdir -p "$tmp" && unzip -q -o "$zip" -d "$tmp" && mkdir -p "$(dirname "$2")" && rm -rf "$2" && mv "$tmp" "$2" && rm -f "$zip"
}
install_tml $TML08_TAG "$TML08"
install_tml $TML10_TAG "$TML10"

# ---------------------------------------------------------------- Calamity + MagicStorage + SerousCommonLib (from public source)
fetch_src() { # $1 = url, $2 = ref (tag or sha), $3 = dest
  [ -f "$3/build.txt" ] && return 0
  rm -rf "$3"; mkdir -p "$3"
  ( cd "$3" && git init -q && git remote add origin "$1" && git fetch -q --depth 1 origin "$2" && git -c advice.detachedHead=false checkout -q FETCH_HEAD ) \
    || { echo "   ERROR: could not fetch $1 @ $2"; rm -rf "$3"; return 1; }
}
build_src() { # $1 = mod name, $2 = url, $3 = ref, $4 = expected "version = x" line in build.txt
  say "$1 (public source @ ${3:0:12}, built with the .NET 10 SDK)"
  if [ -f "$SAVE10/Mods/$1.tmod" ] && [ "$FORCE" != 1 ]; then echo "   have $SAVE10/Mods/$1.tmod"; return; fi
  if [ $CHECK = 1 ]; then need "$1.tmod"; return; fi
  mkdir -p "$SAVE10/Mods" "$SAVE10/ModSources"
  fetch_src "$2" "$3" "$SAVE10/ModSources/$1" || { need "$1 source"; return; }
  grep -q "^version *= *$4" "$SAVE10/ModSources/$1/build.txt" || echo "   WARNING: $1 build.txt is not version $4"
  TAIL=4 "$SHIP_DIR/build_mod.sh" -t 10 "$SAVE10/ModSources/$1" || need "$1 build"
}
if [ -d "$TML10" ] || [ $CHECK = 1 ]; then
  mkdir -p "$SAVE10/ModSources"
  # tModLoader.targets (generated by tML on its first build) must exist for csproj builds; build_mod.sh rewrites it per install
  printf '<Project ToolsVersion="14.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">\n  <Import Project="%s/tMLMod.targets" />\n</Project>\n' "$TML10" > "$SAVE10/ModSources/tModLoader.targets"
  build_src SerousCommonLib "$SCL_URL" "$SCL_REF" 1.0.7
  # MagicStorage's csproj references ..\references\1.4.4\SerousCommonLib.dll, i.e. <ModSources>/references/1.4.4/
  if [ -f "$SAVE10/ModSources/SerousCommonLib/bin/Release/net8.0/SerousCommonLib.dll" ]; then
    mkdir -p "$SAVE10/ModSources/references/1.4.4"; cp -f "$SAVE10/ModSources/SerousCommonLib/bin/Release/net8.0/SerousCommonLib.dll" "$SAVE10/ModSources/references/1.4.4/"
  fi
  build_src MagicStorage "$MS_URL" "$MS_REF" 0.7.0.11
  # Calamity hard-depends on the music mod at runtime (MusicEventSystem calls .Value with no fallback); the source dir MUST be named CalamityModMusic / CalamityMod
  build_src CalamityModMusic "$CALM_URL" "$CALM_REF" 2.1
  build_src CalamityMod "$CAL_URL" "$CAL_REF" 2.2.2
fi

# ---------------------------------------------------------------- ThoriumMod (supplied, not downloaded)
say "ThoriumMod.tmod"
THORIUM_OK=0
if [ -n "${THORIUM_TMOD:-}" ] && [ -f "$THORIUM_TMOD" ]; then mkdir -p "$MODCACHE"; cp -f "$THORIUM_TMOD" "$MODCACHE/ThoriumMod.tmod"; fi
if [ ! -f "$MODCACHE/ThoriumMod.tmod" ] && [ -n "${THORIUM_TMOD_URL:-}" ] && [ $CHECK = 0 ]; then
  mkdir -p "$MODCACHE"; curl -fsSL -o "$MODCACHE/ThoriumMod.tmod" "$THORIUM_TMOD_URL" || rm -f "$MODCACHE/ThoriumMod.tmod"
fi
[ -f "$MODCACHE/ThoriumMod.tmod" ] && THORIUM_OK=1
for d in "$SAVE08/Mods" "$SAVE10/Mods"; do [ -f "$d/ThoriumMod.tmod" ] && THORIUM_OK=1; done
if [ $THORIUM_OK = 1 ] && [ $CHECK = 0 ]; then
  for d in "$SAVE08/Mods" "$SAVE10/Mods"; do mkdir -p "$d"; [ -f "$d/ThoriumMod.tmod" ] || { src="$MODCACHE/ThoriumMod.tmod"; [ -f "$src" ] || src="$(ls "$SAVE08/Mods/ThoriumMod.tmod" "$SAVE10/Mods/ThoriumMod.tmod" 2>/dev/null | head -1)"; cp -f "$src" "$d/ThoriumMod.tmod"; }; done
  echo "   ThoriumMod.tmod in place"
elif [ $THORIUM_OK = 0 ]; then
  need "ThoriumMod.tmod (put it at $MODCACHE/ThoriumMod.tmod, or set THORIUM_TMOD / THORIUM_TMOD_URL, then rerun)"
fi

# ---------------------------------------------------------------- the 2026.08 Mods folder + private ship installs
if [ $CHECK = 0 ]; then
  say "2026.08 build Mods folder $SAVE08/Mods (MagicStorage, SerousCommonLib for Levy's weak reference)"
  mkdir -p "$SAVE08/Mods"
  for m in SerousCommonLib MagicStorage; do [ -f "$SAVE10/Mods/$m.tmod" ] && copy_fresh "$SAVE10/Mods/$m.tmod" "$SAVE08/Mods/$m.tmod"; done
  say "private ship installs (copies, own save dirs)"
  ensure_install "$TML08" "$SHIPNORM" && mkdir -p "$SHIPNORM/save/Mods" "$VERIFY_SAVE/Mods"
  ensure_install "$TML10" "$SHIPCAL"  && mkdir -p "$SHIPCAL/save/Mods"
  [ -f "$SAVE10/Mods/CalamityMod.tmod" ] && sync_thirdparty "$SAVE10/Mods" "$SHIPCAL/save/Mods"
  ls -d "$SHIPNORM" "$SHIPCAL" "$VERIFY_SAVE" 2>/dev/null
fi

# ---------------------------------------------------------------- summary
say "done in $((SECONDS - T_START))s"
if [ ${#MISSING[@]} -gt 0 ]; then
  echo "INCOMPLETE:"; printf '  - %s\n' "${MISSING[@]}"
  [ $CHECK = 1 ] && exit 1
  exit 0
fi
echo "toolchain complete. Next: levy-mod/tools/ship/shipboth.sh"

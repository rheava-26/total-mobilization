#!/usr/bin/env bash
# Shared paths and helpers for the ship toolchain. Source it: . "$(dirname "$0")/env.sh"
# Everything lives OUTSIDE the repo (a container reset wipes it; setup_toolchain.sh rebuilds it).

SHIP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="${REPO:-$(cd "$SHIP_DIR/../../.." && pwd)}"          # repo root (works from any worktree)
LEVY_SRC="$REPO/levy-mod/Levy"
MECH_SRC="$REPO/mechanization-mod/Mechanization"
WARB_SRC="$REPO/warbands-mod/Warbands"

U=/home/user
DOTNET8=$U/dotnet                      # .NET 8 runtime (runs both tML builds)
DOTNET10=$U/dotnet10                   # .NET 10 SDK + the .NET 8 runtime (csproj builds of Calamity / MagicStorage)
TML08=$U/tml-bin/tml                   # tModLoader v2026.08.3.0 (shipping toolchain)
TML10=$U/tml-bin/tml-2026.10           # tModLoader v2026.10.2.3 (Calamity toolchain)
SAVE08="$HOME/.local/share/Terraria/tModLoader"   # default save dir of TML08 builds (Mods/, ModSources/)
SAVE10=$U/tml-save-2026.10             # save dir of TML10 builds (Mods/, ModSources/ with Calamity sources)
MODCACHE=$U/mods-cache                 # drop ThoriumMod.tmod here (not open source; see setup_toolchain.sh)

# PRIVATE ship installs: a copy of tML + its own save dir. tML writes its log into the INSTALL dir
# (tModLoader-Logs/), so a shared install means one boot overwrites another's log. Never hardlink into these.
SHIPNORM=$U/shipnorm                   # vanilla (tML 2026.08): tML copy, save/ inside
SHIPCAL=$U/shipcal                     # Calamity (tML 2026.10): tML copy, save/ inside
VERIFY_SAVE=$U/tml-save-verify         # save dir the vanilla boot runs against (Mods/ holds the built tmods)
LAB=$U/lab/shipworld                   # shipworld: world/ShipWorld.wld + per-run private installs

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

use_dotnet8()  { export DOTNET_ROOT=$DOTNET8;  export PATH=$DOTNET8:$PATH; }
use_dotnet10() { export DOTNET_ROOT=$DOTNET10; export PATH=$DOTNET10:$PATH; }

# The mods Eve ships, in build order (Mechanization and Warbands reference Levy.tmod from the Mods folder).
SHIP_MODS=(Levy Mechanization Warbands)
mod_src() { case "$1" in Levy) echo "$LEVY_SRC";; Mechanization) echo "$MECH_SRC";; Warbands) echo "$WARB_SRC";; *) echo "$1";; esac; }

# Copy (never link) a tree: removes the destination file first so a stray hardlink can never be written through.
copy_fresh() { local src="$1" dst="$2"; rm -f "$dst"; cp -f "$src" "$dst"; }

# Make a private tML install copy once. $1 = source install, $2 = destination.
ensure_install() {
  local src="$1" dst="$2"
  [ -f "$dst/tModLoader.dll" ] && return 0
  [ -f "$src/tModLoader.dll" ] || { echo "ERROR: $src is not a tML install (run setup_toolchain.sh)"; return 1; }
  mkdir -p "$dst" && cp -a "$src/." "$dst/" && rm -rf "$dst/tModLoader-Logs"
}

# Copy built tmods into a ship save. $1 = source Mods dir, $2 = dest Mods dir, rest = mod names.
sync_tmods() {
  local from="$1" to="$2"; shift 2; mkdir -p "$to"
  for m in "$@"; do
    [ -f "$from/$m.tmod" ] || { echo "ERROR: $from/$m.tmod missing"; return 1; }
    copy_fresh "$from/$m.tmod" "$to/$m.tmod"
  done
}

# Same, for the third-party tmods the ship saves need (everything in the source Mods dir that is not one of ours).
sync_thirdparty() {
  local from="$1" to="$2"; shift 2; mkdir -p "$to"
  for f in "$from"/*.tmod; do
    local b; b="$(basename "$f" .tmod)"
    case " ${SHIP_MODS[*]} " in *" $b "*) continue;; esac
    copy_fresh "$f" "$to/$b.tmod"
  done
}

write_enabled() { # $1 = Mods dir, rest = mod names (load order = list order)
  local dir="$1"; shift; local out="["; local first=1
  for m in "$@"; do [ $first = 1 ] || out+=","; out+="\"$m\""; first=0; done
  printf '%s]\n' "$out" > "$dir/enabled.json"
}

# ---------- log analysis (a tML log line looks like "[17:09:24.145] [Main Thread/INFO] [Levy]: text") ----------
EXC_RE='xception|TypeLoad|MissingMethod|malformed'
# self-check summary lines: pass=/fail= counters. "summon-staff pick self-check: N FAILURE(S)" is a known, pre-existing
# vanilla-only failure (armory ranking), reported separately and excluded from the "unexpected" set.
KNOWN_FAIL_RE='summon-staff pick self-check: 2 FAILURE\(S\)'
count_lines() { grep -c -E -i "$1" "$2" 2>/dev/null || true; }

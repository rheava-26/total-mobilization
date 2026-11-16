#!/usr/bin/env bash
# World-level self-checks on a fixed small world, vanilla and Calamity in parallel (~2 min).
#   shipworld.sh            (needs the built tmods: shipboth.sh / shipcal.sh leave them in $VERIFY_SAVE/Mods and $SHIPCAL/save/Mods)
# The world (seed "shipworld1", small) is made once at $LAB/world/ShipWorld.wld and never modified: every run boots a COPY.
# Each half runs on its own private copy of the tML install + a copied Mods folder (ports 7872 vanilla, 7873 Calamity), so the two
# servers (and any lab server) never share a log or a file. With LEVY_SELFCHECK_WORLD=1 the world checks run themselves once a world
# loads: Mechanization's MechWorldCheck (which calls Levy's SummonWorldCheck at its end) and LEVY_MECHWORLD_EXIT=1 stops the server.
# Overrides: VAN_MODS / CAL_MODS (comma lists), SHIPWORLD_SETTLE (seconds, default 25), SHIPWORLD_TIMEOUT,
#   SHIPWORLD_HALVES (default "van cal": which halves to run), SHIPWORLD_SERIAL=1 (run them one after another: one tML process at a time on a loaded box).
set -u
. "$(dirname "${BASH_SOURCE[0]}")/env.sh"
TMO=${SHIPWORLD_TIMEOUT:-480}; SETTLE=${SHIPWORLD_SETTLE:-25}
VAN_MODS=${VAN_MODS:-Levy,Mechanization,Warbands}
CAL_MODS=${CAL_MODS:-CalamityModMusic,CalamityMod,Levy,Mechanization,Warbands}
WORLD=$LAB/world/ShipWorld.wld
mkdir -p "$LAB/world"

# ---- 1. the world, once ----
if [ ! -f "$WORLD" ]; then
  echo "== making ShipWorld (seed shipworld1, small) =="
  GEN=$LAB/gen; rm -rf "$GEN"; mkdir -p "$GEN"; ensure_install "$TML08" "$GEN/tml" || exit 1
  mkdir -p "$GEN/save/Mods"; write_enabled "$GEN/save/Mods"      # no mods: plain vanilla world
  "$SHIP_DIR/tmlboot.sh" -i "$GEN/tml" -s "$GEN/save" -w "$GEN/ShipWorld.wld" -p "$PORT_GEN" -x 'Listening on port' -q 4 -t 600 \
      -- -autocreate 1 -worldname ShipWorld -seed shipworld1 -pass "" -secure 0
  # tML writes an autocreated world to <save>/Worlds/<worldname>.wld whatever -world says
  [ -f "$GEN/save/Worlds/ShipWorld.wld" ] && cp -f "$GEN/save/Worlds/ShipWorld.wld" "$WORLD"
  [ -f "$WORLD" ] || { echo "ERROR: world was not created; see $GEN/tml/boot.out"; tail -20 "$GEN/tml/boot.out"; exit 1; }
  rm -rf "$GEN"
fi
echo "world: $(ls -la "$WORLD" | awk '{print $5, $6, $7, $8}')"

# ---- 2. a private run dir per half ----
prep() { # $1 = name, $2 = tML base install, $3 = source Mods dir, $4 = mod list
  local run=$LAB/run-$1
  rm -rf "$run"; mkdir -p "$run"
  mkdir -p "$run/tml"; cp -a "$2/." "$run/tml/"
  rm -rf "$run/tml/tModLoader-Logs" "$run/tml/boot.out"
  mkdir -p "$run/save/Mods"
  for f in "$3"/*.tmod; do copy_fresh "$f" "$run/save/Mods/$(basename "$f")"; done      # copies, never links
  copy_fresh "$WORLD" "$run/ShipWorld.wld"
  echo "$run"
}
HALVES=${SHIPWORLD_HALVES:-van cal}; VR=; CR=
case " $HALVES " in *" van "*) VR=$(prep van "$TML08" "$VERIFY_SAVE/Mods");; esac
case " $HALVES " in *" cal "*) CR=$(prep cal "$TML10" "$SHIPCAL/save/Mods");; esac

run_half() { # $1 = run dir, $2 = port, $3 = mod list
  "$SHIP_DIR/tmlboot.sh" -i "$1/tml" -s "$1/save" -m "$3" -w "$1/ShipWorld.wld" -p "$2" \
      -e "LEVY_SELFCHECK_WORLD=1 LEVY_MECHWORLD_EXIT=1" -x '\[MechWorld\] self-check pass=' -q "$SETTLE" -t "$TMO" > "$1/half.out" 2>&1
  echo $? > "$1/half.rc"
}
echo "== booting ShipWorld: halves [$HALVES] ($PORT_WVAN vanilla, $PORT_WCAL Calamity), $([ "${SHIPWORLD_SERIAL:-0}" = 1 ] && echo one after another || echo in parallel) =="
T0=$SECONDS; PIDS=()
for h in $HALVES; do
  case $h in van) set -- "$VR" "$PORT_WVAN" "$VAN_MODS";; cal) set -- "$CR" "$PORT_WCAL" "$CAL_MODS";; *) continue;; esac
  if [ "${SHIPWORLD_SERIAL:-0}" = 1 ]; then run_half "$@"; else run_half "$@" & PIDS+=($!); fi
done
[ ${#PIDS[@]} -gt 0 ] && wait "${PIDS[@]}"
echo "halves done in $((SECONDS - T0))s"

# ---- 3. report ----
report() { # $1 = label, $2 = run dir
  local L="$2/tml/tModLoader-Logs/server.log" rc; rc=$(cat "$2/half.rc" 2>/dev/null || echo '?')
  echo "---- $1 (boot rc=$rc: 0 ok, 1 timeout, 2 server exited early) ----"
  if [ ! -f "$L" ]; then echo "NO LOG at $L"; return; fi
  local mp mf; mp=$(grep -c -F "[MechWorld] PASS " "$L"); mf=$(grep -c -F "[MechWorld] FAIL " "$L")
  echo "MechWorld: PASS=$mp FAIL=$mf"
  grep -F "[MechWorld] self-check pass=" "$L" | sed 's/^/  /'
  grep -F "[SummonWorld] self-check pass=" "$L" | sed 's/^/  /'
  echo "IdentityWorld: PASS=$(grep -c -F '[IdentityWorld] PASS ' "$L") FAIL=$(grep -c -F '[IdentityWorld] FAIL ' "$L")"
  grep -F "[IdentityWorld] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[IdentityWorld] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "OfficerWorld: PASS=$(grep -c -F '[OfficerWorld] PASS ' "$L") FAIL=$(grep -c -F '[OfficerWorld] FAIL ' "$L")"
  grep -F "[OfficerWorld] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[OfficerWorld] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "WhipReal: PASS=$(grep -c -F '[WhipReal] PASS ' "$L") FAIL=$(grep -c -F '[WhipReal] FAIL ' "$L")"
  grep -F "[WhipReal] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[WhipReal] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "ShopLag: PASS=$(grep -c -F '[ShopLag] PASS ' "$L") FAIL=$(grep -c -F '[ShopLag] FAIL ' "$L")"
  grep -F "[ShopLag] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[ShopLag] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "PylonClick: PASS=$(grep -c -F '[PylonClick] PASS ' "$L") FAIL=$(grep -c -F '[PylonClick] FAIL ' "$L")"
  grep -F "[PylonClick] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[PylonClick] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "HitParity: PASS=$(grep -c -F '[HitParity] PASS ' "$L") FAIL=$(grep -c -F '[HitParity] FAIL ' "$L")"
  grep -F "[HitParity] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[HitParity] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "SwapRace: PASS=$(grep -c -F '[SwapRace] PASS ' "$L") FAIL=$(grep -c -F '[SwapRace] FAIL ' "$L")"
  grep -F "[SwapRace] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[SwapRace] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "BossBugs: PASS=$(grep -c -F '[BossBugs] PASS ' "$L") FAIL=$(grep -c -F '[BossBugs] FAIL ' "$L")"
  grep -F "[BossBugs] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[BossBugs] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "LevyRules: PASS=$(grep -c -F '[LevyRules] PASS ' "$L") FAIL=$(grep -c -F '[LevyRules] FAIL ' "$L")"
  grep -F "[LevyRules] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[LevyRules] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "PerfBoss: PASS=$(grep -c -F '[PerfBoss] PASS ' "$L") FAIL=$(grep -c -F '[PerfBoss] FAIL ' "$L")"
  grep -F "[PerfBoss] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[PerfBoss] OBSERVED' "$L" | head -8 | cut -c1-240 | sed 's/^/  /'
  grep -F '[PerfBoss] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "TileSweep: PASS=$(grep -c -F '[TileSweep] PASS ' "$L") FAIL=$(grep -c -F '[TileSweep] FAIL ' "$L")"
  grep -F "[TileSweep] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[TileSweep] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "StealthWorld (Calamity rogue commander with and without an army): PASS=$(grep -c -F '[StealthWorld] PASS ' "$L") FAIL=$(grep -c -F '[StealthWorld] FAIL ' "$L")"
  grep -F "[StealthWorld] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[StealthWorld] OBSERVED' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-700 | sed 's/^/  /'
  grep -F '[StealthWorld] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "LineWatch (the line-breaking warning with a real army): PASS=$(grep -c -F '[LineWatch] PASS ' "$L") FAIL=$(grep -c -F '[LineWatch] FAIL ' "$L")"
  grep -F "[LineWatch] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[LineWatch] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "FightReport (boot self-check of the analyzer): $(grep -F '[FightReport] self-check pass=' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | head -1)"
  [ -z "$(grep -F '[MechWorld] self-check pass=' "$L")" ] && echo "  !! no MechWorld summary line (check did not finish; see $2/tml/boot.out)"
  echo "other failures (any FAIL / fail=[1-9] / !!! line not listed above), known summon-staff pick failure excluded:"
  fail_lines "$L" | grep -v -F -e '[MechWorld] FAIL ' -e '[IdentityWorld] FAIL ' -e '[OfficerWorld] FAIL ' -e '[WhipReal] FAIL ' -e '[ShopLag] FAIL ' -e '[PylonClick] FAIL ' -e '[TileSweep] FAIL ' -e '[HitParity] FAIL ' -e '[BossBugs] FAIL ' -e '[SwapRace] FAIL ' -e '[LevyRules] FAIL ' -e '[PerfBoss] FAIL ' -e '[StealthWorld] FAIL ' -e '[LineWatch] FAIL ' | sed 's/^/  /' | head -20
  grep -E "$KNOWN_FAIL_RE" "$L" | head -1 | sed 's/^/  (known, excluded) /'
  echo "FAIL lines in the whole log (known excluded): $(fail_lines "$L" | wc -l)"
  local ex; ex=$(grep -c -E -i "$EXC_RE" "$L"); echo "exceptions: $ex"
  grep -E -i "$EXC_RE" "$L" | head -6 | cut -c1-220 | sed 's/^/  /'
  echo "player-safety: PASS=$(grep -c -F '[MechWorld] PASS player-safety' "$L") FAIL=$(grep -c -F '[MechWorld] FAIL player-safety' "$L")"
  grep -F '[MechWorld] FAIL' "$L" | head -10 | cut -c1-220 | sed 's/^/  /'
  echo "distinct self-check summary lines: $(grep -E 'self-check.*(pass=|fail=|ok|OK|FAILURE)' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | sort -u | wc -l)"
}
case " $HALVES " in *" van "*) report "VANILLA ($VAN_MODS)" "$VR";; esac
case " $HALVES " in *" cal "*) report "CALAMITY ($CAL_MODS)" "$CR";; esac

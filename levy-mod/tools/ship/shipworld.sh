#!/usr/bin/env bash
# World-level self-checks on a fixed small world, vanilla and Calamity in parallel (~2 min).
#   shipworld.sh            (needs the built tmods: shipboth.sh / shipcal.sh leave them in $VERIFY_SAVE/Mods and $SHIPCAL/save/Mods)
# The world (seed "shipworld1", small) is made once at $LAB/world/ShipWorld.wld and never modified: every run boots a COPY.
# Each half runs on its own private copy of the tML install + a copied Mods folder (ports 7872 vanilla, 7873 Calamity), so the two
# servers (and any lab server) never share a log or a file. With LEVY_SELFCHECK_WORLD=1 the world checks run themselves once a world
# loads: Mechanization's MechWorldCheck (which calls Levy's SummonWorldCheck at its end) and LEVY_MECHWORLD_EXIT=1 stops the server.
# Overrides: VAN_MODS / CAL_MODS (comma lists), SHIPWORLD_SETTLE (seconds, default 25), SHIPWORLD_TIMEOUT.
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
  "$SHIP_DIR/tmlboot.sh" -i "$GEN/tml" -s "$GEN/save" -w "$GEN/ShipWorld.wld" -p 7871 -x 'Listening on port' -q 4 -t 600 \
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
VR=$(prep van "$TML08" "$VERIFY_SAVE/Mods"); CR=$(prep cal "$TML10" "$SHIPCAL/save/Mods")

run_half() { # $1 = run dir, $2 = port, $3 = mod list
  "$SHIP_DIR/tmlboot.sh" -i "$1/tml" -s "$1/save" -m "$3" -w "$1/ShipWorld.wld" -p "$2" \
      -e "LEVY_SELFCHECK_WORLD=1 LEVY_MECHWORLD_EXIT=1" -x '\[MechWorld\] self-check pass=' -q "$SETTLE" -t "$TMO" > "$1/half.out" 2>&1
  echo $? > "$1/half.rc"
}
echo "== booting ShipWorld: vanilla (7872) and Calamity (7873) in parallel =="
T0=$SECONDS
run_half "$VR" 7872 "$VAN_MODS" & P1=$!
run_half "$CR" 7873 "$CAL_MODS" & P2=$!
wait $P1 $P2
echo "both halves done in $((SECONDS - T0))s"

# ---- 3. report ----
report() { # $1 = label, $2 = run dir
  local L="$2/tml/tModLoader-Logs/server.log" rc; rc=$(cat "$2/half.rc" 2>/dev/null || echo '?')
  echo "---- $1 (boot rc=$rc: 0 ok, 1 timeout, 2 server exited early) ----"
  if [ ! -f "$L" ]; then echo "NO LOG at $L"; return; fi
  local mp mf; mp=$(grep -c -F "[MechWorld] PASS " "$L"); mf=$(grep -c -F "[MechWorld] FAIL " "$L")
  echo "MechWorld: PASS=$mp FAIL=$mf"
  grep -F "[MechWorld] self-check pass=" "$L" | sed 's/^/  /'
  grep -F "[SummonWorld] self-check pass=" "$L" | sed 's/^/  /'
  [ -z "$(grep -F '[MechWorld] self-check pass=' "$L")" ] && echo "  !! no MechWorld summary line (check did not finish; see $2/tml/boot.out)"
  echo "other failures (fail=[1-9] / FAILURE / !!!), known summon-staff pick failure excluded:"
  grep -E 'fail=[1-9]|FAILURE|!!!' "$L" | grep -v -E "$KNOWN_FAIL_RE" | grep -v -F -e '[MechWorld] FAIL ' -e '[SummonWorld] FAIL ' | sed 's/^/  /' | head -20
  grep -E "$KNOWN_FAIL_RE" "$L" | head -1 | sed 's/^/  (known, excluded) /'
  local ex; ex=$(grep -c -E -i "$EXC_RE" "$L"); echo "exceptions: $ex"
  grep -E -i "$EXC_RE" "$L" | head -6 | cut -c1-220 | sed 's/^/  /'
  echo "player-safety: PASS=$(grep -c -F '[MechWorld] PASS player-safety' "$L") FAIL=$(grep -c -F '[MechWorld] FAIL player-safety' "$L")"
  grep -F '[MechWorld] FAIL' "$L" | head -10 | cut -c1-220 | sed 's/^/  /'
  echo "distinct self-check summary lines: $(grep -E 'self-check.*(pass=|fail=|ok|OK|FAILURE)' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | sort -u | wc -l)"
}
report "VANILLA ($VAN_MODS)" "$VR"
report "CALAMITY ($CAL_MODS)" "$CR"

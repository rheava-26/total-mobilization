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
WOT_MODS=${WOT_MODS:-CalamityModMusic,CalamityMod,Daybreak,Luminance,SubworldLibrary,NoxusBoss,Fargowiltas,miningcracks_take_on_luiafk,Levy,Mechanization,Warbands}   # optional "wot" half (shipwotg.sh --world, SHIPWORLD_HALVES=wot)
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

case " $HALVES " in *" wot "*) WR=$(prep wot "$TML10" "$WOTG_HOME/save/Mods");; esac

run_half() { # $1 = run dir, $2 = port, $3 = mod list
  "$SHIP_DIR/tmlboot.sh" -i "$1/tml" -s "$1/save" -m "$3" -w "$1/ShipWorld.wld" -p "$2" \
      -e "LEVY_SELFCHECK_WORLD=1 LEVY_MECHWORLD_EXIT=1" -x '\[MechWorld\] self-check pass=' -q "$SETTLE" -t "$TMO" > "$1/half.out" 2>&1
  echo $? > "$1/half.rc"
}
echo "== booting ShipWorld: halves [$HALVES] ($PORT_WVAN vanilla, $PORT_WCAL Calamity), $([ "${SHIPWORLD_SERIAL:-0}" = 1 ] && echo one after another || echo in parallel) =="
T0=$SECONDS; PIDS=()
for h in $HALVES; do
  case $h in van) set -- "$VR" "$PORT_WVAN" "$VAN_MODS";; cal) set -- "$CR" "$PORT_WCAL" "$CAL_MODS";; wot) set -- "$WR" "$PORT_WWOT" "$WOT_MODS";; *) continue;; esac
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
  echo "MobWorld (wings, boots, floors, dashes): PASS=$(grep -c -F '[MobWorld] PASS ' "$L") FAIL=$(grep -c -F '[MobWorld] FAIL ' "$L")"
  grep -F "[MobWorld] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[MobWorld] FAIL' "$L" | head -10 | cut -c1-300 | sed 's/^/  /'
  echo "Bodyguard (a boss's target draws it away from the commander): PASS=$(grep -c -F '[Bodyguard] PASS ' "$L") FAIL=$(grep -c -F '[Bodyguard] FAIL ' "$L")"
  grep -F "[Bodyguard] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[Bodyguard] FAIL' "$L" | head -10 | cut -c1-300 | sed 's/^/  /'
  echo "WhipReal: PASS=$(grep -c -F '[WhipReal] PASS ' "$L") FAIL=$(grep -c -F '[WhipReal] FAIL ' "$L")"
  grep -F "[WhipReal] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[WhipReal] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "FacingWorld (a levy's lash, swing and shot do the same whichever way the commander faces): PASS=$(grep -c -F '[FacingWorld] PASS ' "$L") FAIL=$(grep -c -F '[FacingWorld] FAIL ' "$L")"
  grep -F "[FacingWorld] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[FacingWorld] FAIL' "$L" | head -10 | cut -c1-300 | sed 's/^/  /'
  echo "HoldoutWorld (charge and holdout guns fire from the levy with the commander 64 tiles away, the Ontological Despoiler's modes and swaps, a sample of held guns; Calamity parts skip without Calamity): PASS=$(grep -c -F '[HoldoutWorld] PASS ' "$L") FAIL=$(grep -c -F '[HoldoutWorld] FAIL ' "$L")"
  grep -F "[HoldoutWorld] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[HoldoutWorld] OBSERVED despoiler' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-400 | sed 's/^/  /'
  grep -F '[HoldoutWorld] FAIL' "$L" | head -10 | cut -c1-300 | sed 's/^/  /'
  echo "SummonSweep (summon staves in a summoner levy's hands, the commander 64 tiles away: summons at the levy, damage, nothing on the commander, minions that grow with repeated casts, the Saros Possession's ray at full slots; Calamity parts skip without Calamity): PASS=$(grep -c -F '[SummonSweep] PASS ' "$L") FAIL=$(grep -c -F '[SummonSweep] FAIL ' "$L")"
  grep -F "[SummonSweep] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[SummonSweep] FAIL' "$L" | head -10 | cut -c1-300 | sed 's/^/  /'
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
  echo "LostFight (the commander dies in a boss fight: a beaten boss fights on against levies in its leash, the army rallies where he fell; a first fight, a wiped army and a respawn end it): PASS=$(grep -c -F '[LostFight] PASS ' "$L") FAIL=$(grep -c -F '[LostFight] FAIL ' "$L")"
  grep -F "[LostFight] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[LostFight] OBSERVED' "$L" | grep -v -F 'trace' | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-300 | sed 's/^/  /'
  grep -F '[LostFight] FAIL' "$L" | head -10 | cut -c1-300 | sed 's/^/  /'
  echo "MusterBoss (Auto-muster for boss fights: on musters for a boss, off answers events only): PASS=$(grep -c -F '[MusterBoss] PASS ' "$L") FAIL=$(grep -c -F '[MusterBoss] FAIL ' "$L")"
  grep -F "[MusterBoss] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[MusterBoss] FAIL' "$L" | head -10 | cut -c1-300 | sed 's/^/  /'
  echo "LevyRules: PASS=$(grep -c -F '[LevyRules] PASS ' "$L") FAIL=$(grep -c -F '[LevyRules] FAIL ' "$L")"
  grep -F "[LevyRules] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[LevyRules] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "ArmoryRulesWorld (the armory's rules decide who gets which weapon: guns and every other kind and class, armor of every class in stock, Re-arm and the upgrade passes, save and wire, a standing army): PASS=$(grep -c -F '[ArmoryRulesWorld] PASS ' "$L") FAIL=$(grep -c -F '[ArmoryRulesWorld] FAIL ' "$L")"
  grep -F "[ArmoryRulesWorld] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[ArmoryRulesWorld] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "OnlyRule (the category Only mode: nothing outside the kind is issued, nobody left unarmed, class balance above and below, two Only blocks, save, wire and an old save; a new category rule starts as Include): PASS=$(grep -c -F '[OnlyRule] PASS ' "$L") FAIL=$(grep -c -F '[OnlyRule] FAIL ' "$L")"
  grep -F "[OnlyRule] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[OnlyRule] FAIL' "$L" | head -10 | cut -c1-300 | sed 's/^/  /'
  echo "AllyTags (name tags over the army's allies: a levy's minion and sentry, an officer's creature, Mechanization's turret and drone, the settings, a cached frame allocates nothing): PASS=$(grep -c -F '[AllyTags] PASS ' "$L") FAIL=$(grep -c -F '[AllyTags] FAIL ' "$L")"
  grep -F "[AllyTags] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[AllyTags] OBSERVED' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-300 | sed 's/^/  /'
  grep -F '[AllyTags] FAIL' "$L" | head -10 | cut -c1-300 | sed 's/^/  /'
  echo "SidearmWorld (a levy in class armor carries a sidearm of his class, never his primary's type; rules win over armor; pre-Hardmode off-class allowed when much better): PASS=$(grep -c -F '[SidearmWorld] PASS ' "$L") FAIL=$(grep -c -F '[SidearmWorld] FAIL ' "$L")"
  grep -F "[SidearmWorld] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[SidearmWorld] OBSERVED' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-300 | sed 's/^/  /'
  grep -F '[SidearmWorld] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "ArmorClassWorld (a levy's armor matches his weapon's class: Stardust and Nebula over Solar Flare, Spectre Mask, Silva hoods over God Slayer, an army in the wrong armor swaps on its upgrade pass, shared pieces, nothing duplicated): PASS=$(grep -c -F '[ArmorClassWorld] PASS ' "$L") FAIL=$(grep -c -F '[ArmorClassWorld] FAIL ' "$L")"
  grep -F "[ArmorClassWorld] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[ArmorClassWorld] FAIL' "$L" | head -12 | cut -c1-300 | sed 's/^/  /'
  echo "LoadoutFit (one class per levy: primary, sidearm, armor and class accessories; Eve's post-Yharon army and stock with her rules, a vanilla equivalent, convergence in one pass, no thrash, nothing lost): PASS=$(grep -c -F '[LoadoutFit] PASS ' "$L") FAIL=$(grep -c -F '[LoadoutFit] FAIL ' "$L")"
  grep -F "[LoadoutFit] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[LoadoutFit] FAIL' "$L" | head -12 | cut -c1-300 | sed 's/^/  /'
  echo "SetAbility (armor set abilities a key starts: the God Slayer dash from the levy through the enemy line, its cooldown and dodge, the Solar Flare dash, the Forbidden storm on the target, Vortex stealth, Calamity's set key, the setting; the commander 70 tiles away and a levy in the line untouched): PASS=$(grep -c -F '[SetAbility] PASS ' "$L") FAIL=$(grep -c -F '[SetAbility] FAIL ' "$L")"
  grep -F "[SetAbility] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[SetAbility] OBSERVED' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-400 | sed 's/^/  /'
  grep -F '[SetAbility] FAIL' "$L" | head -10 | cut -c1-300 | sed 's/^/  /'
  echo "SaveHold (a world save mid-check: fixtures save-safe, the autosave held off and given back): PASS=$(grep -c -F '[SaveHold] PASS ' "$L") FAIL=$(grep -c -F '[SaveHold] FAIL ' "$L")"
  grep -F "[SaveHold] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[SaveHold] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "MachineDorm (machines far from every player pack and wake as they were, save and load, the garrison sleeps, the boss stand-down holds launches and gives them back): PASS=$(grep -c -F '[MachineDorm] PASS ' "$L") FAIL=$(grep -c -F '[MachineDorm] FAIL ' "$L")"
  grep -F "[MachineDorm] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[MachineDorm] OBSERVED' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-400 | sed 's/^/  /'
  grep -F '[MachineDorm] FAIL' "$L" | head -10 | cut -c1-300 | sed 's/^/  /'
  echo "ActiveArmy (active army size: the cap, same-tick replacement, save and load, beside the boss room-making, the live notice): PASS=$(grep -c -F '[ActiveArmy] PASS ' "$L") FAIL=$(grep -c -F '[ActiveArmy] FAIL ' "$L")"
  grep -F "[ActiveArmy] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[ActiveArmy] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "PerfBoss: PASS=$(grep -c -F '[PerfBoss] PASS ' "$L") FAIL=$(grep -c -F '[PerfBoss] FAIL ' "$L")"
  grep -F "[PerfBoss] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[PerfBoss] OBSERVED' "$L" | head -8 | cut -c1-240 | sed 's/^/  /'
  grep -F '[PerfBoss] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "HookLive (every detour of Levy, Mechanization and Warbands ran during the chain, or is listed with the scenario that needs it): PASS=$(grep -c -F '[HookLive] PASS ' "$L") FAIL=$(grep -c -F '[HookLive] FAIL ' "$L")"
  grep -F "[HookLive] self-check pass=" "$L" | sed 's/^/  /'
  grep -F "[HookLive] OBSERVED" "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-1200 | sed 's/^/  /'
  grep -F '[HookLive] FAIL' "$L" | head -20 | cut -c1-260 | sed 's/^/  /'
  echo "PerfAnalyzer (always-on profiler: attribution by mod, fault safety, fights and popup, rolling log; its cost on and off is the [PerfBoss] analyzer line): PASS=$(grep -c -F '[PerfAnalyzer] PASS ' "$L") FAIL=$(grep -c -F '[PerfAnalyzer] FAIL ' "$L")"
  grep -F "[PerfAnalyzer] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[PerfAnalyzer] OBSERVED' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-520 | head -24 | sed 's/^/  /'
  grep -F '[PerfBoss] OBSERVED analyzer' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-700 | sed 's/^/  /'
  grep -F '[PerfBoss] PASS the analyzer' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-400 | sed 's/^/  /'
  grep -F '[PerfAnalyzer] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "ProgAssist (Calamity progression options: magnet, agitator, ore multiplier, Auric, Uelibloom, Providence drops; each off and on): PASS=$(grep -c -F '[ProgAssist] PASS ' "$L") FAIL=$(grep -c -F '[ProgAssist] FAIL ' "$L")"
  grep -F "[ProgAssist] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[ProgAssist] OBSERVED' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-400 | sed 's/^/  /'
  grep -F '[ProgAssist] FAIL' "$L" | head -10 | cut -c1-300 | sed 's/^/  /'
  echo "AutoSell (Armory autosell: marked items sold into the Barracks treasury at their sell price, keep N, favourited / rule-named / in-use never sold, the armory's own tick, save, load and wire; the 1 platinum withdraw and deposit): PASS=$(grep -c -F '[AutoSell] PASS ' "$L") FAIL=$(grep -c -F '[AutoSell] FAIL ' "$L")"
  grep -F "[AutoSell] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[AutoSell] OBSERVED' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-300 | sed 's/^/  /'
  grep -F '[AutoSell] FAIL' "$L" | head -10 | cut -c1-300 | sed 's/^/  /'
  echo "TileSweep: PASS=$(grep -c -F '[TileSweep] PASS ' "$L") FAIL=$(grep -c -F '[TileSweep] FAIL ' "$L")"
  grep -F "[TileSweep] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[TileSweep] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "StealthWorld (Calamity rogue commander with and without an army): PASS=$(grep -c -F '[StealthWorld] PASS ' "$L") FAIL=$(grep -c -F '[StealthWorld] FAIL ' "$L")"
  grep -F "[StealthWorld] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[StealthWorld] OBSERVED' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-700 | sed 's/^/  /'
  grep -F '[StealthWorld] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "PetWorld (Calamity: pets and helpers a levy's gear makes are his, follow him and end with him; the commander's own pet stays hers; the hide setting): PASS=$(grep -c -F '[PetWorld] PASS ' "$L") FAIL=$(grep -c -F '[PetWorld] FAIL ' "$L")"
  grep -F "[PetWorld] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[PetWorld] OBSERVED' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-400 | sed 's/^/  /'
  grep -F '[PetWorld] FAIL' "$L" | head -10 | cut -c1-300 | sed 's/^/  /'
  echo "LineWatch (the line-breaking warning with a real army): PASS=$(grep -c -F '[LineWatch] PASS ' "$L") FAIL=$(grep -c -F '[LineWatch] FAIL ' "$L")"
  grep -F "[LineWatch] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[LineWatch] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "EventRec (event recordings: Goblin Army, Pumpkin Moon, pillars, a boss alongside, caps, army wiped, a fight that travels, cost): PASS=$(grep -c -F '[EventRec] PASS ' "$L") FAIL=$(grep -c -F '[EventRec] FAIL ' "$L")"
  grep -F "[EventRec] world self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[EventRec] OBSERVED' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | cut -c1-700 | sed 's/^/  /'
  grep -F '[EventRec] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  grep -F "[FightRecAuto] world self-check pass=" "$L" | sed 's/^/  /'
  echo "ChatterWorld (eclipse-end chatter, shared-upgrade lines): PASS=$(grep -c -F '[ChatterWorld] PASS ' "$L") FAIL=$(grep -c -F '[ChatterWorld] FAIL ' "$L")"
  grep -F "[ChatterWorld] self-check pass=" "$L" | sed 's/^/  /'
  grep -F '[ChatterWorld] FAIL' "$L" | head -10 | cut -c1-260 | sed 's/^/  /'
  echo "FightReport (boot self-check of the analyzer): $(grep -F '[FightReport] self-check pass=' "$L" | sed -E 's/^\[[^]]*\] \[[^]]*\] //' | head -1)"
  [ -z "$(grep -F '[MechWorld] self-check pass=' "$L")" ] && echo "  !! no MechWorld summary line (check did not finish; see $2/tml/boot.out)"
  echo "other failures (any FAIL / fail=[1-9] / !!! line not listed above), known summon-staff pick failure excluded:"
  fail_lines "$L" | grep -v -F -e '[MechWorld] FAIL ' -e '[IdentityWorld] FAIL ' -e '[OfficerWorld] FAIL ' -e '[WhipReal] FAIL ' -e '[ShopLag] FAIL ' -e '[PylonClick] FAIL ' -e '[TileSweep] FAIL ' -e '[HitParity] FAIL ' -e '[BossBugs] FAIL ' -e '[SwapRace] FAIL ' -e '[LevyRules] FAIL ' -e '[PerfBoss] FAIL ' -e '[StealthWorld] FAIL ' -e '[LineWatch] FAIL ' -e '[PerfAnalyzer] FAIL ' -e '[ChatterWorld] FAIL ' -e '[SaveHold] FAIL ' -e '[EventRec] FAIL ' -e '[HookLive] FAIL ' -e '[MobWorld] FAIL ' -e '[Bodyguard] FAIL ' -e '[ProgAssist] FAIL ' -e '[AutoSell] FAIL ' -e '[ArmoryRulesWorld] FAIL ' -e '[SidearmWorld] FAIL ' -e '[FacingWorld] FAIL ' -e '[MachineDorm] FAIL ' -e '[LostFight] FAIL ' -e '[MusterBoss] FAIL ' -e '[PetWorld] FAIL ' -e '[ArmorClassWorld] FAIL ' -e '[LoadoutFit] FAIL ' -e '[HoldoutWorld] FAIL ' -e '[OnlyRule] FAIL ' -e '[AllyTags] FAIL ' -e '[ActiveArmy] FAIL ' -e '[SetAbility] FAIL ' -e '[SummonSweep] FAIL ' | head -20
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
case " $HALVES " in *" wot "*) report "WOTG ($WOT_MODS)" "$WR";; esac

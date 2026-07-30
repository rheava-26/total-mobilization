// AUTONOMOUS HOME DEFENSE — docs/CONCEPT.md Pillar 1 ("The Nation Acts":
// "Militias raise themselves in threatened population centers — a posture
// you enable, not units you click out") crossed with Pillar 2 ("Mobilization
// IS the Economy"). This is the module that actually makes the nation
// mobilize ALONGSIDE the player instead of the player being the only thing
// that ever raises a defender: every player-owned city grows its own "Home
// Guard" militia battalion on its own, with zero clicks, at a pace driven by
// game/economy.js's mobilizationLevel (which itself already rises on its own
// — see that file's header) and gated by the same manpower pool every other
// system draws from.
//
// WHAT THIS FILE IS NOT: it does not reimplement garrisoning, combat, or
// morale. A Home Guard battalion is an completely ordinary player battalion
// (game/battalions.js's record shape) with `bn.homeSector` pinned to its
// city's index — game/battalionDoctrine.js already knows exactly what to do
// with that (deploys sub-units into the city, infantry biased into the
// buildings — B5b), and game/battalionMorale.js already grants it the
// garrison defense bonus, side-agnostically, for free. This module's ONLY
// job is deciding WHEN and HOW MANY militia sub-units to add to that
// battalion, and paying manpower for each one.
//
// THE MECHANIC, in one paragraph: for each player-owned city, compute a
// TARGET Home Guard size from the city's own size (`c.r`) and the nation's
// current mobilizationLevel (0..100) — bigger city + more mobilized nation =
// bigger target, capped per city so no amount of mobilization makes a single
// city invincible on its own. Grow the battalion toward that target ONE
// militia at a time on a slow timer (steady mobilization, not a pop-in), and
// pay MANPOWER_PER_MILITIA out of the real economy.manpower pool for each
// one — if the pool can't afford it, mustering simply stalls until it can
// (home defense competes for manpower with production/reinforcements/every
// other manpower sink, it doesn't print its own). A city under detected
// threat musters faster (an "emergency levy"), same "the nation wakes up
// when attacked" flavor as economy.js's own threatLevel/THREAT_RAMP_COEFF.
//
// BALANCE GUARD (see this module's TUNABLES below and the orchestrator's
// balance-harness pass): mobilizationLevel is LOW early (little prep time,
// nothing raised yet) and only reaches its high bands after real elapsed
// time/threat exposure (economy.js's ramp), so an unprepared player still
// has a thin-to-empty Home Guard when the invasion actually lands — this
// layer CONTESTS cities (casualties happen, no more silent walkovers) but a
// bare nation with no other prep still loses. A fully-mobilized nation gets a
// meaningful garrison in every city, but PER_R_AT_FULL_MOB/CAP_PER_CITY below
// are picked modest on purpose: this is a contest-multiplier, not a
// stand-alone army capable of repelling a full invasion by itself.
//
// CALL CONTRACT: updateHomeDefense(world, economy, map, dt) once per frame
// from main.js's loop(), alongside the other economy/battalion updates —
// placed AFTER updateEconomy so it reads this frame's freshly-advanced
// mobilizationLevel, not last frame's. Runs in BOTH phases (PREP and
// COMBAT), same "no phase gate" convention as updateBattalionDoctrine/
// updateReinforcements: the whole point is that cities are already filling
// with defenders DURING prep, not just once combat starts.

import { createEmptyBattalion, addUnitsToBattalion } from './battalions.js';
import { cityCenterWorld } from './objectives.js';
import { detects } from './fog.js';

// ---------------------------------------------------------------------------
// TUNABLES — DESIGNER'S TO TUNE, first-pass placeholders. These are the exact
// knobs the orchestrator's balance harness should retune against:
export const HOME_DEFENSE_TUNABLES = {
  // Throttle cadence for the per-city scan — cheap (O(owned cities)), no
  // need to run every frame; same "reads as the nation redeployed itself,
  // not twitchy" reasoning as battalionDoctrine.js's ASSIGN_INTERVAL_S.
  SCAN_INTERVAL_S: 3,

  // KNOB 1 — how strong a fully-mobilized nation's home guard gets: militia
  // sub-units targeted per unit of city.r (city "size" proxy, same one
  // economy.js's IC/manpower formulas already use) AT mobilizationLevel=100.
  // Cities run r=6..10 (game/mapgen.js; capital is always the biggest at 10)
  // — at 1.0 that's a target of 10 for the capital, 6 for a normal city, once
  // fully mobilized. Scales LINEARLY with mobilizationLevel below that (25%
  // mobilized -> 25% of this target), so a half-mobilized nation reads as a
  // half-filled map, not a step function.
  PER_R_AT_FULL_MOB: 1.0,

  // KNOB 2 — hard per-city cap, independent of size/mobilization. The
  // BALANCE GUARD: no matter how mobilized, no city's own militia garrison
  // exceeds this many sub-units — keeps the auto-defense a contest-multiplier
  // rather than a wall the invasion literally cannot breach.
  CAP_PER_CITY: 8,

  // KNOB 3 — manpower spent per militia raised (economy.manpower, the same
  // pool production/reinforcements/buildings all draw from — see
  // game/economy.js). Sized in the same ballpark as the manual "Local
  // Militia" reinforcement route's per-unit cost (55 manpower / 12 militia in
  // that battalion ≈ 4.6/unit — game/reinforcements.js) so home defense
  // doesn't undercut the player's own manual option, just automates a
  // smaller, capped slice of it.
  MANPOWER_PER_MILITIA: 5,

  // KNOB 4a/4b — seconds per +1 militia. Peacetime pace is deliberately slow
  // (the "gradual mobilization" feel over the ~15min PREP mobilization ramp —
  // see economy.js's MOBILIZATION_BASE_RATE — should read as a militia or two
  // appearing at a time, never a pop-in burst); EMERGENCY_MUSTER_INTERVAL_S
  // is the "the nation wakes up when attacked" accelerant, matching
  // economy.js's own threat-driven-ramp flavor one level up.
  MUSTER_INTERVAL_S: 42,
  EMERGENCY_MUSTER_INTERVAL_S: 11,

  // Detection radius for "this city is under threat right now" (emergency
  // levy trigger) — same value as battalionDoctrine.js's own
  // THREAT_RADIUS_PX, kept in sync deliberately (both answer "is the enemy
  // close enough to this city to matter" the same way).
  THREAT_RADIUS_PX: 420,
};

// ---------------------------------------------------------------------------
// POSTURE TOGGLE — default ON ("the nation defends itself" is the pillar-1
// fantasy; a player who wants to opt out of auto-mustering — e.g. to
// hand-manage manpower entirely themselves — can flip it off). Module-level,
// mirroring every other battalion-layer module's own scratch-state
// convention (battalionDoctrine.js's autoState, battalionMorale.js's
// seenBattalionIds) — a fresh game is a full page reload (main.js's Play
// Again button), so no explicit reset hook is needed here either.
const state = { enabled: true, clock: 0 };
export function isHomeDefenseEnabled() { return state.enabled; }
export function setHomeDefenseEnabled(v) { state.enabled = !!v; }
export function toggleHomeDefense() { state.enabled = !state.enabled; return state.enabled; }

// Per-city scratch: mapIdx -> { progress (0..1, fraction of the way to the
// NEXT militia), bnId (the resolved Home Guard battalion id once one
// exists) }. Kept OUTSIDE world so a battalion that gets wiped out in combat
// (updateBattalions prunes empty battalions — game/battalions.js) still
// leaves `progress`/`bnId` behind; `findHomeGuard` below naturally returns
// null once the battalion record is gone (updateBattalions already dropped
// it from world.battalions), and the next successful muster just creates a
// fresh one and re-links bnId — "a lost city's home guard is on its own; a
// re-taken one can muster again," per the task brief, falls out of this for
// free with no special-case code.
const cityState = new Map();

function findHomeGuard(world, cityIdx) {
  const st = cityState.get(cityIdx);
  if (!st || st.bnId == null) return null;
  return (world.battalions || []).find(b => b.id === st.bnId) || null;
}

// Any living, DETECTED enemy within radiusPx of (cx,cy) — same fog-gated
// shape as battalionDoctrine.js's own threatCount, just a boolean early-out
// rather than a full count (this only needs to know "yes/no, levy faster").
function threatNear(world, side, cx, cy, radiusPx) {
  const r2 = radiusPx * radiusPx;
  for (const u of world.units) {
    if (u.side === side || u.hp <= 0) continue;
    if (!detects(side, u)) continue;
    const dd = (u.x - cx) ** 2 + (u.y - cy) ** 2;
    if (dd <= r2) return true;
  }
  return false;
}

// Target Home Guard size for a city right now — the load-bearing balance
// formula (see KNOB 1/2 above): scales linearly with mobilizationLevel
// (0..100) and city size, capped. Exported so main.js's status UI + headless
// tests can read the exact same number this module musters toward.
export function homeGuardTarget(economy, city) {
  const T = HOME_DEFENSE_TUNABLES;
  const mobFrac = Math.max(0, Math.min(1, (economy.mobilizationLevel || 0) / 100));
  return Math.min(T.CAP_PER_CITY, Math.floor((city.r || 6) * mobFrac * T.PER_R_AT_FULL_MOB));
}

// ---------------------------------------------------------------------------
// updateHomeDefense — call once per frame (see this file's header for exact
// placement). Cheap: throttled to SCAN_INTERVAL_S, and O(player-owned
// cities) per scan.
export function updateHomeDefense(world, economy, map, dt) {
  if (!map || !map.cities || !map.cities.length) return;
  if (!state.enabled) return;

  state.clock += dt;
  if (state.clock < HOME_DEFENSE_TUNABLES.SCAN_INTERVAL_S) return;
  const elapsed = state.clock;
  state.clock = 0;

  const T = HOME_DEFENSE_TUNABLES;

  for (let i = 0; i < map.cities.length; i++) {
    const c = map.cities[i];
    if (c.owner !== 'player') continue; // only OWNED cities muster — an enemy-held or contested one gets nothing (matches battalionDoctrine's own "you garrison your own territory" rule)

    const target = homeGuardTarget(economy, c);
    if (target <= 0) continue; // mobilization/size too low for even one militia yet — nothing to do

    let st = cityState.get(i);
    if (!st) { st = { progress: 0, bnId: null }; cityState.set(i, st); }

    let bn = findHomeGuard(world, i);
    let current = bn ? bn.units.filter(u => u.hp > 0).length : 0;
    if (current >= target) { st.progress = Math.min(st.progress, 1); continue; } // already at (or briefly above, e.g. cap just dropped — never happens since target only grows, but harmless) target

    const { x: cx, y: cy } = cityCenterWorld(map, c);
    const threatened = threatNear(world, 'player', cx, cy, T.THREAT_RADIUS_PX);
    const interval = threatened ? T.EMERGENCY_MUSTER_INTERVAL_S : T.MUSTER_INTERVAL_S;
    st.progress += elapsed / interval;

    // Muster one militia at a time as progress crosses 1.0. Each one is
    // gated on the REAL manpower pool — if it can't be afforded, mustering
    // STALLS (progress banked, clamped to at most 1.0 below) rather than
    // going into debt or skipping the cost; the very next scan tick (or the
    // instant manpower recovers) picks up exactly where it left off.
    while (st.progress >= 1 && current < target) {
      if (economy.manpower < T.MANPOWER_PER_MILITIA) break; // manpower-starved — competes with production/buildings/manual reinforcements for the same pool
      economy.manpower -= T.MANPOWER_PER_MILITIA;
      if (!bn) {
        bn = createEmptyBattalion(world, {
          type: 'militia', side: 'player', name: `${c.name} Home Guard`,
          homeSector: i, stance: 'defensive', // dig in tight — a home guard isn't going on the offensive
        });
        bn.homeGuard = true; // plain marker field, read by findHomeGuard's callers/UI only — battalions.js/battalionDoctrine.js never branch on it
        st.bnId = bn.id;
      }
      addUnitsToBattalion(world, map, bn, { unit: 'militia', count: 1, x: cx, y: cy });
      current++;
      st.progress -= 1;
    }
    // Never bank more than one muster's worth — a long manpower stall (or a
    // scan tick that raced ahead of the loop above because current hit
    // target mid-loop) can't silently accumulate into a multi-unit burst the
    // moment the pool recovers. This is what keeps the pacing reading as
    // "steady mobilization," per the task brief, rather than pop-in.
    st.progress = Math.min(st.progress, 1);
  }
}

// ---------------------------------------------------------------------------
// Read-only per-city snapshot — for main.js's status line + headless
// verification. Never mutates anything.
export function homeDefenseStatus(world, economy, map) {
  const out = [];
  if (!map || !map.cities) return out;
  for (let i = 0; i < map.cities.length; i++) {
    const c = map.cities[i];
    if (c.owner !== 'player') continue;
    const bn = findHomeGuard(world, i);
    const current = bn ? bn.units.filter(u => u.hp > 0).length : 0;
    out.push({
      cityIdx: i, name: c.name, current, target: homeGuardTarget(economy, c),
      battalionId: bn ? bn.id : null, garrisoned: bn ? !!bn.garrisoned : false,
    });
  }
  return out;
}

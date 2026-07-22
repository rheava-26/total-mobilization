// BATTALION MORALE / COHESION — B4 of the 6-phase battalion rework
// (docs/CONCEPT.md, "BATTALIONS, not singular units" -> "Morale / cohesion":
// "Cohesion meter (fresh -> steady -> shaken -> broken/routing) per
// battalion; decays when isolated/under fire, recovers in friendly
// territory / on reinforcement; rout removes/flees the formation. Feeds a
// defensive modifier. Real-time decay rates (per second), not per-turn.").
//
// WHAT THIS FILE IS: the only place `bn.morale`/`bn.cohesion` (stored inert
// since B1 — game/battalions.js) actually change. Every rate below is a
// per-SECOND rate multiplied by `dt`, per the concept doc's explicit
// real-time requirement (never a flat per-tick/per-turn subtraction).
//
// WHAT THIS FILE TOUCHES: `bn.morale`, `bn.cohesion`, a few scratch fields
// this module owns on the battalion record (`bn._prevHpFrac`,
// `bn._underFireT`), and — the ONE per-unit field this phase writes —
// `u.defenseMult` on each of a battalion's own sub-units (read by exactly
// one line in game/units.js's resolveHit; see that file's comment). It
// never issues an order, never moves/fires a unit, never touches any other
// unit field. Rout itself (fleeing) is issued by game/battalionDoctrine.js,
// which just READS `moraleState(bn)`/`bn.morale` from here — see that
// file's new rout branch. This module owns morale math only.
//
// CALL CONTRACT: `updateBattalionMorale(world, map, dt)` once per frame,
// alongside updateBattalions/updateBattalionDoctrine (see main.js's
// loop()). Order relative to those two doesn't matter much — this module
// only reads `bn.units`/positions and `map.cities`, and only writes the
// fields listed above — but it's called AFTER updateBattalions (so it never
// scores a battalion against sub-units that were just pruned as dead this
// frame) in main.js's actual wiring.
//
// SIDE-AGNOSTIC BY DESIGN: loops every battalion in world.battalions
// regardless of `bn.side` (today that's player-only — enemy ground forces
// are still loose enemyai.js units, not battalions; B6 adapts enemy AI to
// battalions and this module needs no changes when that lands).

import { battalionStrength } from './battalions.js';
import { cityCenterWorld } from './objectives.js';

// ---------------------------------------------------------------------------
// TUNABLES — DESIGNER'S TO TUNE placeholders, all per-SECOND rates (or
// one-shot event magnitudes) unless noted. Comments explain the intent so a
// designer can retune without archaeology.

// Casualty shock: a drop in hpFrac this tick costs morale proportional to
// the FRACTION of the battalion's total strength just lost, scaled way up
// (a big instantaneous hit — losing men should hurt right now, not trickle).
// e.g. hpFrac drops by 0.10 (10% of the battalion's strength gone) -> -20
// morale in one shot.
const CASUALTY_MORALE_PER_HPFRAC_LOST = 200;
// Any detected casualty this tick also flags "under fire" for this many
// simulated seconds, during which morale bleeds steadily (being shot at
// right now, regardless of whether every shot connects).
const UNDER_FIRE_WINDOW_S = 2;
const UNDER_FIRE_MORALE_PER_S = -3;

// Isolation: no friendly unit/battalion sub-unit within this radius of the
// battalion's own live-sub-unit centroid -> cut off, morale AND cohesion
// bleed (cohesion faster — a scattered/alone formation loses its shape
// before it loses its nerve).
const ISOLATION_RADIUS_PX = 500;
const ISOLATION_MORALE_PER_S = -4;
const ISOLATION_COHESION_PER_S = -5;

// Friendly territory: battalion centroid inside an OWNED city's capture
// radius -> dug in at home, reforming. Recovers faster than the drift-back
// baseline below (being home is meaningfully better than merely "not under
// fire").
const TERRITORY_MORALE_PER_S = 6;
const TERRITORY_COHESION_PER_S = 8;

// Baseline drift: when neither under fire nor isolated nor in friendly
// territory, morale/cohesion drift gently back toward a steady baseline
// (not all the way to 100 — that's what actually being home is for).
const BASELINE_MORALE = 65; // "steady" band, see STATE thresholds below
const BASELINE_COHESION = 70;
const DRIFT_PER_S = 2;

// Reinforcement bump: a battalion that just spawned this frame (see
// world.battalions membership vs. a small seen-id cache below) gives every
// OTHER friendly battalion within this radius a one-shot morale nudge
// ("the cavalry's here") — cheap: O(battalions) per new arrival, and new
// arrivals are rare relative to frame count.
const REINFORCEMENT_RADIUS_PX = 600;
const REINFORCEMENT_MORALE_BUMP = 8;

// Cohesion feeds BACK into morale loss: low cohesion (formation coming
// apart) accelerates whatever morale loss is already happening this tick.
// Applied as a multiplier on any NEGATIVE morale delta this tick, ranging
// from 1x at full cohesion up to LOW_COHESION_MAX_MULT at 0 cohesion.
const LOW_COHESION_MAX_MULT = 1.6;

// Cohesion's own baseline driver: how tightly clustered sub-units are
// around their own centroid, in px, mapped against a "tight"/"loose"
// reference band. Inside TIGHT_SPREAD_PX, cohesion recovers; beyond
// LOOSE_SPREAD_PX it decays; between the two bands it's inert (own dynamics
// above still apply).
const TIGHT_SPREAD_PX = 90;
const LOOSE_SPREAD_PX = 260;
const SPREAD_COHESION_PER_S = 6;

// STATE thresholds — fresh/steady/shaken/broken, per CONCEPT.md's exact
// naming. `>=` on each lower bound (100 is fresh, 0 is broken).
const STATE_THRESHOLDS = [
  { min: 80, state: 'fresh' },
  { min: 50, state: 'steady' },
  { min: 20, state: 'shaken' },
  { min: -Infinity, state: 'broken' },
];

// defenseMult per state — read by exactly the one multiply in
// game/units.js's resolveHit (`dmg * (target.defenseMult || 1)`). Fresh
// formations are tougher (dug-in, confident, return fire coordinated);
// broken ones take noticeably more damage (falling apart, easy to pick
// off) — the whole point of this phase's "feeds a defensive modifier."
const DEFENSE_MULT_BY_STATE = {
  fresh: 0.85,
  steady: 1.0,
  shaken: 1.15,
  broken: 1.35,
};

// ---------------------------------------------------------------------------
export function moraleState(bn) {
  const m = bn.morale;
  for (const { min, state } of STATE_THRESHOLDS) if (m >= min) return state;
  return 'broken';
}

export function defenseMultFor(bn) {
  return DEFENSE_MULT_BY_STATE[moraleState(bn)] ?? 1;
}

// Convenience for battalionDoctrine.js's rout branch — kept here (rather
// than duplicated) since morale is this module's own concern.
export function isRouting(bn) {
  return moraleState(bn) === 'broken';
}

function clamp01to100(v) { return Math.max(0, Math.min(100, v)); }

function liveCentroid(bn) {
  let sumX = 0, sumY = 0, n = 0;
  for (const u of bn.units) {
    if (u.hp <= 0) continue;
    sumX += u.x; sumY += u.y; n++;
  }
  if (!n) return null;
  return { x: sumX / n, y: sumY / n, n };
}

// Average distance of live sub-units from their own centroid — the
// "formation spread" cohesion reads for its tight/loose recovery/decay
// band. O(units in this battalion); battalions are small (≤14 sub-units per
// B1's templates) so this is cheap even every frame.
function formationSpread(bn, centroid) {
  let sum = 0, n = 0;
  for (const u of bn.units) {
    if (u.hp <= 0) continue;
    sum += Math.hypot(u.x - centroid.x, u.y - centroid.y);
    n++;
  }
  return n ? sum / n : 0;
}

// Nearest-friendly check for isolation: any OTHER live unit (any battalion
// or loose) on the same side within ISOLATION_RADIUS_PX of the centroid
// counts as "not alone" — a battalion isn't isolated just because its own
// escort is a different battalion's sub-unit, or a loose garrison unit
// happens to be standing next to it.
function hasNearbyFriendly(world, bn, centroid) {
  const r2 = ISOLATION_RADIUS_PX * ISOLATION_RADIUS_PX;
  for (const u of world.units) {
    if (u.hp <= 0 || u.side !== bn.side) continue;
    if (u.battalion === bn.id) continue; // don't count the battalion's own sub-units against themselves
    const dd = (u.x - centroid.x) ** 2 + (u.y - centroid.y) ** 2;
    if (dd <= r2) return true;
  }
  return false;
}

function inFriendlyTerritory(map, side, centroid) {
  if (!map || !map.cities) return false;
  for (const c of map.cities) {
    if (c.owner !== side) continue;
    const { x: cx, y: cy } = cityCenterWorld(map, c);
    const rWorld = (c.r || 6) * map.tileSize;
    const dd = (centroid.x - cx) ** 2 + (centroid.y - cy) ** 2;
    if (dd <= rWorld * rWorld) return true;
  }
  return false;
}

// Battalion ids already accounted for by the reinforcement-bump pass, so a
// battalion that just arrived only ever bumps its neighbors ONCE (on the
// first tick it's seen), not every frame it happens to still be "new."
const seenBattalionIds = new Set();

// ---------------------------------------------------------------------------
export function updateBattalionMorale(world, map, dt) {
  if (!world.battalions || !world.battalions.length) return;

  // Prune the seen-id cache for battalions that no longer exist (mirrors
  // battalionDoctrine.js's own autoState prune) so it can't grow forever.
  const liveBnIds = new Set(world.battalions.map(bn => bn.id));
  for (const id of seenBattalionIds) if (!liveBnIds.has(id)) seenBattalionIds.delete(id);

  // Pass 1: detect freshly-arrived battalions (for the reinforcement bump)
  // BEFORE the main per-battalion pass below, so a brand-new battalion can
  // bump its neighbors' morale on the very tick it appears.
  const freshArrivals = [];
  for (const bn of world.battalions) {
    if (!seenBattalionIds.has(bn.id)) {
      seenBattalionIds.add(bn.id);
      freshArrivals.push(bn);
    }
  }

  // Precompute centroids once (used both by the isolation/territory checks
  // below and by the reinforcement-bump pass) — avoids recomputing per pair.
  const centroids = new Map(); // bn.id -> {x,y,n} | null
  for (const bn of world.battalions) centroids.set(bn.id, liveCentroid(bn));

  // Reinforcement bump: for each freshly-arrived battalion, nudge every
  // OTHER friendly battalion within radius. One-shot (only runs the tick a
  // battalion is first seen), so this is cheap overall.
  for (const arrival of freshArrivals) {
    const ac = centroids.get(arrival.id);
    if (!ac) continue;
    const r2 = REINFORCEMENT_RADIUS_PX * REINFORCEMENT_RADIUS_PX;
    for (const bn of world.battalions) {
      if (bn.id === arrival.id || bn.side !== arrival.side) continue;
      const c = centroids.get(bn.id);
      if (!c) continue;
      const dd = (c.x - ac.x) ** 2 + (c.y - ac.y) ** 2;
      if (dd <= r2) bn.morale = clamp01to100(bn.morale + REINFORCEMENT_MORALE_BUMP);
    }
  }

  // Pass 2: the actual per-battalion morale/cohesion update.
  for (const bn of world.battalions) {
    const centroid = centroids.get(bn.id);

    // --- casualty / under-fire detection (hpFrac frame-over-frame) -------
    const { hpFrac } = battalionStrength(bn);
    if (bn._prevHpFrac === undefined) bn._prevHpFrac = hpFrac; // first tick seen — no delta yet
    const dropped = Math.max(0, bn._prevHpFrac - hpFrac);
    if (dropped > 0) {
      bn.morale = clamp01to100(bn.morale - dropped * CASUALTY_MORALE_PER_HPFRAC_LOST);
      bn._underFireT = UNDER_FIRE_WINDOW_S;
    }
    bn._prevHpFrac = hpFrac;
    bn._underFireT = Math.max(0, (bn._underFireT || 0) - dt);
    const underFire = bn._underFireT > 0;

    // --- per-second state (isolation / territory / baseline drift) -------
    let moraleDelta = 0, cohesionDelta = 0;
    if (!centroid) {
      // No live sub-units left this frame — updateBattalions will prune it
      // shortly; nothing more to do for this battalion this tick.
      continue;
    }

    const home = inFriendlyTerritory(map, bn.side, centroid);
    // Isolation means cut off from OTHER friendly forces — a battalion's
    // own sub-units never count toward "nearby friendly" (see
    // hasNearbyFriendly), so a lone battalion sitting by itself in its own
    // home city would otherwise ALWAYS read as isolated too. `home`
    // deliberately overrides that: being dug in at a friendly city means
    // supply/defense infrastructure, not "alone in the field" — see this
    // file's header ("recovers in friendly territory"). Isolation only
    // actually applies away from friendly territory.
    const isolatedRaw = !hasNearbyFriendly(world, bn, centroid);
    const isolated = isolatedRaw && !home;

    if (underFire) moraleDelta += UNDER_FIRE_MORALE_PER_S;
    if (home) {
      moraleDelta += TERRITORY_MORALE_PER_S;
      cohesionDelta += TERRITORY_COHESION_PER_S;
    } else if (isolated) {
      moraleDelta += ISOLATION_MORALE_PER_S;
      cohesionDelta += ISOLATION_COHESION_PER_S;
    } else if (!underFire) {
      // Neither under fire, isolated, nor home: drift gently toward the
      // steady baseline (from either direction).
      if (bn.morale < BASELINE_MORALE) moraleDelta += DRIFT_PER_S;
      else if (bn.morale > BASELINE_MORALE) moraleDelta -= DRIFT_PER_S;
    }

    // Cohesion's own formation-spread contribution (independent of the
    // isolation/territory branch above — a scattered formation loses
    // cohesion even if technically "home," and a tight one recovers even
    // mid-field as long as it's not actively isolated/under fire).
    if (!isolated) {
      const spread = formationSpread(bn, centroid);
      if (spread <= TIGHT_SPREAD_PX) cohesionDelta += SPREAD_COHESION_PER_S;
      else if (spread >= LOOSE_SPREAD_PX) cohesionDelta -= SPREAD_COHESION_PER_S;
      else if (!home) {
        // Between bands with nothing else driving it: drift toward baseline.
        if (bn.cohesion < BASELINE_COHESION) cohesionDelta += DRIFT_PER_S * 0.5;
        else if (bn.cohesion > BASELINE_COHESION) cohesionDelta -= DRIFT_PER_S * 0.5;
      }
    }

    // Low cohesion accelerates morale LOSS (a formation coming apart loses
    // heart faster) — only amplifies negative deltas, never positive ones.
    if (moraleDelta < 0) {
      const cohesionFrac = bn.cohesion / 100; // 1 = full cohesion, 0 = none
      const mult = 1 + (1 - cohesionFrac) * (LOW_COHESION_MAX_MULT - 1);
      moraleDelta *= mult;
    }

    bn.morale = clamp01to100(bn.morale + moraleDelta * dt);
    bn.cohesion = clamp01to100(bn.cohesion + cohesionDelta * dt);

    // --- Effect A: write the defensive modifier onto every sub-unit ------
    const mult = defenseMultFor(bn);
    for (const u of bn.units) u.defenseMult = mult;
  }
}

// ---------------------------------------------------------------------------
// Test-only escape hatch — mirrors battalionDoctrine.js's debugForceTick
// shape: lets a headless test force a battalion straight to a given morale
// value (e.g. to exercise the broken/rout path without waiting out the real
// decay rates above). Clamped the same way the real update clamps.
export function setMorale(world, bnId, value) {
  const bn = (world.battalions || []).find(b => b.id === bnId);
  if (!bn) return false;
  bn.morale = clamp01to100(value);
  return true;
}

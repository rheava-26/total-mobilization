// FUNCTIONAL-AGGRESSOR ENEMY AI (Playability v1 Part B — CONCEPT.md's
// settled "Playability v1" section: "Enemy = a FUNCTIONAL AGGRESSOR. It
// lands in the combat phase, targets your cities/capital, maneuvers toward
// them, engages defenders, and captures — a genuine threat, but no economy
// of its own"). This REPLACES main.js's old scripted "advance toward the
// player base" order with real OBJECTIVE SEEKING, but reuses
// game/units.js's movement/pathfinding/targeting/fire-discipline wholesale
// — the only thing this module adds is picking WHICH city (if any) a given
// enemy unit is currently walking toward, by writing the exact same
// `u.order` field a player's right-click order already writes. Combat
// itself is untouched: updateUnits' own pickTarget/nearestEnemy fallback
// already interrupts a unit's order to fight whatever it detects in range,
// so an AI-driven unit peels off to engage a defender it meets en route
// exactly like a player-ordered unit does, then resumes its march on its
// own once nothing's left to shoot at (its order isn't cleared by combat,
// only by actually being reached or reassigned here).
//
// DATA-DRIVEN, no per-unit-name branching: every unit's objective is picked
// purely from its MOVE CLASS (ground/air/naval — the same attribute
// units.js's own steering already branches on) and its DISPOSITIONS
// (holdGround batteries dig in and don't maneuver, same doctrine the old
// scripted landing force already gave them) plus its current POSITION. No
// unit key/name is ever referenced here.

import { MOVE_CLASSES } from './units.js';
import { cityCenterWorld } from './objectives.js';

// Landing-force doctrine tunables — designer's to retune; nothing below is
// hand-fitted to force a particular win/lose outcome (per the task's
// explicit "don't hand-tune to force either result" instruction), just
// reasonable defaults for "a competent aggressor that pursues objectives."
const CAPITAL_BIAS_PX = 720; // flat world-px discount applied to the capital's effective distance — a thumb on the scale, not a guarantee it's always picked first
const RETARGET_INTERVAL_S = 3; // how often a unit reconsiders its objective (cheap either way, no need every frame)
const ARRIVE_FRACTION = 0.6; // "arrived, start holding" once within this fraction of the target city's own radius — comfortably inside the capture-mechanic's own radius check (game/objectives.js), so a holding unit always actually counts toward capture

// Nearest WATER tile to a world point — naval movement can't be ordered
// onto dry land (see units.js terrainSample's requiresWater branch), so a
// naval unit's objective order gets snapped the same way main.js's
// player-facing issueMoveOrder already snaps a naval click. Duplicated here
// (rather than imported from main.js, which isn't a module other code can
// import from) same spirit as game/mapgen.js's own roadAStar duplicating
// pathfind.js's cost function — small, self-contained, and simpler than
// threading a callback through.
function nearestWaterTile(map, wx, wy, maxTiles = 40) {
  const ts = map.tileSize;
  const gx0 = Math.floor(wx / ts), gy0 = Math.floor(wy / ts);
  for (let r = 0; r <= maxTiles; r++) {
    for (let dy = -r; dy <= r; dy++) {
      for (let dx = -r; dx <= r; dx++) {
        if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
        const t = map.terrainAt(gx0 + dx, gy0 + dy);
        if (t && t.water) return { x: (gx0 + dx + 0.5) * ts, y: (gy0 + dy + 0.5) * ts };
      }
    }
  }
  return { x: wx, y: wy }; // no water found within range: fall back to the raw point
}

// Best objective city for `side` (the attacking side) from world point
// (wx,wy) — any city NOT already owned by `side` is a valid target. Null
// once every city on the map is already held by `side` (nothing left to
// push toward — a landing force that's swept every lesser city but hasn't
// reached the capital just sits on what it's got, per the task's "losing
// lesser cities isn't game over" framing applying in reverse too: taking
// them all isn't an automatic win either).
function pickObjectiveCity(map, side, wx, wy) {
  let best = null, bestScore = -Infinity;
  const cities = map.cities || [];
  for (let i = 0; i < cities.length; i++) {
    const c = cities[i];
    if (c.owner === side) continue;
    const { x: cx, y: cy } = cityCenterWorld(map, c);
    const dist = Math.hypot(cx - wx, cy - wy);
    const bias = i === 0 ? CAPITAL_BIAS_PX : 0; // cities[0] is always the capital (game/mapgen.js's convention)
    const score = -(dist - bias);
    if (score > bestScore) { bestScore = score; best = c; }
  }
  return best;
}

// Per-unit AI scratch state, keyed by unit id (spawnUnit's ids are stable
// small ints — a plain Map is simpler than tagging state onto the unit
// object itself, and keeps this module from mutating shapes units.js owns).
// Never cleaned up on unit death — harmless at this scale (a landing force
// is small and bounded, no reinforcements per the task's explicit "no
// economy" scope), not worth the bookkeeping for a v1.
const aiState = new Map();

// Call once per frame, DURING combat (see main.js's `combatActive` gate —
// this function does nothing dangerous if called outside combat, it just
// has nothing to do since spawnEnemyLandingForce only ever runs when combat
// begins, but main.js gates the call anyway to keep PREP's "no forced
// actions" contract obvious at the call site).
export function updateEnemyAI(world, map, dt, side = 'enemy') {
  for (const u of world.units) {
    if (u.side !== side || u.hp <= 0) continue;
    const moveClass = MOVE_CLASSES[u.def.moveClass];

    // holdGround units (SAM/AA/artillery/etc — the same disposition flag
    // units.js's own steering reads) are a defensive battery doctrine, not
    // maneuver units: they dig in at the beachhead and let anything that
    // comes to them be engaged by units.js's own targeting, exactly what
    // the old scripted landing force already did (main.js's orderAdvance
    // used to skip them for the identical reason). No objective for these.
    if (u.def.dispositions.includes('holdGround')) continue;

    let st = aiState.get(u.id);
    if (!st) { st = { objective: null, retargetAt: 0, t: 0 }; aiState.set(u.id, st); }
    st.t += dt;

    // Retarget when there's no objective yet, the current one is already
    // ours (captured — game/objectives.js flips city.owner in place, so
    // this reads the live result immediately rather than waiting out the
    // interval), or the periodic reconsideration window has elapsed.
    if (!st.objective || st.objective.owner === side || st.t >= st.retargetAt) {
      st.objective = pickObjectiveCity(map, side, u.x, u.y);
      st.retargetAt = st.t + RETARGET_INTERVAL_S;
    }
    if (!st.objective) continue; // every city already ours: nothing left to push toward

    const c = st.objective;
    const { x: cx, y: cy } = cityCenterWorld(map, c);
    const arriveR = (c.r || 6) * map.tileSize * ARRIVE_FRACTION;
    const d = Math.hypot(u.x - cx, u.y - cy);
    if (d <= arriveR) {
      // Arrived: HOLD to feed the capture mechanic (game/objectives.js
      // counts ground-unit presence within the city's FULL radius, which
      // arriveR sits comfortably inside of). Clear any stale order so
      // units.js's own idle/hover behavior takes over instead of
      // continually re-pathing to the exact center point — that would have
      // units jostling/orbiting the goal rather than sitting still while
      // capture progress accrues.
      u.order = null;
      continue;
    }
    // Order toward the objective — reuses units.js's exact `order` field,
    // so ground units get the SAME A* pathfinding a player's right-click
    // order gets (ensurePath/advancePath in game/units.js); air ignores
    // terrain and steers straight there per its existing move class; naval
    // gets its order snapped onto the nearest water tile to the target
    // city (an order landing on dry land is illegal for a requiresWater
    // mover — see units.js terrainSample).
    u.order = moveClass.requiresWater ? nearestWaterTile(map, cx, cy) : { x: cx, y: cy };
  }
}

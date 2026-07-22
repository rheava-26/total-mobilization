// OBJECTIVES — city ownership, capture, and win/lose (Playability v1 —
// CONCEPT.md's settled "Playability v1 — closing the game loop" section).
// Every city (game/mapgen.js's map.cities entries — plain {x,y,r,name}
// objects in TILE coords) gets an OWNER side, default 'player'. An enemy of
// the current owner (in this v1 there are exactly two sides, 'player' and
// 'enemy', so "enemy of the owner" is just "the other side") holding a city
// UNCONTESTED for CAPTURE_TIME_S seconds flips ownership — recapturable the
// same way, fully symmetric: nothing here special-cases "the player attacks"
// vs "the enemy attacks," the same rule runs both directions.
//
// Data lives ON the city object itself (mutated in place), not a parallel
// side-table keyed by city — the renderer (engine/renderer.js) and the
// objective HUD (main.js) both just read city.owner/.captureProgress/
// .contested straight off the exact same objects mapgen.js produced and
// map.cities already holds, so there's no separate store that could ever
// drift out of sync with "what map.cities says."
//
// GROUND ONLY, deliberately: only ground-domain units (foot/wheeled/tracked
// moveClass — the same set game/pathfind.js routes for) count toward
// capture presence. Air/naval units can support a capture by clearing
// defenders, but the actual "boots on the ground holding the objective"
// check only looks at ground units — a fighter loitering overhead can't
// singlehandedly flip a city, which matches how the rest of the game
// already treats ground vs air/naval as different domains for anything
// terrain-related.

import { MOVE_CLASSES } from './units.js';

export const CAPTURE_TIME_S = 18; // seconds an uncontested holder needs to flip a city — designer's to retune

// THE INVASION IS FINITE (CONCEPT.md "Playability v1": win = the landing force
// "eliminated/SPENT"). Outright elimination is one win; the more common one is
// REPELLING it — you break the assault and the survivors can no longer press.
// Once the living landing force is whittled below ROUT_FRACTION of its size at
// landing with the capital still held, the invasion is spent → victory.
// INVASION_DURATION_S is the hold-out backstop: hold the capital that long and
// the invasion has culminated on its own (the book's "it fizzles anticlimactically"
// beat) — this ALSO guarantees a run always RESOLVES instead of grinding into a
// stalemate where a defender holds forever but a few stray attackers never die
// (the "undecided" the balance harness surfaced). All three are the designer's
// to retune by feel.
export const ROUT_FRACTION = 0.2;      // invasion "broken" when living enemy ≤ this fraction of the landing force
export const INVASION_DURATION_S = 210; // hold the capital this long and the invasion culminates (< the harness's 300s combat cap so it actually fires)

// Snapshot the landing force the instant combat begins — main.js calls this
// from beginCombat and threads the returned object into evaluateOutcome each
// frame (ticking .elapsed itself). Kept as caller-owned state (not a module
// global) so a Play-Again reload starts a clean invasion, same reasoning the
// rest of the codebase creates per-run state objects rather than singletons.
export function initInvasion(world) {
  const landingSize = world.units.filter(u => u.side === 'enemy' && u.hp > 0).length;
  return { landingSize, elapsed: 0 };
}

function isGroundUnit(u) {
  const mc = MOVE_CLASSES[u.def.moveClass];
  return !!mc && !!mc.ground;
}

// World-px center of a city (mapgen.js stores x/y/r in TILE coords).
export function cityCenterWorld(map, c) {
  return { x: (c.x + 0.5) * map.tileSize, y: (c.y + 0.5) * map.tileSize };
}

// Called once at load, right after the map is built (see main.js) — sets
// every city's ownership/capture fields in place. Mutates map.cities
// entries directly rather than copying them into a new structure, since
// those are the exact objects the renderer and HUD already read.
export function initCityOwnership(map, defaultOwner = 'player') {
  for (const c of map.cities || []) {
    c.owner = defaultOwner;
    c.captureProgress = 0; // 0..1 toward flipping to the OTHER side
    c.contested = false;
  }
}

// Per-frame update (COMBAT phase only — see main.js's `combatActive` gate;
// PREP never calls this, so cities can never flip during build-up). Returns
// an array of capture EVENTS that happened this call — { city, from, to } —
// so callers (win/lose evaluation, the HUD, the reactive director's bubble)
// can react without diffing every city's owner themselves every frame.
export function updateObjectives(world, map, dt) {
  const events = [];
  for (const c of map.cities || []) {
    if (c.owner === undefined) { c.owner = 'player'; c.captureProgress = 0; } // defensive default
    const { x: cx, y: cy } = cityCenterWorld(map, c);
    const rWorld = (c.r || 6) * map.tileSize;
    const r2 = rWorld * rWorld;
    let ownerPresent = false, challengerPresent = false;
    for (const u of world.units) {
      if (u.hp <= 0 || !isGroundUnit(u)) continue;
      const dd = (u.x - cx) ** 2 + (u.y - cy) ** 2;
      if (dd > r2) continue;
      if (u.side === c.owner) ownerPresent = true;
      else challengerPresent = true;
    }
    c.contested = ownerPresent && challengerPresent;

    if (challengerPresent && !ownerPresent) {
      // uncontested challenger: progress climbs toward flipping the city
      c.captureProgress = Math.min(1, c.captureProgress + dt / CAPTURE_TIME_S);
      if (c.captureProgress >= 1) {
        // Exactly two sides exist in this v1, so "the challenger" is
        // unambiguous — just flip. A future 3+-side game would need to
        // track WHICH challenger side actually accrued the progress rather
        // than assuming "not the owner" means one specific side.
        const from = c.owner;
        c.owner = from === 'player' ? 'enemy' : 'player';
        c.captureProgress = 0;
        events.push({ city: c, from, to: c.owner });
      }
    } else if (c.contested) {
      // both sides present: capture PAUSED — neither climbs nor decays,
      // per the task spec ("Contested = capture paused").
    } else {
      // nobody present, or only the current owner is: decay back toward 0
      // so a brief hit-and-run incursion doesn't leave a city permanently
      // primed to flip on the next stray unit that wanders through.
      c.captureProgress = Math.max(0, c.captureProgress - dt / CAPTURE_TIME_S);
    }
  }
  return events;
}

// WIN/LOSE (checked once per frame by main.js, COMBAT phase only, per the
// task's hard constraint that PREP never evaluates win/lose):
//   'victory' — the landing force is eliminated (no living enemy units
//     remain). A functional aggressor with no reinforcements (CONCEPT.md:
//     "a functional aggressor has no reinforcements") means wiping its
//     units out IS winning the war, cleanly.
//   'defeat'  — the CAPITAL (cities[0], per game/mapgen.js's convention —
//     always the biggest/coastal city) has been captured. Losing any OTHER
//     city is a setback (reflected in the HUD/end-screen stats) but never
//     ends the run on its own.
//   null      — game continues.
export function evaluateOutcome(world, map, invasion) {
  const capital = (map.cities || [])[0];
  if (capital && capital.owner === 'enemy') return 'defeat';
  const enemyAlive = world.units.reduce((n, u) => n + (u.side === 'enemy' && u.hp > 0 ? 1 : 0), 0);
  if (enemyAlive === 0) return 'victory'; // wiped out — the cleanest win
  // Capital still held (checked above) and some enemy remains: is the invasion
  // SPENT? Either broken below the rout floor, or it has culminated by holding
  // out. Without `invasion` (defensive: pre-init frame) fall through to null.
  if (invasion) {
    const routFloor = Math.max(1, Math.round(invasion.landingSize * ROUT_FRACTION));
    if (enemyAlive <= routFloor) return 'victory';                 // broken — too few left to press the assault
    if (invasion.elapsed >= INVASION_DURATION_S) return 'victory'; // culminated — held the capital until it fizzled
  }
  return null;
}

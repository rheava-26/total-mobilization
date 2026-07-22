// BATTALION ENTITY + GROUPING LAYER — B1 of the 6-phase battalion rework
// (docs/CONCEPT.md, "Ground combat model: BATTALIONS, not singular units").
// Reference: docs/reference-battalion-design.md §7 (turn-based-flavored —
// translated here to real-time seconds and world-space points instead of
// per-turn/hex).
//
// WHAT THIS FILE IS: a battalion is a formation RECORD that owns a group of
// already-existing individual units (the EXISTING UNIT_DEFS roster in
// game/units.js, unchanged). It does not add a new kind of combatant — it
// adds bookkeeping on top of the ones that already exist. Every sub-unit is
// spawned through the same spawnUnit() the rest of the game already uses, so
// it fights, moves, paths, and renders through the EXACT same code as any
// other unit on the map; nothing in game/units.js, the renderer, or
// production/economy needs to know battalions exist for combat to work.
//
// B1 IS DELIBERATELY ADDITIVE. This phase must not change any existing
// gameplay, combat, production, or rendering behavior:
//   - No existing spawn path is rerouted through a battalion (that's B2 —
//     "production emits battalions").
//   - Sub-units don't move, garrison, or hold sectors as a group yet (B3 —
//     "self-deploy + sectors + stance"); doctrine.js still governs each
//     tagged sub-unit exactly as it does any other player unit today.
//   - `stance`/`morale`/`cohesion` are STORED on the record now (so later
//     phases don't need a data-migration step) but are INERT — nothing
//     reads them to change behavior yet. Morale/cohesion decay/effects are
//     B4; stance-driven doctrine tuning is B3.
//   - updateBattalions() below is a pure bookkeeping pass: it only READS
//     unit.hp/world.units and WRITES to the battalion record's own `units`
//     list. It never mutates a unit, never issues an order, never touches
//     combat/targeting/rendering.
//
// The upshot: spawning a battalion today is equivalent to spawning its
// constituent units individually, plus a label. Later phases (B2 rerouting
// production, B3 lifting doctrine to the battalion level, B4 morale/cohesion
// effects, B5 garrison bonuses, B6 operational-map presentation) build
// actual behavior on top of these same records without needing to touch
// this file's shape again.

import { spawnUnit, UNIT_DEFS } from './units.js';

// ---------------------------------------------------------------------------
// COMPOSITION TEMPLATES — fixed rosters per CONCEPT.md's three battalion
// sources (elite/standard/militia; B2 will map these onto "Factory Elite",
// "Army-Sent Standard", and "Local Militia" respectively, but B1 only needs
// the composition data itself). Every `unit` key below is a REAL key from
// UNIT_DEFS (game/units.js) — verified to exist at module load time by the
// assertion loop right after this block, so a typo here fails loudly at
// import time instead of silently spawning `undefined` units.
//
// Picks, by role:
//   armor    -> 'tank' (MBT "Aegis") — the roster's baseline tracked gun tank.
//   infantry -> 'infantry' (Infantry Squad) for elite/standard (the sturdier
//               foot unit); 'militia' (Militia Levy) for the militia
//               template, per CONCEPT.md's "cheap, numerous, raised from a
//               controlled city's population" description of that source.
//   support  -> 'aa' (AA Gun "Bristle") for elite (mixed-domain autocannon,
//               screens the formation from air as well as ground) and
//               standard (same screening role, one less than elite);
//               'scout' (Scout Car "Whippet") for militia — cheap, fast
//               recon rather than a real AA/artillery asset, matching a
//               levy formation's light-vehicle-only support per the task
//               brief ("maybe 1-2 light vehicles").
export const BATTALION_TEMPLATES = {
  // Factory Elite — rare, powerful, the "ace" formation (CONCEPT.md source
  // #1). Heavy armor-infantry mix with real air-defense support.
  elite: [
    { unit: 'tank', count: 4 },
    { unit: 'infantry', count: 6 },
    { unit: 'aa', count: 2 },
  ],
  // Army-Sent Standard — the backbone, requisitioned formation (CONCEPT.md
  // source #2). Lighter armor than elite, infantry-heavy, one support asset.
  standard: [
    { unit: 'tank', count: 2 },
    { unit: 'infantry', count: 8 },
    { unit: 'aa', count: 1 },
  ],
  // Local Militia — cheap, numerous, fragile; raised from a controlled
  // city's population (CONCEPT.md source #3). Cheapest foot unit in bulk
  // plus a couple of light scout vehicles, no armor at all.
  militia: [
    { unit: 'militia', count: 12 },
    { unit: 'scout', count: 2 },
  ],
};

// Defensive dev-time check (fails fast on a typo'd UNIT_DEFS key rather than
// silently spawning nothing for that entry) — every template's every `unit`
// key must exist in the real roster.
for (const [type, comp] of Object.entries(BATTALION_TEMPLATES)) {
  for (const { unit } of comp) {
    if (!UNIT_DEFS[unit]) throw new Error(`[battalions] template "${type}" references unknown UNIT_DEFS key "${unit}"`);
  }
}

// ---------------------------------------------------------------------------
// NAMING — small per-side, per-type ordinal counters ("1st Armored", "3rd
// Rifle", "2nd Militia") so repeated spawns of the same type/side don't all
// share a name. Purely cosmetic; later phases are free to replace this with
// something richer (e.g. numbered off a real order-of-battle) without any
// other part of this module caring.
const ORDINALS = ['1st', '2nd', '3rd', '4th', '5th', '6th', '7th', '8th', '9th', '10th'];
function ordinal(n) { return ORDINALS[n - 1] || `${n}th`; }
const TYPE_LABEL = { elite: 'Armored', standard: 'Rifle', militia: 'Militia' };
const nameCounters = new Map(); // `${side}:${type}` -> count so far
function nextBattalionName(side, type) {
  const key = `${side}:${type}`;
  const n = (nameCounters.get(key) || 0) + 1;
  nameCounters.set(key, n);
  return `${ordinal(n)} ${TYPE_LABEL[type] || type}`;
}

let nextBattalionId = 1;

// ---------------------------------------------------------------------------
// spawnBattalion — creates a battalion record and spawns each constituent
// unit from its template via the EXISTING spawnUnit(world, key, x, y, side)
// (game/units.js), scattered in a small cluster around (x,y) so they don't
// all stack on one point. Every spawned unit is tagged `u.battalion = id`
// (a plain extra field on the unit object — units.js never reads or
// branches on it, so this is invisible to combat/movement/rendering).
//
// `map` is accepted (and threaded through in case a future revision wants to
// land-snap spawn points the way main.js's spawnGroundUnit does) but B1
// itself doesn't need it — the scatter offsets here are small enough that
// callers are expected to hand this a point already known to be legal
// ground, same responsibility a caller of spawnUnit already has today.
export function spawnBattalion(world, map, { type, x, y, side, name }) {
  const template = BATTALION_TEMPLATES[type];
  if (!template) throw new Error(`[battalions] unknown battalion type "${type}"`);

  const bn = {
    id: nextBattalionId++,
    name: name || nextBattalionName(side, type),
    type,
    side,
    units: [],
    // B3 fields (inert until then — see this file's header):
    homeSector: null,
    stance: 'balanced',
    // B4 fields (inert until then):
    morale: 100,
    cohesion: 100,
  };

  // Scatter constituents in a small cluster around (x,y): a simple
  // sunflower-style spiral (golden-angle step) so successive slots spread
  // out without any bookkeeping about which offsets are "taken" — same
  // technique game/doctrine.js already uses for garrison slot placement
  // (see that file's GOLDEN_ANGLE), reused here purely as scatter geometry
  // with no behavioral tie to doctrine itself.
  const GOLDEN_ANGLE = 2.399963;
  const RING_STEP = 14; // px of extra radius per further-out slot
  let slot = 0;
  for (const { unit: key, count } of template) {
    for (let i = 0; i < count; i++) {
      const angle = slot * GOLDEN_ANGLE;
      const radius = RING_STEP * Math.sqrt(slot + 1);
      const sx = x + Math.cos(angle) * radius;
      const sy = y + Math.sin(angle) * radius;
      const u = spawnUnit(world, key, sx, sy, side);
      u.battalion = bn.id;
      bn.units.push(u);
      slot++;
    }
  }

  world.battalions.push(bn);
  return bn;
}

// ---------------------------------------------------------------------------
// battalionStrength — read-only summary for later phases/UI (B4's morale
// display, B6's operational-map icon tint, etc). Never mutates anything.
export function battalionStrength(bn) {
  let alive = 0, hp = 0, maxHp = 0;
  for (const u of bn.units) {
    if (u.hp > 0) alive++;
    hp += Math.max(0, u.hp);
    maxHp += u.maxHp;
  }
  return { alive, total: bn.units.length, hpFrac: maxHp > 0 ? hp / maxHp : 0 };
}

// ---------------------------------------------------------------------------
// updateBattalions — light per-frame maintenance pass. Prunes dead/removed
// units from each battalion's `units` list and drops battalions left with no
// units. O(total sub-units across all battalions) per frame — cheap even at
// this game's scale, and safe to call every frame right after updateUnits
// (main.js's loop()) so it always prunes against this frame's freshly
// resolved hp/removal state.
//
// This function ONLY reads unit state (u.hp, world.units) and ONLY writes
// the battalion record's own `units` array — per this file's header
// contract, it must never alter unit behavior, combat, or rendering.
export function updateBattalions(world, dt) {
  if (!world.battalions || world.battalions.length === 0) return;
  const live = new Set(world.units); // world.units already strips hp<=0 each frame (units.js), but a unit could also be removed some other way — checking membership here rather than trusting hp alone is the belt-and-braces version.
  for (const bn of world.battalions) {
    bn.units = bn.units.filter(u => u.hp > 0 && live.has(u));
  }
  world.battalions = world.battalions.filter(bn => bn.units.length > 0);
}

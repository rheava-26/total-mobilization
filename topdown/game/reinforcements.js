// REINFORCEMENTS — B2 of the 6-phase battalion rework ("Production emits
// battalions (three sources)" — docs/CONCEPT.md, "Ground combat model:
// BATTALIONS, not singular units"). This is a NEW, ADDITIVE module: it does
// NOT touch game/production.js's PRODUCTION_DEFS, the per-facility unit
// production tick, the tech tree, the goal system, the balance harness, or
// air/naval/strike production. That whole arsenal (aircraft, warships,
// drones, missiles, hypersonics, air defense, etc.) stays exactly as-is — it
// is the "individual physical craft" side of the roster and this phase does
// not demote it (that's a LATER phase, not B2). Battalions are the ground-
// maneuver force and arrive through THIS parallel subsystem instead.
//
// THE THREE SOURCES (CONCEPT.md's "three sources of battalions"), each a
// REQUEST FUNCTION that:
//   1. checks a gate (Elite only: a complete player tankFactory exists) and
//      affordability against the REAL economy pools (economy.ic/.manpower/
//      .resources[id] — game/economy.js) — refuses with a legible `reason`
//      and spends NOTHING if either check fails;
//   2. if ok, SPENDS THE FULL COST UP FRONT (mirrors game/production.js's
//      spend-then-produce shape and main.js's spendForBuilding/
//      canAffordBuilding pattern for buildings) — so a job can never be
//      queued for free — then pushes a job onto `world.reinforcements`;
//   3. updateReinforcements() ticks every queued job's timeLeft down each
//      frame and, on completion, resolves a spawn point and calls the real
//      spawnBattalion() (game/battalions.js, unchanged) to actually create
//      the formation.
//
// Costs/lead-times below are ALL DESIGNER'S TO TUNE first-pass placeholders,
// picked only to make the three routes clearly distinct (fast+cheap+fragile
// militia vs. slow+dear+elite factory formation) and observable within a
// short playtest — none of it is canon.

import { spawnBattalion, BATTALION_TEMPLATES } from './battalions.js';
import { terrainSample, MOVE_CLASSES } from './units.js';

// ---------------------------------------------------------------------------
// REINFORCEMENT_DEFS — DESIGNER'S TO TUNE. One entry per route. Shape
// mirrors game/production.js's PRODUCTION_DEFS recipe fields (resourceCost/
// icCost/manpowerCost) but flat (a one-shot job cost, not a per-unit rate)
// since a reinforcement request is a single lump purchase, not a continuous
// production line.
export const REINFORCEMENT_DEFS = {
  elite: {
    name: 'Factory Elite',
    battalionType: 'elite',
    // GATE: requires a complete, player-owned tankFactory on the map (your
    // own heavy industry forces this formation out) — see requiresFactory
    // check in requestElite below.
    requiresFactory: 'tankFactory',
    cost: { ic: 200, manpower: 40, resources: { steel: 70, chromium: 25, tungsten: 15 } },
    leadTime: 45, // seconds — long: this is the rare, powerful ace formation
  },
  standard: {
    name: 'Army-Sent Standard',
    battalionType: 'standard',
    requiresFactory: null, // no factory needed — the standing army dispatches it
    cost: { ic: 120, manpower: 30, resources: { steel: 35 } },
    leadTime: 25,
  },
  militia: {
    name: 'Local Militia',
    battalionType: 'militia',
    requiresFactory: null,
    // FIRST-PASS REBALANCE (two independent design reviews flagged militia as
    // trivially spammable — 14 bodies for near-nothing on a 5s timer dominated
    // the three-source choice). Militia is now clearly cheap-in-INDUSTRY but
    // expensive-in-POPULATION (manpower-heavy) and no longer instant, so
    // spamming it genuinely drains the manpower pool that everything else also
    // needs, and Standard/Elite become worth their cost for real striking
    // power. Still the cheap/fast/fragile option, just not free. NEEDS PLAYTEST
    // — the self-play balance harness drives the PRODUCTION path, not the
    // reinforcement panel, so these numbers are reasoned, not yet play-verified.
    cost: { ic: 20, manpower: 55, resources: {} },
    leadTime: 9, // still the fastest source, but not an instant army-in-a-click
  },
};

// ---------------------------------------------------------------------------
// canAfford / spend — generic over a REINFORCEMENT_DEFS-shaped cost object
// ({ic, manpower, resources:{id:amount}}), same direct-subtraction pattern
// game/production.js's updateProduction and main.js's spendForBuilding both
// already use. `spend` assumes canAfford was just checked true (no internal
// re-check) — callers below always check-then-spend in that order, never
// spend blind.
export function canAfford(economy, cost) {
  if (economy.ic < (cost.ic || 0)) return false;
  if (economy.manpower < (cost.manpower || 0)) return false;
  for (const [rid, amt] of Object.entries(cost.resources || {})) {
    if ((economy.resources[rid] || 0) < amt) return false;
  }
  return true;
}

export function spend(economy, cost) {
  economy.ic -= (cost.ic || 0);
  economy.manpower -= (cost.manpower || 0);
  for (const [rid, amt] of Object.entries(cost.resources || {})) {
    economy.resources[rid] -= amt;
  }
}

// ---------------------------------------------------------------------------
// playerCapital — the player-owned city with the largest `r` (city "size" —
// same size proxy game/economy.js's IC/manpower formulas already read off
// map.cities). Ownership field is `c.owner` (game/objectives.js:
// initCityOwnership sets it, updateObjectives flips it on capture — 'player'
// / 'enemy'). Returns null if the player owns no city at all (a legitimate
// state after a bad war — callers fall back to something else, see
// requestStandard/raiseMilitia below).
export function playerCapital(map) {
  let best = null;
  for (const c of (map.cities || [])) {
    if (c.owner !== 'player') continue;
    if (!best || (c.r || 0) > (best.r || 0)) best = c;
  }
  return best;
}

// ---------------------------------------------------------------------------
// SPAWN-POINT HELPERS — small, self-contained re-implementations of
// game/production.js's private findSpawnPoint/facilityRallyPoint (that file
// doesn't export them, and B2 must not touch production.js at all — see this
// file's header — so these are replicated here rather than imported).
// snapToGroundNear finds the nearest passable-for-ground-units point to a
// wx/wy, spiralling outward ring-by-ring exactly like production.js's own
// version; groundMoveClass is resolved once from a real UNIT_DEFS entry
// (game/battalions.js's 'infantry' template unit — any ground unit's
// moveClass reads the same terrain, so this doesn't need to be per-type).
const GROUND_MOVE_CLASS = MOVE_CLASSES['foot'] ? 'foot' : Object.keys(MOVE_CLASSES).find(k => !MOVE_CLASSES[k].ignoresTerrain);

function snapToGroundNear(map, wx, wy, maxTiles = 80) {
  const moveClass = MOVE_CLASSES[GROUND_MOVE_CLASS];
  if (!moveClass || moveClass.ignoresTerrain) return { x: wx, y: wy };
  if (terrainSample(map, moveClass, wx, wy).passable) return { x: wx, y: wy };
  const ts = map.tileSize;
  const gx0 = Math.floor(wx / ts), gy0 = Math.floor(wy / ts);
  for (let r = 1; r <= maxTiles; r++) {
    for (let dy = -r; dy <= r; dy++) {
      for (let dx = -r; dx <= r; dx++) {
        if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
        const wxp = (gx0 + dx + 0.5) * ts, wyp = (gy0 + dy + 0.5) * ts;
        if (terrainSample(map, moveClass, wxp, wyp).passable) return { x: wxp, y: wyp };
      }
    }
  }
  return { x: wx, y: wy };
}

// map.cities[].x/.y are TILE-GRID coordinates (see game/mapgen.js — the same
// space map.roadAt/map.width index into), NOT world pixels like every unit/
// building's .x/.y (which are already *map.tileSize). Every other consumer
// of a city's position converts this way (engine/renderer.js's city-marker
// draw call, main.js's spawn-point math) — replicated here rather than
// silently treating a city's tile coordinate as a pixel one, which would
// place a "spawn near the capital" battalion off in the map's low corner.
function cityWorldPoint(map, city) {
  return { x: city.x * map.tileSize, y: city.y * map.tileSize };
}

// Rally point just south of a building's footprint — same offset shape as
// production.js's facilityRallyPoint, replicated (not imported; see the
// comment above snapToGroundNear).
function buildingRallyPoint(map, b) {
  const ts = map.tileSize;
  const cx = (b.gx + b.def.footprint.w / 2) * ts;
  const cy = (b.gy + b.def.footprint.h) * ts + ts * 1.5;
  return { x: cx, y: cy };
}

function findCompleteBuilding(world, key) {
  for (const b of world.buildings) {
    if (b.side === 'player' && b.key === key && b.status === 'complete') return b;
  }
  return null;
}

// Fallback point when the player owns no city at all (playerCapital ->
// null): centroid of the player's own buildings, else units, else map
// center — "somewhere plausibly the player's own" rather than throwing.
function playerFallbackPoint(world, map) {
  const pts = [];
  for (const b of world.buildings) if (b.side === 'player') pts.push({ x: b.x, y: b.y });
  if (!pts.length) for (const u of world.units) if (u.side === 'player') pts.push({ x: u.x, y: u.y });
  if (!pts.length) return { x: (map.width * map.tileSize) / 2, y: (map.height * map.tileSize) / 2 };
  let sx = 0, sy = 0;
  for (const p of pts) { sx += p.x; sy += p.y; }
  return { x: sx / pts.length, y: sy / pts.length };
}

// ---------------------------------------------------------------------------
// REQUEST FUNCTIONS — each returns { ok, reason } (reason set only when
// ok:false); on success the job carries everything updateReinforcements
// needs to resolve a spawn point later WITHOUT re-deriving it now (so e.g.
// the elite job remembers exactly which tankFactory building forced it out,
// even if a second one gets built/destroyed before the job completes).

export function requestElite(world, economy, map) {
  const def = REINFORCEMENT_DEFS.elite;
  const factory = findCompleteBuilding(world, def.requiresFactory);
  if (!factory) return { ok: false, reason: 'requires a complete Tank Factory' };
  if (!canAfford(economy, def.cost)) return { ok: false, reason: 'cannot afford Factory Elite' };
  spend(economy, def.cost);
  world.reinforcements.push({
    route: 'elite', battalionType: def.battalionType,
    timeLeft: def.leadTime, totalTime: def.leadTime,
    target: { kind: 'building', buildingId: factory.id },
  });
  return { ok: true };
}

export function requestStandard(world, economy, map) {
  const def = REINFORCEMENT_DEFS.standard;
  if (!canAfford(economy, def.cost)) return { ok: false, reason: 'cannot afford Army-Sent Standard' };
  spend(economy, def.cost);
  world.reinforcements.push({
    route: 'standard', battalionType: def.battalionType,
    timeLeft: def.leadTime, totalTime: def.leadTime,
    target: { kind: 'capital' }, // resolved at completion — see resolveTargetPoint below
  });
  return { ok: true };
}

// `city` (optional) — which player-owned city to raise the militia from;
// defaults to the capital (playerCapital(map)) per the task brief. Refuses
// if the resolved city isn't player-owned (a caller-passed city could be
// stale/contested) — same "gate checked, no partial spend" discipline as
// requestElite's factory gate.
export function raiseMilitia(world, economy, map, city) {
  const def = REINFORCEMENT_DEFS.militia;
  const targetCity = city || playerCapital(map);
  if (!targetCity || targetCity.owner !== 'player') {
    return { ok: false, reason: 'requires a player-owned city' };
  }
  if (!canAfford(economy, def.cost)) return { ok: false, reason: 'cannot afford Local Militia' };
  spend(economy, def.cost);
  world.reinforcements.push({
    route: 'militia', battalionType: def.battalionType,
    timeLeft: def.leadTime, totalTime: def.leadTime,
    target: { kind: 'city', cityName: targetCity.name },
  });
  return { ok: true };
}

// ---------------------------------------------------------------------------
// resolveTargetPoint — turns a job's `target` descriptor into an actual
// world-space spawn point AT COMPLETION TIME (not at request time), so a
// tankFactory destroyed mid-build or a city recaptured mid-build degrades
// gracefully instead of spawning nowhere:
//   'building' -> that building's rally point if it still exists & is
//                 complete, else falls through to the player fallback point.
//   'capital'  -> the CURRENT capital (re-resolved, not cached — the capital
//                 could change while the job was in flight) if the player
//                 still owns one, else the player fallback point.
//   'city'     -> the named city if the player still owns it, else the
//                 current capital, else the player fallback point.
function resolveTargetPoint(world, map, target) {
  if (target.kind === 'building') {
    const b = world.buildings.find(x => x.id === target.buildingId && x.status === 'complete');
    if (b) return buildingRallyPoint(map, b);
    return playerFallbackPoint(world, map);
  }
  if (target.kind === 'capital') {
    const cap = playerCapital(map);
    if (cap) return cityWorldPoint(map, cap);
    return playerFallbackPoint(world, map);
  }
  if (target.kind === 'city') {
    const city = (map.cities || []).find(c => c.name === target.cityName && c.owner === 'player');
    if (city) return cityWorldPoint(map, city);
    const cap = playerCapital(map);
    if (cap) return cityWorldPoint(map, cap);
    return playerFallbackPoint(world, map);
  }
  return playerFallbackPoint(world, map);
}

// ---------------------------------------------------------------------------
// updateReinforcements — ticks every queued job's timeLeft down; on
// completion, resolves its spawn point (snapped to legal ground for a
// battalion, same discipline production.js's findSpawnPoint uses for units)
// and calls the real spawnBattalion(). Cheap — O(jobs in flight), which this
// game's scale keeps small. Called once per frame from main.js's loop(),
// near updateBattalions.
export function updateReinforcements(world, economy, map, dt) {
  if (!world.reinforcements || world.reinforcements.length === 0) return;
  const done = [];
  for (const job of world.reinforcements) {
    job.timeLeft -= dt;
    if (job.timeLeft <= 0) done.push(job);
  }
  if (!done.length) return;
  world.reinforcements = world.reinforcements.filter(j => !done.includes(j));
  for (const job of done) {
    const pt = resolveTargetPoint(world, map, job.target);
    const spawnPt = snapToGroundNear(map, pt.x, pt.y);
    spawnBattalion(world, map, { type: job.battalionType, x: spawnPt.x, y: spawnPt.y, side: 'player' });
  }
}

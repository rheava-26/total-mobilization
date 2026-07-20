// Player/enemy BUILDINGS — real entities (not scenery), CONCEPT.md Pillar 1
// & 3: what you build is on the map, changes the map, blocks and gets shot
// at like anything else. No economy yet (P3): construction is instant and
// free — BUILDING_DEFS is pure data, same attribute philosophy as
// game/units.js's UNIT_DEFS, so a new building type is a new entry here, not
// new code.
//
// A building entity looks like: { id, key, def, side, x, y, gx, gy, hp,
// maxHp, aim, cd, target }. x/y are the WORLD-px footprint CENTER (what
// pickTarget/fireProjectile/resolveHit in game/units.js expect from any
// shooter/target shape); gx/gy is the footprint's top-left TILE. `def` is a
// per-instance shallow clone of the BUILDING_DEFS entry with `radius` filled
// in (derived from footprint x map.tileSize at spawn time, since footprint
// is in tiles but a unit's def.radius is a world-px constant) — that's what
// lets resolveHit/pickTarget treat a building exactly like a unit with no
// special-casing per entity kind.
import { pickTarget, nearestEnemy, fireProjectile, claim, claimOf } from './units.js';

// ---------------------------------------------------------------------------
// BUILDING ROSTER — pure data. footprint is in TILES (w x h); hp/vision are
// the same units a unit's own def uses (hp: raw, vision: world-px sensor
// range consumed by game/fog.js). Only gunEmplacement carries a `weapon` +
// dmg/range/rate (same split as UNIT_DEFS: the weapon def owns projectile
// physics/targeting scope, dmg/range/rate live on the shooter).
export const BUILDING_DEFS = {
  factory: {
    name: 'Factory', footprint: { w: 4, h: 3 }, hp: 420, vision: 130,
  },
  barracks: {
    name: 'Barracks', footprint: { w: 3, h: 2 }, hp: 260, vision: 150,
  },
  supplyDepot: {
    name: 'Supply Depot', footprint: { w: 2, h: 2 }, hp: 190, vision: 110,
  },
  radar: {
    // the whole point of this building: vision far beyond anything a unit
    // carries, plugged straight into game/fog.js as just another viewer.
    name: 'Radar Station', footprint: { w: 2, h: 2 }, hp: 150, vision: 780,
  },
  gunEmplacement: {
    // static autocannon — reuses the exact unit weapon/fire-discipline
    // pipeline (game/units.js pickTarget/fireProjectile/claim) via
    // updateBuildings below, it just never moves or chases.
    name: 'Gun Emplacement', footprint: { w: 1, h: 1 }, hp: 140, vision: 230,
    weapon: 'autocannon', dmg: 5, range: 210, rate: 0.32,
  },
};

export function footprintTiles(def, gx, gy) {
  const tiles = [];
  for (let dy = 0; dy < def.footprint.h; dy++) {
    for (let dx = 0; dx < def.footprint.w; dx++) tiles.push({ x: gx + dx, y: gy + dy });
  }
  return tiles;
}

// Buildable terrain (terrain-defs.json's own `buildable` flag — already
// false for forest/marsh/mountain/water) + no overlap with roads or an
// existing obstacle (another building). Shared by both the planner's spiral
// search and the pin-override's exact-tile check, and by the B-mode preview
// so what the player sees green/red for is the literal rule being applied.
export function isValidPlacement(map, gx, gy, def) {
  if (gx < 0 || gy < 0 || gx + def.footprint.w > map.width || gy + def.footprint.h > map.height) return false;
  for (const t of footprintTiles(def, gx, gy)) {
    const terrain = map.terrainAt(t.x, t.y);
    if (!terrain || !terrain.buildable) return false;
    if (map.roadAt(t.x, t.y) > 0) return false;
    if (map.blockAt(t.x, t.y)) return false;
  }
  return true;
}

function isNearRoad(map, gx, gy, def, r = 3) {
  for (let dy = -r; dy < def.footprint.h + r; dy++) {
    for (let dx = -r; dx < def.footprint.w + r; dx++) {
      if (map.roadAt(gx + dx, gy + dy) > 0) return true;
    }
  }
  return false;
}

function nearestCityTileDist(map, gx, gy, def) {
  const cx = gx + def.footprint.w / 2, cy = gy + def.footprint.h / 2;
  let best = Infinity;
  for (const c of (map.cities || [])) {
    const d = Math.hypot(cx - c.x, cy - c.y) - (c.r || 0);
    if (d < best) best = d;
  }
  return Math.max(0, best);
}

// DELEGATED CONSTRUCTION planner (CONCEPT.md Pillar 1's signature verb): the
// player states roughly where; this searches outward in growing square rings
// from the click for legal footprints, scored by a mild preference for
// near-road and near-city siting (per CONCEPT.md's open question: "simple
// scoring — near roads/power ... is probably enough to start"). Stops
// scanning a couple of rings after the FIRST ring that has any valid site at
// all, so a good nearby spot wins on score without the search wandering
// arbitrarily far from where the player actually clicked.
export function sitePlacement(map, key, clickWx, clickWy, maxR = 30) {
  const def = BUILDING_DEFS[key];
  if (!def) return null;
  const ts = map.tileSize;
  const cgx = Math.round(clickWx / ts - def.footprint.w / 2);
  const cgy = Math.round(clickWy / ts - def.footprint.h / 2);
  let best = null, bestScore = -Infinity, firstHitRing = -1;
  for (let r = 0; r <= maxR; r++) {
    if (firstHitRing >= 0 && r > firstHitRing + 2) break;
    let hitThisRing = false;
    for (let dy = -r; dy <= r; dy++) {
      for (let dx = -r; dx <= r; dx++) {
        if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
        const gx = cgx + dx, gy = cgy + dy;
        if (!isValidPlacement(map, gx, gy, def)) continue;
        hitThisRing = true;
        const roadBonus = isNearRoad(map, gx, gy, def) ? 5 : 0;
        const cityDist = nearestCityTileDist(map, gx, gy, def);
        const cityBonus = Math.max(0, 6 - cityDist * 0.1);
        const score = roadBonus + cityBonus - r * 0.4;
        if (score > bestScore) { bestScore = score; best = { x: gx, y: gy }; }
      }
    }
    if (hitThisRing && firstHitRing < 0) firstHitRing = r;
  }
  return best;
}

let nextBuildingId = 1;

export function spawnBuilding(world, map, key, gx, gy, side) {
  const base = BUILDING_DEFS[key];
  if (!base) throw new Error(`unknown building type "${key}"`);
  const ts = map.tileSize;
  const radius = Math.hypot(base.footprint.w * ts, base.footprint.h * ts) / 2;
  const def = { ...base, radius };
  const x = (gx + base.footprint.w / 2) * ts, y = (gy + base.footprint.h / 2) * ts;
  const b = {
    id: nextBuildingId++, key, def, side, x, y, gx, gy,
    hp: def.hp, maxHp: def.hp, aim: 0, cd: Math.random() * (def.rate || 1),
    target: null,
  };
  for (const t of footprintTiles(def, gx, gy)) map.setBlock(t.x, t.y, 1);
  world.buildings.push(b);
  // obstacle layer changed: re-prime the terrain prerender cache AND
  // invalidate game/pathfind.js's path cache, both keyed off map.version.
  map.dirty();
  return b;
}

function removeBuilding(world, map, b) {
  for (const t of footprintTiles(b.def, b.gx, b.gy)) map.setBlock(t.x, t.y, 0);
  map.addScorch(b.x, b.y, b.def.radius);
  map.dirty(); // clears the obstacle, re-primes pathing, bakes the scorch decal into the next prerender
  const i = world.buildings.indexOf(b);
  if (i >= 0) world.buildings.splice(i, 1);
}

// Per-frame building update: weaponed buildings (gun emplacement) acquire
// and fire on targets through the EXACT SAME pipeline a unit uses
// (pickTarget/nearestEnemy/fireProjectile/claim from game/units.js) — static
// disposition only, never chases (there is no movement code path for a
// building at all). Then sweeps for destroyed buildings (hp<=0, set by
// resolveHit during this frame's updateProjectiles call) and cleans them up.
// Mirrors game/units.js's updateUnits: fire/act this frame, filter deaths
// from LAST frame's hits — same one-frame lag as the unit roster already has.
export function updateBuildings(world, dt, map) {
  for (const b of world.buildings) {
    if (b.hp <= 0 || !b.def.weapon) continue;
    b.cd -= dt;
    const target = pickTarget(world, b) || nearestEnemy(world, b);
    b.target = target;
    if (!target) continue;
    b.aim = Math.atan2(target.y - b.y, target.x - b.x);
    const d = Math.hypot(target.x - b.x, target.y - b.y);
    if (d <= b.def.range && b.cd <= 0 && claimOf(target) < target.hp) {
      b.cd = b.def.rate;
      fireProjectile(world, b, target);
      claim(target, b.def.dmg);
    }
  }
  for (const b of world.buildings.slice()) {
    if (b.hp <= 0) removeBuilding(world, map, b);
  }
}

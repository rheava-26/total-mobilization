// Player/enemy BUILDINGS — real entities (not scenery), CONCEPT.md Pillar 1
// & 3: what you build is on the map, changes the map, blocks and gets shot
// at like anything else. P3 (game/economy.js): construction now COSTS IC
// (+manpower for one type) and TAKES TIME — a building spawns in `status:
// 'constructing'` and only starts doing its economic/combat job once it
// reaches `status: 'complete'`. BUILDING_DEFS is still pure data, same
// attribute philosophy as game/units.js's UNIT_DEFS — a new building type is
// a new entry here (data + `cost`/`buildTime`/`econ`), not new code.
//
// A building entity looks like: { id, key, def, side, x, y, gx, gy, hp,
// maxHp, aim, cd, target, status, buildProgress, buildTime }. x/y are the
// WORLD-px footprint CENTER (what pickTarget/fireProjectile/resolveHit in
// game/units.js expect from any shooter/target shape); gx/gy is the
// footprint's top-left TILE. `def` is a per-instance shallow clone of the
// BUILDING_DEFS entry with `radius` filled in (derived from footprint x
// map.tileSize at spawn time, since footprint is in tiles but a unit's
// def.radius is a world-px constant) — that's what lets
// resolveHit/pickTarget treat a building exactly like a unit with no
// special-casing per entity kind.
import { pickTarget, nearestEnemy, fireProjectile, claim, claimOf } from './units.js';

// A building under construction starts at this fraction of its full HP (a
// fragile scaffold, not a one-shot-killable paper target) and grows toward
// full HP as buildProgress advances; damage taken mid-construction still
// subtracts normally and is NOT healed back by construction progress.
// DESIGNER'S TO TUNE.
export const CONSTRUCTION_HP_FLOOR = 0.2;
// Vision multiplier applied while a building is still under construction —
// the scaffold doesn't have its finished sensor/vantage yet. DESIGNER'S TO
// TUNE. Read by game/fog.js via entityVision() below so units and buildings
// share one vision rule with no per-kind special-casing.
export const CONSTRUCTION_VISION_MULT = 0.35;

// ---------------------------------------------------------------------------
// BUILDING ROSTER — pure data. footprint is in TILES (w x h); hp/vision are
// the same units a unit's own def uses (hp: raw, vision: world-px sensor
// range consumed by game/fog.js). Only gunEmplacement carries a `weapon` +
// dmg/range/rate (same split as UNIT_DEFS: the weapon def owns projectile
// physics/targeting scope, dmg/range/rate live on the shooter).
//
// `cost` (IC, optional manpower) and `buildTime` (seconds) are P3's
// construction economy — DESIGNER'S TO TUNE, picked only to (a) let a fresh
// game demo-afford at least one building immediately off the P3 starting IC
// pool (game/economy.js STARTING_IC) and (b) make timed construction
// actually observable within a short playtest.
//
// `econ` is the PATTERN this phase wires buildings into the economy with —
// plain data read by game/economy.js's updateEconomy, only for buildings
// with status 'complete'. Two of the five double as the designer-requested
// upgrade levers: factory is the "arm better" lever (armingBonus raises
// military output QUALITY), barracks is the "accelerate the ramp" lever
// (mobilizationRateBonus steepens the automatic mobilization curve). Radar
// and gunEmplacement carry no econ block — they keep their P2 vision/combat
// roles unchanged. DESIGNER'S TO TUNE — the specific two levers and their
// magnitudes are placeholders demonstrating the pattern, not canon.
export const BUILDING_DEFS = {
  factory: {
    name: 'Factory', footprint: { w: 4, h: 3 }, hp: 420, vision: 130,
    cost: { ic: 150 }, buildTime: 25,
    // ARM-BETTER lever: +IC/sec (raises output QUANTITY via the IC feeding
    // militaryOutputRate) and +armingBonus (raises output QUALITY directly).
    econ: { icRate: 0.5, armingBonus: 0.15 },
  },
  barracks: {
    name: 'Barracks', footprint: { w: 3, h: 2 }, hp: 260, vision: 150,
    cost: { ic: 90 }, buildTime: 18,
    // ACCELERATE lever: +manpower/sec, and +mobilizationRateBonus steepens
    // the automatic ramp itself (mobilizes faster than the default clock).
    econ: { manpowerRate: 0.6, mobilizationRateBonus: 0.15 },
  },
  supplyDepot: {
    name: 'Supply Depot', footprint: { w: 2, h: 2 }, hp: 190, vision: 110,
    cost: { ic: 70 }, buildTime: 14,
    // raises the stockpile caps — pools can't exceed IC_CAP_BASE/
    // MANPOWER_CAP_BASE (game/economy.js) without one of these.
    econ: { icCapBonus: 200, manpowerCapBonus: 150 },
  },
  radar: {
    // the whole point of this building: vision far beyond anything a unit
    // carries, plugged straight into game/fog.js as just another viewer.
    // No econ block — P3 doesn't touch radar's role.
    name: 'Radar Station', footprint: { w: 2, h: 2 }, hp: 150, vision: 780,
    cost: { ic: 110 }, buildTime: 20,
  },
  gunEmplacement: {
    // static autocannon — reuses the exact unit weapon/fire-discipline
    // pipeline (game/units.js pickTarget/fireProjectile/claim) via
    // updateBuildings below, it just never moves or chases. No econ block —
    // P3 doesn't touch its combat role. Costs a little manpower too (crew),
    // demonstrating the manpower-cost path alongside the IC-only buildings.
    name: 'Gun Emplacement', footprint: { w: 1, h: 1 }, hp: 140, vision: 230,
    weapon: 'autocannon', dmg: 5, range: 210, rate: 0.32,
    cost: { ic: 60, manpower: 10 }, buildTime: 12,
  },
  // MINE — the resource-supply-substrate follow-up to P3 (game/resources.js,
  // game/mapgen.js's map.deposits). ONE generic mine type that adapts to
  // whatever deposit it's sited on, rather than a per-resource building
  // roster — simpler, and there's nothing a per-resource mine would do
  // differently yet (no recipes/tiers exist to distinguish them at this
  // phase). `requiresDeposit: true` is the siting rule: isValidPlacement
  // below rejects any site whose footprint isn't on/adjacent to a
  // map.depositAt() hit, so a mine placed with no nearby deposit is refused
  // at siting rather than silently built idle — the player never ends up
  // with a mine quietly producing nothing without being told why. Once
  // built, spawnBuilding binds it to the deposit it landed on
  // (b.resourceType/b.richness); game/economy.js reads that binding each
  // tick to feed the matching resource stockpile (see MINE_TUNABLES in
  // game/resources.js for the extraction-rate formula). No econ block here —
  // resource extraction is a separate accounting path from IC/manpower.
  mine: {
    name: 'Mine', footprint: { w: 2, h: 2 }, hp: 160, vision: 90,
    cost: { ic: 60 }, buildTime: 16,
    requiresDeposit: true,
  },

  // ---------------------------------------------------------------------
  // RESEARCH BUREAU (P4 follow-up — game/research.js, CONCEPT.md's settled
  // "Tech / research model" paragraph). The UNIVERSAL entry gate every
  // advanced tech requires on top of its own category-specific facility
  // (game/research.js TECH_DEFS' prerequisiteBuildings always lists this
  // first) — "build a lab before you can research anything" is the clearest
  // possible first tech step, and it means a fresh player who rushes
  // straight for e.g. a Drone Works still can't skip the R&D step by
  // accident; they need this too. Deliberately cheap/fast relative to the
  // production facilities below (this unlocks nothing by itself — it only
  // OPENS the possibility of researching — so it shouldn't compete on cost
  // with the heavy industry it gates) but not free, so it's still a real
  // build-order decision. No econ block: unlike factory/barracks this
  // building's payoff is entirely through game/research.js, not the
  // IC/manpower/mobilization pipeline.
  researchBureau: {
    name: 'Research Bureau', footprint: { w: 2, h: 2 }, hp: 170, vision: 100,
    cost: { ic: 120, manpower: 5 }, buildTime: 18,
  },

  // ---------------------------------------------------------------------
  // PRODUCTION FACILITIES (P4 — CONCEPT.md's settled "Production & supply
  // chain" ledger). Pure structural data, same as every other building
  // here — footprint/hp/vision/cost/buildTime, sited and constructed
  // through the exact same B-mode costed+timed pipeline as a factory or a
  // mine. What makes these PRODUCTION facilities (auto-produce a unit
  // category once complete, gated on a recipe of resources + prerequisite
  // buildings) is entirely in game/production.js's PRODUCTION_DEFS table,
  // which maps a category to one of these building keys by name — this
  // file stays unaware that "tankFactory" means anything beyond its own
  // footprint/cost/buildTime, same separation as mine not knowing what a
  // resource recipe is. DESIGNER'S TO TUNE — footprints/hp/costs are
  // first-pass placeholders sized only to be affordable within a short
  // playtest, not balanced for a real campaign.
  ammunitionPlant: {
    // the prerequisite building CONCEPT.md's illustrative tank-line example
    // names ("chromium + tungsten + steel + an ammunition plant") — gates
    // production, does not itself consume resources or produce units.
    name: 'Ammunition Plant', footprint: { w: 3, h: 2 }, hp: 220, vision: 100,
    cost: { ic: 120 }, buildTime: 20,
  },
  tankFactory: {
    name: 'Tank Factory', footprint: { w: 4, h: 3 }, hp: 380, vision: 120,
    cost: { ic: 180, manpower: 10 }, buildTime: 28,
  },
  aircraftPlant: {
    name: 'Aircraft Plant', footprint: { w: 4, h: 4 }, hp: 340, vision: 150,
    cost: { ic: 200, manpower: 10 }, buildTime: 30,
  },
  shipyard: {
    // no requiresDeposit-style siting rule yet (unlike mine) — a shipyard
    // built far from open water will still complete and still try to
    // produce, it'll just rally its ships a long way to reach the nearest
    // water (game/production.js findSpawnPoint). Flagged as a follow-up for
    // whoever tunes siting rules further, not treated as a bug here.
    name: 'Shipyard', footprint: { w: 5, h: 3 }, hp: 360, vision: 130,
    cost: { ic: 210, manpower: 12 }, buildTime: 32,
  },
  artilleryWorks: {
    name: 'Artillery Works', footprint: { w: 3, h: 3 }, hp: 300, vision: 110,
    cost: { ic: 150, manpower: 8 }, buildTime: 24,
  },

  // -------------------------------------------------------------------------
  // GROUNDED NEAR-FUTURE PRODUCTION FACILITIES — one new building per new
  // game/production.js category (drones/missiles/hypersonics/airDefense/
  // guidedWeapons/support). Same pure structural data as every facility
  // above; footprint/hp/cost/buildTime scaled against the existing four
  // (tankFactory/aircraftPlant/shipyard/artilleryWorks) rather than a fresh
  // scale, and roughly ordered cheapest-to-priciest to mirror the recipe
  // cost spread in production.js (drones cheapest/fastest, hypersonicWorks
  // the priciest/slowest — the "bleeding edge" facility, deliberately
  // gated behind ALSO having a missileWorks per its recipe's
  // prerequisiteBuildings, same "buildings gate other buildings' output"
  // shape ammunitionPlant already demonstrates for tanks/aircraft).
  droneWorks: {
    name: 'Drone Works', footprint: { w: 3, h: 2 }, hp: 200, vision: 110,
    cost: { ic: 100, manpower: 4 }, buildTime: 16,
  },
  missileWorks: {
    name: 'Missile Works', footprint: { w: 4, h: 3 }, hp: 340, vision: 130,
    cost: { ic: 220, manpower: 12 }, buildTime: 30,
  },
  hypersonicWorks: {
    name: 'Hypersonic Works', footprint: { w: 4, h: 4 }, hp: 400, vision: 160,
    cost: { ic: 320, manpower: 18 }, buildTime: 42,
  },
  airDefenseWorks: {
    name: 'Air Defense Works', footprint: { w: 3, h: 3 }, hp: 260, vision: 140,
    cost: { ic: 160, manpower: 8 }, buildTime: 22,
  },
  guidedWeaponsWorks: {
    name: 'Guided Weapons Works', footprint: { w: 3, h: 2 }, hp: 220, vision: 110,
    cost: { ic: 130, manpower: 6 }, buildTime: 20,
  },
  signalsWorks: {
    name: 'Signals Works', footprint: { w: 2, h: 2 }, hp: 170, vision: 120,
    cost: { ic: 110, manpower: 4 }, buildTime: 16,
  },

  // -------------------------------------------------------------------------
  // INTERMEDIATE-COMPONENT FACILITIES (Part B — game/production.js's new
  // `producesResource` mode). Structurally these are ORDINARY production
  // facilities — same footprint/hp/cost/buildTime shape as every building
  // above, sited/constructed through the identical costed+timed pipeline —
  // the only thing distinguishing them is that game/production.js's
  // PRODUCTION_DEFS points their category at a RESOURCE output instead of a
  // UNIT_DEFS list, so this file (same as with `mine`) stays unaware of
  // what a "component" even is. Deliberately left UNGATED (no TECH_DEFS
  // entry, no prerequisiteBuildings) — they sit upstream of several other
  // categories' recipes now (missiles/hypersonics/airDefense/guidedWeapons/
  // support all draw on Electronics Plant's output; hypersonics also draws
  // on Alloy Forge's), so gating either behind research would gate five
  // categories transitively through one lock, which is more "over-gating"
  // than this pass wants — a fresh player can build either any time they
  // can afford it and have the raw feedstock, per the task's "don't
  // over-gate" instruction.
  electronicsPlant: {
    name: 'Electronics Plant', footprint: { w: 3, h: 2 }, hp: 200, vision: 110,
    cost: { ic: 130, manpower: 5 }, buildTime: 20,
  },
  alloyForge: {
    name: 'Alloy Forge', footprint: { w: 3, h: 2 }, hp: 240, vision: 100,
    cost: { ic: 150, manpower: 6 }, buildTime: 22,
  },
};

// Generic vision-range accessor shared by game/fog.js for both units and
// buildings: a unit has no `status` field so it always falls through to the
// full def.vision branch; a building under construction reads its reduced
// scaffold vision instead. Keeping this here (not duplicated in fog.js)
// means CONSTRUCTION_VISION_MULT has exactly one definition.
export function entityVision(e) {
  const base = (e.def && e.def.vision) || 0;
  return e.status === 'constructing' ? base * CONSTRUCTION_VISION_MULT : base;
}

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
  if (def.requiresDeposit && !footprintHasDeposit(map, gx, gy, def)) return false;
  return true;
}

// A building whose def sets `requiresDeposit` (currently just `mine`) may
// only site where its footprint touches a map deposit — "touches" meaning
// within map.depositAt's shared radius (engine/tilemap.js, sourced from
// game/mapgen.js DEPOSIT_TUNABLES.RADIUS), checked per footprint tile so a
// deposit anchored just off one corner of a multi-tile footprint still
// counts as "adjacent."
export function footprintHasDeposit(map, gx, gy, def) {
  if (!map.depositAt) return false;
  for (const t of footprintTiles(def, gx, gy)) {
    if (map.depositAt(t.x, t.y)) return true;
  }
  return false;
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

// Places a building's FOOTPRINT immediately (it blocks pathing and terrain
// from the instant it's sited — a construction site is a real obstacle, not
// a ghost) but starts it in `status: 'constructing'` at CONSTRUCTION_HP_FLOOR
// of its full HP. game/economy.js's cost check/spend happens in the CALLER
// (main.js's B-mode click handler) before this is invoked — this function
// itself is unconditional, same as before P3 (a test harness or a future
// scripted/free spawn can still call it directly).
export function spawnBuilding(world, map, key, gx, gy, side) {
  const base = BUILDING_DEFS[key];
  if (!base) throw new Error(`unknown building type "${key}"`);
  const ts = map.tileSize;
  const radius = Math.hypot(base.footprint.w * ts, base.footprint.h * ts) / 2;
  const def = { ...base, radius };
  const x = (gx + base.footprint.w / 2) * ts, y = (gy + base.footprint.h / 2) * ts;
  const b = {
    id: nextBuildingId++, key, def, side, x, y, gx, gy,
    hp: Math.max(1, Math.round(def.hp * CONSTRUCTION_HP_FLOOR)), maxHp: def.hp,
    aim: 0, cd: Math.random() * (def.rate || 1), target: null,
    status: 'constructing', buildProgress: 0, buildTime: Math.max(0.1, def.buildTime || 10),
  };
  // DEPOSIT BINDING (mine): resolved once, here, at siting time — a
  // building's footprint doesn't move, so whatever deposit it landed on is
  // fixed for its lifetime. isValidPlacement is what normally guarantees
  // this finds one for a `requiresDeposit` building (the caller checks it
  // first); this still degrades gracefully to "no resource, no output"
  // rather than throwing if something calls spawnBuilding directly without
  // that check (a test harness, e.g.) — same defensive spirit as the rest of
  // this function being unconditional.
  if (base.requiresDeposit) {
    let dep = null;
    for (const t of footprintTiles(def, gx, gy)) {
      dep = map.depositAt && map.depositAt(t.x, t.y);
      if (dep) break;
    }
    b.resourceType = dep ? dep.type : null;
    b.richness = dep ? dep.richness : 0;
  }
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

// ---------------------------------------------------------------------------
// SELL / DEMOLISH — a MINIMAL player-initiated "a building goes away" action
// (didn't exist before this pass; removeBuilding above was only ever reached
// via hp<=0 destruction). Added because game/director.js's Part B reactive
// system needs a real sell/delete action point to hook the self-sabotage gag
// chain onto (CONCEPT.md's settled "Reactive / adaptive guidance"
// paragraph), and because it's generally useful on its own — a player should
// be able to reclaim a mis-sited or obsolete building rather than being
// stuck with every placement forever. Refunds a flat FRACTION of the
// building's IC cost (not the full price — a demolition isn't a free undo)
// and otherwise goes through the exact same removeBuilding path a combat
// kill does (same obstacle-clear, same scorch mark, same map.dirty()) so
// there's only one "a building goes away" code path in this file.
export const SELL_REFUND_FRACTION = 0.5; // DESIGNER'S TO TUNE
export function sellBuilding(world, map, b, economy) {
  const refund = Math.round(((b.def.cost && b.def.cost.ic) || 0) * SELL_REFUND_FRACTION);
  if (economy) economy.ic = Math.min(economy.icCap ?? Infinity, economy.ic + refund);
  removeBuilding(world, map, b);
  return refund;
}

// Per-frame building update: FIRST advances construction (P3) — a
// 'constructing' building gains HP proportional to how much progress it made
// THIS frame (not recomputed from progress wholesale, so damage taken
// mid-build stays subtracted instead of being healed back by the progress
// formula), does nothing else (no weapon, no economic output — those read
// `status === 'complete'` directly in game/economy.js and below) until
// buildProgress reaches 1, at which point it flips to 'complete' at full HP.
// THEN weaponed buildings (gun emplacement) acquire and fire on targets
// through the EXACT SAME pipeline a unit uses (pickTarget/nearestEnemy/
// fireProjectile/claim from game/units.js) — static disposition only, never
// chases (there is no movement code path for a building at all). Finally
// sweeps for destroyed buildings (hp<=0, set by resolveHit during this
// frame's updateProjectiles call, or during construction by the same path)
// and cleans them up — a building destroyed mid-construction reverts to
// nothing + a scorch mark exactly like a finished one (removeBuilding below
// doesn't distinguish). Mirrors game/units.js's updateUnits: fire/act this
// frame, filter deaths from LAST frame's hits — same one-frame lag as the
// unit roster already has.
export function updateBuildings(world, dt, map) {
  for (const b of world.buildings) {
    if (b.status === 'constructing') {
      if (b.hp <= 0) continue; // already dead this frame; the sweep below removes it
      const prevProgress = b.buildProgress;
      b.buildProgress = Math.min(1, b.buildProgress + dt / b.buildTime);
      const grown = (b.buildProgress - prevProgress) * b.maxHp * (1 - CONSTRUCTION_HP_FLOOR);
      b.hp = Math.min(b.maxHp, b.hp + grown);
      if (b.buildProgress >= 1) { b.status = 'complete'; b.hp = b.maxHp; }
      continue; // no combat / economic activity while still under construction
    }
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

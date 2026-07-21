// PRODUCTION CHAIN (P4 — CONCEPT.md's settled "Production & supply chain"
// ledger). This is the "directed-what, automatic-how" layer: the player
// picks a CATEGORY (tanks / aircraft / warships / artillery / …) and builds
// its FACILITY (game/buildings.js BUILDING_DEFS — tankFactory/aircraftPlant/
// shipyard/artilleryWorks); once that facility is complete it produces the
// category's unit(s) ON ITS OWN, no click-to-queue anywhere in this file.
// A facility only produces while its RECIPE is satisfied — required
// resources (game/resources.js stockpiles) + prerequisite buildings both
// present — and STALLS the instant either goes missing, resuming the moment
// it returns. This module owns: the recipe table, the per-frame production
// tick that reads/writes economy.resources + economy.ic/manpower and spawns
// finished units, and the IMPORT FALLBACK (spend influence + a bit of IC to
// top up a stockpile directly, bypassing the mine/deposit chain).
//
// Nothing here hardcodes a category or a unit name into the update loop —
// PRODUCTION_DEFS is the single table a new category needs; the tick below
// only reads its shape.

import { BUILDING_DEFS } from './buildings.js';
import { RESOURCE_DEFS } from './resources.js';
import { UNIT_DEFS, MOVE_CLASSES, terrainSample, spawnUnit } from './units.js';
// RESEARCH GATE (P4 follow-up — game/research.js, CONCEPT.md's settled "Tech
// / research model" paragraph): "production is gated on a unit's tech being
// unlocked." isCategoryUnlocked is the ONLY thing this file borrows from
// research.js — a single yes/no query per facility per tick, read generically
// off the category id exactly like every other gate below (prerequisite
// buildings, resources, IC, manpower). research.js does not import anything
// from this file, so there's no cycle: production.js depends on research.js,
// never the other way around.
import { isCategoryUnlocked } from './research.js';

// ---------------------------------------------------------------------------
// PRODUCTION_DEFS — DESIGNER'S TO TUNE. One entry per producible category.
// Every number here (resourceCostPerUnit amounts, icCostPerUnit,
// manpowerCostPerUnit, buildTimePerUnit) is a first-pass placeholder picked
// only to make the full chain (stalled -> mines built -> producing -> units
// accumulate) clearly observable within a short playtest — none of it is
// canon, and "tanks" is not privileged over the other three rows; it's one
// data row among several, per the designer's explicit meta-rule.
//
//   facility               — game/buildings.js BUILDING_DEFS key that must
//                             reach status:'complete' for this category.
//   outputs                — UNIT_DEFS key(s) this facility produces. More
//                             than one entry means the facility round-robins
//                             between them (aircraft: fighter, then
//                             strikejet, then fighter, ...).
//   recipe.resourceCostPerUnit — { resourceId: amount } consumed per unit,
//                             drawn from economy.resources (game/economy.js,
//                             fed by mines — game/buildings.js `mine` — or by
//                             the import fallback below).
//   recipe.prerequisiteBuildings — BUILDING_DEFS keys that must exist,
//                             complete, on the player's side ANYWHERE on the
//                             map (not necessarily adjacent to the facility)
//                             for production to run at all.
//   recipe.icCostPerUnit / manpowerCostPerUnit — drawn from economy.ic /
//                             economy.manpower alongside the resource cost.
//   recipe.buildTimePerUnit — seconds to produce one unit AT FULL SUPPLY
//                             (i.e. the facility's own production rate; it
//                             stalls rather than slowing down when supply is
//                             short — see updateProduction below).
export const PRODUCTION_DEFS = {
  tanks: {
    name: 'Tanks',
    facility: 'tankFactory',
    outputs: ['tank'],
    recipe: {
      // CONCEPT.md's illustrative (non-canon) tank-line example: chromium +
      // tungsten + steel + an ammunition plant.
      resourceCostPerUnit: { steel: 8, chromium: 3, tungsten: 2 },
      prerequisiteBuildings: ['ammunitionPlant'],
      icCostPerUnit: 12,
      manpowerCostPerUnit: 4,
      buildTimePerUnit: 6,
    },
  },
  aircraft: {
    name: 'Aircraft',
    facility: 'aircraftPlant',
    outputs: ['fighter', 'strikejet'],
    recipe: {
      resourceCostPerUnit: { steel: 5, tungsten: 4, oil: 6 },
      prerequisiteBuildings: ['ammunitionPlant'],
      icCostPerUnit: 15,
      manpowerCostPerUnit: 3,
      buildTimePerUnit: 7,
    },
  },
  warships: {
    name: 'Warships',
    facility: 'shipyard',
    outputs: ['gunboat', 'destroyer'],
    recipe: {
      resourceCostPerUnit: { steel: 12, chromium: 2, oil: 5 },
      prerequisiteBuildings: [],
      icCostPerUnit: 18,
      manpowerCostPerUnit: 6,
      buildTimePerUnit: 9,
    },
  },
  artillery: {
    name: 'Artillery',
    facility: 'artilleryWorks',
    outputs: [
      'artillery',
      // MLRS/rocket artillery slots into the SAME category as tube
      // artillery rather than getting its own facility — it's the same
      // real-world production niche (a battery that lobs ordnance from
      // range), just a different UNIT_DEFS entry, so it round-robins with
      // 'artillery' off the one Artillery Works exactly like fighter/
      // strikejet already round-robin off one Aircraft Plant.
      'mlrs',
    ],
    recipe: {
      resourceCostPerUnit: { steel: 6, tungsten: 3 },
      prerequisiteBuildings: [],
      icCostPerUnit: 8,
      manpowerCostPerUnit: 3,
      buildTimePerUnit: 5,
    },
  },

  // ---------------------------------------------------------------------
  // GROUNDED NEAR-FUTURE CATEGORIES (CONCEPT.md's settled "Arsenal scope &
  // the tech ceiling" paragraph). Same recipe shape as the four categories
  // above — resourceCostPerUnit only ever draws from the FOUR resources that
  // already exist (steel/chromium/tungsten/oil) plus the IC/manpower/
  // buildTime fields every recipe carries; no new resource types added in
  // this pass (see the per-category TODOs below for where the reference
  // doc's "really" wants titanium/electronics/rare-earths once that supply
  // chain exists). Costs are deliberately DIFFERENTIATED against each
  // other, per the task brief's explicit example: hypersonics (below) is
  // by far the most expensive row in the whole production table, drones is
  // by far the cheapest — the same "cheap swarm vs. priceless bleeding
  // edge" contrast CONCEPT.md's arsenal list draws between a loitering
  // munition and a hypersonic missile.
  drones: {
    name: 'Drones',
    facility: 'droneWorks',
    // recon UAV, armed UCAV, loitering munition, and the drone swarm
    // airframe all round-robin off one Drone Works — they're all light,
    // fast-to-build airframes sharing one light-industry-adjacent recipe,
    // same multi-output pattern aircraft/artillery already use above. The
    // DIFFERENTIATION CONCEPT.md's arsenal list wants between e.g. a cheap
    // swarm drone and a pricier system lives in which CATEGORY a unit sits
    // in (drones here is cheap; hypersonics below is not), not in a
    // per-unit-within-a-category cost split — matching the precedent
    // aircraft's category already set (fighter and strikejet share one
    // recipe despite being different planes).
    outputs: ['reconDrone', 'ucav', 'loiteringMunition', 'droneSwarm'],
    recipe: {
      // TODO: richer material (titanium/electronics) when the supply chain
      // expands — real drone airframes lean on lightweight composites and
      // avionics this resource roster doesn't have yet; approximated here
      // with a small steel+oil bill (airframe + engine/fuel) instead.
      resourceCostPerUnit: { steel: 2, oil: 1 },
      prerequisiteBuildings: [],
      icCostPerUnit: 5,
      manpowerCostPerUnit: 1,
      buildTimePerUnit: 3, // fast to build — the cheapest, quickest line in the table
    },
  },
  missiles: {
    name: 'Missiles',
    facility: 'missileWorks',
    // the SRBM and cruise-missile launchers — v1's "strike systems" lineage
    // per the task brief — round-robin off one Missile Works. Sits a clear
    // step above tanks/aircraft in cost (a standoff strike launcher is a
    // bigger-ticket system than a line vehicle) and a clear step BELOW
    // hypersonics (below), which is deliberately gated on this facility
    // existing first.
    outputs: ['srbmLauncher', 'cruiseLauncher'],
    recipe: {
      // TODO: richer material (titanium/electronics) when the supply chain
      // expands — real guidance/airframe packages want exactly those;
      // approximated with steel (launcher body) + tungsten (warhead/
      // penetrator) + oil (propellant) using only the existing four.
      resourceCostPerUnit: { steel: 6, tungsten: 4, oil: 3 },
      // missiles need warhead/propellant production, the same real-world
      // dependency CONCEPT.md's illustrative tank example already leans the
      // ammunition plant on.
      prerequisiteBuildings: ['ammunitionPlant'],
      icCostPerUnit: 20,
      manpowerCostPerUnit: 5,
      buildTimePerUnit: 10,
    },
  },
  hypersonics: {
    name: 'Hypersonics',
    facility: 'hypersonicWorks',
    // the single priciest row in the whole table, by design — CONCEPT.md's
    // "big bonuses behind big gates" tech philosophy applied even without
    // the actual research gate (that's explicitly the NEXT agent's job):
    // for now the "big gate" is just that hypersonicWorks itself requires
    // Missile Works to ALSO exist (prerequisiteBuildings below) — the
    // bleeding edge sits on top of the conventional missile industry, not
    // standing alone, mirroring ammunitionPlant gating tanks/aircraft.
    outputs: ['hypersonicLauncher'],
    recipe: {
      // TODO: richer material (titanium/electronics) when the supply chain
      // expands — a real hypersonic vehicle leans hard on thermal-
      // protection materials and advanced electronics this resource roster
      // doesn't model yet; approximated with a heavy multi-resource bill
      // across everything that DOES exist instead.
      resourceCostPerUnit: { steel: 10, chromium: 6, tungsten: 8, oil: 6 },
      prerequisiteBuildings: ['ammunitionPlant', 'missileWorks'],
      icCostPerUnit: 45,
      manpowerCostPerUnit: 10,
      buildTimePerUnit: 20,
    },
  },
  airDefense: {
    name: 'Air Defense',
    facility: 'airDefenseWorks',
    // MANPADS team, the upgraded layered SAM battery, and early directed-
    // energy point-defense round-robin off one Air Defense Works — three
    // different real-world answers to "something is in the air," bucketed
    // the same way tanks/artillery already bucket ground-combat variants.
    outputs: ['manpadsTeam', 'samUpgrade', 'pointDefenseLaser'],
    recipe: {
      resourceCostPerUnit: { steel: 5, chromium: 3, tungsten: 2 },
      // modern integrated air defense is cued off radar in reality; gating
      // production on the existing Radar Station building is a cheap,
      // grounded way to express that without inventing a new mechanic.
      prerequisiteBuildings: ['radar'],
      icCostPerUnit: 14,
      manpowerCostPerUnit: 4,
      buildTimePerUnit: 6,
    },
  },
  guidedWeapons: {
    name: 'Guided Weapons',
    facility: 'guidedWeaponsWorks',
    // ATGM team (anti-armor) and the anti-ship missile battery round-robin
    // off one Guided Weapons Works — both are ground-launched guided
    // munitions aimed at hard targets (tanks / warships) rather than air
    // threats, which is what separates this category from airDefense above.
    outputs: ['atgmTeam', 'antiShipBattery'],
    recipe: {
      resourceCostPerUnit: { steel: 4, chromium: 1, tungsten: 3 },
      // shaped-charge/anti-armor and anti-ship warheads are exactly the
      // kind of ordnance an ammunition plant produces, same dependency as
      // the missiles category above.
      prerequisiteBuildings: ['ammunitionPlant'],
      icCostPerUnit: 10,
      manpowerCostPerUnit: 3,
      buildTimePerUnit: 5,
    },
  },
  support: {
    name: 'Support',
    facility: 'signalsWorks',
    // the EW/jammer unit — a single-output category (same pattern tanks/
    // hypersonics/etc already use with one entry), cheap and fast since
    // it's a support vehicle carrying sensors/comms rather than a weapon
    // system.
    outputs: ['ewJammer'],
    recipe: {
      resourceCostPerUnit: { steel: 3, oil: 1 },
      // a signals/EW unit is built on the same sensor infrastructure a
      // radar station represents, same grounding as airDefense's prereq.
      prerequisiteBuildings: ['radar'],
      icCostPerUnit: 9,
      manpowerCostPerUnit: 2,
      buildTimePerUnit: 5,
    },
  },
};

// reverse lookup: facility building key -> category id. Built once at module
// load; PRODUCTION_DEFS is static data, not mutated at runtime.
const FACILITY_TO_CATEGORY = {};
for (const [cat, def] of Object.entries(PRODUCTION_DEFS)) FACILITY_TO_CATEGORY[def.facility] = cat;
export function categoryForFacility(key) { return FACILITY_TO_CATEGORY[key]; }

// ---------------------------------------------------------------------------
// IMPORT FALLBACK — DESIGNER'S TO TUNE. CONCEPT.md: "Missing a resource you
// can also import it in exchange for INFLUENCE + a bit of IC — a pricier
// stopgap, not a replacement for owning the deposit." Chose the simpler of
// the two options the brief offered: a per-resource button/action that adds
// one fixed BATCH_SIZE to that resource's stockpile, rather than an
// always-on "auto-import shortfall" toggle wired into every facility — a
// single explicit action is easier to verify (one spend, one stockpile
// bump) and easier for the player to reason about ("that cost me X
// influence") than a background auto-spend that could drain influence
// without the player noticing.
export const IMPORT_TUNABLES = {
  BATCH_SIZE: 25, // resource units added per import action
  INFLUENCE_PER_UNIT: 2.5, // influence cost per resource unit imported — pricier than mining, per CONCEPT.md
  IC_PER_UNIT: 1, // "a bit of IC" per resource unit imported, alongside influence
};

export function importCost(batch = IMPORT_TUNABLES.BATCH_SIZE) {
  return {
    influence: batch * IMPORT_TUNABLES.INFLUENCE_PER_UNIT,
    ic: batch * IMPORT_TUNABLES.IC_PER_UNIT,
    batch,
  };
}

export function canAffordImport(economy, resourceId, batch = IMPORT_TUNABLES.BATCH_SIZE) {
  if (!RESOURCE_DEFS[resourceId]) return false;
  const cost = importCost(batch);
  return economy.influence >= cost.influence && economy.ic >= cost.ic;
}

// Spends influence (+ a bit of IC) and adds `batch` units of `resourceId` to
// the stockpile immediately. Returns true/false so a caller (UI or a
// headless test) can tell whether the spend actually happened — no partial
// spends, no silent no-ops.
export function importResource(economy, resourceId, batch = IMPORT_TUNABLES.BATCH_SIZE) {
  if (!canAffordImport(economy, resourceId, batch)) return false;
  const cost = importCost(batch);
  economy.influence -= cost.influence;
  economy.ic -= cost.ic;
  economy.resources[resourceId] = (economy.resources[resourceId] || 0) + batch;
  return true;
}

// ---------------------------------------------------------------------------
// SPAWN-POINT SNAPPING — generic replacement for main.js's old domain-
// specific nearestWaterPoint/nearestLandPoint pair: reads the SAME
// terrainSample(map, moveClass, wx, wy) every unit's own movement already
// uses (game/units.js), so "a valid spot for this unit" never diverges from
// "a spot the unit could actually stand on/fly over" — a naval output spirals
// out to the nearest water tile, a ground output to the nearest passable
// land tile, an air output accepts the raw rally point outright (moveClass
// ignoresTerrain).
function findSpawnPoint(map, unitKey, wx, wy, maxTiles = 80) {
  const def = UNIT_DEFS[unitKey];
  const moveClass = MOVE_CLASSES[def.moveClass];
  if (moveClass.ignoresTerrain) return { x: wx, y: wy };
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
  return { x: wx, y: wy }; // nothing found within range: fall back to the raw rally point
}

// Rally point: just outside the facility's footprint, south edge — "near
// itself" per the brief, without spawning finished units stacked directly
// inside the (now-blocked) footprint tiles. findSpawnPoint then snaps this
// per-unit for whatever domain is actually being produced.
function facilityRallyPoint(map, b) {
  const ts = map.tileSize;
  const cx = (b.gx + b.def.footprint.w / 2) * ts;
  const cy = (b.gy + b.def.footprint.h) * ts + ts * 1.5;
  return { x: cx, y: cy };
}

function hasCompleteBuilding(world, key) {
  for (const b of world.buildings) {
    if (b.side === 'player' && b.key === key && b.status === 'complete') return true;
  }
  return false;
}

function spawnProducedUnit(world, map, b, prod) {
  const outputs = prod.outputs;
  const key = outputs[b.outputIdx % outputs.length];
  b.outputIdx = (b.outputIdx + 1) % outputs.length;
  const rally = facilityRallyPoint(map, b);
  // small jitter so a burst of same-tick spawns doesn't stack on one exact
  // pixel — units' own separation physics (game/units.js updateUnits) would
  // untangle it anyway, but this keeps the very first frame legible.
  const jx = rally.x + (Math.random() - 0.5) * 40, jy = rally.y + (Math.random() - 0.5) * 30;
  const pt = findSpawnPoint(map, key, jx, jy);
  spawnUnit(world, key, pt.x, pt.y, b.side);
}

// ---------------------------------------------------------------------------
// MAIN TICK — called once per frame from the game loop (main.js), same
// pattern as updateEconomy: zero player input required once a facility is
// complete and fed. For every complete player facility this frame:
//   0. RESEARCH GATE (P4 follow-up — game/research.js): if this category's
//      tech isn't unlocked yet, stall with 'tech not researched' before even
//      looking at buildings/resources — a locked category has nothing to
//      report about its supply chain until the tech exists at all. A
//      baseline category (no TECH_DEFS entry — tanks/aircraft/warships/
//      artillery) always reads unlocked, so this step is a no-op for them,
//      exactly matching CONCEPT.md's "baseline conventional gear is
//      pre-unlocked."
//   1. Check prerequisite buildings (recipe.prerequisiteBuildings) — first
//      missing one stalls production with a legible reason.
//   2. Compute what ONE TICK's worth of the recipe costs (dt / buildTimePerUnit
//      fraction of a unit) and check every resource/IC/manpower requirement
//      against the CURRENT stockpiles — first missing input stalls with a
//      legible reason ("needs Chromium").
//   3. If everything is available, consume the fractional cost right now
//      (so stockpiles visibly draw down continuously, not in one lump at
//      the end) and advance progress; a facility that stalls mid-unit keeps
//      its partial progress and resumes from there once supply returns —
//      it doesn't lose the partial unit.
//   4. When accumulated progress reaches a whole unit, spawn it (looped, so
//      a large dt that would complete more than one unit still spawns all
//      of them) and roll over the remainder.
//
// This is intentionally an instantaneous stall/resume check every frame
// rather than a smoothed rate: the brief calls for a facility that "stalls
// the instant an input runs out, resuming when it returns" — a smoothing
// window would blur exactly that observable behavior.
export function updateProduction(world, economy, map, dt, researchState) {
  for (const b of world.buildings) {
    if (b.side !== 'player' || b.status !== 'complete') continue;
    const category = categoryForFacility(b.key);
    if (!category) continue;
    const prod = PRODUCTION_DEFS[category];
    if (b.prodProgress === undefined) {
      b.prodProgress = 0;
      b.prodState = 'stalled';
      b.prodStallReason = 'starting up';
      b.producedCount = 0;
      b.outputIdx = 0;
    }
    b.prodCategory = category;

    // STEP 0 — research gate (see the MAIN TICK comment above). Checked
    // first, ahead of every other stall reason: a locked category's facility
    // has nothing more specific to report ("needs Chromium" would be
    // misleading when the real blocker is that the tech doesn't exist yet).
    if (!isCategoryUnlocked(researchState, category)) {
      b.prodState = 'stalled';
      b.prodStallReason = 'tech not researched';
      continue;
    }

    const missingPrereq = (prod.recipe.prerequisiteBuildings || [])
      .find(key => !hasCompleteBuilding(world, key));
    if (missingPrereq) {
      b.prodState = 'stalled';
      b.prodStallReason = `needs ${BUILDING_DEFS[missingPrereq].name}`;
      continue;
    }

    const frac = dt / prod.recipe.buildTimePerUnit;
    const need = prod.recipe.resourceCostPerUnit || {};
    let missingResource = null;
    for (const rid of Object.keys(need)) {
      if ((economy.resources[rid] || 0) < need[rid] * frac) { missingResource = rid; break; }
    }
    if (missingResource) {
      b.prodState = 'stalled';
      b.prodStallReason = `needs ${RESOURCE_DEFS[missingResource].name}`;
      continue;
    }
    const icNeed = (prod.recipe.icCostPerUnit || 0) * frac;
    const mpNeed = (prod.recipe.manpowerCostPerUnit || 0) * frac;
    if (economy.ic < icNeed) { b.prodState = 'stalled'; b.prodStallReason = 'needs IC'; continue; }
    if (economy.manpower < mpNeed) { b.prodState = 'stalled'; b.prodStallReason = 'needs manpower'; continue; }

    for (const rid of Object.keys(need)) economy.resources[rid] -= need[rid] * frac;
    economy.ic -= icNeed;
    economy.manpower -= mpNeed;
    b.prodProgress += frac;
    b.prodState = 'producing';
    b.prodStallReason = '';

    while (b.prodProgress >= 1) {
      b.prodProgress -= 1;
      spawnProducedUnit(world, map, b, prod);
      b.producedCount++;
    }
  }
}

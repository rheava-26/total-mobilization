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
      // DEEPENED CHAIN PART A (cashes in the old TODO below): a real
      // airframe leans on aluminum skin + titanium engine/high-heat
      // structure per reference-electronics-and-chains.md's aluminum-alloy
      // and titanium sections — steel/oil kept for the smaller share
      // (mounts/fuel/hydraulics) that's genuinely still steel-and-oil.
      // Deliberately kept off the PART B component chain (electronics) —
      // aircraft is a BASELINE category (no TECH_DEFS gate), so it stays
      // reachable off just the two new raw mines. DESIGNER'S TO TUNE.
      resourceCostPerUnit: { aluminum: 5, titanium: 2, steel: 3, oil: 4 },
      prerequisiteBuildings: ['ammunitionPlant'],
      icCostPerUnit: 15,
      manpowerCostPerUnit: 3,
      buildTimePerUnit: 7,
    },
  },
  warships: {
    name: 'Warships',
    facility: 'shipyard',
    // NAVAL REWORK (docs/reference-naval-warfare.md): frigate slots into the
    // round-robin between gunboat and destroyer — a real 3-class roster
    // (fast/cheap screen, balanced GP escort, slow/long-range capital)
    // instead of the old two-unit "small gun / big gun" pair.
    outputs: ['gunboat', 'frigate', 'destroyer'],
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
  // above, but — as of the DEEPENED SUPPLY CHAIN pass (game/resources.js's
  // aluminum/titanium/rareEarths PART A; a PART B follow-up layers an
  // electronics/advancedAlloy COMPONENT tier on top of that for the
  // guidance-heavy categories below) — each recipe now genuinely reaches
  // into the richer material tier the original pass's `// TODO: richer
  // material (titanium/electronics)...` comments called out; those TODOs
  // are gone, cashed in per-category below. Costs are deliberately
  // DIFFERENTIATED against each other, per the task brief's explicit
  // example: hypersonics (below) is by far the most expensive row in the
  // whole production table, drones is by far the cheapest — the same
  // "cheap swarm vs. priceless bleeding edge" contrast CONCEPT.md's
  // arsenal list draws between a loitering munition and a hypersonic
  // missile.
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
      // DEEPENED CHAIN PART A (cashes in the old TODO below, which read:
      // "richer material (titanium/electronics) when the supply chain
      // expands — real drone airframes lean on lightweight composites and
      // avionics this resource roster doesn't have yet; approximated with
      // a small steel+oil bill instead"). Now genuinely lightweight-
      // composite-coded: aluminum airframe + a little titanium
      // reinforcement + oil for the engine — no steel at all now (a drone
      // isn't built like a manned airframe). Deliberately NOT drawing on
      // the PART B electronics component (kept off droneWorks'
      // prerequisiteBuildings too) so drones stay the cheap, fast,
      // low-barrier entry point into the gated tier CONCEPT.md's "cheap
      // swarm vs. priceless bleeding edge" contrast wants. DESIGNER'S TO
      // TUNE.
      resourceCostPerUnit: { aluminum: 3, titanium: 1, oil: 1 },
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
      // DEEPENED CHAIN PART B (cashes in the old TODO below, which read:
      // "richer material (titanium/electronics) when the supply chain
      // expands — real guidance/airframe packages want exactly those").
      // Steel (launcher body) + tungsten (warhead penetrator) + oil
      // (propellant) kept for the airframe/warhead/propellant share that's
      // genuinely still those three; rareEarths + electronics newly added
      // for the GUIDANCE package (seeker/INS per
      // reference-electronics-and-chains.md section 2) — the first recipe
      // in this table to reach into the new component tier, not just the
      // new raw tier. DESIGNER'S TO TUNE.
      resourceCostPerUnit: { steel: 4, tungsten: 2, oil: 3, rareEarths: 2, electronics: 2 },
      // missiles need warhead/propellant production (ammunitionPlant, same
      // real-world dependency CONCEPT.md's illustrative tank example already
      // leans on) AND now the guidance-electronics chain (electronicsPlant)
      // — "buildings gate other buildings' output" extended one link deeper.
      prerequisiteBuildings: ['ammunitionPlant', 'electronicsPlant'],
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
      // DEEPENED CHAIN PART B (cashes in the old TODO below, which read:
      // "richer material (titanium/electronics) when the supply chain
      // expands — a real hypersonic vehicle leans hard on thermal-
      // protection materials and advanced electronics this resource roster
      // doesn't model yet"). The single deepest recipe in the table by
      // design, mirroring TECH_DEFS' own "hypersonics is the biggest gate"
      // framing: advanced ALLOY (thermal-protection/airframe stock —
      // reference section 3.1's single-crystal superalloy + Ti-6Al-4V
      // forging) is the priciest line item, backed by titanium (raw
      // structure) and rareEarths+electronics (seeker/guidance/avionics) —
      // every raw AND both component tiers this pass added, all in one
      // recipe. Oil kept for propellant. No steel/chromium/tungsten at all
      // anymore — a hypersonic vehicle isn't built like a WWII tank.
      // DESIGNER'S TO TUNE.
      resourceCostPerUnit: { advancedAlloy: 4, titanium: 3, electronics: 3, rareEarths: 3, oil: 4 },
      // now also gated on BOTH component facilities existing, on top of the
      // conventional missile/ammunition industry it already sat on — the
      // bleeding edge draws from every upstream link in the chain.
      prerequisiteBuildings: ['ammunitionPlant', 'missileWorks', 'electronicsPlant', 'alloyForge'],
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
      // DEEPENED CHAIN PART B: air defense is fundamentally a radar+
      // guidance problem (reference-electronics-and-chains.md section
      // 2.1's AESA T/R-module chain) — rareEarths + electronics now carry
      // that share, steel/chromium trimmed down (still there for the
      // launcher/housing) but no longer the whole bill. DESIGNER'S TO TUNE.
      resourceCostPerUnit: { steel: 3, chromium: 2, rareEarths: 2, electronics: 2 },
      // modern integrated air defense is cued off radar in reality; gating
      // production on the existing Radar Station building is a cheap,
      // grounded way to express that without inventing a new mechanic. Now
      // also needs the electronics chain that actually builds its guidance.
      prerequisiteBuildings: ['radar', 'electronicsPlant'],
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
      // DEEPENED CHAIN PART B: kept tungsten (shaped-charge/anti-armor
      // penetrator is genuinely a tungsten part) but chromium dropped in
      // favor of the rareEarths+electronics guidance package a real
      // ATGM/anti-ship missile needs (reference section 2.3/2.4's
      // IMU+seeker chain). DESIGNER'S TO TUNE.
      resourceCostPerUnit: { steel: 3, tungsten: 2, rareEarths: 1, electronics: 1 },
      // shaped-charge/anti-armor and anti-ship warheads are exactly the
      // kind of ordnance an ammunition plant produces, same dependency as
      // the missiles category above; now also needs the guidance chain.
      prerequisiteBuildings: ['ammunitionPlant', 'electronicsPlant'],
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
      // DEEPENED CHAIN PART B: an EW/jammer rig is, physically, mostly
      // electronics (signal-processing/RF hardware) — a small electronics
      // draw on top of the original steel+oil chassis bill. DESIGNER'S TO
      // TUNE.
      resourceCostPerUnit: { steel: 2, oil: 1, electronics: 1 },
      // a signals/EW unit is built on the same sensor infrastructure a
      // radar station represents, same grounding as airDefense's prereq;
      // now also needs the electronics chain that builds its own guts.
      prerequisiteBuildings: ['radar', 'electronicsPlant'],
      icCostPerUnit: 9,
      manpowerCostPerUnit: 2,
      buildTimePerUnit: 5,
    },
  },

  // ---------------------------------------------------------------------
  // PART B — INTERMEDIATE-COMPONENT CATEGORIES (the raw->component->system
  // chain the reference docs describe). These two categories don't produce
  // a UNIT_DEFS entry at all — `outputs` is empty — they instead produce a
  // RESOURCE via the new `producesResource: { resourceId, amountPerCycle }`
  // field, read generically by updateProduction below (see the "RESOURCE
  // OUTPUT MODE" comment there) and by game/techtree.js (a `producesResource`
  // edge from the category node to the resource node, alongside the
  // existing recipe/prereq/enables edges every category already gets) —
  // zero new mechanism beyond "a category can point its output at a
  // resource id instead of a unit list." Deliberately UNGATED (no TECH_DEFS
  // entry — see buildings.js's electronicsPlant/alloyForge comment for why)
  // so the electronics hub in particular stays reachable early: five other
  // categories (missiles/hypersonics/airDefense/guidedWeapons/support) all
  // draw on its output.
  electronics: {
    name: 'Electronics',
    facility: 'electronicsPlant',
    outputs: [], // produces a resource, not a unit — see producesResource below
    producesResource: { resourceId: 'electronics', amountPerCycle: 4 }, // DESIGNER'S TO TUNE
    recipe: {
      // rare earths (GaN/magnet feedstock) + steel (standing in for
      // copper wiring/PCB substrate per reference-electronics-and-chains.md
      // section 1/2's PCBA and T/R-module chains) — the proof-of-pattern
      // component recipe.
      resourceCostPerUnit: { rareEarths: 3, steel: 2 },
      prerequisiteBuildings: [],
      icCostPerUnit: 10,
      manpowerCostPerUnit: 2,
      buildTimePerUnit: 6, // seconds per BATCH (yields amountPerCycle electronics), not per single unit
    },
  },
  advancedAlloy: {
    name: 'Advanced Alloys',
    facility: 'alloyForge',
    outputs: [], // produces a resource, not a unit — see producesResource below
    producesResource: { resourceId: 'advancedAlloy', amountPerCycle: 4 }, // DESIGNER'S TO TUNE
    recipe: {
      // titanium (structure) + steel + chromium (hardening) — the Ti-6Al-4V-
      // forging / nickel-superalloy stand-in per reference section 3.1/4.1.
      // Currently only hypersonics draws on this — kept as its own category
      // rather than folded into electronics so the tech tree shows the two
      // component chains as genuinely separate branches, matching the
      // reference's own "Refined Materials" vs "Components" tiers.
      resourceCostPerUnit: { titanium: 2, steel: 3, chromium: 1 },
      prerequisiteBuildings: [],
      icCostPerUnit: 12,
      manpowerCostPerUnit: 2,
      buildTimePerUnit: 7,
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

    // RESOURCE OUTPUT MODE (PART B): a category whose PRODUCTION_DEFS entry
    // sets `producesResource` feeds a stockpile instead of spawning a unit —
    // this is what turns an "advanced unit" recipe reaching for e.g.
    // `electronics` into a REAL chain (mine rareEarths -> Electronics Plant
    // -> electronics stockpile -> missile/hypersonics/etc recipe) rather
    // than one more flat ore. Surfaced on economy.resourceRates the same way
    // icRate/manpowerRate report a live rate elsewhere in this codebase (ADD
    // rather than overwrite: game/economy.js's updateEconomy already reset
    // every resource's rate to 0 and summed mines' contributions THIS same
    // frame, before updateProduction runs — see main.js's call order — so a
    // resource fed by both a mine binding AND a component facility, if that
    // ever happens, reports their combined rate correctly instead of one
    // clobbering the other).
    if (prod.producesResource) {
      const { amountPerCycle } = prod.producesResource;
      economy.resourceRates[prod.producesResource.resourceId] =
        (economy.resourceRates[prod.producesResource.resourceId] || 0) + amountPerCycle / prod.recipe.buildTimePerUnit;
    }

    while (b.prodProgress >= 1) {
      b.prodProgress -= 1;
      if (prod.producesResource) {
        const { resourceId, amountPerCycle } = prod.producesResource;
        economy.resources[resourceId] = (economy.resources[resourceId] || 0) + amountPerCycle;
      } else {
        spawnProducedUnit(world, map, b, prod);
      }
      b.producedCount++;
    }
  }
}

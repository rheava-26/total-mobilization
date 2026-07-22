// RESEARCH / TECH-UNLOCK FRAMEWORK (P4 follow-up — CONCEPT.md's settled
// "Tech / research model: big bonuses behind big gates" paragraph). That
// paragraph IS this module's spec, verbatim: "Advanced capability isn't just
// 'spend some points.' A heavy tech unlocks only when you satisfy a
// COMBINATION of research effort (IC/effort over time) + prerequisite
// BUILDINGS + RESOURCES ... Buildings do double duty: the same advanced-
// industry building both gates/enables a line of research and later
// produces the unit. Baseline conventional gear is pre-unlocked; production
// is gated on a unit's tech being unlocked."
//
// This module owns the GATE, not the roster. It knows nothing about tanks,
// drones, or hypersonics as concepts — it only knows PRODUCTION CATEGORY ids
// (the same strings game/production.js's PRODUCTION_DEFS keys on) and
// whether each one is currently unlocked. Gating by category rather than by
// individual unit is the cleanest cut given how production.js is already
// shaped: one facility already produces a whole category's worth of units
// (aircraft's Aircraft Plant round-robins fighter/strikejet, drones' Drone
// Works round-robins four types), so "unlock a category" and "unlock a
// facility's whole output list" are the same event — no finer grain needed.
//
// BASELINE VS GATED (CONCEPT.md: "Baseline conventional gear is
// pre-unlocked"): a production category is pre-unlocked UNLESS some
// TECH_DEFS entry names it in `unlocksCategory`. That means the four
// original categories (tanks/aircraft/warships/artillery) need NO entry
// here at all — they're simply absent from TECH_DEFS, which
// isCategoryUnlocked() below reads as "no gate, always on." This file
// deliberately does NOT import game/production.js to enumerate "every
// category that isn't gated" — doing so would create an import cycle
// (production.js needs to ask THIS module "is my category unlocked?" on
// every tick). Baseline-by-omission sidesteps the cycle entirely: nothing
// here needs to know the full category roster, only the gated subset.
//
// ---------------------------------------------------------------------------
// TECH_DEFS — DESIGNER'S TO TUNE (data, not canon). One entry per gated
// production category, covering exactly the six GROUNDED NEAR-FUTURE
// categories the previous pass added to game/production.js (drones/
// missiles/hypersonics/airDefense/guidedWeapons/support). Every entry
// carries the full triad CONCEPT.md demands:
//   prerequisiteBuildings — BUILDING_DEFS keys (game/buildings.js) that must
//     ALL exist complete before this tech can even be STARTED. Every entry
//     includes 'researchBureau' (the universal R&D gate — see buildings.js)
//     PLUS that category's own production facility, mirroring the exact
//     `recipe.prerequisiteBuildings` production.js already declares for
//     that category (missiles needs an Ammunition Plant to PRODUCE; it also
//     needs one to RESEARCH, since you can't credibly develop a warhead
//     without the plant that will build it) — "buildings do double duty"
//     applies to the prerequisite chain too, not just the payoff facility.
//   prerequisiteTechs — TECH_DEFS keys that must be COMPLETED first.
//     Hypersonics is the one entry that uses this (requires missileTech
//     done), matching production.js's own comment that hypersonicWorks
//     "requires Missile Works to ALSO exist" — the bleeding edge sits ON TOP
//     of the conventional missile program, not beside it, in both the
//     factory-gate AND the research-gate.
//   icCost / resourceCost — a ONE-TIME lump sum (not per-unit, unlike
//     production.js's recipes) charged when research STARTS (see
//     startResearch below for why up-front is simpler than metered).
//   researchTime — seconds of progress at the Research Bureau's nominal
//     rate (no building bonuses to this rate yet — a clean follow-up seam,
//     not implemented here).
// Costs/times are graded cheapest-to-priciest exactly parallel to
// production.js's own recipe spread, with hypersonics DELIBERATELY the
// single most expensive entry in this table (biggest IC, richest multi-
// resource bill, longest time, only entry with a tech-prerequisite) — "the
// bigger the payoff, the more infrastructure and material it demands," per
// CONCEPT.md, applied to the research side exactly as production.js already
// applied it to the manufacturing side.
export const TECH_DEFS = {
  droneTech: {
    name: '[PLACEHOLDER] Drone Warfare Program',
    blurb: '[PLACEHOLDER] Unlocks light unmanned airframes — recon, strike, and swarm.',
    unlocksCategory: 'drones',
    prerequisiteBuildings: ['researchBureau', 'droneWorks'],
    prerequisiteTechs: [],
    icCost: 60,
    // DEEPENED CHAIN PART A: mirrors production.js's drones recipe now
    // leaning on aluminum/titanium airframes rather than steel — R&D
    // prototypes want the same lightweight material the production line
    // will actually use.
    resourceCost: { aluminum: 10, titanium: 4, oil: 8 },
    researchTime: 25, // cheapest/fastest — mirrors drones being the cheapest production row
  },
  missileTech: {
    name: '[PLACEHOLDER] Standoff Strike Program',
    blurb: '[PLACEHOLDER] Unlocks short-range ballistic and cruise missile launchers.',
    unlocksCategory: 'missiles',
    // mirrors production.js's missiles recipe.prerequisiteBuildings
    // (ammunitionPlant + electronicsPlant, now the guidance package lives
    // there too) plus the category's own facility + the universal gate.
    prerequisiteBuildings: ['researchBureau', 'missileWorks', 'ammunitionPlant', 'electronicsPlant'],
    prerequisiteTechs: [],
    icCost: 150,
    // DEEPENED CHAIN PART B: rareEarths added for the guidance-package R&D,
    // mirroring production.js's missiles recipe now drawing on it.
    resourceCost: { steel: 25, tungsten: 15, oil: 15, rareEarths: 10 },
    researchTime: 60,
  },
  hypersonicTech: {
    name: '[PLACEHOLDER] Hypersonic Vehicle Program',
    blurb: '[PLACEHOLDER] Unlocks the hypersonic strike launcher — the bleeding edge of the grounded roster.',
    unlocksCategory: 'hypersonics',
    // THE most gated entry in the table, by design (CONCEPT.md: "big
    // bonuses behind big gates" — hypersonics is the paragraph's own
    // canonical "biggest payoff" example): needs its own facility AND the
    // missile program's facility AND BOTH new component facilities
    // (mirroring production.js's hypersonicWorks recipe prereq, which now
    // draws on electronics AND advancedAlloy) AND the missile tech itself
    // DONE first, on top of the universal Research Bureau gate.
    prerequisiteBuildings: [
      'researchBureau', 'hypersonicWorks', 'missileWorks', 'ammunitionPlant',
      'electronicsPlant', 'alloyForge',
    ],
    prerequisiteTechs: ['missileTech'],
    icCost: 400,
    // DEEPENED CHAIN PART B: titanium/rareEarths/advancedAlloy replace
    // chromium/tungsten here, mirroring production.js's hypersonics recipe
    // reaching into both new component tiers for its thermal-protection/
    // guidance R&D.
    resourceCost: { steel: 20, titanium: 25, rareEarths: 20, advancedAlloy: 10, oil: 15 },
    researchTime: 150, // by far the longest — the "big gate" the CONCEPT.md paragraph names explicitly
  },
  airDefenseTech: {
    name: '[PLACEHOLDER] Integrated Air Defense Program',
    blurb: '[PLACEHOLDER] Unlocks MANPADS, the upgraded SAM battery, and point-defense lasers.',
    unlocksCategory: 'airDefense',
    // mirrors production.js's airDefense recipe.prerequisiteBuildings
    // (radar + electronicsPlant, now the radar-guidance package lives there)
    prerequisiteBuildings: ['researchBureau', 'airDefenseWorks', 'radar', 'electronicsPlant'],
    prerequisiteTechs: [],
    icCost: 110,
    // DEEPENED CHAIN PART B: rareEarths added for the AESA/T-R-module R&D,
    // mirroring production.js's airDefense recipe.
    resourceCost: { steel: 20, chromium: 10, rareEarths: 15 },
    researchTime: 45,
  },
  guidedWeaponsTech: {
    name: '[PLACEHOLDER] Guided Munitions Program',
    blurb: '[PLACEHOLDER] Unlocks ATGM teams and anti-ship missile batteries.',
    unlocksCategory: 'guidedWeapons',
    // mirrors production.js's guidedWeapons recipe.prerequisiteBuildings
    // (ammunitionPlant + electronicsPlant, now the guidance package lives
    // there too)
    prerequisiteBuildings: ['researchBureau', 'guidedWeaponsWorks', 'ammunitionPlant', 'electronicsPlant'],
    prerequisiteTechs: [],
    icCost: 90,
    // DEEPENED CHAIN PART B: rareEarths added, mirroring production.js's
    // guidedWeapons recipe reaching into the guidance-electronics chain.
    resourceCost: { steel: 20, tungsten: 10, rareEarths: 8 },
    researchTime: 40,
  },
  supportTech: {
    name: '[PLACEHOLDER] Electronic Warfare Program',
    blurb: '[PLACEHOLDER] Unlocks the EW/jammer support vehicle.',
    unlocksCategory: 'support',
    // mirrors production.js's support recipe.prerequisiteBuildings (radar +
    // electronicsPlant — an EW/jammer rig is, physically, mostly electronics)
    prerequisiteBuildings: ['researchBureau', 'signalsWorks', 'radar', 'electronicsPlant'],
    prerequisiteTechs: [],
    icCost: 70,
    resourceCost: { steel: 10, oil: 6, electronics: 5 },
    researchTime: 30,
  },

  // -------------------------------------------------------------------
  // FUTURE SCI-FI EXAMPLE — ILLUSTRATIVE ONLY, NOT BUILT. CONCEPT.md's own
  // "usable human cloning armies" illustration, shown here to prove the
  // FRAMEWORK (not the roster) already supports it: a future tech this
  // heavy slots in as ONE MORE DATA ROW, zero code changes anywhere in this
  // file, buildings.js, or production.js beyond adding the three new
  // buildings + one new production category those files already know how
  // to hold. Left commented out — do not uncomment as part of this pass,
  // 'clones'/'geneticsTech'/the three buildings below do not exist yet.
  //
  // cloningTech: {
  //   name: '[PLACEHOLDER] Cloning Program',
  //   blurb: '[PLACEHOLDER] Mass-produced, combat-ready human clones.',
  //   unlocksCategory: 'clones',                 // a FUTURE production category
  //   prerequisiteBuildings: [
  //     'researchBureau',                         // still the universal gate
  //     'dataCenter',                              // CONCEPT.md: "big data
  //     'cloningFacility',                         // centers (real intelligence)
  //     'massFarm',                                // + cloning facilities +
  //   ],                                           // massive farms (to feed them)"
  //   prerequisiteTechs: ['geneticsTech'],
  //   icCost: 2000,
  //   resourceCost: { steel: 300, chromium: 200, tungsten: 150, oil: 150 },
  //   researchTime: 600,
  // },
};

// Reverse lookup, built once at module load (TECH_DEFS is static data, same
// pattern as production.js's FACILITY_TO_CATEGORY): production category id
// -> the tech that gates it. A category with NO entry here is baseline/
// pre-unlocked, per the header comment above.
const CATEGORY_TO_TECH = {};
for (const [techId, def] of Object.entries(TECH_DEFS)) CATEGORY_TO_TECH[def.unlocksCategory] = techId;

// The single query game/production.js's updateProduction needs: is this
// category currently producible? A baseline category (no TECH_DEFS entry)
// is always true, unconditionally — this is what makes "conventional gear
// is pre-unlocked" fall out of DATA rather than a hardcoded exemption list.
export function isCategoryUnlocked(researchState, category) {
  const techId = CATEGORY_TO_TECH[category];
  if (!techId) return true;
  return !!researchState.completed[techId];
}

// ---------------------------------------------------------------------------
// RESEARCH STATE + TICK. Deliberately the simplest correct shape per the
// task brief ("at most ONE active research project at a time — keep it
// simple"): `completed` is a set of finished tech ids (an unlock, once
// granted, is permanent — nothing in this framework ever re-locks a tech);
// `active` is either null or { techId, progress } where progress runs
// 0..1 over `researchTime` seconds.
export function createResearchState() {
  return { completed: {}, active: null };
}

// Local duplicate of production.js's private hasCompleteBuilding — same one-
// line "is there a complete player building with this key" check. Kept
// local rather than imported/exported to avoid wiring an import between
// this file and production.js in either direction (production.js already
// imports FROM here for isCategoryUnlocked; a two-way dependency for one
// three-line helper isn't worth the cycle risk).
function hasCompleteBuilding(world, key) {
  for (const b of world.buildings) {
    if (b.side === 'player' && b.key === key && b.status === 'complete') return true;
  }
  return false;
}

// techStatus() is the single source of truth the UI (and startResearch
// below) both read to answer "what state is this tech in right now":
//   'unlocked'      — completed.
//   'active'        — currently the one running project; carries `progress`
//                      (0..1) for a live readout.
//   'locked'        — prerequisite buildings and/or techs are not yet all
//                      satisfied; carries `missingBuildings`/`missingTechs`
//                      (BUILDING_DEFS/TECH_DEFS keys) so a caller can render
//                      exactly what's still needed, not just "locked."
//   'busy'          — every prerequisite IS satisfied, but a DIFFERENT
//                      project is currently occupying the one research slot
//                      (the brief's "at most ONE active project" rule) —
//                      distinct from 'locked' so the UI can say "wait your
//                      turn" instead of "you're missing something."
//   'researchable'  — gate satisfied, slot free: this is what canStart
//                      checks for. Affordability (IC/resources on hand
//                      right now) is intentionally NOT part of this status
//                      — that's a separate, cheaper check (canAffordResearch
//                      below) so a UI can show "researchable but can't
//                      afford it yet" as a distinct, more informative state
//                      than folding it into 'locked'.
export function techStatus(world, researchState, techId) {
  const def = TECH_DEFS[techId];
  if (!def) return { state: 'locked', missingBuildings: [], missingTechs: [] };
  if (researchState.completed[techId]) return { state: 'unlocked' };
  if (researchState.active && researchState.active.techId === techId) {
    return { state: 'active', progress: researchState.active.progress };
  }
  const missingBuildings = (def.prerequisiteBuildings || []).filter(k => !hasCompleteBuilding(world, k));
  const missingTechs = (def.prerequisiteTechs || []).filter(t => !researchState.completed[t]);
  if (missingBuildings.length || missingTechs.length) {
    return { state: 'locked', missingBuildings, missingTechs };
  }
  if (researchState.active) return { state: 'busy' };
  return { state: 'researchable' };
}

export function canStartResearch(world, researchState, techId) {
  return techStatus(world, researchState, techId).state === 'researchable';
}

export function canAffordResearch(economy, techId) {
  const def = TECH_DEFS[techId];
  if (!def) return false;
  if (economy.ic < (def.icCost || 0)) return false;
  for (const [rid, amt] of Object.entries(def.resourceCost || {})) {
    if ((economy.resources[rid] || 0) < amt) return false;
  }
  return true;
}

// Starts a project: charges the FULL lump-sum cost immediately, up front,
// rather than metering it out over researchTime the way production.js
// meters resource draw per-tick. Chose up-front-on-start as "the simpler
// correct option" the task brief explicitly allows for: a metered charge
// would need its own stall/resume state machine (what happens if resources
// run dry mid-research — pause research too?) that duplicates
// updateProduction's stall logic for very little added realism, since a
// research PROJECT (unlike a production LINE) is a one-shot event, not an
// ongoing draw. Returns false (and spends nothing) if the tech isn't
// currently 'researchable' OR isn't currently affordable — no partial
// spends, same contract as production.js's importResource.
export function startResearch(world, economy, researchState, techId) {
  if (techStatus(world, researchState, techId).state !== 'researchable') return false;
  if (!canAffordResearch(economy, techId)) return false;
  const def = TECH_DEFS[techId];
  economy.ic -= def.icCost || 0;
  for (const [rid, amt] of Object.entries(def.resourceCost || {})) {
    economy.resources[rid] -= amt;
  }
  researchState.active = { techId, progress: 0 };
  return true;
}

// MAIN TICK — called once per frame from the game loop (main.js), same
// "zero player input required once started" shape as updateEconomy/
// updateProduction: a project that's already running needs nothing further
// from the player, it just accrues time. Only ever advances the ONE active
// project (if any); completion flips the category unlocked (via `completed`,
// read by isCategoryUnlocked above) and frees the slot for the next pick.
export function updateResearch(researchState, dt) {
  if (!researchState.active) return;
  const def = TECH_DEFS[researchState.active.techId];
  if (!def) { researchState.active = null; return; } // defensive: unknown techId, never happens in normal play
  researchState.active.progress += dt / def.researchTime;
  if (researchState.active.progress >= 1) {
    researchState.completed[researchState.active.techId] = true;
    researchState.active = null;
  }
}

// ---------------------------------------------------------------------------
// PLACEHOLDER COPY (designer to rewrite) — same convention as main.js's own
// COPY block and game/menu.js's MENU_COPY: every player-facing string below
// is a plain, functional stand-in marked [PLACEHOLDER] (as are every tech's
// `name`/`blurb` above — none of it is canon, "don't hardcode any canon
// names" per the task brief). Nothing here is read for gameplay logic, only
// for the research panel's display text (see main.js's RESEARCH PANEL
// section). Find-and-replace this block (and the name/blurb fields on
// TECH_DEFS above) when writing real copy.
export const RESEARCH_COPY = {
  PANEL_TITLE: '[PLACEHOLDER] Research & Development (T)',
  UNLOCKED_LABEL: '[PLACEHOLDER] UNLOCKED',
  ACTIVE_PREFIX: '[PLACEHOLDER] In progress — ',
  LOCKED_PREFIX: '[PLACEHOLDER] Locked — needs: ',
  BUSY_HINT: '[PLACEHOLDER] Another project is already underway — only one at a time.',
  CANT_AFFORD_HINT: "[PLACEHOLDER] Can't afford this yet.",
  START_BUTTON: '[PLACEHOLDER] Start research',
};

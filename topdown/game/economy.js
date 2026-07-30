// MOBILIZATION ECONOMY CORE (P3 — CONCEPT.md Pillar 2: "Mobilization IS the
// Economy"). This is the substrate only: IC (industrial capacity), manpower,
// and a mobilization level that RISES ON ITS OWN over time. There is no
// player-facing "convert economy" button anywhere in this file — the
// designer directive is explicit: "you don't hand-crank the conversion."
// The player's only economic verbs are (a) what/where to build (main.js
// B-mode, now costed+timed) and (b) which buildings/programs to build, which
// changes the CURVE this module runs on (see the two upgrade levers below).
// Unit production queues are P4 (not here). Named resource deposits /
// extractors (mines) ARE wired in here now — see the RESOURCE STOCKPILES
// block in createEconomy and the extraction loop at the end of
// updateEconomy — but production RECIPES that consume those stockpiles
// (factories gated on resources + prerequisite buildings, per CONCEPT.md's
// settled "Production & supply chain" ledger) are still P4, not this file.
//
// ---------------------------------------------------------------------------
// TUNABLE CONSTANTS — DESIGNER'S TO TUNE. Every number below is a first-pass
// placeholder picked to demonstrate the PATTERN (auto ramp + connectivity-fed
// IC + upgrade levers that bend the curve), not canon. Nothing here should be
// read as "the" mobilization economy — it's "a" mobilization economy shaped
// like the one CONCEPT.md describes, sized so a fresh game is observable
// within a couple of minutes rather than the (much longer) real campaign
// pacing an actual playtest would want.
export const ECONOMY_TUNABLES = {
  // ---- starting pools (small civilian baseline, CONCEPT.md: "no huge IC
  // faucet at minute one") ----
  STARTING_IC: 200,
  STARTING_MANPOWER: 50,

  // ---- stockpile caps: pools can't exceed a low base without a Supply
  // Depot (buildings' econ.icCapBonus / manpowerCapBonus raise these) ----
  IC_CAP_BASE: 250,
  MANPOWER_CAP_BASE: 120,

  // ---- IC from cities + road connectivity. A city plugged into the road
  // network (reachable from the capital by road) produces at full rate; a
  // cut-off city still produces something (it isn't a black box), just much
  // less — CONCEPT.md: "a city cut off from the network produces less." ----
  IC_BASE_PER_CITY_PER_SIZE: 0.08, // IC/sec per unit of city.r (city "size")
  IC_CONNECTED_MULT: 1.0,
  IC_ISOLATED_MULT: 0.35,

  // ---- manpower from population (city size is the population proxy this
  // slice uses — there's no separate population model yet) ----
  MANPOWER_BASE_PER_SIZE: 0.05, // manpower/sec per unit of city.r, band mult applied on top

  // ---- automatic mobilization ramp (the designer directive: "mobilization
  // happens automatically, over time — you don't micromanage it"). Baseline
  // is time-only; THREAT_RAMP_COEFF is a clearly-marked SEAM for a future
  // threat system (see threatLevel below) — not implemented here. ----
  MOBILIZATION_BASE_RATE: 100 / 900, // %/sec: reaches 100 in 15min of pure idle time at baseline
  // THREAT-DRIVEN MOBILIZATION (the "nation wakes up when attacked" fantasy —
  // wired now; was a dead seam). threatLevel eases toward economy.threatTarget
  // (main.js's loop sets it: 1 while combat is active, 0 in peacetime) at
  // THREAT_EASE_RATE, so mobilization visibly ACCELERATES once the invasion
  // lands instead of climbing on a flat idle clock. At threatLevel 1 the ramp
  // gains THREAT_RAMP_COEFF on top of the base — sized to a meaningful boost
  // (~+55% ramp speed) without doubling it, so the invasion feels like a
  // mobilization moment without trivializing the difficulty gradient (balance-
  // harness-checked). DESIGNER'S TO TUNE.
  THREAT_RAMP_COEFF: 0.06, // %/sec added per unit of threatLevel (0..1) while threatened
  THREAT_EASE_RATE: 0.14,  // per-sec rate threatLevel eases toward threatTarget (~7s to fully "wake up")

  // ---- military output: capacity that accrues for P4 (unit production) to
  // later consume. Zero at mobilizationLevel 0 by construction (no faucet at
  // minute one), grows as the ramp climbs. ----
  MIL_OUTPUT_BASE_COEFF: 0.4, // fraction of current IC rate that "counts" toward military output at full mobilization

  // ---- INFLUENCE (P4 follow-up — CONCEPT.md's settled "Production & supply
  // chain" ledger: missing resources are importable "in exchange for
  // INFLUENCE + a bit of IC"). A slow-accruing, stockpiled pool, same shape
  // as IC/manpower but with no per-city source — it's the "diplomatic/
  // political capital" pool the import fallback spends, not a production
  // input in its own right. DESIGNER'S TO TUNE — placeholder sized only to
  // make an import affordable within a short playtest without making it
  // free.
  STARTING_INFLUENCE: 30,
  INFLUENCE_CAP_BASE: 150,
  INFLUENCE_BASE_RATE: 0.15, // influence/sec, flat trickle (no connectivity/city dependency yet)
};

// Mobilization bands (0..100 scale) — CONCEPT.md: "volunteers → conscription
// → total mobilization", represented as rate multiplier bands rather than a
// discrete state machine. manpowerMult scales manpower income; outputMult
// scales the "quantity" side of military output (armingQuality, driven by
// the arming upgrade lever below, scales the "quality" side separately).
// DESIGNER'S TO TUNE — band count, thresholds, and multipliers are all
// placeholders.
export const MOBILIZATION_BANDS = [
  { min: 0, label: 'Peacetime (Volunteers)', manpowerMult: 1.0, outputMult: 1.0 },
  { min: 20, label: 'Limited Mobilization', manpowerMult: 1.5, outputMult: 1.2 },
  { min: 45, label: 'Conscription', manpowerMult: 2.2, outputMult: 1.5 },
  { min: 75, label: 'Total Mobilization', manpowerMult: 3.2, outputMult: 2.0 },
];

function bandFor(level) {
  let band = MOBILIZATION_BANDS[0];
  for (const b of MOBILIZATION_BANDS) if (level >= b.min) band = b;
  return band;
}

export function createEconomy() {
  const T = ECONOMY_TUNABLES;
  const resources = {}, resourceRates = {};
  for (const r of RESOURCE_LIST) { resources[r.id] = 0; resourceRates[r.id] = 0; }
  return {
    ic: T.STARTING_IC, icRate: 0, icCap: T.IC_CAP_BASE,
    manpower: T.STARTING_MANPOWER, manpowerRate: 0, manpowerCap: T.MANPOWER_CAP_BASE,
    // INFLUENCE (P4 follow-up, see ECONOMY_TUNABLES above): what the import
    // fallback (game/production.js importResource) spends.
    influence: T.STARTING_INFLUENCE, influenceRate: 0, influenceCap: T.INFLUENCE_CAP_BASE,
    mobilizationLevel: 0, mobilizationRate: 0, band: bandFor(0),
    armingQuality: 1, // multiplier from the "arm better" upgrade lever (factory econ.armingBonus)
    militaryOutput: 0, militaryOutputRate: 0,
    // THREAT HOOK (seam, not a system): a future threat mechanic sets this
    // 0..1-ish and it feeds the mobilization ramp via THREAT_RAMP_COEFF
    // above. Nothing in P3 writes to this — it stays 0.
    threatLevel: 0,
    // RESOURCE STOCKPILES (P3 follow-up — game/resources.js RESOURCE_LIST,
    // fed by mines' map.depositAt binding; see the extraction loop in
    // updateEconomy below). One pool + one current rate per resource id,
    // keyed generically off RESOURCE_LIST so a new resource entry there
    // needs no change here. UNCAPPED for now — unlike ic/manpower there is
    // no depot-style cap wired up yet; noted as a follow-up, not an
    // oversight (a future supply-depot-style cap would slot in exactly like
    // icCap/manpowerCap above).
    resources, resourceRates,
    perCity: [], // debug/HUD breakdown: [{name, connected, icPerSec}, ...]
    _connCacheVersion: -1,
    _connSet: null,
  };
}

// ---------------------------------------------------------------------------
// ROAD CONNECTIVITY: flood-fill over the road overlay (map.roadAt) starting
// at the capital city (map.cities[0] — mapgen.js always roots its MST there
// and the acceptance gate guarantees every generated map's cities sit in one
// connected road component at spawn). A later road cut (or, in a generated
// map that somehow fails to route one, an always-isolated city) falls out of
// this component and its IC contribution drops to the isolated rate. Cached
// on map.version so this only recomputes when the road/obstacle layout
// actually changes, not every tick.
function computeRoadComponent(map) {
  const seen = new Set();
  const capital = map.cities && map.cities[0];
  if (!capital) return seen;
  const w = map.width, h = map.height;
  let sx = Math.round(capital.x), sy = Math.round(capital.y);
  if (map.roadAt(sx, sy) <= 0) {
    // Defensive fallback only — mapgen's acceptance gate means this normally
    // never triggers on a generated map; kept for hand-authored maps that
    // might place a city center a tile off its own road stub.
    let found = false;
    for (let r = 1; r <= 6 && !found; r++) {
      for (let dy = -r; dy <= r && !found; dy++) {
        for (let dx = -r; dx <= r && !found; dx++) {
          if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
          if (map.roadAt(sx + dx, sy + dy) > 0) { sx += dx; sy += dy; found = true; }
        }
      }
    }
    if (!found) return seen; // no road network reachable from the capital at all
  }
  const visited = new Uint8Array(w * h);
  const stack = [sy * w + sx];
  visited[sy * w + sx] = 1;
  while (stack.length) {
    const i = stack.pop();
    seen.add(i);
    const x = i % w, y = (i / w) | 0;
    for (let dy = -1; dy <= 1; dy++) {
      for (let dx = -1; dx <= 1; dx++) {
        if (!dx && !dy) continue;
        const nx = x + dx, ny = y + dy;
        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
        const ni = ny * w + nx;
        if (visited[ni] || map.roadAt(nx, ny) <= 0) continue;
        visited[ni] = 1;
        stack.push(ni);
      }
    }
  }
  return seen;
}

// Is `city` reachable from the capital's road component? Checks a ring
// around the city center out to its footprint radius (+2 tiles) rather than
// just the exact center pixel, so a city whose center tile isn't itself
// paved (only its access road is) still reads as connected.
function cityIsConnected(map, connSet, city) {
  if (!connSet.size) return false;
  const w = map.width;
  const r = Math.ceil(city.r || 0) + 2;
  const cx = Math.round(city.x), cy = Math.round(city.y);
  for (let dy = -r; dy <= r; dy++) {
    for (let dx = -r; dx <= r; dx++) {
      const x = cx + dx, y = cy + dy;
      if (x < 0 || y < 0) continue;
      if (connSet.has(y * w + x)) return true;
    }
  }
  return false;
}

// ---------------------------------------------------------------------------
// MAIN TICK — called once per frame from the game loop, zero player input
// required. Reads map.cities + road connectivity for IC, sums completed
// player buildings' `econ` blocks (game/buildings.js BUILDING_DEFS) for
// building bonuses, advances the automatic mobilization ramp, and accrues
// the IC/manpower/militaryOutput pools.
export function updateEconomy(world, economy, map, dt) {
  const T = ECONOMY_TUNABLES;

  if (economy._connCacheVersion !== map.version) {
    economy._connSet = computeRoadComponent(map);
    economy._connCacheVersion = map.version;
  }
  const connSet = economy._connSet;

  // ---- IC from cities + connectivity ----
  let icRate = 0;
  const perCity = [];
  for (const city of (map.cities || [])) {
    const connected = cityIsConnected(map, connSet, city);
    const mult = connected ? T.IC_CONNECTED_MULT : T.IC_ISOLATED_MULT;
    const icPerSec = T.IC_BASE_PER_CITY_PER_SIZE * (city.r || 1) * mult;
    icRate += icPerSec;
    perCity.push({ name: city.name, connected, icPerSec });
  }
  economy.perCity = perCity;

  // ---- manpower from population (city-size proxy), band multiplier applied
  // after buildings/base sum below ----
  let manpowerRate = 0;
  for (const city of (map.cities || [])) {
    manpowerRate += T.MANPOWER_BASE_PER_SIZE * (city.r || 1);
  }

  // ---- buildings' econ effects — ONLY completed player buildings apply
  // (game/buildings.js BUILDING_DEFS[key].econ; under-construction buildings
  // contribute nothing yet, same as their weapon/vision being gated) ----
  let armingBonus = 0, mobilizationRampBonus = 0, icCapBonus = 0, manpowerCapBonus = 0;
  let influenceRateBonus = 0, influenceCapBonus = 0;
  for (const b of (world.buildings || [])) {
    if (b.side !== 'player' || b.status !== 'complete') continue;
    const econ = b.def.econ;
    if (!econ) continue;
    icRate += econ.icRate || 0;
    manpowerRate += econ.manpowerRate || 0;
    armingBonus += econ.armingBonus || 0;
    mobilizationRampBonus += econ.mobilizationRateBonus || 0;
    icCapBonus += econ.icCapBonus || 0;
    manpowerCapBonus += econ.manpowerCapBonus || 0;
    // no building grants these yet (P4 first pass) — read generically anyway
    // so a future "embassy"-style building can add the lever with zero
    // engine changes, same pattern as every other econ field here.
    influenceRateBonus += econ.influenceRateBonus || 0;
    influenceCapBonus += econ.influenceCapBonus || 0;
  }
  economy.icCap = T.IC_CAP_BASE + icCapBonus;
  economy.manpowerCap = T.MANPOWER_CAP_BASE + manpowerCapBonus;
  economy.influenceCap = T.INFLUENCE_CAP_BASE + influenceCapBonus;
  economy.influenceRate = T.INFLUENCE_BASE_RATE + influenceRateBonus;
  economy.influence = Math.min(economy.influenceCap, economy.influence + economy.influenceRate * dt);

  // ---- automatic mobilization ramp — THE load-bearing rule: this advances
  // with zero player input. mobilizationRampBonus is the ACCELERATOR upgrade
  // lever (barracks econ.mobilizationRateBonus); economy.threatLevel is the
  // THREAT-DRIVEN term (now live): it eases toward economy.threatTarget (set
  // by main.js's loop — 1 while combat is active, 0 in peacetime; undefined in
  // the prep/fastForward sim paths, which read as 0 = peacetime) so the ramp
  // accelerates once the invasion actually lands. ----
  const threatTarget = economy.threatTarget || 0;
  economy.threatLevel += (threatTarget - economy.threatLevel) * Math.min(1, T.THREAT_EASE_RATE * dt);
  const rampRate = T.MOBILIZATION_BASE_RATE + economy.threatLevel * T.THREAT_RAMP_COEFF + mobilizationRampBonus;
  economy.mobilizationLevel = Math.min(100, economy.mobilizationLevel + rampRate * dt);
  economy.mobilizationRate = rampRate;
  economy.band = bandFor(economy.mobilizationLevel);

  manpowerRate *= economy.band.manpowerMult;

  // armingQuality is the ARM-BETTER upgrade lever (factory econ.armingBonus):
  // raises the quality multiplier applied to military output below.
  economy.armingQuality = 1 + armingBonus;

  economy.icRate = icRate;
  economy.manpowerRate = manpowerRate;
  economy.ic = Math.min(economy.icCap, economy.ic + icRate * dt);
  economy.manpower = Math.min(economy.manpowerCap, economy.manpower + manpowerRate * dt);

  // ---- military output: capacity P4 will consume. Zero at
  // mobilizationLevel 0 (no faucet at minute one); grows with the ramp,
  // scaled by both the band's quantity multiplier and the arming lever's
  // quality multiplier. Uncapped — it's a growing bank, not a stockpile. ----
  const outputRate = icRate * (economy.mobilizationLevel / 100) * T.MIL_OUTPUT_BASE_COEFF
    * economy.band.outputMult * economy.armingQuality;
  economy.militaryOutputRate = outputRate;
  economy.militaryOutput += outputRate * dt;

  // ---- resource stockpiles: every COMPLETE player mine (game/buildings.js
  // BUILDING_DEFS.mine, requiresDeposit: true) was bound to a deposit at
  // siting time (b.resourceType/b.richness — see spawnBuilding). Extraction
  // rate scales linearly with that deposit's richness; a mine that somehow
  // ended up with no binding (see spawnBuilding's defensive fallback)
  // contributes nothing, same as "produces nothing" for an unsited mine.
  // Rates are recomputed from scratch each tick (not accumulated) so a mine
  // destroyed mid-game drops out of the rate immediately, same pattern as
  // icRate/manpowerRate above.
  for (const id of Object.keys(economy.resources)) economy.resourceRates[id] = 0;
  for (const b of (world.buildings || [])) {
    if (b.side !== 'player' || b.status !== 'complete' || !b.resourceType) continue;
    const rate = MINE_TUNABLES.BASE_RATE_PER_RICHNESS * (b.richness || 0);
    economy.resourceRates[b.resourceType] = (economy.resourceRates[b.resourceType] || 0) + rate;
  }
  for (const id of Object.keys(economy.resources)) {
    economy.resources[id] += economy.resourceRates[id] * dt;
  }
}

// ---------------------------------------------------------------------------
// CONSTRUCTION COST HELPERS — used by main.js's B-mode click handler so
// placing a building actually spends the economy instead of being free.
// BUILDING_DEFS' `cost`/`buildTime` are defined in game/buildings.js (kept
// next to the rest of each building's data); these just read them.
import { BUILDING_DEFS } from './buildings.js';
import { RESOURCE_LIST, MINE_TUNABLES } from './resources.js';

export function buildingCost(key) {
  const def = BUILDING_DEFS[key];
  return (def && def.cost) || { ic: 0, manpower: 0 };
}

export function canAffordBuilding(economy, key) {
  const cost = buildingCost(key);
  return economy.ic >= (cost.ic || 0) && economy.manpower >= (cost.manpower || 0);
}

export function spendForBuilding(economy, key) {
  const cost = buildingCost(key);
  economy.ic -= (cost.ic || 0);
  economy.manpower -= (cost.manpower || 0);
}

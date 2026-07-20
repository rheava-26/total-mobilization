import { loadMap, loadGeneratedMap } from './engine/tilemap.js';
import { createRenderer, attachCameraControls } from './engine/renderer.js';
import { spawnUnit, updateUnits, updateProjectiles, clearClaims, UNIT_DEFS, MOVE_CLASSES, WEAPON_DEFS, terrainSample } from './game/units.js';
import { BUILDING_DEFS, spawnBuilding, updateBuildings, isValidPlacement, sitePlacement, footprintHasDeposit } from './game/buildings.js';
import {
  createEconomy, updateEconomy, canAffordBuilding, spendForBuilding, buildingCost,
  ECONOMY_TUNABLES, MOBILIZATION_BANDS,
} from './game/economy.js';
import { RESOURCE_DEFS, RESOURCE_LIST, MINE_TUNABLES } from './game/resources.js';
import {
  PRODUCTION_DEFS, updateProduction, categoryForFacility,
  IMPORT_TUNABLES, importCost, canAffordImport, importResource,
} from './game/production.js';
import { createStatCard } from './game/statcard.js';
import { computeFog, detects, fogState, drawFogOverlay } from './game/fog.js';
import { findRoadRoute, findPath, findPathCached, pathfindStats } from './game/pathfind.js';

const canvas = document.getElementById('view');
const hud = document.getElementById('hud');
const econHud = document.getElementById('econHud');
const buildbar = document.getElementById('buildbar');
const card = createStatCard(document.getElementById('card'));

// MAP SOURCE (docs/CONCEPT.md settled ledger: "maps and all place names are
// procedurally generated per skirmish run" — a fresh seed every game by
// default). Two optional URL params, either usable independently:
//   ?map=maps/theater-01.json  — load a specific AUTHORED file (the
//     scenario path; the map's own JSON is the source of truth, seed or no
//     seed inside it).
//   ?seed=1337                 — reproduce a specific GENERATED run.
// With neither param, a fresh random seed is drawn every load so each
// skirmish gets its own geography and its own names.
function randomSeed() {
  return Math.floor(Math.random() * 0xFFFFFFFF) >>> 0;
}
const urlParams = new URLSearchParams(location.search);
let map;
if (urlParams.has('map')) {
  map = await loadMap(urlParams.get('map'));
} else {
  const seedParam = urlParams.get('seed');
  const seed = seedParam !== null && seedParam !== '' && !Number.isNaN(Number(seedParam))
    ? (Number(seedParam) >>> 0)
    : randomSeed();
  map = await loadGeneratedMap(seed);
  console.log(`[mapgen] "${map.name}" (seed ${map.seed}) — ${map.genMs.toFixed(1)}ms, `
    + `${map.genStats.attempts} attempt(s), ${map.genStats.roadTileCount} road tiles`);
}
const renderer = createRenderer(canvas);
renderer.cam.x = map.worldW() / 2;
renderer.cam.y = map.worldH() / 2;
// frame the whole map on load (fit width AND height, with a little breathing
// room) so the whole island is visible instead of an arbitrary fixed zoom
renderer.cam.zoom = Math.max(0.08, Math.min(2,
  Math.min(canvas.clientWidth / map.worldW(), canvas.clientHeight / map.worldH()) * 0.9));

const world = { units: [], projectiles: [], hits: [], buildings: [] };

// MOBILIZATION ECONOMY CORE (P3 — game/economy.js). Created once at load,
// ticked every frame in loop() below with ZERO player input required — a
// fresh game's mobilizationLevel/IC/manpower/militaryOutput all advance on
// their own from here. See game/economy.js's ECONOMY_TUNABLES for every
// tunable number (designer's to set).
const economy = createEconomy();

// find the nearest water tile to a world point (spiral ring search over the
// grid) — used both to place the demo gunboat offshore and to snap naval
// move orders onto water, since a naval unit can't be ordered onto land.
function nearestWaterPoint(wx, wy, maxTiles = 40) {
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

// Inverse of nearestWaterPoint: nearest tile a GROUND unit could actually
// stand on (not water, not impassable terrain like mountain — read off
// t.moveMult generically rather than hardcoding a terrain-type name, since
// the map's terrain and layout are procedural per run). The demo spawn
// offsets below are chosen to land on land for a "typical" generated
// coastline, but a run's actual geometry is never known ahead of time, so
// every ground-unit demo spawn point is snapped through this — a no-op
// when the raw point is already fine, a rescue when a tight beach/city
// shape happens to put it a tile into the sea or up a mountainside.
function nearestLandPoint(wx, wy, maxTiles = 40) {
  const ts = map.tileSize;
  const gx0 = Math.floor(wx / ts), gy0 = Math.floor(wy / ts);
  for (let r = 0; r <= maxTiles; r++) {
    for (let dy = -r; dy <= r; dy++) {
      for (let dx = -r; dx <= r; dx++) {
        if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
        const t = map.terrainAt(gx0 + dx, gy0 + dy);
        if (t && !t.water && (t.moveMult ?? 1) > 0) return { x: (gx0 + dx + 0.5) * ts, y: (gy0 + dy + 0.5) * ts };
      }
    }
  }
  return { x: wx, y: wy };
}
// Convenience: spawn a ground unit at (wx,wy) rescued onto the nearest legal
// land tile if needed. Air units don't go through this (moveClass ignores
// terrain entirely, per units.js terrainSample), so callers below only wrap
// the ground-class spawns.
function spawnGroundUnit(key, wx, wy, side) {
  const p = nearestLandPoint(wx, wy);
  return spawnUnit(world, key, p.x, p.y, side);
}

// Combined-arms demo staged near the map's GENERATED spawn points (map.spawns
// — see game/mapgen.js) — exercises the full attribute matrix at once: every
// move class (foot/wheeled/tracked/air/naval), every weapon (shell/aam/sam/
// autocannon/rocket), and every targeting combination (SAM/AA/fighter vs a
// mixed enemy force that includes air) so the attribute system is
// observable, not just theoretical. Nothing here assumes a specific island's
// geometry: ground spawns are rescued onto legal land via spawnGroundUnit,
// naval spawns are snapped onto water via nearestWaterPoint, and pBase/eBase
// come straight from whatever map.spawns the generator produced this run.
const [pSpawn, eSpawn] = map.spawns;
const pBase = { x: pSpawn.x * map.tileSize, y: pSpawn.y * map.tileSize };
const eBase = { x: eSpawn.x * map.tileSize, y: eSpawn.y * map.tileSize };

// player: infantry + militia + tanks + AA on land, a SAM battery, a small air
// wing, gunboat + destroyer offshore
for (let i = 0; i < 3; i++) spawnGroundUnit('infantry', pBase.x - 70 + i * 22, pBase.y + 50, 'player');
for (let i = 0; i < 3; i++) spawnGroundUnit('militia', pBase.x - 70 + i * 22, pBase.y + 74, 'player');
for (let i = 0; i < 3; i++) spawnGroundUnit('tank', pBase.x + i * 26, pBase.y + (i % 2) * 26, 'player');
spawnGroundUnit('scout', pBase.x + 90, pBase.y + 20, 'player');
for (let i = 0; i < 2; i++) spawnGroundUnit('aa', pBase.x - 90 + i * 26, pBase.y - 40, 'player');
spawnGroundUnit('sam', pBase.x - 130, pBase.y - 10, 'player');
for (let i = 0; i < 2; i++) spawnUnit(world, 'fighter', pBase.x - 40 + i * 30, pBase.y - 100, 'player');
spawnUnit(world, 'strikejet', pBase.x + 10, pBase.y - 110, 'player');
{
  const gb = nearestWaterPoint(pBase.x, pBase.y);
  spawnUnit(world, 'gunboat', gb.x, gb.y, 'player');
  // offset further out to sea (not just nudged off the first water pixel
  // found, which can sit right at the coastline) — re-snap through
  // nearestWaterPoint so the destroyer never spawns straddling the beach
  const dest = nearestWaterPoint(gb.x - 60, gb.y + 10);
  spawnUnit(world, 'destroyer', dest.x, dest.y, 'player');
}

// enemy: mixed ground force PLUS air (fighter + strike jet) so SAM/AA/fighter
// targeting is actually exercised. eBase sits on a coastal beach tile near
// the map's farthest-from-capital city, with water usually nearby (see
// game/mapgen.js's enemySpawn/nearestBeach) — every ground spawn below goes
// through spawnGroundUnit so it's rescued onto land regardless of exactly
// how tight that particular run's beach/city shape turns out to be; air
// units spawn plain since their moveClass ignores terrain entirely.
for (let i = 0; i < 3; i++) spawnGroundUnit('tank', eBase.x - i * 26, eBase.y + (i % 2) * 26, 'enemy');
for (let i = 0; i < 2; i++) spawnGroundUnit('infantry', eBase.x - 60 - i * 22, eBase.y + 10, 'enemy');
for (let i = 0; i < 2; i++) spawnGroundUnit('militia', eBase.x - 60 - i * 22, eBase.y + 34, 'enemy');
spawnGroundUnit('aa', eBase.x + 20, eBase.y - 30, 'enemy');
spawnUnit(world, 'fighter', eBase.x - 20, eBase.y - 100, 'enemy');
spawnUnit(world, 'strikejet', eBase.x + 35, eBase.y - 115, 'enemy');

// FOG OF WAR CHANGES THIS: nearestEnemy's unbounded seek used to be enough on
// its own to march both forces across the whole map into contact — it's now
// gated on detection (game/fog.js), and pBase/eBase sit ~2200px apart, well
// beyond any unit's vision. Nothing would ever meet without a nudge. Give
// both sides a scripted opening order to close the gap — a legitimate order,
// not autonomous seeking, so it doesn't defeat the "no beelining toward
// hidden enemies" rule. holdGround batteries (SAM/AA) sit this out and
// garrison the home base, same as their doctrine implies. Once the fastest
// (highest-vision) unit on either side — the scout — gets close enough, it
// reveals the enemy to its whole side and the fight breaks out organically.
function orderAdvance(u, towardBase) {
  if (u.def.dispositions.includes('holdGround')) return;
  if (MOVE_CLASSES[u.def.moveClass].requiresWater) {
    u.order = nearestWaterPoint(towardBase.x, towardBase.y);
  } else {
    u.order = { x: towardBase.x + (Math.random() - 0.5) * 120, y: towardBase.y + (Math.random() - 0.5) * 120 };
  }
}
for (const u of world.units) orderAdvance(u, u.side === 'player' ? eBase : pBase);

let hovered = null, lastMouse = { x: 0, y: 0 };
canvas.addEventListener('mousemove', e => {
  lastMouse = { x: e.clientX, y: e.clientY };
  const [wx, wy] = renderer.screenToWorld(e.clientX, e.clientY);
  let best = null, bd = 20 * 20;
  for (const u of world.units) {
    // fog: an undetected enemy can't be hovered/stat-carded — the player
    // shouldn't be able to mouse-probe the fog for free intel.
    if (u.side !== 'player' && !detects('player', u)) continue;
    const dd = (u.x - wx) ** 2 + (u.y - wy) ** 2;
    if (dd < bd) { bd = dd; best = u; }
  }
  // buildings are big — hover-test their whole footprint rect, not just a
  // point-radius around their center, so a factory is hoverable across its
  // whole body the way a unit is within its (much smaller) radius.
  for (const b of world.buildings) {
    if (b.side !== 'player' && !detects('player', b)) continue;
    const ts = map.tileSize;
    const x0 = b.gx * ts, y0 = b.gy * ts, x1 = x0 + b.def.footprint.w * ts, y1 = y0 + b.def.footprint.h * ts;
    if (wx >= x0 && wx <= x1 && wy >= y0 && wy <= y1) best = b;
  }
  hovered = best;

  // ROAD MODE: after point A is placed, re-route the preview live under the
  // cursor so the player sees what the planner would actually build before
  // committing with the second click. Only recomputes when the cursor moves
  // to a new tile (not every pixel) — cheap enough not to matter at this
  // grid size, but no reason to solve A* every mousemove either.
  if (roadMode.active && roadMode.a) {
    const ts = map.tileSize;
    const gx = Math.floor(wx / ts), gy = Math.floor(wy / ts);
    if (!roadMode.previewGoalTile || roadMode.previewGoalTile.x !== gx || roadMode.previewGoalTile.y !== gy) {
      roadMode.previewGoalTile = { x: gx, y: gy };
      const route = findRoadRoute(map, roadMode.a.x, roadMode.a.y, wx, wy);
      roadMode.previewPath = route ? route.waypoints : null;
    }
  }

  // B MODE: track the footprint tile under the cursor so the frame draw can
  // paint a green/red preview — validity is the literal same isValidPlacement
  // rule the pin-override (shift-click) enforces, so the preview never lies
  // about what a pin would do (a planner-sited normal click may still move
  // the building off this exact spot if it can find a better-scored one).
  if (buildMode.active && buildMode.selectedType) {
    const ts = map.tileSize;
    const def = BUILDING_DEFS[buildMode.selectedType];
    const gx = Math.round(wx / ts - def.footprint.w / 2);
    const gy = Math.round(wy / ts - def.footprint.h / 2);
    buildMode.previewGx = gx;
    buildMode.previewGy = gy;
    buildMode.previewValid = isValidPlacement(map, gx, gy, def);
  }
});
attachCameraControls(canvas, renderer.cam, { isEntityHit: () => !!hovered });

// right-click issues a move order to the player's units (basic input, proves
// the `order` field). Routed by move-class attribute, not per-unit code:
// ground and air units take the raw click point (ground wall-slides off
// impassable terrain on its own; air ignores terrain entirely); naval units
// only ever accept a water point, so their order gets snapped to the
// nearest water tile — ordering a gunboat onto a beach just parks it
// offshore of where you clicked instead of doing nothing.
//
// While ROAD MODE is active, right-click means "cancel" instead (per spec:
// Esc/right-click exits road mode) — it does not also issue a move order.
// Extracted from the contextmenu handler so a headless test can drive the
// exact same coalescing path without needing to fake mouse/camera math
// (see window.__debug.issueMoveOrder below).
function issueMoveOrder(wx, wy) {
  // COALESCE: a mass order (say, 15 units at once) shouldn't trigger 15
  // separate A* solves next frame just because per-unit jitter happens to
  // spill a few of them into a neighboring goal tile. Pre-warm the path
  // cache ONCE per ground move class actually present in this order,
  // solved from that class's own centroid to the un-jittered click point —
  // every unit of that class then hits the same cache entry in its own
  // lazy ensurePath() call next frame (game/units.js), each picking its own
  // nearest-waypoint entry point onto the shared route (the "per-unit
  // offset" half of coalescing).
  const groups = new Map(); // moveClassName -> {sumX, sumY, n, moveClass}
  for (const u of world.units) {
    if (u.side !== 'player') continue;
    const mc = MOVE_CLASSES[u.def.moveClass];
    if (!mc.ground) continue;
    let g = groups.get(u.def.moveClass);
    if (!g) { g = { sumX: 0, sumY: 0, n: 0, moveClass: mc }; groups.set(u.def.moveClass, g); }
    g.sumX += u.x; g.sumY += u.y; g.n++;
  }
  for (const [moveClassName, g] of groups) {
    findPathCached(map, moveClassName, g.moveClass, g.sumX / g.n, g.sumY / g.n, wx, wy);
  }

  for (const u of world.units) {
    if (u.side !== 'player') continue;
    const jx = wx + (Math.random() - 0.5) * 12, jy = wy + (Math.random() - 0.5) * 12;
    u.order = MOVE_CLASSES[u.def.moveClass].requiresWater ? nearestWaterPoint(jx, jy) : { x: jx, y: jy };
  }
}

canvas.addEventListener('contextmenu', e => {
  e.preventDefault();
  if (roadMode.active) { exitRoadMode(); return; }
  if (buildMode.active) { exitBuildMode(); return; }
  const [wx, wy] = renderer.screenToWorld(e.clientX, e.clientY);
  issueMoveOrder(wx, wy);
});

// ---------------------------------------------------------------------------
// ROAD MODE (P2 — "The Map Is a Machine" / Pillar 1's delegated construction:
// the player states intent — point A to point B — and the nation's planners
// route the actual road via the same A* cost logic pathfinding uses, not a
// straight line). Press R to enter/exit; while active, click point A, then
// point B; Esc or right-click cancels a placement or exits the mode.
const roadMode = { active: false, a: null, previewPath: null, previewGoalTile: null };

function exitRoadMode() {
  roadMode.active = false;
  roadMode.a = null;
  roadMode.previewPath = null;
  roadMode.previewGoalTile = null;
}
function enterRoadMode() {
  roadMode.active = true;
  roadMode.a = null;
  roadMode.previewPath = null;
  roadMode.previewGoalTile = null;
}

// ---------------------------------------------------------------------------
// BUILD MODE (P2 city layer — CONCEPT.md Pillar 1's signature verb,
// "delegated construction"): press B to open a minimal DOM strip of building
// types, click a type, then click a rough location — the NATION'S PLANNERS
// site the actual placement (game/buildings.js sitePlacement, spiral search
// scored by buildable terrain + no overlap + a mild near-road/near-city
// preference). Shift-click is the pressure-valve pin override: place exactly
// at the clicked tile if valid, else reject with a brief on-screen message.
// Construction is instant/free (no economy until P3, per CONCEPT.md).
//
// R and B are made mutually exclusive at the input level (entering one exits
// the other first) so they never fight over clicks/Esc.
const buildMode = {
  active: false, selectedType: null,
  previewGx: undefined, previewGy: undefined, previewValid: false,
  message: null, messageUntil: 0,
};

for (const key of Object.keys(BUILDING_DEFS)) {
  const btn = document.createElement('button');
  btn.textContent = BUILDING_DEFS[key].name;
  btn.dataset.key = key;
  btn.addEventListener('click', () => selectBuildType(key));
  buildbar.appendChild(btn);
}

function updateBuildBarUI() {
  for (const btn of buildbar.children) btn.classList.toggle('selected', btn.dataset.key === buildMode.selectedType);
}
function selectBuildType(key) {
  buildMode.selectedType = key;
  buildMode.previewGx = undefined;
  updateBuildBarUI();
}
function enterBuildMode() {
  buildMode.active = true;
  buildbar.classList.add('open');
  updateBuildBarUI();
}
function exitBuildMode() {
  buildMode.active = false;
  buildMode.selectedType = null;
  buildMode.previewGx = undefined;
  buildbar.classList.remove('open');
}
function flashMessage(text) {
  buildMode.message = text;
  buildMode.messageUntil = performance.now() + 2400;
}

// Left-click while in B mode: DELEGATED placement (CONCEPT.md Pillar 1) —
// the planner's spiral search (game/buildings.js sitePlacement) picks the
// actual site near the click. Shift-click is the pin override: place
// exactly at the hovered footprint tile (buildMode.previewGx/Gy, the same
// tile the green/red preview is drawn from) or reject with a brief message
// if that exact spot is invalid — no silent fallback to the planner.
//
// P3: construction now costs IC (+manpower for one building type) and takes
// build time — checked/spent here via game/economy.js BEFORE spawnBuilding
// is called; an unaffordable placement is rejected with a brief message and
// nothing is spent or spawned, same shape as the existing "invalid site"
// rejection above it.
function affordabilityMessage(key, def) {
  const cost = buildingCost(key);
  const parts = [`${cost.ic || 0} IC`];
  if (cost.manpower) parts.push(`${cost.manpower} manpower`);
  return `Can't afford ${def.name} — needs ${parts.join(' + ')} `
    + `(have ${Math.floor(economy.ic)} IC, ${Math.floor(economy.manpower)} manpower).`;
}
canvas.addEventListener('click', e => {
  if (!buildMode.active || !buildMode.selectedType) return;
  const key = buildMode.selectedType;
  const def = BUILDING_DEFS[key];
  const [wx, wy] = renderer.screenToWorld(e.clientX, e.clientY);
  if (e.shiftKey) {
    const gx = buildMode.previewGx, gy = buildMode.previewGy;
    if (gx === undefined || !isValidPlacement(map, gx, gy, def)) {
      // Mines carry an extra siting rule (def.requiresDeposit — see
      // game/buildings.js) on top of the generic buildable/no-overlap check:
      // call it out specifically so "why was this rejected" is legible
      // rather than folding into the same generic message every other
      // building type gets.
      const depositIssue = def.requiresDeposit && gx !== undefined && !footprintHasDeposit(map, gx, gy, def);
      flashMessage(depositIssue
        ? `Can't pin ${def.name} there — no resource deposit on/adjacent to that site.`
        : `Can't pin ${def.name} there — invalid site.`);
      return;
    }
    if (!canAffordBuilding(economy, key)) {
      flashMessage(affordabilityMessage(key, def));
      return;
    }
    spendForBuilding(economy, key);
    spawnBuilding(world, map, key, gx, gy, 'player');
    flashMessage(`${def.name} pinned — under construction.`);
  } else {
    const site = sitePlacement(map, key, wx, wy);
    if (!site) {
      flashMessage(def.requiresDeposit
        ? `No resource deposit found near there for ${def.name} — mines must site on/adjacent to a deposit.`
        : `No buildable site found near there for ${def.name}.`);
      return;
    }
    if (!canAffordBuilding(economy, key)) {
      flashMessage(affordabilityMessage(key, def));
      return;
    }
    spendForBuilding(economy, key);
    spawnBuilding(world, map, key, site.x, site.y, 'player');
    flashMessage(`${def.name} sited by the planners — under construction.`);
  }
});

// ---------------------------------------------------------------------------
// "CHOOSE WHAT TO PRODUCE" PANEL (P4 — CONCEPT.md's settled "Production &
// supply chain" ledger: "you pick a production category ... see its recipe
// ... build the factory"). Press P to open; click a category button and (a)
// its recipe/requirements are shown in #recipeInfo BEFORE anything is spent
// or placed, so the player learns the supply chain they need to build, and
// (b) B-mode is armed with that category's facility preselected — placing
// it then goes through the EXACT SAME costed+timed construction pipeline
// (isValidPlacement/sitePlacement/spawnBuilding) every other building uses;
// this panel is purely a friendlier front door onto that same buildbar
// entry, not a separate placement path.
const prodPanel = document.getElementById('prodPanel');
const prodCatRow = document.getElementById('prodCatRow');
const recipeInfo = document.getElementById('recipeInfo');
let selectedCategory = null;

function resourceCostText(resourceCostPerUnit) {
  return Object.entries(resourceCostPerUnit || {})
    .map(([rid, amt]) => `${RESOURCE_DEFS[rid] ? RESOURCE_DEFS[rid].name : rid} ${amt}/unit`)
    .join(', ') || 'none';
}
function recipeHtml(category) {
  const prod = PRODUCTION_DEFS[category];
  const facilityDef = BUILDING_DEFS[prod.facility];
  const prereq = prod.recipe.prerequisiteBuildings.length
    ? prod.recipe.prerequisiteBuildings.map(k => BUILDING_DEFS[k].name).join(', ')
    : 'none';
  const outputs = prod.outputs.map(k => UNIT_DEFS[k].name).join(', ');
  const facCost = facilityDef.cost || {};
  return `<b>${prod.name}</b> — produces: ${outputs}\n`
    + `Facility: ${facilityDef.name} (${facCost.ic || 0} IC${facCost.manpower ? ` + ${facCost.manpower} manpower` : ''}, ${facilityDef.buildTime}s to build)\n`
    + `Resources/unit: ${resourceCostText(prod.recipe.resourceCostPerUnit)}\n`
    + `Prerequisite buildings: ${prereq}\n`
    + `IC/unit: ${prod.recipe.icCostPerUnit}   Manpower/unit: ${prod.recipe.manpowerCostPerUnit}\n`
    + `Rate at full supply: 1 unit / ${prod.recipe.buildTimePerUnit}s`;
}
for (const category of Object.keys(PRODUCTION_DEFS)) {
  const btn = document.createElement('button');
  btn.textContent = PRODUCTION_DEFS[category].name;
  btn.dataset.category = category;
  btn.addEventListener('click', () => selectCategory(category));
  prodCatRow.appendChild(btn);
}
function updateProdPanelUI() {
  for (const btn of prodCatRow.children) btn.classList.toggle('selected', btn.dataset.category === selectedCategory);
}
function selectCategory(category) {
  selectedCategory = category;
  updateProdPanelUI();
  recipeInfo.innerHTML = recipeHtml(category);
  // Arm B-mode with this category's facility preselected — same pipeline
  // the buildbar's own per-building buttons use (selectBuildType), just
  // reached from the recipe-first front door instead of a raw building list.
  if (roadMode.active) exitRoadMode();
  enterBuildMode();
  selectBuildType(PRODUCTION_DEFS[category].facility);
}
function enterProdPanel() { prodPanel.classList.add('open'); }
function exitProdPanel() { prodPanel.classList.remove('open'); }

// ---------------------------------------------------------------------------
// IMPORT PANEL (P4 — CONCEPT.md: missing resources are importable "in
// exchange for INFLUENCE + a bit of IC — a pricier stopgap, not a
// replacement for owning the deposit"). Press I to open; each button
// imports one BATCH_SIZE of that resource immediately if affordable
// (game/production.js importResource) — a single explicit spend per click,
// no queue.
const importPanel = document.getElementById('importPanel');
const importResRow = document.getElementById('importResRow');
function importButtonLabel(rid) {
  const cost = importCost();
  return `${RESOURCE_DEFS[rid].name} +${IMPORT_TUNABLES.BATCH_SIZE} (${cost.influence} infl + ${cost.ic} IC)`;
}
for (const r of RESOURCE_LIST) {
  const btn = document.createElement('button');
  btn.textContent = importButtonLabel(r.id);
  btn.dataset.resource = r.id;
  btn.addEventListener('click', () => {
    const ok = importResource(economy, r.id);
    flashMessage(ok
      ? `Imported ${IMPORT_TUNABLES.BATCH_SIZE} ${RESOURCE_DEFS[r.id].name}.`
      : `Can't afford to import ${RESOURCE_DEFS[r.id].name} — needs ${importCost().influence} influence + ${importCost().ic} IC.`);
  });
  importResRow.appendChild(btn);
}
function enterImportPanel() { importPanel.classList.add('open'); }
function exitImportPanel() { importPanel.classList.remove('open'); }

addEventListener('keydown', e => {
  if (e.repeat) return;
  if (e.key === 'r' || e.key === 'R') {
    if (buildMode.active) exitBuildMode();
    if (prodPanel.classList.contains('open')) exitProdPanel();
    if (roadMode.active) exitRoadMode(); else enterRoadMode();
  } else if (e.key === 'b' || e.key === 'B') {
    if (roadMode.active) exitRoadMode();
    if (prodPanel.classList.contains('open')) exitProdPanel();
    if (buildMode.active) exitBuildMode(); else enterBuildMode();
  } else if (e.key === 'p' || e.key === 'P') {
    if (prodPanel.classList.contains('open')) exitProdPanel(); else enterProdPanel();
  } else if (e.key === 'i' || e.key === 'I') {
    if (importPanel.classList.contains('open')) exitImportPanel(); else enterImportPanel();
  } else if (e.key === 'Escape') {
    if (roadMode.active) exitRoadMode();
    if (buildMode.active) exitBuildMode();
    if (prodPanel.classList.contains('open')) exitProdPanel();
    if (importPanel.classList.contains('open')) exitImportPanel();
  }
});

// Left-click places point A, then point B. Construction is instant (timed
// construction is a P3/economy-phase concern per CONCEPT.md) — the planner
// routes with findRoadRoute (game/pathfind.js) and lays every tile along the
// dense route, then bumps map.dirty() ONCE so the renderer re-prerenders the
// new road as a single repaint instead of once per tile.
canvas.addEventListener('click', e => {
  if (!roadMode.active) return;
  const [wx, wy] = renderer.screenToWorld(e.clientX, e.clientY);
  if (!roadMode.a) {
    roadMode.a = { x: wx, y: wy };
    roadMode.previewPath = null;
    roadMode.previewGoalTile = null;
    return;
  }
  const route = findRoadRoute(map, roadMode.a.x, roadMode.a.y, wx, wy);
  if (route) {
    for (const t of route.tiles) map.setRoad(t.x, t.y, 1);
    map.dirty();
  }
  roadMode.a = null;
  roadMode.previewPath = null;
  roadMode.previewGoalTile = null;
});

function drawRoadPreview(ctx, worldToScreen) {
  if (!roadMode.active || !roadMode.a) return;
  ctx.save();
  const [ax, ay] = worldToScreen(roadMode.a.x, roadMode.a.y);
  ctx.fillStyle = 'rgba(255,230,150,0.9)';
  ctx.beginPath(); ctx.arc(ax, ay, 5, 0, 6.28); ctx.fill();
  if (roadMode.previewPath && roadMode.previewPath.length) {
    ctx.strokeStyle = 'rgba(255,230,150,0.85)';
    ctx.lineWidth = 2;
    ctx.setLineDash([7, 5]);
    ctx.beginPath();
    ctx.moveTo(ax, ay);
    for (const p of roadMode.previewPath) {
      const [sx, sy] = worldToScreen(p.x, p.y);
      ctx.lineTo(sx, sy);
    }
    ctx.stroke();
    ctx.setLineDash([]);
  }
  ctx.restore();
}

// B-mode footprint preview: green if isValidPlacement (what shift-click's
// exact pin would do right now), red otherwise. Purely a preview — the
// planner's normal (non-shift) click can still choose a different, better-
// scored spot nearby; this just tells the player the literal rule at the
// exact tile under the cursor.
function drawBuildPreview(ctx, worldToScreen) {
  if (!buildMode.active || !buildMode.selectedType || buildMode.previewGx === undefined) return;
  const def = BUILDING_DEFS[buildMode.selectedType];
  const ts = map.tileSize;
  const [x0, y0] = worldToScreen(buildMode.previewGx * ts, buildMode.previewGy * ts);
  const [x1, y1] = worldToScreen((buildMode.previewGx + def.footprint.w) * ts, (buildMode.previewGy + def.footprint.h) * ts);
  ctx.save();
  ctx.fillStyle = buildMode.previewValid ? 'rgba(90,255,140,0.28)' : 'rgba(255,90,90,0.28)';
  ctx.strokeStyle = buildMode.previewValid ? 'rgba(120,255,160,0.9)' : 'rgba(255,120,120,0.9)';
  ctx.lineWidth = 2;
  ctx.fillRect(x0, y0, x1 - x0, y1 - y0);
  ctx.strokeRect(x0, y0, x1 - x0, y1 - y0);
  ctx.restore();
}

function drawBuilding(ctx, worldToScreen, cam, b) {
  const ts = map.tileSize;
  const [x0, y0] = worldToScreen(b.gx * ts, b.gy * ts);
  const [x1, y1] = worldToScreen((b.gx + b.def.footprint.w) * ts, (b.gy + b.def.footprint.h) * ts);
  const constructing = b.status === 'constructing';
  ctx.save();
  // P3: an in-progress footprint reads visually distinct — dashed outline,
  // lower fill opacity — so "this isn't a real building yet" is legible at
  // a glance, same information the buildbar/HUD progress readout repeats.
  ctx.fillStyle = b.side === 'player'
    ? (constructing ? 'rgba(70,120,170,0.42)' : 'rgba(70,120,170,0.92)')
    : (constructing ? 'rgba(150,60,60,0.42)' : 'rgba(150,60,60,0.92)');
  ctx.strokeStyle = b.side === 'player' ? '#5fd0ff' : '#ff5a5a';
  ctx.lineWidth = 1.5;
  if (constructing) ctx.setLineDash([5, 4]);
  ctx.fillRect(x0, y0, x1 - x0, y1 - y0);
  ctx.strokeRect(x0, y0, x1 - x0, y1 - y0);
  ctx.setLineDash([]);
  ctx.restore();
  if (b.hp < b.maxHp) {
    const w = x1 - x0;
    ctx.fillStyle = 'rgba(0,0,0,.5)'; ctx.fillRect(x0, y0 - 8, w, 3);
    ctx.fillStyle = '#6dffb0'; ctx.fillRect(x0, y0 - 8, w * (b.hp / b.maxHp), 3);
  }
  if (constructing) {
    // build-progress bar, below the footprint (the HP bar above it already
    // owns the top edge) — amber to read distinctly from the green HP bar.
    const w = x1 - x0;
    ctx.fillStyle = 'rgba(0,0,0,.5)'; ctx.fillRect(x0, y1 + 3, w, 4);
    ctx.fillStyle = '#ffcf5c'; ctx.fillRect(x0, y1 + 3, w * b.buildProgress, 4);
    if (cam.zoom > 0.25) {
      ctx.fillStyle = '#ffe9b0';
      ctx.font = '10px Consolas, monospace';
      ctx.textAlign = 'center';
      ctx.fillText(`${Math.round(b.buildProgress * 100)}%`, (x0 + x1) / 2, y1 + 17);
      ctx.textAlign = 'left';
    }
  }
  // P4: production facilities show their live state right on the map —
  // "producing" (green) or "STALLED — needs X" (amber) — same info the econ
  // HUD's Production block repeats in text form, so it's readable at a
  // glance without hunting the HUD for which specific building is stuck.
  if (!constructing && b.prodCategory && cam.zoom > 0.2) {
    const label = b.prodState === 'producing' ? `▶ producing (${b.producedCount} built)` : `⏸ STALLED — ${b.prodStallReason}`;
    ctx.fillStyle = b.prodState === 'producing' ? '#8dffb0' : '#ffcf5c';
    ctx.font = '10px Consolas, monospace';
    ctx.textAlign = 'center';
    ctx.fillText(label, (x0 + x1) / 2, y1 + 15);
    ctx.textAlign = 'left';
  }
  if (b === hovered) { ctx.strokeStyle = '#fff'; ctx.lineWidth = 1.5; ctx.strokeRect(x0 - 3, y0 - 3, (x1 - x0) + 6, (y1 - y0) + 6); }
}

function drawUnit(ctx, worldToScreen, cam, u) {
  const [sx, sy] = worldToScreen(u.x, u.y);
  const r = u.def.radius * cam.zoom * devicePixelRatio;
  // domain read-at-a-glance: air units cast a small drop shadow (hints
  // altitude), naval units get a wake ring (hints they're hull-in-water) —
  // purely cosmetic, driven off the same `domain` attribute as everything else.
  if (u.def.domain === 'air') {
    ctx.beginPath(); ctx.ellipse(sx, sy + r * 0.7, r * 0.75, r * 0.32, 0, 0, 6.28);
    ctx.fillStyle = 'rgba(0,0,0,.35)'; ctx.fill();
  } else if (u.def.domain === 'naval') {
    ctx.beginPath(); ctx.arc(sx, sy, r * 1.7, 0, 6.28);
    ctx.strokeStyle = 'rgba(190,225,255,.4)'; ctx.lineWidth = 1; ctx.stroke();
  }
  ctx.save();
  ctx.translate(sx, sy);
  ctx.rotate(u.aim || 0);
  ctx.fillStyle = u.side === 'player' ? '#5fd0ff' : '#ff5a5a';
  ctx.beginPath();
  ctx.moveTo(r * 1.3, 0);
  ctx.lineTo(-r, r * 0.8);
  ctx.lineTo(-r, -r * 0.8);
  ctx.closePath();
  ctx.fill();
  ctx.restore();
  if (u.hp < u.def.hp) {
    const w = r * 2.4;
    ctx.fillStyle = 'rgba(0,0,0,.5)'; ctx.fillRect(sx - w / 2, sy - r - 8, w, 3);
    ctx.fillStyle = '#6dffb0'; ctx.fillRect(sx - w / 2, sy - r - 8, w * (u.hp / u.def.hp), 3);
  }
  if (u === hovered) { ctx.strokeStyle = '#fff'; ctx.lineWidth = 1.5; ctx.beginPath(); ctx.arc(sx, sy, r + 5, 0, 6.28); ctx.stroke(); }
}

function drawProjectile(ctx, worldToScreen, cam, p) {
  const y = p.physics === 'ballistic' ? p.y - (p.lofted || 0) : p.y;
  const [sx, sy] = worldToScreen(p.x, y);
  ctx.fillStyle = p.side === 'player' ? '#fff0a0' : '#ffb27a';
  ctx.beginPath();
  ctx.arc(sx, sy, Math.max(2, 3 * cam.zoom), 0, 6.28);
  ctx.fill();
}

// headless test hook — safe to leave, no gameplay effect. Exposes spawnUnit
// and the attribute tables too, so a test harness can stage its own
// scenarios (e.g. an isolated SAM-vs-tank standoff) beyond the shipped demo.
// fog: exposes detects() directly and the revealAll flag so a test can flip
// fog off (to compare drawn-vs-hidden positions) or query detection without
// having to reverse-engineer it from rendered pixels.
window.__debug = {
  world, renderer, map, spawnUnit, UNIT_DEFS, MOVE_CLASSES, WEAPON_DEFS, nearestWaterPoint,
  fog: Object.assign(fogState, { detects }),
  // pathfinding + road-building hooks (P2), for headless verification:
  // pathfindStats.solves carries {kind, ms, nodes, t} for every A* solve
  // actually run (cache hits don't add entries), roadMode exposes the R-mode
  // UI state, findPath/findRoadRoute let a test harness solve/time routes
  // directly without needing real DOM input, enterRoadMode/exitRoadMode let
  // a test drive the mode without depending on exact key event shape.
  pathfindStats, roadMode, findPath, findRoadRoute, enterRoadMode, exitRoadMode, terrainSample, issueMoveOrder,
  // buildings / B-mode hooks (P2 city layer), for headless verification:
  // BUILDING_DEFS/spawnBuilding/isValidPlacement/sitePlacement let a test
  // stage or query construction directly; buildMode exposes the UI state
  // (selectedType/previewGx/previewGy/previewValid/message) the same way
  // roadMode already does above; enter/exit/select drive the mode without
  // needing real key/click events.
  BUILDING_DEFS, spawnBuilding, isValidPlacement, sitePlacement, buildMode,
  enterBuildMode, exitBuildMode, selectBuildType,
  // economy hooks (P3 — game/economy.js), for headless verification:
  // `economy` is the live state object the real loop mutates every frame
  // (read it directly to observe the auto-ramp with real wall-clock time);
  // ECONOMY_TUNABLES/MOBILIZATION_BANDS expose the tunable data block;
  // canAffordBuilding/buildingCost mirror the B-mode cost check so a test
  // can query affordability without staging a click. simulateEconomy fast-
  // forwards the economy tick in fixed dt steps WITHOUT touching
  // units/projectiles/rendering — a test-only convenience for comparing
  // curves (e.g. "with vs without the accelerator lever") faster than
  // waiting real wall-clock seconds; the real per-frame loop above is what
  // actually proves "ramps on its own during normal play."
  economy, ECONOMY_TUNABLES, MOBILIZATION_BANDS, canAffordBuilding, buildingCost,
  // resource/deposit hooks (P3 follow-up — game/resources.js, map.deposits/
  // depositAt from game/mapgen.js via engine/tilemap.js), for headless
  // verification: RESOURCE_DEFS/RESOURCE_LIST/MINE_TUNABLES expose the
  // tunable data block; footprintHasDeposit mirrors the siting check a mine
  // placement goes through, so a test can query it without staging a click.
  // map.deposits/map.depositAt are already reachable off `map` above.
  RESOURCE_DEFS, RESOURCE_LIST, MINE_TUNABLES, footprintHasDeposit,
  simulateEconomy(seconds, stepDt = 0.5) {
    let t = 0;
    while (t < seconds) {
      const step = Math.min(stepDt, seconds - t);
      updateEconomy(world, economy, map, step);
      t += step;
    }
  },
  // production-chain hooks (P4 — game/production.js), for headless
  // verification: PRODUCTION_DEFS is the recipe table itself;
  // categoryForFacility mirrors the reverse lookup updateProduction uses;
  // IMPORT_TUNABLES/importCost/canAffordImport/importResource let a test
  // drive the import fallback directly without staging an #importPanel
  // click; prodPanel/importPanel expose the UI open/selected state the same
  // way buildMode/roadMode already do; enter/exit/select* drive the panels
  // without needing real key/click events. fastForward advances
  // construction + economy + production together in fixed dt steps WITHOUT
  // touching units/projectiles/rendering — lets a test observe "stalled ->
  // mines built -> producing -> N units at t" over simulated minutes in
  // milliseconds of real time, using the EXACT SAME updateBuildings/
  // updateEconomy/updateProduction calls the real per-frame loop below
  // makes, so behavior never diverges between the fast-forwarded test path
  // and normal play.
  PRODUCTION_DEFS, categoryForFacility, IMPORT_TUNABLES, importCost, canAffordImport, importResource,
  prodPanel, importPanel, selectCategory, enterProdPanel, exitProdPanel, enterImportPanel, exitImportPanel,
  get selectedCategory() { return selectedCategory; },
  fastForward(seconds, stepDt = 0.5) {
    let t = 0;
    while (t < seconds) {
      const step = Math.min(stepDt, seconds - t);
      updateBuildings(world, step, map);
      updateEconomy(world, economy, map, step);
      updateProduction(world, economy, map, step);
      t += step;
    }
  },
};

let last = performance.now();
function loop(now) {
  const dt = Math.min(0.05, (now - last) / 1000);
  last = now;

  // fog must be current BEFORE targeting runs (pickTarget/nearestEnemy read
  // it) and before rendering reads it for draw-filtering/hover/overlay.
  computeFog(world, map);

  clearClaims();
  updateUnits(world, dt, map);
  updateBuildings(world, dt, map);
  // P3: the mobilization economy ticks every frame, unconditionally — this
  // is the "zero player input" ramp (game/economy.js). Runs after
  // updateBuildings so a building that JUST completed construction this
  // frame already counts toward econ effects this same tick.
  updateEconomy(world, economy, map, dt);
  // P4: production facilities (game/production.js) run AFTER the economy
  // tick so they read this frame's freshly-accrued resource/IC/manpower
  // pools, same ordering rationale as updateEconomy running after
  // updateBuildings (a facility that just finished construction this frame
  // already counts).
  updateProduction(world, economy, map, dt);
  updateProjectiles(world, dt);
  for (const h of world.hits) h.life -= dt;
  world.hits = world.hits.filter(h => h.life > 0);

  renderer.frame(map, (ctx, worldToScreen, cam) => {
    drawFogOverlay(ctx, worldToScreen, 'player', world, map);
    for (const b of world.buildings) {
      // fog: same rule as units — an undetected enemy building doesn't draw.
      if (b.side !== 'player' && !detects('player', b)) continue;
      drawBuilding(ctx, worldToScreen, cam, b);
    }
    for (const p of world.projectiles) drawProjectile(ctx, worldToScreen, cam, p);
    for (const u of world.units) {
      // fog: only draw enemy units the player side currently detects. Own
      // units always draw regardless (you always see yourself).
      if (u.side !== 'player' && !detects('player', u)) continue;
      drawUnit(ctx, worldToScreen, cam, u);
    }
    for (const h of world.hits) {
      const [sx, sy] = worldToScreen(h.x, h.y);
      ctx.strokeStyle = `rgba(255,200,120,${h.life * 4})`;
      ctx.lineWidth = 2;
      ctx.beginPath(); ctx.arc(sx, sy, (0.25 - h.life) * 60, 0, 6.28); ctx.stroke();
    }
    drawRoadPreview(ctx, worldToScreen);
    drawBuildPreview(ctx, worldToScreen);
  });

  card.show(hovered, lastMouse.x, lastMouse.y);
  const alive = { player: 0, enemy: 0 };
  for (const u of world.units) alive[u.side]++;
  const roadHint = roadMode.active
    ? (roadMode.a ? 'road mode: click point B (Esc/right-click to cancel)' : 'road mode: click point A (Esc/right-click to exit)')
    : 'R: build road';
  const buildHint = buildMode.active
    ? (buildMode.selectedType ? 'build mode: click a rough spot (shift-click to pin exact) — Esc to exit' : 'build mode: pick a building below — Esc to exit')
    : 'B: build';
  const flash = (buildMode.message && performance.now() < buildMode.messageUntil) ? `  |  ${buildMode.message}` : '';
  // Island name + seed shown subtly up front (map.seed is only set for a
  // procedurally-generated map — see game/mapgen.js/loadGeneratedMap; an
  // authored file loaded via ?map= just shows its name) so a player can
  // share/reproduce a good island via ?seed=N.
  const seedTag = map.seed !== undefined ? ` (seed ${map.seed})` : '';
  hud.textContent = `${map.name}${seedTag} — player ${alive.player} vs enemy ${alive.enemy} — right-click: move order — drag: pan — wheel: zoom — ${roadHint} — ${buildHint}${flash}`;

  // Resource stockpile line (P3 follow-up — game/resources.js): compact,
  // icons (colored glyph spans) + amount + current rate, one resource per
  // line-item, joined with a couple of spaces so it wraps as a paragraph
  // instead of forcing #econHud wider. Iterates RESOURCE_LIST generically —
  // a new resource entry there shows up here with no HUD change.
  const resourceLine = RESOURCE_LIST.map(r => {
    const amt = economy.resources[r.id] || 0;
    const rate = economy.resourceRates[r.id] || 0;
    return `<span style="color:${r.color}">${r.glyph}</span> ${amt.toFixed(0)} (+${rate.toFixed(2)}/s)`;
  }).join('&nbsp;&nbsp;');

  // P3 economy HUD — compact readout: pool/cap + current rate for IC and
  // manpower, mobilization % + its band label, military output + rate, and
  // (below) the resource stockpiles mines feed.
  // P4: one status line per complete player production facility — "state
  // (producing/stalled-needs-X) + rate + units made so far", generic over
  // whatever's in world.buildings (no per-category branching here; a
  // facility's own b.prodState/b.prodStallReason/b.producedCount, set by
  // game/production.js's updateProduction, carry everything this needs).
  const prodLines = [];
  for (const b of world.buildings) {
    if (b.side !== 'player' || !b.prodCategory) continue;
    const prod = PRODUCTION_DEFS[b.prodCategory];
    const stateTxt = b.prodState === 'producing'
      ? `producing (${(1 / prod.recipe.buildTimePerUnit).toFixed(2)}/s)`
      : `STALLED — ${b.prodStallReason}`;
    prodLines.push(`${prod.name}: ${stateTxt} — ${b.producedCount} built`);
  }
  const prodBlock = prodLines.length ? `\n<b>Production</b>\n${prodLines.join('\n')}` : '';

  econHud.innerHTML =
    `<b>IC</b> ${economy.ic.toFixed(0)}/${economy.icCap.toFixed(0)}  (+${economy.icRate.toFixed(2)}/s)\n`
    + `<b>Manpower</b> ${economy.manpower.toFixed(0)}/${economy.manpowerCap.toFixed(0)}  (+${economy.manpowerRate.toFixed(2)}/s)\n`
    + `<b>Influence</b> ${economy.influence.toFixed(0)}/${economy.influenceCap.toFixed(0)}  (+${economy.influenceRate.toFixed(2)}/s)\n`
    + `<b>Mobilization</b> ${economy.mobilizationLevel.toFixed(1)}%  (+${economy.mobilizationRate.toFixed(3)}/s)\n`
    + `${economy.band.label}\n`
    + `<b>Military Output</b> ${economy.militaryOutput.toFixed(1)}  (+${economy.militaryOutputRate.toFixed(2)}/s)\n`
    + `Arming quality x${economy.armingQuality.toFixed(2)}\n`
    + `<b>Resources</b>\n${resourceLine}`
    + prodBlock;

  requestAnimationFrame(loop);
}
requestAnimationFrame(loop);

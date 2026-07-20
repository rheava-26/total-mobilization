import { loadMap } from './engine/tilemap.js';
import { createRenderer, attachCameraControls } from './engine/renderer.js';
import { spawnUnit, updateUnits, updateProjectiles, clearClaims, UNIT_DEFS, MOVE_CLASSES, WEAPON_DEFS, terrainSample } from './game/units.js';
import { BUILDING_DEFS, spawnBuilding, updateBuildings, isValidPlacement, sitePlacement } from './game/buildings.js';
import { createStatCard } from './game/statcard.js';
import { computeFog, detects, fogState, drawFogOverlay } from './game/fog.js';
import { findRoadRoute, findPath, findPathCached, pathfindStats } from './game/pathfind.js';

const canvas = document.getElementById('view');
const hud = document.getElementById('hud');
const buildbar = document.getElementById('buildbar');
const card = createStatCard(document.getElementById('card'));

const map = await loadMap('maps/theater-01.json');
const renderer = createRenderer(canvas);
renderer.cam.x = map.worldW() / 2;
renderer.cam.y = map.worldH() / 2;
// frame the whole map on load (fit width AND height, with a little breathing
// room) so the whole island is visible instead of an arbitrary fixed zoom
renderer.cam.zoom = Math.max(0.08, Math.min(2,
  Math.min(canvas.clientWidth / map.worldW(), canvas.clientHeight / map.worldH()) * 0.9));

const world = { units: [], projectiles: [], hits: [], buildings: [] };

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

// Combined-arms demo staged near the map's authored spawn points — exercises
// the full attribute matrix at once: every move class (foot/wheeled/tracked/
// air/naval), every weapon (shell/aam/sam/autocannon/rocket), and every
// targeting combination (SAM/AA/fighter vs a mixed enemy force that includes
// air) so the attribute system is observable, not just theoretical.
const [pSpawn, eSpawn] = map.spawns;
const pBase = { x: pSpawn.x * map.tileSize, y: pSpawn.y * map.tileSize };
const eBase = { x: eSpawn.x * map.tileSize, y: eSpawn.y * map.tileSize };

// player: infantry + militia + tanks + AA on land, a SAM battery, a small air
// wing, gunboat + destroyer offshore
for (let i = 0; i < 3; i++) spawnUnit(world, 'infantry', pBase.x - 70 + i * 22, pBase.y + 50, 'player');
for (let i = 0; i < 3; i++) spawnUnit(world, 'militia', pBase.x - 70 + i * 22, pBase.y + 74, 'player');
for (let i = 0; i < 3; i++) spawnUnit(world, 'tank', pBase.x + i * 26, pBase.y + (i % 2) * 26, 'player');
spawnUnit(world, 'scout', pBase.x + 90, pBase.y + 20, 'player');
for (let i = 0; i < 2; i++) spawnUnit(world, 'aa', pBase.x - 90 + i * 26, pBase.y - 40, 'player');
spawnUnit(world, 'sam', pBase.x - 130, pBase.y - 10, 'player');
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

// enemy: mixed ground force PLUS air (fighter + strike jet) so SAM/AA/fighter targeting is actually exercised.
// eBase sits on a coastal beach tile (Kastavia) with water immediately east/
// south of it — offsets below are chosen (and checked against the map data)
// to land every ground unit on grass/sand/urban, never in the sea; air units
// don't care since their moveClass ignores terrain entirely.
for (let i = 0; i < 3; i++) spawnUnit(world, 'tank', eBase.x - i * 26, eBase.y + (i % 2) * 26, 'enemy');
for (let i = 0; i < 2; i++) spawnUnit(world, 'infantry', eBase.x - 60 - i * 22, eBase.y + 10, 'enemy');
for (let i = 0; i < 2; i++) spawnUnit(world, 'militia', eBase.x - 60 - i * 22, eBase.y + 34, 'enemy');
spawnUnit(world, 'aa', eBase.x + 20, eBase.y - 30, 'enemy');
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
canvas.addEventListener('click', e => {
  if (!buildMode.active || !buildMode.selectedType) return;
  const key = buildMode.selectedType;
  const def = BUILDING_DEFS[key];
  const [wx, wy] = renderer.screenToWorld(e.clientX, e.clientY);
  if (e.shiftKey) {
    const gx = buildMode.previewGx, gy = buildMode.previewGy;
    if (gx === undefined || !isValidPlacement(map, gx, gy, def)) {
      flashMessage(`Can't pin ${def.name} there — invalid site.`);
      return;
    }
    spawnBuilding(world, map, key, gx, gy, 'player');
    flashMessage(`${def.name} pinned.`);
  } else {
    const site = sitePlacement(map, key, wx, wy);
    if (!site) {
      flashMessage(`No buildable site found near there for ${def.name}.`);
      return;
    }
    spawnBuilding(world, map, key, site.x, site.y, 'player');
    flashMessage(`${def.name} sited by the planners.`);
  }
});

addEventListener('keydown', e => {
  if (e.repeat) return;
  if (e.key === 'r' || e.key === 'R') {
    if (buildMode.active) exitBuildMode();
    if (roadMode.active) exitRoadMode(); else enterRoadMode();
  } else if (e.key === 'b' || e.key === 'B') {
    if (roadMode.active) exitRoadMode();
    if (buildMode.active) exitBuildMode(); else enterBuildMode();
  } else if (e.key === 'Escape') {
    if (roadMode.active) exitRoadMode();
    if (buildMode.active) exitBuildMode();
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
  ctx.save();
  ctx.fillStyle = b.side === 'player' ? 'rgba(70,120,170,0.92)' : 'rgba(150,60,60,0.92)';
  ctx.strokeStyle = b.side === 'player' ? '#5fd0ff' : '#ff5a5a';
  ctx.lineWidth = 1.5;
  ctx.fillRect(x0, y0, x1 - x0, y1 - y0);
  ctx.strokeRect(x0, y0, x1 - x0, y1 - y0);
  ctx.restore();
  if (b.hp < b.maxHp) {
    const w = x1 - x0;
    ctx.fillStyle = 'rgba(0,0,0,.5)'; ctx.fillRect(x0, y0 - 8, w, 3);
    ctx.fillStyle = '#6dffb0'; ctx.fillRect(x0, y0 - 8, w * (b.hp / b.maxHp), 3);
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
  hud.textContent = `${map.name} — player ${alive.player} vs enemy ${alive.enemy} — right-click: move order — drag: pan — wheel: zoom — ${roadHint} — ${buildHint}${flash}`;

  requestAnimationFrame(loop);
}
requestAnimationFrame(loop);

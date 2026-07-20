import { loadMap } from './engine/tilemap.js';
import { createRenderer, attachCameraControls } from './engine/renderer.js';
import { spawnUnit, updateUnits, updateProjectiles, clearClaims, UNIT_DEFS, MOVE_CLASSES, WEAPON_DEFS, terrainSample } from './game/units.js';
import { createStatCard } from './game/statcard.js';
import { computeFog, detects, fogState, drawFogOverlay } from './game/fog.js';
import { findRoadRoute, findPath, findPathCached, pathfindStats } from './game/pathfind.js';

const canvas = document.getElementById('view');
const hud = document.getElementById('hud');
const card = createStatCard(document.getElementById('card'));

const map = await loadMap('maps/theater-01.json');
const renderer = createRenderer(canvas);
renderer.cam.x = map.worldW() / 2;
renderer.cam.y = map.worldH() / 2;
// frame the whole map on load (fit width AND height, with a little breathing
// room) so the whole island is visible instead of an arbitrary fixed zoom
renderer.cam.zoom = Math.max(0.08, Math.min(2,
  Math.min(canvas.clientWidth / map.worldW(), canvas.clientHeight / map.worldH()) * 0.9));

const world = { units: [], projectiles: [], hits: [] };

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

addEventListener('keydown', e => {
  if (e.repeat) return;
  if (e.key === 'r' || e.key === 'R') {
    if (roadMode.active) exitRoadMode(); else enterRoadMode();
  } else if (e.key === 'Escape' && roadMode.active) {
    exitRoadMode();
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
  updateProjectiles(world, dt);
  for (const h of world.hits) h.life -= dt;
  world.hits = world.hits.filter(h => h.life > 0);

  renderer.frame(map, (ctx, worldToScreen, cam) => {
    drawFogOverlay(ctx, worldToScreen, 'player', world, map);
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
  });

  card.show(hovered, lastMouse.x, lastMouse.y);
  const alive = { player: 0, enemy: 0 };
  for (const u of world.units) alive[u.side]++;
  const roadHint = roadMode.active
    ? (roadMode.a ? 'road mode: click point B (Esc/right-click to cancel)' : 'road mode: click point A (Esc/right-click to exit)')
    : 'R: build road';
  hud.textContent = `${map.name} — player ${alive.player} vs enemy ${alive.enemy} — right-click: move order — drag: pan — wheel: zoom — ${roadHint}`;

  requestAnimationFrame(loop);
}
requestAnimationFrame(loop);

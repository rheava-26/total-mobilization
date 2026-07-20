import { loadMap } from './engine/tilemap.js';
import { createRenderer, attachCameraControls } from './engine/renderer.js';
import { spawnUnit, updateUnits, updateProjectiles, clearClaims, UNIT_DEFS, MOVE_CLASSES, WEAPON_DEFS } from './game/units.js';
import { createStatCard } from './game/statcard.js';

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

let hovered = null, lastMouse = { x: 0, y: 0 };
canvas.addEventListener('mousemove', e => {
  lastMouse = { x: e.clientX, y: e.clientY };
  const [wx, wy] = renderer.screenToWorld(e.clientX, e.clientY);
  let best = null, bd = 20 * 20;
  for (const u of world.units) {
    const dd = (u.x - wx) ** 2 + (u.y - wy) ** 2;
    if (dd < bd) { bd = dd; best = u; }
  }
  hovered = best;
});
attachCameraControls(canvas, renderer.cam, { isEntityHit: () => !!hovered });

// right-click issues a move order to the player's units (basic input, proves
// the `order` field). Routed by move-class attribute, not per-unit code:
// ground and air units take the raw click point (ground wall-slides off
// impassable terrain on its own; air ignores terrain entirely); naval units
// only ever accept a water point, so their order gets snapped to the
// nearest water tile — ordering a gunboat onto a beach just parks it
// offshore of where you clicked instead of doing nothing.
canvas.addEventListener('contextmenu', e => {
  e.preventDefault();
  const [wx, wy] = renderer.screenToWorld(e.clientX, e.clientY);
  for (const u of world.units) {
    if (u.side !== 'player') continue;
    const jx = wx + (Math.random() - 0.5) * 30, jy = wy + (Math.random() - 0.5) * 30;
    u.order = MOVE_CLASSES[u.def.moveClass].requiresWater ? nearestWaterPoint(jx, jy) : { x: jx, y: jy };
  }
});

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
window.__debug = { world, renderer, map, spawnUnit, UNIT_DEFS, MOVE_CLASSES, WEAPON_DEFS, nearestWaterPoint };

let last = performance.now();
function loop(now) {
  const dt = Math.min(0.05, (now - last) / 1000);
  last = now;

  clearClaims();
  updateUnits(world, dt, map);
  updateProjectiles(world, dt);
  for (const h of world.hits) h.life -= dt;
  world.hits = world.hits.filter(h => h.life > 0);

  renderer.frame(map, (ctx, worldToScreen, cam) => {
    for (const p of world.projectiles) drawProjectile(ctx, worldToScreen, cam, p);
    for (const u of world.units) drawUnit(ctx, worldToScreen, cam, u);
    for (const h of world.hits) {
      const [sx, sy] = worldToScreen(h.x, h.y);
      ctx.strokeStyle = `rgba(255,200,120,${h.life * 4})`;
      ctx.lineWidth = 2;
      ctx.beginPath(); ctx.arc(sx, sy, (0.25 - h.life) * 60, 0, 6.28); ctx.stroke();
    }
  });

  card.show(hovered, lastMouse.x, lastMouse.y);
  const alive = { player: 0, enemy: 0 };
  for (const u of world.units) alive[u.side]++;
  hud.textContent = `${map.name} — player ${alive.player} vs enemy ${alive.enemy} — right-click: move order — drag: pan — wheel: zoom`;

  requestAnimationFrame(loop);
}
requestAnimationFrame(loop);

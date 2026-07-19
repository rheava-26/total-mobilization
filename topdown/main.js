import { loadMap } from './engine/tilemap.js';
import { createRenderer, attachCameraControls } from './engine/renderer.js';
import { spawnUnit, updateUnits, updateProjectiles, clearClaims, UNIT_DEFS } from './game/units.js';
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

// spawn a small demo force on each side, near the map's authored spawn points,
// to prove: physical movement, projectile flight, fire discipline, and the stat card.
const [pSpawn, eSpawn] = map.spawns;
for (let i = 0; i < 6; i++) spawnUnit(world, 'tank', pSpawn.x * map.tileSize + i * 24, pSpawn.y * map.tileSize + (i % 2) * 24, 'player');
for (let i = 0; i < 2; i++) spawnUnit(world, 'fighter', pSpawn.x * map.tileSize - 40, pSpawn.y * map.tileSize - 60 + i * 30, 'player');
for (let i = 0; i < 5; i++) spawnUnit(world, 'tank', eSpawn.x * map.tileSize - i * 24, eSpawn.y * map.tileSize + (i % 2) * 24, 'enemy');

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

// right-click issues a move order to the player's units (basic input, proves the `order` field)
canvas.addEventListener('contextmenu', e => {
  e.preventDefault();
  const [wx, wy] = renderer.screenToWorld(e.clientX, e.clientY);
  for (const u of world.units) if (u.side === 'player') u.order = { x: wx + (Math.random() - 0.5) * 30, y: wy + (Math.random() - 0.5) * 30 };
});

function drawUnit(ctx, worldToScreen, cam, u) {
  const [sx, sy] = worldToScreen(u.x, u.y);
  const r = u.def.radius * cam.zoom * devicePixelRatio;
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
  const y = p.kind === 'shell' ? p.y - (p.lofted || 0) : p.y;
  const [sx, sy] = worldToScreen(p.x, y);
  ctx.fillStyle = p.side === 'player' ? '#fff0a0' : '#ffb27a';
  ctx.beginPath();
  ctx.arc(sx, sy, Math.max(2, 3 * cam.zoom), 0, 6.28);
  ctx.fill();
}

window.__debug = { world, renderer, map }; // headless test hook — safe to leave, no gameplay effect

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

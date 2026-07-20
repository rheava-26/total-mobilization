// Basic fog of war (P1.5). CONCEPT.md Pillar 3: "you see your territory, your
// sensor ranges, your patrols; beyond that, intelligence estimates" — this
// file builds the SENSOR layer only (raw detected/not-detected per side).
// Intelligence estimates (remembered contacts, confidence, etc.) are a later
// phase; this is the literal "can you currently see it" substrate they'd sit
// on top of.
//
// Rule: side S detects enemy unit T if some living unit U on side S has
// dist(U, T) <= U.vision * (1 - concealment(T's tile)). Air targets carry no
// terrain concealment (they're in the sky, not the trees). Every side runs
// the identical rule against every other side — there is no special-casing
// of "the player" in the detection math itself, only in what gets rendered.

export const fogState = { revealAll: false };

// side -> Set<unit>, the enemies (any unit not on that side) that side
// currently detects. Rebuilt every call to computeFog.
let detectedBySide = new Map();

function targetConcealment(map, target) {
  if (!map || target.def.domain === 'air') return 0;
  const t = map.terrainAtWorld(target.x, target.y);
  return t ? (t.concealment || 0) : 0;
}

// O(sides * targets * viewers) — trivial at demo scale (~30 units). Call once
// per frame, before targeting (pickTarget/nearestEnemy) and before rendering
// both read the result via detects().
export function computeFog(world, map) {
  detectedBySide = new Map();
  const sides = new Set();
  for (const u of world.units) if (u.hp > 0) sides.add(u.side);

  for (const side of sides) {
    const detected = new Set();
    const viewers = world.units.filter(u => u.side === side && u.hp > 0);
    const targets = world.units.filter(u => u.side !== side && u.hp > 0);
    for (const target of targets) {
      const concealment = targetConcealment(map, target);
      for (const viewer of viewers) {
        const visionRange = (viewer.def.vision || 0) * (1 - concealment);
        const dd = (target.x - viewer.x) ** 2 + (target.y - viewer.y) ** 2;
        if (dd <= visionRange * visionRange) { detected.add(target); break; }
      }
    }
    detectedBySide.set(side, detected);
  }
}

// Does `side` currently detect `unit`? Own-side units are always "detected"
// by convention (nobody calls this for friendlies, but return true rather
// than false so a careless call doesn't accidentally hide your own units).
export function detects(side, unit) {
  if (fogState.revealAll) return true;
  if (unit.side === side) return true;
  const set = detectedBySide.get(side);
  return !!set && set.has(unit);
}

// ---------------------------------------------------------------------------
// RENDERING: a low-res offscreen "fog" canvas covering the whole map, redrawn
// every frame (cheap because it's tiny — a coarse cell per couple of tiles,
// not per-pixel), then blitted over the terrain scaled up to the map's full
// world footprint through the camera transform (same drawImage trick the
// terrain pre-render uses). Vision circles are punched out with
// destination-out radial gradients for a soft edge; the browser's own
// bilinear upscaling softens it further for free.
let fogCanvas = null, fogCtx = null, fogMeta = null;

function ensureFogCanvas(map) {
  if (fogMeta && fogMeta.map === map) return;
  const cellPx = map.tileSize * 2; // coarser than the terrain grid — cheap + already soft
  const w = Math.max(1, Math.ceil(map.worldW() / cellPx));
  const h = Math.max(1, Math.ceil(map.worldH() / cellPx));
  fogCanvas = document.createElement('canvas');
  fogCanvas.width = w;
  fogCanvas.height = h;
  fogCtx = fogCanvas.getContext('2d');
  fogMeta = { map, cellPx, w, h };
}

// Draws dimming for everything the given side does NOT currently detect.
// Call from inside the per-frame draw callback, after terrain and before
// units, so units always render at full brightness and the dimming only
// affects the background.
export function drawFogOverlay(ctx, worldToScreen, side, world, map) {
  if (fogState.revealAll) return;
  ensureFogCanvas(map);
  const { cellPx, w, h } = fogMeta;

  fogCtx.clearRect(0, 0, w, h);   // the canvas persists across frames — without this the 0.6-alpha dim re-composites over itself every frame and asymptotes to a full blackout within a second
  fogCtx.globalCompositeOperation = 'source-over';
  fogCtx.fillStyle = 'rgba(4,8,14,0.6)';
  fogCtx.fillRect(0, 0, w, h);
  fogCtx.globalCompositeOperation = 'destination-out';
  for (const u of world.units) {
    if (u.side !== side || u.hp <= 0) continue;
    const cx = u.x / cellPx, cy = u.y / cellPx;
    const r = Math.max(0.5, (u.def.vision || 0) / cellPx);
    const grad = fogCtx.createRadialGradient(cx, cy, 0, cx, cy, r);
    grad.addColorStop(0, 'rgba(0,0,0,1)');
    grad.addColorStop(0.75, 'rgba(0,0,0,0.85)');
    grad.addColorStop(1, 'rgba(0,0,0,0)');
    fogCtx.fillStyle = grad;
    fogCtx.beginPath();
    fogCtx.arc(cx, cy, r, 0, Math.PI * 2);
    fogCtx.fill();
  }
  fogCtx.globalCompositeOperation = 'source-over';

  const [sx0, sy0] = worldToScreen(0, 0);
  const [sx1, sy1] = worldToScreen(map.worldW(), map.worldH());
  ctx.drawImage(fogCanvas, 0, 0, w, h, sx0, sy0, sx1 - sx0, sy1 - sy0);
}

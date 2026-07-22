// AMBIENT LIFE — direct answer to real playtest feedback: "the game is too
// still — things should be moving around on the ground level while you
// build stuff up and prepare." This is a PURELY-VISUAL layer, deliberately
// isolated from everything the game actually simulates: it reads map/world
// state READ-ONLY (road tiles, building positions, city/town layout) and
// never writes back to any of it — no effect on units, economy, combat,
// pathfinding, or fog. Delete this file and every import of it, and the
// game plays byte-identical; the world just looks frozen again.
//
// Three cheap systems, every one capped and mostly precomputed once at map
// load so per-frame cost stays flat regardless of map size:
//   - ROAD TRAFFIC: small dots that travel the actual road network
//     (map.roadAt) — the biggest "alive" win, and it reinforces the
//     supply-chain fantasy (CONCEPT.md Pillar 3) for free. Vehicles hop
//     tile-to-tile along the same 8-directional road adjacency the
//     renderer's own road-stroke pass already walks (engine/renderer.js
//     prerenderRoads), so traffic only ever travels where a road is
//     actually painted.
//   - FACTORY SMOKE: a capped, recycled particle pool drifting up off
//     complete player production buildings — makes the prep-phase build-up
//     read as active industry, not a diorama.
//   - COASTAL SHIMMER + CITY LIGHT FLICKER + a capital flag: precomputed
//     point sets whose entire per-frame cost is a Math.sin() alpha wobble —
//     zero per-frame allocation, nothing ever grows.
//
// Everything here is capped (see the MAX_* constants) specifically so a
// pathological map (huge road network, dozens of factories) can't turn this
// into a frame-time problem — worst case is still "a few dozen small canvas
// draws," the same order of magnitude as the unit/building draw loop this
// sits next to in main.js's loop().

import { categoryForFacility } from './production.js';

const MAX_VEHICLES = 26;
const MAX_SMOKE = 90;
const SMOKE_PER_SOURCE_CAP = 7; // don't let one factory alone fill the whole shared pool
const MAX_SMOKE_SOURCES_PER_TICK = 14; // plenty for any realistic base; caps the scan cost too
const MAX_SHIMMER = 70;
const MAX_FLICKER = 70;

// deterministic hash — mirrors engine/renderer.js's own hash2 exactly (kept
// as a local copy rather than an import so this file has zero dependency on
// renderer internals). Used only to pick stable-looking seed positions/
// phases so a reload doesn't visibly reshuffle the whole layer for no
// reason; the motion itself still animates via wall-clock time regardless.
function hash2(x, y, seed) {
  let h = (x * 374761393 + y * 668265263 + seed * 2147483647) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  h = (h ^ (h >>> 16)) >>> 0;
  return h / 4294967296;
}

// 8-directional road-tile neighbors — same adjacency the road-stroke
// painter considers connected, so traffic never "drives" somewhere the
// painted road doesn't actually go.
const DIRS8 = [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [1, -1], [-1, 1], [-1, -1]];
function roadNeighbors(map, gx, gy) {
  const out = [];
  for (const [dx, dy] of DIRS8) {
    const nx = gx + dx, ny = gy + dy;
    if (map.roadAt(nx, ny) > 0) out.push([nx, ny]);
  }
  return out;
}

// One-time scan of the whole road overlay to seed vehicles onto real road
// tiles. O(width*height) — same cost class as the renderer's own one-time
// prerender pass, and this only runs once per map load, never per frame.
function buildVehicles(map) {
  const roadTiles = [];
  for (let gy = 0; gy < map.height; gy++) {
    for (let gx = 0; gx < map.width; gx++) {
      if (map.roadAt(gx, gy) > 0) roadTiles.push([gx, gy]);
    }
  }
  if (!roadTiles.length) return [];
  // scale with network size (a tiny authored map shouldn't get 26 trucks
  // jammed onto a handful of tiles) but always cap at MAX_VEHICLES. A
  // theater-scale generated map (game/mapgen.js) typically lays ~150-350
  // road tiles across its 5-city MST + town spurs — dividing by 15 lands
  // that in the ~10-23 range, which is the difference between "a road
  // network" and "a couple of lonely dots," while a tiny/sparse authored
  // map still only gets a handful.
  const count = Math.min(MAX_VEHICLES, Math.max(6, Math.floor(roadTiles.length / 15)));
  const vehicles = [];
  for (let i = 0; i < count; i++) {
    const [gx, gy] = roadTiles[Math.floor(hash2(i, 7, 501) * roadTiles.length) % roadTiles.length];
    const nbrs = roadNeighbors(map, gx, gy);
    if (!nbrs.length) continue; // an isolated single-tile road stub — nothing to travel along, skip
    const [ngx, ngy] = nbrs[Math.floor(hash2(i, 11, 502) * nbrs.length) % nbrs.length];
    vehicles.push({
      gx, gy, ngx, ngy, prevgx: gx, prevgy: gy, t: hash2(i, 3, 503),
      speed: 0.35 + hash2(i, 5, 504) * 0.45, // tiles/sec — deliberately slow background scenery, not a race
      truck: hash2(i, 13, 505) < 0.4,
      wx: (gx + 0.5) * map.tileSize, wy: (gy + 0.5) * map.tileSize,
    });
  }
  return vehicles;
}

// Coastal shimmer sample points: any water tile with a land neighbor.
// Stride scales with map area so the one-time scan cost stays flat instead
// of growing with map size, and the point count is capped regardless.
function sampleShimmerPoints(map) {
  const pts = [];
  const ts = map.tileSize;
  const stride = Math.max(1, Math.floor(Math.sqrt((map.width * map.height) / 6000)));
  for (let gy = 1; gy < map.height - 1; gy += stride) {
    for (let gx = 1; gx < map.width - 1; gx += stride) {
      const t = map.terrainAt(gx, gy);
      if (!t || !t.water) continue;
      const n1 = map.terrainAt(gx + 1, gy), n2 = map.terrainAt(gx - 1, gy);
      const n3 = map.terrainAt(gx, gy + 1), n4 = map.terrainAt(gx, gy - 1);
      const landAdjacent = (n1 && !n1.water) || (n2 && !n2.water) || (n3 && !n3.water) || (n4 && !n4.water);
      if (!landAdjacent) continue;
      pts.push({
        x: (gx + 0.5) * ts, y: (gy + 0.5) * ts,
        phase: hash2(gx, gy, 601) * 6.283, speed: 0.6 + hash2(gx, gy, 602) * 0.5,
      });
      if (pts.length >= MAX_SHIMMER) return pts;
    }
  }
  return pts;
}

// City/town light-flicker sample points, scattered inside each settlement's
// core (cities AND small towns both read as "inhabited" this way) — capped
// total and split evenly per settlement so a map with lots of small towns
// doesn't starve the 5 real cities of their share.
function sampleFlickerPoints(map) {
  const pts = [];
  const ts = map.tileSize;
  const settlements = (map.cities || []).concat(map.towns || []);
  if (!settlements.length) return pts;
  const perSettlement = Math.max(2, Math.floor(MAX_FLICKER / settlements.length));
  for (const c of settlements) {
    for (let i = 0; i < perSettlement; i++) {
      const ang = hash2(Math.floor(c.x), Math.floor(c.y), 700 + i) * Math.PI * 2;
      const dist = hash2(Math.floor(c.x), Math.floor(c.y), 750 + i) * (c.r || 4) * 0.75;
      pts.push({
        x: (c.x + 0.5) * ts + Math.cos(ang) * dist * ts,
        y: (c.y + 0.5) * ts + Math.sin(ang) * dist * ts,
        phase: hash2(Math.floor(c.x), Math.floor(c.y), 800 + i) * 6.283,
        speed: 1.2 + hash2(Math.floor(c.x), Math.floor(c.y), 850 + i) * 1.6,
      });
      if (pts.length >= MAX_FLICKER) return pts;
    }
  }
  return pts;
}

// Which building keys plausibly puff smoke: the base civilian/arming
// `factory` plus every dedicated production facility (game/production.js's
// FACILITY_TO_CATEGORY, via categoryForFacility). Read-only lookup — never
// mutates production state, just decides who gets a chimney.
function isSmokeSource(b) {
  return b.side === 'player' && b.status === 'complete' && (b.key === 'factory' || !!categoryForFacility(b.key));
}

// Called ONCE per map load (main.js, right after `map` exists) — everything
// expensive here is a one-time scan, never repeated per frame.
export function initAmbient(map) {
  const capital = (map.cities || [])[0];
  return {
    vehicles: buildVehicles(map),
    shimmer: sampleShimmerPoints(map),
    flicker: sampleFlickerPoints(map),
    smoke: [], // capped, recycled particle pool — grown/shrunk in updateAmbient
    smokeTimer: 0,
    turnSeed: 0, // monotonic counter feeding hash2 at junction turns, so picks vary without needing Math.random or wall-clock time
    flagPole: capital ? { x: (capital.x + 0.5) * map.tileSize, y: (capital.y - (capital.r || 6) - 1) * map.tileSize } : null,
  };
}

// Called every frame from main.js's loop(), regardless of phase (PREP or
// COMBAT — the world should feel alive in both, per the task). Cheap: a
// bounded vehicle array, a capped particle pool, no per-frame allocation of
// anything that isn't immediately capped.
export function updateAmbient(ambient, world, map, dt) {
  const ts = map.tileSize;

  // --- road traffic: advance progress along the current tile-to-tile hop,
  // and at each arrival pick the next hop (biased away from an immediate
  // U-turn so it reads as "going somewhere," falling through to reversing
  // only at a genuine dead end) ---
  for (const v of ambient.vehicles) {
    v.t += v.speed * dt;
    if (v.t >= 1) {
      v.t -= 1;
      v.gx = v.ngx; v.gy = v.ngy;
      const nbrs = roadNeighbors(map, v.gx, v.gy);
      const forward = nbrs.filter(([nx, ny]) => !(nx === v.prevgx && ny === v.prevgy));
      const pool = forward.length ? forward : nbrs;
      if (pool.length) {
        ambient.turnSeed++;
        const pick = pool[Math.floor(hash2(v.gx, v.gy, ambient.turnSeed) * pool.length) % pool.length];
        v.prevgx = v.gx; v.prevgy = v.gy;
        v.ngx = pick[0]; v.ngy = pick[1];
      }
      // no neighbors at all (shouldn't happen — we arrived FROM one): just
      // sit this tick out, next frame will retry the same junction
    }
    const gx = v.gx + (v.ngx - v.gx) * v.t;
    const gy = v.gy + (v.ngy - v.gy) * v.t;
    v.wx = (gx + 0.5) * ts;
    v.wy = (gy + 0.5) * ts;
  }

  // --- factory smoke: trickle-spawn from complete player production
  // buildings. Re-scanning world.buildings is cheap — it's already a small
  // array the real game loop walks multiple times every frame elsewhere. ---
  ambient.smokeTimer += dt;
  if (ambient.smokeTimer > 0.12) {
    ambient.smokeTimer = 0;
    let sourcesSeen = 0;
    for (const b of world.buildings) {
      if (!isSmokeSource(b)) continue;
      if (++sourcesSeen > MAX_SMOKE_SOURCES_PER_TICK) break;
      if (ambient.smoke.length >= MAX_SMOKE) break;
      let already = 0;
      for (const p of ambient.smoke) if (p.srcId === b.id) already++;
      if (already >= SMOKE_PER_SOURCE_CAP) continue;
      ambient.smoke.push({
        srcId: b.id,
        x: b.x + (Math.random() - 0.5) * ts * 0.6,
        y: b.y - ts * 0.4,
        vy: 6 + Math.random() * 5,
        drift: (Math.random() - 0.5) * 5,
        life: 0, maxLife: 2.2 + Math.random() * 1.4,
        size: ts * (0.22 + Math.random() * 0.16),
      });
    }
  }
  for (let i = ambient.smoke.length - 1; i >= 0; i--) {
    const p = ambient.smoke[i];
    p.life += dt;
    if (p.life >= p.maxLife) { ambient.smoke.splice(i, 1); continue; }
    p.y -= p.vy * dt;
    p.x += p.drift * dt;
    p.size += dt * 3;
  }
}

// GROUND LAYER draw: shimmer + flicker + traffic + the capital's flag.
// Called from inside main.js's renderer.frame() drawWorld callback, EARLY
// (right after the fog overlay, before buildings/units) so it reads as part
// of the ground rather than floating on top of the units it sits next to.
export function drawAmbientGround(ambient, ctx, worldToScreen, cam) {
  const dpr = devicePixelRatio;
  const now = performance.now() / 1000;

  ctx.save();
  for (const p of ambient.shimmer) {
    const r = 2.4 * cam.zoom * dpr;
    if (r < 0.6) continue; // sub-pixel at this zoom — skip the draw call entirely rather than paint nothing
    const a = 0.10 + 0.16 * (0.5 + 0.5 * Math.sin(now * p.speed + p.phase));
    const [sx, sy] = worldToScreen(p.x, p.y);
    ctx.fillStyle = `rgba(255,255,255,${a.toFixed(3)})`;
    ctx.beginPath(); ctx.arc(sx, sy, r, 0, 6.283); ctx.fill();
  }
  for (const p of ambient.flicker) {
    const r = 1.6 * cam.zoom * dpr;
    if (r < 0.5) continue;
    const a = 0.12 + 0.22 * (0.5 + 0.5 * Math.sin(now * p.speed + p.phase));
    const [sx, sy] = worldToScreen(p.x, p.y);
    ctx.fillStyle = `rgba(255,224,150,${a.toFixed(3)})`;
    ctx.beginPath(); ctx.arc(sx, sy, r, 0, 6.283); ctx.fill();
  }
  ctx.restore();

  ctx.save();
  for (const v of ambient.vehicles) {
    const size = (v.truck ? 3.4 : 2.4) * cam.zoom * dpr;
    if (size < 0.8) continue;
    const [sx, sy] = worldToScreen(v.wx, v.wy);
    const ang = Math.atan2(v.ngy - v.gy, v.ngx - v.gx);
    ctx.save();
    ctx.translate(sx, sy);
    ctx.rotate(ang);
    ctx.fillStyle = v.truck ? 'rgba(118,138,150,0.88)' : 'rgba(205,184,132,0.88)';
    ctx.beginPath();
    ctx.ellipse(0, 0, size, size * 0.6, 0, 0, 6.283);
    ctx.fill();
    ctx.restore();
  }
  ctx.restore();

  // capital flag — one cheap wavy polygon, skipped entirely once too small
  // to read (matches the shimmer/flicker "don't bother drawing sub-pixel
  // detail" convention above)
  if (ambient.flagPole) {
    const poleH = 22 * cam.zoom * dpr;
    if (poleH >= 6) {
      const [sx, sy] = worldToScreen(ambient.flagPole.x, ambient.flagPole.y);
      ctx.save();
      ctx.strokeStyle = 'rgba(40,34,26,0.85)';
      ctx.lineWidth = Math.max(1, 1.4 * cam.zoom * dpr);
      ctx.beginPath(); ctx.moveTo(sx, sy); ctx.lineTo(sx, sy - poleH); ctx.stroke();

      const flagW = poleH * 0.75, flagH = poleH * 0.42, segs = 5;
      ctx.beginPath();
      for (let i = 0; i <= segs; i++) {
        const t = i / segs;
        const wave = Math.sin(now * 3.2 + t * 3) * flagH * 0.22;
        const x = sx + flagW * t, y = sy - poleH + wave - flagH * 0.5;
        if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
      }
      for (let i = segs; i >= 0; i--) {
        const t = i / segs;
        const wave = Math.sin(now * 3.2 + t * 3) * flagH * 0.22;
        ctx.lineTo(sx + flagW * t, sy - poleH + wave + flagH * 0.5);
      }
      ctx.closePath();
      ctx.fillStyle = 'rgba(206,58,50,0.88)';
      ctx.fill();
      ctx.restore();
    }
  }
}

// SMOKE LAYER draw: called separately, AFTER buildings are drawn (see
// main.js), so plumes visibly rise above rooftops instead of being painted
// under them.
export function drawAmbientSmoke(ambient, ctx, worldToScreen, cam) {
  const dpr = devicePixelRatio;
  ctx.save();
  for (const p of ambient.smoke) {
    const frac = p.life / p.maxLife;
    const a = (1 - frac) * 0.28;
    if (a <= 0.01) continue;
    const r = p.size * cam.zoom * dpr;
    if (r < 0.8) continue;
    const [sx, sy] = worldToScreen(p.x, p.y);
    const grad = ctx.createRadialGradient(sx, sy, 0, sx, sy, r);
    grad.addColorStop(0, `rgba(210,210,210,${a.toFixed(3)})`);
    grad.addColorStop(1, 'rgba(210,210,210,0)');
    ctx.fillStyle = grad;
    ctx.beginPath(); ctx.arc(sx, sy, r, 0, 6.283); ctx.fill();
  }
  ctx.restore();
}

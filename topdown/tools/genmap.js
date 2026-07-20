#!/usr/bin/env node
// Deterministic generator for topdown/maps/theater-01.json — the authored
// theater map for the canonical playthrough (see docs/CONCEPT.md). Run with
// `node genmap.js` from anywhere; it always writes ../maps/theater-01.json
// relative to this file. Re-run any time to regenerate after tweaking the
// parameters below (same SEED -> byte-identical output).
//
// Approach: a fixed-seed value-noise heightfield masked into an island
// shape, classified into terrain bands (sea/beach/plains/forest/hills/
// mountains), a steepest-descent river traced from high ground to the sea,
// marsh stamped near the river mouth, five named urban blots (biggest at
// the coast: Syrograd), and named rectangular regions covering the island.
// Everything is derived from SEED — no hand-typed rows.

const fs = require('fs');
const path = require('path');

const SEED = 1337;
const WIDTH = 220;
const HEIGHT = 150;
const TILE_SIZE = 32;

// ---------------------------------------------------------------- rng ----
// mulberry32: tiny, fast, deterministic PRNG. Good enough for terrain art.
function mulberry32(seed) {
  let a = seed >>> 0;
  return function rng() {
    a |= 0; a = (a + 0x6D2B79F5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// Deterministic hash of integer coords -> [0,1), independent of generation
// order (unlike the rng stream) so it can be resampled anywhere.
function hash2(x, y, seed) {
  let h = (x * 374761393 + y * 668265263 + seed * 2147483647) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  h = (h ^ (h >>> 16)) >>> 0;
  return h / 4294967296;
}

// --------------------------------------------------------- value noise ----
// Lattice value noise with smoothstep interpolation over a small random grid,
// WRAPPED (toroidal) so callers can freely pass u,v outside [0,1) — several
// call sites here multiply u,v by a frequency >1 to layer detail, which
// would otherwise walk off the lattice into out-of-bounds reads (NaN).
// Wrapping just makes it tileable; visually harmless at these frequencies.
function makeNoise(seed, cells) {
  cells = Math.max(2, Math.round(cells));
  const g = new Float32Array(cells * cells);
  for (let i = 0; i < g.length; i++) g[i] = hash2(i, seed, seed * 7 + 3);
  const at = (xi, yi) => g[(((yi % cells) + cells) % cells) * cells + (((xi % cells) + cells) % cells)];
  return function sample(u, v) {
    const fx = u * cells, fy = v * cells;
    const x0 = Math.floor(fx), y0 = Math.floor(fy);
    let tx = fx - x0, ty = fy - y0;
    tx = tx * tx * (3 - 2 * tx); // smoothstep
    ty = ty * ty * (3 - 2 * ty);
    const v00 = at(x0, y0), v10 = at(x0 + 1, y0);
    const v01 = at(x0, y0 + 1), v11 = at(x0 + 1, y0 + 1);
    const a = v00 + (v10 - v00) * tx, b = v01 + (v11 - v01) * tx;
    return a + (b - a) * ty;
  };
}

// Fractal sum of a few octaves of value noise, normalized to [0,1].
function makeFractal(seed, baseCells, octaves = 4) {
  const layers = [];
  let amp = 1, cells = baseCells, total = 0;
  for (let o = 0; o < octaves; o++) {
    layers.push({ sample: makeNoise(seed + o * 101, Math.round(cells)), amp });
    total += amp;
    amp *= 0.5;
    cells *= 2.15;
  }
  return function sample(u, v) {
    let s = 0;
    for (const l of layers) s += l.sample(u, v) * l.amp;
    return s / total;
  };
}

const rng = mulberry32(SEED);
const elevationNoise = makeFractal(SEED, 5, 5);
const moistureNoise = makeFractal(SEED + 1000, 6, 4);
const angleNoise = makeNoise(SEED + 2000, 10); // coastline irregularity, sampled around a circle

// ---------------------------------------------------------- elevation ----
const cx = WIDTH * 0.48, cy = HEIGHT * 0.54;
const baseRadius = Math.min(WIDTH, HEIGHT) * 0.46;

function islandMask(x, y) {
  const dx = x - cx, dy = y - cy;
  const dist = Math.hypot(dx, dy);
  const angle = Math.atan2(dy, dx); // -PI..PI
  const au = (angle + Math.PI) / (2 * Math.PI); // 0..1 around the circle
  // sample the angle-noise as a 1D ring by walking a small circle in its uv space
  const wobble = angleNoise(0.5 + 0.35 * Math.cos(au * 2 * Math.PI), 0.5 + 0.35 * Math.sin(au * 2 * Math.PI));
  const radius = baseRadius * (0.72 + wobble * 0.6); // irregular coastline: bays + headlands
  const t = 1 - dist / radius;
  return Math.max(0, Math.min(1, t * 2.2)); // steep-ish falloff so the coast reads crisp, not a smear
}

// The mountain range is an explicit spine (polyline), not just "high noise
// everywhere" — that reads as a proper range that channels movement rather
// than a mountainous island interior. Two shallow saddles are carved across
// it so ground forces always have somewhere to funnel through.
const spine = [
  { x: WIDTH * 0.34, y: HEIGHT * 0.18 },
  { x: WIDTH * 0.44, y: HEIGHT * 0.34 },
  { x: WIDTH * 0.50, y: HEIGHT * 0.46 },
  { x: WIDTH * 0.58, y: HEIGHT * 0.58 },
];
const passes = [
  { x: WIDTH * 0.40, y: HEIGHT * 0.29, r: 6 },
  { x: WIDTH * 0.54, y: HEIGHT * 0.52, r: 6 },
];

function distToSegment(x, y, ax, ay, bx, by) {
  const abx = bx - ax, aby = by - ay;
  const len2 = abx * abx + aby * aby;
  let t = len2 > 0 ? ((x - ax) * abx + (y - ay) * aby) / len2 : 0;
  t = Math.max(0, Math.min(1, t));
  return Math.hypot(x - (ax + abx * t), y - (ay + aby * t));
}
function distToSpine(x, y) {
  let best = Infinity;
  for (let i = 0; i < spine.length - 1; i++) {
    const a = spine[i], b = spine[i + 1];
    best = Math.min(best, distToSegment(x, y, a.x, a.y, b.x, b.y));
  }
  return best;
}

function elevationAt(x, y) {
  const u = x / WIDTH, v = y / HEIGHT;
  const mask = islandMask(x, y);
  if (mask <= 0) return 0;
  // gentle rolling base terrain (plains/forest band material)
  let e = mask * (0.26 + elevationNoise(u * 1.6, v * 1.6) * 0.22);
  // mountain spine: a band around the polyline, jittered so its width and
  // crest wobble instead of reading as a ruler-straight wall
  const jag = elevationNoise(u * 3.4 + 9.1, v * 3.4 + 4.4);
  const coreWidth = (WIDTH / 220) * (4 + jag * 4);
  const skirtWidth = coreWidth + (WIDTH / 220) * (7 + jag * 4);
  const d = distToSpine(x, y);
  if (d < coreWidth) e += (1 - d / coreWidth) * 0.62 * mask;
  else if (d < skirtWidth) e += (1 - (d - coreWidth) / (skirtWidth - coreWidth)) * 0.2 * mask;
  for (const p of passes) {
    const pd = Math.hypot(x - p.x, y - p.y);
    if (pd < p.r) e -= (1 - pd / p.r) * 0.55;
  }
  return Math.max(0, Math.min(1.4, e));
}

const SEA_LEVEL = 0.30;
const BEACH_BAND = 0.045;
const HILL_LEVEL = 0.44;
const MOUNTAIN_LEVEL = 0.60;

// ------------------------------------------------------------- fields ----
const elevation = new Float32Array(WIDTH * HEIGHT);
for (let y = 0; y < HEIGHT; y++) for (let x = 0; x < WIDTH; x++) elevation[y * WIDTH + x] = elevationAt(x, y);

function isWater(x, y) {
  if (x < 0 || y < 0 || x >= WIDTH || y >= HEIGHT) return true;
  return elevation[y * WIDTH + x] < SEA_LEVEL;
}

// terrain codes (before urban/river/beach overlays)
const T_GRASS = '.', T_FOREST = 'f', T_HILLS = 'h', T_MOUNTAIN = 'M',
      T_WATER = '~', T_RIVER = 'r', T_MARSH = 'm', T_URBAN = 'u', T_SAND = 's';

const terrain = new Array(WIDTH * HEIGHT);
for (let y = 0; y < HEIGHT; y++) {
  for (let x = 0; x < WIDTH; x++) {
    const e = elevation[y * WIDTH + x];
    const i = y * WIDTH + x;
    if (e < SEA_LEVEL) { terrain[i] = T_WATER; continue; }
    if (e < SEA_LEVEL + BEACH_BAND) { terrain[i] = T_SAND; continue; }
    if (e >= MOUNTAIN_LEVEL) { terrain[i] = T_MOUNTAIN; continue; }
    if (e >= HILL_LEVEL) { terrain[i] = T_HILLS; continue; }
    const m = moistureNoise(x / WIDTH * 2.4, y / HEIGHT * 2.4);
    if (e < SEA_LEVEL + BEACH_BAND + 0.05 && m > 0.62) { terrain[i] = T_MARSH; continue; }
    terrain[i] = m > 0.52 ? T_FOREST : T_GRASS;
  }
}

// -------------------------------------------------------------- river ----
// Steepest-descent walk from a high-elevation source down to the sea,
// carving a 1-2 tile wide channel as it goes.
function traceRiver() {
  let best = null, bestE = -1;
  for (let y = 0; y < HEIGHT; y++) for (let x = 0; x < WIDTH; x++) {
    const e = elevation[y * WIDTH + x];
    if (e > bestE && e < 1.1 && x > WIDTH * 0.25 && x < WIDTH * 0.75 && y > HEIGHT * 0.25 && y < HEIGHT * 0.7) {
      bestE = e; best = { x, y };
    }
  }
  const path = [];
  let { x, y } = best;
  const visited = new Set();
  for (let steps = 0; steps < WIDTH + HEIGHT; steps++) {
    path.push({ x, y });
    visited.add(x + ',' + y);
    if (isWater(x, y)) break;
    let nx = x, ny = y, ne = elevation[y * WIDTH + x];
    for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
      if (dx === 0 && dy === 0) continue;
      const ax = x + dx, ay = y + dy;
      if (ax < 0 || ay < 0 || ax >= WIDTH || ay >= HEIGHT) continue;
      if (visited.has(ax + ',' + ay)) continue;
      const e = elevation[ay * WIDTH + ax];
      if (e < ne) { ne = e; nx = ax; ny = ay; }
    }
    if (nx === x && ny === y) break; // local minimum with no lower unvisited neighbor; stop
    x = nx; y = ny;
  }
  return path;
}

const riverPath = traceRiver();
for (let k = 0; k < riverPath.length; k++) {
  const { x, y } = riverPath[k];
  if (isWater(x, y)) continue;
  terrain[y * WIDTH + x] = T_RIVER;
  // widen slightly in its lower third (closer to the sea) for readability
  if (k > riverPath.length * 0.6) {
    const alt = [[1, 0], [0, 1], [-1, 0], [0, -1]][Math.floor(hash2(x, y, 55) * 4)];
    const ax = x + alt[0], ay = y + alt[1];
    if (ax >= 0 && ay >= 0 && ax < WIDTH && ay < HEIGHT && !isWater(ax, ay)) terrain[ay * WIDTH + ax] = T_RIVER;
  }
}

// extra marsh stamped around the river's final stretch (classic delta/mouth wetland)
for (let k = Math.floor(riverPath.length * 0.75); k < riverPath.length; k++) {
  const { x, y } = riverPath[k];
  for (let dy = -2; dy <= 2; dy++) for (let dx = -2; dx <= 2; dx++) {
    const ax = x + dx, ay = y + dy;
    if (ax < 0 || ay < 0 || ax >= WIDTH || ay >= HEIGHT) continue;
    const i = ay * WIDTH + ax;
    if (terrain[i] === T_GRASS && hash2(ax, ay, 77) > 0.45) terrain[i] = T_MARSH;
  }
}

// ------------------------------------------------------------- cities ----
function distToWater(x, y, maxR = 6) {
  for (let r = 1; r <= maxR; r++) {
    for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) {
      if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
      const ax = x + dx, ay = y + dy;
      if (ax < 0 || ay < 0 || ax >= WIDTH || ay >= HEIGHT) return r;
      if (isWater(ax, ay)) return r;
    }
  }
  return maxR + 1;
}

function buildableAt(x, y) {
  const t = terrain[y * WIDTH + x];
  return t === T_GRASS || t === T_HILLS || t === T_SAND;
}

const candidates = [];
for (let y = 4; y < HEIGHT - 4; y++) for (let x = 4; x < WIDTH - 4; x++) {
  if (buildableAt(x, y)) candidates.push({ x, y, coast: distToWater(x, y) });
}

function farthestFrom(points, pool) {
  let best = null, bestD = -1;
  for (const c of pool) {
    let d = Infinity;
    for (const p of points) d = Math.min(d, (c.x - p.x) ** 2 + (c.y - p.y) ** 2);
    if (d > bestD) { bestD = d; best = c; }
  }
  return best;
}

// Syrograd: the biggest city, on the coast, biased toward the map's south-
// west quadrant so it reads as "a" place rather than dead-center.
const coastalCandidates = candidates.filter(c => c.coast <= 4);
let syrograd = null, syBest = -Infinity;
for (const c of coastalCandidates) {
  const biasX = 1 - Math.abs(c.x / WIDTH - 0.4);
  const biasY = 1 - Math.abs(c.y / HEIGHT - 0.62);
  const score = biasX + biasY - c.coast * 0.05 + hash2(c.x, c.y, 99) * 0.3;
  if (score > syBest) { syBest = score; syrograd = c; }
}

const chosen = [syrograd];
const cityPool = candidates.filter(c => (c.x - syrograd.x) ** 2 + (c.y - syrograd.y) ** 2 > 30 * 30);
while (chosen.length < 5) {
  const next = farthestFrom(chosen, cityPool);
  chosen.push(next);
  const nx = next.x, ny = next.y;
  for (let i = cityPool.length - 1; i >= 0; i--) {
    if ((cityPool[i].x - nx) ** 2 + (cityPool[i].y - ny) ** 2 < 22 * 22) cityPool.splice(i, 1);
  }
}

const cityNames = ['Syrograd', 'Kastavia', 'Vintor Cross', "Brennhold", 'Aduna Port'];
const cities = chosen.map((c, i) => ({
  name: cityNames[i],
  x: c.x, y: c.y,
  r: i === 0 ? 10 : 6 + Math.floor(hash2(c.x, c.y, 21) * 2), // Syrograd is deliberately the biggest
}));

// stamp irregular urban blots at each city
for (const c of cities) {
  const r = c.r;
  for (let dy = -r - 2; dy <= r + 2; dy++) for (let dx = -r - 2; dx <= r + 2; dx++) {
    const x = c.x + dx, y = c.y + dy;
    if (x < 0 || y < 0 || x >= WIDTH || y >= HEIGHT) continue;
    const d = Math.hypot(dx, dy) + (hash2(x, y, 31) - 0.5) * 3.2; // ragged edge, not a perfect disc
    if (d > r) continue;
    const i = y * WIDTH + x;
    if (terrain[i] === T_WATER || terrain[i] === T_RIVER || terrain[i] === T_MOUNTAIN) continue;
    terrain[i] = T_URBAN;
  }
}

// ------------------------------------------------------------- regions ----
// Six broad named regions as bounding rects (rect areas are a fine
// approximation for P1.5 — plot/district polygons are a later phase).
const regions = [
  { name: 'Syrograd Coast', x0: 0, y0: Math.floor(HEIGHT * 0.42), x1: Math.floor(WIDTH * 0.42), y1: HEIGHT },
  { name: 'The Ashfall Range', x0: Math.floor(WIDTH * 0.30), y0: Math.floor(HEIGHT * 0.18), x1: Math.floor(WIDTH * 0.68), y1: Math.floor(HEIGHT * 0.60) },
  { name: 'Kastavia Plains', x0: Math.floor(WIDTH * 0.55), y0: 0, x1: WIDTH, y1: Math.floor(HEIGHT * 0.40) },
  { name: 'Vintor Woods', x0: 0, y0: 0, x1: Math.floor(WIDTH * 0.42), y1: Math.floor(HEIGHT * 0.42) },
  { name: 'The Long Marsh', x0: Math.floor(WIDTH * 0.38), y0: Math.floor(HEIGHT * 0.55), x1: Math.floor(WIDTH * 0.70), y1: HEIGHT },
  { name: 'Aduna Shore', x0: Math.floor(WIDTH * 0.62), y0: Math.floor(HEIGHT * 0.36), x1: WIDTH, y1: HEIGHT },
];

// ------------------------------------------------------------- spawns ----
// Player starts near Syrograd (the canonical playthrough's home city).
// Enemy starts at a beach near a different coastal city, standing in for
// the invasion fleet's landing point (kept on land/sand so the demo's
// ground units are legally placed — the fleet itself is a later phase).
const playerSpawn = { x: cities[0].x + (cities[0].r + 6), y: cities[0].y - (cities[0].r + 6) };
let enemyCity = cities[1];
for (const c of cities.slice(1)) {
  if ((c.x - syrograd.x) ** 2 + (c.y - syrograd.y) ** 2 > (enemyCity.x - syrograd.x) ** 2 + (enemyCity.y - syrograd.y) ** 2) enemyCity = c;
}
function nearestBeach(cx0, cy0) {
  let best = null, bestD = Infinity;
  for (let y = 0; y < HEIGHT; y++) for (let x = 0; x < WIDTH; x++) {
    if (terrain[y * WIDTH + x] !== T_SAND) continue;
    const d = (x - cx0) ** 2 + (y - cy0) ** 2;
    if (d < bestD) { bestD = d; best = { x, y }; }
  }
  return best || { x: cx0, y: cy0 };
}
const enemySpawn = nearestBeach(enemyCity.x, enemyCity.y);

// clamp player spawn onto land if it drifted into water/mountain
function nearestLand(x, y) {
  if (!isWater(x, y) && terrain[y * WIDTH + x] !== T_MOUNTAIN) return { x, y };
  return nearestBeach(x, y);
}
const playerSpawnSafe = nearestLand(
  Math.max(0, Math.min(WIDTH - 1, playerSpawn.x)),
  Math.max(0, Math.min(HEIGHT - 1, playerSpawn.y))
);

const spawns = [
  { side: 'player', x: playerSpawnSafe.x, y: playerSpawnSafe.y },
  { side: 'enemy', x: enemySpawn.x, y: enemySpawn.y },
];

// -------------------------------------------------------------- roads ----
// Starter highway network (P2): connect all five cities with a minimum
// spanning tree over straight-line distance, then ROUTE each MST edge with
// the same A*-style cost logic the in-game road planner uses (game/
// pathfind.js's roadTileCost) — mountains and open sea are hard walls, a
// river crossing is legal-but-costly (a bridge), and tiles that are already
// road are heavily discounted so later edges branch off earlier ones instead
// of laying redundant parallel routes. This is a deliberate duplication of
// pathfind.js's cost function (not an import): genmap.js is a standalone
// Node/CommonJS script with no browser ESM graph to join, same reason
// hash2() above is already re-implemented here instead of imported from
// engine/renderer.js.
//
// Crucially: this section only READS `terrain` (never writes to it) and
// writes results into a brand new `roads` overlay array — regenerating the
// map with this code added does not change a single terrain character.

function terrainMoveMult(ch) {
  // mirrors maps/terrain-defs.json's moveMult per type
  switch (ch) {
    case T_FOREST: return 0.55;
    case T_HILLS: return 0.7;
    case T_URBAN: return 0.85;
    case T_SAND: return 0.8;
    case T_MARSH: return 0.4;
    default: return 1.0; // grass and anything unlisted
  }
}

const roadFlags = new Uint8Array(WIDTH * HEIGHT);
const ROAD_REUSE_COST = 0.12;

function roadTileCost(x, y) {
  const ch = terrain[y * WIDTH + x];
  if (ch === T_MOUNTAIN) return Infinity;
  if (ch === T_WATER) return Infinity; // open sea: unbridgeable
  if (roadFlags[y * WIDTH + x]) return ROAD_REUSE_COST;
  if (ch === T_RIVER) return 4; // bridge: legal but discouraged
  return 1 / Math.max(0.15, terrainMoveMult(ch));
}

// Same binary-heap A* as game/pathfind.js's aStarGrid: 8-directional with
// corner-cut prevention, octile-distance heuristic scaled by a lower-bound
// cost so it stays admissible. Returns the dense per-tile grid path or null.
function roadAStar(sx, sy, gx, gy) {
  const SQRT2 = Math.SQRT2;
  const NEI = [[1, 0, 1], [-1, 0, 1], [0, 1, 1], [0, -1, 1], [1, 1, SQRT2], [1, -1, SQRT2], [-1, 1, SQRT2], [-1, -1, SQRT2]];
  const n = WIDTH * HEIGHT;
  const gScore = new Float64Array(n).fill(Infinity);
  const cameFrom = new Int32Array(n).fill(-1);
  const closed = new Uint8Array(n);
  const startIdx = sy * WIDTH + sx;
  gScore[startIdx] = 0;
  const heap = []; // simple binary heap of [f, idx]
  function push(node, f) {
    heap.push([f, node]);
    let i = heap.length - 1;
    while (i > 0) {
      const p = (i - 1) >> 1;
      if (heap[p][0] <= heap[i][0]) break;
      [heap[p], heap[i]] = [heap[i], heap[p]]; i = p;
    }
  }
  function pop() {
    const top = heap[0], last = heap.pop();
    if (heap.length) {
      heap[0] = last;
      let i = 0;
      for (;;) {
        const l = i * 2 + 1, r = i * 2 + 2; let s = i;
        if (l < heap.length && heap[l][0] < heap[s][0]) s = l;
        if (r < heap.length && heap[r][0] < heap[s][0]) s = r;
        if (s === i) break;
        [heap[s], heap[i]] = [heap[i], heap[s]]; i = s;
      }
    }
    return top[1];
  }
  const h = (x, y) => {
    const dx = Math.abs(x - gx), dy = Math.abs(y - gy);
    const dmin = Math.min(dx, dy), dmax = Math.max(dx, dy);
    return (dmax - dmin + SQRT2 * dmin) * ROAD_REUSE_COST;
  };
  push(startIdx, h(sx, sy));
  while (heap.length) {
    const cur = pop();
    if (closed[cur]) continue;
    closed[cur] = 1;
    const cx = cur % WIDTH, cy = (cur / WIDTH) | 0;
    if (cx === gx && cy === gy) {
      const path = [];
      let n2 = cur;
      while (n2 !== -1) { path.push({ x: n2 % WIDTH, y: (n2 / WIDTH) | 0 }); n2 = cameFrom[n2]; }
      path.reverse();
      return path;
    }
    for (const [dx, dy, dist] of NEI) {
      const nx = cx + dx, ny = cy + dy;
      if (nx < 0 || ny < 0 || nx >= WIDTH || ny >= HEIGHT) continue;
      const nCost = roadTileCost(nx, ny);
      if (!(nCost < Infinity)) continue;
      if (dx !== 0 && dy !== 0) {
        if (!(roadTileCost(cx + dx, cy) < Infinity) || !(roadTileCost(cx, cy + dy) < Infinity)) continue;
      }
      const ni = ny * WIDTH + nx;
      if (closed[ni]) continue;
      const tentative = gScore[cur] + dist * nCost;
      if (tentative < gScore[ni]) {
        gScore[ni] = tentative;
        cameFrom[ni] = cur;
        push(ni, tentative + h(nx, ny));
      }
    }
  }
  return null;
}

// Prim's MST over the 5 cities (straight-line distance — a fine proxy for
// "which pairs to connect"; the actual route between each pair still bends
// around terrain via A* above).
const mstEdges = [];
{
  const inTree = [0];
  const remaining = cities.map((c, i) => i).filter(i => i !== 0);
  while (remaining.length) {
    let best = null, bestD = Infinity, bestI = -1;
    for (let ri = 0; ri < remaining.length; ri++) {
      const j = remaining[ri];
      for (const i of inTree) {
        const d = (cities[i].x - cities[j].x) ** 2 + (cities[i].y - cities[j].y) ** 2;
        if (d < bestD) { bestD = d; best = { a: i, b: j }; bestI = ri; }
      }
    }
    mstEdges.push(best);
    inTree.push(best.b);
    remaining.splice(bestI, 1);
  }
}

let roadTileCount = 0;
for (const edge of mstEdges) {
  const a = cities[edge.a], b = cities[edge.b];
  const routePath = roadAStar(a.x, a.y, b.x, b.y);
  if (!routePath) {
    console.error(`roads: no route found between ${cities[edge.a].name} and ${cities[edge.b].name} — skipping edge`);
    continue;
  }
  for (const { x, y } of routePath) {
    const i = y * WIDTH + x;
    if (!roadFlags[i]) { roadFlags[i] = 1; roadTileCount++; }
  }
}

const roads = [];
for (let y = 0; y < HEIGHT; y++) {
  let row = '';
  for (let x = 0; x < WIDTH; x++) row += roadFlags[y * WIDTH + x] ? 'r' : '.';
  roads.push(row);
}
console.log('roads:', roadTileCount, 'tiles across', mstEdges.length, 'routed legs connecting', cities.length, 'cities');

// --------------------------------------------------------------- write ----
const grid = [];
for (let y = 0; y < HEIGHT; y++) grid.push(terrain.slice(y * WIDTH, (y + 1) * WIDTH).join(''));

const mapData = {
  name: 'Theater 01 — The Isle of Syrograd',
  tileSize: TILE_SIZE,
  width: WIDTH,
  height: HEIGHT,
  terrainDefs: 'terrain-defs.json',
  legend: {
    '.': 'grass', 'f': 'forest', 'h': 'hills', 'M': 'mountain',
    '~': 'water', 'r': 'river', 'm': 'marsh', 'u': 'urban', 's': 'sand',
  },
  grid,
  roads,
  cities,
  regions,
  spawns,
  notes: 'Generated by topdown/tools/genmap.js (seed ' + SEED + '). Re-run that script to regenerate after tweaking its parameters — do not hand-edit the grid or roads.',
};

const outPath = path.join(__dirname, '..', 'maps', 'theater-01.json');
fs.writeFileSync(outPath, JSON.stringify(mapData, null, 0));
console.log('wrote', outPath, WIDTH + 'x' + HEIGHT, 'cities:', cities.map(c => c.name).join(', '));

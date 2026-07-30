// A* pathfinding over the tile grid (P2 — "The Map Is a Machine"). Replaces
// "steer straight at the order point and wall-slide off whatever's in the
// way" with a real route: units path AROUND mountains/marsh instead of
// hugging them, and wheeled units prefer roads because the cost function
// makes roads cheaper, not because of any special-cased "follow the road"
// logic.
//
// Two consumers share the same A* core (`aStarGrid` below) with different
// per-tile cost functions:
//   - unit orders (findPath / findPathCached): cost driven by
//     game/units.js's terrainSample, so a unit's route and its actual
//     movement physics can never diverge — same function, same answer.
//   - the R-mode road planner (findRoadRoute, driven by main.js): cost
//     driven by raw terrain (no move-class bias — infrastructure isn't
//     built for one vehicle type) with a steep discount on tiles that are
//     already road, so new construction naturally branches off the
//     existing network instead of laying parallel routes next to it.
//
// Grid is small (220x150 = 33k nodes for the theater map) so a binary-heap
// A* per order is cheap; see `pathfindStats` below for the numbers actually
// measured on this map.

import { terrainSample } from './units.js';

const SQRT2 = Math.SQRT2;

// ---------------------------------------------------------------------------
// Binary min-heap keyed by f-score. Plain array of node indices; f-scores
// live in a parallel typed array owned by the caller (avoids boxing/GC
// churn on a 33k-node grid).
class MinHeap {
  constructor() { this.items = []; }
  get size() { return this.items.length; }
  push(node, f) {
    const items = this.items;
    items.push([f, node]);
    let i = items.length - 1;
    while (i > 0) {
      const p = (i - 1) >> 1;
      if (items[p][0] <= items[i][0]) break;
      [items[p], items[i]] = [items[i], items[p]];
      i = p;
    }
  }
  pop() {
    const items = this.items;
    const top = items[0];
    const last = items.pop();
    if (items.length) {
      items[0] = last;
      let i = 0;
      for (;;) {
        const l = i * 2 + 1, r = i * 2 + 2;
        let smallest = i;
        if (l < items.length && items[l][0] < items[smallest][0]) smallest = l;
        if (r < items.length && items[r][0] < items[smallest][0]) smallest = r;
        if (smallest === i) break;
        [items[smallest], items[i]] = [items[i], items[smallest]];
        i = smallest;
      }
    }
    return top[1];
  }
}

const NEI = [
  [1, 0, 1], [-1, 0, 1], [0, 1, 1], [0, -1, 1],
  [1, 1, SQRT2], [1, -1, SQRT2], [-1, 1, SQRT2], [-1, -1, SQRT2],
];

// Running stats surfaced to the debug hook / headless tests so mass-order
// hitching can be measured instead of eyeballed.
export const pathfindStats = { solves: [], lastMs: 0 };
function recordSolve(kind, ms, nodes) {
  pathfindStats.lastMs = ms;
  pathfindStats.solves.push({ kind, ms, nodes, t: performance.now() });
  if (pathfindStats.solves.length > 100) pathfindStats.solves.shift();
}

// Core grid A*, 8-directional with corner-cut prevention (a diagonal step is
// only allowed if both orthogonal neighbors it would cut past are also
// enterable — otherwise a unit could path "through" the corner of an
// impassable tile). costFn(gx, gy) returns the cost to ENTER that tile, or
// Infinity if it can't be entered at all. minCost is a lower bound on any
// cost the grid can produce, used to keep the octile-distance heuristic
// admissible (never overestimates -> A* stays optimal).
function aStarGrid(width, height, sx, sy, gx, gy, costFn, minCost) {
  // start tile IS the goal tile: return the {path, expansions} shape both
  // callers (findPath / the road solver) expect — they read result.path and
  // result.expansions after a `!result` guard. Returning a bare array here
  // (the original bug) left result.path undefined -> simplifyPath(undefined)
  // threw. Surfaces routinely now that battalion rout/garrison orders can
  // target the exact tile a unit already stands on.
  if (sx === gx && sy === gy) return { path: [{ x: sx, y: sy }], expansions: 0 };
  const n = width * height;
  const gScore = new Float64Array(n).fill(Infinity);
  const cameFrom = new Int32Array(n).fill(-1);
  const closed = new Uint8Array(n);
  const startIdx = sy * width + sx;
  gScore[startIdx] = 0;
  const heap = new MinHeap();
  const h = (x, y) => {
    const dx = Math.abs(x - gx), dy = Math.abs(y - gy);
    const dmin = Math.min(dx, dy), dmax = Math.max(dx, dy);
    return (dmax - dmin + SQRT2 * dmin) * minCost;
  };
  heap.push(startIdx, h(sx, sy));
  let expansions = 0;
  const maxExpansions = n * 4; // generous safety cap, never hit on a normal grid

  while (heap.size) {
    const cur = heap.pop();
    if (closed[cur]) continue;
    closed[cur] = 1;
    expansions++;
    const cx = cur % width, cy = (cur / width) | 0;
    if (cx === gx && cy === gy) {
      const path = [];
      let n2 = cur;
      while (n2 !== -1) {
        path.push({ x: n2 % width, y: (n2 / width) | 0 });
        n2 = cameFrom[n2];
      }
      path.reverse();
      return { path, expansions };
    }
    if (expansions > maxExpansions) return null;
    for (const [dx, dy, dist] of NEI) {
      const nx = cx + dx, ny = cy + dy;
      if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
      const nCost = costFn(nx, ny);
      if (!(nCost < Infinity)) continue;
      if (dx !== 0 && dy !== 0) {
        // corner-cut guard: both straight neighbors must be enterable
        if (!(costFn(cx + dx, cy) < Infinity) || !(costFn(cx, cy + dy) < Infinity)) continue;
      }
      const ni = ny * width + nx;
      if (closed[ni]) continue;
      const tentative = gScore[cur] + dist * nCost;
      if (tentative < gScore[ni]) {
        gScore[ni] = tentative;
        cameFrom[ni] = cur;
        heap.push(ni, tentative + h(nx, ny));
      }
    }
  }
  return null;
}

// Spiral search outward from (gx,gy) for the nearest tile passing `ok(x,y)`.
// Used to snap clicks/orders that land on an impassable tile (mountain,
// open water, off in the weeds) onto the nearest legal tile instead of
// silently failing to path at all.
export function nearestPassableTile(width, height, gx, gy, ok, maxR = 40) {
  if (gx >= 0 && gy >= 0 && gx < width && gy < height && ok(gx, gy)) return { x: gx, y: gy };
  for (let r = 1; r <= maxR; r++) {
    for (let dy = -r; dy <= r; dy++) {
      for (let dx = -r; dx <= r; dx++) {
        if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
        const x = gx + dx, y = gy + dy;
        if (x < 0 || y < 0 || x >= width || y >= height) continue;
        if (ok(x, y)) return { x, y };
      }
    }
  }
  return null;
}

// Greedy backward line-of-sight simplification ("string pulling"): collapses
// a dense one-tile-per-step path into the turning points only, so the
// existing acceleration/turn-rate steering advances toward a handful of
// waypoints instead of jittering tile-to-tile. `passable(wx, wy)` tests a
// single world point (sampled every half-tile along each candidate segment).
function simplifyPath(denseGridPath, tileSize, passable) {
  if (denseGridPath.length <= 1) return denseGridPath.map(p => ({ x: (p.x + 0.5) * tileSize, y: (p.y + 0.5) * tileSize }));
  const world = denseGridPath.map(p => ({ x: (p.x + 0.5) * tileSize, y: (p.y + 0.5) * tileSize }));
  const segmentClear = (a, b) => {
    const dist = Math.hypot(b.x - a.x, b.y - a.y);
    const steps = Math.max(1, Math.ceil(dist / (tileSize * 0.5)));
    for (let s = 1; s <= steps; s++) {
      const t = s / steps;
      if (!passable(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t)) return false;
    }
    return true;
  };
  const out = [];
  let i = 0;
  while (i < world.length - 1) {
    let j = world.length - 1;
    for (; j > i + 1; j--) {
      if (segmentClear(world[i], world[j])) break;
    }
    out.push(world[j]);
    i = j;
  }
  return out;
}

// ---------------------------------------------------------------------------
// UNIT ORDER PATHING — cost from terrainSample so it's physically identical
// to what the unit will actually experience while following the route.
const MAX_ROAD_MULT = 1.6; // highest roadMult across MOVE_CLASSES (wheeled) — keeps the heuristic admissible

function unitTileCost(map, moveClass, tileSize, gx, gy) {
  const s = terrainSample(map, moveClass, (gx + 0.5) * tileSize, (gy + 0.5) * tileSize);
  return s.passable ? 1 / s.mult : Infinity;
}

// Raw (uncached) solve: world start/goal -> simplified world waypoint list,
// or null if unreachable. Snaps start/goal off impassable tiles first.
export function findPath(map, moveClass, sx, sy, gx, gy) {
  const ts = map.tileSize;
  const sgx = Math.max(0, Math.min(map.width - 1, Math.floor(sx / ts)));
  const sgy = Math.max(0, Math.min(map.height - 1, Math.floor(sy / ts)));
  const ggx = Math.max(0, Math.min(map.width - 1, Math.floor(gx / ts)));
  const ggy = Math.max(0, Math.min(map.height - 1, Math.floor(gy / ts)));
  const ok = (x, y) => unitTileCost(map, moveClass, ts, x, y) < Infinity;
  const start = nearestPassableTile(map.width, map.height, sgx, sgy, ok) || { x: sgx, y: sgy };
  const goal = nearestPassableTile(map.width, map.height, ggx, ggy, ok) || { x: ggx, y: ggy };

  const t0 = performance.now();
  const result = aStarGrid(map.width, map.height, start.x, start.y, goal.x, goal.y,
    (x, y) => unitTileCost(map, moveClass, ts, x, y), 1 / MAX_ROAD_MULT);
  const ms = performance.now() - t0;
  recordSolve('unit', ms, result ? result.expansions : 0);
  if (!result) return null;
  return simplifyPath(result.path, ts, (wx, wy) => terrainSample(map, moveClass, wx, wy).passable);
}

// Cache keyed by move-class + destination tile + map version (roads change
// the version, which naturally invalidates every cached route). This is the
// "coalesce" mechanism: a mass order to N units of the same move class
// landing on (about) the same goal tile triggers exactly one A* solve — the
// first unit to ask computes and caches it, everyone else in the same tick
// reuses the reference. Each unit still picks its own entry point onto the
// shared route (see game/units.js ensurePath), so it reads as "share one
// solve with per-unit offsets" rather than everyone teleporting onto a
// single unit's exact path.
const pathCache = new Map();
let cacheEpoch = -1;

export function findPathCached(map, moveClassName, moveClass, sx, sy, gx, gy) {
  if (map.version !== cacheEpoch) { pathCache.clear(); cacheEpoch = map.version; }
  const ts = map.tileSize;
  const ggx = Math.max(0, Math.min(map.width - 1, Math.floor(gx / ts)));
  const ggy = Math.max(0, Math.min(map.height - 1, Math.floor(gy / ts)));
  const key = moveClassName + '|' + ggx + ',' + ggy;
  // BUGFIX (found verifying P4): findPath returns `null` for a genuinely
  // unreachable goal (e.g. an order whose destination a building now
  // blocks off). `pathCache.get(key)` legitimately returns that `null` for
  // an already-solved-and-failed goal, but `if (cached)` treated null as a
  // cache MISS, not a cached failure — so every unit stuck on an
  // unreachable order re-ran a full A* solve (several ms, thousands of
  // node expansions) EVERY FRAME forever, since game/units.js's
  // ensurePath() also always treats `!u.path` as stale and asks again.
  // `has()` distinguishes "never solved" from "solved, no path exists" so a
  // failed solve is cached exactly like a successful one — re-solved only
  // when map.version actually changes (a road/building could open/close
  // the route), not every call.
  if (pathCache.has(key)) return pathCache.get(key);
  const path = findPath(map, moveClass, sx, sy, gx, gy);
  pathCache.set(key, path);
  if (pathCache.size > 128) pathCache.delete(pathCache.keys().next().value);
  return path;
}

// ---------------------------------------------------------------------------
// ROAD PLANNER — used by both the R-mode builder (main.js) and genmap.js's
// starter highway network. Cost is move-class-agnostic (infrastructure
// serves everyone, not one vehicle type): mountains and open sea are hard
// walls, a river crossing is expensive-but-legal (a bridge), and tiles that
// are already road are heavily discounted so new construction branches off
// the existing network instead of laying a redundant parallel route.
export function roadTileCost(map, gx, gy) {
  const t = map.terrainAt(gx, gy);
  if (!t) return Infinity;
  if (t.name === 'mountain') return Infinity;
  if (t.water && t.name !== 'river') return Infinity; // open sea: unbridgeable
  if (map.blockAt && map.blockAt(gx, gy)) return Infinity; // a building footprint (P2 city layer): can't pave through one
  if (map.roadAt(gx, gy) > 0) return 0.12; // reuse/extend the existing network cheaply
  if (t.name === 'river') return 4; // bridge: legal but discouraged, keeps crossings short
  return 1 / Math.max(0.15, t.moveMult ?? 1);
}
const ROAD_MIN_COST = 0.12;

// Route a road between two world points. Returns { tiles, waypoints } where
// `tiles` is the DENSE per-tile grid path (every cell to actually pave —
// string-pulling would skip tiles a straight line can cut through) and
// `waypoints` is the simplified world-space polyline (for the placement
// preview line). Null if no legal route exists (e.g. point stranded on an
// island of mountains with no pass).
export function findRoadRoute(map, ax, ay, bx, by) {
  const ts = map.tileSize;
  const sgx = Math.max(0, Math.min(map.width - 1, Math.floor(ax / ts)));
  const sgy = Math.max(0, Math.min(map.height - 1, Math.floor(ay / ts)));
  const ggx = Math.max(0, Math.min(map.width - 1, Math.floor(bx / ts)));
  const ggy = Math.max(0, Math.min(map.height - 1, Math.floor(by / ts)));
  const ok = (x, y) => roadTileCost(map, x, y) < Infinity;
  const start = nearestPassableTile(map.width, map.height, sgx, sgy, ok) || { x: sgx, y: sgy };
  const goal = nearestPassableTile(map.width, map.height, ggx, ggy, ok) || { x: ggx, y: ggy };

  const t0 = performance.now();
  const result = aStarGrid(map.width, map.height, start.x, start.y, goal.x, goal.y,
    (x, y) => roadTileCost(map, x, y), ROAD_MIN_COST);
  const ms = performance.now() - t0;
  recordSolve('road', ms, result ? result.expansions : 0);
  if (!result) return null;
  const waypoints = simplifyPath(result.path, ts, (wx, wy) => roadTileCost(map, Math.floor(wx / ts), Math.floor(wy / ts)) < Infinity);
  return { tiles: result.path, waypoints };
}

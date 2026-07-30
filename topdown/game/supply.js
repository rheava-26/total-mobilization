// SUPPLY CONVOYS — a second PURELY-VISUAL ambient layer, sibling to
// game/ambient.js's road traffic but with a point: instead of anonymous
// dots wandering the road network, these are cargo trucks that actually
// drive from a real resource SOURCE (a player mine) or a real PRODUCER (a
// player factory/production facility) to a real CONSUMER (another
// production facility, a supply depot, or the capital) along an ACTUAL road
// route (game/pathfind.js findRoadRoute). The goal is purely to make the
// logistics CONCEPT.md's Pillar 3 cares about legible at a glance — "I can
// see ore leaving that mine and reaching that factory" — with zero coupling
// to whether that resource accounting is real.
//
// Exactly like ambient.js, this is READ-ONLY over map/world: it reads
// world.buildings and map.cities/map.roadAt/map.version to decide where a
// convoy goes, and it NEVER writes back to economy/production/resources/
// units/buildings/fog. Delete this file and every import of it and the game
// plays byte-identical — the road network just goes back to only having
// ambient.js's anonymous traffic on it.
//
// Perf discipline mirrors ambient.js exactly:
//   - a hard population cap (MAX_CONVOYS) regardless of map/base size
//   - route-finding (the one genuinely non-trivial cost here) happens AT
//     MOST once per SCAN_INTERVAL seconds, spawning at most one new convoy
//     per scan, and even then only after trying a handful of nearest
//     candidates (MAX_ROUTE_TRIES_PER_SPAWN) — never a frame-by-frame or
//     per-convoy re-solve
//   - solved routes are cached by (source id, destination id), invalidated
//     only when map.version changes (a road was actually built/changed) —
//     same invalidation contract game/pathfind.js's own findPathCached uses
//   - once spawned, a convoy just walks its pre-solved waypoint polyline;
//     per-frame cost per convoy is a few multiplications, no pathfinding
//
// Degrades gracefully to "zero convoys" with no error if there are no
// buildings yet, no mines, no producers, or no road connectivity between
// them — exactly ambient.js's contract for a sparse/fresh map.

import { findRoadRoute } from './pathfind.js';
import { RESOURCE_DEFS } from './resources.js';

const MAX_CONVOYS = 12;
const SCAN_INTERVAL = 1.1; // seconds between spawn attempts — keeps findRoadRoute calls rare
const MAX_ROUTE_TRIES_PER_SPAWN = 3; // nearest-N destination candidates tried before giving up this tick
const ROUTE_CACHE_CAP = 200;

// Truck pace: deliberately slower than any combat unit (game/units.js's
// slowest infantry is speed:30-40 world-px/sec) so convoys read as
// logistics, not a race, but faster than ambient.js's own background
// traffic dots (0.35-0.8 tiles/sec, i.e. ~11-26px/sec) so they're visibly a
// distinct, purposeful layer on top of that ambient churn.
const CONVOY_SPEED_MIN = 34; // world px/sec
const CONVOY_SPEED_MAX = 46;

// Generic "manufactured goods" cargo tint for producer -> depot/city
// convoys, which have no single RESOURCE_DEFS color to borrow (a factory's
// output isn't one named resource). Distinct from every RESOURCE_DEFS color
// above so raw-material and finished-goods convoys read as two different
// kinds of traffic at a glance.
const GOODS_CARGO_COLOR = '#c2985a';

// Every player building key that plausibly CONSUMES a raw resource a mine
// produces — the full production-facility roster (game/buildings.js) plus
// supplyDepot (a legitimate "materials flow here to be stockpiled" sink even
// though it has no recipe of its own).
const CONSUMER_KEYS = new Set([
  'factory', 'tankFactory', 'aircraftPlant', 'ammunitionPlant', 'shipyard',
  'artilleryWorks', 'droneWorks', 'missileWorks', 'hypersonicWorks',
  'airDefenseWorks', 'guidedWeaponsWorks', 'signalsWorks', 'electronicsPlant',
  'alloyForge', 'supplyDepot',
]);
// Producer keys (a subset of CONSUMER_KEYS, minus supplyDepot itself) — a
// completed one of these can ALSO act as a convoy SOURCE, sending finished
// goods onward to a depot/the capital, so the supply layer reads two-sided
// (raw material inbound to industry, product outbound from it) rather than
// only ever "mine -> factory."
const PRODUCER_KEYS = new Set([...CONSUMER_KEYS].filter(k => k !== 'supplyDepot'));

function dist2(a, b) {
  const dx = a.x - b.x, dy = a.y - b.y;
  return dx * dx + dy * dy;
}

// The capital (map.cities[0], same convention ambient.js's flagPole uses) as
// a standing "destination of last resort" — every base has one, road-
// connected or not, so a fresh economy with no supplyDepot yet still has
// somewhere for a convoy to plausibly head.
function capitalPoint(map) {
  const capital = (map.cities || [])[0];
  if (!capital) return null;
  const ts = map.tileSize;
  return { id: 'city0', x: (capital.x + 0.5) * ts, y: (capital.y + 0.5) * ts };
}

// Every complete player building that can act as a convoy SOURCE this scan:
// mines bound to a real deposit (raw material) and producer facilities
// (finished goods). Read-only scan of world.buildings — same cost class as
// ambient.js's own smoke-source scan, which already walks this array once
// per tick.
function collectSources(world) {
  const sources = [];
  for (const b of world.buildings) {
    if (b.side !== 'player' || b.status !== 'complete') continue;
    if (b.key === 'mine' && b.resourceType) sources.push({ b, kind: 'raw' });
    else if (PRODUCER_KEYS.has(b.key)) sources.push({ b, kind: 'goods' });
  }
  return sources;
}

// Candidate destinations for a given source kind: a raw-material convoy
// (mine) may head to any OTHER consumer facility or the capital; a
// finished-goods convoy (producer) heads only to a supply depot or the
// capital (it doesn't make sense for a tank factory to ship crates to
// another tank factory).
function collectDestinations(world, map, kind, srcBuilding) {
  const pool = [];
  for (const b of world.buildings) {
    if (b.side !== 'player' || b.status !== 'complete' || b === srcBuilding) continue;
    if (kind === 'raw' ? CONSUMER_KEYS.has(b.key) : b.key === 'supplyDepot') {
      pool.push({ id: 'b' + b.id, x: b.x, y: b.y });
    }
  }
  const cap = capitalPoint(map);
  if (cap) pool.push(cap);
  return pool;
}

function getCachedRoute(supply, map, key, ax, ay, bx, by) {
  if (supply.cacheVersion !== map.version) {
    supply.routeCache.clear();
    supply.cacheVersion = map.version;
  }
  if (supply.routeCache.has(key)) return supply.routeCache.get(key);
  const route = findRoadRoute(map, ax, ay, bx, by);
  supply.routeCache.set(key, route);
  if (supply.routeCache.size > ROUTE_CACHE_CAP) supply.routeCache.delete(supply.routeCache.keys().next().value);
  return route;
}

// Try to spawn exactly one new convoy this scan: pick a random live source,
// route to the nearest few destination candidates (cheapest-first via
// straight-line distance) and take the first that's actually road-connected.
// Bounded cost: at most MAX_ROUTE_TRIES_PER_SPAWN findRoadRoute calls, most
// of which are cache hits after the first scan of a stable base.
function attemptSpawn(supply, world, map) {
  if (supply.convoys.length >= MAX_CONVOYS) return;
  const sources = collectSources(world);
  if (!sources.length) return;
  const src = sources[Math.floor(Math.random() * sources.length)];
  const destPool = collectDestinations(world, map, src.kind, src.b);
  if (!destPool.length) return;
  destPool.sort((a, b) => dist2(src.b, a) - dist2(src.b, b));
  const tries = Math.min(destPool.length, MAX_ROUTE_TRIES_PER_SPAWN);
  for (let i = 0; i < tries; i++) {
    const dst = destPool[i];
    const key = src.b.id + '>' + dst.id;
    const route = getCachedRoute(supply, map, key, src.b.x, src.b.y, dst.x, dst.y);
    if (route && route.waypoints && route.waypoints.length >= 2) {
      const cargoColor = src.kind === 'raw'
        ? (RESOURCE_DEFS[src.b.resourceType] ? RESOURCE_DEFS[src.b.resourceType].color : '#9aa0a6')
        : GOODS_CARGO_COLOR;
      supply.convoys.push({
        id: supply.nextId++,
        kind: src.kind, // 'raw' (mine -> consumer) or 'goods' (producer -> depot/capital) — debug/inspection only, unused by rendering
        waypoints: route.waypoints,
        segIdx: 0, segT: 0,
        speed: CONVOY_SPEED_MIN + Math.random() * (CONVOY_SPEED_MAX - CONVOY_SPEED_MIN),
        cargoColor,
        wx: route.waypoints[0].x, wy: route.waypoints[0].y,
        ang: 0,
        bobPhase: Math.random() * 6.283,
      });
      return;
    }
  }
  // no road-connected destination found among the nearest few this tick —
  // just skip; the next scan will try again (maybe a different random
  // source, maybe roads have since been extended and map.version bumped
  // the cache)
}

// Advance every live convoy along its pre-solved waypoint polyline by
// speed*dt world-px, despawning on arrival. Pure arithmetic — no
// pathfinding, no allocation beyond the fixed convoy objects themselves.
function moveConvoys(supply, dt) {
  for (let i = supply.convoys.length - 1; i >= 0; i--) {
    const c = supply.convoys[i];
    let remaining = c.speed * dt;
    while (remaining > 0 && c.segIdx < c.waypoints.length - 1) {
      const a = c.waypoints[c.segIdx], b = c.waypoints[c.segIdx + 1];
      const segLen = Math.hypot(b.x - a.x, b.y - a.y) || 1e-6;
      const distLeft = segLen * (1 - c.segT);
      if (remaining < distLeft) {
        c.segT += remaining / segLen;
        remaining = 0;
      } else {
        remaining -= distLeft;
        c.segIdx++;
        c.segT = 0;
      }
    }
    const a = c.waypoints[c.segIdx];
    const b = c.waypoints[Math.min(c.segIdx + 1, c.waypoints.length - 1)];
    c.wx = a.x + (b.x - a.x) * c.segT;
    c.wy = a.y + (b.y - a.y) * c.segT;
    if (b.x !== a.x || b.y !== a.y) c.ang = Math.atan2(b.y - a.y, b.x - a.x);
    if (c.segIdx >= c.waypoints.length - 1) {
      // arrived — despawn; the next scan tick will spawn a replacement
      // somewhere else on the network, so the population stays lively
      // instead of draining to zero over time
      supply.convoys.splice(i, 1);
    }
  }
}

// Called ONCE per map load (main.js, right alongside initAmbient). No
// one-time scan needed here (unlike ambient.js's road-tile scan) — the
// economy this layer visualizes doesn't exist yet at map load (no buildings
// placed), so there's nothing useful to precompute; the first scan just
// naturally finds nothing and quietly waits until a mine/factory exists.
export function initSupply() {
  return {
    convoys: [],
    scanTimer: 0,
    routeCache: new Map(),
    cacheVersion: -1,
    nextId: 1,
  };
}

// Called every frame from main.js's loop(), right alongside updateAmbient.
// Movement is cheap and runs every frame; the expensive part (route search)
// is throttled to once per SCAN_INTERVAL seconds via scanTimer.
export function updateSupply(supply, world, map, dt) {
  moveConvoys(supply, dt);
  supply.scanTimer += dt;
  if (supply.scanTimer >= SCAN_INTERVAL) {
    supply.scanTimer = 0;
    attemptSpawn(supply, world, map);
  }
}

// GROUND/TRAFFIC LAYER draw — called from main.js right alongside (just
// after) drawAmbientGround, so convoys read as part of the same road-life
// layer as ambient.js's traffic dots, still well below buildings/units.
export function drawSupply(supply, ctx, worldToScreen, cam) {
  const dpr = devicePixelRatio;
  const now = performance.now() / 1000;

  // Smooth fade-out approaching the low end of the zoom range (strategic
  // view) so convoys thin out gracefully instead of popping — the hard
  // per-convoy size cutoff below is the real anti-clutter guarantee, this
  // just makes the transition into it less abrupt.
  const zoomFade = cam.zoom < 0.34 ? Math.max(0, (cam.zoom - 0.16) / 0.18) : 1;
  if (zoomFade <= 0.02) return;

  ctx.save();
  ctx.globalAlpha = zoomFade;
  for (const c of supply.convoys) {
    const scale = cam.zoom * dpr;
    const size = 3.0 * scale;
    if (size < 1.0) continue; // sub-pixel truck — skip the draw call entirely, matches ambient.js's own convention
    const [sx, sy] = worldToScreen(c.wx, c.wy);
    const bob = Math.sin(now * 9 + c.bobPhase) * 0.35 * scale; // small rumble, purely cosmetic
    const len = size * 2.6, wid = size * 1.3;
    ctx.save();
    ctx.translate(sx, sy + bob);
    ctx.rotate(c.ang);
    // cargo box (rear ~58% of body) — tinted by source resource/goods
    ctx.fillStyle = c.cargoColor;
    ctx.fillRect(-len * 0.5, -wid * 0.5, len * 0.58, wid);
    if (size > 1.6) {
      ctx.strokeStyle = 'rgba(20,18,14,0.4)';
      ctx.lineWidth = Math.max(0.5, 0.5 * scale);
      ctx.strokeRect(-len * 0.5, -wid * 0.5, len * 0.58, wid);
    }
    // cab (front)
    ctx.fillStyle = 'rgba(58,60,66,0.94)';
    ctx.fillRect(len * 0.06, -wid * 0.42, len * 0.42, wid * 0.84);
    if (size > 2.2) {
      ctx.fillStyle = 'rgba(190,215,230,0.55)';
      ctx.fillRect(len * 0.30, -wid * 0.28, len * 0.12, wid * 0.56);
    }
    ctx.restore();
  }
  ctx.restore();
}

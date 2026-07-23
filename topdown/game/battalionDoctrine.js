// BATTALION-LEVEL SELF-DEPLOYING DOCTRINE — B3 of the 6-phase battalion
// rework (docs/CONCEPT.md, "BATTALIONS, not singular units" ->
// "Self-deploying + stance"). game/doctrine.js already gives every LOOSE
// player unit a self-organizing garrison AI; this module is the same idea
// lifted one level up, to the game/battalions.js formation record: instead of
// each sub-unit picking its own city independently, the whole battalion picks
// ONE shared sector (a city) and holds it as a visible cluster, with a
// player-settable STANCE (defensive/balanced/aggressive) shaping where in
// that sector the formation actually sits.
//
// HOW IT PLUGS IN: exactly like updatePlayerDoctrine, this only ever WRITES
// `u.order = {x,y,doctrine:true}` on a battalion's sub-units — the same
// field a player right-click or doctrine.js itself already writes, consumed
// unmodified by units.js's own steering/pathing/combat. This module never
// moves a unit itself, never fires a weapon. It runs once per frame BEFORE
// updateUnits (see main.js's loop()), right alongside updatePlayerDoctrine —
// see doctrine.js's one-line handoff comment: a unit tagged `u.battalion`
// is skipped by updatePlayerDoctrine entirely, so this module is the ONLY
// thing issuing it doctrine orders. A unit with a live PLAYER order (no
// `.doctrine` flag) or one actively engaged (`u.target` with hp>0) is left
// alone, same arbitration rule as doctrine.js, for the same reason: explicit
// orders always win, and doctrine only governs the IDLE/hold goal.
//
// SECTOR PICK: `bn.homeSector` (game/battalions.js field, inert until now)
// is an explicit player assignment (see assignSector below) and, once set,
// persists and always wins AS LONG AS the side still owns that city — a
// player who rallies a battalion to a city shouldn't have it silently
// wander off next tick. With no usable explicit assignment, the battalion
// AUTO-picks the best OWNED city using the same scoring shape doctrine.js
// scores loose units with (capital bias + detected threat nearby - distance
// penalty), just evaluated once for the whole battalion (from its live
// sub-units' centroid) instead of once per unit.
//
// STANCE: acted on via the formation's hold-point and ring size (see
// STANCE_RING_MUL / formationCenterFor below) — defensive digs in tight at
// the sector's own center, balanced uses doctrine.js's normal garrison ring,
// aggressive shifts the formation center out toward the nearest detected
// threat near the sector (bounded — never a full-map charge) and widens the
// ring slightly for a forward posture.
//
// Several small helpers below (spiralAround/holdRingFor/safeGroundPoint/
// nearestWaterTile/cityThreatCount) are DUPLICATED from game/doctrine.js
// rather than imported — doctrine.js doesn't export them, and per that
// module's own precedent (it duplicates enemyai.js's nearestWaterTile rather
// than importing it), this file stays a separate, self-contained module.
//
// B5b adds a second kind of duplication in the same spirit: onSettlementLane
// (+ its settlementLaneParams helper) replicates engine/renderer.js's
// street-grid hit test verbatim (same hash2(seedX,seedY,9001/9002/9003)
// formula) so garrison hold-points can be biased onto/off the SAME lanes the
// renderer actually draws — see garrisonBiasPoint below for the full
// rationale.

import { MOVE_CLASSES, terrainSample } from './units.js';
import { detects } from './fog.js';
import { cityCenterWorld } from './objectives.js';
// B4 (game/battalionMorale.js): morale OWNS bn.morale/bn.cohesion — this
// module only ever READS `isRouting(bn)` to decide garrison-vs-flee, per
// that file's header ("battalionMorale owns morale; battalionDoctrine reads
// it to choose garrison-vs-flee"). Never writes morale/cohesion itself.
import { isRouting } from './battalionMorale.js';

// ---------------------------------------------------------------------------
// TUNABLES — same scoring shape/values as doctrine.js's own (kept identical
// so a battalion and a loose unit read a city's worth the same way; a
// battalion just isn't crowd-penalized against other battalions the way
// individual units are against each other, since there are far fewer of them
// and stacking two battalions in the same city is a legitimate defensive
// choice, not the swarm-dogpile doctrine.js's CROWD_PENALTY guards against).
const ASSIGN_INTERVAL_S = 2.5; // same cadence as doctrine.js — "the nation redeployed itself," not twitchy per-frame reassignment
const REASSIGN_MARGIN = 60; // hysteresis for the AUTO-pick path only (an explicit homeSector never needs hysteresis — it just sticks)
const CAPITAL_VALUE = 260;
const THREAT_RADIUS_PX = 420;
const THREAT_VALUE_PER_ENEMY = 340;
const DISTANCE_PENALTY = 0.5;
const SLOT_RADIUS_STEP = 16; // px of extra ring radius per further-out ground slot (see spiralAround)
const GOLDEN_ANGLE = 2.399963; // sunflower-seed spiral constant — see doctrine.js's identical constant for the full rationale
const NAVAL_RING = { base: 40, step: 24, max: 170 };

// STANCE knobs. STANCE_RING_MUL scales doctrine.js's own default garrison
// ring sizing (holdRingFor below) per stance: defensive digs in small,
// balanced is the unmodified default, aggressive spreads slightly wider (a
// forward line, not a huddle). AGGRESSIVE_SHIFT_FRAC is how far (as a
// fraction of the city's own capture radius) the aggressive formation CENTER
// is pulled toward a nearby detected enemy — bounded by the capture radius
// itself so "aggressive" reads as "forward edge of our own sector," never a
// march across the map.
const STANCE_RING_MUL = {
  defensive: { base: 0.35, max: 0.4 },
  balanced: { base: 1, max: 1 },
  aggressive: { base: 1.2, max: 1.35 },
};
const AGGRESSIVE_SHIFT_FRAC = 0.55;
const DEFAULT_STANCE = 'balanced';

// ROUT knobs (B4 — see isRouting import above). A routing battalion huddles
// tight around the nearest friendly city's own center — smaller than even
// the defensive stance ring (STANCE_RING_MUL.defensive) since this reads as
// "fleeing men bunching up at the gate," not an organized dig-in.
const ROUT_RING_MUL = { base: 0.25, max: 0.35 };

// Per-battalion assignment memory, keyed by battalion id (mirrors doctrine.
// js's postState, one level up) — holds { mapIdx } for the AUTO-picked
// sector only (an explicit bn.homeSector needs no scratch memory of its
// own: it's already sitting right there on the record). Pruned every call
// for battalions that no longer exist.
const autoState = new Map();

// Debug hook (see main.js's window.__debug.battalionDoctrine), a plain
// serializable snapshot mirroring doctrine.js's own debugDoctrineState.
export function debugBattalionDoctrineState() {
  const out = [];
  for (const [id, st] of autoState) out.push({ id, mapIdx: st.mapIdx });
  return out;
}

// Test-only escape hatch, same shape as doctrine.js's debugForceAssignTick —
// forces the next updateBattalionDoctrine call to run its full pass
// regardless of the real elapsed-time throttle.
let clock = 0;
export function debugForceTick() { clock = ASSIGN_INTERVAL_S; }

// ---------------------------------------------------------------------------
// DUPLICATED HELPERS (see this file's header for why) — identical in
// substance to game/doctrine.js's own copies.

function nearestWaterTile(map, wx, wy, maxTiles = 25) {
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
  return null;
}

function spiralAround(cx, cy, baseR, step, idx, maxR) {
  const angle = idx * GOLDEN_ANGLE;
  const r = Math.min(maxR, baseR + Math.sqrt(idx) * step);
  return { x: cx + Math.cos(angle) * r, y: cy + Math.sin(angle) * r };
}

// Ground/naval hold points must land inside the city's own capture radius
// (game/objectives.js) so a garrisoned formation actually COUNTS as
// contesting presence. Air gets a slightly wider ring for an "overhead and
// around" footprint, matching doctrine.js's own domain split. Returned base/
// max are BEFORE the stance multiplier (applied by the caller).
function holdRingFor(def, capRWorld) {
  if (def.domain === 'air') return { base: capRWorld * 0.45, max: capRWorld * 1.25 };
  return { base: capRWorld * 0.3, max: capRWorld * 0.85 };
}

function safeGroundPoint(map, moveClass, cx, cy, x, y) {
  if (terrainSample(map, moveClass, x, y).passable) return { x, y };
  return { x: cx, y: cy };
}

// ---------------------------------------------------------------------------
// GARRISON PLACEMENT — B5b (docs/reference-battalion-design.md §4: "Infantry
// in Cities & Urban Garrisoning"). When a battalion is garrisoning its own
// city, its ground sub-units should sit PLAUSIBLY in that city instead of a
// bare spiral ring on open ground: infantry biased into the buildings/blocks
// (off the streets — "holed up," hard to root out), armor/vehicles biased
// onto the street network (the only place a tank/AA/scout car could actually
// stand in a built-up block).
//
// To know street-vs-building at a world point this REPLICATES engine/
// renderer.js's settlement lane test (settlementGrid + onSettlementLane) —
// same hash2(seedX,seedY,9001/9002/9003) rotation/spacing/width formula,
// copied rather than imported (renderer.js doesn't export it, and per this
// file's header, this module stays self-contained the same way it already
// duplicates spiralAround/holdRingFor/nearestWaterTile from doctrine.js
// rather than importing them) — so a unit ordered here to "stand in a
// building" or "stand on a street" actually matches what B5a's renderer
// draws at that point. If this formula and the renderer's ever drift apart,
// update both together.
function laneHash2(x, y, seed) {
  let h = (x * 374761393 + y * 668265263 + seed * 2147483647) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  h = (h ^ (h >>> 16)) >>> 0;
  return h / 4294967296;
}

function settlementLaneParams(c, ts) {
  const seedX = Math.floor(c.x), seedY = Math.floor(c.y);
  const rot = (laneHash2(seedX, seedY, 9001) - 0.5) * Math.PI * 0.9;
  const spacing = ts * (2.2 + laneHash2(seedX, seedY, 9002) * 1.3);
  const width = ts * (0.14 + laneHash2(seedX, seedY, 9003) * 0.08);
  return { rot, spacing, width };
}

// Same rotated-grid test as renderer.js's onSettlementLane, evaluated at one
// world-px point.
function onSettlementLane(c, ts, wx, wy) {
  const { rot, spacing, width } = settlementLaneParams(c, ts);
  const relx = wx - (c.x + 0.5) * ts, rely = wy - (c.y + 0.5) * ts;
  const lx = relx * Math.cos(rot) + rely * Math.sin(rot);
  const ly = -relx * Math.sin(rot) + rely * Math.cos(rot);
  const halfW = width / 2;
  const laneX = ((lx + halfW) % spacing + spacing) % spacing;
  const laneY = ((ly + halfW) % spacing + spacing) % spacing;
  return laneX < width || laneY < width;
}

// Nudges a candidate ground hold-point toward (wantLane=true) or away from
// (wantLane=false) the settlement's street network — tries a handful of
// small spiral offsets around the candidate (own golden-angle spiral, offset
// by `idx` so different sub-units searching from similar starting points
// fan out to different offsets rather than piling onto the same nudge) until
// one lands on the right side of the lane test AND is still legal ground for
// this move class; if nothing found within the search budget, keeps the
// original (terrain-clamped) point — never leaves a unit with no order.
const GARRISON_SNAP_TRIES = 18;
const GARRISON_SNAP_STEP = 12; // px of extra search radius per try — a settlement's lane spacing (70-112px @ TILE_SIZE 32) is comfortably covered within the try budget
function garrisonBiasPoint(map, city, moveClass, cx, cy, x, y, wantLane, idx) {
  const ts = map.tileSize;
  if (onSettlementLane(city, ts, x, y) === wantLane) return safeGroundPoint(map, moveClass, cx, cy, x, y);
  for (let t = 1; t <= GARRISON_SNAP_TRIES; t++) {
    const angle = idx * GOLDEN_ANGLE + t * 1.31;
    const r = t * GARRISON_SNAP_STEP;
    const nx = x + Math.cos(angle) * r, ny = y + Math.sin(angle) * r;
    if (onSettlementLane(city, ts, nx, ny) !== wantLane) continue;
    const safe = safeGroundPoint(map, moveClass, cx, cy, nx, ny);
    if (safe.x === nx && safe.y === ny) return safe; // only accept if it actually kept the biased point (not the impassable-fallback)
  }
  return safeGroundPoint(map, moveClass, cx, cy, x, y); // search exhausted — fall back to the plain spiral slot rather than leave the unit stranded
}

// How many living enemy units (fog-gated) sit within THREAT_RADIUS_PX of a
// point — the scoring input for "reinforce what's under attack."
function threatCount(world, side, cx, cy) {
  const r2 = THREAT_RADIUS_PX * THREAT_RADIUS_PX;
  let n = 0;
  for (const u of world.units) {
    if (u.side === side || u.hp <= 0) continue;
    if (!detects(side, u)) continue;
    const dd = (u.x - cx) ** 2 + (u.y - cy) ** 2;
    if (dd <= r2) n++;
  }
  return n;
}

// Nearest living, DETECTED enemy within maxR of a point — the AGGRESSIVE
// stance's "which way is forward" input. Same fog-gating as threatCount
// above (no beelining toward an unspotted enemy).
function nearestThreat(world, side, cx, cy, maxR) {
  const r2 = maxR * maxR;
  let best = null, bd = Infinity;
  for (const u of world.units) {
    if (u.side === side || u.hp <= 0) continue;
    if (!detects(side, u)) continue;
    const dd = (u.x - cx) ** 2 + (u.y - cy) ** 2;
    if (dd > r2 || dd >= bd) continue;
    bd = dd; best = u;
  }
  return best;
}

// ---------------------------------------------------------------------------
// STANCE -> formation center. Defensive/balanced both hold dead center on
// the sector; aggressive pulls the center toward the nearest detected enemy
// near the sector, bounded to AGGRESSIVE_SHIFT_FRAC of the city's own
// capture radius so it reads as "our forward edge," not a sortie.
function formationCenterFor(world, side, sectorInfo, stance) {
  if (stance !== 'aggressive') return { x: sectorInfo.cx, y: sectorInfo.cy };
  const enemy = nearestThreat(world, side, sectorInfo.cx, sectorInfo.cy, THREAT_RADIUS_PX);
  if (!enemy) return { x: sectorInfo.cx, y: sectorInfo.cy };
  const dx = enemy.x - sectorInfo.cx, dy = enemy.y - sectorInfo.cy;
  const d = Math.hypot(dx, dy) || 1;
  const shift = sectorInfo.capRWorld * AGGRESSIVE_SHIFT_FRAC;
  return { x: sectorInfo.cx + (dx / d) * shift, y: sectorInfo.cy + (dy / d) * shift };
}

function ringForStance(def, sectorInfo, stance) {
  const base = holdRingFor(def, sectorInfo.capRWorld);
  const mul = STANCE_RING_MUL[stance] || STANCE_RING_MUL[DEFAULT_STANCE];
  return { base: base.base * mul.base, max: base.max * mul.max };
}

// Same shape as ringForStance but for the ROUT branch below — no stance
// input, always the tight panic-huddle multiplier.
function ringForRout(def, sectorInfo) {
  const base = holdRingFor(def, sectorInfo.capRWorld);
  return { base: base.base * ROUT_RING_MUL.base, max: base.max * ROUT_RING_MUL.max };
}

// ---------------------------------------------------------------------------
// PLAYER CONTROL API — called both from main.js's Battalions panel directly
// and mirrored into window.__debug.battalionDoctrine for headless tests (see
// main.js). Both just look the battalion up by id and mutate its record;
// the actual re-posting of its sub-units happens on the next assignment
// tick above, same "write the intent, let the next tick execute it" shape
// the rest of this module already uses.
export function assignSector(world, bnId, cityIdx) {
  const bn = (world.battalions || []).find(b => b.id === bnId);
  if (!bn) return false;
  bn.homeSector = cityIdx;
  return true;
}

const STANCES = ['defensive', 'balanced', 'aggressive'];
export function setStance(world, bnId, stance) {
  const bn = (world.battalions || []).find(b => b.id === bnId);
  if (!bn || !STANCES.includes(stance)) return false;
  bn.stance = stance;
  return true;
}
// Cycles a battalion's stance to the next in STANCES order (wrapping) — the
// Battalions panel's per-row stance control uses this directly.
export function cycleStance(world, bnId) {
  const bn = (world.battalions || []).find(b => b.id === bnId);
  if (!bn) return false;
  const i = STANCES.indexOf(bn.stance);
  bn.stance = STANCES[(i + 1) % STANCES.length];
  return true;
}
export { STANCES };

// ---------------------------------------------------------------------------
// Call once per frame, BEFORE updateUnits, right alongside
// updatePlayerDoctrine (see main.js's loop()) — same unconditional-of-phase
// contract as doctrine.js (a freshly reinforced battalion should start
// dispersing to its post during PREP already).
export function updateBattalionDoctrine(world, map, dt, side = 'player') {
  if (!map || !map.cities || !map.cities.length) return;
  if (!world.battalions || !world.battalions.length) return;

  // PRUNE scratch state for battalions that no longer exist (disbanded via
  // updateBattalions once every sub-unit is dead).
  const liveBnIds = new Set(world.battalions.map(bn => bn.id));
  for (const id of autoState.keys()) if (!liveBnIds.has(id)) autoState.delete(id);

  // THROTTLE — same reasoning as doctrine.js's own: this is the only
  // O(battalions * cities) pass here, and reads as "the nation redeployed
  // its formations," not twitchy per-frame reshuffling.
  clock += dt;
  if (clock < ASSIGN_INTERVAL_S) return;
  clock = 0;

  // Candidate cities: whatever `side` currently OWNS, same convention as
  // doctrine.js (you garrison your own territory).
  const cityInfos = [];
  for (let i = 0; i < map.cities.length; i++) {
    const c = map.cities[i];
    if (c.owner !== side) continue;
    const { x: cx, y: cy } = cityCenterWorld(map, c);
    const threat = threatCount(world, side, cx, cy);
    const capRWorld = (c.r || 6) * map.tileSize;
    cityInfos.push({
      mapIdx: i, cx, cy, capRWorld,
      isCapital: i === 0,
      value: (i === 0 ? CAPITAL_VALUE : 0) + threat * THREAT_VALUE_PER_ENEMY,
    });
  }
  if (!cityInfos.length) return; // no owned territory at all — nothing to deploy to

  for (const bn of world.battalions) {
    if (bn.side !== side) continue;
    const aliveUnits = bn.units.filter(u => u.hp > 0);
    if (!aliveUnits.length) continue; // updateBattalions prunes these out shortly anyway; just skip this tick

    // Battalion "position" for scoring/travel = centroid of its live
    // sub-units, per the task brief — not any single unit's position.
    let sumX = 0, sumY = 0;
    for (const u of aliveUnits) { sumX += u.x; sumY += u.y; }
    const bx = sumX / aliveUnits.length, by = sumY / aliveUnits.length;

    // ROUT (B4 — game/battalionMorale.js owns the morale math; this is the
    // ONLY place battalionDoctrine reads it, per that file's header). A
    // `broken` battalion flees toward the nearest FRIENDLY city instead of
    // holding its assigned sector — bypasses homeSector/stance entirely for
    // as long as it stays broken. Once it sits in that city and stops
    // taking fire, battalionMorale's territory-recovery rule lifts its
    // morale back out of `broken` on its own, and this branch simply stops
    // firing next tick — the battalion falls through to normal garrison
    // doctrine below with no separate "regroup" bookkeeping needed here.
    if (isRouting(bn)) {
      let nearest = null, nearestD2 = Infinity;
      for (const info of cityInfos) {
        const dd = (bx - info.cx) ** 2 + (by - info.cy) ** 2;
        if (dd < nearestD2) { nearestD2 = dd; nearest = info; }
      }
      if (nearest) {
        let idx = 0;
        for (const u of aliveUnits) {
          if (u.order && !u.order.doctrine) continue; // live player order — never touched, even mid-rout
          if (u.target && u.target.hp > 0) continue;   // actively engaged — let combat run (see this file's header: routing doesn't force units to stop firing)
          const def = u.def;
          const moveClass = MOVE_CLASSES[def.moveClass];
          const naval = !!moveClass.requiresWater;
          const isAir = def.domain === 'air';
          const ring = ringForRout(def, nearest);
          let pt;
          if (naval) {
            const water = nearestWaterTile(map, nearest.cx, nearest.cy);
            pt = water
              ? spiralAround(water.x, water.y, NAVAL_RING.base, NAVAL_RING.step, idx, NAVAL_RING.max)
              : { x: nearest.cx, y: nearest.cy };
          } else if (isAir) {
            pt = spiralAround(nearest.cx, nearest.cy, ring.base, SLOT_RADIUS_STEP, idx, ring.max);
          } else {
            const raw = spiralAround(nearest.cx, nearest.cy, ring.base, SLOT_RADIUS_STEP, idx, ring.max);
            pt = safeGroundPoint(map, moveClass, nearest.cx, nearest.cy, raw.x, raw.y);
          }
          idx++;
          u.order = { x: pt.x, y: pt.y, doctrine: true };
        }
      }
      continue; // skip normal sector-pick/garrison logic entirely while routing
    }

    // SECTOR PICK: an explicit homeSector wins outright as long as it's
    // still an owned city (see this file's header). Otherwise AUTO-pick.
    let sectorInfo = bn.homeSector != null ? cityInfos.find(ci => ci.mapIdx === bn.homeSector) : null;
    if (!sectorInfo) {
      const cur = autoState.get(bn.id);
      const curInfo = cur ? cityInfos.find(ci => ci.mapIdx === cur.mapIdx) : null;
      let bestInfo = null, bestScore = -Infinity;
      for (const info of cityInfos) {
        const d = Math.hypot(bx - info.cx, by - info.cy);
        let score = info.value - d * DISTANCE_PENALTY;
        if (curInfo && info.mapIdx === curInfo.mapIdx) score += REASSIGN_MARGIN; // hysteresis: the sitting tenant needs to be clearly beaten
        if (score > bestScore) { bestScore = score; bestInfo = info; }
      }
      sectorInfo = bestInfo;
      if (sectorInfo) autoState.set(bn.id, { mapIdx: sectorInfo.mapIdx });
    }
    if (!sectorInfo) continue; // e.g. homeSector points at a city `side` no longer owns, and there's nothing else owned either

    const stance = bn.stance || DEFAULT_STANCE;
    const center = formationCenterFor(world, side, sectorInfo, stance);

    // GARRISON PLACEMENT (B5b) — "is this a garrison": sectorInfo is ALWAYS
    // an owned city (cityInfos above is built exclusively from `c.owner ===
    // side`), so the only extra check needed is "is the formation center
    // actually sitting at/near that city" (the normal deployed case) rather
    // than, say, an aggressive-stance shift that happened to land outside
    // the city's own capture radius. Gates the building/street bias below —
    // routing/in-transit battalions never reach this branch at all (see the
    // `continue` at the end of the rout branch above), so they're never
    // treated as garrisoned here either, matching game/battalionMorale.js's
    // own bn.garrisoned definition (home AND not routing).
    const isGarrisonDeploy = Math.hypot(center.x - sectorInfo.cx, center.y - sectorInfo.cy) <= sectorInfo.capRWorld;
    const garrisonCity = isGarrisonDeploy ? map.cities[sectorInfo.mapIdx] : null;

    // Issue each idle/doctrine sub-unit a slot in the cluster, same
    // arbitration as doctrine.js: a live PLAYER order or an actively
    // engaged unit is left completely alone. `idx` (the spiral slot) only
    // advances for units actually (re)posted this tick, same convention as
    // doctrine.js's own per-city spiralN counter.
    let idx = 0;
    for (const u of aliveUnits) {
      if (u.order && !u.order.doctrine) continue; // live player order — never touched
      if (u.target && u.target.hp > 0) continue;   // actively engaged — let units.js's own combat run

      const def = u.def;
      const moveClass = MOVE_CLASSES[def.moveClass];
      const naval = !!moveClass.requiresWater;
      const isAir = def.domain === 'air';
      const ring = ringForStance(def, sectorInfo, stance);

      let pt;
      if (naval) {
        // Naval can't stand on the ground hold-point at all — snap to the
        // nearest coastal water (near the formation center if possible,
        // else the sector's own center); if this sector simply isn't
        // coastal, fall back to the sector center itself rather than
        // leaving the unit stranded with no order (per the task brief:
        // "don't crash on an odd unit" — this also just doesn't move it
        // usefully, which is the best any placement can do here).
        const water = nearestWaterTile(map, center.x, center.y) || nearestWaterTile(map, sectorInfo.cx, sectorInfo.cy);
        pt = water
          ? spiralAround(water.x, water.y, NAVAL_RING.base, NAVAL_RING.step, idx, NAVAL_RING.max)
          : { x: sectorInfo.cx, y: sectorInfo.cy };
      } else if (isAir) {
        // Air ignores terrain entirely (MOVE_CLASSES.air.ignoresTerrain) —
        // any spiral point around the formation center is always valid.
        pt = spiralAround(center.x, center.y, ring.base, SLOT_RADIUS_STEP, idx, ring.max);
      } else {
        const raw = spiralAround(center.x, center.y, ring.base, SLOT_RADIUS_STEP, idx, ring.max);
        const safe = safeGroundPoint(map, moveClass, sectorInfo.cx, sectorInfo.cy, raw.x, raw.y);
        if (garrisonCity) {
          // Classify by moveClass, not by name (data-driven, per the task
          // brief): 'foot' (infantry/militia) biases OFF the street network
          // into the buildings/blocks; any other ground move class (armor's
          // 'tracked', a scout car's 'wheeled', etc — the vehicles) biases
          // ONTO it, since a tank has no business parked inside a block.
          const wantLane = def.moveClass !== 'foot';
          pt = garrisonBiasPoint(map, garrisonCity, moveClass, sectorInfo.cx, sectorInfo.cy, safe.x, safe.y, wantLane, idx);
        } else {
          pt = safe;
        }
      }

      idx++;
      u.order = { x: pt.x, y: pt.y, doctrine: true };
    }
  }
}

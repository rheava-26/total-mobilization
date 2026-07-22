// DEFENSIVE SELF-ORGANIZING DOCTRINE for the PLAYER's own units (CONCEPT.md
// Pillar 1, "The Nation Acts": militias garrison themselves, the army
// reinforces threatened regions, units hold defensible ground — the player
// directs, the nation self-organizes). Before this module existed, a player
// unit with no explicit order just sat wherever it spawned/was placed —
// units.js's idle branch (`else { u.target = null; }`) does nothing else —
// so a pile of units built at the same factory stayed a pile forever. This
// module is the DEFENSIVE MIRROR of game/enemyai.js's assault-group AI: same
// shape (periodic reassignment, per-unit scratch state keyed by id, pruned
// for the dead, data-driven off attributes/dispositions, never a per-unit-
// name branch), opposite goal — instead of massing to seize an objective, it
// disperses the standing force to GARRISON what's already held and shifts
// weight toward whatever's under threat.
//
// HOW IT PLUGS IN: exactly like updateEnemyAI, this only ever WRITES
// `u.order` — the same {x,y} move-destination field a player's right-click
// already writes, consumed by units.js's own steering/A*-pathing/standoff-
// engagement code untouched. This module never moves a unit itself, never
// fires a weapon, never overrides combat. It runs once per frame BEFORE
// updateUnits (see main.js's loop()), in BOTH game phases (PREP and COMBAT —
// requirement 4: a freshly built unit should disperse to a sensible holding
// spot immediately, not just once the invasion starts).
//
// ARBITRATION (idle vs ordered vs engaged), matching the task's hard rule
// "explicit player orders always win":
//   - u.order set and NOT ours (no `.doctrine` flag) -> a live player order.
//     Never touched, never even inspected past the flag check.
//   - u.target with real remaining hp -> the unit is actively pursuing/
//     fighting a live target (units.js's own pickTarget/nearestEnemy
//     fallback already drives its movement this frame regardless of order —
//     see units.js's `if (enemy) {...}` branch). Left alone; doctrine only
//     governs the IDLE goal, per the task brief, and re-posting a unit mid-
//     fight would just be thrown away this frame and risk drift once the
//     fight ends.
//   - Anything else (no order, or an order THIS module set earlier) is fair
//     game for (re)assignment below.
// A unit whose doctrine order is still in flight is left to keep walking it
// off between assignment ticks — see the throttle below.

import { MOVE_CLASSES, terrainSample } from './units.js';
import { detects } from './fog.js';
import { cityCenterWorld } from './objectives.js';

// ---------------------------------------------------------------------------
// TUNABLES — the designer's to feel-tune (per the task brief: sane defaults,
// not hand-fitted to force an outcome). Nothing here branches on a unit
// NAME; every knob reads an attribute (domain/moveClass/dispositions).
const ASSIGN_INTERVAL_S = 2.5; // how often the whole side's idle units get (re)evaluated. O(units*cities) is trivial at this game's scale, but there's no reason to pay it 60x/second, and a slower cadence is also what keeps this reading as "the nation redeployed itself" rather than twitchy micro.
const REASSIGN_MARGIN = 60; // hysteresis: a unit already posted at city X only switches to city Y if Y's score beats X's by more than this — stops two similarly-scored cities from stealing a unit back and forth every tick.
const HOLDGROUND_REASSIGN_MARGIN = 1e6; // holdGround batteries (SAM/artillery/AA/MLRS/etc, per units.js's own disposition — "a battery — never chases") effectively never voluntarily abandon a post once dug in; the only thing that can still move one is its city being lost outright (see cityInfos below: a lost city just isn't a candidate anymore, so the unit falls through to a genuine fresh assignment regardless of this margin).
const CAPITAL_VALUE = 260; // flat bonus on the capital's assignment score — "post around the capital first" (mirrors enemyai.js's CAPITAL_BIAS_PX, same idea pointed the other way: it's the city the AI presses hardest, so it's also the one the garrison should default toward).
const THREAT_RADIUS_PX = 420; // an enemy within this of a city counts as "threatening" it for scoring purposes.
const THREAT_VALUE_PER_ENEMY = 340; // each detected (fog-gated — see cityInfos) enemy within THREAT_RADIUS_PX adds this much to a city's score — the actual "reinforce what's under attack" lever.
const DISTANCE_PENALTY = 0.5; // score lost per world-px of travel — keeps a unit from marching clear across the map for a marginal threat/capital bonus; distance still has to be paid back by real value.
const CROWD_PENALTY = 70; // subtracted per unit already assigned to a city THIS tick — the anti-dogpile lever: once a city's got enough garrison, the next unit's best score tips toward a different city instead of stacking further.
const SLOT_RADIUS_STEP = 16; // px of extra ring radius per further-out ground/naval slot (see spiralAround) — spreads a larger garrison across more of the city's footprint instead of one tight ring.
const GOLDEN_ANGLE = 2.399963; // radians (~137.5°) — the sunflower-seed spiral constant: successive slot indices land at this angle apart, which is famously the spacing that never re-aligns/overlaps as the index grows, so slots stay visually spread with zero bookkeeping about which angles are "taken."
const ORBIT_STEP_RAD = 0.35; // air doctrine: how far a patrol point advances around its hub each time the unit goes idle at it — turns "reassigned every ~2.5s" into a slow, visible circling patrol instead of a fixed loiter point.
const NAVAL_RING = { base: 40, step: 24, max: 170 }; // small ring around the coastal anchor point (see nearestWaterTile) — a picket screening the approach, not scattered leagues offshore.

// Per-unit assignment memory, keyed by unit id (spawnUnit's ids are stable
// small ints — same pattern enemyai.js's aiState uses, and for the same
// reason: simpler than tagging scratch fields onto the unit object this
// module doesn't own). Holds { mapIdx, x, y, orbitPhase } — mapIdx is the
// unit's currently-posted city's index into map.cities (identity for the
// hysteresis check below), x/y is its assigned hold point (kept so a
// "staying" unit's post never needs to be recomputed), orbitPhase is air-
// only patrol state. PRUNED every call (below) for dead units — the exact
// cleanup gap the enemy AI's own v1 report flagged, not repeated here.
const postState = new Map();

// Debug hook (see main.js's window.__debug.doctrine) — a plain serializable
// snapshot so a headless test can watch assignment/spread without reaching
// into this module's private Map directly.
export function debugDoctrineState() {
  const out = [];
  for (const [id, st] of postState) out.push({ id, mapIdx: st.mapIdx, x: st.x, y: st.y });
  return out;
}

// Test-only escape hatch: force the next updatePlayerDoctrine call to run
// its full (re)assignment pass regardless of the real elapsed-time throttle,
// so a headless test doesn't have to simulate ASSIGN_INTERVAL_S of frames
// just to observe one assignment tick.
let clock = 0;
export function debugForceAssignTick() { clock = ASSIGN_INTERVAL_S; }

// Nearest WATER tile to a world point — duplicated from game/enemyai.js's
// identical helper rather than imported (enemyai.js doesn't export it, and
// this module deliberately stays a separate, self-contained file per the
// task brief — same "small, self-contained, simpler than threading a
// callback" precedent enemyai.js's own copy already sets against main.js).
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
  return null; // no water within range: this city isn't meaningfully coastal, naval can't garrison it
}

// Sunflower-seed spiral point around a hub — see GOLDEN_ANGLE's comment.
// `idx` is 0-based (this slot's position in assignment order this tick);
// radius grows with sqrt(idx) so density stays roughly even per ring
// (matching how many slots naturally fit at each radius) rather than every
// unit crowding the innermost ring before spilling outward. Clamped to
// `maxR` so a large garrison still stays within a sane radius of its city.
function spiralAround(cx, cy, baseR, step, idx, maxR) {
  const angle = idx * GOLDEN_ANGLE;
  const r = Math.min(maxR, baseR + Math.sqrt(idx) * step);
  return { x: cx + Math.cos(angle) * r, y: cy + Math.sin(angle) * r };
}

// Ground hold points MUST land inside the city's own capture radius (see
// game/objectives.js's `rWorld` check) — that's what makes a garrisoned unit
// actually COUNT as presence contesting an incursion, not just stand nearby
// looking useful while an uncontested enemy quietly captures the place out
// from under it. Air doesn't have that constraint (only ground-domain units
// count toward capture, per objectives.js's isGroundUnit), so it's allowed a
// slightly wider ring for a proper "overhead and around" patrol footprint.
function holdRingFor(def, capRWorld) {
  if (def.domain === 'air') return { base: capRWorld * 0.45, max: capRWorld * 1.25 };
  return { base: capRWorld * 0.3, max: capRWorld * 0.85 };
}

// Terrain safety net: a spiral point can, in principle, overshoot onto
// water/mountain near a coastal or hilly city's edge (this module doesn't
// run full pathfinding validation per candidate slot — that's the kind of
// per-slot A* cost this file's whole "periodic, not per-frame" throttling
// exists to avoid). If the computed point isn't passable for this unit's
// move class, fall back to the city's own center — always land, since a
// city necessarily sits on buildable ground.
function safeGroundPoint(map, moveClass, cx, cy, x, y) {
  if (terrainSample(map, moveClass, x, y).passable) return { x, y };
  return { x: cx, y: cy };
}

// How many living enemy units (fog-gated: only ones `side` currently
// detects — no beelining doctrine off units the player hasn't actually
// spotted) sit within THREAT_RADIUS_PX of a city's center. Purely a scoring
// input, same spirit as enemyai.js's cityDefenseCount but pointed at the
// opposite side.
function cityThreatCount(world, side, cx, cy) {
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

// Call once per frame, BEFORE updateUnits (see main.js's loop()) — mirrors
// updateEnemyAI's own calling contract exactly, just unconditional on
// combatActive: doctrine also governs PREP (requirement 4 — a freshly
// placed/built unit should disperse to a sensible post immediately, not
// stack at the build site waiting for the invasion to start).
export function updatePlayerDoctrine(world, map, dt, side = 'player') {
  if (!map || !map.cities || !map.cities.length) return;

  // PRUNE dead-unit scratch state every call — cheap, and keeps postState
  // from retaining stale entries for units that died frames ago (the exact
  // cleanup gap enemyai.js's own header comment calls out as a real v1 bug,
  // not repeated here).
  const liveIds = new Set();
  for (const u of world.units) if (u.side === side && u.hp > 0) liveIds.add(u.id);
  for (const id of postState.keys()) if (!liveIds.has(id)) postState.delete(id);

  // THROTTLE: the actual (re)assignment pass below is the only O(units *
  // cities) work in this module, and it only needs to run a few times a
  // minute, not every frame — see ASSIGN_INTERVAL_S's comment. Between
  // ticks there's nothing to do: a unit's already-assigned doctrine order
  // (if any) keeps being walked off by units.js's own steering exactly like
  // a player order would be.
  clock += dt;
  if (clock < ASSIGN_INTERVAL_S) return;
  clock = 0;

  // Candidate cities are whatever `side` currently OWNS — you garrison your
  // own territory, not the enemy's. A city lost since the last tick simply
  // stops being a candidate, which is also what naturally forces any unit
  // still posted there (including a holdGround battery — see
  // HOLDGROUND_REASSIGN_MARGIN) to accept a fresh assignment below: its
  // `curInfo` lookup just won't find a match anymore.
  const cityInfos = [];
  for (let i = 0; i < map.cities.length; i++) {
    const c = map.cities[i];
    if (c.owner !== side) continue;
    const { x: cx, y: cy } = cityCenterWorld(map, c);
    const threat = cityThreatCount(world, side, cx, cy);
    const capRWorld = (c.r || 6) * map.tileSize;
    cityInfos.push({
      mapIdx: i, cx, cy, capRWorld,
      isCapital: i === 0, // cities[0] is always the capital, per game/mapgen.js's convention (same one objectives.js/enemyai.js rely on)
      value: (i === 0 ? CAPITAL_VALUE : 0) + threat * THREAT_VALUE_PER_ENEMY,
      waterAnchor: nearestWaterTile(map, cx, cy), // null if this city isn't meaningfully coastal — naval simply can't garrison it
    });
  }
  if (!cityInfos.length) return; // no owned territory at all (shouldn't happen while the game is still running — losing the capital already ends it — but defensive)

  const crowd = new Map();   // mapIdx -> units assigned to it so far THIS tick (the CROWD_PENALTY input)
  const spiralN = new Map(); // mapIdx -> ground/naval slot counter THIS tick (spiralAround's `idx`)

  for (const u of world.units) {
    if (u.side !== side || u.hp <= 0) continue;
    if (u.battalion != null) continue; // B3 handoff: a unit tagged with a battalion is governed by game/battalionDoctrine.js's formation-level assignment instead — both writing u.order here would fight over the same field
    if (u.order && !u.order.doctrine) continue; // a live PLAYER order — never touched, never even scored (hard rule: explicit orders always win)
    if (u.target && u.target.hp > 0) continue;  // actively engaged with (or chasing) a live enemy — let units.js's own combat/pursuit logic run; doctrine only governs the IDLE goal

    const def = u.def;
    const moveClass = MOVE_CLASSES[def.moveClass];
    const naval = !!moveClass.requiresWater;
    const isAir = def.domain === 'air';

    const cur = postState.get(u.id);
    const curInfo = cur ? cityInfos.find(ci => ci.mapIdx === cur.mapIdx) : null;
    const margin = def.dispositions.includes('holdGround') ? HOLDGROUND_REASSIGN_MARGIN : REASSIGN_MARGIN;

    // SCORE every owned city this unit could plausibly post to. Naval only
    // ever considers cities with a real coastal water anchor; everything
    // else considers every owned city. `score` combines the city's own
    // value (capital bias + live threat), a distance cost from where the
    // unit actually is right now, and the running crowd penalty so later
    // units in this same loop are steered off an already-full city.
    let bestInfo = null, bestScore = -Infinity;
    for (const info of cityInfos) {
      if (naval && !info.waterAnchor) continue;
      const ax = naval ? info.waterAnchor.x : info.cx;
      const ay = naval ? info.waterAnchor.y : info.cy;
      const d = Math.hypot(u.x - ax, u.y - ay);
      let score = info.value - d * DISTANCE_PENALTY - (crowd.get(info.mapIdx) || 0) * CROWD_PENALTY;
      if (curInfo && info.mapIdx === curInfo.mapIdx) score += margin; // hysteresis: the sitting tenant needs to be clearly beaten, not just nosed out
      if (score > bestScore) { bestScore = score; bestInfo = info; }
    }
    if (!bestInfo) continue; // e.g. a naval unit with no coastal owned city to screen at all right now

    crowd.set(bestInfo.mapIdx, (crowd.get(bestInfo.mapIdx) || 0) + 1);

    const changingCity = !curInfo || curInfo.mapIdx !== bestInfo.mapIdx;
    const idleNow = !u.order; // truly stopped right now (arrived at its last post, or never had one) — vs. still mid-transit on a doctrine order from an earlier tick
    // STEADY STATE: already posted at the right city and (for anything but
    // air) not due for a fresh point — leave it alone. This is the anti-
    // oscillation guardrail: without it, every throttle tick would reissue
    // a fresh order (and repath) for every idle unit even when nothing
    // actually changed, which reads as units perpetually shuffling in
    // place rather than holding a formation. Air is the one exception, by
    // design — see ORBIT_STEP_RAD's comment: its "steady state" IS a slow
    // continuous patrol, so it always wants a fresh point once idle.
    if (!changingCity && !(isAir && idleNow)) continue;

    let pt;
    if (naval) {
      const idx = spiralN.get(bestInfo.mapIdx) || 0;
      spiralN.set(bestInfo.mapIdx, idx + 1);
      pt = spiralAround(bestInfo.waterAnchor.x, bestInfo.waterAnchor.y, NAVAL_RING.base, NAVAL_RING.step, idx, NAVAL_RING.max);
    } else if (isAir) {
      // Continuous slow patrol: advance this unit's own orbit phase (not a
      // shared per-tick counter — each air unit circles independently) by a
      // fixed step every time it goes idle at its hub, rather than
      // recomputing from scratch. Seeded off the unit id so several fresh
      // air units posted to the same city this tick don't all start at the
      // same angle and briefly overlap before spreading.
      const ring = holdRingFor(def, bestInfo.capRWorld);
      const startPhase = (u.id % 97) * 0.0648; // ~[0, 2*PI) spread, deterministic per unit
      const phase = (cur && cur.mapIdx === bestInfo.mapIdx ? (cur.orbitPhase ?? startPhase) : startPhase) + ORBIT_STEP_RAD;
      pt = { x: bestInfo.cx + Math.cos(phase) * ring.base, y: bestInfo.cy + Math.sin(phase) * ring.base };
      postState.set(u.id, { mapIdx: bestInfo.mapIdx, x: pt.x, y: pt.y, orbitPhase: phase });
      u.order = { x: pt.x, y: pt.y, doctrine: true };
      continue; // air's postState write happens above (it needs the extra orbitPhase field) — skip the shared write below
    } else {
      const idx = spiralN.get(bestInfo.mapIdx) || 0;
      spiralN.set(bestInfo.mapIdx, idx + 1);
      const ring = holdRingFor(def, bestInfo.capRWorld);
      const raw = spiralAround(bestInfo.cx, bestInfo.cy, ring.base, SLOT_RADIUS_STEP, idx, ring.max);
      pt = safeGroundPoint(map, moveClass, bestInfo.cx, bestInfo.cy, raw.x, raw.y);
    }

    u.order = { x: pt.x, y: pt.y, doctrine: true };
    postState.set(u.id, { mapIdx: bestInfo.mapIdx, x: pt.x, y: pt.y });
  }
}

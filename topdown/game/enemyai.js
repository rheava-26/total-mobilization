// FUNCTIONAL-AGGRESSOR ENEMY AI (Playability v1 Part B — CONCEPT.md's
// settled "Playability v1" section: "Enemy = a FUNCTIONAL AGGRESSOR. It
// lands in the combat phase, targets your cities/capital, maneuvers toward
// them, engages defenders, and captures — a genuine threat, but no economy
// of its own"). This REPLACES main.js's old scripted "advance toward the
// player base" order with real OBJECTIVE SEEKING, but reuses
// game/units.js's movement/pathfinding/targeting/fire-discipline wholesale
// — the only thing this module adds is picking WHICH city (if any) a given
// enemy unit is currently walking toward, by writing the exact same
// `u.order` field a player's right-click order already writes. Combat
// itself is untouched: updateUnits' own pickTarget/nearestEnemy fallback
// already interrupts a unit's order to fight whatever it detects in range,
// so an AI-driven unit peels off to engage a defender it meets en route
// exactly like a player-ordered unit does, then resumes its march on its
// own once nothing's left to shoot at (its order isn't cleared by combat,
// only by actually being reached or reassigned here).
//
// COORDINATION (this pass): the v1 report flagged that units used to path
// independently and trickle into a city one at a time, getting picked off
// piecemeal. The fix is CONCENTRATION OF FORCE: maneuver units are grouped
// by landing proximity into a small number of ASSAULT GROUPS, each group
// masses at a RALLY POINT before committing together to a single objective
// city, and survivors of a badly mauled group fold into another live group
// instead of scattering to found their own doomed push. See the group
// lifecycle comment above updateEnemyAI below for the state machine.
//
// DATA-DRIVEN, no per-unit-name branching: every unit's objective is picked
// purely from its MOVE CLASS (ground/air/naval — the same attribute
// units.js's own steering already branches on), its DISPOSITIONS
// (holdGround batteries dig in and don't maneuver, same doctrine the old
// scripted landing force already gave them), and its current POSITION
// (which is also all group membership is ever decided from). No unit
// key/name is ever referenced here.

import { MOVE_CLASSES } from './units.js';
import { cityCenterWorld } from './objectives.js';
// B7 (ENEMY FIELDS BATTALIONS TOO): the only battalion-aware thing this
// module reads is moraleState — see the ROUT branch below. It never reads
// bn.units/morale/cohesion for anything else, and never writes to a
// battalion record itself (that stays battalionMorale.js's job); this module
// only ever writes u.order, exactly as it always has.
import { battalionStrength } from './battalions.js';

// ---------------------------------------------------------------------------
// LANDING-FORCE DOCTRINE TUNABLES — designer's to retune; nothing below is
// hand-fitted to force a particular win/lose outcome (per the task's
// explicit "don't hand-tune to force either result" instruction), just
// reasonable first-pass defaults for "a competent aggressor that masses and
// pushes on objectives." The balance agent/designer should feel free to
// retune every constant in this block once there's real playtest data.
const CAPITAL_BIAS_PX = 720; // flat world-px discount applied to the capital's effective distance when scoring candidate objectives — a thumb on the scale, not a guarantee it's always picked first
const ARRIVE_FRACTION = 0.6; // "arrived, start holding" once within this fraction of the target city's own radius — comfortably inside the capture-mechanic's own radius check (game/objectives.js), so a holding unit always actually counts toward capture
const DEFENSE_WEIGHT_PX = 220; // each living defender detected near a candidate city adds this many world-px of "effective distance" to it — biases group targeting toward the weakest-held city, not just the nearest one, without ever fully overriding a very close/very valuable target
const CLUSTER_RADIUS_PX = 320; // units that land within this radius of a group's founding member join that same group — this is what turns "one landing force" into "one or a few assault groups" instead of nine independent stragglers; tuned to comfortably span the whole default landing-force beachhead spread (see spawnEnemyLandingForce in main.js), not to force every unit into a single group on every difficulty
const RALLY_RADIUS_PX = 130; // how close to the group's rally point counts as "massed" for the purposes of the commit fraction below
const RALLY_FRACTION = 0.6; // fraction of a group's LIVING members that must be massed within RALLY_RADIUS_PX before the group commits to its objective together, rather than pushing off piecemeal as each unit happens to arrive
const RALLY_TIMEOUT_S = 25; // safety valve: commit anyway once a group has spent this long trying to mass, even if the fraction was never hit (e.g. a straggler stuck on bad terrain, or losses during rally) — a group that can never quite finish massing must still eventually fight, not idle forever
const DISBAND_FRACTION = 0.34; // a group whose LIVING headcount has fallen to this fraction (or less) of its size at commit time is "largely destroyed" — its survivors look to fold into another still-active group rather than solo-push (or found a fresh assault alone) with too little force to matter

// B7 ROUT — a unit belonging to a GUTTED battalion disengages entirely: it's
// excluded from assault-group commit math (rally-fraction/disband checks below
// only ever see maneuverUnits, and routed units are filtered out of that list
// before any group bookkeeping runs) and given a straight flee order away from
// the nearest city instead of its group's assault/rally order. This is what
// makes a shattered invasion formation visibly fall apart rather than keep
// pressing a city with a remnant — the "invasion routs" payoff the morale/
// cohesion pass (B4) exists to set up.
//
// ROUT_STRENGTH_FRAC (see isUnitRouted's own comment): rout is gated on
// battalion STRENGTH, not morale — an attacking invasion owns no territory so
// its morale can only ever fall, which made a morale gate rout the whole
// invasion at full strength and hand the game away. A battalion only routs once
// its live strength drops below this fraction of its original size.
const ROUT_STRENGTH_FRAC = 0.2;
// How far a routed unit's flee order reaches, in world px — comfortably past
// CLUSTER_RADIUS_PX/RALLY_RADIUS_PX above so a routed unit visibly disengages
// the fight instead of shuffling a few steps and re-triggering combat with
// whatever it was just fighting.
const RETREAT_DISTANCE_PX = 900;

// Nearest WATER tile to a world point — naval movement can't be ordered
// onto dry land (see units.js terrainSample's requiresWater branch), so a
// naval unit's move order gets snapped the same way main.js's player-facing
// issueMoveOrder already snaps a naval click. Duplicated here (rather than
// imported from main.js, which isn't a module other code can import from)
// same spirit as game/mapgen.js's own roadAStar duplicating pathfind.js's
// cost function — small, self-contained, and simpler than threading a
// callback through.
function nearestWaterTile(map, wx, wy, maxTiles = 40) {
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

// Order a single unit toward a world point, snapping onto water first if the
// unit's move class requires it. The one place both the rally phase and the
// assault phase funnel through, so naval handling never has to be duplicated
// between them.
function orderToward(u, map, tx, ty) {
  const moveClass = MOVE_CLASSES[u.def.moveClass];
  u.order = moveClass.requiresWater ? nearestWaterTile(map, tx, ty) : { x: tx, y: ty };
}

function centroid(units) {
  let sx = 0, sy = 0;
  for (const u of units) { sx += u.x; sy += u.y; }
  return { x: sx / units.length, y: sy / units.length };
}

// How many living defenders (anything not `side` — the AI's own side) are
// standing near a candidate city right now. Used purely to bias group
// targeting toward the WEAKEST-held city rather than just the nearest one
// ("concentrating on one objective beats splitting across all of them" cuts
// both ways — no point concentrating on the most heavily-defended target
// when a softer one is available). A generous radius (wider than the city's
// own capture radius) so defenders staged just outside the exact capture
// circle still count — a smart defender garrisoning just off the objective
// shouldn't read as "undefended" to the AI.
function cityDefenseCount(world, map, side, c) {
  const { x: cx, y: cy } = cityCenterWorld(map, c);
  const r = (c.r || 6) * map.tileSize * 1.6;
  const r2 = r * r;
  let n = 0;
  for (const u of world.units) {
    if (u.side === side || u.hp <= 0) continue;
    const dd = (u.x - cx) ** 2 + (u.y - cy) ** 2;
    if (dd <= r2) n++;
  }
  return n;
}

// Best objective city for `side` (the attacking side) as seen from world
// point (wx,wy) — usually a group's current centroid, not any one unit's
// position (that's the whole point: the GROUP picks one shared target, not
// each member picking its own). Any city NOT already owned by `side` is a
// valid candidate. Null once every city on the map is already held by
// `side` (nothing left to push toward — a landing force that's swept every
// lesser city but hasn't reached the capital just sits on what it's got,
// per the task's "losing lesser cities isn't game over" framing applying in
// reverse too: taking them all isn't an automatic win either).
function pickObjectiveCity(world, map, side, wx, wy) {
  let best = null, bestScore = -Infinity;
  const cities = map.cities || [];
  for (let i = 0; i < cities.length; i++) {
    const c = cities[i];
    if (c.owner === side) continue;
    const { x: cx, y: cy } = cityCenterWorld(map, c);
    const dist = Math.hypot(cx - wx, cy - wy);
    const capitalBias = i === 0 ? CAPITAL_BIAS_PX : 0; // cities[0] is always the capital (game/mapgen.js's convention)
    // The capital is the DECISIVE objective — the only city whose loss ends
    // the war — so a competent invasion presses it EVEN THOUGH it's the best-
    // defended: the garrison is exactly what it landed to break. Only LESSER
    // cities get the "avoid defenders, pick the soft target" penalty. Applying
    // that penalty to the capital too made the AI cede the whole war — it fled
    // the garrisoned capital for undefended backwaters and never threatened
    // the one city that matters, which the balance harness caught as an
    // UNPREPARED player "winning" on normal just by turtling the capital while
    // ceding soft cities. Now the main effort goes at the capital, so keeping
    // it means actually reinforcing it — i.e. prep decides the outcome.
    const defenseBias = i === 0 ? 0 : cityDefenseCount(world, map, side, c) * DEFENSE_WEIGHT_PX;
    const score = -(dist - capitalBias + defenseBias);
    if (score > bestScore) { bestScore = score; best = c; }
  }
  return best;
}

// B7 ROUT helpers ------------------------------------------------------------
// True once a unit's own battalion (if any — loose, unbattalioned units,
// e.g. holdGround batteries or anything spawned outside a battalion, never
// rout by this mechanic and always return false) has broken. O(1) lookup
// isn't worth the bookkeeping at this game's unit counts (a landing force
// tops out in the dozens even on Brutal), so this is a plain linear find
// against world.battalions, same cost class as this file's other per-unit
// per-frame scans.
function isUnitRouted(world, u) {
  if (u.battalion == null) return false;
  const bn = world.battalions.find(b => b.id === u.battalion);
  if (!bn) return false;
  // B7 BALANCE FIX: gate rout on STRENGTH, not morale. An attacking invasion
  // owns no territory, so its battalions' morale can only ever fall (no
  // friendly-city recovery) — under a morale-broken gate every enemy battalion
  // inevitably fled at ~full strength, so the whole invasion routed off the map
  // without pressing the assault and the player won on the hold-out timer with
  // the enemy still ~fully alive (the balance harness showed unprepared winning
  // on every difficulty). A battalion now routs only once GENUINELY GUTTED —
  // its live strength has fallen below ROUT_STRENGTH_FRAC — so a fighting-fit
  // enemy battalion presses its objective exactly as the pre-battalion loose
  // landing force did, and only the shattered remnant flees (the "invasion
  // crumbles" payoff, without gifting the game away).
  return battalionStrength(bn).hpFrac < ROUT_STRENGTH_FRAC;
}

// Nearest city (any owner) to a world point — the routed unit flees AWAY
// from whichever city is closest, i.e. away from wherever the fight is
// happening, back toward the beachhead it landed on. Deliberately ignores
// ownership (unlike pickObjectiveCity): a broken unit isn't looking for a
// weak objective, it's fleeing the nearest source of fire, period.
function nearestCityWorld(map, wx, wy) {
  let best = null, bd = Infinity;
  for (const c of (map.cities || [])) {
    const { x: cx, y: cy } = cityCenterWorld(map, c);
    const d = Math.hypot(cx - wx, cy - wy);
    if (d < bd) { bd = d; best = c; }
  }
  return best;
}

// Order `u` to flee directly away from (fromX,fromY) by RETREAT_DISTANCE_PX,
// through the same orderToward() water-snapping path every other order in
// this module goes through (so a routed naval/amphibious unit still gets a
// legal destination instead of an order that silently never resolves).
function orderRetreat(u, map, fromX, fromY) {
  const dx = u.x - fromX, dy = u.y - fromY;
  const dist = Math.hypot(dx, dy) || 1; // guard the degenerate "standing exactly on the city center" case
  const tx = u.x + (dx / dist) * RETREAT_DISTANCE_PX;
  const ty = u.y + (dy / dist) * RETREAT_DISTANCE_PX;
  orderToward(u, map, tx, ty);
}

// Nearest OTHER active group to fold survivors into — "nearest" measured
// from whatever that group is actually doing right now: a committed
// (assault-phase) group's anchor is its objective city itself (join the
// ongoing push at the point of contact), a still-rallying group's anchor is
// its rally point (join the muster). Returns null if `g` is the only group
// left, in which case its survivors have nowhere to fold into and keep
// fighting under their own group (see updateEnemyAI's fold branch).
function nearestOtherGroup(map, groups, g, refUnit) {
  let best = null, bd = Infinity;
  for (const og of groups.values()) {
    if (og === g) continue;
    const anchor = (og.phase === 'assault' && og.target) ? cityCenterWorld(map, og.target) : og.rallyPoint;
    const d = Math.hypot(refUnit.x - anchor.x, refUnit.y - anchor.y);
    if (d < bd) { bd = d; best = og; }
  }
  return best;
}

// Per-unit AI scratch state, keyed by unit id (spawnUnit's ids are stable
// small ints — a plain Map is simpler than tagging state onto the unit
// object itself, and keeps this module from mutating shapes units.js owns).
// Only ever holds an entry for a maneuver unit currently (or once) assigned
// to a group — `{ groupId }`. PRUNED every call (see updateEnemyAI's top)
// for units no longer alive, so this never grows unbounded even though a
// landing force's population only ever shrinks (v1 report cleanup flag).
const aiState = new Map();

// Assault groups, keyed by a small incrementing group id. A group is:
//   { id, phase: 'rally'|'assault', rallyPoint: {x,y}, target: city|null,
//     t: seconds since formed, peakSize: living headcount at commit time }
// LIFECYCLE:
//   1. FORM — any maneuver unit with no live group joins the nearest
//      existing group within CLUSTER_RADIUS_PX of that group's rally point,
//      or founds a brand new one at its own (landing) position. This is
//      pure position-based clustering, run once per unit (subsequent frames
//      skip units that already belong to a live group) — it's what turns a
//      beachhead into "one or a few assault groups" instead of nine
//      independent wanderers.
//   2. RALLY — every living member is ordered to the group's rally point.
//      Once RALLY_FRACTION of the living membership is massed within
//      RALLY_RADIUS_PX (or RALLY_TIMEOUT_S has simply run out), the group
//      COMMITS: it picks one shared objective city and moves to phase
//      'assault'. This is the actual fix for "trickles in one at a time" —
//      the group doesn't advance on the objective until enough of it is
//      actually there to advance together.
//   3. ASSAULT — every living member is ordered at the shared objective
//      city (still via the exact same order/pathfinding fields a player
//      order uses, and still subject to units.js's own targeting peeling a
//      unit off to fight whatever it meets en route). Each frame the group
//      re-checks: has the target already been captured (owner flipped to
//      `side`)? Has the group been largely destroyed (living count fell to
//      DISBAND_FRACTION or less of its size at commit)? Either condition
//      means it needs a new plan — see RETARGET/FOLD below.
//   RETARGET — objective captured (mission complete) or never set: pick the
//      next-best city from the group's current centroid. Null result means
//      every city is already `side`'s; the group just holds where it is.
//   FOLD — group largely destroyed: rather than have 1-2 survivors solo-push
//      a fresh objective (exactly the "trickle in and die" pattern this
//      pass exists to fix, just at group scale instead of per-unit), they
//      fold into whichever other group is nearest to what it's doing right
//      now (see nearestOtherGroup) by simply reassigning their aiState
//      groupId — the host group's own logic picks them up starting next
//      tick. If there IS no other group (this was the only one left), the
//      survivors keep fighting under their own group and just retarget
//      normally; a lone unit or two is still better than freezing in place.
const groups = new Map();
let nextGroupId = 1;

// Debug/test hooks (see main.js's window.__debug.objectives) — a plain
// serializable snapshot of live group state, and the raw aiState size so a
// headless test can assert the prune above is actually keeping it bounded
// without reaching into this module's private Maps directly.
export function debugEnemyGroups() {
  const out = [];
  for (const g of groups.values()) {
    const memberIds = [];
    for (const [id, st] of aiState) if (st.groupId === g.id) memberIds.push(id);
    out.push({
      id: g.id, phase: g.phase, t: g.t,
      rallyPoint: { x: g.rallyPoint.x, y: g.rallyPoint.y },
      target: g.target ? g.target.name : null,
      memberIds,
    });
  }
  return out;
}
export function debugAiStateSize() { return aiState.size; }

// Call once per frame, DURING combat (see main.js's `combatActive` gate —
// this function does nothing dangerous if called outside combat, it just
// has nothing to do since spawnEnemyLandingForce only ever runs when combat
// begins, but main.js gates the call anyway to keep PREP's "no forced
// actions" contract obvious at the call site).
export function updateEnemyAI(world, map, dt, side = 'enemy') {
  // PRUNE dead-unit entries every call — cheap (a landing force is small and
  // bounded, no reinforcements per the task's "no economy" scope) and keeps
  // aiState from retaining stale entries for units that died frames ago
  // (v1 report's flagged cleanup gap).
  const liveIds = new Set();
  for (const u of world.units) if (u.side === side && u.hp > 0) liveIds.add(u.id);
  for (const id of aiState.keys()) if (!liveIds.has(id)) aiState.delete(id);

  // holdGround units (SAM/AA/artillery/etc — the same disposition flag
  // units.js's own steering reads) are a defensive battery doctrine, not
  // maneuver units: they dig in at the beachhead and let anything that
  // comes to them be engaged by units.js's own targeting, exactly what the
  // old scripted landing force already did. They never join a group and
  // never get an aiState entry.
  const maneuverUnits = [];
  // B7 ROUT: units whose battalion is BROKEN peel off here, before any group
  // FORM/RALLY/ASSAULT bookkeeping ever sees them — they never join
  // `maneuverUnits`, so they can't count toward a group's living headcount,
  // RALLY_FRACTION massing, or DISBAND_FRACTION "largely destroyed" checks.
  // They're ordered to flee in a separate pass right below instead.
  const routedUnits = [];
  for (const u of world.units) {
    if (u.side !== side || u.hp <= 0) continue;
    if (u.def.dispositions.includes('holdGround')) continue;
    if (isUnitRouted(world, u)) { routedUnits.push(u); continue; }
    maneuverUnits.push(u);
  }

  // Flee order for every routed unit this frame: away from the nearest city
  // (any owner — see nearestCityWorld), i.e. away from the fight, back
  // toward the beachhead. This REPLACES whatever assault/rally order the
  // unit had; it does not touch aiState/groups at all, so if the battalion
  // later recovers (morale climbs back out of 'broken'), the unit simply
  // resumes being picked up by the FORM loop below next tick, same as any
  // other unlisted maneuver unit.
  for (const u of routedUnits) {
    const nearest = nearestCityWorld(map, u.x, u.y);
    if (!nearest) { u.order = null; continue; } // no cities on the map at all (degenerate/test map) — nothing to flee from
    const { x: cx, y: cy } = cityCenterWorld(map, nearest);
    orderRetreat(u, map, cx, cy);
  }

  // FORM: any maneuver unit without a live group joins (or founds) one,
  // purely by proximity to an existing group's rally point. Processed in
  // world.units order so units that land together (the normal case — see
  // spawnEnemyLandingForce) settle into the same group within one frame.
  for (const u of maneuverUnits) {
    let st = aiState.get(u.id);
    if (!st) { st = { groupId: null }; aiState.set(u.id, st); }
    if (st.groupId != null && groups.has(st.groupId)) continue; // already belongs to a live group
    let joined = null;
    for (const g of groups.values()) {
      if (Math.hypot(u.x - g.rallyPoint.x, u.y - g.rallyPoint.y) <= CLUSTER_RADIUS_PX) { joined = g; break; }
    }
    if (!joined) {
      joined = { id: nextGroupId++, phase: 'rally', rallyPoint: { x: u.x, y: u.y }, target: null, t: 0, peakSize: 0 };
      groups.set(joined.id, joined);
    }
    st.groupId = joined.id;
  }

  // Drop any group with zero living members left (its last unit either died
  // or folded into another group this frame) — otherwise `groups` would
  // grow a dead entry per wipeout, the group-level mirror of the aiState
  // prune above.
  const membersByGroup = new Map();
  for (const u of maneuverUnits) {
    const gid = aiState.get(u.id).groupId;
    let arr = membersByGroup.get(gid);
    if (!arr) { arr = []; membersByGroup.set(gid, arr); }
    arr.push(u);
  }
  for (const gid of groups.keys()) if (!membersByGroup.has(gid)) groups.delete(gid);

  for (const g of groups.values()) {
    g.t += dt;
    const members = membersByGroup.get(g.id);
    if (!members || !members.length) continue; // defensive; prune above should already have removed this

    if (g.phase === 'rally') {
      let atRally = 0;
      for (const u of members) {
        const d = Math.hypot(u.x - g.rallyPoint.x, u.y - g.rallyPoint.y);
        if (d <= RALLY_RADIUS_PX) { u.order = null; atRally++; } // massed: hold rather than jostle in place
        else orderToward(u, map, g.rallyPoint.x, g.rallyPoint.y);
      }
      const massed = members.length === 1 || (atRally / members.length) >= RALLY_FRACTION;
      if (massed || g.t >= RALLY_TIMEOUT_S) {
        // COMMIT: pick one shared objective from the group's current
        // centroid and push together, per this module's whole reason for
        // existing — no per-member objective picking past this point.
        g.phase = 'assault';
        g.peakSize = members.length;
        const c0 = centroid(members);
        g.target = pickObjectiveCity(world, map, side, c0.x, c0.y);
      }
      continue;
    }

    // --- assault phase ---
    const targetCaptured = g.target && g.target.owner === side;
    const largelyDestroyed = members.length <= (g.peakSize || members.length) * DISBAND_FRACTION;

    if (largelyDestroyed) {
      const host = nearestOtherGroup(map, groups, g, members[0]);
      if (host) {
        for (const u of members) aiState.get(u.id).groupId = host.id;
        continue; // folded into `host` — it takes over ordering them next tick
      }
      // no other group to fold into: keep fighting under this group, just
      // fall through to the normal retarget/order logic below with whatever
      // is left.
    }
    if (!g.target || targetCaptured) {
      const c0 = centroid(members);
      g.target = pickObjectiveCity(world, map, side, c0.x, c0.y);
    }
    if (!g.target) { for (const u of members) u.order = null; continue; } // every city already `side`'s

    const c = g.target;
    const { x: cx, y: cy } = cityCenterWorld(map, c);
    const arriveR = (c.r || 6) * map.tileSize * ARRIVE_FRACTION;
    for (const u of members) {
      const d = Math.hypot(u.x - cx, u.y - cy);
      if (d <= arriveR) {
        // Arrived: HOLD to feed the capture mechanic (game/objectives.js
        // counts ground-unit presence within the city's FULL radius, which
        // arriveR sits comfortably inside of). Clear any stale order so
        // units.js's own idle/hover behavior takes over instead of
        // continually re-pathing to the exact center point — that would
        // have units jostling/orbiting the goal rather than sitting still
        // while capture progress accrues.
        u.order = null;
        continue;
      }
      orderToward(u, map, cx, cy);
    }
  }
}

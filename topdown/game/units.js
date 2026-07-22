// Physical units + projectiles for the top-down slice. Movement is
// acceleration/steering based (not teleport-to-target) and weapons fire real
// projectiles that travel over time — same physics feel as the original game,
// just on a 2D plane instead of a side-view strip.
//
// This is the ATTRIBUTE/LAYER UNIT SYSTEM (CONCEPT.md Pillar 4): unit
// behavior is composable data — domain, movement class, weapon, terrain
// affinities, targeting flags, AI dispositions — not per-unit code. A new
// unit type is a new UNIT_DEFS entry; updateUnits/fireProjectile never
// branch on a unit's *name*, only on the shape of its attributes.

import { detects } from './fog.js';
import { findPathCached } from './pathfind.js';

// ---------------------------------------------------------------------------
// MOVEMENT CLASSES — how a unit's domain interacts with terrain.
//
// Ground classes (foot/wheeled/tracked) layer a per-class multiplier on top
// of the terrain's own moveMult: effective speed = def.speed * terrainMult *
// classMod. classMod > 1 means this class handles that terrain BETTER than
// the terrain's base penalty implies (e.g. foot in marsh: terrain moveMult
// 0.4 means a flat 60% penalty for a generic mover, but foot's marsh
// classMod of 1.3 claws back a third of that penalty — foot effectively
// crosses marsh at ~0.8x of the terrain's raw penalty). classMod < 1 means
// this class does WORSE than the terrain's base number suggests (wheeled in
// marsh: classMod 0.2 turns a 60%-penalty tile into a near-crawl). Terrain
// types with no entry in a class's terrainMods use classMod 1 (trust the
// terrain's own number as-is) — grass never needs an entry because its
// moveMult is already 1 (no penalty to modify).
//
// `ignoresTerrain` (air): terrain is never sampled at all, effective speed
// is always def.speed.
// `requiresWater` (naval): the INVERSE of every ground class — passable only
// on tiles flagged `water` in terrain-defs, impassable everywhere else, and
// moves at full def.speed while on water (the terrain's own moveMult, which
// encodes "0 = impassable to ground movers", is not applied to naval at all
// since water having moveMult 0 means something entirely different to a
// boat than to a tank). Naval ignores the road overlay entirely — it has no
// bearing on a hull that's already in open water.
//
// `roadMult` (ground only, P2): a road tile OVERRIDES the terrain's own
// moveMult + classMod outright — a paved road flattens whatever's under it,
// including a river (that's a bridge). `ground: true` marks the three
// classes pathfinding/road-building are allowed to route for; naval/air
// don't path (see game/pathfind.js). Wheeled benefits most (that's the
// point — roads exist for trucks), tracked next, foot least (legs don't
// care much whether the ground is paved).
export const MOVE_CLASSES = {
  foot: {
    ground: true,
    terrainMods: { marsh: 1.3, forest: 1.55 },
    roadMult: 1.15,
  },
  wheeled: {
    ground: true,
    terrainMods: { marsh: 0.2, forest: 0.27, hills: 0.8, urban: 1.2, sand: 0.8 },
    roadMult: 1.6,
  },
  tracked: {
    ground: true,
    terrainMods: { marsh: 0.7, forest: 0.9, hills: 1.05 },
    roadMult: 1.3,
  },
  air: {
    ignoresTerrain: true,
  },
  naval: {
    requiresWater: true,
  },
};

// Sample a move class's terrain interaction at a world point. Returns
// { mult, passable } — mult scales top speed, passable gates whether a unit
// may occupy that point at all (mult 0 does not necessarily mean impassable
// for a ground class in principle, but in practice every terrain type with
// moveMult 0 — water, mountain — is meant to be a hard wall for ground).
//
// Exported so game/pathfind.js's A* cost function reuses this EXACT rule —
// movement and pathfinding must never diverge, or units would route through
// tiles they then can't actually walk (or vice versa: avoid tiles that were
// really fine).
export function terrainSample(map, moveClass, wx, wy) {
  if (!map || moveClass.ignoresTerrain) return { mult: 1, passable: true };
  if (moveClass.requiresWater) {
    const t = map.terrainAtWorld(wx, wy);
    if (!t) return { mult: 1, passable: true }; // off the edge of the map: don't block on it here
    const passable = !!t.water;
    return { mult: passable ? 1 : 0, passable };
  }
  // OBSTACLE LAYER (P2 city layer): a building footprint is a hard wall for
  // every ground class, unconditionally — checked before the road override
  // below so a (rules-forbidden but not code-enforced-elsewhere) building
  // sitting on a road tile still blocks rather than inheriting the road's
  // free-flow multiplier.
  if (map.blockAt && map.blockAt(Math.floor(wx / map.tileSize), Math.floor(wy / map.tileSize))) {
    return { mult: 0, passable: false };
  }
  const t = map.terrainAtWorld(wx, wy);
  if (!t) return { mult: 1, passable: true };
  // ROAD OVERRIDE: a road (or bridge, if the tile under it is a river) beats
  // the terrain's own number entirely. Open sea can never legally carry a
  // road (enforced at construction time in genmap.js and the R-mode router
  // in main.js), but the water-that-isn't-a-river guard here is a cheap
  // belt-and-braces check so a corrupt/hand-edited map can't produce a
  // walk-on-water bug.
  if (moveClass.roadMult && map.roadAtWorld && map.roadAtWorld(wx, wy)) {
    const blockedWater = t.water && t.name !== 'river';
    if (!blockedWater) return { mult: moveClass.roadMult, passable: true };
  }
  const base = Math.max(0, t.moveMult ?? 1);
  const classMod = moveClass.terrainMods?.[t.name] ?? 1;
  const mult = Math.max(0, base * classMod);
  return { mult, passable: mult > 0 };
}
function isPassable(map, moveClass, wx, wy) {
  return terrainSample(map, moveClass, wx, wy).passable;
}

// ---------------------------------------------------------------------------
// WEAPONS — projectile physics + targeting attributes, referenced by name
// from a unit def. dmg/range/rate stay on the UNIT (two units can share a
// weapon type but hit differently), the weapon def owns everything about how
// the projectile flies and what domains it's allowed to lock onto.
export const WEAPON_DEFS = {
  shell: {
    kind: 'ballistic', speed: 260, arcVisual: true,
    canTargetGround: true, canTargetAir: false,
  },
  aam: {
    // air-intercept missile: homing, air-only — an interceptor cannot strike ground
    kind: 'homing', speed: 60, maxSpeed: 420, thrust: 650, turnRate: 5, life: 5,
    canTargetGround: false, canTargetAir: true,
  },
  sam: {
    // surface-to-air missile: homing, longer reach + slower turn than an AAM
    kind: 'homing', speed: 50, maxSpeed: 380, thrust: 420, turnRate: 2.6, life: 7,
    canTargetGround: false, canTargetAir: true,
  },
  autocannon: {
    // fast small tracer-like rounds, short range, engages both domains
    kind: 'gun', speed: 520, canTargetGround: true, canTargetAir: true,
  },
  rocket: {
    // CAS ordnance: homing, ground-only — cannot reach airborne targets
    kind: 'homing', speed: 80, maxSpeed: 300, thrust: 500, turnRate: 3.5, life: 4,
    canTargetGround: true, canTargetAir: false,
  },

  // -------------------------------------------------------------------------
  // GROUNDED NEAR-FUTURE ARSENAL (CONCEPT.md's settled "Arsenal scope & the
  // tech ceiling" paragraph — conventional through present-day/bleeding-edge
  // real-world lineage, no sci-fi). Every entry below REUSES one of the three
  // projectile physics kinds already implemented above (ballistic/homing/
  // gun) — nothing here needed a fourth kind. What makes each of these a
  // distinct real-world system is entirely in the numbers and the
  // canTargetAir/canTargetGround flags, exactly per the file's cardinal rule.
  cruise: {
    // long-range land-attack cruise missile — CONCEPT.md names this
    // explicitly ("precision cruise missiles (long-range strike)"). Same
    // 'homing' physics as rocket/atgm/etc, just tuned for ENDURANCE over
    // agility: a long `life` (it has to survive a long flight to a distant
    // target) and a low turnRate (a cruise missile flies a route and makes a
    // terminal correction, it doesn't dogfight). Ground-only.
    kind: 'homing', speed: 90, maxSpeed: 260, thrust: 300, turnRate: 1.8, life: 12,
    canTargetGround: true, canTargetAir: false,
  },
  ballisticMissile: {
    // short/medium-range ballistic missile launcher. Reuses 'shell's exact
    // 'ballistic' physics (straight-line flight timed by distance/speed,
    // with a cosmetic lofted arc) rather than 'homing' — a real ballistic
    // missile is unguided/unpowered after the boost phase, so the
    // "straight line to a precomputed impact point" model fits it better
    // than a missile that steers at its target every frame. Much faster
    // than a shell (long real-world range covered in a short flight time).
    kind: 'ballistic', speed: 480, arcVisual: true,
    canTargetGround: true, canTargetAir: false,
  },
  hypersonic: {
    // the bleeding-edge tier CONCEPT.md calls out by name ("up through the
    // bleeding edge of real-world lineage — hypersonics"). Identical shape
    // to ballisticMissile — same 'ballistic' physics kind — just drastically
    // faster: the entire point of a hypersonic weapon is compressing the
    // flight-time window an air-defense umbrella has to react in, which the
    // ballistic kind's speed field already models directly (t = dist/speed).
    kind: 'ballistic', speed: 900, arcVisual: true,
    canTargetGround: true, canTargetAir: false,
  },
  atgm: {
    // anti-tank guided missile (infantry ATGM team). Homing, ground-only,
    // short range/life and a sharp turnRate with high thrust — a team
    // engaging armor at a few hundred px, not a standoff strike weapon.
    kind: 'homing', speed: 70, maxSpeed: 260, thrust: 550, turnRate: 4.2, life: 3,
    canTargetGround: true, canTargetAir: false,
  },
  manpads: {
    // MANPADS (shoulder-fired SAM team) — the infantry mirror of atgm above:
    // homing, short-legged, sharp turn. THE flag that matters here is
    // canTargetGround: false — a MANPADS physically cannot engage a ground
    // target, and getting this flag wrong would let an infantry AA team
    // shell tanks, defeating the entire point of modeling it as a distinct
    // real-world system rather than a reskinned autocannon.
    kind: 'homing', speed: 90, maxSpeed: 340, thrust: 600, turnRate: 4.8, life: 3,
    canTargetGround: false, canTargetAir: true,
  },
  loitering: {
    // loitering munition ("suicide drone"): homing, ground-only, with a much
    // longer `life` than rocket/atgm — the real thing loiters over a search
    // area before committing to its terminal dive. Modeled here simply as
    // "stays airborne a long time before it must expire" rather than a real
    // search/loiter state machine, which is out of scope for this pass.
    kind: 'homing', speed: 55, maxSpeed: 200, thrust: 250, turnRate: 3, life: 9,
    canTargetGround: true, canTargetAir: false,
  },
  antiship: {
    // anti-ship missile. Flagged canTargetGround (not a new "canTargetNaval"
    // flag) because enemyEntities/weaponCanTarget above already treat naval
    // as "ground" for targeting purposes — so this legitimately threatens
    // warships AND anything on land, same as a real anti-ship missile can be
    // used in a land-attack role. Faster/longer-legged than an ATGM
    // (sea-skimming standoff range), shorter-lived than a cruise missile
    // (shorter real-world range class than a land-attack cruise missile).
    kind: 'homing', speed: 100, maxSpeed: 360, thrust: 500, turnRate: 2.2, life: 6,
    canTargetGround: true, canTargetAir: false,
  },
  directedEnergy: {
    // early directed-energy point-defense. Reuses the 'gun' physics kind
    // (straight-line burst, short life, no homing) at an extreme speed — a
    // laser doesn't fly like a missile, it's effectively instantaneous at
    // point-defense ranges, and 'gun's straight-line model already IS that
    // shape with no new physics kind needed (per the task brief: "model as a
    // fast gun-kind"). Air-only and deliberately NOT dual-purpose like
    // autocannon — this is a specialist anti-air/anti-drone interceptor.
    kind: 'gun', speed: 1400, canTargetGround: false, canTargetAir: true,
  },
};

// naval counts as "ground" for targeting purposes for now (P1.5 scope) — a
// weapon's canTargetGround flag governs anything that isn't domain 'air'.
// Buildings (game/buildings.js) carry no `domain` at all, which falls
// through to the same "ground" branch — a factory is exactly as much a
// ground target as a tank is, never an air one.
function weaponCanTarget(wdef, target) {
  if (!wdef) return false;
  return target.def.domain === 'air' ? !!wdef.canTargetAir : !!wdef.canTargetGround;
}

// Combined pool of everything on the OTHER side that a unit or building can
// possibly shoot at or be shot by: live enemy units plus live enemy
// buildings (game/buildings.js — pushed onto world.buildings, never
// world.units, since buildings don't move/steer/path). Centralizing this
// here means pickTarget/nearestEnemy/resolveHit — and by extension every
// weapon in the game, including a gun emplacement's own fire loop in
// game/buildings.js which calls pickTarget/nearestEnemy directly — treat a
// building as just another target shape rather than needing a parallel
// "or check world.buildings too" at every call site.
function enemyEntities(world, side) {
  const out = [];
  for (const o of world.units) if (o.side !== side && o.hp > 0) out.push(o);
  for (const b of (world.buildings || [])) if (b.side !== side && b.hp > 0) out.push(b);
  return out;
}

// ---------------------------------------------------------------------------
// UNIT ROSTER — pure data. Every field here is a composable attribute; there
// is no per-unit-name branch anywhere in the update/fire code below.
//
// `vision`: sensor range in world px, consumed by game/fog.js — a unit's
// SIDE detects an enemy within vision * (1 - target's terrain concealment).
// Scout is the recon specialist (highest ground vision, its whole job);
// air sees farthest of all; SAM carries real radar reach; infantry/militia
// are modest (eyes and small optics, not sensors); naval sits in between.
export const UNIT_DEFS = {
  infantry: {
    name: 'Infantry Squad', domain: 'ground', moveClass: 'foot', weapon: 'autocannon',
    hp: 70, armor: 2, speed: 40, range: 140, dmg: 4, rate: 0.22, turnRate: 6, radius: 6,
    vision: 200,
    dispositions: ['aggressive'], // closes to point-blank instead of standing off
  },
  militia: {
    // deliberately weaker/cheaper/shorter-legged than infantry — later phases
    // auto-muster this exact def as the garrison unit, so it stays a distinct
    // entry rather than a reskin (foot moveClass still gives it the same
    // rough-terrain edge infantry gets, just from a worse baseline).
    name: 'Militia Levy', domain: 'ground', moveClass: 'foot', weapon: 'autocannon',
    hp: 40, armor: 1, speed: 36, range: 90, dmg: 2, rate: 0.3, turnRate: 5, radius: 6,
    vision: 180,
    dispositions: ['aggressive'],
  },
  scout: {
    name: 'Scout Car "Whippet"', domain: 'ground', moveClass: 'wheeled', weapon: 'autocannon',
    hp: 35, armor: 1, speed: 130, range: 100, dmg: 2, rate: 0.3, turnRate: 8, radius: 7,
    vision: 380, // highest vision on the ground — recon is its whole job
    dispositions: ['recon'],
  },
  tank: {
    name: 'MBT "Aegis"', domain: 'ground', moveClass: 'tracked', weapon: 'shell',
    hp: 110, armor: 8, speed: 70, range: 220, dmg: 18, rate: 1.6, turnRate: 3, radius: 10,
    vision: 260,
    dispositions: [],
  },
  artillery: {
    // P4 production-chain follow-up (CONCEPT.md's settled "Production &
    // supply chain" ledger names "artillery" as one of the illustrative
    // categories alongside tanks/aircraft/warships) — a tracked, long-range
    // ground shell lobber. Reuses the same 'shell' ballistic weapon a tank
    // carries (no new projectile physics needed) but trades the tank's
    // armor/mobility for reach: much longer range and harder-hitting per
    // shot, at less than half the tank's HP and a slower turret. holdGround
    // like SAM/AA — it's a battery that reaches out, not a unit that closes.
    name: 'Artillery "Culverin"', domain: 'ground', moveClass: 'tracked', weapon: 'shell',
    hp: 65, armor: 3, speed: 40, range: 340, dmg: 26, rate: 1.1, turnRate: 2, radius: 9,
    vision: 220,
    dispositions: ['holdGround'],
  },
  sam: {
    name: 'SAM Battery "Warden"', domain: 'ground', moveClass: 'tracked', weapon: 'sam',
    hp: 90, armor: 4, speed: 30, range: 480, dmg: 34, rate: 2.4, turnRate: 2, radius: 11,
    vision: 360, // decent radar reach
    dispositions: ['holdGround'], // a battery — never chases, just engages what enters its umbrella
  },
  aa: {
    name: 'AA Gun "Bristle"', domain: 'ground', moveClass: 'tracked', weapon: 'autocannon',
    hp: 85, armor: 5, speed: 55, range: 190, dmg: 6, rate: 0.35, turnRate: 4, radius: 10,
    vision: 300,
    dispositions: ['holdGround'],
  },
  fighter: {
    name: 'Fighter "Kestrel"', domain: 'air', moveClass: 'air', weapon: 'aam',
    hp: 60, armor: 3, speed: 220, range: 260, dmg: 12, rate: 0.9, turnRate: 5, radius: 8,
    vision: 420, // air sees farthest of all
    dispositions: [],
  },
  strikejet: {
    name: 'Strike Jet "Warthog"', domain: 'air', moveClass: 'air', weapon: 'rocket',
    hp: 70, armor: 4, speed: 190, range: 190, dmg: 20, rate: 1.3, turnRate: 4, radius: 9,
    vision: 400,
    dispositions: ['aggressive'], // CAS run: gets in close instead of standing off
  },
  gunboat: {
    name: 'Gunboat "Osprey"', domain: 'naval', moveClass: 'naval', weapon: 'shell',
    hp: 130, armor: 6, speed: 60, range: 240, dmg: 16, rate: 1.8, turnRate: 2.2, radius: 13,
    vision: 300,
    dispositions: [],
  },
  destroyer: {
    // longer-legged gun and a lot more hull than the gunboat — the ship you
    // want screening the coast, not just harassing it.
    name: 'Destroyer "Marchioness"', domain: 'naval', moveClass: 'naval', weapon: 'shell',
    hp: 220, armor: 10, speed: 50, range: 320, dmg: 22, rate: 2.0, turnRate: 1.6, radius: 16,
    vision: 340,
    dispositions: [],
  },

  // ---------------------------------------------------------------------
  // GROUNDED NEAR-FUTURE ROSTER (CONCEPT.md's settled "Arsenal scope & the
  // tech ceiling" paragraph). Stats are picked to sit inside the SAME scale
  // the roster above already uses (hp roughly 10-220, dmg roughly 2-40,
  // range roughly 90-620) rather than inventing a new tier — the newest,
  // priciest systems here (hypersonic launcher) sit at the top of that
  // existing range, they don't blow past it. None of these are
  // research-gated yet (that's explicitly the NEXT agent's job); they're
  // plain UNIT_DEFS entries, producible like everything else already is.

  // --- Drones (air domain, unarmed recon through armed/expendable types) ---
  reconDrone: {
    // pure recon UAV: no `weapon` at all (same "no weapon field" shape a
    // building like Radar Station uses) — pickTarget/nearestEnemy both
    // resolve WEAPON_DEFS[undefined] to nothing and simply never find a
    // target for it, so it flies its orders and never fires, no special
    // casing required anywhere in the update loop. Highest vision in the
    // entire roster (a flying scout, its whole job) — beats even the
    // Fighter's radar-assisted 420 and the Scout Car's ground-recon 380.
    name: 'Recon UAV "Sentinel"', domain: 'air', moveClass: 'air',
    hp: 20, armor: 0, speed: 150, range: 0, dmg: 0, rate: 1, turnRate: 6, radius: 5,
    vision: 560,
    dispositions: ['recon'],
  },
  ucav: {
    // armed drone / UCAV: ground-attack, reuses the 'rocket' CAS weapon
    // strikejet already carries — an armed drone and a manned strike jet
    // fire the same kind of homing ground ordnance in the real world, so no
    // new weapon behavior is needed, just a cheaper/frailer/slower airframe.
    name: 'UCAV "Talon"', domain: 'air', moveClass: 'air', weapon: 'rocket',
    hp: 45, armor: 2, speed: 175, range: 170, dmg: 14, rate: 1.1, turnRate: 4.5, radius: 8,
    vision: 380,
    dispositions: ['aggressive'], // gets in close for its strike run, like strikejet
  },
  loiteringMunition: {
    // cheap, "one-shot-ish" loitering munition. There's no real per-unit
    // "consumed on fire" mechanic in this pass (would need new state on the
    // unit + a fire-time removal hook) — approximated instead with a very
    // slow rate (it doesn't wait around to reload after committing) and low
    // HP (a light expendable airframe, dies to almost anything that hits
    // back), which reads the same in practice: it gets one real strike in
    // before it's likely dead or has used its shot.
    name: 'Loitering Munition "Wraith"', domain: 'air', moveClass: 'air', weapon: 'loitering',
    hp: 14, armor: 0, speed: 130, range: 230, dmg: 26, rate: 2.6, turnRate: 3.5, radius: 5,
    vision: 260,
    dispositions: ['aggressive'],
  },
  droneSwarm: {
    // drone swarm: the "edge of modern" bullet from CONCEPT.md's arsenal
    // list. Modeled as a single very cheap, very weak airframe meant to be
    // PRODUCED in bulk (see game/production.js's drones recipe) rather than
    // a unit that spawns multiple bodies per build — lowest hp/dmg/radius in
    // the roster, reusing autocannon (already dual air+ground) so a swarm
    // can harass whatever it reaches.
    name: 'Drone Swarm "Midge"', domain: 'air', moveClass: 'air', weapon: 'autocannon',
    hp: 12, armor: 0, speed: 165, range: 100, dmg: 3, rate: 0.22, turnRate: 7, radius: 4,
    vision: 200,
    dispositions: ['aggressive'],
  },

  // --- Rocket & missile artillery (ground, long-reach bombardment) ---
  mlrs: {
    // MLRS / rocket artillery: area ground bombardment. Reuses the 'shell'
    // ballistic weapon the tank/artillery/gunboat/destroyer already share
    // (same precedent — a rocket salvo's flight-time-to-impact shape is the
    // same "straight line, timed by distance/speed" as a tube shell) rather
    // than adding a new weapon behavior purely for this. Wheeled (a truck-
    // mounted launcher, not tracked like Culverin) — faster, thinner-skinned,
    // fires quicker but hits softer per shot than tube artillery, and a
    // battery doctrine (holdGround) exactly like Culverin and the SAM.
    name: 'MLRS "Barrage"', domain: 'ground', moveClass: 'wheeled', weapon: 'shell',
    hp: 55, armor: 1, speed: 85, range: 300, dmg: 20, rate: 0.5, turnRate: 3, radius: 9,
    vision: 200,
    dispositions: ['holdGround'],
  },
  srbmLauncher: {
    // short/medium-range ballistic missile launcher (SRBM/MRBM lineage).
    name: 'SRBM Launcher "Reach"', domain: 'ground', moveClass: 'wheeled', weapon: 'ballisticMissile',
    hp: 60, armor: 2, speed: 60, range: 560, dmg: 45, rate: 3.5, turnRate: 2, radius: 10,
    vision: 220,
    dispositions: ['holdGround'],
  },
  cruiseLauncher: {
    // long-range precision cruise missile launcher — v1's "strike systems"
    // lineage per the task brief. Longest ground-attack range in the
    // pre-hypersonic roster; slow reload reflects a real launcher's salvo
    // size/rearm time, not a rapid-fire battery.
    name: 'Cruise Missile Launcher "Longhand"', domain: 'ground', moveClass: 'wheeled', weapon: 'cruise',
    hp: 58, armor: 2, speed: 55, range: 620, dmg: 42, rate: 4.0, turnRate: 2, radius: 10,
    vision: 230,
    dispositions: ['holdGround'],
  },
  hypersonicLauncher: {
    // the bleeding-edge top of the whole roster: longest range, hardest hit,
    // slowest reload of anything here — deliberately the most expensive
    // thing to mass (see game/production.js's hypersonics recipe), matching
    // CONCEPT.md's "big bonuses behind big gates" tech philosophy even
    // though the actual gate isn't built yet (this agent's task explicitly
    // leaves that to the next one; for now it's just a pricey unit to build).
    name: 'Hypersonic Launcher "Zenith"', domain: 'ground', moveClass: 'wheeled', weapon: 'hypersonic',
    hp: 65, armor: 3, speed: 50, range: 760, dmg: 70, rate: 6.0, turnRate: 1.5, radius: 11,
    vision: 240,
    dispositions: ['holdGround'],
  },

  // --- Guided infantry teams & modern air defense (ground) ---
  atgmTeam: {
    // ATGM team: foot infantry, holdGround (a prepared ambush position, not
    // a unit that chases armor into the open) — anti-armor homing ground
    // fire from a squad-sized footprint, much cheaper and shorter-legged
    // than a vehicle-mounted line.
    name: 'ATGM Team "Lance"', domain: 'ground', moveClass: 'foot', weapon: 'atgm',
    hp: 50, armor: 1, speed: 36, range: 260, dmg: 30, rate: 1.7, turnRate: 5, radius: 6,
    vision: 220,
    dispositions: ['holdGround'],
  },
  manpadsTeam: {
    // MANPADS team: the short-range, foot-mobile answer to air threats —
    // sits between AA Gun (autocannon, dual air+ground, longer legs) and a
    // full SAM battery in both role and cost, per the manpads weapon's
    // air-only flag.
    name: 'MANPADS Team "Bracer"', domain: 'ground', moveClass: 'foot', weapon: 'manpads',
    hp: 45, armor: 1, speed: 36, range: 210, dmg: 22, rate: 1.9, turnRate: 5, radius: 6,
    vision: 240,
    dispositions: ['holdGround'],
  },
  samUpgrade: {
    // modern layered SAM upgrade: reuses the EXISTING 'sam' weapon
    // unchanged (CONCEPT.md's arsenal list doesn't call for a new SAM
    // projectile behavior, just "longer reach") — the upgrade is entirely in
    // this unit's own range/dmg/vision numbers being pushed past the
    // original SAM Battery "Warden" (range 480, dmg 34, vision 360), the
    // same way Artillery "Culverin" already out-ranges the tank on the same
    // 'shell' weapon. A real layered-defense battery, not a new projectile.
    name: 'SAM Battery "Palisade"', domain: 'ground', moveClass: 'tracked', weapon: 'sam',
    hp: 100, armor: 5, speed: 28, range: 620, dmg: 40, rate: 2.6, turnRate: 2, radius: 11,
    vision: 520,
    dispositions: ['holdGround'],
  },
  antiShipBattery: {
    // anti-ship missile battery: per the antiship weapon's comment, "ground"
    // targeting already covers naval, so this threatens warships and land
    // targets alike from a coastal battery position.
    name: 'Anti-Ship Battery "Harrow"', domain: 'ground', moveClass: 'wheeled', weapon: 'antiship',
    hp: 70, armor: 2, speed: 45, range: 520, dmg: 38, rate: 2.2, turnRate: 2.4, radius: 10,
    vision: 280,
    dispositions: ['holdGround'],
  },
  pointDefenseLaser: {
    // early directed-energy point-defense: very short range but near-instant
    // (see the directedEnergy weapon's huge `speed`) and a very fast rate —
    // a last-ditch interceptor against drones/missiles/aircraft that get
    // close, not a standoff weapon. Air-only.
    name: 'Point-Defense Laser "Halo"', domain: 'ground', moveClass: 'tracked', weapon: 'directedEnergy',
    hp: 75, armor: 4, speed: 45, range: 160, dmg: 8, rate: 0.12, turnRate: 5, radius: 9,
    vision: 260,
    dispositions: ['holdGround'],
  },

  // --- Support (ground) ---
  ewJammer: {
    // EW / jammer support unit: per the task brief, a real "reduce enemy
    // detection" jamming EFFECT would be more than a small data field (it'd
    // need a new per-side detection-modifier hook in game/fog.js), so this
    // stays a plain recon/support unit with no weapon and very high vision —
    // exactly the reconDrone pattern above, just ground-mobile instead of
    // airborne, standing in for "signals/EW company" rather than a flying
    // sensor.
    //
    // TODO(jamming): when game/fog.js grows a per-side "reduce ENEMY sensor
    // range within radius" hook, wire it in here (e.g. a `jamRadius`/
    // `jamPower` field read by detects()/computeFog alongside vision) — this
    // unit is deliberately left as a clean, effect-free placeholder for that
    // hook rather than a half-built jamming subsystem.
    name: 'EW Jammer "Raven"', domain: 'ground', moveClass: 'wheeled',
    hp: 50, armor: 1, speed: 70, range: 0, dmg: 0, rate: 1, turnRate: 4, radius: 8,
    vision: 480,
    dispositions: ['recon'],
  },
};

let nextId = 1;

export function spawnUnit(world, key, x, y, side) {
  const def = UNIT_DEFS[key];
  const u = {
    id: nextId++, key, def, side, x, y, vx: 0, vy: 0,
    hp: def.hp, maxHp: def.hp, aim: 0, cd: Math.random() * def.rate,
    target: null, order: null, // order = {x,y} move destination, set by player input
    // turretAim is the weapon's own facing (see updateUnits), decoupled
    // from u.aim (hull/movement facing) so a stopped unit still swings its
    // gun onto target. muzzleFlash is the fire-event hook a visual pass
    // reads for muzzle flash/recoil — {t: seconds since this shot, angle:
    // turretAim at the moment of firing} — and is aged out / cleared
    // automatically below.
    turretAim: 0, muzzleFlash: null,
    // A* order-following state (ground move classes only — see ensurePath).
    // pathPrevX/Y is the start of the CURRENT leg (the previous waypoint, or
    // the unit's position when it joined the route) — used by advancePath's
    // "passed the waypoint" projection check below.
    path: null, pathIdx: 0, pathGoalTileX: undefined, pathGoalTileY: undefined, pathMapVersion: -1,
    pathPrevX: x, pathPrevY: y,
  };
  world.units.push(u);
  return u;
}

// Fallback radius for advancing off a waypoint when the projection check
// below doesn't apply (e.g. the final waypoint, or a near-zero-length leg).
// Deliberately generous — see advancePath's comment for why a tight radius
// causes fast/slow-turning units to orbit instead of converging.
const WAYPOINT_RADIUS = 20;

// Keep a ground unit's A* route current. Cheap staleness check every frame
// (a few field comparisons) rather than repathing every frame: only
// recomputes when the order's destination TILE changes (not on every pixel
// of jitter) or when the map's road layer changed (map.version — a fresh
// road can shorten an in-flight unit's route). findPathCached does the
// actual coalescing across units sharing a goal; this function's own job is
// just picking THIS unit's entry point onto whatever route comes back.
function ensurePath(u, map, moveClassName, moveClass) {
  const goalTileX = Math.floor(u.order.x / map.tileSize);
  const goalTileY = Math.floor(u.order.y / map.tileSize);
  const stale = !u.path || u.pathGoalTileX !== goalTileX || u.pathGoalTileY !== goalTileY || u.pathMapVersion !== map.version;
  if (!stale) return;
  const path = findPathCached(map, moveClassName, moveClass, u.x, u.y, u.order.x, u.order.y);
  u.path = path;
  u.pathGoalTileX = goalTileX; u.pathGoalTileY = goalTileY; u.pathMapVersion = map.version;
  u.pathPrevX = u.x; u.pathPrevY = u.y; // this leg starts from wherever the unit is right now
  if (path && path.length) {
    // Merge onto the (possibly shared/cached) route at whichever waypoint
    // is nearest to THIS unit rather than restarting from wherever the
    // solving unit began — the "per-unit offset" half of path coalescing.
    let bi = 0, bd = Infinity;
    for (let i = 0; i < path.length; i++) {
      const dd = (path[i].x - u.x) ** 2 + (path[i].y - u.y) ** 2;
      if (dd < bd) { bd = dd; bi = i; }
    }
    u.pathIdx = bi;
  } else {
    u.pathIdx = 0;
  }
}

// Should the unit advance from its current waypoint to the next? Proximity
// alone (Math.hypot < radius) is NOT enough: a unit whose turning radius is
// bigger than that proximity threshold (fast + slow-turning, e.g. the tank —
// speed 70, turnRate 3) physically cannot converge onto the exact point and
// instead sweeps past it forever, tracing a stable orbit around the
// waypoint that never satisfies the radius check (verified empirically —
// this was a real bug during development, not a hypothetical). The fix is
// the standard pure-pursuit one: also advance once the unit's position has
// PROJECTED past the waypoint along the leg's direction, regardless of how
// far off to the side it is — this lets a fast unit cut the corner and
// commit to the next leg's heading instead of trying to thread the needle.
function advancePath(u, wp) {
  const dx = u.x - u.pathPrevX, dy = u.y - u.pathPrevY;
  const segX = wp.x - u.pathPrevX, segY = wp.y - u.pathPrevY;
  const segLen2 = segX * segX + segY * segY;
  const projectedPast = segLen2 > 1 && (dx * segX + dy * segY) >= segLen2;
  const close = Math.hypot(wp.x - u.x, wp.y - u.y) < WAYPOINT_RADIUS;
  if (projectedPast || close) {
    u.pathPrevX = wp.x; u.pathPrevY = wp.y;
    u.pathIdx++;
    return true;
  }
  return false;
}

// unbounded seek: a unit needs to know the nearest enemy ANYWHERE on the map
// so it advances toward the fight, not just enemies already within gun range
// (which was a real bug — units 1400px apart never had a reason to move).
// Respects the unit's weapon targeting flags: a SAM never even considers a
// tank a candidate, so with no valid-domain enemy on the field it falls
// through to order/idle behavior instead of chasing something it can't hit.
//
// FOG: also gated on detection — a unit only seeks enemies its SIDE currently
// detects (game/fog.js). No beelining toward hidden units; with nothing
// visible this falls through to order/idle behavior, same as "no enemy at
// all" used to.
export function nearestEnemy(world, u) {
  const wdef = WEAPON_DEFS[u.def.weapon];
  let best = null, bd = Infinity;
  for (const o of enemyEntities(world, u.side)) {
    if (!weaponCanTarget(wdef, o)) continue;
    if (!detects(u.side, o)) continue;
    const dd = (o.x - u.x) ** 2 + (o.y - u.y) ** 2;
    if (dd < bd) { bd = dd; best = o; }
  }
  return best;
}

// FIRE DISCIPLINE: track committed (in-flight) damage per target so units
// don't all dogpile the same nearly-dead unit — the #1 non-mathy fix for the
// "everyone shoots one drone" problem. A unit whose target is already claimed
// past its remaining HP holds fire / looks for another target instead.
const claimed = new Map();
export function claim(target, dmg) { claimed.set(target, (claimed.get(target) || 0) + dmg); }
export function claimOf(target) { return claimed.get(target) || 0; }
export function clearClaims() { claimed.clear(); }

// Generic — works for a unit OR a building shooter (anything with
// .side/.x/.y/.def.weapon/.def.range): game/buildings.js's gun-emplacement
// fire loop calls this directly instead of reimplementing target selection.
export function pickTarget(world, u) {
  const wdef = WEAPON_DEFS[u.def.weapon];
  let best = null, bestScore = -Infinity;
  for (const o of enemyEntities(world, u.side)) {
    if (!weaponCanTarget(wdef, o)) continue; // targeting attribute: e.g. SAM ignores ground entirely
    if (!detects(u.side, o)) continue; // fog: can't target what your side hasn't detected
    const dd = (o.x - u.x) ** 2 + (o.y - u.y) ** 2;
    if (dd > u.def.range * u.def.range) continue;
    const overkill = claimOf(o) >= o.hp; // already dead from committed fire
    if (overkill) continue;
    const score = -dd; // nearest unclaimed-lethal target
    if (score > bestScore) { bestScore = score; best = o; }
  }
  return best;
}

// ---------------------------------------------------------------------------
// ENGAGEMENT TUNABLES — the designer's to feel-tune. Replaces the old crude
// `aggressive ? 0 : range*0.7` standoff (point-blank knife-fighting for half
// the roster) with a disposition-driven fraction of the unit's OWN weapon
// range: ranged/heavy units hold near the outer edge of their envelope and
// shell from there; only short-range/assault-doctrine units (marked
// `aggressive` — CAS jets, drone swarms, infantry autocannon teams) close
// further, and even they stop well short of stacking on the target.
const RANGED_STANDOFF_FRAC = 0.85; // tank (range 220) holds ~187px — "shelling from range", not hugging
const AGGRESSIVE_STANDOFF_FRAC = 0.32; // still a real gap, not literal point-blank
// Aim tolerance (radians) heavy direct-fire weapons need before they'll pull
// the trigger — see the ballistic-kind gate in the fire block below.
const FIRE_AIM_TOLERANCE = 0.22; // ~12.5 degrees off the target's bearing
// How long a fire-event stays flagged on the unit for the visual pass to
// pick up (muzzle flash / recoil) before this file clears it back out.
const MUZZLE_FLASH_LIFE = 0.3;
// Separation tunables (anti-cluster formation) — see the push loop below.
const SEPARATION_GAP_PX = 4; // clearance added on top of the two radii
const SEPARATION_FORCE = 90; // px/s at maximum overlap; falls off to 0 at the gap boundary

export function updateUnits(world, dt, map) {
  for (const u of world.units) {
    if (u.hp <= 0) continue;
    const def = u.def;
    const moveClass = MOVE_CLASSES[def.moveClass];
    const terrainConstrained = !!map && !moveClass.ignoresTerrain;

    // --- steering: accelerate toward an order point or an engage-range hover ---
    let goalX = u.x, goalY = u.y;
    const enemy = pickTarget(world, u) || nearestEnemy(world, u);
    if (enemy) {
      u.target = enemy;
      // AI DISPOSITIONS: holdGround batteries never chase — they just wait
      // for something to enter their umbrella, unchanged from before.
      // Everything else holds at a STANDOFF DISTANCE from the target (not
      // "walk to the target's exact xy", which is what used to put every
      // attacker's nose on the enemy's hull) — a ranged/heavy unit sits near
      // the outer edge of its own weapon range and shells from there; only
      // short-range/assault-doctrine units (`aggressive`) close further, and
      // even they stop well short of stacking on the target. If the unit is
      // already INSIDE its standoff (enemy closed the gap), the same check
      // sends it backing off to reopen the range instead of just holding —
      // a tank reversing to keep its distance, not getting run up on.
      if (!def.dispositions.includes('holdGround')) {
        const standoffFrac = def.dispositions.includes('aggressive') ? AGGRESSIVE_STANDOFF_FRAC : RANGED_STANDOFF_FRAC;
        const standoff = def.range * standoffFrac;
        const rx = u.x - enemy.x, ry = u.y - enemy.y;
        const d = Math.hypot(rx, ry);
        // Dead-zone band around the standoff distance: once inside it, the
        // unit just holds current position (goalX/Y stay at u.x/u.y, set
        // above) instead of continuing to chase the target's exact spot.
        // This band existing at all is what makes two units that have each
        // targeted the other actually stop, rather than each perpetually
        // re-chasing a moving reference point and never bleeding off
        // velocity — verified during this pass to matter in practice: a
        // pair of tanks with a too-tight band never stopped closing/
        // opening, coasted across the map together indefinitely, and in
        // the process consistently outran their own shells' flight time
        // (the ballistic 'shell' kind fire-and-forgets at the target's
        // position AT THE MOMENT OF FIRING — a target that's still moving
        // when the shell arrives just isn't there anymore).
        const band = Math.max(15, standoff * 0.08);
        if (Math.abs(d - standoff) > band) {
          // RADIAL-ONLY correction — walk straight down the CURRENT line to
          // the target until back inside the band, no fixed angular/
          // formation offset baked into this goal. That's deliberate: a
          // goal that's a constant angle away from a bearing recomputed
          // fresh every frame keeps adding that same angular offset again
          // each frame relative to wherever the unit now is, which is a
          // rotational bias — it reads as the unit slowly orbiting the
          // target instead of holding station (verified empirically).
          // Radial-only can't do that: moving straight along the existing
          // bearing never introduces a tangential component, so there's no
          // drift to accumulate. It's also naturally self-consistent for a
          // MUTUAL engagement (two units that have each targeted the
          // other) with no extra bookkeeping — each side's target point
          // sits exactly on the line between them by construction, so
          // there's no "wrong offset" for the two to disagree about the
          // way a fixed angular fan could (also verified: a fixed fan that
          // wasn't precisely antiparallel between two mutual targets left
          // their two goals with no shared stationary point at all, and
          // the pair provably drifted at a constant velocity forever).
          // Lateral spread between several units sharing one target — the
          // actual anti-cluster/formation ask — is handled by the
          // separation pass below instead, which doesn't reopen either
          // failure mode.
          const ux = d > 0.5 ? rx / d : Math.cos(u.aim || 0);
          const uy = d > 0.5 ? ry / d : Math.sin(u.aim || 0);
          goalX = enemy.x + ux * standoff;
          goalY = enemy.y + uy * standoff;
        }
      }
    } else if (u.order) {
      // A* PATHING: ground move classes route around impassable terrain
      // (mountains, marsh, rivers with no bridge) and prefer roads via the
      // cost function, instead of steering straight at the order point and
      // wall-sliding along whatever they hit. Air/naval keep the old direct
      // steer — air ignores terrain entirely, naval's open-water routes
      // rarely need routing around obstacles at this scope.
      if (moveClass.ground && map) {
        ensurePath(u, map, def.moveClass, moveClass);
        const wp = u.path && u.path[u.pathIdx];
        if (wp) {
          goalX = wp.x; goalY = wp.y;
          advancePath(u, wp);
        } else {
          goalX = u.order.x; goalY = u.order.y; // no route (or path exhausted): direct final approach
        }
      } else {
        goalX = u.order.x; goalY = u.order.y;
      }
      if (Math.hypot(u.order.x - u.x, u.order.y - u.y) < 6) { u.order = null; u.path = null; }
    } else {
      u.target = null;
    }

    // current-tile terrain scales top speed per this unit's MOVE CLASS
    // (moveMult 1 = full speed; air ignores terrain; naval only ever gets
    // full speed on water and 0 anywhere else).
    const moveMult = terrainSample(map, moveClass, u.x, u.y).mult;

    const dx = goalX - u.x, dy = goalY - u.y, dist = Math.hypot(dx, dy);
    if (dist > 2) {
      const desiredAngle = Math.atan2(dy, dx);
      let curAngle = Math.atan2(u.vy, u.vx) || desiredAngle;
      let da = desiredAngle - curAngle;
      while (da > Math.PI) da -= 2 * Math.PI;
      while (da < -Math.PI) da += 2 * Math.PI;
      curAngle += Math.max(-def.turnRate * dt, Math.min(def.turnRate * dt, da));
      u.aim = curAngle;
      const targetSpeed = Math.min(def.speed * moveMult, dist * 3); // ease in near the goal
      u.vx += (Math.cos(curAngle) * targetSpeed - u.vx) * Math.min(1, dt * 4);
      u.vy += (Math.sin(curAngle) * targetSpeed - u.vy) * Math.min(1, dt * 4);
    } else {
      u.vx *= 0.85; u.vy *= 0.85;
    }

    let mdx = u.vx * dt, mdy = u.vy * dt;
    if (terrainConstrained && (mdx || mdy)) {
      const nx = u.x + mdx, ny = u.y + mdy;
      if (!isPassable(map, moveClass, nx, ny)) {
        // cheap wall-slide: try each axis alone, otherwise just stop (no
        // real pathfinding — good enough to keep ground units off water/
        // mountain, and naval units IN water, without them getting stuck
        // buzzing at the border)
        const okX = isPassable(map, moveClass, u.x + mdx, u.y);
        const okY = isPassable(map, moveClass, u.x, u.y + mdy);
        if (okX && !okY) mdy = 0;
        else if (okY && !okX) mdx = 0;
        else { mdx = 0; mdy = 0; u.vx = 0; u.vy = 0; }
      }
    }
    u.x += mdx; u.y += mdy;

    // ANTI-CLUSTER SEPARATION so a mass of units given the same order (or
    // fanning around the same engagement target above) actually spreads into
    // a formation instead of stacking on one pixel — the push force here was
    // strengthened (SEPARATION_FORCE, up from a barely-there 40) specifically
    // because the old value was too weak to un-stack a mass converging on a
    // shared order point before this pass's engagement-fan fix existed; the
    // fan now does most of the work of NOT converging in the first place, but
    // this still needs to be strong enough to resolve march-order stacking
    // and any residual overlap without noticeably fighting a unit's own
    // steering toward its goal. BUG FIX (pre-existing, kept): the push used
    // to be applied blindly, so on a packed coastline it could nudge a
    // ground unit a pixel into water (or a naval unit a pixel onto a beach)
    // because passability was never re-checked after the displacement.
    // Re-check the same way the main move step does — wall-slide the push
    // along one axis, or drop it entirely if both axes land on impassable
    // terrain for this unit's move class.
    for (const o of world.units) {
      if (o === u || o.hp <= 0) continue;
      const ox = u.x - o.x, oy = u.y - o.y, od = Math.hypot(ox, oy);
      const min = def.radius + o.def.radius + SEPARATION_GAP_PX;
      if (od < min) {
        // EXACT-OVERLAP FALLBACK: two units spawned (or pushed) onto the
        // identical point have no direction to push apart — ox/oy is (0,0)
        // and od is 0, so the old `od > 0` guard just left them stacked
        // forever. A stable per-PAIR hash (not Math.random(), so it doesn't
        // jitter frame to frame) gives them a deterministic direction to
        // separate along instead.
        let ux, uy;
        if (od > 0.001) { ux = ox / od; uy = oy / od; }
        else {
          const a = (((u.id * 7919 + o.id * 104729) >>> 0) % 6283) / 1000; // ~[0, 2*PI)
          ux = Math.cos(a); uy = Math.sin(a);
        }
        const push = (min - od) / min * SEPARATION_FORCE * dt;
        let pdx = ux * push, pdy = uy * push;
        if (terrainConstrained) {
          const nx = u.x + pdx, ny = u.y + pdy;
          if (!isPassable(map, moveClass, nx, ny)) {
            const okX = isPassable(map, moveClass, u.x + pdx, u.y);
            const okY = isPassable(map, moveClass, u.x, u.y + pdy);
            if (okX && !okY) pdy = 0;
            else if (okY && !okX) pdx = 0;
            else { pdx = 0; pdy = 0; }
          }
        }
        u.x += pdx; u.y += pdy;
      }
    }

    // TURRET AIM: decoupled from u.aim (hull/movement facing, only updated
    // while actually driving somewhere — see the steering block above). A
    // unit stopped at its standoff hold point has u.aim frozen on whatever
    // heading it walked in on, which is NOT necessarily pointed at the
    // target it's holding station on; turretAim tracks the target's live
    // bearing independently, turning at the same def.turnRate (a real
    // turret usually slews faster than the hull steers, but reusing one
    // existing tunable keeps this data-driven without adding a second
    // per-unit turn-rate field the roster doesn't have yet). This is what
    // "tanks fire like tanks" is gated on below, and what a visual pass
    // should read to draw the gun/turret orientation.
    if (u.target) {
      const bearing = Math.atan2(u.target.y - u.y, u.target.x - u.x);
      let tda = bearing - u.turretAim;
      while (tda > Math.PI) tda -= 2 * Math.PI;
      while (tda < -Math.PI) tda += 2 * Math.PI;
      u.turretAim += Math.max(-def.turnRate * dt, Math.min(def.turnRate * dt, tda));
    } else {
      u.turretAim = u.aim;
    }

    // age out the fire-event hook (see spawnUnit's comment) so it reads as
    // "recently fired" for a bounded window rather than staying set forever
    if (u.muzzleFlash) {
      u.muzzleFlash.t += dt;
      if (u.muzzleFlash.t >= MUZZLE_FLASH_LIFE) u.muzzleFlash = null;
    }

    // --- weapon ---
    // u.target may have come from the unbounded-seek nearestEnemy() fallback
    // above (used so units keep advancing toward the fight even when every
    // in-range enemy is already claimed dead) rather than from pickTarget's
    // overkill-filtered search — so the fire decision re-checks the claim
    // itself. Without this re-check, fire discipline only ever affected
    // target *preference*, not whether a shot actually goes out: once every
    // enemy in range was already claimed lethal, units would still fall
    // through to nearestEnemy and fire into an already-guaranteed kill.
    u.cd -= dt;
    if (u.target && u.target.hp > 0 && u.cd <= 0 && claimOf(u.target) < u.target.hp) {
      const d = Math.hypot(u.target.x - u.x, u.target.y - u.y);
      // HEAVY DIRECT FIRE READS AS AIMED, NOT SPRAYED: a ballistic-kind
      // weapon (tank/artillery/MLRS/SRBM/cruise/hypersonic — every tube- or
      // rail-launched shell in the roster) needs its turret roughly on
      // target before it fires; a homing weapon steers itself after launch
      // so hull/turret alignment at the moment of firing doesn't matter the
      // same way, and 'gun'-kind autocannon/laser fire is already a fast
      // stream closer to a spray than a single aimed shot. Gating this on
      // wdef.kind (not a unit name) is what keeps "tanks fire like tanks"
      // an ATTRIBUTE rule instead of a tank-shaped special case.
      const wdef = WEAPON_DEFS[def.weapon];
      let aimed = true;
      if (wdef && wdef.kind === 'ballistic') {
        const bearing = Math.atan2(u.target.y - u.y, u.target.x - u.x);
        let aimErr = bearing - u.turretAim;
        while (aimErr > Math.PI) aimErr -= 2 * Math.PI;
        while (aimErr < -Math.PI) aimErr += 2 * Math.PI;
        aimed = Math.abs(aimErr) <= FIRE_AIM_TOLERANCE;
      }
      if (d <= def.range && aimed) {
        u.cd = def.rate;
        fireProjectile(world, u, u.target);
        claim(u.target, def.dmg);
        // FIRE EVENT HOOK for the visual pass: muzzle flash / recoil should
        // render off THIS, not off the render code re-deriving "did it fire
        // this frame" from cooldown deltas. Shape: { t: 0 (seconds since
        // the shot, counts up, aged out above), angle: the turret facing
        // the shot went out on }.
        u.muzzleFlash = { t: 0, angle: u.turretAim };
        // COMBAT-FEEL/AUDIO EVENT QUEUE: push plain data, never call into
        // game/audio.js directly — this file has ZERO import of it on
        // purpose, so the sim layer never depends on Web Audio (and stays
        // testable/headless). main.js drains world.sfx once per frame and
        // turns each entry into sound + screen shake, then clears the array.
        // Same shape convention as world.hits just above: {x,y,weapon} only,
        // never read back by any gameplay code.
        if (world.sfx) world.sfx.push({ kind: 'fire', weapon: def.weapon, x: u.x, y: u.y });
      }
    }
  }
  world.units = world.units.filter(u => u.hp > 0);
}

// Exported so game/buildings.js's gun-emplacement fire loop launches the
// exact same physical projectiles a unit would — `u` just needs the shape
// {x,y,side,def:{weapon,dmg}}, which a building entity already has.
export function fireProjectile(world, u, target) {
  const def = u.def;
  const wdef = WEAPON_DEFS[def.weapon];
  const dmg = def.dmg;
  if (wdef.kind === 'ballistic') {
    // straight-line travel with a visible flight time (arc drawn via a lofted height curve)
    const dist = Math.hypot(target.x - u.x, target.y - u.y);
    const speed = wdef.speed;
    const t = dist / speed;
    world.projectiles.push({
      physics: 'ballistic', weapon: def.weapon, x: u.x, y: u.y, x0: u.x, y0: u.y, tx: target.x, ty: target.y,
      t: 0, total: t, dmg, side: u.side,
    });
  } else if (wdef.kind === 'homing') {
    // launches, then steers toward the target each frame — physical flight, not hitscan
    const ang = Math.atan2(target.y - u.y, target.x - u.x);
    world.projectiles.push({
      physics: 'homing', weapon: def.weapon, x: u.x, y: u.y, vx: Math.cos(ang) * wdef.speed, vy: Math.sin(ang) * wdef.speed,
      speed: wdef.speed, maxSpeed: wdef.maxSpeed, thrust: wdef.thrust, turnRate: wdef.turnRate, target, dmg,
      side: u.side, life: wdef.life,
    });
  } else if (wdef.kind === 'gun') {
    // fast small tracer: straight-line burst (no homing), short life — hits
    // whatever it crosses paths with, not necessarily the locked target
    const ang = Math.atan2(target.y - u.y, target.x - u.x);
    world.projectiles.push({
      physics: 'gun', weapon: def.weapon, x: u.x, y: u.y, vx: Math.cos(ang) * wdef.speed, vy: Math.sin(ang) * wdef.speed,
      dmg, side: u.side, life: 0.6,
    });
  }
}

export function updateProjectiles(world, dt) {
  for (const p of world.projectiles) {
    if (p.physics === 'ballistic') {
      p.t += dt;
      const f = Math.min(1, p.t / p.total);
      p.x = p.x0 + (p.tx - p.x0) * f;
      p.y = p.y0 + (p.ty - p.y0) * f;
      p.lofted = Math.sin(f * Math.PI) * 24; // visual arc height, purely cosmetic
      if (f >= 1) { resolveHit(world, p, p.tx, p.ty); p.dead = true; }
    } else if (p.physics === 'homing') {
      p.life -= dt;
      if (p.target && p.target.hp > 0) {
        const ang = Math.atan2(p.target.y - p.y, p.target.x - p.x);
        let cur = Math.atan2(p.vy, p.vx);
        let da = ang - cur;
        while (da > Math.PI) da -= 2 * Math.PI;
        while (da < -Math.PI) da += 2 * Math.PI;
        cur += Math.max(-p.turnRate * dt, Math.min(p.turnRate * dt, da));
        p.speed = Math.min(p.maxSpeed, p.speed + p.thrust * dt);
        p.vx = Math.cos(cur) * p.speed; p.vy = Math.sin(cur) * p.speed;
      }
      p.x += p.vx * dt; p.y += p.vy * dt;
      if (p.target && p.target.hp > 0) {
        const d = Math.hypot(p.target.x - p.x, p.target.y - p.y);
        if (d < 10) { resolveHit(world, p, p.x, p.y); p.dead = true; }
      }
      if (p.life <= 0) p.dead = true;
    } else if (p.physics === 'gun') {
      p.life -= dt;
      p.x += p.vx * dt; p.y += p.vy * dt;
      if (resolveHit(world, p, p.x, p.y)) p.dead = true;
      if (p.life <= 0) p.dead = true;
    }
  }
  world.projectiles = world.projectiles.filter(p => !p.dead);
}

// Buildings included via enemyEntities: a building instance's def carries a
// `radius` computed at spawn time (game/buildings.js) from its footprint, in
// the same world-px units a unit's def.radius already is — so this hit
// circle test needs no special-casing per entity kind.
function resolveHit(world, p, x, y) {
  for (const o of enemyEntities(world, p.side)) {
    if (Math.hypot(o.x - x, o.y - y) < o.def.radius + 10) {
      o.hp -= p.dmg;
      // `weapon`/`side` are carried purely for the render pass's impact FX
      // (a shell/missile hit should flash bigger/brighter than a gun-kind
      // tracer's spark — see engine/renderer.js's drawImpacts, which reads
      // WEAPON_DEFS[weapon].kind the same data-driven way projectile flight
      // itself already does). Never read back by any gameplay code — {x,y,
      // life} remain the only fields anything in THIS file consumes.
      world.hits.push({ x, y, life: 0.25, weapon: p.weapon, side: p.side });
      // same event-queue hook as the fire site above — an impact event for
      // main.js to turn into sound + screen shake + lingering smoke/scorch.
      if (world.sfx) world.sfx.push({ kind: 'impact', weapon: p.weapon, x, y });
      return true;
    }
  }
  return false;
}

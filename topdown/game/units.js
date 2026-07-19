// Physical units + projectiles for the top-down slice. Movement is
// acceleration/steering based (not teleport-to-target) and weapons fire real
// projectiles that travel over time — same physics feel as the original game,
// just on a 2D plane instead of a side-view strip.

export const UNIT_DEFS = {
  tank: {
    name: 'MBT "Aegis"', domain: 'ground', hp: 110, armor: 8, speed: 70,
    range: 220, dmg: 18, rate: 1.6, weapon: 'shell', turnRate: 3, radius: 10,
  },
  fighter: {
    name: 'Fighter "Kestrel"', domain: 'air', hp: 60, armor: 3, speed: 220,
    range: 260, dmg: 12, rate: 0.9, weapon: 'missile', turnRate: 5, radius: 8,
  },
};

let nextId = 1;

export function spawnUnit(world, key, x, y, side) {
  const def = UNIT_DEFS[key];
  const u = {
    id: nextId++, key, def, side, x, y, vx: 0, vy: 0,
    hp: def.hp, maxHp: def.hp, aim: 0, cd: Math.random() * def.rate,
    target: null, order: null, // order = {x,y} move destination, set by player input
  };
  world.units.push(u);
  return u;
}

// unbounded seek: a unit needs to know the nearest enemy ANYWHERE on the map
// so it advances toward the fight, not just enemies already within gun range
// (which was a real bug — units 1400px apart never had a reason to move).
function nearestEnemy(world, u) {
  let best = null, bd = Infinity;
  for (const o of world.units) {
    if (o.side === u.side || o.hp <= 0) continue;
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
function claim(target, dmg) { claimed.set(target, (claimed.get(target) || 0) + dmg); }
function claimOf(target) { return claimed.get(target) || 0; }
export function clearClaims() { claimed.clear(); }

function pickTarget(world, u) {
  let best = null, bestScore = -Infinity;
  for (const o of world.units) {
    if (o.side === u.side || o.hp <= 0) continue;
    const dd = (o.x - u.x) ** 2 + (o.y - u.y) ** 2;
    if (dd > u.def.range * u.def.range) continue;
    const overkill = claimOf(o) >= o.hp; // already dead from committed fire
    if (overkill) continue;
    const score = -dd; // nearest unclaimed-lethal target
    if (score > bestScore) { bestScore = score; best = o; }
  }
  return best;
}

export function updateUnits(world, dt) {
  for (const u of world.units) {
    if (u.hp <= 0) continue;
    const def = u.def;

    // --- steering: accelerate toward an order point or an engage-range hover ---
    let goalX = u.x, goalY = u.y;
    const enemy = pickTarget(world, u) || nearestEnemy(world, u);
    if (enemy) {
      const d = Math.hypot(enemy.x - u.x, enemy.y - u.y);
      const standoff = def.range * 0.7; // close to engage range, don't ram
      if (d > standoff) { goalX = enemy.x; goalY = enemy.y; }
      u.target = enemy;
    } else if (u.order) {
      goalX = u.order.x; goalY = u.order.y;
      if (Math.hypot(goalX - u.x, goalY - u.y) < 6) u.order = null;
    } else {
      u.target = null;
    }

    const dx = goalX - u.x, dy = goalY - u.y, dist = Math.hypot(dx, dy);
    if (dist > 2) {
      const desiredAngle = Math.atan2(dy, dx);
      let curAngle = Math.atan2(u.vy, u.vx) || desiredAngle;
      let da = desiredAngle - curAngle;
      while (da > Math.PI) da -= 2 * Math.PI;
      while (da < -Math.PI) da += 2 * Math.PI;
      curAngle += Math.max(-def.turnRate * dt, Math.min(def.turnRate * dt, da));
      u.aim = curAngle;
      const targetSpeed = Math.min(def.speed, dist * 3); // ease in near the goal
      u.vx += (Math.cos(curAngle) * targetSpeed - u.vx) * Math.min(1, dt * 4);
      u.vy += (Math.sin(curAngle) * targetSpeed - u.vy) * Math.min(1, dt * 4);
    } else {
      u.vx *= 0.85; u.vy *= 0.85;
    }
    u.x += u.vx * dt; u.y += u.vy * dt;

    // simple separation so a mass of units spreads into a formation instead of stacking
    for (const o of world.units) {
      if (o === u || o.hp <= 0) continue;
      const ox = u.x - o.x, oy = u.y - o.y, od = Math.hypot(ox, oy);
      const min = def.radius + o.def.radius + 4;
      if (od > 0 && od < min) {
        const push = (min - od) / min * 40 * dt;
        u.x += (ox / od) * push; u.y += (oy / od) * push;
      }
    }

    // --- weapon ---
    u.cd -= dt;
    if (u.target && u.target.hp > 0 && u.cd <= 0) {
      const d = Math.hypot(u.target.x - u.x, u.target.y - u.y);
      if (d <= def.range) {
        u.cd = def.rate;
        fireProjectile(world, u, u.target);
        claim(u.target, def.dmg);
      }
    }
  }
  world.units = world.units.filter(u => u.hp > 0);
}

function fireProjectile(world, u, target) {
  const def = u.def;
  if (def.weapon === 'shell') {
    // ballistic: straight-line travel with a visible flight time (arc drawn via a lofted height curve)
    const dist = Math.hypot(target.x - u.x, target.y - u.y);
    const speed = 260;
    const t = dist / speed;
    world.projectiles.push({
      kind: 'shell', x: u.x, y: u.y, x0: u.x, y0: u.y, tx: target.x, ty: target.y,
      t: 0, total: t, dmg: def.dmg, side: u.side,
    });
  } else if (def.weapon === 'missile') {
    // homing: launches, then steers toward the target each frame — physical flight, not hitscan
    const ang = Math.atan2(target.y - u.y, target.x - u.x);
    world.projectiles.push({
      kind: 'missile', x: u.x, y: u.y, vx: Math.cos(ang) * 40, vy: Math.sin(ang) * 40,
      speed: 40, maxSpeed: 340, thrust: 500, turnRate: 4, target, dmg: def.dmg,
      side: u.side, life: 4,
    });
  }
}

export function updateProjectiles(world, dt) {
  for (const p of world.projectiles) {
    if (p.kind === 'shell') {
      p.t += dt;
      const f = Math.min(1, p.t / p.total);
      p.x = p.x0 + (p.tx - p.x0) * f;
      p.y = p.y0 + (p.ty - p.y0) * f;
      p.lofted = Math.sin(f * Math.PI) * 24; // visual arc height, purely cosmetic
      if (f >= 1) { resolveHit(world, p, p.tx, p.ty); p.dead = true; }
    } else if (p.kind === 'missile') {
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
    }
  }
  world.projectiles = world.projectiles.filter(p => !p.dead);
}

function resolveHit(world, p, x, y) {
  for (const o of world.units) {
    if (o.side === p.side || o.hp <= 0) continue;
    if (Math.hypot(o.x - x, o.y - y) < o.def.radius + 10) {
      o.hp -= p.dmg;
      world.hits.push({ x, y, life: 0.25 });
      break;
    }
  }
}

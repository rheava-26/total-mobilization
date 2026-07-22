// LINGERING BATTLE VFX — smoke/dust wisps and scorch decals spawned off
// weapon impacts. PURELY VISUAL, same "never touches gameplay" contract as
// game/ambient.js (see that file's header): this module reads nothing but
// the (x, y, weapon) an impact event already carries, writes only into its
// own pools, and nothing in the sim ever reads them back. Delete this file
// and its call sites in main.js and the game plays byte-identical — impacts
// just go back to vanishing the instant engine/renderer.js's drawImpacts
// flash fades (0.25s, see game/units.js's resolveHit).
//
// Two pools, deliberately different lifetimes:
//   - SMOKE rises and fades quickly (~1-2s) — reads as "something just
//     happened here."
//   - SCORCH is a flat ground mark that lingers for several seconds so a
//     battle's geography visibly accumulates wear even after the smoke
//     clears (reference doc Part 2: "environmental storytelling ... battle
//     damage"), without needing an actual persistent decal texture — it's
//     just a fading circle redrawn every frame off a tiny plain-data record.
//
// Both pools are hard-capped and age out the OLDEST entry first once full,
// so a sustained multi-front battle can't grow either array without bound —
// the same discipline game/ambient.js's own smoke pool already follows.

import { WEAPON_DEFS } from './units.js';

const MAX_SMOKE = 160;
const MAX_SCORCH = 100;

// Reuses the exact same "gun-kind hits smaller than everything else" split
// engine/renderer.js's drawImpacts already draws (its `big` flag), so the
// instant flash and the decal it leaves behind agree on scale.
function weightFor(weapon) {
  const wdef = WEAPON_DEFS[weapon];
  return !wdef || wdef.kind !== 'gun' ? 1 : 0.5;
}

export function createImpactFx() {
  return { smoke: [], scorch: [] };
}

// Called once per impact event (main.js drains world.sfx and calls this for
// every 'impact' entry, right alongside the audio dispatch). Deliberately a
// FEW puffs, not a full particle system — this reads as a wisp drifting off
// the hit, not a smoke cloud filling the screen; "subtle and weighty, never
// cartoonish" per the task brief.
export function spawnImpactFx(vfx, x, y, weapon) {
  const w = weightFor(weapon);
  const puffs = w >= 1 ? 3 : 1;
  for (let i = 0; i < puffs; i++) {
    if (vfx.smoke.length >= MAX_SMOKE) vfx.smoke.shift(); // age out oldest rather than grow unbounded
    vfx.smoke.push({
      x: x + (Math.random() - 0.5) * 10 * w,
      y: y + (Math.random() - 0.5) * 10 * w,
      vy: -(4 + Math.random() * 5) * w, // drifts upward — "vy" is world px/sec, negative = up
      drift: (Math.random() - 0.5) * 6,
      life: 0,
      maxLife: (0.9 + Math.random() * 0.6) * (0.8 + w * 0.4),
      size: (5 + Math.random() * 4) * w,
    });
  }
  if (vfx.scorch.length >= MAX_SCORCH) vfx.scorch.shift();
  vfx.scorch.push({
    x, y,
    r: (6 + Math.random() * 3) * w,
    life: 0,
    maxLife: (4 + Math.random() * 2) * (0.8 + w * 0.5), // lingers well past the smoke, per the header comment
  });
}

// Ages/culls both pools. Called with the REAL (unscaled) frame delta, same
// "render-only effects keep real cadence" rule the hit-stop/screen-shake
// systems in main.js follow — a decal fading out shouldn't itself appear to
// stutter if a future hit-stop pass ever scales the sim's own dt.
export function updateImpactFx(vfx, dt) {
  for (let i = vfx.smoke.length - 1; i >= 0; i--) {
    const p = vfx.smoke[i];
    p.life += dt;
    if (p.life >= p.maxLife) { vfx.smoke.splice(i, 1); continue; }
    p.y += p.vy * dt;
    p.x += p.drift * dt;
    p.size += dt * 2.5;
  }
  for (let i = vfx.scorch.length - 1; i >= 0; i--) {
    const s = vfx.scorch[i];
    s.life += dt;
    if (s.life >= s.maxLife) vfx.scorch.splice(i, 1);
  }
}

// SCORCH draw: ground layer, call this BELOW units/buildings (main.js calls
// it alongside drawAmbientGround) so marks read as part of the terrain
// rather than floating over the units standing near them.
export function drawScorch(vfx, ctx, worldToScreen, cam) {
  const dpr = devicePixelRatio;
  ctx.save();
  for (const s of vfx.scorch) {
    const frac = s.life / s.maxLife;
    const a = (1 - frac) * 0.32; // capped low — a battlefield accumulates wear, it doesn't turn black
    if (a <= 0.01) continue;
    const r = s.r * cam.zoom * dpr;
    if (r < 1) continue; // sub-pixel at this zoom — skip the draw call, matches ambient.js's own convention
    const [sx, sy] = worldToScreen(s.x, s.y);
    ctx.fillStyle = `rgba(20,16,14,${a.toFixed(3)})`;
    ctx.beginPath(); ctx.ellipse(sx, sy, r, r * 0.7, 0, 0, 6.283); ctx.fill();
  }
  ctx.restore();
}

// SMOKE draw: call this ABOVE units (same ordering game/ambient.js's own
// factory smoke uses relative to buildings) so drifting wisps read as
// sitting over the battlefield instead of tucked under a unit marker.
export function drawSmoke(vfx, ctx, worldToScreen, cam) {
  const dpr = devicePixelRatio;
  ctx.save();
  for (const p of vfx.smoke) {
    const frac = p.life / p.maxLife;
    const a = (1 - frac) * 0.22; // thin/semi-transparent — a wisp, not a cloud
    if (a <= 0.01) continue;
    const r = p.size * cam.zoom * dpr;
    if (r < 0.8) continue;
    const [sx, sy] = worldToScreen(p.x, p.y);
    const grad = ctx.createRadialGradient(sx, sy, 0, sx, sy, r);
    grad.addColorStop(0, `rgba(160,155,150,${a.toFixed(3)})`); // desaturated grey, not bright white — matches the "realistic" brief
    grad.addColorStop(1, 'rgba(160,155,150,0)');
    ctx.fillStyle = grad;
    ctx.beginPath(); ctx.arc(sx, sy, r, 0, 6.283); ctx.fill();
  }
  ctx.restore();
}

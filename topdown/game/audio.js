// COMBAT AUDIO — hybrid file/synth player, FILES TAKE PRECEDENCE. Like
// game/ambient.js, this is a layer that's allowed to READ world/cam state but
// must never touch gameplay: it only reacts to world.sfx events pushed by
// game/units.js's fire/resolveHit sites (plain data — see world.sfx's own
// comment in main.js; units.js has ZERO import of this file, on purpose, so
// the sim layer never depends on Web Audio).
//
// THE SWAP CONTRACT: at init, each of the five sound keys below kicks off a
// best-effort fetch+decode of assets/audio/<key>.mp3. With that folder empty
// (today's state) every decode 404s/fails, every key falls back to its
// procedural synth recipe, and the game sounds fully synthesized. The
// INSTANT a real file lands at the right path, playback for that key
// switches to it automatically, with ZERO code change — playSound() below
// just checks "do I have a decoded buffer for this key yet?" on every single
// play, not once at load. Keys are independent, so files can land one at a
// time (e.g. just cannon.mp3) while the rest keep using synth.
//
// SYNTH RECIPES follow docs/reference-combat-feel-audio.md's Part 3/4
// layering (crack + boom + rumble + crackle, per sound) rather than a single
// beepy oscillator — a flat sine/square blip reads as a placeholder toy, and
// the designer has explicitly called synth "wonky" before, so the fallback
// path gets the same care as a real shipped sound: multiple layered
// oscillators/filtered-noise bursts per hit, all with linear attack into
// exponential decay (never a hard on/off, which is what makes synth sound
// like a beep), plus per-play pitch/timing jitter so repeats never sound
// like the exact same sample looping.

import { WEAPON_DEFS } from './units.js';

const ASSET_BASE = 'assets/audio/';
const ASSET_FILES = {
  cannon: 'cannon.mp3',      // tank/artillery/heavy ballistic gun fire
  gun: 'gun.mp3',            // MG/autocannon-kind fire, short
  missile: 'missile.mp3',    // homing-kind launch whoosh
  explosion: 'explosion.mp3',// shell/missile (non-gun) impact
  impact: 'impact.mp3',      // gun-kind impact tick
};

let ctx = null;
let master = null;
const buffers = {}; // key -> decoded AudioBuffer, only ever set on a successful decode
let loadStarted = false;

function ensureCtx() {
  if (ctx) return ctx;
  try {
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return null;
    ctx = new AC();
    master = ctx.createGain();
    master.gain.value = 0.55; // headroom for ~MAX_VOICES simultaneous voices without clipping
    master.connect(ctx.destination);
  } catch (e) {
    ctx = null; // AudioContext unavailable in this environment — degrade silently forever
  }
  return ctx;
}

// Kicks off best-effort loads of every known sound. Safe to call more than
// once (guarded by loadStarted) and safe to call BEFORE the context is
// unlocked by a user gesture — fetch/decode don't need an unlocked context,
// only actually hearing anything does (see resumeAudio/playSfx below). A
// missing/broken file just leaves buffers[key] unset forever, which
// playSound()'s per-play check below reads as "use the synth fallback."
export function initAudio() {
  if (loadStarted) return;
  loadStarted = true;
  const c = ensureCtx();
  if (!c) return;
  for (const [key, file] of Object.entries(ASSET_FILES)) {
    fetch(ASSET_BASE + file)
      .then(res => { if (!res.ok) throw new Error('http ' + res.status); return res.arrayBuffer(); })
      .then(buf => c.decodeAudioData(buf))
      .then(decoded => { buffers[key] = decoded; })
      .catch(() => { /* missing/broken asset — stays on the synth fallback forever, no retry, no throw */ });
  }
}

// User-gesture unlock (wired to a one-shot pointerdown/keydown in main.js).
// Browsers start an AudioContext 'suspended' until a real user gesture; safe
// to call repeatedly, and a no-op once already running.
export function resumeAudio() {
  const c = ensureCtx();
  if (c && c.state === 'suspended') c.resume().catch(() => {});
}

// ---------------------------------------------------------------------------
// CAMERA-DISTANCE FALLOFF — a strategic-camera battle can have dozens of
// simultaneous weapons firing across a map far wider than the screen; without
// this every shot would mix at full volume and the whole thing turns to mush
// (reference doc, Part 3, "The Battlefield Mush Problem"). Two multiplied
// factors:
//   - a distance ramp in WORLD units, whose falloff radius is scaled by
//     1/cam.zoom so it stays keyed to what's actually near the CAMERA rather
//     than a fixed world radius that would read wrong at every zoom level
//   - a flat "zoom ceiling": even a nearby-in-world-units event should never
//     hit full volume while zoomed out to a whole-theater view — that's the
//     literal RTS-mush case the falloff exists to prevent
const REFERENCE_FALLOFF_DIST = 900; // world px at zoom=1 where a sound has faded to ~silent
function distanceGain(x, y, cam) {
  const dist = Math.hypot(x - cam.x, y - cam.y);
  const maxDist = REFERENCE_FALLOFF_DIST / Math.max(0.15, cam.zoom);
  let g = 1 - dist / maxDist;
  if (g <= 0) return 0;
  const zoomCeiling = Math.max(0.35, Math.min(1, cam.zoom));
  g = Math.min(1, g) * zoomCeiling;
  return g < 0.08 ? 0 : g; // below this it's inaudible anyway — cull rather than schedule a silent voice
}

// ---------------------------------------------------------------------------
// VOICE BUDGET — caps concurrent scheduled voices so a big battle degrades by
// silently dropping the least-important NEW sound rather than the browser
// stuttering/crackling under too many simultaneous audio nodes (reference
// doc: "~16-24 simultaneous audio nodes without stuttering"). Shared by both
// the file path and the synth path — a voice is a voice either way.
const MAX_VOICES = 20;
let activeVoices = []; // { peakGain, stopAt }

function reapVoices(now) {
  activeVoices = activeVoices.filter(v => v.stopAt > now);
}

// Returns { ctx, now, voiceGain } to build/play a sound through, or null if
// this sound loses the budget tradeoff and must be skipped entirely.
function allocateVoice(peakGain, durationSec) {
  const c = ensureCtx();
  if (!c) return null;
  const now = c.currentTime;
  reapVoices(now);
  if (activeVoices.length >= MAX_VOICES) {
    let minIdx = 0;
    for (let i = 1; i < activeVoices.length; i++) {
      if (activeVoices[i].peakGain < activeVoices[minIdx].peakGain) minIdx = i;
    }
    if (activeVoices[minIdx].peakGain >= peakGain) {
      // everything currently playing is louder/closer than this new sound —
      // drop the NEW one (never hard-stop something already playing; a
      // forced stop mid-sound is what causes an audible click/stutter, a
      // skipped new distant shot is inaudible by definition)
      return null;
    }
    // this new sound wins the tradeoff — stop counting the quietest existing
    // voice against the budget (it keeps playing out on its own; nodes free
    // themselves when they end) so a burst of loud, close fire can't be
    // starved by a pile of stale far-away leftovers
    activeVoices.splice(minIdx, 1);
  }
  const voiceGain = c.createGain();
  voiceGain.gain.value = peakGain;
  voiceGain.connect(master);
  activeVoices.push({ peakGain, stopAt: now + durationSec });
  return { ctx: c, now, voiceGain };
}

// ---------------------------------------------------------------------------
// NOISE BUFFER CACHE — every synth recipe below needs one or more short white
// -noise bursts (shock crackle, debris, tail rumble texture). Generating a
// fresh Float32Array per play would be wasteful busywork for what's always
// one of a handful of fixed durations, so buffers are cached by their
// rounded millisecond length and reused across every future call — the
// AudioBuffer itself is stateless and one buffer can back any number of
// simultaneous AudioBufferSourceNodes.
const noiseBufferCache = new Map();
function getNoiseBuffer(c, durationSec) {
  const key = Math.round(durationSec * 1000);
  let buf = noiseBufferCache.get(key);
  if (buf) return buf;
  buf = c.createBuffer(1, Math.max(1, Math.round(c.sampleRate * durationSec)), c.sampleRate);
  const data = buf.getChannelData(0);
  for (let i = 0; i < data.length; i++) data[i] = Math.random() * 2 - 1;
  noiseBufferCache.set(key, buf);
  return buf;
}

// ---------------------------------------------------------------------------
// SYNTH RECIPES — one per sound key, each a small layered graph feeding the
// voiceGain node allocateVoice() already handed us (so distance falloff and
// the voice budget apply identically whether a sound came from a file or
// from here). Every gain envelope starts at a near-zero value and LINEAR-ramps
// up before exponentially decaying — starting cold at 0 and exponentially
// ramping up front is what makes naive synth read as a "beep"; a fast linear
// attack reads as a transient instead. `pitch`/timing jitter is rolled fresh
// on every call so a burst of MG fire never sounds like one clip looping.

function synthCannon(c, now, voiceGain) {
  const pitch = 1 + (Math.random() * 2 - 1) * 0.07;

  // 1) transient crack (~0-12ms) — the bright ignition snap
  const crack = c.createOscillator();
  const crackGain = c.createGain();
  crack.type = 'square';
  crack.frequency.setValueAtTime(8200 * pitch, now);
  crack.frequency.exponentialRampToValueAtTime(5800 * pitch, now + 0.012);
  crackGain.gain.setValueAtTime(0.0001, now);
  crackGain.gain.linearRampToValueAtTime(0.30, now + 0.002);
  crackGain.gain.exponentialRampToValueAtTime(0.0005, now + 0.05);
  crack.connect(crackGain).connect(voiceGain);
  crack.start(now); crack.stop(now + 0.06);

  // 2) body/impact boom — the weight; drops fast into sub-bass, ~0.5s tail
  const boom = c.createOscillator();
  const boomGain = c.createGain();
  boom.type = 'sine';
  boom.frequency.setValueAtTime(155 * pitch, now + 0.004);
  boom.frequency.exponentialRampToValueAtTime(72 * pitch, now + 0.14);
  boomGain.gain.setValueAtTime(0.0001, now);
  boomGain.gain.linearRampToValueAtTime(0.5, now + 0.018);
  boomGain.gain.exponentialRampToValueAtTime(0.0005, now + 0.48);
  boom.connect(boomGain).connect(voiceGain);
  boom.start(now + 0.004); boom.stop(now + 0.5);

  // 3) tail/room rumble — sustained mid-bass, decays a little slower than
  // the boom above so the very end of the shot reads as "settling," not a
  // clean cutoff
  const rumble = c.createOscillator();
  const rumbleGain = c.createGain();
  rumble.type = 'sine';
  rumble.frequency.setValueAtTime(260 * pitch, now + 0.05);
  rumble.frequency.exponentialRampToValueAtTime(190 * pitch, now + 0.45);
  rumbleGain.gain.setValueAtTime(0.0001, now + 0.05);
  rumbleGain.gain.linearRampToValueAtTime(0.16, now + 0.09);
  rumbleGain.gain.exponentialRampToValueAtTime(0.0005, now + 0.5);
  rumble.connect(rumbleGain).connect(voiceGain);
  rumble.start(now + 0.05); rumble.stop(now + 0.5);

  // 4) breech/mechanical clang — brief metallic tick, gives the shot a
  // "machine" character instead of reading as pure explosion
  const clang = c.createOscillator();
  const clangGain = c.createGain();
  clang.type = 'triangle';
  clang.frequency.setValueAtTime(520 * pitch, now + 0.03);
  clangGain.gain.setValueAtTime(0.0001, now + 0.03);
  clangGain.gain.linearRampToValueAtTime(0.09, now + 0.035);
  clangGain.gain.exponentialRampToValueAtTime(0.0005, now + 0.09);
  clang.connect(clangGain).connect(voiceGain);
  clang.start(now + 0.03); clang.stop(now + 0.1);

  // 5) debris/crackle — brief high-passed noise texture
  const crackle = c.createBufferSource();
  crackle.buffer = getNoiseBuffer(c, 0.22);
  const crackleFilter = c.createBiquadFilter();
  crackleFilter.type = 'highpass';
  crackleFilter.frequency.value = 4200;
  const crackleGain = c.createGain();
  crackleGain.gain.setValueAtTime(0.0001, now + 0.05);
  crackleGain.gain.linearRampToValueAtTime(0.12, now + 0.08);
  crackleGain.gain.exponentialRampToValueAtTime(0.0005, now + 0.3);
  crackle.connect(crackleFilter).connect(crackleGain).connect(voiceGain);
  crackle.start(now + 0.05); crackle.stop(now + 0.3);
}

function synthGun(c, now, voiceGain) {
  const pitch = 1 + (Math.random() * 2 - 1) * 0.09;

  // per-round crack
  const crack = c.createOscillator();
  const crackGain = c.createGain();
  crack.type = 'triangle';
  crack.frequency.setValueAtTime(6200 * pitch, now);
  crackGain.gain.setValueAtTime(0.0001, now);
  crackGain.gain.linearRampToValueAtTime(0.17, now + 0.001);
  crackGain.gain.exponentialRampToValueAtTime(0.0005, now + 0.012);
  crack.connect(crackGain).connect(voiceGain);
  crack.start(now); crack.stop(now + 0.015);

  // low body tap — without this the crack alone reads as a click, not a shot
  const body = c.createOscillator();
  const bodyGain = c.createGain();
  body.type = 'sine';
  body.frequency.setValueAtTime(300 * pitch, now);
  body.frequency.exponentialRampToValueAtTime(150 * pitch, now + 0.04);
  bodyGain.gain.setValueAtTime(0.0001, now);
  bodyGain.gain.linearRampToValueAtTime(0.13, now + 0.004);
  bodyGain.gain.exponentialRampToValueAtTime(0.0005, now + 0.06);
  body.connect(bodyGain).connect(voiceGain);
  body.start(now); body.stop(now + 0.07);

  // shell casing ping — not every round, per the reference doc's own ~50% figure
  if (Math.random() < 0.4) {
    const ping = c.createOscillator();
    const pingGain = c.createGain();
    ping.type = 'sine';
    ping.frequency.setValueAtTime(4200 + Math.random() * 2200, now + 0.018);
    pingGain.gain.setValueAtTime(0.0001, now + 0.018);
    pingGain.gain.linearRampToValueAtTime(0.05, now + 0.02);
    pingGain.gain.exponentialRampToValueAtTime(0.0005, now + 0.07);
    ping.connect(pingGain).connect(voiceGain);
    ping.start(now + 0.018); ping.stop(now + 0.08);
  }
}

function synthMissile(c, now, voiceGain) {
  const pitch = 1 + (Math.random() * 2 - 1) * 0.08;

  // ignition thump
  const thump = c.createOscillator();
  const thumpGain = c.createGain();
  thump.type = 'sine';
  thump.frequency.setValueAtTime(130 * pitch, now);
  thump.frequency.exponentialRampToValueAtTime(65 * pitch, now + 0.16);
  thumpGain.gain.setValueAtTime(0.0001, now);
  thumpGain.gain.linearRampToValueAtTime(0.28, now + 0.02);
  thumpGain.gain.exponentialRampToValueAtTime(0.0005, now + 0.32);
  thump.connect(thumpGain).connect(voiceGain);
  thump.start(now); thump.stop(now + 0.33);

  // whoosh — band-passed noise whose cutoff sweeps upward, reads as motion
  const whoosh = c.createBufferSource();
  whoosh.buffer = getNoiseBuffer(c, 0.4);
  const whooshFilter = c.createBiquadFilter();
  whooshFilter.type = 'bandpass';
  whooshFilter.Q.value = 0.7;
  whooshFilter.frequency.setValueAtTime(450 * pitch, now);
  whooshFilter.frequency.exponentialRampToValueAtTime(2100 * pitch, now + 0.35);
  const whooshGain = c.createGain();
  whooshGain.gain.setValueAtTime(0.0001, now);
  whooshGain.gain.linearRampToValueAtTime(0.16, now + 0.06);
  whooshGain.gain.exponentialRampToValueAtTime(0.0005, now + 0.4);
  whoosh.connect(whooshFilter).connect(whooshGain).connect(voiceGain);
  whoosh.start(now); whoosh.stop(now + 0.4);
}

function synthExplosion(c, now, voiceGain, size = 1) {
  const pitch = 1 + (Math.random() * 2 - 1) * 0.06;
  const duration = 0.7 + size * 0.4;

  // shock — ultra-brief bright crackle at the very front
  const shock = c.createBufferSource();
  shock.buffer = getNoiseBuffer(c, 0.035);
  const shockFilter = c.createBiquadFilter();
  shockFilter.type = 'highpass';
  shockFilter.frequency.value = 5000;
  const shockGain = c.createGain();
  shockGain.gain.setValueAtTime(0.32 * Math.min(1.4, size), now);
  shockGain.gain.exponentialRampToValueAtTime(0.0005, now + 0.035);
  shock.connect(shockFilter).connect(shockGain).connect(voiceGain);
  shock.start(now); shock.stop(now + 0.035);

  // deep boom — the actual "impact," frequency/gain both scale with size
  const boom = c.createOscillator();
  const boomGain = c.createGain();
  boom.type = 'sine';
  const boomFreq = Math.max(26, 56 - size * 15) * pitch;
  boom.frequency.setValueAtTime(boomFreq, now + 0.008);
  boom.frequency.exponentialRampToValueAtTime(Math.max(16, boomFreq * 0.55), now + 0.16);
  boomGain.gain.setValueAtTime(0.0001, now);
  boomGain.gain.linearRampToValueAtTime(0.5 * Math.min(1.5, size), now + 0.016);
  boomGain.gain.exponentialRampToValueAtTime(0.0005, now + duration);
  boom.connect(boomGain).connect(voiceGain);
  boom.start(now + 0.008); boom.stop(now + duration);

  // debris crackle — mid-band noise settling in after the boom lands
  const debrisDur = Math.max(0.15, duration - 0.1);
  const debris = c.createBufferSource();
  debris.buffer = getNoiseBuffer(c, debrisDur);
  const debrisFilter = c.createBiquadFilter();
  debrisFilter.type = 'bandpass';
  debrisFilter.frequency.value = 1900;
  debrisFilter.Q.value = 0.9;
  const debrisGain = c.createGain();
  debrisGain.gain.setValueAtTime(0.0001, now + 0.1);
  debrisGain.gain.linearRampToValueAtTime(0.13 * size, now + 0.16);
  debrisGain.gain.exponentialRampToValueAtTime(0.0005, now + duration);
  debris.connect(debrisFilter).connect(debrisGain).connect(voiceGain);
  debris.start(now + 0.1); debris.stop(now + duration);

  // tail rumble — only for the larger warheads; a long, quiet sub-bass decay
  // so a big explosion doesn't cut off abruptly right at `duration`
  if (size > 0.9) {
    const tail = c.createOscillator();
    const tailGain = c.createGain();
    tail.type = 'sine';
    tail.frequency.setValueAtTime(Math.max(22, boomFreq * 0.5), now + 0.15);
    tailGain.gain.setValueAtTime(0.0001, now + 0.15);
    tailGain.gain.linearRampToValueAtTime(0.1 * size, now + 0.2);
    tailGain.gain.exponentialRampToValueAtTime(0.0005, now + duration + 0.3);
    tail.connect(tailGain).connect(voiceGain);
    tail.start(now + 0.15); tail.stop(now + duration + 0.3);
  }
}

function synthImpact(c, now, voiceGain) {
  const pitch = 1 + (Math.random() * 2 - 1) * 0.1;

  const thwack = c.createOscillator();
  const thwackGain = c.createGain();
  thwack.type = 'sine';
  thwack.frequency.setValueAtTime(260 * pitch, now);
  thwack.frequency.exponentialRampToValueAtTime(105 * pitch, now + 0.05);
  thwackGain.gain.setValueAtTime(0.0001, now);
  thwackGain.gain.linearRampToValueAtTime(0.22, now + 0.004);
  thwackGain.gain.exponentialRampToValueAtTime(0.0005, now + 0.15);
  thwack.connect(thwackGain).connect(voiceGain);
  thwack.start(now); thwack.stop(now + 0.16);

  const crackle = c.createBufferSource();
  crackle.buffer = getNoiseBuffer(c, 0.06);
  const crackleFilter = c.createBiquadFilter();
  crackleFilter.type = 'highpass';
  crackleFilter.frequency.value = 3200;
  const crackleGain = c.createGain();
  crackleGain.gain.setValueAtTime(0.11, now);
  crackleGain.gain.exponentialRampToValueAtTime(0.0005, now + 0.08);
  crackle.connect(crackleFilter).connect(crackleGain).connect(voiceGain);
  crackle.start(now); crackle.stop(now + 0.08);
}

const SYNTH = { cannon: synthCannon, gun: synthGun, missile: synthMissile, explosion: synthExplosion, impact: synthImpact };

// ---------------------------------------------------------------------------
// PLAY DISPATCH — the one place that decides file vs. synth, checked fresh
// on every single call (never cached/decided once at load) so a file that
// finishes decoding mid-session takes over immediately, per the swap
// contract at the top of this file.
function playSound(key, gain, { durationHint, synthDuration = 0.3, size = 1 } = {}) {
  const c = ensureCtx();
  if (!c || c.state !== 'running') return; // suspended (pre-gesture) or unavailable — never queue, just skip

  const buf = buffers[key];
  if (buf) {
    const alloc = allocateVoice(gain, durationHint ?? buf.duration);
    if (!alloc) return;
    try {
      const src = c.createBufferSource();
      src.buffer = buf;
      // ±8% playback-rate jitter — repeated fire never sounds like the exact
      // same sample looping. Changing playbackRate moves pitch and speed
      // together, which for a short combat foley clip reads as natural
      // variation, not a detuned copy.
      src.playbackRate.value = 1 + (Math.random() * 2 - 1) * 0.08;
      src.connect(alloc.voiceGain);
      src.start(alloc.now);
    } catch (e) { /* never let a playback failure reach the main loop */ }
    return;
  }

  // no decoded buffer for this key yet (missing file, still fetching, or
  // failed to decode) — fall back to the procedural recipe
  const synth = SYNTH[key];
  if (!synth) return;
  const alloc = allocateVoice(gain, synthDuration);
  if (!alloc) return;
  try { synth(alloc.ctx, alloc.now, alloc.voiceGain, size); }
  catch (e) { /* never let a synth graph failure reach the main loop */ }
}

// ---------------------------------------------------------------------------
// WEAPON → SOUND-KEY MAPPING. Same data-driven rule the rest of the combat
// code follows (units.js's own header: "never branch on a unit's name, only
// the shape of its attributes") — this reads WEAPON_DEFS[weapon].kind, never
// a weapon's literal key, so a newly added weapon entry gets a sensible
// sound automatically as long as its `kind` is one of the three existing
// physics kinds.
//
// Explosion "size": WEAPON_DEFS carries flight physics, not payload weight,
// so there's no existing field to derive a warhead size from. A small
// hand-tuned table (falling back to a kind-based default) scales both the
// synth boom's frequency/gain and the file path's overall gain a little, so
// a hypersonic strike still reads heavier than an ATGM even on the same
// explosion.mp3 sample.
const EXPLOSION_WEIGHT = {
  shell: 1.0, ballisticMissile: 1.3, hypersonic: 1.5, cruise: 1.4,
  sam: 1.05, aam: 0.8, rocket: 0.9, atgm: 0.85, manpads: 0.7,
  loitering: 0.95, antiship: 1.25,
};
function explosionWeight(weapon) {
  if (weapon in EXPLOSION_WEIGHT) return EXPLOSION_WEIGHT[weapon];
  const wdef = WEAPON_DEFS[weapon];
  return wdef && wdef.kind === 'ballistic' ? 1.0 : 0.8;
}

// Drains a single world.sfx event (main.js calls this once per queued event,
// then clears world.sfx itself — see that file's loop()). Never throws:
// every branch below either resolves to a playSound() call (itself guarded
// top to bottom) or falls through to nothing.
export function playSfx(evt, cam) {
  const c = ensureCtx();
  if (!c || c.state !== 'running') return; // no context yet, or still waiting on the gesture unlock
  const g = distanceGain(evt.x, evt.y, cam);
  if (g <= 0) return; // culled — out of audible range for the current camera
  const wdef = WEAPON_DEFS[evt.weapon];
  const kind = wdef ? wdef.kind : null;
  if (evt.kind === 'fire') {
    if (kind === 'ballistic') playSound('cannon', g, { durationHint: 0.6, synthDuration: 0.5 });
    else if (kind === 'gun') playSound('gun', g, { durationHint: 0.15, synthDuration: 0.1 });
    else if (kind === 'homing') playSound('missile', g, { durationHint: 0.5, synthDuration: 0.4 });
  } else if (evt.kind === 'impact') {
    if (kind === 'gun') playSound('impact', g, { durationHint: 0.2, synthDuration: 0.16 });
    else {
      const size = explosionWeight(evt.weapon);
      playSound('explosion', g * (0.75 + size * 0.3), { durationHint: 1.2, synthDuration: 1.1 + size * 0.4, size });
    }
  }
}

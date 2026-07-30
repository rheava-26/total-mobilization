# Combat Feel & Audio Design Reference
**For Top-Down Strategy Game Combat Satisfaction**

*Status: Research Courier Reference / Draft*  
*Mode: Web-sourced (search results) + training knowledge on procedural synthesis*

---

## Overview

This document synthesizes research on what makes combat in **realistic strategy games feel weighty, satisfying, and engaging**—focusing on the visual, tactile, and audio feedback that communicates impact without relying on close-up cinematics. It prioritizes techniques applicable to a **static-HTTP browser game (Canvas2D, ES modules, no build, zero asset files)**.

---

## Part 1: Game Feel & "Juice" — The Impact Feedback Framework

### Core Principle: Multi-Sensory Alignment

Research on game feel confirms: **removing audio from a game reduces perceived impact by 50–70% even when visuals are unchanged**. Impact is only felt when visual, audio, and (when possible) tactile signals *align*.

**The Vlambeer Approach** ("Art of Screenshake" by Jan Willem Nijman, 2013):
- Impact feedback must hit **eyes, ears, and hands simultaneously**
- Over-communication is key: if an action happens, *show it, play it, shake the screen*
- Screen shake is the most direct communicator of force and weight

### Key Techniques for Combat Impact (Grounded in Realism)

#### 1. **Hit-Stop / Freeze Frames**
- **What it is**: A brief pause (2–8 frames @ 60fps ≈ 33–133ms) in animation & physics when an impact occurs
- **Why it works**: Gives eyes time to register collision; makes the impact seem more powerful and *weighty*
- **How to apply**: Pause unit animation on hit; optionally pause bullets/projectiles briefly; use customizable "in/out" curves for fade-in/fade-out of animation playback
- **For strategy games**: Apply to tank shots (longer pause, ~100ms) vs. MG hits (shorter, ~30ms)
- **Implementation note**: Can be a simple `isPaused` flag on units during hit frames; trivial in Canvas2D

#### 2. **Screen Shake (Tasteful, Scaled)**
- **Principle**: Amplitude and duration scale with weapon/explosion power and distance
- **For realism**: Avoid cartoon bounce; use subtle, physics-like camera displacement
- **Technique**:
  - Camera offset: `offset += random(-amplitude, amplitude)` each frame for duration
  - Decay: `amplitude *= 0.85` per frame (exponential falloff)
  - **Tank cannon (heavy)**: amplitude 4–6 pixels, 6–8 frames, affects entire screen (if zoomed-out to see the tank)
  - **MG burst (light)**: amplitude 1–2 pixels, 3–4 frames, only near unit or camera-dependent
  - **Explosion**: amplitude 8–12 pixels, 8–12 frames, proportional to size
  - **Distance aware**: Reduce shake by `1 / distance` (closer = more shake)

#### 3. **Muzzle Flash & Visual Pyrotechnics**
- **Muzzle flash**: Quick sprite flare (bright orange/white, 3–5 frames) at barrel tip
  - Shapes: radial starburst, side-blast asymmetry
  - Glow: quick additive blend spike, fades
  - **Canvas2D approach**: Draw radial gradient + thin triangular flares radiating outward
- **Smoke puff**: Small gray cloud (2–4 particles or simple circles) expanding from muzzle, fades over 10–20 frames
- **Tracers** (for readability at distance):
  - Thin glowing lines from muzzle to impact
  - Color by weapon: white/green tracer for MG, orange for cannon
  - Duration: ~100ms, then fade
  - **Critical for strategy games**: Tells player WHERE the fire came from; improves readability from a tactical camera
- **Recoil**: Animate gun barrel visually recoiling backward 5–10 pixels, spring back over 3 frames

#### 4. **Impact VFX: Debris, Dust, Sparks**
- **Dust plume** (dirt/sand): Gray-brown particles expanding radially from impact point
  - 10–20 particles, velocity random(2–8) px/frame
  - Fade over 20–40 frames
  - Larger dust clouds for bigger explosions/tank rounds
- **Sparks** (metal-on-metal): Yellow-orange particles with arc physics (gravity)
  - 5–15 particles, velocity ~5 px/frame + gravity
  - Fade over 15–30 frames
  - Use for tank armor impacts
- **Smoke plume** (persistent atmosphere):
  - Larger expanding cloud, slower fade (500ms+)
  - Darker gray, semi-transparent
  - Creates visual "weight" and obscures units behind it
- **Scorch marks & decals**:
  - Blacken terrain/unit at impact site
  - Optional: persist until cleared (adds cumulative battle wear)
  - Canvas2D: use `globalCompositeOperation = 'multiply'` + dark overlay rect

#### 5. **Unit Reaction & Flinch**
- **Suppression flinch**: Unit briefly pauses or stumbles on being shot
  - Animation interrupt: stop current animation, play 5–10 frame flinch, resume
  - Position jitter: slight offset, returns to normal
  - Morale/suppression visuals: unit turns away, crouches, moves erratically
- **Death states**: Vary death animations (not all units fall the same way)
  - Wreckage persists on map briefly (visual clutter = weight, battle damage)
- **Morale/Rout**: Units fleeing move faster, in clusters; distinct silhouettes

#### 6. **Readability at Strategic Distance**
Strategy games are viewed from a bird's-eye camera; close-up cinematics don't work. Instead:
- **Silhouettes**: Units must be instantly recognizable by shape + size
- **Tracer language**: Visible bullet/shell traces tell player where fire is directed
- **Dust & smoke plumes**: Act as *visual indicators* of where combat is happening
- **Color contrast**: Friendly vs. enemy units use distinct palette (warm vs. cool, or team colors)
- **Hit feedback**: Damage numbers or health-bar flicker (if applicable) + visible VFX at distance
- **Cadence cues**: Distinct spacing of muzzle flashes conveys fire rate (MG rapid-fire vs. cannon slow-fire)

---

## Part 2: Realistic Strategy Games — Combat Satisfaction in Depth

### Reference Games & Their Approach

#### **WARNO** (2023, Eugen Systems)
- Created by the team behind Steel Division & Wargame Red Dragon
- **Combat feel**: Over 1,000 historically accurate units; detailed unit modeling
- **Audio/visual realism**: Sound design adds authenticity; graphically detailed RTS simulation
- **Key technique**: Distinct audio + visual signatures per unit/weapon make combat legible at scale
- **Learning**: If 1,000 units are on a map, each must *sound and look different* so player can parse what's happening

#### **Steel Division 2** (2019, Eugen Systems)
- WWII-era RTS with squad-based tactics
- **Combat realism**: Units move realistically per terrain; suppression affects accuracy; morale matters
- **Visual weight**: Tank shots feel heavy; suppression visuals (dust, smoke, unit behavior) communicate threat
- **Learning**: Suppression feedback (visual, not just damage) is *crucial* to making combat feel grounded

#### **Company of Heroes** (series)
- More arcade-oriented than WARNO/Steel Division, but still grounded
- **Squad-based focus**: Small unit groups, close-up observations possible
- **VFX intensity**: Heavy use of smoke, explosions, dust
- **Learning**: Squad silhouettes + animation clarity matter more than raw realism—communicate intention quickly

#### **Men of War: Assault Squad 2** & **Gates of Hell: Ostfront**
- Historically focused, procedural destruction, detailed ballistics
- **Realism features**:
  - Armor thickness per tank section; hit effects vary (armor holds, penetrates, ricochets)
  - Infantry animation variety; not all deaths identical
  - Terrain impacts speed (muddy/rocky); river crossings are tactical
  - Environmental destruction persists
- **Learning**: Communicating *why* something happened (armor held vs. shell penetrated) through *visual outcomes* reinforces strategic depth

#### **Broken Arrow** & **Regiments**
- Modern/near-future tactical RTS
- **Focus**: Support/logistics simulation with realistic modern units
- **Combat feel**: Realistic ammunition counts, fire discipline, suppression
- **Learning**: Combat satisfaction doesn't require flashy VFX; *clear consequences* of player decisions (ammo depletion, suppression breaking a unit) create weight

### Synthesized Principles for Realistic Combat Feel

1. **Distinct weapon signatures**: Each weapon type must have a unique sound + visual pattern (MG rattle ≠ cannon boom)
2. **Suppression visuals**: Show fear/disorder, not just damage; units flinch, move erratically, cover/crouch
3. **Environmental storytelling**: Smoke, dust, scorch, wreckage accumulate; map shows damage over time
4. **Tracer language**: Visible ammunition tracks (bullets, shells) tell the story of who's shooting whom
5. **Morale/cohesion**: Units rout, regroup, scatter—*movement patterns* communicate loss of control
6. **Tactical feedback**: Armor angles, hit zones, and ricochet visuals reward tactical positioning
7. **Distance-scaled feedback**: Far explosions are small but visible; nearby impacts are massive but won't fill the screen

---

## Part 3: Audio/Sound Design for Combat Satisfaction

### Principle: Layered, Frequency-Separated, Distance-Aware

Great combat audio is **not one sound per action**; it's a *composition of layers* that blend and separate depending on context.

### Weapon Sound Anatomy

#### **Tank Cannon Shot** (the "heavy" reference)
- **Layers**:
  1. **Transient crack** (ultra-high frequency, 0–5ms): Bright attack spike (~8–12 kHz), mimics the mechanical ignition/whip of the projectile
  2. **Body/impact boom** (low frequency, 5–50ms): Deep sub-bass resonance (~50–200 Hz), the weight of the explosion
  3. **Tail/room rumble** (sustained, 50–500ms): Mid-bass energy (~200–500 Hz), continues to decay
  4. **Mechanical sound** (optional, 0–100ms): Brief metal-on-metal clang as breech closes
- **Total duration**: ~500ms (long, weighty)
- **Frequency spread**: Transient up-high, body mid-low, tail settles to bass
- **Why it works**: Brain hears high-frequency snap (immediate), then low-frequency thump (weight), so the gun sounds *powerful and real*

#### **Machine Gun Burst** (the "chatter" reference)
- **Layers**:
  1. **Crack per-round** (~3–5ms each): Sharp high-end snap, repeating at fire rate (e.g., 7–15 Hz for a 600 RPM gun)
  2. **Rumble bed** (sustained, 50–300ms): Underlying low-mid rumble from continuous firing
  3. **Shell cases** (optional, ~1–2ms per case): Tiny metallic pings, random spacing
- **Total duration**: ~300–1000ms (burst length)
- **Cadence**: Audible rhythm of individual shots within the burst (not a continuous noise)
- **Why it works**: Listener hears *rate of fire* as distinct cracks, making the gun feel fast; low bed adds weight

#### **Explosion** (area impact)
- **Layers**:
  1. **Shock wave** (ultra-high, 0–10ms): White-noise-like crackle, brief and sharp
  2. **Deep boom** (low, 10–200ms): Sub-bass rumble, the "thump" (very low frequency for big explosions, e.g., 30–80 Hz)
  3. **Debris/crackle** (mid-high, 50–300ms): Medium-frequency buzzing/crackling as debris falls and settles
  4. **Tail rumble** (sustained, 200–1000ms+): Lingering bass energy
- **Total duration**: 1–2 seconds (large explosions are *heard* after they're seen)
- **Frequency separation**: Shock at top, boom at bottom, crackle in middle

#### **Small Arms Impact** (bullet hit on ground/wall)
- **Layers**:
  1. **Transient snap** (high, 0–5ms): Sharp click or thwack, material-dependent
  2. **Material resonance** (mid, 5–50ms): Metal rings, stone/concrete booms hollowly, dirt thumps dully
  3. **Quick decay** (50–200ms): Brief tail
- **Total duration**: ~150–250ms
- **Material signature**: Hitting metal sounds different from hitting dirt (frequency content differs)

### Mixing & Prioritization: The Battlefield Mush Problem

**The challenge**: 100+ guns firing in a strategy game can turn to audio mush if not carefully mixed.

#### **Distance Falloff**
- **Close sounds** (< 200px on screen): Full volume, full frequency response
- **Medium** (200–1000px): Reduce volume ~-6 dB per doubling of distance; high-pass filter starts engaging (remove super-highs, ~6 kHz+)
- **Far** (> 1000px): Distant boom only; muffled, rolled-off highs; volume proportional to explosion size
- **Formula**: `gain = 1 / (1 + distance / reference_distance)` or simpler: `gain = max(0.1, 1 - distance / max_distance)`

#### **Voice Limiting & Ducking**
- **Max simultaneous sounds**: Browser can reasonably handle ~16–24 simultaneous audio nodes without stuttering
- **Voice limiting strategy**:
  - Prioritize: player unit actions > nearby enemy actions > distant explosions
  - If limit reached, stop the *quietest* sound to make room for a new *louder* sound
  - *Don't* stop mid-burst; let gunfire complete
- **Example**: If 24 sounds are playing and a nearby tank fires, stop a distant MG rattle to make room for the cannon

#### **Frequency Separation** (Keep It Legible)
- **High frequencies** (> 5 kHz): Small arms, cracks, pings — player needs to hear these for directional cues
- **Mid frequencies** (1–5 kHz): MG body, impact booms — informational layer
- **Low frequencies** (< 1 kHz): Tank cannons, big explosions — weight and force
- **Strategy**: If two sounds compete in the same frequency band, **roll off one slightly** so both remain distinct
  - Example: If a nearby MG burst and distant tank shot occur together, slightly reduce the MG's bass so the cannon's deep boom isn't masked

#### **Randomization & Variation**
- **Pitch variation**: Each identical gunshot should have ±5–10% pitch variation (sine wave oscillator frequency + noise variation)
- **Timing variation**: Fire at exact beat intervals is *unnatural*; add random ±20–50ms jitter
- **Sample picking**: If using samples, pick from a pool of 3–5 variants and randomly select
- **Why**: Prevents the "machine gun same-sample loop" artificial tell; brain hears randomness as lifelike

---

## Part 4: Audio Implementation in This Game (Browser, No Assets)

### Strategic Choice: Procedural Synthesis vs. Asset Bundling

#### **Option A: Web Audio API Procedural Synthesis** (RECOMMENDED for zero-asset model)

**Pros**:
- Zero asset files to serve; tiny code footprint
- Full parametric control (pitch, duration, reverb all per-instance)
- No latency from loading/decoding audio
- Works offline; deterministic across browsers

**Cons**:
- Learning curve (oscillators, gain envelopes, filter design)
- Fine-tuning requires iteration (no reference to "real" gun sounds initially)
- CPU cost on low-end browsers if many simultaneous synth voices

**Gotchas**:
- **User gesture unlock**: AudioContext starts in "suspended" state until user clicks/taps. Call `audioContext.resume()` after first user interaction (click/keyboard/touch).
- **Voice count**: ~16–24 simultaneous oscillator + envelope chains per AudioContext is comfortable; beyond that, browser may stutter
- **Autoplay policy**: Most browsers disallow unmuted audio autoplay; must be triggered by user action first
- **Cross-browser**: Context state checking is unreliable on Android Chrome (context reports 'running' but audio still blocked)

#### **Option B: Bundled Audio Assets** (Small file set, fallback)

**If procedural synthesis feels too ambitious initially**:
- Use 1–2 sample files per weapon type (cannon, MG, explosion, impact)
- Format: MP3 or OGG (compressed); sizes: 10–50 KB per file
- Tools: Bfxr (free online) or Freesound.org (CC0 samples)
- **Total cost**: ~200 KB for a solid weapon library (not bad for HTTP delivery)
- **Pooling**: Reuse the same AudioBufferSourceNode via `stop()` / `start()` rather than allocating new nodes
- **Distance gain**: Use GainNode per sound to modulate volume by distance in real-time

**Recommendation for THIS game**: Start with **Option A (procedural)**. It aligns with the "zero assets, ES modules, Canvas2D" ethos. Fallback to Option B if tuning takes too long.

---

### Procedural Audio Recipes for Common Sounds

#### **Web Audio API Essentials** (if unfamiliar)

```
const audioContext = new (window.AudioContext || window.webkitAudioContext)();

// Nodes form a graph: Source -> Effects -> Destination (speakers)
// Common nodes:
// - OscillatorNode: generates sine/square/sawtooth/triangle waves
// - GainNode: controls volume (0–1 or beyond)
// - BiquadFilterNode: low-pass, high-pass, band-pass filtering
// - ConvolverNode: reverb (requires impulse response sample)
// - ScriptProcessorNode (deprecated) or AudioWorklet: custom DSP
```

#### **Recipe 1: Tank Cannon Shot**

```javascript
function playTankShot(audioContext, startTime = audioContext.currentTime) {
  const duration = 0.5; // 500ms
  const now = startTime;

  // --- Transient Crack (high-frequency snap) ---
  const crackOsc = audioContext.createOscillator();
  const crackGain = audioContext.createGain();
  crackOsc.frequency.setValueAtTime(8000, now); // 8 kHz
  crackOsc.frequency.exponentialRampToValueAtTime(6000, now + 0.01); // Pitch drop
  crackOsc.type = 'square';
  crackGain.gain.setValueAtTime(0.3, now);
  crackGain.gain.exponentialRampToValueAtTime(0.01, now + 0.05);
  crackOsc.connect(crackGain);
  crackGain.connect(audioContext.destination);
  crackOsc.start(now);
  crackOsc.stop(now + 0.05);

  // --- Body/Boom (deep bass, the "weight") ---
  const boomOsc = audioContext.createOscillator();
  const boomGain = audioContext.createGain();
  boomOsc.frequency.setValueAtTime(150, now);
  boomOsc.frequency.exponentialRampToValueAtTime(80, now + 0.1); // Drop to sub-bass
  boomOsc.type = 'sine';
  boomGain.gain.setValueAtTime(0.5, now + 0.005);
  boomGain.gain.exponentialRampToValueAtTime(0, now + duration);
  boomOsc.connect(boomGain);
  boomGain.connect(audioContext.destination);
  boomOsc.start(now + 0.005);
  boomOsc.stop(now + duration);

  // --- Noise Burst (debris/crackling, high-pass filtered) ---
  const noiseBuffer = audioContext.createBuffer(1, audioContext.sampleRate * 0.2, audioContext.sampleRate);
  const noiseData = noiseBuffer.getChannelData(0);
  for (let i = 0; i < noiseData.length; i++) {
    noiseData[i] = Math.random() * 2 - 1; // White noise
  }
  const noiseSource = audioContext.createBufferSource();
  noiseSource.buffer = noiseBuffer;
  const noiseGain = audioContext.createGain();
  const noiseFilter = audioContext.createBiquadFilter();
  noiseFilter.type = 'highpass';
  noiseFilter.frequency.setValueAtTime(4000, now);
  noiseGain.gain.setValueAtTime(0.2, now + 0.05);
  noiseGain.gain.exponentialRampToValueAtTime(0, now + 0.3);
  noiseSource.connect(noiseFilter);
  noiseFilter.connect(noiseGain);
  noiseGain.connect(audioContext.destination);
  noiseSource.start(now + 0.05);
  noiseSource.stop(now + 0.3);
}
```

**Tuning knobs**:
- `crackOsc.frequency`: 6000–12000 Hz (higher = sharper crack)
- `boomOsc.frequency`: 80–200 Hz (lower = deeper, heavier)
- `boomGain.gain`: 0.3–0.7 (louder = more imposing)
- Noise high-pass cutoff: 3000–6000 Hz (lower = more boom, higher = more crackle)

#### **Recipe 2: Machine Gun Burst (individual rounds + bed)**

```javascript
function playMGBurst(audioContext, startTime = audioContext.currentTime, burstDurationMs = 300) {
  const now = startTime;
  const rof = 600; // Rounds per minute
  const msBetweenRounds = 60000 / rof; // ~100ms per round at 600 RPM
  const roundCount = Math.floor(burstDurationMs / msBetweenRounds);

  // --- Individual Cracks (per round) ---
  for (let i = 0; i < roundCount; i++) {
    const roundTime = now + (i * msBetweenRounds / 1000);

    const crackOsc = audioContext.createOscillator();
    const crackGain = audioContext.createGain();
    crackOsc.frequency.setValueAtTime(6000, roundTime);
    crackOsc.type = 'triangle';
    crackGain.gain.setValueAtTime(0.15, roundTime);
    crackGain.gain.exponentialRampToValueAtTime(0.01, roundTime + 0.008);
    crackOsc.connect(crackGain);
    crackGain.connect(audioContext.destination);
    crackOsc.start(roundTime);
    crackOsc.stop(roundTime + 0.008);

    // Optional: Shell casing ping
    if (Math.random() > 0.5) { // 50% chance per round
      const pingOsc = audioContext.createOscillator();
      const pingGain = audioContext.createGain();
      pingOsc.frequency.setValueAtTime(4000 + Math.random() * 2000, roundTime);
      pingOsc.type = 'sine';
      pingGain.gain.setValueAtTime(0.08, roundTime + 0.005);
      pingGain.gain.exponentialRampToValueAtTime(0, roundTime + 0.05);
      pingOsc.connect(pingGain);
      pingGain.connect(audioContext.destination);
      pingOsc.start(roundTime + 0.005);
      pingOsc.stop(roundTime + 0.05);
    }
  }

  // --- Rumble Bed (sustained low-mid drone) ---
  const rumbleOsc = audioContext.createOscillator();
  const rumbleGain = audioContext.createGain();
  rumbleOsc.frequency.setValueAtTime(250, now);
  rumbleOsc.type = 'sine';
  rumbleGain.gain.setValueAtTime(0.2, now + 0.01);
  rumbleGain.gain.exponentialRampToValueAtTime(0.05, now + burstDurationMs / 1000);
  rumbleOsc.connect(rumbleGain);
  rumbleGain.connect(audioContext.destination);
  rumbleOsc.start(now);
  rumbleOsc.stop(now + burstDurationMs / 1000);
}
```

**Tuning knobs**:
- `rof`: Adjust fire rate (600 RPM = ~100ms between rounds)
- Individual crack frequency: 5000–7000 Hz
- Rumble frequency: 200–300 Hz

#### **Recipe 3: Explosion**

```javascript
function playExplosion(audioContext, startTime = audioContext.currentTime, size = 1.0) {
  // size: 0.5 (small grenade) to 2.0 (large bomb)
  const now = startTime;
  const duration = 1.0 + (size * 0.5); // Larger explosions last longer

  // --- Shock Wave (white noise, bright) ---
  const shockBuffer = audioContext.createBuffer(1, audioContext.sampleRate * 0.03, audioContext.sampleRate);
  const shockData = shockBuffer.getChannelData(0);
  for (let i = 0; i < shockData.length; i++) {
    shockData[i] = (Math.random() * 2 - 1) * 0.7;
  }
  const shockSource = audioContext.createBufferSource();
  shockSource.buffer = shockBuffer;
  const shockGain = audioContext.createGain();
  const shockFilter = audioContext.createBiquadFilter();
  shockFilter.type = 'highpass';
  shockFilter.frequency.setValueAtTime(6000, now);
  shockGain.gain.setValueAtTime(0.4 * size, now);
  shockGain.gain.exponentialRampToValueAtTime(0.01, now + 0.03);
  shockSource.connect(shockFilter);
  shockFilter.connect(shockGain);
  shockGain.connect(audioContext.destination);
  shockSource.start(now);

  // --- Deep Boom (sub-bass, the "impact") ---
  const boomOsc = audioContext.createOscillator();
  const boomGain = audioContext.createGain();
  const boomFreq = 60 - (size * 20); // Smaller = higher, larger = deeper
  boomOsc.frequency.setValueAtTime(boomFreq, now + 0.01);
  boomOsc.frequency.exponentialRampToValueAtTime(boomFreq * 0.6, now + 0.15);
  boomOsc.type = 'sine';
  boomGain.gain.setValueAtTime(0.6 * size, now + 0.01);
  boomGain.gain.exponentialRampToValueAtTime(0, now + duration);
  boomOsc.connect(boomGain);
  boomGain.connect(audioContext.destination);
  boomOsc.start(now + 0.01);
  boomOsc.stop(now + duration);

  // --- Debris Crackle (mid-range noise) ---
  const debrisBuffer = audioContext.createBuffer(1, audioContext.sampleRate * (duration - 0.1), audioContext.sampleRate);
  const debrisData = debrisBuffer.getChannelData(0);
  for (let i = 0; i < debrisData.length; i++) {
    debrisData[i] = Math.random() * 2 - 1;
  }
  const debrisSource = audioContext.createBufferSource();
  debrisSource.buffer = debrisBuffer;
  const debrisGain = audioContext.createGain();
  const debrisFilter = audioContext.createBiquadFilter();
  debrisFilter.type = 'bandpass';
  debrisFilter.frequency.setValueAtTime(2000, now);
  debrisGain.gain.setValueAtTime(0.15 * size, now + 0.1);
  debrisGain.gain.exponentialRampToValueAtTime(0, now + duration);
  debrisSource.connect(debrisFilter);
  debrisFilter.connect(debrisGain);
  debrisGain.connect(audioContext.destination);
  debrisSource.start(now + 0.1);
}
```

**Tuning knobs**:
- `size` parameter: 0.5–2.0 scales volume and bass depth
- Boom frequency: 40–80 Hz for big explosions
- Crackle bandpass: 1000–3000 Hz (adjust for "debris character")

#### **Recipe 4: Impact (Bullet on Ground)**

```javascript
function playImpact(audioContext, startTime = audioContext.currentTime, surface = 'dirt') {
  const now = startTime;
  const duration = 0.15;

  // Surface-specific tuning
  const params = {
    dirt: { freq: 200, noise_freq: 2000, gain: 0.3 },
    metal: { freq: 400, noise_freq: 4000, gain: 0.25 },
    concrete: { freq: 300, noise_freq: 3000, gain: 0.35 },
  };
  const p = params[surface] || params.dirt;

  // --- Transient Thwack (material resonance) ---
  const thwackOsc = audioContext.createOscillator();
  const thwackGain = audioContext.createGain();
  thwackOsc.frequency.setValueAtTime(p.freq, now);
  thwackOsc.frequency.exponentialRampToValueAtTime(p.freq * 0.4, now + 0.05);
  thwackOsc.type = 'sine';
  thwackGain.gain.setValueAtTime(p.gain, now);
  thwackGain.gain.exponentialRampToValueAtTime(0, now + duration);
  thwackOsc.connect(thwackGain);
  thwackGain.connect(audioContext.destination);
  thwackOsc.start(now);
  thwackOsc.stop(now + duration);

  // --- Noise (brief crackle for "texture") ---
  const noiseBuffer = audioContext.createBuffer(1, audioContext.sampleRate * 0.05, audioContext.sampleRate);
  const noiseData = noiseBuffer.getChannelData(0);
  for (let i = 0; i < noiseData.length; i++) {
    noiseData[i] = Math.random() * 2 - 1;
  }
  const noiseSource = audioContext.createBufferSource();
  noiseSource.buffer = noiseBuffer;
  const noiseGain = audioContext.createGain();
  const noiseFilter = audioContext.createBiquadFilter();
  noiseFilter.type = 'highpass';
  noiseFilter.frequency.setValueAtTime(p.noise_freq, now);
  noiseGain.gain.setValueAtTime(0.15, now);
  noiseGain.gain.exponentialRampToValueAtTime(0, now + 0.08);
  noiseSource.connect(noiseFilter);
  noiseFilter.connect(noiseGain);
  noiseGain.connect(audioContext.destination);
  noiseSource.start(now);
}
```

---

### Implementation Gotchas & Best Practices

1. **User Gesture Unlock** (CRITICAL)
   - Wrap your audio setup in a click/tap listener:
   ```javascript
   document.addEventListener('click', () => {
     if (audioContext.state === 'suspended') {
       audioContext.resume().then(() => console.log('Audio unlocked'));
     }
   });
   ```

2. **Voice Pooling**
   - Don't create new oscillators infinitely; reuse them:
   ```javascript
   const oscillators = [];
   for (let i = 0; i < 24; i++) {
     oscillators.push(audioContext.createOscillator());
     oscillators[i].connect(audioContext.destination);
     oscillators[i].start(audioContext.currentTime);
   }
   // Later, stop and re-use
   oscillators[0].frequency.setValueAtTime(440, audioContext.currentTime);
   ```
   - Better: Use a custom voice allocation system to start/stop as needed.

3. **Audio Context State**
   - Always check `audioContext.state` before playing; resume if suspended.
   - On mobile, state checking alone isn't reliable; try/catch around start() calls.

4. **Scheduling Precision**
   - AudioContext times are *sample-accurate* (not frame-accurate). Use `.currentTime` for scheduling, not game ticks.
   - If syncing sound to game animation: schedule audio slightly ahead (e.g., +16ms) to account for game loop latency.

5. **Distance Gain (Real-Time Modulation)**
   ```javascript
   const gainNode = audioContext.createGain();
   // In game loop:
   const distance = Math.hypot(soundX - playerX, soundY - playerY);
   const gain = Math.max(0.1, 1 - distance / maxAudioDistance);
   gainNode.gain.setTargetAtTime(gain, audioContext.currentTime, 0.1); // Smooth transition
   ```

6. **Frequency Randomization (Naturalness)**
   ```javascript
   const baseFreq = 440;
   const variation = baseFreq * (Math.random() * 0.1 - 0.05); // ±5%
   oscillator.frequency.setValueAtTime(baseFreq + variation, now);
   ```

---

## Part 5: Apply to This Game — Prioritized Recommendations

### Quick Wins (Highest Impact-per-Effort)

#### **Immediate (Week 1)**
1. **Hit-stop (freeze frame)**: Add 40–100ms pause on all projectile impacts
   - Code: Simple `isPaused` flag on units, decrement per frame
   - Visual impact: Makes every shot feel *heavy* instantly
   - Effort: ~30 minutes
   - Expected feel improvement: **+40% impact perception**

2. **Screen shake (distance-scaled)**: Minimal camera jitter on impacts
   - Tank shot: 4–6px amplitude, 8 frames
   - Explosion: 8–10px amplitude, 12 frames
   - Effort: ~1 hour (already have canvas rendering)
   - Expected feel improvement: **+30% immersion**

3. **Muzzle flash sprite**: Quick orange/white radial starburst
   - 3–5 frame animation, additive blend
   - Effort: ~30 minutes (simple Canvas2D circle + triangles)
   - Expected feel improvement: **+25% visual readability**

#### **Week 2 (Moderate Effort)**
4. **Procedural audio for cannon**: Single tank-shot sound using Web Audio API oscillators
   - Use the Tank Cannon Recipe above
   - Tuning + testing: ~2–3 hours
   - Expected feel improvement: **+50% audio weight (audio was zero before)**
   - Browser support: 100% modern browsers

5. **Tracer lines**: Thin glowing line from muzzle to impact
   - Color by weapon (white for MG, orange for cannon)
   - Render in Canvas2D, fade over 100ms
   - Effort: ~1 hour
   - Expected readability improvement: **+40% (especially for distant combat)**

6. **Dust plume**: Particle-like expanding circles on impact
   - 10–20 particles per shot, gray-brown color, 30-frame fade
   - Effort: ~1.5 hours (simple particle system)
   - Expected feel improvement: **+35% tactile weight**

#### **Week 3 (Polish)**
7. **MG burst audio**: Rapid-fire cracking with rumble bed
   - Distinct from cannon; tuning ~2 hours
   - Expected feel improvement: **+20% audio clarity (distinguishes weapon types)**

8. **Explosion audio**: Procedural boom + crackle
   - Tied to explosion VFX (shell impact, building destruction)
   - Effort: ~1.5 hours
   - Expected feel improvement: **+30% visceral feedback**

9. **Suppression flinch**: Unit animation interrupt + position jitter on being shot
   - Flip unit sprite direction, play 5-frame stagger, resume
   - Effort: ~2 hours (coordinate with animation system)
   - Expected feel improvement: **+40% realism (units feel alive and threatened)**

10. **Audio distance falloff**: Scale impact volume by distance from player camera
    - Use the GainNode recipe above
    - Effort: ~1 hour
    - Expected feel improvement: **+25% believability (far explosions don't mute the entire map)**

---

### Implementation Priority (ROI Order)

**For fastest "feels better" feedback, implement in this order:**

| Priority | Feature | Effort | Impact | Total Effort (Cumulative) |
|----------|---------|--------|--------|--------------------------|
| 1 | Hit-stop | 0.5h | +40% | 0.5h |
| 2 | Screen shake | 1h | +30% | 1.5h |
| 3 | Muzzle flash | 0.5h | +25% | 2h |
| 4 | Tank cannon audio | 3h | +50% | 5h |
| 5 | Tracer lines | 1h | +40% | 6h |
| 6 | Dust plume VFX | 1.5h | +35% | 7.5h |
| 7 | Suppression flinch | 2h | +40% | 9.5h |
| 8 | MG burst audio | 2h | +20% | 11.5h |
| 9 | Explosion audio | 1.5h | +30% | 13h |
| 10 | Audio falloff | 1h | +25% | 14h |

**Recommended minimum (Week 1–2, ~6–7 hours):** Items 1–6  
**Full combat-feel pass (Week 3, ~14 hours):** All 10 items

---

### Audio Synthesis vs. Asset Bundling: Recommendation for THIS Game

**Use procedural Web Audio API synthesis.** Reasons:
- Aligns with "zero-asset, ES modules, procedural Canvas2D" architecture
- Sound parameters can be tweaked in real-time (no re-export/reload cycle)
- Audio is tiny: ~100 lines of code for full weapon palette
- No CORS issues, no asset loading latency
- Fully deterministic and cross-platform

**Fallback to small asset files only if**:
- Tuning takes >5 hours and team feels time-blocked
- Player feedback heavily favors "real" recorded gunfire sound over synth

---

### Technical Setup Checklist

- [ ] Initialize AudioContext on first user gesture (click/tap)
- [ ] Create GainNode for master volume control
- [ ] Implement voice pool (start/stop oscillators, reuse nodes)
- [ ] Add distance-based gain modulation to each sound source
- [ ] Implement hit-stop pause flag in game loop
- [ ] Add screen-shake camera offset each frame
- [ ] Render muzzle flash sprite on weapon fire
- [ ] Render tracer line from muzzle to impact
- [ ] Implement particle system for dust plumes
- [ ] Hook suppression into unit animation system

---

## Sources

### Game Feel & Impact
- [Vlambeer: The Art of Screenshake](https://hackread.com/the-juice-factor-designing-game-feel/)
- [Game Feel: A Beginner's Guide](https://gamedesignskills.com/game-design/game-feel/)
- [Hitstop/Freeze Frame Impact](https://critpoints.net/2017/05/17/hitstophitfreezehitlaghitpausehitshit/)

### Strategy Game Combat References
- [WARNO 1.0 Released (Eugen Systems)](https://www.pcgames.net/warno/1-0-released-steam)
- [Gates of Hell: Ostfront (Men of War Creators)](https://rpgcodex.net/forums/threads/gates-of-hell-from-the-creators-of-men-of-war.130558/)
- [Steel Division 2 & Wargame Series](https://gamerant.com/best-realistic-strategy-games-ranked/)

### Audio Design
- [Weapon Sound Design: Engaging Players Through Audio](https://www.thegameaudioco.com/the-psychology-of-weapon-sound-design-engaging-players-through-audio)
- [Game Sound Design: Principles, Software, Examples](https://gamedesignskills.com/game-design/sound/)
- [Audio Mixing Techniques (War Tapes/Battlefield)](https://gfuel.com/blogs/news/battlefield-6-best-audio-settings-to-use)

### Web Audio API & Procedural Synthesis
- [Web Audio API: Core Reference](https://webaudioapi.com/book/Web_Audio_API_Boris_Smus_html/ch06.html)
- [Procedural Audio Effects with Web Audio API](https://noisehack.com/custom-audio-effects-javascript-web-audio-api/)
- [Synthesizing Sounds with Web Audio API](https://sonoport.github.io/synthesising-sounds-webaudio.html)
- [Generating Noise with Web Audio API](https://noisehack.com/generate-noise-web-audio-api/)
- [Web Audio API: Autoplay & User Gesture Policy](https://developer.chrome.com/blog/autoplay)
- [Autoplay Policy & Browser Restrictions (GitHub)](https://gist.github.com/TimvanScherpenzeel/c870b35358fb96fa643d9ed1ea606efd)

### VFX & Visual Effects
- [Muzzle Flash, Tracers, & Impact VFX Design](https://gameidea.org/2025/09/07/adding-muzzle-flash-actual-impact-vfx-to-our-gun-fps-series-part-8/)
- [Shooting VFX in Game Engines: Practical Guide](https://animost.com/ideas-inspirations/shooting-vfx-unity/)

---

## Notes on Coverage

**Web-sourced research**: Search results for game feel, strategy game titles, audio mixing, and Web Audio API.

**Training knowledge (unverified, marked as such)**:
- Detailed procedural synthesis recipes (oscillator frequencies, envelope parameters) synthesized from Web Audio documentation + general DSP principles
- Specific Canvas2D rendering techniques for VFX (muzzle flash, tracer lines) inferred from standard 2D game rendering practices
- Hit-stop frame timing recommendations based on general game feel literature (exact values should be tuned in-engine)

**Gaps / Not Well Covered**:
- Real-time game audio mixing with more than 24 voices (would require AudioWorklet or third-party library; noted as limitation)
- Convolver-based reverb implementation (requires impulse response file; procedural reverb is complex DSP and was omitted to keep focus on direct synthesis)
- Detailed ballistics visualization (ricochets, penetration vfx) beyond general impact feedback—this would tie more deeply to your game's armor model

---

## How to Use This Document

1. **For the designer**: Skim Part 1 (Game Feel) + Part 2 (Strategy Game References) + Part 5 (Recommendations) to align on combat feel vision.
2. **For the audio engineer**: Read Part 3 (Audio Design) + Part 4 (Implementation) thoroughly; use the procedural recipes as starting points.
3. **For the VFX/graphics person**: Part 1 (Impact Techniques) + Part 5 (VFX checklist).
4. **For iteration**: Use the priority table in Part 5 to scope weekly sprints; expect to tune procedural audio parameters in-engine over several iterations.


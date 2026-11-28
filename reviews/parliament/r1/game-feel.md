# Round 1 — The Bureau of Spectacle (`game-feel`)

## 1. Opening Speech

*Honourable members, ministers of ledgers and logistics: I rise on behalf of the Bureau of Spectacle. A war nobody can feel is a war already lost.*

You have spent three sessions arguing over what the Autofleet should be made of. I have stood in the field, and I will tell you what the Autofleet is made of: **silence**. Two hundred and forty Falcons shoot down a Skyfortress, and the Skyfortress dies the way a spreadsheet cell is deleted. The screen jiggles at random, one sound plays on one speaker, and the ledger of the dead goes up by one. Our Treasury now stamps thousands of cached sprites for pennies. The Engineers made us rich in bodies. Nobody made us rich in *impact*.

Look at what we own. One master gain at 0.22. One `noise()` call that builds a fresh buffer on every explosion. A shake that is a dice roll. A boss that dies and gets one banner. A fleet that withdraws and gets a toast.

The President asked for "snappy, big on-hit effects, kind of cartoony but not really." We have the bullets. We do not yet have the *consequences of the bullets*. I propose five bills that cost almost nothing to run and that make a thousand-unit battle sound like a thousand-unit battle. I also propose one bill that is frankly indecent. The Ministry of Ledgers may weep.

*I yield the floor, and I turn the volume up.*

---

## 2. The Gap

Evidence, all from `title.html`:

- **Audio is one flat bus.** `initAudio` (L1120) makes a single `mvol` gain at 0.22, with no compressor, no panner and no distance. `SFX` (L1123) is eight oscillator blips plus `noise()`. Nothing is positional: a Halo dying over the sea sounds identical to a bunker blown up at your feet.
- **A hidden perf cost.** `noise()` (L1122) allocates and fills a new `AudioBuffer` on every call. `boom()` fires it on 30% of mid-size blasts (L5111), and a mass kill can fire it dozens of times a second. It is a GC treadmill. Only `SFX.shoot` is rate-limited (45 ms).
- **Shake is a coin flip.** `shake(a)` (L6675) keeps only the max, capped at 16, and it applies `Math.random()` jitter (L10211). It is global, with no distance attenuation. A kill 4,000 px off-screen shakes you the same as one under your nose. There are 56 `shake(` call sites, so in a big fight the screen is either constantly buzzing or pinned at the cap, and the player stops perceiving shake as information.
- **There is no hit-stop and no time-dilation anywhere.** The only time knob is `G.speed` via `SPEED_CYCLE = [0.5,1,2,3]` (L10849), a manual button.
- **Big moments are text.** A boss kill gives +10 stability (+15 for the flagship, L6863) and a `banner`. Withdrawal sets `G.withdraw` (L5991) and a banner. A Mind-Forge or nuke has a `nukeFlash` and a `SFX.boom`. Report 10, "a flawless defence", ended the same way a skirmish ends.
- **Music reacts only to phase.** `musDuck` (L1298) ducks to 28% for 8 seconds and the playlist swaps PREP to COMBAT. Nothing reads kills per second, enemies on screen, or city HP.
- **Readability.** A 1,000-unit mêlée has no team-silhouette contract that is cheap and uniform. `spark` and `floatText` (L5115-5117) are the entire on-hit vocabulary.

Standing evidence: Article VI says mass is back on the table. Mass without feel is just a bigger number.

---

## 3. Bills

### GAME-FEEL-1 — The Mix Bus (positional, budgeted, honest sound)

**Pitch (in character):** Sound is physics, ministers. Light arrives first and the bang arrives later, and the gap *is* the distance. Give me a stereo panner, a limiter and a voice budget, and the war gets a horizon.

**Player fantasy:** You hear the battle as a space. A Zeus rod hits across the map and you see the flash, count one, two, and feel the thump. The high-orbit fight is thin and crackly. Your bunkers at the front are a wall of sound.

**Clerk's Annex**
- **Mechanic.**
  - A master chain `mvol → DynamicsCompressor (threshold −14 dB, ratio 6, attack 3 ms, release 180 ms) → destination`, so 200 simultaneous booms glue instead of clip.
  - A new `sfxAt(kind, worldX, worldY, power)` wrapper. Pan = clamp((x − cam.x − W/2) / W, −1, 1) through a `StereoPannerNode`. Gain falls off as 1 / (1 + d/900). Beyond 1.5 screens, a lowpass whose cutoff slides 12 kHz to 800 Hz.
  - **Speed-of-sound lag.** Delay = min(0.6 s, dist_px / 2200 real-seconds). The flash is instant and the thump trails, scaled so it reads at gameplay scale and is never frustrating.
  - **Altitude thinning.** Above `THERMO_Y` the lowpass is aggressive and the low sine is removed. Above `SPACE_Y` it is fully silent, except that a satellite kill plays a tiny structure-borne "tick". This matches the existing `vacuum` boom family.
  - **Voice budget.** Max 24 live voices, with priority by `power`. Lower-power voices are dropped when the budget is full, never delayed.
  - **Cached noise.** Build 6 noise buffers once (short, mid, long, crackle, rumble, hiss) and reuse them with random `playbackRate` and `detune`. This fixes the per-call allocation.
- **Real science.** Speed of sound is about 343 m/s at sea level and drops with altitude temperature. Air absorption is frequency-dependent: high frequencies die first over distance, which is why distant thunder is a rumble. A limiter is how film mixes handle loud sequences.
- **Code grounding.** `initAudio` L1120, `blip` L1121, `noise` L1122, `SFX` L1123, `boom` L5075 (SFX call at L5111), `destroyUnit` and `destroyEnemy` call `SFX.boom` and `SFX.rip` (L6825 area). `cam` is read at L10211.
- **Build size.** M (about 150 lines).
- **TM1.**
- **Tenet check.** Pure addition. Nothing is capped or nerfed. The voice budget drops *audio* voices only, never units.
- **Interactions.** Sim and Render councils (the call sites already exist, so no sim change). Naval and Air councils get unique timbres per domain for free. Accessibility: a "Sound Mix: Cinematic / Flat" toggle next to `tShake`.
- **Research tree.** Not a unit or tech bill, so no node. Optional unlock: Radar and Comms Tower buildings (`BUILD_DEFS` ~L3381) already raise the stability cap. They could also "extend your ears", adding a sound-ping when an enemy enters their range.

---

### GAME-FEEL-2 — The Impact Budget (trauma shake and kill-weighted punch)

**Pitch:** Stop rolling dice at the player. A fixed budget of violence per second: I decide how much punch you can feel, and the biggest event wins.

**Player fantasy:** Small kills tick, a capital-ship death thumps, a nuke on your own shoulder rolls the screen. The player *learns* shake as a language, with no more constant buzz.

**Clerk's Annex**
- **Mechanic.**
  - Replace `shakeAmt` with a **trauma** model (Squirrel Eiserloh's GDC talk). `trauma ∈ [0,1]`, and offset = `trauma² × maxShake × noise(t)`, with smooth value noise at 25 Hz instead of `Math.random()`, so it feels like a physical thing rather than static. Add a tiny rotation of ±0.6° at trauma 1.
  - Add `shakeAt(worldX, worldY, p)`: p is attenuated by 1 / (1 + (|x − camCenter| / 700)²) and zero when off-screen by more than one screen.
  - **Log-summed loudness.** Many small events add as `trauma += (1 − trauma) × k × log2(1 + p)`. A thousand small kills raise trauma but saturate smoothly, so a boss kill (p = 11) still *feels* different.
  - **Hit-stop as time dilation, never a freeze.** Only for rare events (boss kill, flagship, capital-district loss, strike impact, nuke). The sim step multiplier drops to 0.12× for 60–110 ms real time. Rendering and camera keep running, so nothing hitches. Use `G.hitStopT`.
  - Existing `hurt` effects (`spark`, `flash`) stay as they are. This bill only governs *camera and time*.
- **Real science.** Weber–Fechner: perceived intensity is roughly logarithmic in stimulus. That is why summing by log makes a thousand kills read as "a lot" and not "broken". Hit-stop works through the known frame-hold effect in fighting games and film "impact frames". It has been used since Street Fighter II.
- **Code grounding.** `shake` L6675 (56 call sites keep their signature, so migration is mechanical), camera apply L10211, sim loop `updateGame(SIM_STEP * 0.45 * G.speed)` at L10205 (multiply by `G.timeScale`), `destroyEnemy` L6834 and stabGain L6863 for the boss hook, `destroyUnit` L6825 for the unit hook.
- **Build size.** M (about 120 lines).
- **TM1.**
- **Tenet check.** Additive. It never touches damage, economy or caps. The existing `shakeOn` and `flashRed` accessibility switches stay honoured (`reducedMotion` forces `timeScale = 1`, trauma = 0).
- **Interactions.** Sim council: `G.timeScale` must be applied in exactly one place so determinism holds. Strikes and Superweapon councils: they get a `feel.big(power)` hook to call. Render council: no new draw cost.
- **Research tree.** None. It is a pure engine feature.

---

### GAME-FEEL-3 — The Money Shot (the Director: slow-motion moments and the withdrawal)

**Pitch:** The designer's fantasy is "watch the army obliterate the invaders". A fantasy needs a camera. The Director picks the one moment per minute that deserves it, and lingers.

**Player fantasy:** The flagship cracks and the world slows. The fleet turns tail, the screen letterboxes, the music drops to one note, and you watch the Autofleet *leave*. It is the Report 10 ending, with a score.

**Clerk's Annex**
- **Mechanic.** A small `Director` state machine with a 45-second cooldown between moments, so it stays special.
  - **Triggers and effects.**
    - Boss or flagship death: `timeScale` 0.25 for 1.4 s, ease-out, camera nudge-zoom to 1.15× centred on the kill, a short low-pass sweep on the music bus (pitch-drop feel).
    - Withdrawal trigger (`G.withdraw` set): 3 s of letterbox bars and a slow pan to the highest-density enemy cluster, with a single sustained chord.
    - Last-city save (a city reaching under 15% and then recovering) and a city-saving AA kill (an interceptor kills a warhead in flight): a 0.6 s dilation and a callout.
  - Free camera wins: any player input cancels the moment instantly.
  - Reuses the existing `banner()` and `toast()`.
- **Real science.** High-speed cinematography (Phantom-style) trades temporal resolution for readability. We are doing time dilation, not frame generation, so the sim runs the same steps at a smaller `dt`, with no extra cost. The audio lowpass emulates Doppler or compression-wave smear.
- **Code grounding.** `updateCamera` L6649, `banner` L10335, `G.withdraw` L5991, `WITHDRAW_BREAK_FRAC` L3858, stabGain L6863, `musDuck` L1298 (extend with a sweep), `SPEED_CYCLE` L10849 (the Director must respect a manual speed of 3× and never fight the player's own speed choice: it multiplies it).
- **Build size.** M (about 200 lines).
- **TM1.**
- **Tenet check.** This is a pure spectacle reward for the player's power and never a penalty. The dilation never makes the enemy stronger relative to the player: the sim step is uniform.
- **Interactions.** Superweapons, Boss and Fleet councils (they own the trigger moments). Society-layer TM2 can later add a "headline" overlay to the same moment. Accessibility: "Cinematics: On / Short / Off".
- **Research tree.** None for the moment itself. The *Director's Chair* could be attached to the existing `particlebeam` node as a flavour flag, but it is better left free.

---

### GAME-FEEL-4 — The Roar (adaptive battle bed and the Kill Ladder)

**Pitch:** A thousand units should *sound* like a thousand. Today it sounds like the same blip, a thousand times, rate-limited to silence. The Roar listens to the battle and plays the battle back.

**Player fantasy:** Open the lid on your fortress and hear it rise. Each unbroken kill in a row climbs a pentatonic ladder, like a Geiger counter of victory, until the fleet breaks. The fantasy of "I did that" is something the player *hears* build.

**Clerk's Annex**
- **Mechanic.**
  - **Battle bed.** One procedural looping layer: filtered noise (crowd-like roar, pink-weighted), driven by an intensity `I = smooth(kills/s × 0.6 + onScreenEnemies/40 + projectilesOnScreen/150)`. Gain, lowpass cutoff and rumble depth follow `I`. The bed *replaces* many individual SFX at high density: the SFX voice budget (GAME-FEEL-1) is the pressure valve.
  - **Kill Ladder.** `killStreak` resets after 2.5 s without a kill. Each kill within the window plays one note from a pentatonic scale, climbing for 12 steps and holding at the top. It is a tiny, soft triangle-wave tick, and it never obscures other sounds.
  - **Music intensity.** If the player supplies local files (v3.84 local mode), expose `MUS.intensity` so the playlist can pick Combat tracks by intensity tier. Procedural fallback: a one-bar sub-bass pulse layered when `I > 0.6`.
  - **Stingers.** Short procedural cues for boss arrival (already `musDuck`), fleet-half-broken, and withdrawal. They sit on the bus *above* the duck, so ducking helps rather than muffles.
- **Real science.** Crowd and battle ambience is closer to filtered pink noise than to white noise (equal energy per octave, matching hearing). A pentatonic scale never produces a dissonant interval whatever order notes arrive in, which is why it is used for procedural reward audio.
- **Code grounding.** `G.kills` and `G._tickKills` (`destroyEnemy` L6834 and the tick counter nearby), `updateGame`, `musTick` (L1335 region), `musDuck` L1298, `loadPlaylists`/`MUSIC` L1204.
- **Build size.** M (about 180 lines).
- **TM1.**
- **Tenet check.** Does not change what you can build or place. Spamming 100 of anything makes the Roar louder, which is the correct reward.
- **Interactions.** Sim council for the stats feed (already collected). A "Sound: Battle Bed" toggle for players who want only SFX.
- **Research tree.** None.

---

### GAME-FEEL-5 — Legible Mass (team silhouette contract and impact language)

**Pitch:** A thousand units is only fun if you can read who is winning in half a second. A war you cannot read is wallpaper.

**Player fantasy:** Zoom all the way out and still see the line, where it is bending and where it holds, because friendly and hostile read as different *shapes of colour* and not as noise.

**Clerk's Annex**
- **Mechanic.**
  - **Pre-baked rim variants.** The sprite cache (`SPR`, L3049 `bakeMs`) already bakes each unit once. Bake a 1-px team rim into the cached bitmap at bake time: warm cyan-white for friendly, hot orange-red for enemy. The cost is paid once per sprite and the per-frame cost is zero. Colour-blind mode (`cbMode`) swaps to blue/orange with a *shape* difference (rim dashed vs solid).
  - **Impact vocabulary by team.** `spark` (L5115) takes a colour parameter: friendly hits throw white-gold sparks, enemy hits throw red-orange. Same pool, no new draw call.
  - **Hit pips.** A 3-frame white-flash recolour using a second cached "flash" bitmap per sprite, on units taking a hit. It is skipped when more than N units are on screen: LOD, not removal.
  - **Zoomed-out density glyphs.** Below a zoom threshold, replace unit sprites with a team-coloured 2×2 pixel dot of the same cached colour, with a faint glow where density is high. This is the "readability of 1,000 units" tool. It also makes the render cheaper at far zoom.
- **Real science.** Figure-ground separation: a thin luminance-contrast outline is the most robust cue for object recognition at small sizes. Colour-blind safety follows the deuteranopia-safe orange/blue axis.
- **Code grounding.** Sprite cache and bake: `SPR` and `bakeMs` (L3049). Colour-blind flag `cbMode` L911. `spark` L5115, `lodOnScreen` (used at L4008), `fxHeavy` (referenced in `boom` L5076).
- **Build size.** L at the edges (cache key changes touch many draw paths), about 400 lines. Coordinate with the Render council.
- **TM1.**
- **Tenet check.** Does not touch the player's toys. LOD only changes how things are drawn when zoomed out.
- **Interactions.** Render council (owns the cache), VFX board in `reviews/vfx/` (I do not propose explosion art, only the hit-flash colouring).
- **Research tree.** None.

---

## 4. Hot Take

### GAME-FEEL-H — The Warhead Cam (ride the shot, and the Hush)

**Pitch:** Fire a strategic strike and the camera *leaves your army* and rides the warhead. It hangs off the missile's shoulder through the stratosphere, dilated to a crawl, until the last 400 px. Then the world goes **silent for one full second**, and the bang arrives late, huge and rolling, behind the flash. Every player will fire one just to see it. That is a quality problem the Ministry of Ledgers should be proud to have.

The twist is the **Hush**. For one second after every big strike, the Roar and the music are cut to nothing. It is the only time the game goes quiet, so it is the only time silence means something: you chose this, and now you hear it.

**Player fantasy:** The nuke is not a button. It is a *film shot* that you directed.

**Clerk's Annex**
- **Mechanic.**
  - On strike confirm (`armSilo` flow to the launch), start `G.warheadCam` bound to the projectile. The camera lerps to a trailing offset (about 140 px behind and 40 above), `timeScale` ramps to 0.2, and the zoom pulls in to 1.3. Skippable with any click or Esc.
  - **Impact sequence.** In the last 0.5 s of flight the flash fires (existing `nukeFlash`), the music and Roar buses drop to zero, and the camera freezes. The bang is delayed 1.0 s by the GAME-FEEL-1 speed-of-sound lag, played as a sub-bass sine at 40 Hz plus a long noise tail. Then the Roar resumes with a stinger.
  - Also works for Kinetic Lance (the rod from space) and Antimatter Strike. The Antimatter Strike gets the SCI-FI variant (below).
  - **Cooldown and opt-out.** Per-session toggle "Warhead Cam: Always / First 3 / Off", and it never triggers while a bunch of other strikes are in flight, so strike chains stay fast.
- **Real science.** Real reentry vehicles do generate a plasma sheath and a radio blackout during reentry. That is real, and the camera can show a brief signal-noise static overlay as the missile hits the thick air. Flash-before-bang is real physics of light and sound. The Hush has a real basis in the near-field overpressure: a blast's sound is preceded by a thin silence in perception as hearing reflexively clamps (the acoustic reflex).
- **SCI-FI (labelled):** the **Antimatter Strike** camera pulls *away* at the end, because annihilation radiates gamma rays that blind sensors. The cam cuts to a white frame, then to a recorded afterimage. Internal logic: the Autofleet's own sensors are damaged by it, which matches Article V (the thrown friend is a single ship that cannot afford to be seen).
- **Code grounding.** `STRAT` L6878 and the strike launch and confirm bar (`armSilo` flow), `G.nukeFlash` draw at L9403, `updateCamera` L6649, `SPEED_CYCLE` L10849, `musDuck` L1298.
- **Build size.** L (about 450 lines), mostly the camera state and the audio-bus mute. It depends on GAME-FEEL-1, 2 and 3 landing first.
- **TM1.**
- **Tenet check.** It rewards *using* the strike, which supports the power fantasy. It does not touch the stability cost of strikes (Article VI: that is another council's lever) and it never delays the strike itself, only the camera.
- **Interactions.** Strikes and Superweapons councils (the stability-cost reform is theirs). The Society layer (TM2) can attach a "witness" headline to the Hush. Accessibility: an off switch that is respected globally.
- **Research tree.** Attach to the existing `directedenergy` branch as a flavour node, "Strike Optics" (cost tier low, non-`dang`, no combo), whose only effect is to unlock Warhead Cam as a *game option*. If the designer prefers no gate, ship it free. A research gate has no gameplay value here, so I recommend free.

---

## 5. Alliances Sought

- **Render / Performance council.** GAME-FEEL-5 lives in their sprite cache, and I need their blessing that rim variants and zoomed-out glyphs add zero per-frame cost.
- **Sim / Architecture council.** One `G.timeScale` in exactly one place keeps hit-stop and the Director deterministic. I will not touch the 30 Hz step.
- **Strikes and Superweapons councils.** They own the "big moment" triggers and the stability-cost reform. The Warhead Cam is their showcase.
- **Boss / Fleet councils.** Withdrawal and the flagship are my best three seconds of film. I will gladly hand them a `feel.big(power)` hook.
- **VFX board (`reviews/vfx/`).** They paint the explosion, I decide the room it goes off in: sound, shake and time. We should agree on one `power` scale (the existing `boom(x, y, p, style)` p-value) so both systems speak the same number.
- **Accessibility, wherever it sits.** Every bill ships with a toggle, and I will not be the council that gives someone a headache.

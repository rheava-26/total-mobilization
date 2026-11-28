# R3 — Clean-slate engine architect review of TOTAL MOBILIZATION

Reviewer lens: "I was hired to make 1,000+ units fight on screen at 60 fps in one static HTML file."
Scripts and raw numbers: `/tmp/claude-0/-home-user-total-mobilization/44c0f627-2c4e-5264-ab7c-20b27493f157/scratchpad/r3/` (t1..t14 .mjs). Nothing in the repo was edited.

---

## 1. Executive summary

1. The simulation is not the problem. Measured headless: 800 mixed units cost 2.5-3.1 ms per 30 Hz step (about 3 us per unit), which matches the designer's real 3.8 ms. The sim plus the render JS is about 9 of the 75 ms real frame.
2. The missing ~66 ms is canvas rasterization/commit cost. The JS timers (`render`, `units`) only time how long it takes to *issue* canvas calls; the work happens afterwards. Headless reproduces the exact signature: 800 units give a 72 ms frame with only 16 ms of JS.
3. Cost is proportional to the number of canvas ops, and units dominate. Every unit is re-drawn every frame as vector art, about 130 ops each (about 107,000 ops/frame at 800 units), under a per-draw `shadowBlur`. Nothing caches unit art (the terrain and minimap are cached, the units are not).
4. Single highest-leverage change: bake each unit art (with its shadow) into a cached bitmap and `drawImage` it. One wrapper around `ART[k]` at one place, no call-site changes, no sim changes. Measured 110 ms to 47 ms (800 units) and 203 ms to 67 ms (1,600 units) for the full frame.
5. Workers/SharedArrayBuffer cannot fix this (the offloadable JS is about 12% of the frame, and the raster is not JS). The grid is fine. The existing LOD and adaptive-FX machinery was tuned against the wrong metric and is switched off or unable to touch the dominant cost.

---

## 2. Lens findings

### 2.0 What I would build from scratch (written before reading the code in depth)

| Idea | My clean-slate answer |
|---|---|
| Data layout | SoA typed arrays (x, y, vx, vy, hp, type, state), free-list slots |
| Rendering | One WebGL instanced draw per sprite atlas page; or, on Canvas2D, a pre-baked sprite atlas and `drawImage` only. Never vector paths per unit per frame |
| Aggregation | Squad/battlegroup = one simulated brain, many drawn bodies; far/small = impostor |
| Tick tiers | Idle/far/off-screen entities think at 4-10 Hz; combat-engaged at full rate |
| Event-driven | Wake units on enemy-in-range events instead of polling |
| Pools | Particles/projectiles as ring buffers in typed arrays, no allocation |
| Threads | Sim in a worker, render in main or OffscreenCanvas, only if the per-frame cost is JS |
| Time | Fixed step with interpolation |
| Culling | Viewport culling both axes, cached static layers (terrain, minimap) |

### 2.1 What the game already does, per idea

| Idea | Status | Evidence |
|---|---|---|
| Fixed-step + interpolation | **Done well** | `SIM_HZ = 30`, `simAcc` (L10073 `frame`), `simAlpha`, `interpEnter/interpExit` (L10055). Cost of interp swap is trivial. |
| Spatial grid | **Done well** | `class SpatialGrid` L4271, `GRID_E/P/F/A` L4291; lazy sync, slack padding, per-phase rebuild. Profile: grid build+extend about 2.4% of sim. See Q3. |
| Static-layer caching | **Done well for terrain/minimap, missing for units** | `bakeTerrain/TCACHE` L1466-1624 (terrain 0.1 ms real). `MMC` minimap 10 Hz cache L9627. `_glowCache` caches the *gradient object* only (L9545). |
| Viewport culling | **Done** | x via `sx()` and y band `VY0/VY1` in `renderGame` L9148+; `RSTAT.culled`. |
| Aggregate units | **Half done** | `SQUAD_LOADOUT` L3883 (inf = 3 bodies/1 entity, fighter flight, cdrone swarm up to 48 bodies); `BATTLEGROUPS` L4113 (drone/clone family only, shared AI brain, max 20). Heavy armour, SAM, arty, rail, air wings, ships: all one entity per body. And aggregation only reduces *sim*, which is already cheap; every body is still drawn individually. |
| Tiered tick rates | **Half done, and low value** | `u._huntT` 0.5 s hunt scan (L8019), `lodBuildEngageIndex` L7666, bg followers skip AI. Sim is only about 3 us/unit so there is little left to save. |
| Event-driven | Missing | Everything polls; irrelevant at measured sim cost. |
| Object pools | Missing | `G.parts.push({...})`, `G.proj.push({...})` literals. GC is 2.8% of the sim profile. Minor. |
| SoA / typed arrays | Missing | Units are 60+ field object literals with fields added lazily (`_rx`, `_hunt`, `px`...). Costs hidden-class churn but sim is cheap. A 12k-line rewrite for under 2 ms: **no**. |
| Batched/instanced rendering | **Missing, and this is the whole problem** | No sprite cache anywhere for units. L9259: per unit `c.save(); c.translate; shadowBlur=2.4; ART[d.art](c, s, aim); c.restore()`. |
| LOD tiers | **Present but disabled by default** | `_heavyLOD`, `_extremeLOD` (L9155+) are gated on `!fullFx`; `let fullFx = true` (L909). Comment there says "render is cheap by comparison", which is the misreading (see 2.2). |
| Adaptive FX | **Present but cannot reach the dominant cost** | `fxTick` L914 scales particle cap/glow cap/trails; the per-unit `shadowBlur = 2.4` (L9259) is not wired to `FXB`/`_fxNoBlur`. |
| Workers/OffscreenCanvas | Missing | See Q2. |

### 2.2 Why the existing diagnosis fits the timers but not the machine

The real report: sim 3.8 ms x 1.4 steps, render 3.9 ms, but a 75.4 ms real frame. That is 12% explained. `perfAdd('render')` (L10091) brackets only `drawWorld+renderGame+minimap`, i.e. JS time spent *recording* canvas commands. Chrome rasterizes them after the rAF task (or when the command buffer fills, which then shows up inside the JS timer). So the in-code claim at L909 ("the sim is what's ever actually slow, render is cheap") and the `gap` column comment at L921 are describing JS issue time, not frame cost.

### 2.3 Measurements (headless Chromium, SwiftShader, 1920x1080)

**M1. Sim cost per unit (t1, t10).** 800 mixed units (inf, tank, heavy, aa, arty, railgun, sam, fighter, strikejet, heli, gunboat, space frigate), prep phase, no hostiles (all units idle):

- 3.1 ms median per step, p90 6.4 ms (t1); 2.5 ms average over 600 steps (t10).
- CPU profile: `updateGameInner` self 14%, `updUnits` 11%, `updShrap` 6%, `sepU` 2%, grid `build+_ext` 2.4%, GC 2.8%.
- Real machine: 3.8 ms for 792 units. The headless sim reproduces the real sim cost, so the headless render numbers below are credible *as a model*.
- Upper bound on "tier idle units" gain: even if idle units cost zero, saves at most about 2.5 ms/step, about 3% of a 75 ms frame.

**M2. Live frame loop vs unit count (t2).** Real rAF intervals, sim running, `js` = whole `frame()` callback:

| units | median frame ms | mean ms | js ms | sim ms | steps | gap (frame - js) |
|---|---|---|---|---|---|---|
| 0 | 33.3 | 27.5 | 1.6 | 0.2 | 0.8 | 26 |
| 134 | 33.3 | 35.0 | 4.8 | 0.9 | 1.0 | 30 |
| 271 | 50 | 50.5 | 7.3 | 1.2 | 1.3 | 43 |
| 800 | 66.7 | 72.2 | 15.8 | 2.5 | 1.5 | 56 |

This is the real machine's signature: frame 72 ms (real: 75), sim 2.5 x 1.5 (real 3.8 x 1.4), js small, a huge unexplained gap that grows with units. The 0-unit gap (26 ms) is a SwiftShader fixed cost (full-screen terrain blit, vignette, minimap) that a real GPU will not have.

**M3. Frame cost forced to completion (t3).** Render one frame, then `getImageData(0,0,1,1)` to force the raster to finish. JS vs flush:

| scene | js ms | flush ms | total | ops/frame |
|---|---|---|---|---|
| 0 units | 1.0 | 12.9 | 13.9 | 8,271 |
| 200 on screen | 5.3 | 29.5 | 34.8 | 31,082 |
| 400 on screen | 8.1 | 50.0 | 58.1 | 57,447 |
| 800 spread (293 on screen) | 7.3 | 41.4 | 48.7 | 43,526 |
| 800 on screen | 101 (buffer overflow flush lands in JS) | 3 | about 105 | 106,954 |

About 130 canvas operations per drawn unit, about 0.12 ms raster per drawn unit in software.

**M4. Where the unit cost is (t5), 800 units, 293 drawn:**

| variant | total ms |
|---|---|
| full | 50.1 |
| no units at all | 16.9 |
| units drawn but `ART[*]` replaced by no-ops | 18.7 |
| `shadowBlur` writes ignored | 32.6 |

So 94% of per-unit cost is the `ART` vector draw (31.4 of 33.2 ms), and half of it is the `shadowBlur=2.4` applied across every sub-fill of the art. HP bars, shields, contact shadows, selection, etc. total about 2 ms.

**M5. `fullFx` ON vs OFF (t6).** At 800 on-screen units, fullFx on = 160 ms, off = 73 ms. At 1,600: 202 vs 126. So the shipped default is the expensive one. The "LOD" path helps but even OFF it is 73-126 ms.

**M6. Micro-benchmark: vector vs blit per unit (t4), 300 draws each:**

| art | vector us/unit (raster) | blit us/unit |
|---|---|---|
| tank | 32.7 | 3.0 |
| heavytank | 36.3 | 3.7 |
| arty | 30.7 | 3.0 |
| railgun | 40.0 | 3.0 |
| fighter | 100.7 | 2.7 |
| strikejet | 76.7 | 4.7 |
| inf | 15.0 | 6.7 |

10-35x cheaper per unit.

**M7. Sprite-cache prototype inside the real `renderGame` (t7), monkeypatching `ART[k]`** (key = art, size, INK, aim quantised to 1/12 rad, animation phase in 8 bins; shadow baked in; blit with shadow off). Full frame, forced flush:

| units on screen | vector (now) | cached | speedup |
|---|---|---|---|
| 200 | 40.4 ms | 24.1 | 1.7x |
| 400 | 64.9 | 34.7 | 1.9x |
| 800 | 110.4 | 46.6 | 2.4x |
| 1,600 | 203.5 | 67.4 | 3.0x |

Subtracting the 14 ms empty-frame floor, the unit cost falls from 96 to 33 ms (800) and 189 to 53 ms (1,600). Canvas ops per frame at 800: 107k to 24k. The cache needed 12 bakes (0.8 ms) to warm. Caching only the 45 non-animated arts (t7s): 106 to 63 ms at 800.

**M8. Particles (t8, t14).** 3,000 particles (as in the real 10:31 battle window: 2,877) cost about 60 ms raster headless (about 21 us each), versus 2-9 ms of JS. The expensive kind is the big additive `glow()` blooms inside `flash` particles: removing 142 flash particles cut the frame from 85 to 30 ms. Swapping the glow gradient fill for a pre-baked sprite blit (t11) moved only 82 to 77 ms: the cost is fill area x `lighter` blending, not gradient generation.

**M9. Empty-world floor (t12).** `drawWorld` alone 14 ms, minimap + vignette +8.5 ms. These are software-raster artifacts of full-screen fills; `buildings` (43) are negligible.

### 2.4 What SwiftShader headless cannot tell us

- It rasterizes in software, so absolute milliseconds are not the designer's. Per-op relative costs (shadow blur vs plain fill vs blit) are plausible proxies, but a real GPU makes plain fills cheap and makes per-draw state changes, `shadowBlur` layer allocation and texture switches the main costs. Expect shadowBlur-per-fill to be at least as bad on GPU.
- It cannot show a GPU-process stall, compositor backpressure, vsync pacing, or a driver quirk.
- The 14 ms empty-world floor is largely fake.

How the designer's in-game report confirms or refutes this: look at the existing `gap` column (frame minus `js`) and `perfMachine().soft` in the next report. If `gap` is about 60 ms while `js` is about 10 ms, the cost is outside script; `soft: true` would mean Chrome hardware acceleration is off and this is exactly the software model above. Decisive A/B (needs a tiny debug toggle): hold the army constant and flip a key that skips the unit draw loop (or the cache on/off); if the frame interval drops by tens of ms while `js` barely moves, the claim is confirmed.

---

## 3. The three fixed questions

### Q1. One change to make hundreds of units cheap

**Cache every unit/enemy art as a pre-rendered bitmap (shadow included) and blit it with `drawImage`.** Concretely:

- One new function, about 70-90 lines, placed right after the `ART` object (L3005): `for (const k in ART) ART[k] = cachedArt(k, ART[k])`. The wrapper acts only when the target context is the main `c` (cards/icons draw into other contexts and pass through).
- The 11 call sites of `ART[...](` (L9259, 9350, 9381, 9398, 9578, 9581, ...) stay unchanged. `renderGame`, the sim, `drawSquadUnit`, wrecks and chunks all go through the same wrapper, so squads, enemies and wreckage get it for free.
- Key = art name, `s`, current `INK` (3 variants: neutral/ally/enemy; `GLASS` is tied to it), aim bucket (about 1/12 rad), zoom bucket, and an animation phase for the 34 animated arts. Value = offscreen canvas with the shadow baked in. Wrapper draws with `shadowBlur = 0` so it never pays the blur twice.
- Per-unit shadow at L9259 becomes irrelevant (baked). Visually the same, since the baked shadow is identical.
- Measured gain (M7): 800 units 110 to 47 ms, 1,600 units 203 to 67 ms; ops 107k to 24k per frame. Static-only version: 106 to 63 ms.
- Estimated real machine: the 75 ms idle window should fall to roughly 30-45 ms (see proposal 1 for the reasoning and the honest range).

Why not something grander (WebGL instancing, SoA): the same ops-count reduction is available in about 90 lines with zero changes to game logic. A WebGL renderer would be better still but is a 3,000+ line rewrite of `renderGame` and every art function.

### Q2. Would multithreading help?

No, not for this problem, and not at this cost.

- Offloadable JS is small: sim 5.3 ms + render JS 3.9 ms = 9.2 of 75 ms (12%) on the real machine. Even an infinitely fast worker sim+record would give 75 to about 66 ms: 13%.
- The 66 ms remainder is raster/commit of canvas ops. An OffscreenCanvas worker does the same number of ops; if raster is on the GPU process it does not change; if it is on a main-thread flush, it moves it off the UI thread but the frame still takes 66 ms to produce. fps does not go up.
- Cost of doing it: `G` is one giant mutable object graph with closures over 12k lines; every renderer read (`G.units`, `G.buildings`, `cam`, `animClock`, cities, terrain edits) would need a per-frame snapshot transfer. That is a structural rewrite of the render data path.
- `SharedArrayBuffer` needs cross-origin isolation (COOP/COEP headers). GitHub Pages cannot set headers. A service-worker shim (coi-serviceworker) can add them on second load, but COEP will break the cross-origin YouTube music iframe unless made `credentialless`, and the first load needs a reload.
- Verdict: worth revisiting only after the op count is fixed and only if a later report shows `js` dominating.

### Q3. Is the spatial grid doing its job?

Yes. Evidence: `SpatialGrid` (L4271) is a uniform 256 px cell grid, bucketed once per phase, with `slack` padding for in-phase drift (`GRID_F.slack = 160` while units move, L6461). `acquireEnemy`, projectile hits, splash, flak and CIWS all use `GRID_E/P/F/A`. CPU profile of 800 units: grid build + extend = 2.4% of a 2.5 ms step; the whole sim is 3 us per unit. Not the bottleneck.

Where it is bypassed (all small today, could grow with enemy counts):

- L8019 `_huntT` hunt scan: for each air unit every 0.5 s, loops all `G.enemies` (O(air x enemies)).
- L8095 retreat check: for each retreating ground unit, loops all `G.units` for nearby allies.
- L8145 rail-hub hop: for each unit near a hub, loops hubs x enemies.
- L7780 carrier deck count: for each carrier each tick, loops all `G.units`.
- L7866: per-unit nested loop over `G.units` (fed by an air-defence branch).
- L8326: per building, loops all `G.enemies` instead of using `GRID_E`.
- L7732, L8503: single O(enemies) scans, fine.

None of these showed up in the profile. Fix last, if ever.

---

## 4. Top 3 structural proposals

### Proposal 1 — Sprite cache for `ART` (the Q1 answer)

- **Mechanism:** `cachedArt` wrapper around every `ART[k]`; bake once per (art, size, ink, aim bucket, zoom bucket, anim phase); `drawImage` thereafter; shadow baked into the bitmap and live shadow suppressed for the blit. Optional phase 2: pack bitmaps into one atlas canvas so Skia can batch same-texture blits.
- **Evidence:** M3, M4, M6, M7 above: 94% of per-unit raster cost is `ART`; half of that is shadowBlur; blit is 10-35x cheaper per unit; prototype full-frame 2.4-3x.
- **Measured gain (headless):** 110 to 47 ms at 800 on-screen units; 203 to 67 ms at 1,600; ops 107k to 24k per frame. Static-arts-only: 106 to 63 ms.
- **Estimated real-machine gain:** the real 75 ms idle frame = about 9 ms JS + about 66 ms raster. If real raster scales like software, unit cost (about 65% of the raster above the floor) falls 3x: about 75 to 35-40 ms. If the real machine's cost is dominated by something unit-independent (a vignette/gradient fill-rate, driver overhead), the gain is smaller; floor of the claim is about 1.3x, central estimate about 2x. JS also drops (fewer calls; for 800 units js fell from 107 ms to 5 ms in the overflow case).
- **Risk:** visual only; the simulation and balance are untouched. Specific visible risks: (a) aim quantisation (1/12 rad = about 0.3 px at barrel tip for s=4; use finer bins for large ships); (b) the 34 animated arts (spinning rotors/radars, pulsing cores) need phase bins; a master loop that is not a multiple of each pulse period can show a once-per-loop twitch, so either bake per-art phase counts from the known `animClock*k` speeds or leave animated arts as vector in v1; (c) zoom changes (`cam.z`) need bucketing or every zoom frame re-bakes; (d) memory: cap with LRU (about 100-3,000 small canvases; each 10-60 KB); (e) many separate small canvases are many textures on GPU: atlas them in phase 2.
- **Blast radius:** one new function + one init line after the `ART` const (L3005). Reads `INK`, `GLASS`, `animClock`, `EMP_PULSE`, `cam.z` only (verified: ART bodies reference no other globals). Call sites unchanged. Card/icon renderers (L9815, 9875, 9984, 10351) pass through (`x !== c`).
- **How to verify:** (1) pixel-diff screenshot test: render the same paused scene with cache on/off, assert max-diff in a tolerance (and eyeball animated arts); (2) headless: `renderOnce()` with forced flush as in t7, assert op count and total ms; (3) `pageerror` count zero over a 6,000-step run; (4) real machine: A/B in the perf report, watch the `gap` column.
- **Tenet check:** same army, same count, same behaviour; every unit is still drawn with the same art. Cheaper per unit, no cap.

### Proposal 2 — Make the existing LOD/adaptive-FX machinery act on the right cost (cheap, GATED visual)

- **Mechanism:** (a) wire the unit `shadowBlur` at L9259 to `FXB`/`_fxNoBlur` so the adaptive FX controller can actually remove it when frames run long; (b) stop `fullFx` from disabling the count-based `_heavyLOD/_extremeLOD` tiers wholesale: use them as the fallback when `fxTick` has already hit `FX_Q = .25` and the frame is still over budget; (c) cap the number of additive large `glow()` blooms per frame by *area*, not count (M8: 142 flashes cost 55 ms headless).
- **Evidence:** M4 (shadowBlur is half the unit cost), M5 (fullFx on 160 vs off 73 ms), M8 (flash blooms), `fxTick` reads only `PERF.frame` and its knobs never touch unit art.
- **Measured gain:** shadowBlur off: 50 to 32.6 ms (293 drawn units); fullFx off at 800 units: 160 to 73 ms. Per-flash glow removal 85 to 30 ms in a 3,000-particle scene.
- **Estimated real:** 20-40% on battle frames; superseded for units by Proposal 1 (the shadow is baked), so (a)/(b) are needed only as a safety net; (c) still valuable for battles.
- **Risk:** visual degradation under load, which the adaptive controller already promises. Must not reduce unit count or simulation. Low.
- **Blast radius:** `renderGame` header (L9148-9160), L9259, `fxDerive/fxTick` (L913-925), `glow()` (L9548).
- **How to verify:** t6/t8 style forced-flush benches; confirm FX_Q reacts to a synthetic slow frame via `window.__fakeFrameMs`.
- **Tenet check:** only visual effects scale; the army is untouched. Under the designer's own rule ("fix perf, never the fantasy") it is acceptable only as an *adaptive* fallback, never a default cut; Proposal 1 should land first so it rarely triggers.

### Proposal 3 — Particles/projectiles as flat batched draws (second-order, battle frames)

- **Mechanism:** (a) skip `save/translate/rotate/restore` for particles smaller than about 2 px (`deb`, `clod`, `fleck`: a 1-2 px rotated square is indistinguishable) and use a single `fillRect`; (b) sort or bucket particles by kind/colour so state changes (`fillStyle`, `globalAlpha`, `globalCompositeOperation`) happen once per bucket; (c) pre-bake the glow bloom as a sprite (done in t11: no gain by itself; combine with the area cap in Proposal 2c).
- **Evidence:** M8: 3,000 particles = about 60 ms headless (about 21 us each, about 10-15 ops each); the real battle window had 2,877 particles and 671 projectiles at 68.7 ms.
- **Measured gain:** (a)/(b) not prototyped end to end; the t14 removals bound the possible saving (deb 1,278 particles at roughly 1 ms, flash 142 at roughly 55 ms, so big blooms are the lever, not small debris). Honest: unknown, probably 10-25% of a battle frame.
- **Risk:** low visual risk for (a); (b) changes draw order (alpha layering order of overlapping smoke vs embers) and must keep z-order for explosions.
- **Blast radius:** `renderPart` L9555-9610, the particle loop in `renderGame`, `updParts` (L9083) if the structure becomes arrays-of-kind.
- **How to verify:** forced-flush bench with `boom()` spam (t8/t14); visual diff of an explosion frame.
- **Tenet check:** same effects, fewer ops; no gameplay effect.

(Not proposed, deliberately: sim tiering, SoA, workers, ECS. Measured sim is 3 us/unit and 7% of the frame.)

---

## 5. Adversarial questions

1. L909 `let fullFx = true` with the comment "the sim is what's ever actually slow, render is cheap". What measurement showed that? Suspect: the `render` PERF timer (JS only), which cannot see raster.
2. L9259: why is a `shadowBlur` set on the *whole* ART draw, so every sub-path of every unit pays for its own blurred shadow, rather than shadowing the finished body once? Suspect: it was the cheapest way to add depth, and was fine at 50 units.
3. Why is there a terrain cache (`bakeTerrain`), a minimap cache and a glow gradient cache, but no unit cache? Suspect: terrain showed up in the JS timer (`terrain 0.98 ms`), units' raster cost did not.
4. L10073 `frame()` runs the sim up to 3 steps per frame when behind. With raster-bound frames, does extra sim per slow frame make the next frame slower (spiral)? Suspect: mild, because sim is only 2.5 ms/step, but it is still positive feedback.
5. L914 `fxTick`: why does the adaptive-FX controller steer a knob set (particle cap, glow cap, trail length) that excludes the single biggest per-unit knob? Suspect: it was designed around explosions, before 800-unit armies.
6. L9155 `_heavyLOD = (!fullFx && n > 200) || cz < .85`: why does turning on "Full Effects" also turn off the *count-based* LOD that existed for exactly the 1,000-unit case? Suspect: "favor keeping visuals" was bundled with "skip LOD" by one flag.
7. L4113 `BATTLEGROUPS (PERF STOPGAP)`: followers skip AI "because 1,900 bodies crush the simulation". Measured sim is 3 us/body. Is the stopgap still worth the behaviour change (formation easing, shared target)? Suspect: no longer needed for perf; kept because the designer asked for it.
8. L6032 `perfCapStep`: why does the instrumentation re-render the entire world three extra times (and `drawImage(cv)` from the live canvas) inside the live frame loop? Suspect: it is "bounded" (3 frames per ranked worst window) but it perturbs exactly the frames being measured.
9. L10091 `perfAdd('render')` is described as render cost in the report. Should it be renamed "draw-call issue time" and paired with a true cost (frame interval minus JS)? Suspect: yes; the `gap` column is the honest number.
10. L7719 `updUnits` stores `u.px/u.py/u.pAim` for every unit and `interpEnter` mutates `u.x/u.y/u.aim` and restores them. Why mutate the entity instead of passing interpolated coordinates to draw? Suspect: it let all draw code stay unchanged; cost is small but any exception between enter and exit would corrupt sim state (there is a try/finally, good).
11. L9148 `renderGame` counts squads on every frame (`liveSquadN` loop over all units) and runs the culling check per entity. Why not cull via the grid? Suspect: 800 entities is nothing; it only matters if sprites get cheap enough that this becomes visible.
12. L8019 `_huntT` loops all enemies per air unit every 0.5 s: why not `GRID_E.nearest`? Suspect: it needs a role filter (`aF`) and `THREAT_W_GRID` weighting that `nearest` did not originally support.
13. `G.parts.push({ ... })` literals everywhere (L4999-5046, `emit*`, `boom`): why no pooling when a battle holds 2,900 live particles? Suspect: GC is only 2.8% of the sim profile so it never looked urgent.
14. L902 `getContext('2d')` with default attributes: has `alpha:false` (opaque canvas), `desynchronized`, or `willReadFrequently:false` been tried? Suspect: no; worth a one-line A/B on the real machine.
15. L10030 `drawVignette` + `drawMinimap` add 8.5 ms in software on top of an empty world: are they still full-screen fills every frame? Suspect: the vignette could be one pre-baked overlay image per resolution.
16. `hudAcc` throttle at 30 Hz (L10116): is `syncHUD` (0.75 ms real) a DOM cost that shows up in `loafStyle`? Suspect: not significant, but the new LoAF instrumentation can answer it.
17. `SQUAD_LOD_N = 45` (L3897) collapses squads into cluster glyphs past 45 live squads. Does that LOD fire in a typical 800-unit army? Suspect: it fires, which means the designer's infantry already look different at scale than at 40 units, while the heavy armour never gets an LOD. That is inconsistent.
18. L3883 `SQUAD_LOADOUT` has only `inf`, `cdrone`, `fighter`: why no squad rendering for tanks/arty/SAM at 100+ count? Suspect: aggregation was designed for sim cost, not draw cost, so tanks never needed it.
19. L9259 branches on `u.entr`, `u.fort`, `u.shield`, `u.onRail`, glow for air era >0, contact shadows: how many per-unit one-off ops remain after the sprite bake? Suspect: about 20% of the remaining ops; fine.
20. `window.__fakeFrameMs`, `e.deployed`, `perfOverlay`, `perfLine`: why does test scaffolding live in the shipped file? Suspect: single-file constraint.

---

## 6. Cleanup list

| Item | Where | Mark |
|---|---|---|
| Comment claiming "render is cheap by comparison" / "bought ~nothing" | L909 `fullFx` and the matching `fxTick` note L921 | SAFE (comment only; correct it after the A/B) |
| Per-unit `shadowBlur` live draw (replaced by baked shadow) | L9259 | GATED (visual diff) |
| `_heavyLOD`/`_extremeLOD` dead under default `fullFx` | L9155-9170 | GATED (needs a visual + perf test) |
| `fxTick` only reacts to `PERF.frame`; not connected to unit cost | L914-925 | GATED |
| BATTLEGROUPS "PERF STOPGAP" premise stale (sim 3 us/body) | L4113-4200, L7798, L7810, L8143 | GATED (changes unit behaviour: formation ease, shared target) |
| Double bookkeeping: `PERF_R` rolling window + `PW` per-2 s window both updated in `perfAdd` | L6178 | SAFE |
| `perfCapStep` re-renders whole scenes inside the live frame | L6032-6048 | SAFE (instrumentation only) but perturbs measurements |
| `perfOverlay`/`perfLine` dev overlay | L10047-10054 | SAFE |
| `window.__fakeFrameMs` test hook in `fxTick` | L915 | SAFE |
| `e.deployed` legacy check (comment says nothing sets it) | L6469 | SAFE |
| `interpEnter/interpExit` mutate entities to draw them | L10055-10063 | GATED (leave unless proven slow) |
| `_glowCache` caches gradient objects but still pays a fill per glow | L9545-9553 | SAFE (replace with sprite only if it measures better; t11 says no) |
| Particles created as fresh object literals | L4999-5046 and others | SAFE (pooling) but low value |
| Several overlapping LOD names (`_heavyLOD`, `_extremeLOD`, `_blipLOD`, `_dt2`, `_dt3`, `squadLOD`, `SQUAD_LOD_N`) and v-tags (v4.5 P, v4.8 E, v3.99.x): layers of patches on the same problem | L9148-9170, L3897 | GATED |

---

## 7. What I would NOT touch

- **The sim loop, `updUnits`, `acquireEnemy`, the grids, `updProj`.** 3 us per unit, grid 2.4% of that. Any change risks balance for under 3% of frame time.
- **Fixed 30 Hz step + interpolation.** It is correct and the right design.
- **Terrain bake and minimap cache.** Already doing their job.
- **SoA/typed-array conversion or an ECS rewrite.** About 12k lines of long-line code and the sim is not the bottleneck.
- **Worker/OffscreenCanvas migration.** See Q2.
- **Army caps, diminishing returns, or hiding units.** Forbidden by the design tenet and unnecessary: the same army is drawable at a third of the cost.
- **Squad body counts and battlegroup membership rules.** Gameplay; leave to the designer.

---

## 8. Where I disagree with the ROADMAP

Read after the findings above were written. Agreements first: the coordinator's hypothesis (1) RASTER with sprite cache as the fix and WebGL as the endgame, the "grid is why the sim is cheap" note, and the "multithreading only moves ~10 of 75 ms; no COOP/COEP on Pages" note all match my measurements. Disagreements:

1. **The Aug-23 "render only ~16 ms (NOT the bottleneck)" and the "PERF LOOP CLOSED at 3.5x" ruling (ROADMAP "PERF LOOP" section, "BASELINE" line).** That render number is JS issue time. My forced-flush measurements show the same renderGame costs 40-100 ms once the raster is included, versus 5-10 ms of JS. The whole 301 to 86 ms/tick campaign optimised the part that is about 7% of a real frame at 800 units (sim is 2.5-3 ms/step at 800). It was real work on a real cost, but at the wrong scale for the 60 fps goal. The closing rule "benchmark at 5,000 units, sim ms/tick" would never have noticed the raster.
2. **The v4.8.1 suspect order** ("browser style/layout after HUD writes" first, "hardware acceleration OFF" second, "recorder", then "GPU raster of blurred canvas"). I would put style/layout last: `syncHUD` is 0.75 ms JS, the HUD is already write-on-change, and headless reproduces the full gap with the HUD untouched. The remaining three all resolve to the same fix: fewer canvas ops per unit. Whether Chrome's canvas is CPU-rastered (hardware acceleration off) or GPU-rastered, 107,000 ops/frame under per-draw `shadowBlur` is what costs. So the sprite cache does not have to wait for "report 11" to choose a suspect; report 11 should be used to *measure the gain*, not to pick the lane.
3. **"Next perf pass starts from report 11, not a guess."** I agree with the principle, but a headless reproduction of the exact report-10 signature (frame 72 ms, js 16 ms, sim 2.5 x 1.5, gap growing with units) plus a forced-flush bench is evidence, not a guess. The sprite-cache prototype is a monkeypatch of about 25 lines; building it costs less than waiting for another match.
4. **The v4.8.1 claim that effects quality "engages now" (FX_Q .85 at 5 s, .55 at 9 s).** True, but the knobs it turns (particle cap, glow cap, trails, a handful of `FXB()` call sites: only 6 in the whole file, versus 178 `shadowBlur` sites) do not include the per-unit `shadowBlur = 2.4` at L9259 or anything in `ART`. In the idle windows of report 10 (49 and 159 particles) there is almost nothing for FX_Q to remove, so FX_Q reaching .25 would change nothing. That absence of effect will look like "no improvement", which the code comment already calls "itself the diagnosis".
5. **Coordinator hypothesis (2) SWEEPS (53 `for (const u of G.units)` passes).** Not supported by the numbers: the entire step is 2.5 ms for 800 units, and `updateGameInner` self time (which holds the loose sweeps) is 0.67 ms/step, about 1% of a 75 ms frame. Bucketing by role would also carry behaviour-order risk (iteration order feeds ties, merges, death sweeps). Skip it until the raster is fixed and the sim is the largest remaining slice.
6. **The older "Perf: it's RENDERING ... web-workers can't touch Canvas2D" note (ROADMAP near L1150/L1048).** The conclusion is right, the reason is outdated. `OffscreenCanvas` in a worker can touch Canvas2D. The actual reasons workers do not help are (a) the cost is op count/raster, not JS, and (b) the object graph is not transferable cheaply. It is worth correcting so a future model does not "disprove" it by testing OffscreenCanvas and finding it works, then concluding it is the fix.
7. **The "Lever B aggregate battlegroups would cost the granular-soldier feel" reasoning.** Correct for sim, but it also never addressed the draw side. At 800 units the sim is 3 us/unit; battlegroup aggregation would save under 3% of the frame. The *draw* side is where aggregation-like savings exist (sprite cache), and it keeps every soldier granular. Worth restating so Lever B stays declined.
8. **`fullFx` default ON "favor keeping visuals".** I agree with the intent (tenet: do not cut the fantasy), but the premise stated in the code comment is the same JS-timer misreading as item 1. With a baked sprite (shadow included) the default look is preserved and the cost is paid once, which satisfies the designer's visuals-first rule better than either LOD tier.

# R2 — The drawing and the browser

Reviewer lens: `frame()` -> `renderGame()` -> per-unit art -> what Chrome does with it.
All scripts live in `/tmp/claude-0/-home-user-total-mobilization/44c0f627-2c4e-5264-ab7c-20b27493f157/scratchpad/r2/` (`count2.mjs`, `bench2.mjs`, `bench5.mjs`, `bench5b.mjs`, `slow.mjs`, `blurhist.mjs`, `prof.mjs`). Nothing in the repo was edited.

## 1. Executive summary

1. The game's "render ms" (3.9 ms on the designer's PC) only measures how long JS takes to *record* canvas calls. A 800-unit frame is ~84,000 canvas calls, ~18,000 of them draw ops, and ~12,000 of those are drawn with a blurred drop shadow. Recording that is cheap; **rasterizing it is not**, and that cost never shows up in any JS timer. Drawing IS where the missing ~60 ms of the 75 ms frame goes, by every piece of evidence I can produce.
2. Headless (SwiftShader, a CPU rasterizer) with the game's own `ART[]` functions: JS 15 ms, but **time-to-pixels 94 ms for 1,111 bodies (800 units)**. Removing only `shadowBlur` drops that to 55 ms. The cost is linear in bodies (277 bodies 22.6 ms, 555 -> 51.8, 1111 -> 87.5).
3. Baking each unit type once to an atlas and `drawImage`-ing it: **11 ms** to pixels for the same 1,111 bodies, JS 1.2 ms. About 8x less in SwiftShader, and the op count falls from ~18,000 draw ops to ~1,100.
4. The designer's three worst windows give 84 / 95 / 87 microseconds of *frame time per unit* (53.4/633, 75.4/792, 68.7/789) regardless of whether 0 or 671 projectiles and 49 or 2,877 particles were alive. That flat per-unit slope is what unit art cost looks like and what particle/projectile cost does not look like. My headless slope (118 us/unit, 95 us/body) is the same order of magnitude, which is suggestive, not proof.
5. Sim, DOM/HUD and the spatial grid are not the problem in this window: `syncHUD` 0.4 ms, layout 0 ms, sim section `units` 2.9 ms. Chrome-side facts I cannot measure here (GPU vs software canvas, real GPU blur cost) are listed with a one-click probe in section 3/4.

## 2. My lens: findings

### 2.1 What happens to put one unit on screen (`frame` 10064, `renderGame` 9148, unit loop 9253-9259)

Per frame, in order: `frame()` clears + fills the whole canvas, `drawWorld(1)` (full-height sky gradient of `worldH()` ~1565 px, nebula/stars/aurora/clouds/ridges/skyline, terrain blitted from the `bakeTerrain` offscreen cache, water, roads), `interpEnter()` (walks every unit/enemy/projectile), `renderGame()`, `interpExit()` (walks them again), `drawVignette()` (full-screen radial-gradient fillRect), `drawMinimap()` (offscreen cached at `_fxMMms`), `syncHUD()` at 30 Hz.

For one non-squad ground unit the unit loop (9253-9259) does: cull, optional contact-shadow ellipse (`save`, `globalAlpha`, `fillStyle`, `beginPath`, `ellipse`, `fill`, `restore`), then `c.save(); c.translate(px,gdy);` and — because **`fullFx` defaults to `true` (line 909), `_heavyLOD` is never on** (9157) — `c.shadowColor='rgba(0,0,0,.55)'; c.shadowBlur=2.4; c.shadowOffsetY=1.4;`, then `c.scale(facing,1); ART[d.art](c, d.s, aim)`. The `ART` function is a straight sequence of `fillStyle=`, `beginPath/moveTo/lineTo/fill`, `fillRect`, `arc` (e.g. `ART.tank` 2033-2046 = ~14 fills, 2 `save/restore`, a `rotate`). **Canvas 2D applies the shadow to every draw op separately**, so one tank = ~21 individually blurred shadow draws, not one.

Then bars (`bar()` = 2 `fillRect`, 2 `fillStyle`), selection ring, shield ring (`arc` + `stroke`, 2 `globalAlpha`), battlegroup badge (`fillText`).

### 2.2 Measured call counts (`count2.mjs`, 800-unit scene, 1544x824, camera over the army, particles/projectiles cleared)

Whole frame, 666-unit scene (heavy/spg not spawnable by plain `spawnUnit`): 65,915 canvas method calls, 18,218 draw ops (`fill/stroke/fillRect/...`), 11,896 of them with `shadowBlur>0`, 3,726 `save` + 3,726 `restore`, 8,810 `beginPath`, 18,012 `fillStyle` sets, 9,043 `globalAlpha` sets, 3,122 `shadowBlur` sets, 2,354 `globalCompositeOperation` sets, 2 gradients, 2 `fillText`. 800-unit scene: 83,796 calls total.

Shadow radius histogram at draw time (`blurhist.mjs`): 12,960 ops at <=3 px, **1,200 at 3-8 px, 160 at >8 px** (afterburner/engine glows inside `ART.fighter`/`aburn` 1978-1985 use `shadowBlur=len*1.1`).

Per unit type (ops = draw ops, sh = drawn with a blur shadow, calls = all canvas method calls):

| art | draw ops/body | shadowed ops/body | all calls/unit |
|---|---|---|---|
| inf (squad of 3, flat, no shadow by design) | 41.5 | 0 | 82 |
| tank | 24.2 | 21.0 | 85 |
| aa | 23.2 | 20.0 | 67 |
| railgun | 26.2 | 23.0 | 70 |
| sam | 26.2 | 23.0 | 66 |
| fighter (squad with wingmen) | 60.2 | 27.0 | **376** |
| strikejet | 35.6 | 31.5 | 107 |
| heli | 26.2 | 22.0 | 91 |
| ogunboat | 5.2 | 5.0 | 26 |
| spacefrigate | 6.2 | 6.0 | 27 |

In the 800-unit scene the game issues **1,111 `ART[]` body draws** (squads multiply bodies: the "unit count" understates draw count; a swarm squad can have up to 48 bodies per `G.units` entry, see `swarmBodies` 4003).

`ART` census (`artstat.mjs`): 79 art functions, 51 referenced by `UNIT_DEFS` (57 defs). 38 are time-animated (`animClock`/`pulseA`/`jetflame`/`aburn`/`Math.random`), 18 take a rotating turret/aim angle, 39 contain their own `shadowBlur`/`skyGlow`, 8 use `globalCompositeOperation='lighter'`, 0 create gradients (good).

### 2.3 Measured cost, headless (`bench2.mjs`, `bench5.mjs`, `bench5b.mjs`)

Median over 25-30 frames, units only on screen, particles/projectiles cleared. "to pixels" = after a 1x1 `getImageData` forces the raster to complete.

| variant | JS ms | to pixels ms |
|---|---|---|
| world only (sky, terrain, vignette; no units) | 0.8 | 15.3 |
| game full frame, 800 units (1,111 bodies) | 98.8* | 104.8 |
| game's `ART[]` body draws only (same transforms, same shadow state) | 92.6* | 94.4 |
| same, `shadowBlur` forced to 0 (bench2, 666 units) | 14.5 | **55.4** (vs 94.5 baseline) |
| same, `fullFx=false` (`_heavyLOD`, bench2) | 15.9 | 62.4 |
| same, `globalCompositeOperation` forced `source-over` | 15.9 | 95.1 (no change) |

\* The JS number swings between ~15 ms and ~100 ms run to run. `slow.mjs` shows why: of 83,796 canvas calls exactly **one** (a `fill()` inside `aburn`) took 87.6 ms. Headless Chrome with software canvas rasterizes inline when its recorded-op buffer fills and charges the whole flush to whichever call trips it. That is an artefact of this environment (and a nice illustration that "JS time issuing canvas calls" is not "time to draw"). Use the to-pixels column, which is stable (p90 within ~25% of median).

Scaling (`bench5b.mjs`), bodies vs time-to-pixels: 277 -> 22.6 ms, 555 -> 51.8, 1,111 -> 87.5. Linear, ~80 us/body. Not O(n^2).

CPU profile of the headless frame (`prof.mjs`): 83% of samples are the native `CanvasRenderingContext2D.fill` itself (the flush); JS functions (`fighter`, `renderGame`, `bar`, `aburn`, `inf`...) are 1% each.

### 2.4 Where a real Chrome on Windows spends the ~60 ms (mechanism; partly inference)

Evidence from the designer's report: per frame JS = sim 5.3 ms + render 3.9 ms + HUD ~0.4 ms = about 10 ms, yet the frame is 53-75 ms. My own count says 3.9 ms of JS for ~84,000 canvas calls is exactly the cost of *recording* ops (~50 ns each), so the report's `render` number is consistent with a recording-only measurement. The rest is outside the game's timers:

1. **Raster of the recorded display list.** Chrome 153 canvas 2D records a paint-op list and rasterizes it in the GPU process (OOP-R / Skia Ganesh or Graphite) or, if GPU raster is unavailable, in software. ~18,000 draw ops per frame, each a separate path or rect with its own paint.
2. **Blurred shadows are the multiplier.** Skia implements `shadowBlur` as a second draw per op through a blur mask/image-filter: render the shape to a scratch surface, blur (two passes), composite, then draw the shape itself. 11,896 shadowed ops = ~12,000 extra scratch-surface allocations, blur passes and composites per frame, each tiny, each a render-target switch on a GPU. GPUs are bad at thousands of tiny dependent passes. In SwiftShader the blur alone costs 39 ms of 94 (42%); on a real GPU the *ratio* may differ, but the *count* of passes is what matters for a GPU.
3. **Path tessellation of ~9,000 tiny convex paths per frame** (`beginPath` 8,810). Skia has no instancing for canvas2D paths; every `fill()` is its own geometry upload/draw.
4. **State churn.** 3,700 `save`/`restore` pairs, 18,000 fillStyle string parses, 9,000 `globalAlpha` changes, 2,354 `lighter` composite switches. Each composite-mode change breaks Skia's draw batching.
5. **Backpressure.** `requestAnimationFrame` only advances when the previous frame's GPU work completes. If the GPU process needs 60 ms per frame, rAF intervals are 60+ ms while the main thread idles. This is the exact signature of the report: small JS, long frame, `PERF_LOAF` would show little script time.
6. **Pixel ratio.** `resize()` (904) sets `cv.width=innerWidth`, i.e. backing store = CSS pixels (1544x824), NOT devicePixelRatio-scaled. At dpr 0.9 the compositor downsamples to 1390x742 device px per frame: a trivial GPU blit, not a cause. Overdraw of full-screen layers (sky ~1.9x viewport tall gradient, terrain blit, tint, vignette, `.grain` CSS layer) is real but fixed cost; headless world-only to-pixels is 15 ms, which on a GPU is ~1-2 ms.

What I could NOT establish: whether the designer's Chrome is GPU-rasterizing or has fallen back to software canvas. The coincidence that the real per-unit slope (84-95 us/unit) matches my CPU-rasterizer slope (95-118 us/unit) is a red flag worth one probe: if the real machine is on software canvas, the cause is not "GPU is slow at blur", it is "the whole canvas is CPU-rasterized", and the cure (fewer ops) is the same but the expected gain is larger.

What is *not* the cause (measured): `syncHUD` 0.4 ms and forced layout 0 ms with 1,948 DOM nodes; no `getImageData`/`toDataURL` in the frame path (grep: only after-action `captureSnap` via `drawImage` of the main canvas to an offscreen, plus `perfCapStep` JPEG snapshots, both queued/rare); no CSS `backdrop-filter`/`filter` over the canvas during play (only `.modal-bg`, line 42-43, which pauses); `drawMinimap` is cached offscreen; terrain is already baked (`bakeTerrain` 1600, `TCACHE`). The world layer is *already* cached-sprite-style. Units are the one big uncached vector layer.

### 2.5 Cached-sprite and WebGL microbenchmark (800 units, 1,111 bodies, same positions/transforms as the game)

Standalone harness: `bench5.mjs` (runs inside the real game page so it can call the game's own `ART[]`; the WebGL part uses a second `webgl2` canvas in the same page). SwiftShader (software rasterizer, ANGLE over Vulkan SwiftShader Subzero), viewport 1544x824, 1 sprite per body, atlas 1024x48 px for 10 sprite types (bake 0.6 ms), average sprite 1,943 px, 2.16 Mpx of blended quads per frame.

| path | JS ms (med) | to pixels ms (med) | p90 to pixels |
|---|---|---|---|
| 1. game as-is (`ART[]` bodies only) | 92.6* / 15 (when no flush lands) | **94.4** | 130.8 |
| 2. atlas `drawImage` | **1.2** | **11.4** | 13.1 |
| 3. WebGL instanced quads, GL only (`readPixels` 1x1) | 0.0 | 41.9 | 49.1 |
| 3b. WebGL then `drawImage(glCanvas)` into the 2D canvas | 47.6 | 49.3 | 54.0 |
| world only (for reference, always needed) | 0.8 | 15.3 | 15.9 |
| world + sprites | 2.1 | 29.1 | 32.8 |

**How to read this honestly.** SwiftShader is a CPU rasterizer. It makes anything "blur a lot of tiny passes" or "blend many pixels" slow in absolute terms, and it affects 2D and WebGL differently: Skia's CPU blitter for `drawImage` is highly optimized, whereas SwiftShader emulates a GPU's programmable pipeline on CPU (slow per-fragment). That is why WebGL (42 ms) looks *worse* than 2D `drawImage` (11 ms) here, which is the **opposite** of what a real GPU does (a real GPU draws 1,111 textured quads in a single instanced call in well under 1 ms, but then you still pay the readback/composite when mixing with a 2D canvas, so you would put the whole world on WebGL or use WebGL as the only canvas). I would not use my WebGL number for anything except "it works and takes one draw call". The 2D-atlas number is the more transferable one because the count reduction (18,000 draw ops -> 1,111 `drawImage` from a single texture, which Skia batches into one draw) is hardware-independent.

Expected real-machine effect of option 2: removes ~12,000 blur passes, ~8,800 path tessellations, ~3,700 save/restore and ~18,000 style parses per frame; leaves ~1,100 textured-quad draws from one atlas texture, which the GPU batches. I would expect time-to-pixels for the unit layer to fall from tens of ms to low single digit ms. I cannot prove the 60 ms -> ~5 ms on the designer's PC from here; the in-game probe in 4.1 proves it in one run.

### 2.6 What a sprite cache must handle to look identical

| aspect | how the art does it today | cache strategy | cost |
|---|---|---|---|
| Team colours | global `INK`, `GLASS` reassigned for ally (`ALLY_INK`) and enemy (`ENEMY_INK`) passes; `HI()/LO()/TRIM()` read `INK` | key = (art, size, palette[player/ally/enemy], facing). Palette is a 3-value enum, not a free colour | x3 sprites |
| Facing | `c.scale(facing,1)` | bake both, or flip at draw time (`drawImage` with negative scale is nearly free in 2D, one extra matrix in GL) | x1-x2 |
| Turret/gun aim | 18 of 79 art fns `rotate(a)` a barrel; `a` is continuous | quantize aim to 16-32 steps per (art,size,palette) = 16-32 sprites per aiming type; or split ART into hull (static sprite) + barrel (own tiny sprite rotated with `drawImage` + `rotate`) | most work, 18 fns |
| Animation | 38 fns read `animClock` (spinning radar `ART.aa`, pulsing `pulseA` glows, `jetflame` flicker, afterburner, rotors) | static body baked once; the animated sub-part drawn live on top as a *few* ops (rotor line, flame triangle), or 4-8 frame flipbook | per-art edit |
| Damage / hit flash | draw path has no per-unit hit-flash tint; damage is HP bar only (`bar()` when `hp<maxHp`) | no change: bars stay vector (2 `fillRect` each), or a tiny bar sprite | none |
| Era upgrades | `effDef(key)` swaps `d.art`/stats via `UPGRADES` (plasma re-arms, fusion Hornet, microrockets) | key on `d.art` string + size, not on unit key; re-bake lazily on first use; invalidate when `soc`/era changes | lazy bake |
| Selection ring, shield ring, range ring, bars, badges | vector, per-unit, state-driven | stay vector (they are 2-4 ops, and most units are not selected/shielded) | none |
| Zoom levels | world is drawn in unscaled pixels and the frame applies `c.scale(cz)` | bake at ceil(cz) resolution or 2-3 mip levels; at `cz<.85` sprites are tiny anyway (and `_blipLOD` already swaps in rects at `cz<.5`) | 2-3 bake sizes |
| Shadow | `shadowBlur=2.4` per op | bake the shadow INTO the sprite once (the blur cost is paid once per type, not 12,000 times per frame) | negative cost |
| Contact shadow ellipse, entrenchment berm | per-unit vector, cheap | keep, or bake one ellipse sprite | none |
| Sub-pixel position | positions are floats | `drawImage` at float coordinates gets bilinear filtering; round to 0.5 px or accept; motion looks the same | none |
| Atlas size | sizes `s` vary (inf 9 down to 3, bodyS) | key by `size.toFixed(1)`; ~50 unit types x 3 palettes x 2 facings x (1 or 16 aim steps) = a few hundred to ~5,000 small sprites, <4 MB | memory |

Net: the static, biggest-by-count bodies (tanks, SAMs, railguns, hulls, space frigates) need only (art,size,palette,facing). Aim quantization and a "live overlay" for animated parts are the real work, concentrated in ~18+38 art functions, not 79.

## 3. The three fixed questions

### 3.1 If you could change ONE thing to make hundreds of units cheap?

Replace the per-frame vector redraw of every unit with a sprite cache plus a live overlay: bake each (art, size, palette, facing, aim-step) to one atlas and draw bodies with `drawImage`, keeping bars/rings as vector. It changes `renderGame`'s unit and enemy loops (9253-9259, `drawSquadUnit` 9329, `drawSquadCluster`) and adds one `getUnitSprite()`; about 300-500 new lines plus splitting out animated sub-parts of the ~38 animated `ART` functions; measured gain headless is 94 ms -> 11 ms to-pixels (~8x) and 18,000 -> 1,100 draw ops, and on the real machine it is expected to remove most of the ~60 ms non-JS frame time.

### 3.2 Would multithreading help?

Not as the first move. Evidence says the main thread is ~10-12 ms of a 53-75 ms frame and the rest is raster/GPU: OffscreenCanvas in a worker moves the *recording* (3.9 ms) off the main thread and does nothing about the ~84,000 calls' raster work, which is identical wherever the context lives, so the frame would still take 60+ ms and rAF in the worker would be GPU-bound just the same; the win is only that input/sim would stop sharing a thread with it. A worker simulation would be the thing that helps once raster is fixed, and it does NOT need `SharedArrayBuffer` or COOP/COEP headers: state needed by the renderer is ~800 units x (x, y, aim, facing, hp, type) = ~20-40 KB per step, which `postMessage` with transferred `Float32Array`s moves in microseconds at 30 Hz. GitHub Pages cannot set the COOP/COEP headers SAB needs; a `coi-serviceworker` workaround exists but is brittle and unnecessary here. Sim is 5.3 ms of the frame in the real report, so a sim worker caps its gain at ~5 ms.

### 3.3 Is the spatial grid doing its job? Where is it bypassed?

From my lens: the grids (`SpatialGrid` 4271; `GRID_E/P/F/A` 4291) are used where intended (enemy acquisition, projectile hit tests, splash 7452-7461, flak 8483-8491, nearest 7909); the report's sim sections (`units` 2.9 ms in a 75 ms frame, `enemies` 0.2, `proj` 0.0) say the sim is cheap and the grid is earning its keep. The render side bypasses the grid by design and that is fine: renderGame culls with `sx(u.x)` against the viewport (RSTAT.culled/drawn) in a linear pass over `G.units` that costs ~0.1 ms; the passes that DO scale linearly and are not culled are `interpEnter/Exit` (every unit/enemy/projectile twice per frame, 10055), the `liveSquadN` counting loop (9248) and 61 other `for (const x of G.units)` loops across the file. I did not profile the sim side in depth; defer to the other reviewers on grid bypass inside `updUnits`.

## 4. Top 3 structural proposals, ranked

### 4.1 Sprite-cache the unit and enemy bodies (the ONE thing)

- **Mechanism.** `getSprite(art, size, palette, facing, aimStep)` bakes a body once into a shared atlas canvas (static drop shadow baked in) and unit loops `drawImage` it. Bars, rings, badges stay vector. Animated sub-parts (aa radar, rotor, afterburner, `jetflame`, `pulseA` glows) draw as a handful of live ops over the baked body, or as a 4-8 frame flipbook. `_heavyLOD`/`_extremeLOD` become unnecessary for units.
- **Evidence.** Section 2.3 and 2.5: 18,218 draw ops (11,896 blurred) -> ~1,100 atlas draws; to-pixels 94.4 -> 11.4 ms; linear in bodies on both sides, so the gap widens with army size (1,111 bodies: 88 vs 12.6 ms; at 4,000 bodies extrapolates to ~320 vs ~45 ms).
- **Measured gain.** 8x on the unit layer, SwiftShader. JS recording falls from ~85k calls to ~3-5k.
- **Estimated real-machine gain.** Unit layer ~60 ms of the 75 ms frame -> single-digit ms if the cost is raster of ops (my estimate, and the thing I could not measure). If the real canvas is on the software path the gain is larger.
- **Risk.** Visual parity on animated art and aim; atlas fragmentation across eras/palettes; memory on mega-armies (bounded by types, not units). The aim-quantization step visibly changes turret angles unless the barrel is a separate rotated sprite.
- **Blast radius.** unit loop and enemy loop in `renderGame` (9253-9259 and the enemy loop that follows), `drawSquadUnit` 9329, `drawSquadCluster`, `drawSwarmCluster`, `ART` functions that read `a` or `animClock` (about 18 + 38), `INK/GLASS/HI/LO/TRIM` palette globals. Terrain, buildings, cities, projectiles and particles are untouched.
- **How to verify.** (a) In the actual game, add a hidden `?sprites` toggle and compare `perfSample` frame ms with the toggle on/off in the same save; (b) pixel-diff a fixed 800-unit scene (`bench5.mjs`'s capture) sprite vs vector at 1x and 0.7x zoom, tolerate <2% differing pixels; (c) the designer's own report window at 7:44, same unit count, frame ms before/after.
- **Tenet check.** Same army, same bodies, same art, same effects; the cost per body falls ~8x so 1,000+ units become *more* viable. No cap, no LOD that removes units.

### 4.2 Stop paying the blur 12,000 times: bake or drop the per-op `shadowBlur` (cheap, decisive stopgap)

- **Mechanism.** The ART bodies are drawn with `shadowBlur=2.4` set once around the whole art call (9259), so every internal fill/rect/arc re-blurs. Replace the per-op shadow with ONE flat dark silhouette offset by (0,1.4) behind the body (the squad path already deliberately draws flat for exactly this reason: comment at 9319-9323 says per-body blur "used to both cost the most and read the worst"), or bake the shadow into the sprite from 4.1.
- **Evidence.** `bench2.mjs`: shadowBlur off = 94.5 -> 55.4 ms to pixels (-41%) in a 666-unit scene; `_heavyLOD` path (blur off) 62.4 ms. 12,960 of 14,320 shadowed ops are small (<=3 px) blurs, the ones a one-pixel offset darker silhouette reproduces to within a pixel.
- **Measured gain.** ~39 ms of 94 in SwiftShader.
- **Estimated real-machine gain.** Likely 30-50% of the non-JS frame time if blur passes dominate on GPU; can be tested within one match by toggling `fullFx` off (already a setting: it sets `_heavyLOD` which drops this blur above 200 units). **The designer can run this experiment today with no code: turn off "Full Effects" and compare the in-game performance report at 790 units.** If the frame drops from ~75 to ~45, hypothesis confirmed.
- **Risk.** Slight look change (soft vs hard shadow edge). The `fullFx` comment at 909 says the visuals "cost the game's whole appeal", so this should be a visually-neutral swap rather than a quality toggle.
- **Blast radius.** One block in the unit loop (9259), `drawSquadUnit` (already flat), enemy loop's equivalent, plus the 39 ART functions that set their own `shadowBlur` for glows (160 ops at >8 px are the worst: `aburn` 1978, `skyGlow`).
- **How to verify.** `bench2.mjs` + the designer's report toggle above.
- **Tenet check.** Same army, same look, cheaper to draw.

### 4.3 Measure the real rasterizer: add a calibration probe to `perfMachine`

- **Mechanism.** The existing report (`perfMachine` ~10530) records cores, dpr and viewport but not what is rasterizing. Add: `WEBGL_debug_renderer_info` renderer string, `ctx.getContextAttributes()`, and a 100 ms calibration draw (1,000 blurred rects + a forced 1x1 `getImageData`) timed at match start. Also dump `PERF_LOAF` render/style/layout phases per worst window (already collected, `PERF_LOAF` 6188).
- **Evidence.** Everything in 2.4 point 1-5 is inference because no real-machine number separates "JS recording", "raster" and "present". This probe separates them in one session.
- **Measured gain.** None directly; it decides between 4.1/4.2 expectations (GPU-slow-at-blur vs software-canvas fallback).
- **Estimated real-machine gain.** n/a; prevents building the wrong thing.
- **Risk.** Near zero. 1,000 rects x 100 ms once per session.
- **Blast radius.** `perfMachine`, `perfSample` (6032-6040 region), the report HTML.
- **How to verify.** Compare the probe's ms against my SwiftShader reference: 1,000 shadowed rects + sync here takes ~X ms (script in the scratch dir if wanted); a GPU Chrome should be far under it, a software fallback should be close to or above it.
- **Tenet check.** Diagnostic only.

## 5. Adversarial questions

1. Why is `fullFx` default `true` (909) with a comment saying "render is cheap by comparison" and removing the count-based LOD? Suspect: the JS `render` number (3.9 ms) was read as "render cost", which is only recording cost, so the one tool that would have shown the truth (frame interval minus JS) was misread.
2. Why does the unit loop set `shadowBlur` once around `ART[...]` (9259) instead of baking a silhouette? Suspect: it was a one-line way to get a soft shadow on 79 hand-drawn art functions without editing them; the squad path already rejected it (9319-9323) for cost and legibility.
3. Why does `ART.fighter`/`aburn` (1978-1985, 2133) set `shadowBlur=len*1.1` inside an art already inside a shadowBlur=2.4 save? Suspect: glow-by-shadow was the quickest afterburner; the 160 big-radius blurs per frame at 67 fighters show it.
4. Why `interpEnter()`/`interpExit()` (10055-10062) walk and mutate every unit twice per frame instead of interpolating at draw time (a local `lx`,`ly`)? Suspect: renderGame and ART read `u.x`/`u.aim` directly so swapping state was the minimal patch.
5. Why does `frame()` have the `perfCapStep` JPEG snapshot machinery (6032) inside the render path, re-rendering the whole world up to 3 times? Suspect: AAR "worst window" screenshots; it only runs when queued but it renders the *real* scene at 60% zoom with every unit visible, a frame-time spike after every bad window.
6. Why is the main canvas `getContext('2d')` with no options (902)? `{alpha:false}` lets Chrome skip alpha blending against the page for a full-screen opaque layer; `desynchronized:true` can reduce latency. Suspect: nobody tried.
7. Why is the sky gradient (`drawWorld` 1640) `worldH()` tall (~1565 px) and re-created and filled every frame at full width? Suspect: it is the simplest way to cover the vertical scroll; it could be a cached 1xN strip.
8. Why does `drawSquadUnit` (9329) draw a ground contact shadow per body and `drawSquadCluster` only one shared? Suspect: SQUAD_LOD was added after the fact; the full path is still the default below 45 squads (`SQUAD_LOD_N=45`, 3899).
9. Why is `drawVignette()` (10022) a full-screen gradient `fillRect` plus a CSS `.grain` layer (202), when the comment says the CSS vignette was removed to avoid double darkening? Suspect: leftover; one of them (probably the CSS repeating-linear-gradient scanlines) could be a static pre-rendered PNG.
10. Why does `glow()` (9548) do `save`, `globalCompositeOperation='lighter'`, `translate`, `scale`, `arc`, `fill`, `restore` per glow rather than `drawImage` of a cached glow sprite? Suspect: it began as a particle effect and acquired units (era glow at 9255) later; `GLOW_SOFT_CAP` is a budget on the symptom.
11. Why do `bar()` (9600) and the unit/squad HP bars exist as 4 canvas calls per unit rather than only when damaged and only when the unit is on screen at normal zoom? Suspect: they already are gated (`u.hp<u.maxHp`); low priority.
12. Why does the FX_Q adaptive system (910-914, `fxDerive`) scale particle/glow budgets and `FXB()` shadow blur, but not the per-body `shadowBlur` at 9259? Suspect: it was designed around particles and glows; the unit shadow predates it and is gated only by `_heavyLOD`. (ROADMAP says `fxTick` now keys on the real frame interval, so it engages, but its knobs miss the dominant cost.)
13. Why are `ART` sprites drawn at the unscaled size and the whole world zoomed with `c.scale(cz)` (10125 area) rather than selecting a smaller sprite at low zoom? Suspect: zoom was added late; the blip LOD at `_cz<.5` is the only concession.
14. Why does the unit loop re-check `u.carried`, `d.domain`, `curEra()`, `_extremeLOD` per unit and call `curEra()` as a function per aircraft (9255)? Suspect: trivial JS cost, but it signals the loop is a long accretion of per-unit features, each 1-3 ops, which is how 100 calls/unit happened.
15. Why does `renderGame` run `for (const su of G.units) if (su.squad...)` (9248) every frame to compute `liveSquadN` when the sim could maintain it? Suspect: SQUAD_LOD was bolted on in the render pass.
16. Why is `getImageData`/`toDataURL` used by `captureSnap` (6094-6099) against the *full* canvas at 3 moments rather than from the already-downscaled minimap or a second offscreen render? Suspect: simplicity; each is a GPU->CPU readback and a pipeline stall on the frame it runs.
17. Why does the perf report log `render` as `performance.now()` around `renderGame()` (10098-ish, `perfAdd('render')`) rather than also logging the rAF delta minus script time? Suspect: JS timers were the available instrument; `PERF_LOAF` now exists and could close the gap.

## 6. Cleanup list

| item | where | note | tag |
|---|---|---|---|
| `_heavyLOD` / `_dt2` / `_dt3` / `_extremeLOD` count-based branches are dead while `fullFx` is default true and `fxAuto` can only reduce non-unit knobs | 9157-9160 | keep until sprites replace them, then delete | GATED (needs visual diff) |
| Per-op `shadowBlur=2.4` around `ART[]` | 9259 and enemy loop | replace with baked/flat shadow | GATED |
| Two separate shadow strategies (squad flat vs unit blurred) | 9259 vs 9319-9323 | one policy | GATED |
| Duplicated squad cluster paths (`drawSquadCluster`, `drawSwarmCluster`, full `drawSquadUnit`) | 9329-9400 | three renderers for one concept; a sprite path unifies them | GATED |
| `SQUAD_LOD_N`/`SQUAD_LOD_ZOOM` LOD that swaps visuals when many squads | 3899, 9248 | bandage on the same raster-cost problem; replace by sprites | GATED |
| `liveSquadN` recomputed every frame by scanning `G.units` | 9248 | maintain in sim | SAFE |
| `interpEnter/Exit` full-array mutate-and-restore twice per frame | 10055-10062 | interpolate at draw time | GATED |
| `GLOW_SOFT_CAP` white-out clamp and `_glowFrameCount` | 9548-9554 | cap exists because glow is expensive per draw; cached glow sprite removes the need | GATED |
| `perfCapStep` re-renders full scene up to 3x for AAR thumbnails | 6032 | produces its own frame spikes | GATED (feature) |
| Duplicate vignette (CSS `.grain` scanlines + canvas `drawVignette`) | 202, 10022 | evaluate which is needed | SAFE if visually identical |
| Debug/diagnostic remnants: `RSTAT.culled/drawn` counters on hot loops | 9156, loops | tiny but per-entity increments | SAFE |
| `FXB(blur)` helper and ART-internal `shadowBlur` for glows | 1980-1985 and 39 fns | candidates for baked sprites | GATED |
| comments claiming "render is cheap by comparison" and "the sim is what's ever actually slow" | 909 | contradicted by the measurement above; reword | SAFE |

## 7. What I would NOT touch, and why

- **The sim and the spatial grid** (`SpatialGrid`, `GRID_*`, `updUnits`): the real report shows `units` 2.9 ms, `enemies` 0.2, `proj` 0.0 in the worst window. Whatever else is slow, it is not these.
- **`bakeTerrain`/`terrainBlit` and the minimap cache**: already the right pattern (cache the static layer, blit it); they are the template for the unit sprite cache.
- **Particles and projectiles drawing**: the report's frame time did not move between 49 and 2,877 particles. Optimizing them would be shaving 0.2 ms patches.
- **HUD/DOM**: 0.4 ms measured, no layout cost.
- **The art itself**: 79 hand-drawn vector functions are the game's look. Bake them, do not redraw them.
- **Designer-facing rules** (CLAUDE.md tenet): no caps, no hidden unit LOD that drops or hides units. All three proposals keep every body drawn.

## 8. Where I disagree with the ROADMAP

Read after sections 1-7 were written. The ROADMAP (entries dated 2026-11-28 "BLIND STRUCTURAL REVIEW", v4.8.1 "WHERE THE TIME GOES", v4.8.0 "THE FRAME BUDGET", and the 2026-08 perf loop) mostly points the same way as my measurements; the places it looks wrong or incomplete:

1. **The v4.8.0 adaptive-effects lane cannot reach the dominant cost.** `fxDerive` (914) scales `_fxPartCap`, `GLOW_SOFT_CAP`, particle life, terrain re-bake and minimap cadence, and sets `_fxNoBlur = q<.6`, but `_fxNoBlur` only gates the `FXB()` helper used by the glow shadows inside ART. The per-body `shadowBlur=2.4` at line 9259 is gated only by `_heavyLOD` (`!fullFx && >200 entities`, or `cz<.85`), never by `FX_Q`. So even at FX_Q .25 the 12,000 blurred ops per frame stay, and the ROADMAP's "it engages now, FX_Q .55 at 9 s" says the lever moved while the thing that costs time did not. The v4.8.0 design assumed particles and glow were the visual cost; the measured report (49 particles vs 2,877 gives the same frame time) already said they are not.
2. **"~60 ms ... scales with unit count while idle" is right, and the ROADMAP's own coordinator hypothesis (1) RASTER is the one I back, but the list of suspects in v4.8.1 is mis-ranked.** "The recorder's capture+VP9 encode" only exists while recording (`REC.on`); "browser style/layout after HUD writes" is falsified by H's own finding and by my 0.4 ms `syncHUD` + 0 ms forced layout. The per-unit slope of the three report windows (84, 95, 87 us/unit, particles and projectiles irrelevant) is the discriminator and is already in the ROADMAP numbers; it was not computed.
3. **The "hardware acceleration OFF" detector is the wrong test.** `perfMachine` matches `swiftshader|software|llvmpipe` against the WebGL unmasked renderer. A machine can have a real GPU for WebGL while the 2D canvas is CPU-rasterized (GPU raster blocklisted, `--disable-accelerated-2d-canvas`, a driver bug, remote desktop, or Chrome's own demotion of a 2D canvas after repeated readbacks), and then the WebGL string reads as a normal GPU. And the converse: a fully GPU-accelerated canvas can still be 60 ms from thousands of blur passes. The decisive test is a timed calibration (1,000 shadowed rects + forced 1x1 readback, my proposal 4.3) compared against the SwiftShader reference measured here, not a string match. The designer's `chrome://gpu` check is good but may say "Canvas: Hardware accelerated" and still not explain the frame.
4. **"Sweeps — 53 separate full passes over G.units" (hypothesis 2) is not where the frame goes.** The render-side linear passes (`interpEnter/Exit`, `liveSquadN`, culling loop) cost ~0.1-0.3 ms in my profile (`prof.mjs`: JS functions <1% each; 83% of samples in native `fill`), and the sim `units` section is 2.9 ms in the worst real window. Merging sweeps would be a nice cleanup but would not touch the 60 ms.
5. **The 2026-08 ruling "render is cheap by comparison" (909) and the v3.98.0 perf diagnosis ("Bottleneck is `updUnits` ... 82 of 85 ms/tick") were measured headless with sim-only benches.** They answered "what is the sim cost" correctly but were then generalised into "the sim is what is ever actually slow". The ROADMAP line "Render ... was NOT separately measured (rAF-driven) but scales with the same body count — a co-cost on top of sim" was the right instinct that never got turned into a measurement, because render was timed by JS call time. 3.5x on a 5,000-unit sim bench does not move a raster-bound frame.
6. **"Lever B (aggregate battlegroups, the only true 5k scaler)" and "squad LOD" are the wrong shape of fix for the raster problem.** They reduce bodies drawn by changing what is drawn (cluster glyphs), which is exactly the tenet-sensitive direction; the sprite cache keeps every body and every pixel. Per the tenet this should be preferred to any LOD that changes what the player sees.
7. **Headless "render 41 ms at 940 units" (v4.8.0 ship notes) is a to-pixels-ish number from a CPU rasterizer, and is quoted next to "render ms" from the real report (3.9 ms) as if comparable.** They are different quantities (my JS 15 ms vs to-pixels 94 ms for the same scene). Any further "render at zoom 1 unchanged" gate should compare time-to-pixels, with a forced readback, not JS call time.

# R1 review: the simulation and the data

Reviewer lens: `updateGame` -> `updateGameInner` and everything it touches per step. All numbers below are headless Chromium (SwiftShader, 1920x1080) unless stated. Scripts are in the scratch dir `.../scratchpad/r1/` (`scale.mjs`, `sweeps.mjs`, `wrapt.mjs`, `fx2.mjs`, `shcount.mjs`, `bm.mjs`, `sprite.mjs`).

## 1. Executive summary
1. The simulation is NOT where the frame goes. On the designer's PC the worst window is sim 3.8 ms x 1.4 steps = 5.3 ms plus 3.9 ms of render JS = about 9 ms of JavaScript inside a 75 ms frame. At least 66 ms is spent outside JS (canvas raster and compositing).
2. The sim is already linear and well bucketed. Idle cost is about 2.3 us per unit per step in the slope (3.4 us at 800 units), growing linearly from 100 to 3,200 units with no O(n^2) term. Headless 800 units = 2.7 ms against 3.8 ms measured on the real PC, so the headless sim number is a good proxy.
3. The thing that scales with unit count and is catastrophic is Canvas2D drawing. 800 units issue about 23,700 fill/fillRect calls per frame and 17,100 of them (72%) carry an active shadow. Making `shadowColor` a no-op takes the 800-unit frame from 3,041 ms to 64 ms in SwiftShader (47x). A 40-bomber frame goes 2,395 ms to 15 ms.
4. The structural fix is to stop re-issuing about 30 vector draw calls per unit per frame: bake each unit's art once into a sprite and `drawImage` it. A synthetic 800-unit test: 13.0 ms direct with shadow versus 0.7 ms sprites (SwiftShader).
5. Multithreading would not help (see Q2). The existing spatial grid works, but the sim does not need much more from it.

## 2. My lens: findings

### 2.1 One step, walked
`frame()` (line 10064) runs a fixed 30 Hz accumulator, capped at 3 steps per frame (line ~10074). Each step calls `updateGame` (6197) -> `updateGameInner` (6198). In order:
- housekeeping at 1 Hz (`checkAchievements`, `checkEvents`, `tickAutoStrikes`, `sampleHist`): negligible.
- `recalcBuildings`, economy loops over `G.buildings` (6217-6230) and `G.units` (6224, 6241), the aura and fort bucket maps rebuilt every step (6226-6234): small, 43-170 buildings.
- `G.fires`, `G.beams`, `G.teleLines`, `G.domes`: empty when idle.
- garrison fill loop over buildings (6341+).
- per-city loops 6492 (all enemies) and 6493 (all units) for each city: cities x (E+U) sweeps; 3 per step idle, 7 in battle. Cheap but this is the only "multiplied by something" sweep in the step. Could be replaced by one pass bucketing by city.
- `updUnits` (7719): the main cost, see 2.2.
- `updBuildings` (8308), `updEnemies` (8507), `updProj` (8826), `updShrap`, `updSwarmSupport` (8771), `updParts` (9082).

Sweep counts per step (counted by patching the iterator and array methods on the live arrays, `sweeps.mjs`, 20 steps averaged, about 600-740 units):

| list | idle | battle (6 hostiles, 63 proj) |
|---|---|---|
| G.units full iterations | about 12 | about 16 |
| G.enemies | about 8 | about 12 |
| G.buildings | about 12 (+17 in `nearestDoneAssembly`, line 8771) | similar |
| G.proj | 3 | 3 |
| G.parts | 2 | 2 |
| filter/find/some/indexOf/splice on those arrays | 1 (`G.buildings.filter` in `updUnits` 7723) | 1 |

So roughly 12-16 full passes over units per step. At 3 us per unit per step total that is NOT a fan-out problem; the passes are short and mostly sequential reads. Nothing is O(n^2) in disguise at normal density: the separation pass (line 7757-7764) is bucketed 150px with `SEP_CAP = 12` and runs every other tick above 400 units; it costs 0.2 ms at 700 units (6%).

### 2.2 What does ONE idle unit cost, and where
Measured with timer wrappers around 40 sub-functions (`wrapt.mjs`; the wrapping itself inflates totals about 25%, so read the relative shape): at 713 units idle, per step:

| function | ms/step | calls/step | note |
|---|---|---|---|
| `updUnits` inclusive | 3.3 (about 2.4 uninstrumented) | 1 | 75% of the sim |
| `updSquadMembers` | 0.36 | 74 | infantry squads, 4.8 us per squad |
| `updEnemies` | 0.31 | 1 | with ZERO enemies; most of it is `friendliesList` -> `refreshFriendlies` (4297), which rebuilds the friendly list and BOTH grids every step (about 26 KB garbage) |
| `elevAt` | 0.29 | 1,409 | called twice per unit |
| `radarBoostU` + `radarBoost` | 0.20 + 0.19 | 713 + 279 | per unit, per step |
| `fortBonusAt` | 0.15 | 320 | |
| `airFilter` | 0.14 | 713 | one call per unit, result is a pure function of the def (`d`) |
| `canIntercept` | 0.12 | 504 | also pure per def |
| `auraAt` | 0.08 | 290 | |
| `updParts` | 0.16 | 1 | |

An idle ground unit: copies pose for interpolation (`u.px = u.x`, 7721), is bucketed for separation, then in the main loop (7771+) evaluates `radarBoostU`, `airFilter`, `lodShouldScan`, `acqKeep`, `elevAt`, `fortBonusAt` and `auraAt`, and only then decides it has nothing to do. All of those are small lookups, so the cost is death by many small calls, not by any one loop. Roughly 15-20 function calls per unit per step.

Per-unit-type variation is small: rendered cost is the large one (section 2.4).

### 2.3 Scaling (headless, `scale.mjs`, 200 steps each, G.paused = true and stepped manually)
Idle (prepTimer = 9999):

| units | sim ms/step | us per unit |
|---|---|---|
| 100 | 0.59 | 5.9 |
| 200 | 0.79 | 4.0 |
| 400 | 1.28 | 3.2 |
| 800 | 2.69 | 3.4 |
| 1,600 | 3.79 | 2.4 |
| 3,200 | 7.35 | 2.3 |

Linear with a fixed overhead of about 0.4 ms. No super-linear term up to 3,200 units. Battle (invasion running, random hostile counts, so noisier): 800 units 2.8 ms, 1,600 units + 13 hostiles 10.4 ms, 3,200 units + 69 hostiles + 2,680 projectiles 17.8 ms (`updProj` 3.4 ms, `updUnits` 16.9 ms of 23 ms instrumented).

A 2,400-step battle with about 500 units (`bt.mjs`): average 1.8-6.0 ms per step by 300-step window, but the MAX single step reached 44 ms and 26 ms in the heavy windows, so there are spike steps. I did not isolate their source (candidates: `boom`/chain-explosion bursts, `hurt` fan-out, `G.parts` splice, GC); that is worth a `PERF_LOAF` read on the real machine. The 217 ms worst frame is probably one of these plus the raster stall.

Garbage: about 200 KB per step total (`alloc.mjs`), top contributors `refreshFriendlies` 26 KB, `updSquadMembers` 20 KB, `acqKeep` 14 KB, `radarBoostU` 13 KB, `elevAt` 12 KB. That is about 6 MB/s at 30 Hz, GC about 0.15 ms per step. Not a problem.

### 2.4 Follow the evidence: where the frame actually goes
Headless can render the real thing at 800 units: `rast.mjs`/`fx2.mjs` run the real `drawWorld` + `renderGame` for N units.

- Draw-call census (`shcount.mjs`, 800 mixed units, viewport at the army): 11,873 `fill`, 11,428 `fillRect`, 420 `stroke`, 4,288 `save`, 13 gradients, 1 `drawImage`. Total about 23,700 paint ops. 17,123 had a live shadow (`shadowColor` set with blur > 0 or offset != 0), 456 of those with a non-`source-over` composite. That is about 30 paint ops per unit, set up by `drawUnit` at line 9259 (`c.shadowColor = 'rgba(0,0,0,.55)'; c.shadowBlur = 2.4; c.shadowOffsetY = 1.4` then `ART[d.art](c, d.s, aim)`).
- Bisecting what makes it slow, real frames at 800 units (`fx2.mjs`, rAF-measured, 5 s): default 3,042 ms (2 frames); `shadowBlur` setter ignored 2,908 ms; `shadowColor` setter ignored 64 ms; `globalCompositeOperation` ignored 131 ms; `fullFx = false` 81 ms. So the cost is "shadow on" combined with the art's `lighter` glows and per-op layer: killing either shadow colour or composite collapses it.
- Per type (40 units each, `perkey.mjs`, ms per unit of raster+JS over the baseline frame): infantry 0.06, tank 0.12, fighter 0.22, heli 0.10, but bomber 53 ms and `pdjet` 13 ms. `bm.mjs`: 40 bombers 2,395 ms default, 15 ms with `shadowColor` forced transparent, 22.7 ms with composite forced `source-over`. pdjet 601 ms -> 12.6 ms. These two arts draw a `lighter` glow while a shadow is still in the context state inherited from the unit draw.
- The real-machine report is consistent with this: 9 ms of JS in a 75 ms frame cannot be the sim. The JS time measured as "render" (3.9 ms) is only the time to ISSUE the calls. Chrome records them and rasterises later on the GPU/raster thread, and every shadowed op forces an offscreen blur layer there.

What SwiftShader cannot tell us: it is a CPU rasteriser, so the absolute numbers (seconds) are far worse than a real GPU, and the relative cost of shadow versus plain fill is different on a GPU (blur passes batch better). The 47x ratio is NOT a prediction for the real machine; the claim is only "shadowed + composited ops are by far the most expensive thing per unit". The in-game report would confirm it by (a) the existing 'real frame ms' minus (sim + render JS) staying at 60+ ms with identical units, (b) toggling Settings -> Full Effects off (that is `fullFx = false`, which sets `_heavyLOD` and removes the shadow, line 9157/9259) on the designer's PC and seeing the real frame fall from 75 ms to 20-25 ms with the same army.

## 3. The three fixed questions

### Q1. If you could change ONE thing?
Bake every unit's art into a cached sprite (per def key, ally/enemy palette, facing, and quantised turret aim for ground units) and `drawImage` that instead of running `ART[d.art]` each frame. This turns about 30 paint ops with a shadow into one `drawImage`, so 800 units cost 800 blits regardless of unit complexity. Measured synthetic 800-unit draw: 13.0 ms direct with shadow (9.2 ms without) versus 0.7 ms with baked sprites in SwiftShader (18x); the real-machine gain is probably 40-60 ms of the 75 ms frame in the worst case. Rewrite size: `drawUnit` at line 9259 plus a ~150-line sprite cache; the ART functions stay as they are and get called only at bake time.
(Two cheaper stopgaps before that: set `c.shadowColor = 'transparent'` before any `lighter` glow inside ART, and drop the per-unit shadow when zoomed out.)

### Q2. Would multithreading help?
No, not meaningfully.
- Sim: 3.8 ms x 1.4 = 5.3 ms of a 75 ms frame (7%). Even a perfect 8x parallel sim would save at most 4.6 ms. `G` is a graph of thousands of mutable objects with back-references (`u.bgLeader`, `carrFrom`, `ferryClaim`, `e._lodTgt`), so a worker needs full serialisation each step; cost likely exceeds the sim itself.
- Render: OffscreenCanvas in a worker would move the 3.9 ms JS issuing off the main thread, but the 66 ms is raster/compositing that already runs on GPU/raster threads, so the main thread is not the bottleneck; it would only get a pipelined frame at the cost of state snapshots.
- SharedArrayBuffer needs cross-origin isolation headers (COOP/COEP) which GitHub Pages cannot set; the `coi-serviceworker` trick works but costs a reload and breaks the YouTube iframe music embed (a cross-origin embed that is not CORP-ready under COEP).
- The 28-core machine is idle for this workload; the bottleneck is one GPU path.

### Q3. Is the spatial grid doing its job?
Yes for combat queries. `SpatialGrid` (line 4271): 256px cells, four instances (`GRID_E` enemies, `GRID_P` projectiles, `GRID_F` friendlies, `GRID_A` air), lazy rebuild, `acquireEnemy` (4304) and the other `near`/`firstHit`/`forNear` consumers walk only the cells under the circle. The cost it pays: `refreshFriendlies` (4297) rebuilds the friendly list and both friendly grids every step (units move every step), 0.2-0.3 ms at 700 units, and runs EVEN WITH ZERO ENEMIES because `updEnemies` (8507) calls it unconditionally.
Bypassed (raw sweeps that scale with entity count, not with local density): the per-city loops at 6492/6493, the ferry scan (`for (const c of G.units)` at ~7860, once per ferry-heli every `FERRY_SCAN_INTERVAL`), the carrier wing count (`for (const o of G.units) if (o.carrFrom === u)` in `updUnits` per carrier per step, O(carriers x units)), `nearestDoneAssembly` (17 calls per step over buildings), `G.buildings.filter` for rail hubs each step (7723), and `particleHit` (`G.enemies.concat(incomingList())` allocating a fresh array per call, 4305). In total there are 36 `for (const e of G.enemies)` and 53 `for (const u of G.units)` loops in the file. None showed up above 0.1 ms in the profile at 800 units, but they will matter in a 5,000-enemy swarm.

## 4. Top 3 structural proposals

### P1. Sprite-cache the units (render, not sim)
- Mechanism: per (def key, owner palette, facing, aim bucket) bake `ART[d.art](ctx, d.s, aim)` once, with the shadow baked in at bake time, then `c.drawImage(sprite, px - ox, py - oy)` per unit per frame. Keep vector draw only for units with dynamic parts (carrying cargo, damage flash, rotor angle), or bake those parts as separate sprites.
- Evidence: 23,700 paint ops per frame at 800 units, 72% shadowed (`shcount.mjs`); `shadowColor` off -> 3,042 -> 64 ms; `sprite.mjs` 13.0 -> 0.7 ms.
- Measured gain: 18x on the unit drawing cost in SwiftShader (synthetic).
- Estimated real-machine gain: 40-60 ms off the 75 ms worst frame, i.e. back under 16.7 ms. Uncertain because it is a GPU path I cannot measure here.
- Risk: art that animates (rotor, turret aim, muzzle, fuel/ammo bars) needs aim quantisation (say 32 buckets) or layering; bars and selection rings stay vector. Memory: 14 unit types x 2 palettes x 2 facings x 32 aims x ~64x64x4 B = about 30 MB worst case; bake lazily.
- Blast radius: the unit draw loop at 9259 and its helpers (`bar`, `ring`, `drawSquadUnit` 9328, `drawSquadCluster` 9368), `ART` untouched.
- Verify: headless frame time at 800 units before/after; screenshot diff at cz = 1; designer's PC performance report, same army, compare "real frame ms" minus JS.
- Tenet check: same army, same visuals, cheaper to draw. No cap.

### P2. Make shadow a bake-time or one-pass effect, and fence `lighter` away from shadow state (the cheap version of P1, also useful alone)
- Mechanism: in `drawUnit` (9259) and every ART that sets `globalCompositeOperation = 'lighter'`, reset `shadowColor = 'transparent'` first (or draw the shadow as a flat dark ellipse under the unit and never use canvas shadows).
- Evidence: bomber 2,395 ms -> 15 ms; pdjet 601 -> 12.6 ms; `inf` 49.5 -> 14 ms (40 units); `shadowColor` ignored at 800 mixed units 3,042 -> 64 ms.
- Gain / risk: tiny diff, tiny visual change (soft shadow becomes flat or baked). Uncertain on GPU; expected positive because the shadowed-layer cost is per op.
- Blast radius: line 9259, ART functions using `lighter` (29 occurrences of `'lighter'` in the file). Verify as P1. Tenet: yes.

### P3. Fold the per-unit sim helpers into a once-per-step cache (sim, small)
- Mechanism: `airFilter(d)`, `canIntercept(d)`, `weaponCeil` result (already memoised as `d._cf`), `radarBoostU` and `elevAt(u.x)` are pure per def or per x-cell; precompute per unit-def and per 64px bucket once per step. Skip `refreshFriendlies` while `G.enemies.length === 0`; build the friendly grid lazily on first query instead of in `updEnemies`.
- Evidence: `elevAt` 1,409 calls/step, `radarBoostU` 713, `airFilter` 713, `canIntercept` 504, all returning the same values; `refreshFriendlies` 0.2-0.3 ms idle with no enemies.
- Measured gain: estimated 0.6-0.8 ms of the 2.7 ms idle step (about 25%), but that is under 1 ms on a 75 ms frame. Honest: this is the "tiny patches" the designer does not want, listed for completeness.
- Risk: low; behaviour identical. Blast radius: `updUnits`, `updEnemies`, `refreshFriendlies`. Tenet: yes.

## 5. Adversarial questions
1. Line 909: `fullFx = true` with the comment "the sim is what's ever actually slow (render is cheap by comparison)". Why was this concluded? I suspect it was inferred from the in-game "render ms" timer, which only counts JS that issues canvas calls, not raster. The report's section timers cannot see raster.
2. Line 9259: why set a canvas shadow per unit instead of a ground ellipse or a baked sprite? Probably it looked good and was cheap at 50 units.
3. Why do ART functions use `'lighter'` while inheriting the caller's shadow state? Likely no one ever audited `save/restore` ordering between `drawUnit` and ART.
4. Line 10074: why a 3-step catch-up cap and 30 Hz sim? Presumably to survive tab-switch dt; but a slow render frame still produces `perfAdd('steps')` of 1.4-1.5, so the sim runs more steps exactly when the raster is slow. Do you want the sim to be decoupled from render stalls?
5. `refreshFriendlies` (4297): why rebuild the whole friendly list + two grids every step, including with zero enemies? Presumably because a unit moves every step and simple is safe.
6. `updUnits` line 7721: why copy `px/py/pAim` for every unit each step instead of only when drawing? To support `interpEnter/Exit`; it doubles a store on every unit. Alternative: keep two position arrays.
7. Line 7776+: why does an idle unit with no target and no enemy on the map still evaluate `radarBoostU`, `airFilter`, `lodShouldScan`, `acqKeep`? I suspect the idle path was never given an early-out when `G.enemies.length === 0`.
8. `G.units.filter(...)` for rail hubs at 7723: why allocate a fresh array every step? Probably left from before there were buildings lists indexed by type.
9. `particleHit` (4305): `G.enemies.concat(incomingList())` allocates a combined array per call. Why not query both grids?
10. 6492/6493: why sweep all enemies and all units once per city per step to count ground forces? I suspect a per-city threat score computed before grids existed.
11. `updSquadMembers` (8211): 74 calls per step at 4.8 us, with 20 KB garbage: why per-squad per-step allocation?
12. Why are there four grids (E, P, F, A) with different modes inside one class (`mode` 0/1/2) rather than two typed ones? To reuse code; fine, but `GRID_A` is a strict subset of `GRID_F`.
13. `perfCapStep` / `PERF_LOAF` / `perfMachine` instrumentation sits inside `frame()`: does it run unconditionally? It showed up at 0.49 ms/step in one headless profile when a capture was queued; it should be zero cost otherwise.
14. Why is 'Full Effects' default ON while the Auto-FX controller (`fxDerive`, 914) is also there? The comment at 909 says the old cutbacks "bought nothing" yet `fullFx = false` is the only setting that brings 800 units from 3,000 ms to 81 ms in SwiftShader.
15. Line 9157/9166: `_heavyLOD` and `_extremeLOD` are keyed on unit count only when `fullFx` is off. Is there any LOD at all for Full Effects at 800+ units besides zoom?
16. Why does the real-machine report's "projectiles 0.0" section timer read zero in a window with 671 projectiles? I suspect the per-section timers (`perfLap`) are only wired for some sections; verify the timers cover what they claim.

## 6. Cleanup list
- SAFE: `G.units.filter` for `railHubs` (7723): hoist or cache per step.
- SAFE: `G.enemies.concat(incomingList())` in `particleHit` (4305): two grid queries.
- SAFE: `updEnemies` calling `friendliesList` before checking `G.enemies.length`: early-out.
- SAFE: per-step `new Map()` aura and fort caches at 6226-6234: persistent Maps with pooled arrays like `SEPB`.
- SAFE: `incomingList` / `interceptableProjList` / `airFilter` / `canIntercept`: memoise per step or per def.
- GATED: separate `GRID_A` vs `GRID_F`: build once.
- GATED: layers of LOD patches on the same problem: `lodPrep`, `lodShouldScan`, `lodBuildEngageIndex`, `acqKeep`, `_heavyLOD`, `_extremeLOD`, `fxHeavy`, `_dt2`/`_dt3`, `v4.5 P` retarget cooldown, `bgLeader` shared brain. Several overlap; none addresses raster.
- GATED: ferry scan `for (const c of G.units)`: grid query instead.
- GATED: carrier wing count: maintain a counter on the carrier instead of scanning units.

## 7. What I would not touch
- The simulation core and its 30 Hz fixed step: 2-3 us per unit, linear, already bucketed with caps (`SEP_CAP`, `lodShouldScan`). The sim is 7% of the real worst frame.
- `SpatialGrid` itself: correct and cheap; improve call sites, not the class.
- Army caps, unit counts, ammo/fuel logic: no tenet-friendly gain.
- The research tree, economy and stability code: all trivial per step.
- Do not touch multithreading until the raster problem is solved; it would add a serialisation layer for 7% of the frame.

## 8. Where I disagree with the ROADMAP

(Written after reading the perf entries in ROADMAP.md: the 2026-11-28 BLIND STRUCTURAL REVIEW entry, v4.8.1 "WHERE THE TIME GOES", and the 2026-08 PERF LOOP entries. I did not open any other reviewer's file. The ROADMAP entry quotes some other-reviewer conclusions, which I read only now and which did not shape sections 1-7.)

1. **"Perf loop CLOSED at ~3.5x ... 5,000-unit sim 301 -> 86 ms/tick" (Aug 2026) measured the wrong thing for this problem.** I agree the sim got to its floor (my idle numbers: 2.3-3.4 us per unit per step, linear to 3,200 units), and I would not reopen it. But "closed" let the code comment at line 909 stand ("the sim is what's ever actually slow, render is cheap by comparison"), and `fullFx = true` was set on that basis. That comment is the opposite of the evidence: 800 units is 2.7 ms of sim and 24,000 paint ops, 72% shadowed. The perf loop measured sim ms/tick in a headless bench at 5,000 units and never measured raster.
2. **"Effects quality never engaged because fxTick read the timer sum" (v4.8.1) is true but is not the main reason FX_Q cannot save us.** Even with `fxTick` fixed, `FX_Q` scales the particle cap, glow cap and similar (line 914, `fxDerive`) and does not touch the per-unit `shadowColor/shadowBlur/shadowOffsetY` at line 9259. In my bisect, `shadowColor` ignored is 3,042 -> 64 ms while `fullFx=false` is 81 ms: the per-unit shadow is the load. Auto-FX will keep failing until it can switch that line, and a sprite cache makes the question moot.
3. **The v4.8.1 suspect list (style/layout after HUD writes, hardware acceleration off, recorder encode) is incomplete.** It puts the 60 ms gap on browser-side causes that are not in our drawing code. I can reproduce the gap with NO DOM and NO recorder, in a pure canvas call sequence: one 800-unit `renderGame()` followed by `getImageData` is 3,000 ms in SwiftShader and 64 ms with shadows off, while the JS issuing time is a few ms. So at least one cheap, code-side explanation fits the data before anyone blames the PC. The designer's chrome://gpu check is still worth doing (if Canvas is "Software only" the effect is much worse) and the next report's `gap`/LoAF line will separate "GPU raster" from "layout".
4. **"Coordinator's hypothesis: 53 separate full sweeps over G.units" is wrong, I agree with that, and the ROADMAP already says so.** My counts are about 12-16 unit passes per step, all cheap. I would go further: do not spend a lane on merging sweeps. The sweeps cost under 10% of a step that is under 10% of a frame.
5. **"Workers: at most ~13% (offloadable JS about 9 of 75 ms)" is the right order of magnitude, but the designer's "13% is still quite a bit" deserves a harder answer.** Only the sim (5.3 ms, 7%) could realistically move to a worker, and the object graph (`bgLeader`, `carrFrom`, `ferryClaim`, `_lodTgt` pointers) makes that a rewrite, not a port. Moving canvas issuing to an OffscreenCanvas worker needs the render data snapshot per frame. Both are large for a gain that disappears once the raster stall is fixed. Do the sprite cache first and re-measure; if the frame is then 8 ms, nobody will ask for workers.
6. **"A subtle frame skip so the simulation slows smoothly" (designer idea) addresses the wrong side.** The sim is about 2 ms of the stall; slowing it to protect the frame rate buys almost nothing here, because the frame is slow for raster reasons. It is a fine safety net for a real 5,000-unit sim spike (the Aug 2026 "Lever A"), but it will not fix the 75 ms idle frame.
7. **The ROADMAP says the "single biggest" outliers are not individually visible. They are.** In my per-type census two arts are 100x outliers: `lancer` (bomber) 53 ms per unit and `pdjet` 13 ms per unit versus 0.06-0.24 ms for everything else, because they draw `lighter` glows under the inherited shadow. A player with an air wing of 100+ bombers (the designer's own "air units were overpowered, dirt cheap once you have airbases" complaint) is hitting this. Fix those two before building any sprite system; it is a two-line change per art and it answers "why does the air force lag more than the ground army".
8. **Missing from the ROADMAP: the 44 ms and 26 ms single-step spikes in my battle run** (`bt.mjs`, units about 500). The ROADMAP's sim numbers are all averages. The real report's 217 ms worst frame is not explained by average sim cost. Whoever builds the next perf lane should add a "worst step" and "worst step's top function" to the performance report, not another average.

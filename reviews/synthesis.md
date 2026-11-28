# Synthesis: three blind reviews, one answer

Three reviewers each wrote their own review without seeing the others: R1 (simulation and data), R2 (drawing and browser) and R3 (clean-slate architecture). None saw the design conversation. R1 restarted after an interruption. It read the ROADMAP only after writing its sections 1–7, and by then the ROADMAP quoted R2 and R3. Its findings were already written, so it still counts as independent. All numbers are headless Chromium on SwiftShader, a CPU rasterizer. Absolute milliseconds are not the designer's. The ratios and the op counts carry over.

## The constant thing
All three, asked "if you could change ONE thing?", gave the same answer: **stop redrawing every unit as live vector art each frame. Bake each unit's picture once, with its shadow, and stamp it with `drawImage`.**

| | R1 | R2 | R3 |
|---|---|---|---|
| One thing | sprite cache, shadow baked | sprite cache, atlas | `cachedArt` wrapper around `ART[k]` |
| Measured | 800-unit draw 13.0 → 0.7 ms | unit layer 94 → 11 ms to pixels; ops ~18,000 → ~1,100 | full frame 110 → 47 ms (800), 203 → 67 ms (1,600); ops 107k → 24k |
| Real-machine estimate | 40–60 ms off the 75 ms frame | unit layer to single-digit ms | 75 → 35–40 ms (floor 1.3×) |
| Workers | skip (sim 7% of frame) | no (raster isn't JS) | ≤ ~13% (9 of 75 ms offloadable) |
| Grid | doing its job | doing its job | ~2.4% of a step |

All three explain report 10 the same way. R3 reproduced it headless: a 72 ms frame against 2.5 ms × 1.5 of sim, where the real box showed 75 ms against 3.8 × 1.4. The simulation is about 7% of the frame. It is linear at ~3 µs per unit, from 100 to 3,200 units. The rest is the browser rasterizing roughly 100–130 canvas operations per drawn unit after our JS returns. The game's "render ms" timer only measures the time to issue those calls.

## The mechanism they each found
- **One shadow, blurred thousands of times.** `renderGame` sets `shadowBlur = 2.4` around the whole `ART[...]` call (title.html ~L9259). Canvas2D then blurs every internal fill and stroke separately. That's ~12,000 blurred ops per 800-unit frame, about half of unit raster cost.
- **`fullFx` defaults to true**, with a comment saying "render is cheap by comparison". That turns off the count-based LOD that would have dropped this blur. The adaptive effects controller (`FX_Q`) never touches this line. R1 and R3 both found that the comment, and the ROADMAP's "perf loop closed" ruling, rested on JS issue-time, not raster.
- **R1 only: glows inherit the shadow.** Additive `'lighter'` glows inside unit art pick up the unit's shadow state. Measured headless, 40 bombers took 2,395 ms with it and 15 ms without. This makes some unit types catastrophically more expensive than others.

## Coordinator's hypotheses, scored
1. **Raster cost; fix with a sprite cache.** CONFIRMED by all three.
2. **53 full sweeps over `G.units`.** WRONG. R1 counted 12–16 per step, none O(n²). R3 puts the whole sweep cost at ~1%.

## Where they disagree, and what decides it
- **Size of the real-machine gain.** R2 says single-digit ms for the unit layer, R1 says 40–60 ms off, R3 says about 2×. All three say SwiftShader can't settle it. **Decider:** ship the cache with an in-game toggle. Flipping it mid-match puts both modes in one performance report, so the `gap` column answers it without a 40-minute replay.
- **Animated art.** 34 of 79 art functions pulse or spin, and ~18 have aimed turrets. R1 suggests ~32 aim buckets. R2 suggests live sub-part overlays or 4–8 frame flipbooks. R3 suggests per-art phase bins, or leaving animated arts as vector in v1. **Decider:** a pixel-diff test per art at zoom 1 and .7. Static arts go first. Animated parts become live overlays drawn on top of a baked body.
- **The battle frame.** R3 measured large additive `flash` glow blooms as the battle-time lever: removing 142 flashes took a frame from 85 to 30 ms. Particles and projectiles are second-order. R2 says leave particles alone. **Decider:** a second, later lane. Cap bloom by area, not by count, and batch particle state.

## Ranked shortlist
1. **Unit and enemy sprite cache.** Shadow baked in, animated sub-parts live, bars and rings vector. Plus an Options/HUD toggle and a pixel-diff gate. Blast radius: the unit and enemy loops in `renderGame`, `drawSquadUnit`, `drawSquadCluster`, `drawSwarmCluster`, and a wrapper after `ART`. Sim untouched. Target v4.9.0.
2. **Shadow hygiene** (R1 P2, R2 4.2). Fence `'lighter'` glows from shadow state, and use one flat silhouette shadow instead of per-op blur for anything still drawn live. Small, and needed for the live overlays in item 1.
3. **Measure the rasterizer** (R2 4.3). A calibration probe at match start (1,000 blurred rects plus a forced readback), and the sprite toggle logged per window. This decides how far item 1 went on the real GPU.
4. **Battle frames.** An area budget for additive blooms, batched particle state, and sub-2-px particles as `fillRect`.
5. **One-line A/Bs.** `getContext('2d', { alpha: false })`; a cached vignette overlay.
6. **Later, if the numbers ask for it:** a simulation worker. After item 1 the sim's share rises from ~7% to maybe 15–20%. The designer wants the cores used, so re-measure then.

## Also found, not in the shortlist
- **Unexplained simulation spikes** of 44 ms and 26 ms in a single step during a 2,400-step battle (R1). The averaged report hides them. Worth tracing.
- **`perfCapStep` disturbs the frames it measures.** The worst-moment snapshot re-renders the whole scene inside live frames (R2, R3).
- **BATTLEGROUPS "PERF STOPGAP".** Its premise is stale, since the sim costs ~3 µs per body (R3). But it changes unit behaviour, so it's the designer's call.
- **`SQUAD_LOD_N = 45`.** Infantry already look different at scale while armour never gets an LOD (R3). Sprites make this LOD unnecessary.
- **Safe cleanups.** A per-step `railHubs` filter allocation, a `G.enemies.concat(...)` in `particleHit`, `refreshFriendlies` running with zero enemies, `liveSquadN` recomputed every frame, and a stale `e.deployed` check. All small; take them during item 1's lane only where they touch the same code.

## What all three would NOT touch
The simulation core and the fixed 30 Hz step. The spatial grid. A typed-array or ECS rewrite. Army caps or hidden units (the tenet). The art itself: bake it, don't replace it.

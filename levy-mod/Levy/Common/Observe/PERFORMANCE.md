# Arena view: performance note

Eve's ask: a huge zoomed-out static-camera view (2x to 3x normal) that is not laggy. This note says what is MEASURED (headless, CPU only) and what is ESTIMATED. Frame rate itself cannot be measured headless: nothing here was run on a GPU.

## What the view does (so you know what scales)

`ArenaView` (see the class comment) enlarges the game's virtual screen by the zoom factor `f` per side and shrinks the view matrix by `1/f`. Everything that is sized by the screen then covers `f*f` times the area:

| Cost | Scales with | Notes |
|---|---|---|
| Lighting | area (f^2) | Legacy (Retro) engine is used while zoomed out: it is the only one the draw-to-screen path can use. |
| Tile and wall draw | visible tiles (f^2) | Sprite batches per tile type; GPU fill is the same (the screen is the same size), the CPU work is per visible tile. |
| NPC / projectile draw | entities in the larger area | Projectiles and NPCs are culled to a screen +-800 px; the arena sees more of them. This is the point of the view. |
| Dust / gore | spawn count | Skipped entirely in the arena (config, on by default). |
| Network | sections around the camera | Multiplayer only: 3x3 sections (600x450 tiles) are sent once; NPC and projectile updates for that area then flow. |

## Measured (headless, CPU): lighting engines

Method: `LEVY_OBSERVE_BENCH=1` boot of the Levy mod on the dedicated server. A synthetic 700x260-tile cave world (rolling surface, dirt walls, 14% air pockets, torches) is written into the tile array; for each factor the virtual screen is set to `1920f x 1080f`, the light engine is rebuilt, and `Lighting.LightTiles` is called 20 times (first 5 discarded, median of 15), once per engine. Machine: shared container, CPUs as listed below, other builds running in parallel, so treat the absolute numbers as noisy and the RATIOS as the finding.

4 CPUs. Milliseconds of CPU for one complete lighting update of the visible area. "Retro" is the engine the arena view uses (one update = three consecutive calls, which the game spreads over three frames); "Color" is the vanilla default, shown for comparison (two separate runs, because the machine was shared and noisy). "Tile scan" is only a floor: visit every visible tile, read its flags and its light, draw nothing.

| Zoom per side | Tiles in view | Retro (arena) | Color run 1 / run 2 | Tile scan floor |
|---|---|---|---|---|
| 1.0 (normal) | 8,418 | 0.04 ms | 2.61 / 1.44 ms | 0.05 ms |
| 1.5 | 18,746 | 0.07 ms | 2.07 / 2.21 ms | 0.13 ms |
| 2.0 | 33,154 | 0.14 ms | 8.04 / 7.91 ms | 0.27 ms |
| 2.5 | 51,340 | 0.20 ms | 10.88 / 8.64 ms | 0.38 ms |
| 3.0 | 73,848 | 0.30 ms | 14.61 / 23.82 ms | 0.56 ms |

What this says: the lighting cost grows about linearly with the area (9x tiles, about 7x time for Retro; the Color engine grows a little worse than linear and is about 20 to 50 times more expensive). Retro lighting at 3x per side costs well under a millisecond of CPU, so the lighting is NOT the limit of the arena view; Color lighting at 3x would be (15 to 24 ms, a whole 60 fps frame), which is why the arena uses Retro. Caveats: the scene is synthetic, has no NPC or projectile lights, and the server build has no draw cost; the Retro numbers are so small that they are near the timer's resolution, treat them as "cheap", not as exact.

## Estimated (not measured)

- Tile draw CPU grows with the visible tile count exactly like the tile-scan proxy above; the real draw does several times the work per tile (texture lookup, framing, sprite submit), so expect the arena at 2x to cost about 4x the vanilla tile pass and 3x about 9x.
- At 1920x1080, vanilla draws roughly 8,000 tiles; 2x per side is about 32,000, 3x about 73,000 per layer (walls, tiles, liquids).
- GPU: fill rate is unchanged (same pixels out), but the number of sprites and draw calls grows with f^2. A mid-range GPU should handle 2x; 3x is the one most likely to cost frames on a laptop.

## Mitigations in the build

1. Lighting drops to Retro (white/colour-less per tile light) while zoomed out, and goes back when you leave. Retro is also what makes the wider draw possible at all (no render targets).
2. No dust, no gore while zoomed out (`SkipDustAndGoreInArena`, on by default).
3. Mouse wheel changes the zoom while connected, so a heavy scene can be pulled back in without leaving the camera; the cap is the `ArenaZoomPerSide` config.
4. The enlarged draw area exists only while a static camera is connected; the GoPro and mind-control views are always 1x.

## Recommended defaults

- Default `ArenaZoomPerSide` = 2 (four times the area). It is the setting the numbers above support best.
- 3 is allowed (nine times the area) and is the "100 NPCs fighting" view; it is a config choice, not the default.
- Keep `SkipDustAndGoreInArena` on.
- Do not use 3x on a machine that already struggles at 1x.

## Known limits (unverified on a GPU)

- Lights added by NPCs and projectiles during the update are only accepted inside the real-screen-sized rectangle at the top-left of the arena (tile lights, torches, lava and so on cover the whole arena). Glowing projectiles far from that corner will not light the scene.
- Sky, sun, moon and parallax backgrounds are positioned for the real screen; in the arena they may sit slightly off.
- Mouse-to-world is not corrected for the zoom; mouse aim is not used while a camera is connected.

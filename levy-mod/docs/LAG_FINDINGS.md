# Lag in Eve's 11-17 fights: what scales, what it is not, what changed

Eve's log: `levyperf-2026-11-17.jsonl` (Levy 3.0.8, Calamity Master, about 50 mods, 28-core Windows PC), 1,843 windows of 8 s and 42 fight summaries. The laggy fights were the Nebula Pillar at 16:57 (median 5 fps), the Solar Pillar at 17:00 (3.9 fps), the Plaguebringer at 17:55 (31.8 fps) and the Nebula Pillar at 20:28 (10.9 fps); almost every other fight ran at 59 to 60 fps with the same army. Analysis script: `lab/lagsweep/eve_log.py LOG`. Lab: `lab/lagsweep/` (pillar scene, armies, A/B spec, `crowd.py`). "Laggy" below = focused windows under 20 fps with levies (69 windows); "smooth" = focused, over 50 fps, 20+ levies (1,101 windows).

## The short answer

It is not one or two weapons. In a pillar event every levy is in the fight at once, and an engaged levy costs about 230 microseconds of every game update on Eve's machine (about 150 for the levy body, most of it the per-tick gear frame, and about 80 for its shots). Forty-five of them put the update at 16 to 17 ms. That is the edge of the 16.7 ms a frame has, and with the game's Frame Skip set to On the game answers an update that runs long by keeping every update and dropping the drawing: the frame rate falls off a cliff from 60 to 2 to 5 while the game keeps 58 updates a second. A few levies dying takes the update back under the edge, which is why "when a couple of levies die it runs smooth" is a true observation with the wrong cause. In the lab, taking out the 8 levies that carry the two costliest weapons and taking out 8 levies at random save the same time (20% and 22%).

## 1. What the log shows (OBSERVED)

### The frame rate is a cliff, not a slope

| ms per update | windows | median fps | median updates/s | median draw ms per frame |
|---|---|---|---|---|
| under 8 | 880 | 59.4 | 60.0 | 8.3 |
| 8-10 | 494 | 58.2 | 60.0 | 7.2 |
| 10-12 | 196 | 58.7 | 60.0 | 4.6 |
| 12-14 | 55 | 32.1 | 60.0 | 6.0 |
| 14-15.5 | 20 | 9.4 | 61.0 | 9.6 |
| 15.5-17 | 35 | 3.0 | 59.1 | 13.3 |
| over 17 | 10 | 3.0 | 54.8 | 11.7 |

The updates per second never fall; the frames do. That is the signature of Frame Skip On (a fixed time step): the game runs every update it owes and draws once in the half second the engine allows it to fall behind, so once an update takes 16.7 ms nothing is left for drawing. Decompiled `Main.DoUpdate` (tML 2026.10): with Frame Skip Off or Subtle the time step is variable and the game draws after every update, slowing down instead. Eve's laggy windows had 15.9 ms of update and 12.0 ms of draw on average; with Subtle that is about one update and one frame every 28 ms, **35 fps (median of the 69 windows; the worst 24) at about 0.6 game speed instead of 3 to 5 fps at full speed**. Her log has 58-60 updates a second in every laggy window, so her Frame Skip is On.

### Where an update goes (ms per tick, mean)

| | laggy | smooth | ratio |
|---|---|---|---|
| whole update | 15.89 | 7.66 | 2.1 |
| NPC phase | 8.36 | 3.72 | 2.3 |
| of which levy bodies (SoldierNPC) | 6.61 | 2.41 | 2.7 |
| of which base-game NPCs | 0.62 | 0.52 | 1.2 |
| projectile phase | 4.72 | 1.41 | 3.3 |
| of which army shots | 3.55 | 0.61 | 5.8 |
| other base-game and Calamity shots | 0.43 | 0.47 | 0.9 |
| global AI hooks: Calamity / Levy / NoxusBoss | 0.84 / 0.40 / 0.13 | 0.31 / 0.15 / 0.01 | 2.7 / 2.6 / 20 |
| rest of the update (players, dust, gore, world) | 2.81 | 2.53 | 1.1 |
| draw per frame | 11.98 | 6.60 | 1.8 |
| of which dust and gore | 0.43 | 0.1 | |

### What scaled: more of the same things, not dearer things

| | laggy | smooth |
|---|---|---|
| levies | 44 | 41 |
| NPCs (all) | 122 | 118 |
| hostile NPCs above the calm baseline of 76 town NPCs and drones | median 0, up to 64 | |
| army shots alive | 336 | 88 |
| army shots per levy | 7.7 | 2.2 |
| cost per army shot, us | 10.5 | 7.0 |
| Calamity global hook, us per call | 0.24 | 0.21 |
| dust / gore alive | 3,111 / 390 | 357 / 45 |

The NPC count did not grow: the lag is not the pillar's enemy density as such. The levy count did not grow either. What grew is how much of the army was fighting: 3.8 times the shots in the air, and 5.5 times the gear-frame time ("stats") for the same number of levies.

### Levy's own buckets (ms per tick)

The "lv" numbers in the log were wrong by a factor of 11 in 22 of the 69 laggy windows and 316 of the smooth ones (every window recorded while a fight was being recorded): with the in-fight profiling on, Levy's bucket timers run on every update, and the analyzer divided their total by the sampled updates only (one in eleven). Fixed on this branch (`PerfAnalyzer.End`) with a world check. Corrected:

| bucket | laggy | smooth | in-fight smooth (recorded) | ratio laggy/smooth |
|---|---|---|---|---|
| ai (all levy AI) | 5.91 | 1.82 | 2.22 | 3.2 |
| stats (the gear frame) | 3.93 | 0.71 | 0.85 | 5.5 |
| g.reset (ResetEffects) | 1.03 | 0.15 | | 6.8 |
| g.misc (PostUpdateMiscEffects) | 1.00 | 0.18 | | 5.6 |
| g.equips | 0.70 | 0.11 | | 6.3 |
| dmg (incoming hits, debuffs) | 0.95 | 0.58 | | 1.6 |

**Is "stats/g.*" in the bad windows gear dormancy waking for every levy every tick?** Yes. A levy runs the gear frame (a player's ResetEffects, UpdateEquips, armor sets, every mod's PostUpdateEquips and PostUpdateMiscEffects) every tick unless it has been calm for 6 s with no enemy within 110 tiles. In her boss fights a big part of the army stood back (the battery measured 57% of levies not engaged at the Empress), so the frame ran for about ten levies' worth: 0.85 ms. In a pillar event the enemies come from everywhere and every levy is engaged: 3.9 ms, about 89 us per levy, the same per-levy cost the Destroyer lab measured (80 us). Nothing woke wrongly; the whole army was legitimately awake.

### Projectile types

| type (weapon) | ms/tick in laggy windows | shots alive | us per shot | us per shot in smooth windows |
|---|---|---|---|---|
| Chlorophyte Bullet (Realm Ravager) | 0.40 | 23 | 17.1 | 15.5 |
| Dreadmine (Dreadmine Staff) | 0.28 | 56 | 5.0 | 5.2 |
| Sprouting Arrow split (Malevolence, Vernal Bolter) | 0.26 | 11 | 22.5 | 21.4 |
| Astral Probe round (Starspawn Helix Staff) | 0.08 | 5 | 17.0 | |
| Cosmic Tentacle (Apathanull) | 0.07 | 2 | 43.9 | |
| Nebula Arcanum subshot | 0.04 | 6 | 5.7 | |
| Plaguenade bee | 0.03 | 4 | 6.1 | |
| Pulse Turret shot | 0.02 | 1 | 30.7 | |

Every flagged type costs the same per shot in the laggy windows as in the smooth ones; there are just more of them. The biggest, all three together, is about 0.9 ms of the 15.9 ms update. (The log keeps the three costliest types per window, so this is a lower bound on how spread the cost is.)

### Least squares over all 1,690 focused windows with levies

ms per update = 3.0 + 0.045 per army shot alive + 0.015 per other NPC + 0.004 per levy + 0.09 per 1,000 dust (R^2 0.71). The per-shot coefficient (45 us, four times what a shot costs itself) carries the engaged levies with it: shots in the air go up exactly when levies are engaged, and an engaged levy costs its gear frame. Dust and gore barely move the update.

## 2. The lab (OBSERVED)

Scene: `boss solarp` / `nebulap` (new, `CombatHarness.PillarLab.cs`): the pillar with its shield held, the Lunar Events on, the commander in the pillar's zone, and the pillar's own enemies set down at random spots of the a3 arena whenever fewer than 30 are alive (400 were set down in 40 s). Army: Eve's 34 recorded levies plus copies of the first 11 (`eve45.json`, 45 levies, her pillar fights had 44-46). Calamity, Master + Death, one tML server, CPU ms per tick of the NPC and projectile phases ([Crowd] census, LEVY_CROWD=1), first 6 s skipped, 40 s per fight. The box was shared (load average 20 to 33 on 4 cores), so compare arms of one run, not absolute numbers.

| arm | fights | CPU ms/tick (NPCs + projectiles) | per fight | levies alive | enemies | army shots | army damage/s |
|---|---|---|---|---|---|---|---|
| Solar Pillar, her army | 3 | 12.97 (7.62 + 4.52) | 13.3, 12.6, 13.0 | 39 | 75 | 301 | 40,293 |
| without the top two weapons (Apathanull, the Sprouting Arrow bows: 8 levies) | 3 | 10.34 (6.17 + 3.38) | 10.3, 10.0, 11.2 | 34 | 78 | 271 | 38,601 |
| 8 levies out at random | 3 | 10.08 (6.00 + 3.23) | 10.0, 10.1, 11.6 | 30 | 76 | 154 | 36,468 |
| Nebula Pillar, her army | 1 | 11.05 (5.93 + 4.33) | | 40 | 13 | 321 | 23,035 |
| Plaguebringer (life x4), her army | 1 | 11.64 (6.84 + 4.05) | | 43 | 1 | 311 | 7,467 |

The lab reproduces the shape of her log: 300 army shots in the air, the NPC phase (the levies) the bigger half. **The hypothesis test:** removing the levies that carry the two weapons whose shots cost the most in this scene (Cosmic Tentacle 1.2 ms/tick, 94 us a shot; Sprouting Arrow split 0.8 ms/tick) saves 2.6 ms; removing the same number of levies at random saves 2.9 ms, although the random draw happened to take out more of the shot-heavy levies (154 shots alive against 271). The cost goes with the number of engaged levies, not with which weapons they hold.

Sampling profile of the same scene (EventPipe, `lab/perf/profile.sh`; the harness's own per-shot immunity census, 11% of the tick, is lab-only and is now off in pillar scenes): levy AI 46% of the update, of which the gear frame is 31% (Calamity's ResetEffects 7%, UpdateEquips 4%, PostUpdateMiscEffects and the other mod hooks 8%, and inside ResetEffects a `Main.projectile.Any(...)` over all 1,000 slots that Calamity runs for every player, 3%), movement 6%, incoming damage and debuffs 5% (a full player ResetEffects on a scratch player for every debuffed levy, 4%), combat 2%, dodge 4%; army shot updates about 20%. LEVY_PROF buckets (ms per tick, the timers inflate them): stats 7.5 of the levy's 13.5, g.misc 2.6, g.reset 1.7, g.equips 1.3, g.sets 0.8.

## 3. Per-weapon cost (army sweep)

SWEEP_TABLE

## 4. What changed on this branch

FIXES_TABLE

## 5. Checks added

CHECKS_TABLE

## 6. INFERRED, not measured here

The draw side: a headless server cannot draw, so the Frame Skip estimate (35 fps at 0.6 speed) is arithmetic on her own update and draw times, and the effect of thinning dust and gore under pressure is bounded by her log (dust and gore drawing was 0.43 ms of a 12 ms frame). Her real machine is a little slower per update than this box's CPU time (her 45 levies cost 10 ms against the lab's 13 ms CPU on a box at load 30, so not comparable absolutely).

Not done, and why: the gear frame is a real player's equipment pass for every levy, and most of its time is Calamity's own per-player code (three whole-world loops among it: the `Main.projectile.Any` in ResetEffects, the proximity-rage loop over every NPC in UpdateRippers, the buff loops in OtherBuffEffects). Running it less often for engaged levies would change what the gear does (rogue stealth, cooldowns, Rage), which is a nerf, so it stays every tick. Replacing Calamity's `Any` with an equivalent loop through an IL edit would save about 3% of the tick; it is a patch into another mod's code and was left as an option for Eve to decide.

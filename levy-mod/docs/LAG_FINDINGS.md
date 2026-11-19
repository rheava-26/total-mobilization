# Lag in Eve's 11-17 fights: what scales, what it is not, what changed

Eve's log: `levyperf-2026-11-17.jsonl` (Levy 3.0.8, Calamity Master, about 50 mods, 28-core Windows PC), 1,843 windows of 8 s and 42 fight summaries. The laggy fights were the Nebula Pillar at 16:57 (median 5 fps), the Solar Pillar at 17:00 (3.9 fps), the Plaguebringer at 17:55 (31.8 fps) and the Nebula Pillar at 20:28 (10.9 fps); almost every other fight ran at 59 to 60 fps with the same army. Analysis script: `lab/lagsweep/eve_log.py LOG`. Lab: `lab/lagsweep/` (pillar scene, armies, A/B spec, `crowd.py`). "Laggy" below = focused windows under 20 fps with levies (69 windows); "smooth" = focused, over 50 fps, 20+ levies (1,101 windows).

## The short answer

It is not one or two weapons. In a pillar event every levy is in the fight at once, and an engaged levy costs about 230 microseconds of every game update on Eve's machine (about 150 for the levy body, most of it the per-tick gear frame, and about 80 for its shots). Forty-five of them put the update at 16 to 17 ms. That is the edge of the 16.7 ms a frame has, and with the game's Frame Skip set to On the game answers an update that runs long by keeping every update and dropping the drawing: the frame rate falls off a cliff from 60 to 2 to 5 while the game keeps 58 updates a second. A few levies dying takes the update back under the edge, which is why "when a couple of levies die it runs smooth" is a true observation with the wrong cause. In the lab, taking out the 8 levies that carry the two costliest weapons and taking out 8 levies at random save the same time (20% and 22%).

What changed: every levy at the army's stations was running a whole player pass every tick because the game flags Happy! and Cozy Fire as debuffs (39 to 56 us a levy a tick with Calamity, about 2 ms a tick for 40 levies; fixed, section 2b); the lag popup now recognises that pattern and offers to switch Frame Skip to Subtle (her own numbers give about 35 fps at 0.6 game speed instead of 3 to 5 fps), the log's Levy bucket numbers are no longer eleven times too high during recorded fights, and the army's own dust and gore are thinned while the screen is already full of them. The per-levy cost of an engaged army is unchanged: most of it is a real player's equipment pass per levy, mostly Calamity's own per-player code, and running it less often would change what the gear does.

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

## 2b. Calamity and the puppet player (gear-cost lab, 11-18)

Eve: "My last playthrough was mostly Mechanization + QoL and I got to 40 levies no problem with no lag. It's nigh unplayable now." The lab agrees, and says where. Probe: `LEVY_GEARCOST=1` (`CombatHarness.GearCost.cs`) instruments every ModPlayer hook of every loaded mod, every GlobalItem / GlobalBuff hook and every ModItem / ModBuff UpdateAccessory, UpdateEquip, UpdateArmorSet and Update override, every method of CalamityPlayer, and Levy's own frame steps, and reports microseconds per levy per tick (inclusive and exclusive, wall time; the probe adds a little to every wrapped call). Scene: the Solar Pillar, her first 40 levies, 30 s, 2 seeds per arm. Armies: `eve40.json` (her gear) and `eve40v.json` (the same 40 with every Calamity item swapped for a vanilla one of the same job), `lab/lagsweep/mkarmy.py gearcost`, specs `mkgc.py`, table `gearcost.py`.

| microseconds per levy per tick | no Calamity, vanilla gear | Calamity loaded, the same vanilla gear | Calamity loaded, her own gear |
|---|---|---|---|
| the levy's whole AI (inclusive) | 34.9 | 129 (79 after the fix below) | 265 (325 in the second run) |
| gear frame, every tick (a player's equipment pass) | not run: vanilla gear needs no per-tick frame | not run | 169 to 238 |
| damage-over-time step (DebuffDot) | 6.6 | 56 | 39 |
| Calamity's Rage and Adrenaline (UpdateRippers, from Levy's Rippers_Tick) | - | 11 to 12 | 10 to 16 |

So with the same gear, Calamity makes a levy 2 to 4 times dearer, and with Calamity gear 8 to 9 times: every levy in modded armor or accessories runs the per-tick gear frame (that is what makes Calamity gear work: rogue stealth, cooldowns, Rage, defense damage), and the frame is Calamity's per-player code. Inside the frame, per levy per tick (exclusive, her gear, after the fix):

| hook | us | what it is |
|---|---|---|
| CalamityPlayer.ResetEffects | 28.6 | includes `Main.projectile.Any(...)` over all 1,000 slots (Virid Vanguard), every player every tick |
| Player.UpdateEquips (vanilla, with tML's item dispatch) | 27.7 | every equipped item's UpdateEquip / UpdateAccessory, Calamity's included |
| Player.ResetEffects (vanilla part) | 19.9 | |
| CalamityPlayer.MiscEffects / PostUpdateMiscEffects (own) | 13.5 / 12.7 | |
| CalamityPlayer.UpdateEquips | 11.7 | |
| CalamityPlayer.OtherBuffEffects | 11.7 | walks every active NPC or projectile |
| armor sets (vanilla 6.6 + Calamity UpdateArmorSet 5.2) | 11.8 | |
| CalamityPlayer.AndroombaRightClick | 4.3 | a right-click helper for the local player: walks every NPC looking for an Androomba |
| ForceVariousEffects, HandleTileEffects, OldDukeScales, UpdateDrawingParameters, AbyssEffects | 3.7, 2.5, 1.9, 2.0, 1.1 | |

**The one that was Levy's own: Happy! and Cozy Fire count as debuffs.** The probe also logs which debuffs the levies carry: in every census, every levy carried "Sunflower" (Happy!, from the army's Sunflower station). The game flags Happy!, Cozy Fire, Heart Lamp, Star in a Bottle, the candles and the banner buff as debuffs only so they cannot be clicked off; Levy's damage-over-time step treated each as a reason to run a whole player's ResetEffects, buff and regeneration pass on a scratch player, every tick, for every levy, to take nothing. 6.6 us a levy without Calamity, 39 to 56 with it (Calamity's ResetEffects is in that pass): about 2 ms a tick for 40 levies in the profiled lab. Fixed: with only those base-game buffs on the body, the step is the no-debuff case (any other or modded debuff still runs it; `LEVY_DOTHARMLESS=0` for A/B). The ProjParity world check proves the skipped pass does nothing (with all ten buffs on, the full step takes no life and no defense over 240 ticks) and that it is skipped. Measured: the step falls from 39-56 to 0.1-0.4 us per levy per tick.

**Fight outcomes in this lab depend on the box's load, not only on the code.** A first A/B of the skip looked as if it changed the fights (24 to 40 of 40 levies dead in 30 s against 1 to 8), and a six-variant bisect seemed to pin it on constructing the scratch Player. Repeating the A/B later on the same build put the old code at 34 to 36 deaths too, and the new at 33 to 40: the same seeds give 3 or 36 deaths depending on when they run. The reason is in the army's own code: a levy's target search is charged against a per-tick wall-clock account (`SoldierNPC.ChargeSearch`, searches deferred once it is spent), so on a box loaded by other servers levies find targets later and take more hits. Deaths and damage per second are therefore not comparable across runs here; the skip's exactness rests on the world check (the skipped pass takes no life and no defense) and on the code (the skipped branch is the existing no-debuff branch). CPU per tick, same build, skip on / off / on again: 6.99 / 6.88 / 7.05 ms (vanilla-swapped gear) and 9.14 / 9.34 / 6.86 ms (her gear), within this box's drift; the per-levy saving is the probe's 39-56 us, about 2 ms a tick at 40 levies on the profiled server, roughly 1 ms on Eve's machine by the ratio of her stats bucket to the lab's. A wall-clock budget inside the AI also means a slower machine plays a little differently from a fast one; worth knowing when reading any lab outcome.

**Can Levy skip or cache the rest safely?** Only a little. AndroombaRightClick is a pure no-op for a levy (it acts only when the local player holds a clentaminator solution over an Androomba and right-clicks), UpdateDrawingParameters and AbyssEffects are drawing and lighting: together about 7 us a levy a tick, 0.3 ms for 40 levies, and each skip is a detour into Calamity's private methods; not done here, listed for Eve to decide. Everything big (ResetEffects, UpdateEquips, MiscEffects, OtherBuffEffects, armor sets, Rage) carries stats or mechanics that work across ticks; running it less often for an engaged levy would change what the gear does, which is the nerf the brief rules out. Dormancy already runs it every 8th tick for a calm levy (and the stat pass every 120); in a pillar event no levy is calm. Calamity's whole-world walks inside it (the 1,000-slot `Any` in ResetEffects, the NPC walks in OtherBuffEffects, UpdateRippers and AndroombaRightClick) are where an O(levies x entities) cost lives, and they are Calamity's code: the cheapest real fix is upstream (one cached answer per tick instead of one walk per player), which a Levy-side IL patch could imitate exactly for the `Any` (about 3% of the tick) if Eve wants Levy to patch Calamity.

## 2c. Would one coordinating AI for the whole army be cheaper?

Measured answer: not for this lag. The work a coordinator could share is already small or already shared. Levy's dodge reads one hostile-projectile snapshot per tick (`ThreatField.Snapshot`, built once and queried by every levy through `Near`); target candidates come from per-tick enemy tables. In the profiled pillar lab (42 levies, ms per tick, timers on): building the danger snapshot 0.11, every levy's nearby-threat query 0.04, hit tests 0.03, dodge 0.28, brain 0.30, combat 0.82, movement 0.93, against 7.5 for the gear frames and 7.7 for the army's shots. The sampling profile agrees: dodge 3.8% of the update, movement 5.9%, against the gear frame's 31%. Merging attack and movement AIs into one army brain would trade a few hundred microseconds for a large rewrite, and the gear frame stays per levy because each levy is a player with its own gear; it is per-levy by nature, not a scan.

Where Eve's idea is right: the remaining O(levies x things) costs are inside other mods' per-player code (Calamity's proximity Rage walk over every NPC, 11 to 16 us per levy per tick; the projectile `Any`), and those are exactly "compute once per tick, let every levy read it" problems. They can only be shared by reimplementing or patching Calamity's logic. For dodging quality, the shared danger field already exists; making levies dodge better is a design change to how they read it, not a performance one.

## 3. Per-weapon cost (army sweep)

The army sweep (`IdentityWorldCheck.ArmySweep.cs`) now records, for every weapon, the cost of its run: wall ms per tick inside the updates of the shots (every friendly projectile: shots, minions, their bolts), ms per tick of the levy body's own update (its AI and gear frame), shots alive (mean and peak), Dust.NewDust and Gore.NewGore calls per second (a server makes neither, so these are the calls a client would have made), and the cost per shot for the levy and for a real player firing the same weapon at the same dummy. One levy, the boss dummy 12 tiles away, 600 ticks, Calamity, the same boot for every weapon (`LEVY_ARMYSWEEP=1 LEVY_ARMYSWEEP_ALONE=1 LEVY_ARMYSWEEP_ONLY=$(python3 lab/lagsweep/sweeplist.py)`, `Weapon@Ammo` picks the ammo). Her army's weapons plus every pair her log flagged, 58 weapon runs (Cosmolight is a utility item); ranked by the levy's whole cost per tick (excerpt; the full table is in the run log, `army cost` lines):

| weapon | levy ms/tick (shots + body) | shots alive mean/peak | dust/s | gore/s | us per shot, levy / player | player's shots ms/tick |
|---|---|---|---|---|---|---|
| Plaguenade | 1.84 (1.67 + 0.17) | 189 / 258 | 275 | 0 | 8.8 / 6.8 | 1.68 |
| Terra Blade | 0.78 (0.44 + 0.34) | 1.7 / 2 | 509 | 0 | 262 / 82 | 0.25 |
| Apathanull | 0.73 (0.57 + 0.16) | 5.3 / 6 | 636 | 0 | 107 / 55 | 0.31 |
| System Bane | 0.57 (0.40 + 0.16) | 28 / 56 | 152 | 0 | 14 / 14 | 0.44 |
| The Syringe | 0.51 (0.37 + 0.14) | 14 / 18 | 251 | 0 | 26 / 9 | 0.14 |
| Radiant Star | 0.47 (0.32 + 0.15) | 17 / 29 | 574 | 13 | 19 / 12 | 0.20 |
| Shard of Antumbra | 0.45 (0.32 + 0.13) | 7 / 9 | 251 | 0 | 44 / 34 | 0.25 |
| Havoc's Breath | 0.41 (0.27 + 0.13) | 9 / 16 | 281 | 0 | 30 / 52 | 0.52 |
| Dreadmine Staff | 0.25 (0.16 + 0.10) | 22 / 26 | 255 | 0.6 | 7 / 7 | 0.17 |
| Vernal Bolter @ Sprouting Arrow | 0.27 (0.14 + 0.13) | 2.8 / 6 | 540 | 0 | 50 / 113 | 0.09 |
| Malevolence @ Sprouting Arrow | 0.27 (0.10 + 0.16) | 2.4 / 6 | 439 | 0 | 44 / 106 | 0.06 |
| Realm Ravager @ Chlorophyte Bullet | 0.21 (0.08 + 0.13) | 1.5 / 4 | 1,127 | 0 | 53 / 65 | 0.05 |
| Nebula Arcanum | 0.25 (0.12 + 0.14) | 3.3 / 8 | 654 | 3.4 | 36 / 21 | 0.19 |
| Starspawn Helix Staff | 0.16 (0.07 + 0.09) | 1.0 / 2 | 18 | 0 | 69 / 83 | 0.08 |
| Pulse Turret Remote | 0.13 (0.03 + 0.11) | 0.5 / 2 | 11 | 0 | 51 / 98 | 0.10 |
| median of the 58 runs | about 0.22 | | | | | |

What it says. One weapon stands out on its own: **Plaguenade**, 1.8 ms a tick for one levy, eight times the median, because one throw makes a swarm of bees that live for seconds (189 alive on average). A player throwing it costs the same (1.68 ms), so it is the weapon, not the levy layer; a levy is a player, and the sweep confirms parity. Several weapons cost a levy two to three times what they cost a player per shot (Terra Blade 262 against 82 us, Apathanull 107 against 55, The Syringe 26 against 9): the held-swing and cursor puppets around a levy's shot. Those are real Levy overheads, but on one or two shots alive they are a fraction of a millisecond.

Rolled up over her army (count of each primary weapon in `eve45.json` times its single-levy cost): Terra Blade 3.9 (5 levies), Apathanull 3.6 (5), Ark of the Ancients 2.5 (6), The Syringe 1.0, Wingman 1.0, Dark Harvest 0.9, everything else under 0.7, total about 17 ms of single-levy cost on this box. The top two weapons are 44% of it because they are carried by ten levies, not because each is unusual; the scene test above (8 levies out by weapon or at random: the same saving) is the direct answer. Two notes on the table: the sweep list has Malevolence twice and the second Sniper Rifle pair took the first one's ammo (a dictionary keyed by weapon name), so Sniper Rifle @ Musket Ball was not measured separately.

**Her hypothesis, answered.** "One or two weapons cause the lag" is not what the data shows. No projectile type was more than 0.4 ms of a 15.9 ms laggy update in her log, every flagged type cost the same per shot in smooth and laggy windows, and in the lab taking out the levies carrying the two costliest weapons saved the same as taking out the same number of random levies. The one weapon that is individually heavy is Plaguenade (1.8 ms per levy carrying it), and it was in her log as a minority (PlaguenadeBee up to 0.9 ms in one window). What makes "a couple of levies die and it is smooth" true is the cliff: at 16 to 17 ms per update with Frame Skip On, removing a millisecond or two takes the game from 3 fps back to 30.

## 4. What changed on this branch

None of these touches damage, hits, projectiles, targets or what a levy does.

**The damage-over-time step skips station and candle buffs (the update-side saving).** Happy!, Cozy Fire, Heart Lamp, Star in a Bottle, the Peace, Water and Shadow Candles, the banner buff and the two sickness timers are flagged as debuffs by the game but never take life or defense; with only those on a levy the step is the no-debuff case. 39 to 56 us per levy per tick with Calamity (6.6 without), so about 2 ms a tick for 40 levies in the profiled lab (section 2b).

**Frame Skip advice (the change that moves her frame rate).** The analyzer now logs the game's Frame Skip setting in every window (`"fs"` in the context, -1 in old lines). When a laggy fight shows the Frame Skip On pattern (laggy windows mostly with Frame Skip On, median under 20 fps with 50 or more updates a second), the verdict adds two lines ("Frame Skip is On: when an update runs long, the game drops frames to keep full speed." / "Set to Subtle, the game slows down instead and keeps drawing.") and the popup offers **Set Frame Skip to Subtle** first, which sets the game's own option and saves the settings. Her setting is her choice: nothing changes unless she presses it. On her own numbers (15.9 ms update, 12.0 ms draw in the laggy windows) that is about 35 fps at 0.6 game speed instead of 3 to 5 fps at full speed (INFERRED from the decompiled loop and her times; a headless server cannot draw).

**Levy's bucket numbers in the log are per update again.** With a recorded fight's profiling (or /levyprof) on, the "lv" buckets were 11 times too high (`PerfAnalyzer.End` counted only sampled updates). This is why the log said "stats 37 ms" inside 17 ms updates.

**Army dust and gore thinned on a full screen (cosmetic).** While 2,500 dust or 400 gore are already alive, two of every three dust particles and one of every two gore pieces made inside a levy's update or an army shot's update are dropped (the game's own "no dust / no gore" answer). Outside that, nothing is dropped; "Lower levy effects" still thins army dust always. Her laggy windows held 3,111 dust and 390 gore on average (5,800 and the 600 cap at the peak) against 357 and 45 in smooth ones; dust and gore drawing was 0.43 ms of the 12 ms frame, so this is a small, safe saving on the draw, not the fix.

**Gear frame: the owned-projectile counts.** After ResetEffects (which already zeroes the array) the frame cleared the whole per-type array again, thousands of entries per levy per tick; it now clears only entries still set. Same state, written the same way (the values are restored from the same saved list).

**Lab only:** the pillar scene, Eve's army at 45, the per-weapon cost columns, the A/B spec and table script, and the per-shot immunity census switched off in pillar scenes (it cost 11% of a lab tick).

### Before and after, same scene (Solar Pillar, her 45 levies, 3 seeds each, CPU ms per tick)

| build | CPU ms/tick (NPCs + projectiles) | per fight | army shots | army damage/s |
|---|---|---|---|---|
| before (this branch with the gear frame change reverted) | 14.86 (8.77 + 5.07) | 14.9, 15.2, 14.7 | 324 | 40,114 |
| after | 14.28 (8.45 + 4.83) | 14.3, 13.9, 14.4 | 309 | 38,980 |
| before again, right after | 12.80 (7.66 + 4.27) | 13.9, 12.7, 12.8 | 280 | 37,426 |

The server-side change is inside the box's drift (two runs of the same "before" build differ by 2 ms on a box at load 20-30): the gear frame's count copy was 0.11 ms a tick in the profiled lab, so no server-side saving was expected, and none is claimed. Damage per second is the same within seed noise (38,980 against 40,114 and 37,426); the changes cannot alter it by construction (the gear frame writes the same values, the rest is client-side drawing and a setting). The update-side cost of an engaged army is unchanged; what changes for Eve is how the game spends a slow update (the Frame Skip choice) and how much dust and gore it draws on a full screen.

## 5. Checks added

- `ProjParity` world check: with Happy!, Cozy Fire, Heart Lamp, Star in a Bottle, the candles, the banner buff and both sicknesses on a levy, the full damage-over-time step takes no life and no defense over 240 ticks, and with the skip the step does not run.
- `[PerfAnalyzer]` world check: a test NPC burns 0.3 ms an update inside Levy's "ai" bucket; the window must read half to twice the time the busy-wait really took with the profiler off **and with the player's profiler on** (before the fix the second read about 3.3).
- `[PerfAnalyzer]` world check: with the screen full (pressure forced), two of three army dust particles and one of two army gore pieces are dropped, nothing outside an army update, and nothing when the screen is not full.
- `PerfSelfTest` (boot): Frame Skip On with 3 fps and 58 updates offers Subtle first; Subtle, Off, unknown, or a slow-update fight (30 fps, 40 updates) do not; the lines stay short; the log keeps the setting and an old line reads as unknown. The option-count guard now expects six options.
- `[PerfBoss]` world check, **the culprit path bounded**: a levy's stat pass (the gear frame) timed in thread CPU on the standing army with 0 and with 900 shots alive, interleaved, best of three: the pass with 900 alive must cost under 3 times the pass with none. An engaged levy runs this pass every tick, so a walk over every shot added to it (or to a mod hook it runs) would cost levies x shots; this fails it.
- Army sweep: the cost columns ride on every sweep run (`army cost` lines, ranked).

## 6. INFERRED, not measured here

The draw side: a headless server cannot draw, so the Frame Skip estimate (35 fps at 0.6 speed) is arithmetic on her own update and draw times, and the effect of thinning dust and gore under pressure is bounded by her log (dust and gore drawing was 0.43 ms of a 12 ms frame). Absolute numbers do not compare between her machine and the lab: her 44 engaged levies cost about 10 ms of her update (levy bodies 6.6, army shots 3.6), the lab's whole NPC and projectile phases were 10 to 15 ms of CPU on a box shared with other servers.

Not done, and why: the gear frame is a real player's equipment pass for every levy, and most of its time is Calamity's own per-player code (three whole-world loops among it: the `Main.projectile.Any` in ResetEffects, the proximity-rage loop over every NPC in UpdateRippers, the buff loops in OtherBuffEffects). Running it less often for engaged levies would change what the gear does (rogue stealth, cooldowns, Rage), which is a nerf, so it stays every tick. Replacing Calamity's `Any` with an equivalent loop through an IL edit would save about 3% of the tick; it is a patch into another mod's code and was left as an option for Eve to decide.

## 7. Caching the engaged gear frame (lag2, 11-18): built, proven exact, not worth turning on

The question: an engaged levy runs the whole gear frame every tick; how much of it recomputes the same thing, and can that part be served from a cache without changing anything a levy does? Lab: her army (`lagsweep/eve40.json`) against the Plaguebringer Goliath, and the Solar Pillar scene (`eve45.json`); Calamity, Master + Death, one server.

**What the frame changes, tick to tick** (probe `LEVY_GCPROBE=1`, `Common/Testing/GearCacheProbe.cs`: every field of the dummy Player, its 66 ModPlayers, the worn items with their ModItem and globals, and the random generator, imaged at six points of every frame; 10 levies x 1,500 consecutive frames). The frame has two halves:

- The **build** (ResetEffects of the game and every mod, the buff pass, UpdateEquips with every item's hooks, armor sets, station and potion stats) wipes the stats and rebuilds them: 62% of the frame (63-72 of 105-117 us). From one frame to the next it changes about 100 of the dummy's ~6,000 leaves; everything else is rebuilt to the value it already had.
- The **tail** (PostUpdateEquips, Calamity's PostUpdateMiscEffects) evolves something every tick for every levy: Rage, Adrenaline, `persecutedEnchantSummonTimer`, the stealth meter, the TimeHolding counters. It has to run every tick; nothing here touches it.

Fields that change between one frame's start and the next (share of 14,990 consecutive frames): the random generator 100%; Calamity's Virid Vanguard rotation 100% (stepped by Calamity's ResetEffects for every player; cosmetic); Adrenaline 98%, stealth UI 90%, Rage 82% (tail); position 78%, velocity 62%; Levy's regeneration clock 25%; Beetle orbs 20-30% and the Yoyo Bag's counterweight 17% (vanilla build code, drawing random numbers); mana 13%; `miscCounter` 13% (set by the held-weapon and minion puppets); owned-projectile counts 10%; life 4%; wing time, controls and direction 1-2%.

**Which of those the build reads** (`LEVY_GEARCACHE_ILSCAN=1`, `Common/Testing/GearCacheReadScan.cs`: the IL of 747 build entry points and the 2,614 methods they call, three calls deep). Read: position (the game's ResetEffects, ApplyEquipFunctional, UpdateArmorSets, UpdateBuffs; Victide armor), velocity (ApplyEquipFunctional, UpdateArmorSets, Calamity's GlobalItem.UpdateAccessory, the wings), direction, `miscCounter` (ApplyEquipFunctional, UpdateArmorSets, Trinket of Chi and five more accessories), mana (Calamity's PreUpdateBuffs), life, controlJump, wing time, itemAnimation, the inventory, owned-projectile counts, lifeRegen, Calamity's cooldowns, lifeRegenTime (a debuff), Rage (the Rage Mode buff). Not read: rogue stealth, Adrenaline, TimeHolding, the tail's timers, the arm and item animation, controlLeft / controlRight, oldPosition / oldVelocity. The game's equip methods are giant per-item switches, so "which item reads position" has no answer from the IL: for an exact cache, motion is an input of every build.

**The cache** (`Content/NPCs/SoldierNPC.GearCache.cs`, `Common/Util/StateImage.cs`): where the build would start, the live dummy is compared with the image taken where the last full build started (raw byte ranges for plain fields, compiled reads for references, collections followed). Equal outside the fields the build provably does not read, and with the same key (body buffs, stations, set state, world flags, boss alive), the image of what that build produced is written back and the tail runs as usual. The build's own per-tick counters (Calamity's three Virid Vanguard lines) are stepped by the same statements. A build that drew random numbers, spawned a projectile or wrote a field the cache keeps is never reused.

**Parity** (`LEVY_GEARCACHE=1 LEVY_GEARCACHE_PARITY=1`, `SoldierNPC.GearCacheParity.cs`): on every tick the cache serves, the full build also runs from the same state (dummy, ModPlayers, items, random generator, the levy's build outputs, a digest of every NPC and projectile) and the two results are compared leaf by leaf; the full result stays. Plaguebringer, her 40, 3,000 ticks: **2,873 cached ticks checked, 0 divergences**. The first run caught a real error (Calamity's Virid Vanguard cooldown is a float and the stepper read it as an int: 2 leaves off on every check), fixed before the count above.

**The result** (same boot, harness token `gc=0` / `gc=1`, `HARNESS-GCH` lines):

| scene | arm | frames served from the cache | us per frame | CPU ms per tick (NPCs + projectiles) |
|---|---|---|---|---|
| Solar Pillar, 45 levies, 2 seeds x 1,800 ticks | full frame | - | 105 | 15.15 (7.41 + 6.88) |
| | exact cache | 1.1-1.3% | 200-225 (a served frame: 90) | 19.31 (11.87 + 6.52) |
| Plaguebringer, her 40, 3,000 ticks | exact cache | 2.4% | | |
| Plaguebringer, her 40, 600 ticks | upper bound, not exact: motion, life, mana, controls, wing time, `miscCounter`, inventory left out | 35.8% | 200 (a served frame: 81) against 114 | |

It cannot pay for itself. An engaged levy moves on three ticks in four and the build reads its motion; a quarter of her army's builds draw random numbers every tick (Beetle armor, Yoyo Bag, the Chlorophyte and Plaguebringer sets); and checking ~300 objects (about 30 KB) per levy costs ~25 us a tick and recording a new reference after a miss ~50 us, against a 63-72 us build. Even a served frame still pays the tail (~40 us). So the cache stays **off** (`LEVY_GEARCACHE=1` turns it on for a lab) and nothing a levy does changes on this branch.

Where the engaged frame's cost is, then: the build is the game's and Calamity's own per-player code, run for every levy because every levy is a player; the tail is Calamity's per-tick player step. Within "same behaviour, no patches into Calamity" the frame is irreducible. The two Calamity patches (the visual-only hooks, about 0.3 ms at 40 levies; the projectile `Any` in ResetEffects, about 3% of the tick: section 2b) were not done and not re-measured here.

## 8. The approved levers (lag3, 11-18)

Eve approved small accuracy trade-offs ("I'll take the downgrade a little bit… anything to not have 3 frames") and the two Calamity patches. Lab: the Solar Pillar scene, `eve45.json` (her army, 45 levies), `pw=45`, Calamity, Master + Death, 1,800 ticks a fight, one tML server, same boot for every arm. Specs `lab/lagsweep/l3_*.tsv` (harness tokens `gi=N` gear interval, `gl=N` learning interval, `cp=0` Calamity patches bypassed). CPU and microseconds per gear frame come from the harness's own timers (`HARNESS-GA`); "us/frame" is the whole gear frame per levy per tick.

Before and after, same boot, 3 seeds per arm (`l3_ab.tsv`):

| arm | gear frame, us per levy per tick | of which the build | frames served | CPU ms/tick (NPCs + projectiles) | army damage/s | levy deaths |
|---|---|---|---|---|---|---|
| before (patches off, interval 1) | 95.0 (100.5, 98.4, 86.2) | 57.5 | - | 14.70 | 42,823 | 5 |
| Calamity patches | 75.5 (72.2, 72.9, 81.4) | 41.0 | - | 13.98 | 40,511 | 2 |
| patches + interval 3 | 78.0 (82.3, 76.4, 75.2) | 48.3 | 43% | 13.99 | 39,964 | 3 |
| **patches + interval 4 (default)** | **70.3** (70.5, 69.7, 70.8) | 48.2 | 48% | 13.75 | 42,683 | 3 |
| patches + interval 6 | 68.4 (67.8, 68.7, 68.8) | 50.5 | 54% | 13.32 | 40,326 | 5 |

About 43 levies were alive on average, so per tick: **the Calamity patches save 0.84 ms, interval 4 another 0.22 ms: 1.06 ms of gear frame a tick** (the CPU census moved 14.70 → 13.75 ms, inside this box's drift but the same way). Damage per second and deaths are the same within seed noise (deaths in this lab follow the box's load: section 2b). An earlier run (`l3_swapprof.tsv`, profilers on) gave the same order: before 101 us, patches 84, interval 4 75, interval 6 73.

Estimated frame rate with Frame Skip On, from section 1 (her laggy windows: 15.9 ms of update, of which levy bodies 6.6 ms; the lab's levy bodies are 7.2 ms of 14.7, so the saving scales to about 0.95 ms on her machine): about 15.0 ms an update, which is still in the 14-15.5 ms band (median 9.4 fps) instead of the 15.5-17 band (3 fps). It does not reach the 12 ms edge where the game draws at full rate. With Frame Skip Subtle the same fight is about one update and one frame every 27 ms: about 37 fps at 0.62 game speed (35 fps at 0.6 before).


### 8a. Approximate gear frame for fighting levies (`SoldierNPC.GearApprox.cs`)

Setting: **Levy: Performance → Speed in big fights → Gear update interval when fighting** (1-8, default 4; 1 is the exact, every-tick frame). Dormant levies and stat refreshes always build in full.

How it works. A fighting levy runs the build half of the gear frame (ResetEffects, buffs, equips, armor sets, stations, potions) on one tick in N, staggered by body index so the army's builds spread evenly. On the ticks between, the leaves the last build wrote are written back from an image taken right after that build, and the tail (PostUpdateEquips, Calamity's PostUpdateMiscEffects: Rage, Adrenaline, stealth, TimeHolding, cooldowns) runs as every tick. So the stats a levy fights with are always exactly the stats of a full build at most N-1 ticks old: never above or below what a player in that gear and state had then.

- **Which leaves are written back** is learned per levy: the dummy (Player, its 66 ModPlayers, worn ModItems) is imaged before and after its first four builds and then every eighth (StateImage); every leaf that differs is "written by the build". The core stats ResetEffects resets (life regeneration, defense, damage classes, speed, endurance, minion slots...) are always written back, so a tail that adds to one of them only now and then cannot add up. Never written back: what other code moves every tick (the body's motion, life, mana, buff timers, wing and rocket time, the tail's meters).
- **Buff timers** count down on a served tick as the buff pass would; a buff about to run out forces the build that removes it.
- **Cooldowns the build counts down** (an accessory's `cooldown--` in UpdateAccessory, found as a field nothing else touched that the build lowered by one on two builds running, and never raised) are counted down on served ticks too, to zero or past it as the build was seen to do. The drift probe caught this on Void of Calamity, whose cooldown otherwise ran N times slower.
- **Forced full build on any input change**: worn gear and every stat refresh (once a second and whenever the stats are marked dirty), the dummy's buffs (added or removed), the body's buffs (potions, stations, banners), the held weapon, a 5% step of life or a 10% step of mana, armor-set state, sidearm, defense damage, mount, and the world flags the build reads (day, weather, events, Calamity's boss flag; once a tick).
- **Exact frame kept** for a levy whose build casts a projectile (Plaguebringer and Mollusk sets, the Chlorophyte set with Soul of Cryogen) or counts a counter up to a threshold (the Beetle sets): a served tick could not repeat those. Gear seen to do it is remembered, so the next levy in the same gear starts exact. Builds that only draw random numbers (the Yoyo Bag's counterweight type, set dust) are approximated.
- Calamity's Virid Vanguard counters, which its ResetEffects steps every tick, are stepped by the same statements; a levy holding the Virid Vanguard while one of its blades is out builds every tick.

**Drift probe** (`LEVY_GEARAPPROX_PROBE=1`): on every served tick the full build also runs from the same state and the two are compared stat by stat and leaf by leaf; the served result stays. Her army, Solar Pillar, interval 4, 1,200 ticks: 24,676 served ticks compared; the stats (defense, endurance, movement, regeneration, wing time, minion slots, mana, damage, crit and attack speed of every class, armor penetration) differed in 8 (0.03%): the Plague Reaper set's ranged bonus, 4 higher and 4 lower. 2 casts a full build would have made came on served ticks.

What drifts (the only differences left, all reaction timing):
- **Bonuses that switch with state the build reads each tick**: the Plague Reaper set's toggled ranged bonus (+60% damage, +20 crit, and its vision flags) and the Reaver set's decaying defense and damage bonus start and end up to N-1 ticks late (both directions for Plague Reaper; Reaver's ending is late, so up to N-1 ticks of the higher value). 0.03% of served ticks at interval 4; the Reaver case showed in earlier probe runs (11 of 24,584).
- **Motion-reactive accessories** follow the body's motion at the last build: anything the build computes from velocity or standing still (Shiny Stone's regeneration while still, Calamity's camper glow) lags up to N-1 ticks. None changed a stat in the probe's 24,676 comparisons beyond the above.
- **Wing time** is not touched: it is the body's (kept live, never written back); the wings' maximum is a build stat and stays exact.
- **Cosmetic**: the Yoyo Bag's counterweight type and set dust draws (random numbers not drawn on a served tick); the honey flag (Levy sets it again each tick after the frame).
- **Minions and casts**: 2 casts in 24,584 served ticks would have come on a served tick (a rare cast before the gear was marked exact).

**Default 4, and why.** Interval 2 and 3 cost more than they save (98 and 78 us against 75.5 exact): every full build pays about 35 us of imaging and checking (`before` 14 us, `after` 21 us in `HARNESS-GA`), and a served tick still costs about 41 us (the tail, the frame's own setup, the write-back). From 4 on it pays: 70 us at 4, 68 at 6. Six buys another 2 us a levy (0.08 ms a tick) for half again the lag; 4 keeps the longest a stat can trail its cause at 3 ticks (50 ms). About a quarter of her army keeps the exact frame (Beetle sets, Plaguebringer and Mollusk sets, a Chlorophyte set that casts), which is why at most half the frames are served.

What the lever is worth, honestly: about 0.2 ms a tick at 45 levies. With the Calamity patches the build itself fell from 57 to 41 us, so there is less left to skip; the per-tick tail (about 30 us) is the larger half of a levy's gear frame now and it runs every tick by design.

### 8b. Calamity patches for levy dummies (`Common/Compat/Calamity/CalamityLevyPatches.cs`)

Found by name (no reference to Calamity); nothing is patched without it; `LEVY_CALPATCH=0` turns them off. Each patch first asks "is this a levy dummy" (`SoldierOwner.IsDummy`) and is otherwise transparent.
- `AndroombaRightClick`: skipped for a dummy (the local player's right-click on a friendly Androomba; walks every NPC).
- `AbyssEffects`: its first call, `SetAbyssLightLevels`, is skipped for a dummy (the abyss darkness and glow numbers, read only by the lighting and the darkness shader). The abyss's breath, defense and life loss below it still run.
- `UpdateDrawingParameters`: a levy is drawn with its dummy, so its Rover Drive / Lunic Corps / Profaned / Sponge shield visuals need it; it is skipped for a dummy only on a dedicated server, where nothing is drawn.
- `ResetEffects`' `Main.projectile.Any(...)` (Virid Vanguard Zenith slash, 1,000 slots for every player every tick): inside a levy's gear frame the same predicate is asked of the slots that held an active Virid Vanguard blade (a list made once a tick, remade when a blade is spawned). The predicate requires an active blade, so the answer is the same.

The `[LevyRules]` set and accessory parity check leaves out the four abyss lighting numbers while the patches are in force (they are the skipped call's output, read only by the lighting); the gear frame check below compares everything else.

The projectile `Any` was the large one: inside the build it cost about 13 us per levy per tick (build 60 → 46 us in the same boot), more than the 3% of the tick the sampling profile gave.

### 8c. The soldier shot swaps: measured, not cut

`LEVY_SWAPPROF=1` times the owner swaps around every soldier shot (OwnerPuppet's transform, life routing, cursor and button puppets; UseGuard's snapshot; the held-item view) against the shot's whole update. Her army, Solar Pillar, 15 fights: owner swaps around the update pass 0.21-0.31 ms a tick, around the hit pass 0.21-0.43 ms, UseGuard 0.04-0.06 ms, the held-item view 0 (no plain-shot holdouts in her army), against 3.2-6.0 ms a tick of soldier shot updates (240-340 shot updates and 340-680 hit passes a tick). So the swaps are about 12-14% of the army's shot time (13 to 17 us per levy per tick of about 110-130). About half of it is the hit pass's swap opened inside the update pass of the same shot (Projectile.Damage runs every tick for every friendly shot, about 2 per shot update): it re-sets what the update pass already set. Skipping it is not provably identical: the life routing then gives a lifesteal heal to the soldier at the end of the update instead of right after the hit (the "+N" and the soldier's life move later in the same update, and a second lifesteal check in that update would read the healed life), and Thorium's local-player cache is only swapped by the hit pass. Not cut; worth about 0.3 ms a tick at 45 levies if Eve accepts that timing difference.

### 8d. Frame Skip in Levy's settings

**Levy: Performance → Speed in big fights → Frame Skip: On / Subtle / Off**, with the hint "The game's Frame Skip setting. Subtle keeps the picture moving in heavy fights by slowing the game slightly." It reads and writes the game's own `Main.FrameSkipMode` and saves it the game's way (`Main.SaveSettings`, not on a server); it is not stored in Levy's file. The lag popup's "Set Frame Skip to Subtle" uses the same path.

### 8e. Checks

- `[PerfBoss]` (ship world, standing army): interval 1 serves nothing; interval 3 serves ticks, every compared served tick has exactly a full build's stats (nothing moves, so any difference is an error), no cast is missed, the tail runs on every frame; a change of gear and a buff on the body each rebuild on the very next tick (Ironskin's +8 defense is there at once). With Calamity: one gear frame run twice from the same state (dummy, ModPlayers, items, random generator), patches bypassed and in force, leaves the same dummy outside the lighting numbers; the skipped calls are reached for dummies; every patched projectile question is also asked of the whole array and gives the same answer.
- `[PerfAnalyzer]`: Frame Skip in Levy's settings sets and shows the game's setting and is not stored in Levy's file; the gear interval has its default and is the one the frame uses.
- Ship world (serial, both halves): **0 FAIL**, 2,057 PASS (`SHIPWORLD_SERIAL=1`, 1,183 s; the known summon-staff pick line excluded as always). The first run had 6: the three `[LevyRules]` parity lines (the abyss lighting numbers the patch skips, now left out of that check), the patch-name check and the drawing-parameters difference on a server (both in the new check itself), and the quiet-field dormancy check (an enemy stood near the army in that run; 8,803 frames skipped on the rerun).


## 9. Her own culprits, and the swap (lag4, 11-19)

Eve still feels "a couple of weapons are the problem". The lab can't see her machine, so the analyzer now measures it in her own fights. Lab: the Solar Pillar scene, `pw=45`, Calamity, Master + Death, 1,800 ticks a fight, one tML server, one server at a time.

### 9a. Per-weapon cost in the always-on analyzer (`Common/Perf/PerfWeapons.cs`)

On the sampled updates (one in eleven), each levy's own update (AI and gear frame, with other mods' hooks on it) and each update of its shots (AI, the hit pass and its swaps, with other mods' hooks) are charged to the weapon the levy holds right then. That's its sidearm when it's fighting with the sidearm. The owner swap around each shot's update is timed too: it sits outside the timed body (`OwnerPuppet.UpdateSampled`). Per weapon, each window keeps:
- the levies holding it;
- ms per update of their bodies and of their shots;
- shots alive;
- the projectile type that costs it the most.

The log stores these as `"wp"`: `[name, levies, body ms, shot ms, shots alive, top projectile, its ms]`, the 8 biggest. It also stores `"wpa"` (all levy weapons together, ms per update) and `"wpr"` (the reference it's checked against: levy bodies and army shots without other mods' hooks, those hooks, the swaps). The fight's log line carries `"wpn"`: the top 5 as `[name, levies, ms, % of the army]`.

What she sees:
- `/levyperf`, over the last minute, and `/levyperf fight`.
- In the lag popup of a laggy fight, a line like this one from her army in the lab: "Heaviest levy weapons this fight: Apathanull (5 levies) 2.3 ms/tick, 19% of the army; Dark Harvest (4 levies) 2.0 ms/tick, 16%; Morning Star (3 levies) 1.3 ms/tick, 11%."
- When one weapon costs each levy holding it at least twice the army's average (and at least 0.1 ms), a second line, for example: "Dearest per levy: Plaguenade, 2.0 ms/tick for each levy holding it, 8.0 times the army's average of 0.25." Her army in the lab has no such weapon: the dearest per levy is Dark Harvest at 0.51 ms against an average of 0.27.

One approximation: a shot is charged to the weapon its levy holds when the shot updates. A summoner's Dreadmines from its sidearm land on its Morning Star once it switches back.

**Checks** (`[PerfBoss]`, the engaged ship-world army with Plantera; `PerfSelfTest` at boot):
- Every kit weapon has a row with a quarter of the levies.
- The weapons together come to the analyzer's own army rows: 4.392 ms/tick against 4.355 from the levy and army-shot rows, plus 0.037 of swaps.
- The rows survive the log's round trip, and an old line reads as none.
- A laggy fight's popup names the three heaviest with their share. The exact words and the "dearest per levy" line are checked on synthetic windows.

**Cost**, timed call by call on a real levy and shot, with the attribution on and off:
- levy body pair: 85 → 100 ns;
- shot pair: 91 → 110 ns;
- swap timing: 126 ns;
- window rows: 1.3 us a window.

That's **4.0 us per tick at 45 levies and 300 shots** (13 us at 150 and 1,000), against the 0.1 ms bar. In the pillar lab the attributed total matches the census of the NPC and projectile phases (her army 12.4 against 13.5 ms/tick CPU; Apathanull 23.2 against 21.9). Lab only: `LEVY_PERFAN_SERVER=1 LEVY_PERFAN_ECHO=1 LEVY_PERFAN_WIN=2` writes 2 s windows into the server log (`[PerfWin]`). `lab/lagsweep/l4_weapons.py` tabulates them.

### 9b. The nested hit-pass swap (approved): skipped (`OwnerPuppet.CanSkipNested`)

`Projectile.Update` runs the shot's hit pass inside itself, about twice per shot update. The update pass has already put the owner on the levy. The hit pass's own swap now runs only when something it would write has changed. It's skipped when:
- the open frame is this shot's update pass;
- the transform, the levy's max life and the live owner are still in place;
- for a shot that reads the cursor, the commander's cursor and Calamity's aim already point at the levy's target. The levy's `AimCursor` is called once here, as the entry would.

Thorium's local-player cache, which only the hit pass swaps, is still swapped around the call. A lifesteal gain stays in the owner's life (the levy's, for the pass) until the update pass closes, and the levy gets it then, in the same tick. `LEVY_NESTSKIP=0` turns the skip off, and the harness token `ns=0|1` sets it per fight.

**Measured** (her 45 levies, 3 seeds per arm, same boot; `LEVY_SWAPPROF=1`, which times the swaps directly; ms per tick):

| arm | hit passes a tick | hit-pass swaps | skip tests and skipped passes | together |
|---|---|---|---|---|
| kept (`ns=0`) | 636-650 | 0.315-0.324 | 0.045 (the test's clock reads) | 0.365 |
| skipped (`ns=1`) | 592-638 skipped, 0.3 kept | 0.000 | 0.168-0.178 | 0.173 |
| skipped, with the compiled Calamity accessors (9d) | 568-620 skipped | 0.000 | 0.140-0.148 | 0.145 |

**Saving: about 0.2 ms per tick at 45 levies.** That's 0.22 as timed, about 0.17 once the extra clock reads of the timing itself are taken out. This is less than section 8's estimate of 0.3, because the test that proves the skip safe costs about 0.24 us per pass against 0.5 us for the swap. A first version that left cursor-reading shots alone skipped only two thirds of the passes and saved 0.06 ms. The cursor shots (148 passes a tick in her army) were the dearest passes.

**Proof:**
- `[IdentityWorld]` nested hit pass, with Calamity:
  - **Lifesteal totals:** a levy shot with three hit passes per update heals its owner on every hit (Player.Heal, and a bare `statLife +=`), from 20% and from 94% life. Over 8 updates, with the skip on and off from the same state: the same levy life after (400 = 400), the same hits (24), the same routed life, the commander's life untouched. With Player.Heal, every life a hit saw is the same. With a bare `+=`, a later hit in the same update reads the unclamped life until the update ends; the levy's total is clamped the same.
  - Six cursor-reading Calamity shots (Anomaly's Nanogun, Auger, Countermeasure Mitt, Hydraulic Volt Crasher, Phaseslayer, Pulse Grenade), seeded the same, end with the same place, velocity, AI state, damage to the target and commander cursor (32 passes skipped).
  - Skipped with the skip on (96 passes), never with it off.
- **Armysweep ship subset** (the same boot setup, `LEVY_ARMYSWEEP_ALONE=1`, `LEVY_NESTSKIP=0` then `1`): all nine stay OK, `pass=13 fail=0` both. Ratios (min of idle and commander-holding):

| weapon | ratio kept → skipped | levy damage, idle |
|---|---|---|
| Terra Blade | 0.94 → 0.91 | 2,897 → 2,906 |
| Vernal Bolter | 0.80 → 0.82 | 3,936 → 3,853 |
| Sniper Rifle | 0.88 → 0.78 | 3,040 → 3,004 |
| Stellar Knife | 0.74 → 0.75 | 4,015 → 3,756 |
| Morning Star | 3.54 → 3.60 | 10,632 → 10,522 |
| Infested Clawmerang | 0.80 → 0.87 | 507 → 518 |
| Wingman | 0.88 → 0.87 | 7,467 → 7,487 |
| Doomsday Device | 16.3 → 15.1 | 2,181 → 2,181 |
| Nucleosynthesis | 0.87 → 0.83 | 20,368 → 19,719 |

  The ratios move by as much as the stand-in player's own damage does between two boots (Sniper Rifle's player run, a path the skip doesn't touch, went 3,466 → 3,848). The sweep isn't bit-deterministic across boots; no ratio moved beyond that.

### 9c. Her hypothesis in the lab: every levy holding one heavy weapon

The base is `eve45.json`, her 45 levies with her armor, accessories and stations. In each arm every levy holds one of the heaviest weapons of the section 3 sweep, with no sidearm. `dr=0.95` (every hit on a levy cut by 95%) keeps all 45 alive and engaged; without it, five of the eight armies were wiped in 10 to 26 s. 3 seeds each, `lab/lagsweep/l4_weapons.tsv`, `mkweap.py`. Numbers are from the analyzer's own attribution:

| every levy holds | army ms/tick (bodies + shots) | **ms/tick per levy** (seeds) | shots alive | costliest projectile, ms/tick | CPU ms/tick, NPC + projectile phases |
|---|---|---|---|---|---|
| Apathanull | 23.2 (5.3 + 17.9) | **0.515** (0.518, 0.525, 0.500) | 287 | Cosmic Tentacle 15.1 | 21.9 |
| Shard of Antumbra | 23.1 (5.8 + 17.3) | **0.513** (0.521, 0.493, 0.525) | 318 | Antumbra Shard 15.2 | 24.2 |
| Havoc's Breath (Gel) | 21.9 (6.7 + 15.2) | **0.487** (0.499, 0.471, 0.491) | 377 | Brimstone Fire 13.7 | 24.1 |
| System Bane | 18.4 (5.7 + 12.8) | **0.409** (0.415, 0.409, 0.404) | 820 | System Bane Lightning 6.5 | 19.8 |
| Radiant Star | 16.7 (6.0 + 10.7) | **0.371** (0.380, 0.366, 0.368) | 764 | Radiant Star 5.9 | 18.2 |
| Plaguenade | 16.2 (6.0 + 10.2) | **0.360** (0.356, 0.368, 0.355) | 986 | Plaguenade Bee 8.9 | 17.5 |
| The Syringe | 14.1 (5.9 + 8.2) | **0.313** (0.315, 0.321, 0.302) | 668 | Syringe Cinder 4.2 | 15.9 |
| *her own army* | 12.4 (3.7 + 6.1) | **0.276** (0.274, 0.276, 0.278) | 254 | Cosmic Tentacle 1.5 | 13.5 |
| Terra Blade | 8.4 (5.5 + 2.9) | **0.186** (0.183, 0.191, 0.186) | 115 | Terra Blade beam 1.5 | 10.6 |

What it says:
- The dearest weapon costs a levy **1.9 times** her mixed army's average. The cheapest costs **0.7 times**.
- An army of only Apathanull would run about 11 ms per tick more than hers. A single Apathanull levy in her army costs about 0.25 ms more than an average one.
- Taking out "a couple of levies" holding the worst weapon saves about 1 ms. Taking out a couple at random saves about 0.55. Both are enough near the 16.7 ms cliff (section 1), which is why the observation feels true.
- In her own army no weapon is an outlier. Per levy: Dark Harvest 0.51, Apathanull 0.47, Morning Star 0.45, Malevolence 0.37, Star Shower 0.25, Wingman 0.22, Terra Blade 0.16, Ark of the Ancients 0.12, against an average of 0.27. The cost is spread.
- Plaguenade is the dearest weapon in the one-levy sweep, but not here. Its bees die on hits in a crowded fight; 986 alive for 45 levies is 22 a levy, against 191 on a lone dummy.

**Pathological in levy hands but not in player hands? No.** The same eight weapons in the army sweep's cost table, this build, one levy against one stand-in player at the same dummy, us per shot, levy / player:

| weapon | us per shot, levy / player | ratio |
|---|---|---|
| Plaguenade | 8.2 / 5.7 | 1.4 |
| Terra Blade | 33 / 24 | 1.4 |
| Apathanull | 56 / 50 | 1.1 |
| Shard of Antumbra | 27 / 27 | 1.0 |
| System Bane | 11.0 / 10.5 | 1.05 |
| Radiant Star | 11.8 / 10.5 | 1.1 |
| The Syringe | 9.9 / 10.1 | 1.0 |
| Havoc's Breath | 19.7 / 22.6 | 0.9 |

Section 3's 2 to 3 times for Terra Blade, Apathanull and The Syringe (262 / 82, 107 / 55, 26 / 9) did not come back. That sweep ran on a box at load 20 to 30, and per-shot costs over 1 or 2 shots alive move with it. What makes Apathanull, Antumbra and Havoc's Breath dear is their own shots, the same in a player's hands: Cosmic Tentacle 94 us a shot (section 2), Antumbra shards and Brimstone fire at 13 to 15 ms per tick for 45 levies.

### 9d. Anything else, behaviour-identical (`AltPolicies`): compiled accessors, about 0.06 ms

The cursor puppet calls `Player.GetModPlayer<CalamityPlayer>` through `MethodInfo.Invoke` (an argument array each call). It also reads and writes Calamity's per-player aim (`mouseWorldDeltaFromPlayer`, `mouseRotationFromPlayer`) through `FieldInfo` (boxing). Both happen around every update and hit pass of a cursor-reading shot. They're now compiled delegates over the same call and the same fields, with reflection as the fallback. `LEVY_CALFAST=0` restores plain reflection.

- **Check:** writes by one read back through the other, and both find the same CalamityPlayer.
- **Measured** (her army, 2 runs x 3 seeds per arm, `LEVY_SWAPPROF=1`): the update swap fell from 0.85 to 0.75 us a shot and the skip test from 0.30 to 0.245 us a pass. That's **about 0.06 ms per tick**.

That's under the 0.2 ms bar on its own. It's kept because it is exact and it is part of what brings the swap saving to 0.2. To drop it, revert the commit.

Nothing else provably identical worth 0.2 ms turned up:
- UseGuard costs 0.05 ms a tick.
- The miss path on non-army projectiles costs 0.04.
- The update swap is 0.22, every tick, by design.
- The gear frame (section 8) is the rest.

**Ship world** (serial, both halves, `SHIPWORLD_SERIAL=1 SHIPWORLD_TIMEOUT=1500`, 1,260 s): **0 FAIL**, 2,073 PASS (the known summon-staff pick line excluded as always). The new checks pass in both halves (the cursor-shot and compiled-accessor checks need Calamity: the Calamity half); the attribution cost on the ship army is 4.1 us per tick at 45 levies and 300 shots.


## 10. Active army size: the player's own lever (activecap, 11-19)

Eve asked for a tool she chooses: "you can set an amount of levies that can be active at once, and when one dies, it's immediately replaced by the next levy, who takes their place and spawns in immediately", and for the game to say flat out when the army is more than her computer can run.

### How it works (`Common/Systems/RosterSystem.ActiveArmy.cs`)

The active army size is how many of one commander's Regulars have a body in the field at once. It is off by default (0: everyone fights). The rest stay on active duty: paid, holding their command points, taking orders, kit on the record. They simply have no body, the way soldiers over the render cap always waited. It is built on the existing deployment and reserve code, not beside it: `SpawnBody` refuses a body while the commander's field is full, so the respawn path, Remobilize and the deployment pass all respect it without changes of their own; lowering the size benches the lowest-priority soldiers in the field the way `Demobilize` does (live HP onto the record, the body retired, the kit kept), without putting them in the reserve.

Who fights first is the deployment order, now with tie-breaks: the order (Follow with the commander online, then Patrol, then the rest), then grade (Champions, then Men-at-Arms), then enlistment. Officers, militia and drones are never limited.

When a fielded soldier falls, his place is filled the same tick (after the NPC phase, before the deployment pass, and again at the end of the tick for deaths in the projectile phase) by the next soldier waiting, where the fallen one stood. Under Regroup he appears beside the commander (the existing Regroup spawn), and after a lava, drowning or thorn death he comes in where deployment would put him instead of on the hazard. The existing safe-spawn rule still keeps him out of a knot of enemies. A replacement is a living soldier who was waiting: the fallen one still costs his barracks one manpower and waits out his own respawn timer, then waits again if the field is full.

The size is a setting of this computer, like Frame Skip: **Levy: Performance → Speed in big fights → Active army size**, sent to the server when the player enters a world and whenever it changes. It can also be set from three places:
- the Barracks, on the reserve row beside Stand down all / Remobilize all: `-` `Active: 25` `+` `?` (Shift steps by 5; clicking the size clears it). The "?" says: "Set how many soldiers fight at once. When one falls, the next in reserve takes their place right away. Fewer soldiers on the field means less lag." A soldier waiting reads "Waiting in reserve" on his card.
- the lag popup after a laggy fight where the army was the cost: a line "About N soldiers at once would run smoothly here. Set an active army size?", a `-` N `+` `?` stepper set to the suggestion, and a "Set active army to N" button (the old reserve-bank hook, now real).
- the live notice (below).

### The game says it flat out (`PerfAnalysis.ArmyNotice`, `PerfPopup.CheckLive`)

As each 8-second analyzer window closes, once a session: if the whole window ran at 14 ms an update or more (or with Frame Skip Subtle under 0.8 of full speed), the army's own cost was at least 30% of the update, and at least ten of the player's soldiers were in the field, the corner window opens at once, mid-fight: "Your army is more than this computer can run smoothly (about N soldiers at once). Set an active army size?" with the same stepper and **Set active army to N** / **Open Levy settings** / **Don't ask again** (its own setting, so it does not silence the post-fight note). It stays up through the fight; the post-fight note still hides when a boss comes near.

**The suggestion.** The army's measured cost per update (its bodies, its shots, other mods' hooks on them and the owner swaps: the weapon attribution's reference) shared over the soldier bodies it was measured on is the cost of one soldier; enough of the player's own soldiers come off to bring the update to 12 ms (with Frame Skip Subtle slowing the game, to whatever leaves room for that window's drawing inside one frame, at least 4 ms), never under 5. On her 11-17 laggy windows (15.9 ms an update, 44 soldiers, levy bodies 6.6 + army shots 3.6 = 10.2 ms) that is 27.

### Measured (Solar Pillar, `pw=45`, Calamity, Master + Death, 2,400 ticks, one tML server; `lab/activecap/ac_pillar.tsv`, `ac_table.py`)

`ac=N` is the harness token (0: no size, logged as the control). Update ms is the server analyzer's own per-window update time (first window skipped); army ms its army reference; the box was at load 14 to 18 throughout, so compare arms, not absolute numbers.

| army, size | fights | update ms/tick | army ms/tick | in the field | dead at end | army damage/s | damage/s at 15 / 25 / 35 s |
|---|---|---|---|---|---|---|---|
| her 45, no size | 2 | 21.7 (19.7, 23.8) | 18.9 | 42 | 4 | 39,918 | 41.4k / 37.8k / 41.8k |
| her 45, 30 | 2 | 15.9 (15.4, 16.4) | 13.1 | 30 | 12 | 41,179 | 39.6k / 44.1k / 40.3k |
| her 45, 25 | 2 | 14.6 (14.0, 15.2) | 11.3 | 25 | 17 | 37,227 | 38.2k / 39.5k / 37.0k |
| her 45, 20 | 2 | 11.4 (10.2, 12.5) | 8.3 | 20 | 23 | 35,141 | 37.5k / 37.6k / 36.9k |
| her 34, no size | 3 | 14.2 (13.5, 16.2, 14.2) | 12.2 | 32 | 2 | 39,617 | 41.0k / 39.2k / 41.7k |
| her 34, 30 | 3 | 14.9 (13.1, 15.2, 14.9) | 12.3 | 29 | 9 | 39,047 | 34.0k / 48.7k / 37.6k |
| her 34, 25 | 3 | 13.8 (14.9, 13.8, 13.1) | 11.9 | 25 | 11 | 38,206 | 45.7k / 42.4k / 34.4k |
| her 34, 20 | 3 | 10.6 (10.6, 12.0, 10.1) | 8.1 | 20 | 11 | 34,781 | 36.3k / 33.1k / 37.6k |

OBSERVED. With her 45, a size of 20 halves the update (21.7 → 11.4 ms, the army's own share 18.9 → 8.3) for 12% less damage per second; 25 takes a third off for 7% less; 30 takes a quarter off for the same damage within noise. The damage per second holds flat across the fight in every capped arm: the field stays full (replacements arrive the tick a soldier falls; with 45, 4 were still waiting at the end of every capped fight) and the pressure stays up. With her 34 the 30 arm is effectively no limit (32 alive) and 25 is inside the box's noise; 20 cuts a quarter.

OBSERVED, and a real cost: **more soldiers die** with a size set (45: 4 deaths uncapped, 12 / 17 / 23 at 30 / 25 / 20). Each death costs a manpower and a respawn wait, so a small size trades lag for attrition. Plausible causes, not yet separated: the pillar keeps 45 enemies alive whatever the army's size, so each fielded soldier draws more of them; a replacement comes in where the last one fell, at the front; fewer soldiers mean fewer heals, auras and set procs shared between them. A follow-up lab (replacement at the muster point vs at the spot, same size) would separate the second from the rest.

INFERRED for her machine (a headless server cannot draw): the update scales with the arms above (20: 52%, 25: 67%, 30: 73% of uncapped), so her 15.9 ms becomes about 8.3 / 10.7 / 11.6 ms. With Frame Skip On the game spends 60 × update ms a second on updates and draws in what is left at about 12 ms a frame: (1000 − 60 × 15.9) / 12 ≈ 4 fps uncapped (her log: 3 to 5), and about 42 / 30 / 25 fps at full speed with sizes 20 / 25 / 30, before the drawing itself gets cheaper with fewer soldiers on screen. With Frame Skip Subtle, about 49 / 44 / 42 fps at 0.82 / 0.73 / 0.71 game speed (36 fps at 0.6 uncapped).

### Checks

- `[ActiveArmy]` world check (ship world, both halves; `LEVY_ACTIVEARMY_ONLY=1` runs it alone), 30 soldiers of one commander, three Champions enlisted last, size 20: the size holds through two deployment passes, one body per soldier; the Champions stay in the field and the last-enlisted Levies are benched; benching keeps all 120 items and the live HP; command points held by all; militia never limited; a soldier who falls is dead with his own respawn timer and is replaced within one tick by the lowest-id soldier waiting, where he fell (0.0 tiles); five falling in one tick are all replaced in that tick; after a lava death the replacement comes in beside the commander, not in the lava; under Regroup beside the commander; respawned soldiers are refused a body while the field is full; sizes 25 and none field the rest; a save and load round trip (records written and read back, bodies gone) gives back 30 records with unique ids, 20 in the field, one dead, one body each and every item; the live notice shows at once with the right size and words, only once a session, and Don't ask again silences it; the "?" text and the labels.
- `PerfSelfTest` (boot and the command-line tool): the suggestion's arithmetic (27 for her numbers; the cut comes off the player's own soldiers; the floor of 5; nothing under budget or with a number missing; the largest army that lands at 12 ms), the post-fight offer (after Lower levy effects, with the size in words; never in the other-mod popup or when the frames were the cost), and the live notice's rules (14 ms, 0.8 speed with Subtle, the army's share, ten soldiers, focus). The option-count guard still expects six options; "reserve bank" is now "active army".

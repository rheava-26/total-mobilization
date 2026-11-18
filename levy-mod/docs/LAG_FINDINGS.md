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

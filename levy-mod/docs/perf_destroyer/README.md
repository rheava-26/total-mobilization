# perf-destroyer: why the army costs 12 ms a tick against the Destroyer

Lab: `levy-mod/lab/levylab run levy-mod/lab/specs/perf_destroyer.yaml` (Calamity world, Death mode, 36 levies of `postwof.best` each carrying a Caustic Staff, the Destroyer's life x8 so the fight lasts, levies take 10% damage so they stay up, 40 s per fight, 3 seeds). `python3 levy-mod/lab/perf_summary.py RUN_DIR` prints the table, `perf_buckets.py RUN_DIR cell cell` the per-bucket comparison. Env gates: `LEVY_CROWD=1` (census: CPU ms per tick of NPC / projectile updates, allocation, GC, spike ticks), `LEVY_PROF=1` (buckets below; on a client: the `[Prof] client` line), `LEVY_OPT=0` (the perf-destroyer fixes off). `lab/perf/profile.sh` takes a sampling profile (EventPipe) of any spec.

## What the tick is made of (CPU ms per tick, medians over 3 seeds, this machine)

| | Destroyer alone (1 levy) | + 36 levies | + 36 levies, no summon items |
|---|---|---|---|
| whole update | 2.7 | 14.2 | 11.8 |
| levy AI (`npc.levy`) | 0.14 | 5.1 | 5.0 |
| &nbsp;&nbsp;gear frames (`stats`: Calamity ResetEffects 0.7, UpdateEquips 0.5, armor sets 0.3, PostUpdateMiscEffects 1.0) | 0.01 | 2.9 | 2.8 |
| &nbsp;&nbsp;dodge / movement / combat / brain | 0.1 | 0.4 / 0.5 / 0.4 / 0.2 | same |
| Destroyer's 82 segments (`npc.boss`) | 1.05 | 1.1 | 1.1 |
| army projectiles (`proj.army`, of which the hit loop `Projectile.Damage` 4.8) | 0.25 | 7.4 | 4.7 |
| NPCs / projectiles alive | 93 / 16 | 88 / 212 | 91 / 125 |
| projectile-NPC pairs asked per tick (hits landed) | 235 (0.2) | 33 000 (9) | 22 800 (5) |
| allocation KB per tick | 48 | 402 | 393 |

* The army is 36 Calamity-geared players (`GearUpkeep` runs a full `Player.Update` slice on every levy every tick, all of it Calamity / tModLoader code), and 200 to 300 projectiles (78 of them minions and their bolts) each asking every Destroyer segment "can I hit you" through the mod hooks: 33 000 pairs for nine hits, 140 ns a pair, a third of the tick. Neither is a Levy loop gone wrong. Turning the gear frames off (`LEVY_UPKEEP=0`) takes 1.7 to 3.0 ms off, dropping the Caustic Staff minions 1.7 to 3.8 ms (median 2.7) over four runs.
* Allocation is 400 KB per tick (Calamity's `ResetEffects` rebuilds `DamageClassData`, its armor-change tables allocate arrays on every property read); the GC pauses 3.5 ms per 2 s, not a cost.
* Drones and turrets (`drones=10 turrets=3`), a sea shore arena (`arena=a3w`), a visible commander (`spec=0`): within noise of the plain army (see the run tables).
* Spikes: the first seconds of a fight (the boss spawns 82 segments in one tick) and, before the fix below, every 60th tick when all 36 stat refreshes landed together (`stats` 14 ms in one tick).

## Why this is the 2 FPS

The in-game clock moved 10 minutes in 14 s of video, i.e. 600 ticks in 14 s = 43 updates per second, about 23 ms per update, while the frame rate was 2. That is the signature of the update being slower than 16.7 ms, not of a slow draw: the engine runs up to 30 catch-up updates between two draws (it clamps the backlog at half a second), so once an update costs more than 16.7 ms the game does 30 updates, one draw, 30 updates... and FPS is about UPS / 30. This lab measures 14 ms (CPU time of a server tick, no client work) where the video implies 23 ms, a factor 1.6 that a laptop and the client-side extras (dust, lighting, sound, the player) account for. To get a game under 16.7 ms the army's increment has to come down by about 40%, which the first pass did not reach; the pair cull of the perf2 section below takes about a third of the whole update. `LEVY_PROF=1` on a client now logs one `[Prof] client` line every 2 s (fps, ups, ms per update split NPC / projectile / rest, ms per frame of draw split NPCs / projectiles / dust / gore / players / interface / levy bodies / nameplates): one fight on the machine that ran at 2 FPS settles update against draw. The draw side could not be measured here (headless).

## What changed (all behind `LEVY_OPT=0` for A/B, none changes what a levy does)

* Dodge (`ThreatField.Hits`, `SoldierNPC.Consider`): a candidate maneuver scores `hits x 100 + penalty (+ a non-negative worm term)` and only wins by scoring under the best so far, so the hit count now stops as soon as the candidate cannot win. `LEVY_CUTCHK=1` counted every candidate to the end as well: 207 000 candidates, 87% cut off early, 0 verdicts different. dodge 0.66 -> 0.39 ms, `hits` 0.47 -> 0.20 ms, fewer spikes (ticks over 22 ms after the first ten seconds: 187 -> 135 per fight).
* Stat refresh of a levy is phased by body index instead of all landing on the same tick (a 14 ms tick every second for an army spawned together).
* Gear frame: the saved projectile counts are sparse (only the non-zero entries are saved and restored, the array has thousands of entries), and the per-frame timing is lab-only.
* `CanHitNPC` of `LootCreditGlobalProjectile` folded into `SoldierProjectileTag.CanHitNPC`: one hook and one tag lookup per pair instead of two.
* `PairCull` (since perf2 an IL edit of `Projectile.Damage`, on by default, `LEVY_PAIRCULL=0` turns it off): see the perf2 section below.
* Tooling: Destroyer boss, `drones=` / `turrets=` / `arena=a3w` in the Calamity harness, `LevyProf` buckets (gear frame parts, NPCs and projectiles by owner, per type, hit loop, pairs, hits), CPU-time census with allocation, GC and spike ticks, `ClientProf`, `lab/perf/` (EventPipe profile scripts), `perf_summary.py`, `perf_buckets.py`.

## Before / after (same seeds, 3 each)

| build | 36 levies (ms per tick) | ticks over 22 ms after 10 s | 1 levy | no summon items |
|---|---|---|---|---|
| original code (wall) | 15.3 | | 2.2 | 12.5 |
| `LEVY_OPT=0` (CPU, profiled) | 15.8 | 187 | | |
| this branch (CPU, profiled) | 14.2 | 135 | 2.7 | 11.8 |

A process is worth about +-1 ms in this lab, so the gain (7 to 10%) is real but small; the dodge buckets, which are measured inside one process, show the part the fixes touch.

## perf2: the pair loop is cut before any hook is dispatched

`Projectile.Damage` walks the 200 NPC slots for every damaging projectile and, for each live NPC, dispatches `CombinedHooks.CanHitNPCWithProj` (every mod's `GlobalProjectile.CanHitNPC`, `GlobalNPC.CanBeHitByProjectile`, `ModProjectile`, `ModNPC`, `ModPlayer`, Calamity's included) before it tests the hitboxes. 212 army projectiles x the Destroyer's 82 segments = 33 000 dispatches a tick for 9 hits. The previous `PairCull` ran inside Levy's own hook, after the other mods' hooks, and saved 2%. It is now an IL edit of `Projectile.Damage` (`PairCullSystem`, `Common/Weapons/PairCull.cs`, installed with `MonoModHooks.Modify`):

* Right after the damage hitbox is stored (vanilla's local `val`, which already includes every mod's `ModifyDamageHitbox`), `PairCull.Begin` decides once per `Damage` call whether this is an army shot (soldier, officer / summon, drone shot; never hostile) whose hit test is provably the plain overlap of `val` with the NPC's rectangle, and builds four bounds: `val` grown by 32 px.
* At the top of the NPC loop, right after vanilla's own "slot alive" check and before anything else (immunity flags, hooks, netOffset shift, hitbox test), `PairCull.Far` skips (the same `continue` vanilla uses for a dead slot) an NPC whose rectangle, with its `netOffset` added exactly as vanilla does, lies entirely outside those bounds. Per pair that is one local test and a few field reads, inlined.
* Why it is exact: a hook can only veto a pair or force it into the hitbox test (which still decides), the loop has no side effect before the hooks, and for a plain-overlap shot a pair farther than the grown hitbox cannot overlap. The hit test is only plain when none of these holds, so `Classify` leaves alone (margin 0, never skipped) any projectile with a vanilla type that `Projectile.Colliding` / `Damage_GetHitbox` names (53 types: lasers, beams, lances, cones, trails...), an AI style 15 / 19 / 137 / 190, a whip, a modded projectile that overrides `Colliding`, and any projectile a loaded mod's `GlobalProjectile` that overrides `Colliding` applies to. The lists were read from the game's code (`ilspycmd`); a modded projectile never reaches the vanilla per-type branches. In the Destroyer lab only Calamity's ShadeFire, StormSurgeTornado and SandstreamScepterExplosion (all override `Colliding`) and vanilla 974 are left alone (about 3% of army projectile ticks).
* If the game's code does not have the shape the edit expects (the matched instructions are checked before anything is emitted) the edit is not made and the log says so.
* Audit, `LEVY_PAIRCHK=1` (lab): culls nothing and asks vanilla's real `Colliding(val, rect)` of EVERY classified pair; `paircull[checked audited violations maxHitExcessPx]` in the profile line. Result: 977 million pairs checked (969 million of them would be skipped), 0 violations, no hit ever landed outside the plain rectangle (`maxHitExcessPx` 0.0), over 12 Destroyer seeds; vanilla Skeletron, 20 seeds, 47 million pairs, 0 violations.

### Numbers (36 levies, postwof.best, Caustic Staffs, Death mode, 12 seeds, same seeds in both arms, CPU ms per tick, profiled; the box was loaded by other builders, so look at the paired difference, not the absolute)

| | cull off (`LEVY_PAIRCULL=0`) | cull on | difference |
|---|---|---|---|
| whole update | 17.68 | 11.62 | -5.5 ms mean (-32%), better on 12 / 12 seeds |
| projectile hit loop (`Projectile.Damage`) | 5.98 | 1.06 | -4.8 ms (-82%), 12 / 12 |
| projectiles (all) | 9.50 | 4.20 | -5.1 ms (-54%) |
| pairs the mods are still asked about per tick | 35 800 | 800 | -98% |
| pairs skipped per tick | 0 | 34 500 | |
| ticks over 22 ms after 10 s (per fight) | 517 | 44 | -89%, 12 / 12 |
| shot hits per tick | 9.5 | 9.2 | same within noise |

The previous branch head (`e1e6532`, same spec, same box a few minutes later) measured 17.26 ms, 478 ticks over 22 ms. A vanilla boss with a medium army (Skeletron, vanilla world, 100 seeds): whole update 1.53 -> 1.45 ms, hit loop 0.15 -> 0.10 ms, hits per tick 0.40 vs 0.35, damage dealt per second 232 vs 234, wins 57 vs 60 of 100 (p 0.77): no regression and no change in what the army does. (The fights are chaotic: two identical arms differ by a few wins; one 40-seed comparison showed 20 against 28 wins, the 100 fresh seeds did not repeat it.)

On her machine the update is 23 ms and the pair loop was about a third of it, so the expected update is about 16 to 17 ms: right at the 16.7 ms line, not safely under it. The rest of the army's cost is now gear frames (3.2 ms, Calamity's own code), the levy AI itself (4.8 ms with the gear frames inside it as `stats`), the Caustic Staff minions' AI (1.9 ms) and the Destroyer (1.2 ms).

Other places in the same loop looked at: the vanilla loop before the hooks reads only flags, so nothing else is wasted there; the player branch (`Main.player[Main.myPlayer].hostile`) is player-versus-player only; the per-type NPC loops after the hit loop belong to Slime and corruption-spread projectiles. After the cull the remaining 800 pairs a tick are shots that really are near a segment.

## The profiler for the player: /levyprof

`/levyprof` (or `/levyprof on` / `off`) in chat switches the client profiler on and off at runtime, no environment variable needed. Off, nothing is hooked and nothing is measured; on, it hooks the game's update and draw calls and every 4 seconds prints four short lines to chat and to the log (`[Prof]`), for example: "Levy profiler: 2.1 frames and 43.0 updates per second." / "One update takes 23.0 ms. Holding 60 per second needs under 16.7 ms, so the updates are the slow part." / "Per update: NPCs 9.1 ms, projectiles 8.0 ms (shot hit checks 1.1 ms of that), everything else 5.9 ms. One frame draws in 4.2 ms." / "Levy costs per update: AI 5.1 ms (dodging 0.4, moving 0.5, fighting 0.4, deciding 0.2), gear upkeep 2.9 ms." The log also gets the detailed `[Prof] client` line with the draw split and every Levy bucket of the window. On a multiplayer client it measures that client; the army's own costs show on the host. `LEVY_PROFTEST=1` (lab) lets a headless server run the same code, `LEVY_PROF=1` still starts it with the game (log only).

## Options that need a design decision (not done)

1. Gear-frame cadence: a frame every second tick for levies with Calamity gear when the army is large saves 1.4 ms, but every per-tick timer in that gear (cooldowns, stealth regeneration, Volatile Gelatin) runs at half rate.
2. The Caustic Staff rule (every non-summoner casts a minion) is about 2.7 ms of the 14.
3. Fewer projectiles per levy (the pair loop scales with projectiles x NPCs).

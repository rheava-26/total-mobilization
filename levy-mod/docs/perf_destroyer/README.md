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

The in-game clock moved 10 minutes in 14 s of video, i.e. 600 ticks in 14 s = 43 updates per second, about 23 ms per update, while the frame rate was 2. That is the signature of the update being slower than 16.7 ms, not of a slow draw: the engine runs up to 30 catch-up updates between two draws (it clamps the backlog at half a second), so once an update costs more than 16.7 ms the game does 30 updates, one draw, 30 updates... and FPS is about UPS / 30. This lab measures 14 ms (CPU time of a server tick, no client work) where the video implies 23 ms, a factor 1.6 that a laptop and the client-side extras (dust, lighting, sound, the player) account for. To get a game under 16.7 ms the army's increment has to come down by about 40%, which the changes below do not reach (see the options). `LEVY_PROF=1` on a client now logs one `[Prof] client` line every 2 s (fps, ups, ms per update split NPC / projectile / rest, ms per frame of draw split NPCs / projectiles / dust / gore / players / interface / levy bodies / nameplates): one fight on the machine that ran at 2 FPS settles update against draw. The draw side could not be measured here (headless).

## What changed (all behind `LEVY_OPT=0` for A/B, none changes what a levy does)

* Dodge (`ThreatField.Hits`, `SoldierNPC.Consider`): a candidate maneuver scores `hits x 100 + penalty (+ a non-negative worm term)` and only wins by scoring under the best so far, so the hit count now stops as soon as the candidate cannot win. `LEVY_CUTCHK=1` counted every candidate to the end as well: 207 000 candidates, 87% cut off early, 0 verdicts different. dodge 0.66 -> 0.39 ms, `hits` 0.47 -> 0.20 ms, fewer spikes (ticks over 22 ms after the first ten seconds: 187 -> 135 per fight).
* Stat refresh of a levy is phased by body index instead of all landing on the same tick (a 14 ms tick every second for an army spawned together).
* Gear frame: the saved projectile counts are sparse (only the non-zero entries are saved and restored, the array has thousands of entries), and the per-frame timing is lab-only.
* `CanHitNPC` of `LootCreditGlobalProjectile` folded into `SoldierProjectileTag.CanHitNPC`: one hook and one tag lookup per pair instead of two.
* `PairCull` (off by default, `LEVY_PAIRCULL=1`): an army shot with the plain hit test refuses an NPC out of any hitbox's reach inside Levy's own `CanHitNPC`. With `LEVY_PAIRCHK=1` it audited 99.6 million refused-to-be pairs: 0 would have hit, and no hit ever landed beyond its plain reach (0 px). It refuses 35% of the pairs and saves about 2% of the tick (the dispatch up to Levy's hook, which comes after Calamity's, and the vanilla loop are the cost), so it ships off. If Levy's hook ran first in the dispatch (`sortBefore = CalamityMod`, a load-order change that touches every hook pair of the two mods) it would save more.
* Tooling: Destroyer boss, `drones=` / `turrets=` / `arena=a3w` in the Calamity harness, `LevyProf` buckets (gear frame parts, NPCs and projectiles by owner, per type, hit loop, pairs, hits), CPU-time census with allocation, GC and spike ticks, `ClientProf`, `lab/perf/` (EventPipe profile scripts), `perf_summary.py`, `perf_buckets.py`.

## Before / after (same seeds, 3 each)

| build | 36 levies (ms per tick) | ticks over 22 ms after 10 s | 1 levy | no summon items |
|---|---|---|---|---|
| original code (wall) | 15.3 | | 2.2 | 12.5 |
| `LEVY_OPT=0` (CPU, profiled) | 15.8 | 187 | | |
| this branch (CPU, profiled) | 14.2 | 135 | 2.7 | 11.8 |

A process is worth about +-1 ms in this lab, so the gain (7 to 10%) is real but small; the dodge buckets, which are measured inside one process, show the part the fixes touch.

## Options that need a design decision (not done)

1. Gear-frame cadence: a frame every second tick for levies with Calamity gear when the army is large saves 1.4 ms, but every per-tick timer in that gear (cooldowns, stealth regeneration, Volatile Gelatin) runs at half rate.
2. The Caustic Staff rule (every non-summoner casts a minion) is about 2.7 ms of the 14.
3. Fewer projectiles per levy (the pair loop scales with projectiles x NPCs).

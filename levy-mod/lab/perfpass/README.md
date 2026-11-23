# Perf pass (11-22): benchmark, what costs the time, what changed

## The benchmark

`mkbench.py` writes three specs into `specs/` (raw harness fight lines, run through `levylab`):

- `bench.tsv`: the benchmark. Her real armies, 40 regulars each, wearing her gear: `armies/eve_exo.json` (her 11-21 17:19 Exo Mechs fight, from exolab) and `armies/eve_polter.json` (her 11-21 12:58 Polterghast fight, rebuilt with `exolab/mkeve.py` from the recording). Three busy endgame scenes: the Exo Mechs (Ares first) with the Exo army, Providence (the projectile storm) and Polterghast with the Polterghast army. Calamity, Master+Death, arena `big`, commander far, order Free Rein, 40 s, 2 seeds. `dr=0.9` (every hit on a levy cut by 90%) keeps the army alive, so the population and the work stay the same for the whole measured span.
- `fights.tsv`: her fight scenarios as they really go: the same three scenes without `dr`, 60 s cap.
- `smoke.tsv`: 15 s of each scene.

Run (private lab, one server at a time):
```
LEVY_CROWD=1 LEVY_CR_IMMCENSUS=0 LEVY_PERFAN_SERVER=1 LEVY_PERFAN_ECHO=1 \
LEVYLAB_HOME=<private> LEVYLAB_SRC_TML=/home/user/tml-bin/tml-2026.10 LEVYLAB_SRC_SAVE=/home/user/tml-save-2026.10 LEVYLAB_WORLD=<ShipWorld.wld> LEVYLAB_PORT=<port> \
levy-mod/lab/levylab run levy-mod/lab/perfpass/specs/bench.tsv --jobs 1 [--rev REV]
python3 levy-mod/lab/perfpass/bench.py RUN_DIR [RUN_DIR ...] [--merge] [--top]
```
What `bench.py` reads per fight (first 6 s skipped): thread CPU ms per tick of the whole entity update and of the NPC and projectile phases, allocation per tick, gen0 GCs and GC pause (`[Crowd]` census); the always-on analyzer's windows on the server (`[PerfWin]`: Levy's share = levy bodies + army shots + Levy's global AI hooks, Levy's own buckets on one update in 66, per-weapon rows, the top cost rows); and behaviour from `out.tsv` (damage and hits on the boss, deaths). `LEVY_CR_IMMCENSUS=0` leaves out the harness's per-shot immunity census, which was 27% of a lab tick.

Caveats. A dedicated server builds network packets that her single-player game never sends (`NetMessage.SendData` was about 7% of a lab tick, all of it server-only); the lab's absolute numbers are therefore higher than hers and Levy's share in the lab (80-95%) is not her share (38-55%), which includes drawing and her other mods. Compare arms of the same lab, not lab against her log. Fights are chaotic: one run of 2 seeds moves by 5-10% from the next, so the tables below pool 3 runs (6 fights) per arm.

Sampling profiles: `lab/perf/profile.sh` style, EventPipe, with the `nettrace` tool extended to write allocation-tick stacks (`<trace>.alloc.txt`, the first non-runtime frame that allocated, filtered to the game update). Built with `/home/user/dotnet10`.

## What her logs say (OBSERVED, `levyperf-2026-11-21.jsonl` and `-22`, 198 combat windows with 30+ levies)

Mean per window: 44.8 fps, 13.9 ms update, 9.0 ms draw, 41.6 levies, 394 projectiles.

| # | cost | ms | where |
|---|---|---|---|
| 1 | Calamity's own projectiles (bosses' and everyone's) | 3.51 /update | not Levy |
| 2 | levy bodies (SoldierNPC update: AI 2.66, of which the gear frame 1.82; the rest vanilla NPC update and other mods' hooks on levies) | 3.33 /update | Levy |
| 3 | army shots (the weapons' own projectile AI, hit passes, Levy's wrappers) | 2.31 /update | Levy + weapons |
| 4 | drawing levies (player renderer per levy, nameplates) | 1.58 /frame (up to 5) | Levy |
| 5 | drawing army shots | 0.98 /frame (one window 10) | weapons |
| 6 | vanilla NPCs | 0.58 /update | not Levy |
| 7 | the info-accessory bar (vanilla UI) | 0.48 /frame | not Levy |
| 8 | Calamity's global AI hooks | 0.40 /update | not Levy |
| 9 | Levy's global AI hooks | 0.37 /update | mostly the analyzer's own wrap (see below) |
| 10 | single projectile types: Calamity's ScarletDevilBullet (her own weapon), FlareDust, HolyBurnOrb, SoldierMinion... | 0.1-1.4 /update | |

The weapon rows: the "9 ms in Polterghast and Exo" was the whole army's weapon total (`wpa`), not Onyxia and Vega alone; Onyxia and Vega led with about 1 ms each. Per levy holding it, Onyxia is the dearest (0.65 ms per levy-update against 0.12-0.40 for the rest). The lab says why: Onyxia fires Luminite Bullets, `extraUpdates = 5` (six updates a tick, infinite pierce), so each bullet runs Levy's per-update wrappers six times a tick. The army sweep (`IdentityWorldCheck` army cost, Onyxia@Luminite) measured 104-190 us per shot for a levy against 77-92 for a player holding the same weapon; Vega, Ghoulish Gouger and SDFMG are within 15% of the player.

## Profile of the benchmark (OBSERVED, Providence scene, before)

Of the update: projectile phase about 45% (Levy's own wrapper frames on army shots about 12% of the tick: use guard snapshots, owner puppet, stealth guard, the AI and hit-pass detour chain), levy bodies about 37% (gear frame 25%: Calamity's PostUpdateMiscEffects 5.5%, UpdateEquips 2.7%, ResetEffects 1.2%, the approximator's imaging 4%), harness and server artifacts the rest. Levy bucket timers (LEVY_PROF, ms/tick): stats 3.9 (g.misc 1.25, g.sets 0.83, g.equips 0.58, g.reset 0.37, g.post 0.30, g.copy 0.23), move 0.38, combat 0.50, brain 0.25, dodge 0.09, near 0.03; proj.army 7.9, npc.levy 6.5. Allocation 550-850 KB a tick; the biggest Levy allocators were closures made on every projectile AI call by four detours, a `bool[]` per use-guard snapshot, reflection boxing (Calamity rage/adrenaline, lifesteal rules, team flags), a debug string built every tick per levy, and gear-frame lambdas.

The analyzer's "glob Levy" row (0.4-1.4 ms) is mostly its own clock reads around each wrapped global hook on sampled updates: the sampling profile puts every Levy global AI hook together at about 0.5% of the tick.

The fight recorder: about 0.9% of a tick in the profile (FightRecorder.Sample and its frame formatting).

## What changed (no behaviour change; commit fdab0c7)

- `LEVY_PROJSLOTS` read once, not per (hostile shot, levy) pair (1% of the tick).
- Four AI detours no longer allocate a closure for every projectile AI call (`ProjAiHooks.Wrap`). Note: a method-group delegate over `orig.Invoke` made coreclr segfault under the EventPipe sampler, so the lambda form is kept.
- Use-guard snapshots keep the mouse-listener flags as bits.
- Gear-frame hooks are static delegates; Calamity rage, adrenaline, modes, stealth and the CalamityPlayer lookup use compiled accessors (`Common/Util/FieldAccess.cs`); lifesteal rules read once a tick; team-item mod flags compiled; a tuple key instead of a string concat; the movement debug string formatted only when read.

## Before and after (pooled: 3 runs x 2 seeds per scene; before = 57c5a09, after = fdab0c7; box load 1.6-6 during the runs, recorded per run in `loadavg.txt`)

| scene | CPU ms/tick before | after | alloc KB/tick before | after | gen0 GCs per 10 s before | after | dmg to boss before | after | hits before | after |
|---|---|---|---|---|---|---|---|---|---|---|
| Exo (Ares first) | 19.22 | 19.67 | 552 | 342 | 2.6 | 1.6 | 2.93M | 3.39M | 27.5k | 24.5k |
| Providence | 16.95 | 15.97 | 635 | 386 | 2.9 | 1.8 | 1.36M | 1.34M | 8.8k | 8.7k |
| Polterghast | 20.72 | 20.66 | 782 | 474 | 3.7 | 2.6 | 1.39M | 1.38M | 4.8k | 4.6k |

Her fight scenarios (no damage cut, 2 seeds each, one run; loads 2.0-3.8): Exo 14.95 -> 13.56 ms/tick, alloc 392 -> 231 KB; Providence 15.17 -> 14.46, 536 -> 340; Polterghast 17.61 -> 18.10, 636 -> 419.

Reading: allocation per tick fell 38-43% and gen0 collections 30-40%; CPU per tick moved -6% to +2%, inside the run-to-run spread. The pass removed waste but did not remove a big block of CPU: what is left is structural (below).

## [PerfPass] world check

`Common/Testing/PerfPassCheck.cs`: 24 levies (four kits, proc accessories, modded accessories when a gear mod is loaded) against a pinned boss-flagged target with 250 hostile seeds over them; control arm without the army. Measured on the pre-pass build (two runs each): added cost 3.8-4.7 ms/tick (Calamity), 1.8-2.1 (vanilla); shots 5.26 (Calamity) / 5.14 (vanilla) per levy-second, target hits 2.00. After: 4.06 ms, shots 5.25, hits 1.99 (halves 766/746 shots against 766/748 before).

## Needs a design decision (not done)

1. The gear frame's tail (Calamity's PostUpdateMiscEffects and PostUpdateEquips for every fighting levy every tick, about 1.5 ms at 40 levies in the lab): a setting to run it every N ticks would cut it, at the price of rage, stealth and cooldowns stepping in N-tick jumps. The build half already has such a setting (default 4).
2. Per-update wrappers on high-`extraUpdates` shots (Luminite Bullets: six updates a tick): the use guard, stealth guard and owner puppet open and close on every inner update and hit pass. One scope per tick per shot is not provably identical (the commander's use state would be restored later inside the tick), so it was not done. It is the reason Onyxia costs a levy twice what it costs a player.
3. Consolidating Levy's six Projectile.Update detours and eight AI detours into one dispatcher: a large refactor across files the summon and ammo sweeps edit (HeldWeaponSwap, SoldierMinions, UseGuard, OwnerPuppet); about 3-5% of a busy tick by the profile.
4. Calamity's per-player walks (OtherBuffEffects, MiscEffects' `Any`) inside every levy's frame: only reachable by patching Calamity (the pattern of `CalamityLevyPatches`).
5. Drawing (1.6-5 ms a frame of levy drawing in her log): a headless server cannot draw and the box has no Terraria content to run a client, so nothing on the draw side was changed or measured. `/levyprof` on her machine splits levy body drawing from nameplates (ClientProf.SoldierBody / SoldierPlate); "Lower levy effects" already hides nameplates.

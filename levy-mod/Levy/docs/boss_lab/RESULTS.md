# Boss lab results (branch `boss-lab`, overnight 2026-11-07)

Recovery file: read this, `LOADOUTS.md`, `notable_index.txt`, `main_table.txt`, and `git log` on `boss-lab`.
Harness: `Common/Testing/CombatHarness.CalRev.cs` + `CombatHarness.CalRevLab.cs` (scenario `calrev`, Calamity 2.2.2, tML 2026.10). Raw rows: `raw/*.tsv` (one row per fight, 60+ columns; `*.dbg.tsv` = 30-tick boss traces). Specs and run scripts: `scripts/`. Generators: `gen_specs.py`, `tab.py`, `count_an.py`.
Replays: `/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/replays/notable/` (index.txt there and `notable_index.txt` here). All 430+ recordings: `/home/user/lab/rec` (gz).

## 0. Conventions and builds
- Result: `WIN` = boss (all worm segments) killed; `DESPAWN` = boss left without dying (counted separately, never a win); `TIMEOUT` = cap (300 s unless noted); `LOSS` = all levies dead.
- Boss target %: sampled every tick over every boss head (L = a levy via slot 255/300+, P = a player slot = the Spectator commander, N = none). `addTgt` = the same for adds (servants, creepers).
- Levy count `n=cp` = the real command-point count for that fight from Levy's tier math (defaults: start 10, Wall of Flesh 20). Boss Checklist positions: KS 1, DS 1.6, Eye 2, Crabulon 2.7, Eater/Brain 3, Hive Mind 3.98, Perforators 3.99, QB 4, Skeletron 5. Result: **KS 10, DS 11, Eye 11, EoW/Brain 13, QB 15, Skeletron 16** (the Calamity bosses beaten count too, via the unweighted share, so QB/Skeletron are 15/16, not the 14 a vanilla-only world gives).
- Levies are Levy-grade (1 command point, 1 accessory slot); gear = tier tables in `LOADOUTS.md`. `gear=van` = the old vanilla-only rolls (35% Man-at-Arms at no CP cost).
- `cmd=far` (default for the lab): the commander (Spectator, pinned, immune) stands outside the arena. `cmd=near` is the old harness position (inside the levy cluster). See the targeting finding, section 1.
- Builds: Levy 2.0.12 + harness: smoke, dbg_eye, e0, s1_main, b1b, b2_eye, b4_ug. Levy 2.0.13 (mobility round 3, a023bc0): everything started after 05:09 (b3_*, b2_brain, b5_brain, b1a_far, b1_far13, b2_eye13, near13, death_more, wormsx, worms_fix, eye_phase*, e16v19, big*, anom*). Duplicate cells across builds are deduplicated in `main_table.txt` (the 2.0.13 row wins). Servers use private save dirs (`/home/user/lab/save{1,2,3}`), not the shared tml-save-2026.10.
- 3 seeds minimum; most key cells have 6, the Eye count cells 6-14.

## 1. The Calamity targeting caveat (Rev Brain / Rev Eye) - observed
Question: do the old calrev Rev results still hold now that `CalamityTargetingCompat` exists?
- **Old Rev numbers do not reproduce as such, and the cause is the Spectator commander, not a frozen AI.** Brain Rev, old vanilla gear, near commander (this build): n10 1/6 wins (tgt L23/P77), n12 2/6 (L42/P58), n16 1/6 (L58/P42). Old data: 0/3, 5/6, 6/6. Eye Rev n10 0/3 (L49/P51), n16 1/3 (L82/P18).
- **Cause (observed from the Spectator diagnostic, `diag=1`):** `Compete 118x last=player (vanilla pick (player 0) closer: levy 1718 >= player 950)`, `hidden-window used 0x`. The commander is a valid pick (the Spectator is NOT hidden in these searches although `hides spectator from it=True`), Calamity's override only re-searches when its target is invalid, and Levy's soft weighting then keeps the commander whenever he is closer than the nearest levy. The Eye sits on top of the commander (x constant at his x, bobbing in y) for hundreds of ticks (the "AI-stuck Eye").
- Brain (inferred from `OnSpawn` in `BrainOfCthulhuAI.cs`): its spawn runs `CalamityTargeting` before Levy's PreAI, picks the commander, and places the Brain at `target.Center +-900 px`; with the commander parked far from the levies the Brain is next to him, levies are farther, so `Compete` keeps the player (stuck in `ai0=7` with `lock=-1` for 100+ s in one recording). With `cmd=far` Brain heads were L65-91%.
- Effect on results (observed): cmd=near Eye Rev n11: 0 wins, 3/3 TIMEOUT, boss on commander 54% of ticks; cmd=far same army: 2/9 wins, 6 wipes, L96%. So near-commander cells over-protect levies and hide how lethal Rev is. The lab main matrix uses `cmd=far`; `near13.tsv` has the near counterparts.
- Servants of Cthulhu (Eye adds) and Brain creepers target the commander far more than the boss does: Eye addTgt P=100% in every cell, Brain creepers L83-92% at cmd=far but P64-79% at cmd=near (matches mobility3's "adds chase the player").
- Not fixed (measurement only). Suggested fix for spectator-fix: hide the Spectator in the Calamity search AND in `Compete` for a downed boss, and re-run `Retarget` when the Calamity override keeps a player target while `lock == -1`.

## 2. Levy count and the 16 vs 19 question
Eye of Cthulhu, tier-2 gear, `cmd=far`, wins / kill time of wins / levy deaths (6-14 seeds per cell):

| n | Expert wins | Expert ttk s (mean/median) | Expert deaths | Rev wins | Rev ttk s | Rev deaths |
|---|---|---|---|---|---|---|
| 4 | 2/6 | 151 | 3.2 | 0/3 | - | 3.7 |
| 8 | 6/6 | 112/115 | 1.8 | 0/3 | - | 6.7 |
| 11 (real max) | 6/6 | 96 | 1.7 | 2/9 | 209 | 10.3 |
| 12 | 6/6 | 55/49 | 1.0 | 3/3 | 157 | 2.0 |
| 16 | 14/14 | 47.8/46.0 | 0.9 | 9/14 | 168/149 | 10.4 |
| 19 | 14/14 | 42.0/41.2 | 1.2 | 14/14 | 154/147 | 9.6 |
| 24 | 6/6 | 38.4/39.4 | 0.3 | 3/3 | 144 | 11.0 |

Death mode Eye (n11 2/9 wins, n16 4/6, n19 6/6 from `big1`); Rev n16 4/6, n19 6/6 in the first 6-seed batch.
- **16 vs 19, observed:** a real but modest difference. Expert: 47.8 s vs 42.0 s (-12%, 14 fights each, deaths unchanged). Rev: wins 9/14 vs 14/14 (64% vs 100%), Death: 4/6 vs 6/6; kill time of the winners about equal (168 vs 154 s). In Rev/Death 19 buys SURVIVAL (win rate), not speed; casualties are about 10 either way (63% vs 50% of the army).
- **Why kill time flattens (measured):**
  - (a) Boss HP scaling (+2% of base per engaged levy, `BossRule.OnSoldierDamagedNpc`): explains most of it. HP(16)=1.32x, HP(19)=1.38x, HP(24)=1.48x. Predicted 16->19 speedup from count x HP = 0.88; measured 42.0/47.8 = 0.88. With scaling off (`hpscale=0`, 6 seeds): ttk 94.9 (n8), 40.5 (12), 34.6 (16), 39.8 (19), 19.4 (24): still noisy, still improving at 24.
  - Per-levy damage while the boss is hittable is flat for n>=12: 7.0, 6.5, 6.3, 5.8 dmg/s per levy at n=12/16/19/24 (Expert). No DPS cap.
  - (b) shared immunity frames: ruled out for ranged shots. Identical Boomstick levies against a x40-HP Eye with invulnerable levies (`b3_clone`, 60 s): total dmg/s 12.0, 17.5, 35.5, 62.4, 77.0, 90.2, 103.4, 144.1 for n=1,2,4,8,12,16,19,24 and hits/s 2.6 -> 25.4: keeps scaling to 24, per-levy 5.4-6.4 dmg/s from n=12 on. Per-levy efficiency does drop from 12 (n=1) to 7.8 (n=8): range/aim/travel overhead (c), then flat.
  - (c) range/crowding: per-levy damage halves from 1 to 8 levies then stays flat, so crowding costs a one-time factor ~2, not a plateau. Mages (Vilethorn clone): 2.3 -> 11.3 total, per-levy 2.3 -> 0.47 (20-mana pools: ~0.5/levy once n>=8). Summoner (Finch Staff) 2.5 -> 14.3 total.
  - (d) overkill: not tested separately; kills at 20-45 s with 3640-5000 HP are not overkill-limited (inferred).
  - (e) projectile caps: hits/s kept growing to 25/s; not hit (inferred from the clone curve).
  - Fixed per-fight overhead (Eye approach, servants, phase 2, ~15-20 s) is the remaining reason that 24 levies are not 2x faster than 12 (inferred).
- Rev/Death Eye ttk is flat (~140-170 s) for n=12..24 because the fight is attrition- and phase-gated: ~10 levies die regardless of n (levy-count only changes the survivors).
- Melee note: the clone test with `UrchinMace` (class E) dealt 0 damage in 60 s to the hovering Eye at every n (a melee levy cannot reach it; the Eye hovers 400 px above the commander/levy). In the mixed rolls melee levies contribute 0-600 damage.

Brain of Cthulhu count curve, `cmd=far`, tier-3 gear:

| n | Expert wins | Expert ttk | Rev wins (surface) | Rev ttk | Rev deaths |
|---|---|---|---|---|---|
| 4 | 2/3 | 116 | 0/3 | - | 4.0 |
| 8 | 3/3 | 144 | 0/3 | - | 8.0 |
| 12-13 | 9/9 | 44-52 | 1/12 | 174 | 12.2 |
| 16 | 3/3 | 51 | 0/9 | - | 14.7 |
| 19 | 3/3 | 35 | 6/15 | 219 | 12.6 |
| 24 | 3/3 | 27 | 4/9 | 237 | 16 |
Brain Rev has a cliff between 16 and 19 levies, not a plateau: 0/9 vs 6/15 (surface) and 1/9 vs 4/9 at 24.

## 3. Real-count results (tier gear, cmd=far, Levy 2.0.13; wins / deaths / ttk)
`main_table.txt` has every cell. Summary (seeds in brackets):

| boss | mode | n | kill/despawn/timeout/loss | deaths | ttk (wins) | levy hits on boss | boss target L/P % |
|---|---|---|---|---|---|---|---|
| King Slime | Expert | 10 | 0/0/6/0 (6) | 4.0 | - | 352 | 100/0 |
| King Slime | Rev | 10 | 5/0/1/0 (6) | 4.8 | 182 | 666 | 100/0 |
| King Slime | Death | 10 | 2/0/1/0 (3) | 4.7 | 198 | 638 | 100/0 |
| Desert Scourge | Expert | 11 | 0/0/0/6 (6) | 11 | - | 334 | 100/0 |
| Desert Scourge | Rev | 11 | 0/0/0/6 (6) | 11 | - | 342 | 100/0 |
| Desert Scourge | Death | 11 | 0/0/0/3 (3) | 11 | - | 396 | 100/0 |
| Eye | Expert | 11 | 3/0/0/0 (3) | 1.0 | 84 | 769 | 100/0 |
| Eye | Rev | 11 | 2/0/1/6 (9) | 10.3 | 209 | 824 | 96/4 |
| Eye | Death | 11 | 2/0/0/7 (9) | 9.3 | 187 | 852 | 96/4 |
| Eater of Worlds | Expert | 13 | 0/0/0/6 | 13 | - | 2093 | 100/0 |
| Eater of Worlds | Rev | 13 | 0/0/0/6 | 13 | - | 1648 | 68/32 |
| Eater of Worlds | Death | 13 | 0/0/0/3 | 13 | - | 669 | 98/2 |
| Brain | Expert | 13 | 6/0/0/0 | 0.0 | 57 | 700 | 100/0 |
| Brain | Rev | 13 | 1/0/0/8 (9) | 12.2 | 175 | 72 | 84/16 |
| Brain | Death | 13 | 0/0/1/2 (3) | 12.3 | - | 12 | 89/11 |
| Queen Bee | Expert | 15 | 0/0/0/6 | 15 | - | 296 | 100/0 |
| Queen Bee | Rev | 15 | 0/0/3/3 | 13.7 | - | 897 | 93/7 |
| Queen Bee | Death | 15 | 1/0/1/1 (3) | 10.7 | 249 | 892 | 92/8 |
| Skeletron | Expert | 16 | 4/0/2/0 | 11.7 | 161 | 1442 | 100/0 |
- Eve's expectation "Death EoC brutal but winnable with heavy losses": observed at the real count (11): winnable 2/9, 9.3 of 11 dead. At 16 it is 4/6 and at 19 6/6 wins with 10 deaths. Brutal yes, winnable only with more levies than the progression allows at that point (11).
- Skeletron Rev/Death not run: Calamity overrides Skeletron only in single player (`CalamityVanillaAIOverrideNPC.cs:165`), so a dedicated server runs vanilla Skeletron. Queen Bee Rev has `CurrentlyEnraged` for ~278 s of the fight (enrage flag observed, not investigated).
- Death Brain: boss damage 0.2/s; same shield story as Rev (section 5).
- "No kill with kill despawn": one DESPAWN (King Slime Rev seed 2 in the first batch, rerun later gave a timeout); never counted as a win.

## 4. Worms (observed)
`cmd=far`, tier-3 gear for EoW (13 levies), tier-0 for DS (11 levies, desert zone set so the DS enrage timer stays off):

| boss | mode | arena | n fights | wins | levy deaths | avg levy death time | dmg head/body/tail | boss left (avg) |
|---|---|---|---|---|---|---|---|---|
| EoW | Expert | surface | 7 | 0 | 13/13 | 39 s | 1841 / 10902 / 438 | 34% |
| EoW | Expert | underground | 6 | 1 (64 s) | 12.5/13 | 40 s | 2364 / 11909 / 378 | 24% |
| EoW | Rev | surface | 6 | 0 | 13/13 | 32 s | 547 / 6445 / 106 | 71% |
| EoW | Rev | underground | 6 | 0 | 13/13 | 34 s | 634 / 6871 / 152 | 69% |
| EoW | Death | surface | 3 | 0 | 13/13 | 9 s | 127 / 963 / 67 | 94% |
| DS | Expert | surface | 7 | 0 | 11/11 | 15 s | 774 / 1619 / 133 | 60% |
| DS | Expert | underground | 6 | 1 (52 s) | 10.8/11 | 15 s | 749 / 2061 / 142 | 53% |
| DS | Rev | surface | 7 | 0 | 11/11 | 21 s | 836 / 2455 / 142 | 57% |
| DS | Rev | underground | 6 | 0 | 11/11 | 21 s | 766 / 2349 / 133 | 59% |
| DS | Death | surface | 3 | 0 | 11/11 | 25 s | 806 / 2943 / 145 | 51% |
- Eve's expectation (DS massacres, EoW milder) is NOT what the harness shows: EoW is as lethal as DS (13/13 dead in 30-70 s in Expert, 9-20 s in Death). EoW takes more damage (levies chew 62-76% of its total HP in Expert before dying; with 65 segments there is always one in reach) but never loses. DS head HP 6400-8000 scaled; levies take ~45% off it at best before the wipe.
- Underground vs surface made no outcome difference (0/7 vs 1/6 EoW Expert, 0/7 vs 1/6 DS Expert, all other cells 0 wins). Eve expected the underground to differ; nothing in the data supports that. Both arenas are enclosed boxes with the boss free to dig; the only AI-visible difference is `worldSurface` checks and zone flags (inferred).
- 70-85% of levy hits land on BODY segments (head 15%, tail 3%).
- **Damage source ("takenOther") explained:** it was my attribution bug (only the head type counted as the boss; body/tail segment contact fell into "other"). Fixed: `worms_fix` rows show every worm cell with takenOther 0 except a residual of 100-320 damage per DS fight with `attacker = none` (LastAttackerType -1; 5-11% of DS damage, source not identified: a projectile or environment hit without a source NPC). EoW damage is 100% `Eater_of_Worlds`; DS is body 64-81%, head 14-30%, none 5-11%. Per-source column: `dmgSources` in the raw rows. Replays: `notable/` (DS Expert surface + EoW Expert surface from worms_fix).
- Early DS deaths (3-10 s after contact): the head and body drive through the levy cluster (`alive` falls 11 -> 4 within 90 ticks in the debug trace); contact damage 64+ per hit vs 100-200 HP levies.

## 5. Brain of Cthulhu Rev, surface vs underground (observed + source)
- **Enrage condition (source, Calamity 2.2.2 `BrainOfCthulhuAI.cs`):** there is NO enrage flag for the Rev Brain: `CurrentlyEnraged` is never set for it anywhere in the source (only Skeletron, WoF, Plantera, Empress, DS, QB-like fights and others set it). The harness `enragedSec` was 0.0 for every Brain cell. What differs above/below `Main.worldSurface` (line 314/852/2257): (1) spawn animation (`ai0` 1 = Surface, 0 = Underground) and spawn offset (target +-900 px), (2) creeper swipe angles are flattened on the surface ("creepers do not go as high"), (3) the Crimson-eye charge speed is 10 px/tick on the surface vs 8 underground. It despawns if the target is dead or NOT in the Crimson (`!Target.ZoneCrimson`, also checked through `ValidateTarget`); the harness sets ZoneCrimson in both arenas. Eve's "Rev BoC enrages above the surface" is therefore a speed/animation difference at most, not an enrage.
- **Outcome (observed, `cmd=far`, 13 levies):** surface Rev 1/12 wins (L84%), underground Rev 1/6 (L79%); Expert Brain wins 6/6 and 6/6 in both. At 19 levies: surface 6/15, underground 1/9 (5 timeouts). No reliable surface/underground effect; the underground 19-levy cell timed out more (stuck 200+ s in `ai0=7` Telekinetic Onslaught with the target = commander in two recordings).
- **Why levies deal ~0 damage to the Rev Brain (answer to the lead):** it is a race, not a bug.
  - dontTakeDamage = 91-100% of the fight in every LOSS (22-33% in the three winning fights). The Brain is invulnerable behind its creeper shield: 18 creepers at the start (Rev), up to 18 again after each recovery.
  - Creepers: kills 0.0-0.4/s. Typical wipe: levies alive 13 -> 1 in 36-70 s while creepers go 18 -> 5..17. Only 5 of 12 recorded fights cleared all 18 (first zero at 28-41 s); in 4 of them the creepers RESPAWNED to 18 within ~8-10 s after the stun window (observed: `creeperRespawnEvents` 2 in 5 fights), i.e. the shield rebuilds until the Brain's health gate (70%, `Phase2HealthGate`) is passed.
  - Phase timing (ai0 sequence from the traces): spawn animation 15 s, Phase1Idle, CreeperSwipes (3), CreeperSwings (4), Orbit (5), Spiral (6), TelekineticOnslaught (7), then Stunned (8, 360 ticks = 6-7 s): the only hittable window (the boss took 118-1500 dmg in that window); after 70% it enters phase 2 transitions (9/10) and phase 2 (11+). The wipes happen before or during the first cycle (36-100 s).
  - Creeper damage taken is counted separately (`takenAdd`, `dmgAdd` 4-7k per fight); creeper hits are in `dmgAdd`, never `dmgBoss`.
  - The recorder only stores boss parts, so creepers are not in the replays. I did not add them (the harness would have to edit `Common/Recording`); the debug traces (`*.dbg.tsv`: `adds=` per 30 ticks) are the creeper record.

## 6. Anomaly list (observed -> explanation)
1. **Eye Rev boss target 50% player, boss "stuck"**: commander-closer rule + Spectator not hidden + Calamity only re-searches on invalid target (section 1). Rerun with `diag=1` + `dbg=1` (`dbg_eye.tsv`). Real mod issue (Eve's spectator bug), amplified by the harness commander position.
2. **Brain Rev dmg/s 0.0 / 3 s of levy contact**: invulnerable Brain, creeper shield race (section 5). Not a bug.
3. **DS Rev: 11 levies dead in 3 s** (b1b seed 2, build 2.0.12, not reproduced on 2.0.13 where the same cell lasted 61 s; fast wipes of 7-12 s occurred in b4_ug/worms rows): the worm reaches the cluster 3 s after spawn; contact damage 64+. Explained, real.
4. **"takenOther" large in worm rows**: harness attribution bug (body/tail types), fixed; see section 4. Also the first DS rows had `bossMax=134080` (sum of Calamity worm segments that share the head's life via realLife); fixed in later rows. Rows before worms_fix (b1b, b4_ug, wormsx, death_more) still have the old attribution and DS bossMax/bossLeft in the raw `bossLeft` column are the head's life only after the fix; use `dmgHead/dmgBody/dmgTail` and `result`.
5. **EoW Rev boss target P up to 43%** (underground): same commander rule as section 1 for the EoW head; it still killed everyone.
6. **King Slime Expert: 0/6 wins and 6 timeouts at ~6 dmg/s with 10 levies, while Rev wins 5/6** (Rev ttk 182 s). Debug (`anom2`): hits 264-352, adds killed 19/fight; levies spend their time on the spawned slimes (addTgt L100%) and the boss is hit rarely; the Rev Calamity KS AI jumps/teleports differently. Cause not isolated (inferred: slime-add distraction + vanilla KS Expert AI keeps the boss off levy range).
7. **Queen Bee Expert wipes 15 levies in 40-100 s (0/6 wins)** while Rev reaches 278 s of enrage: bees swarm (39 add kills/fight, 1.0/s); QB damage in the first 15 s is 600+ but the army dies first. Not investigated further.
8. **UrchinMace clone dealt 0 damage vs the Eye (60 s, every n)**: melee reach vs a hovering boss (inferred; the mixed melee rolls do hit the Eye in real fights).
9. **cmd=near vs far changes outcome class** (Eye Rev timeouts vs losses): flagged in section 1; every cell lists `cmd` in the key (`|cfar`).
10. **Boss `ai[0]` is meaningless for KS (a-200..) and worms**: the phase column buckets by `ai[0]`; only trust it for Eye/Brain/QB.
11. **Count 11 vs 12 for the Eye**: Rev Eye wins 2/9 at 11 vs 3/3 at 12 (single seeds) and the 16/19 cells above: strongly count-sensitive, the real max (11) sits on the cliff.
12. **No despawns counted as wins**; one King Slime Rev DESPAWN was seen and reported as such. No 0-hit fights except Brain Rev/Death (invulnerable by design) and the UrchinMace clone.

## 7. Observed vs inferred (summary)
- Observed: every number in the tables; the Compete diagnostic text; dontTakeDamage 91-100%; creeper counts and respawns; per-levy DPS curve; the HP-scaling arithmetic (computed from the config +2%/CP and matched by measured ttk ratio 0.88); damage-source breakdown.
- Inferred: why melee clone = 0; KS Expert slowness; QB Expert lethality; the Brain's OnSpawn target path; the "none" damage residual source; fixed overhead (approach time) as the rest of the flattening.
- Not covered: Skeletron/QB Rev (Skeletron Rev needs single player), Master mode, the Death mode of QB beyond 3 seeds, Death/Rev with `cmd=near` for worms.

## 8. Status log
- 04:30 setup (private installs `/home/user/lab/{tml1,tml2,tml3,tmlb}`), 04:42 first build, 05:09 merged Levy 2.0.13, 05:34 added phase/hittable columns, 05:45 damage-source columns. Everything is on `origin/boss-lab`.

# Boss trials: can levies beat King Slime, the Eye of Cthulhu and the Brain of Cthulhu?

Measurement only. Harness: `Common/Testing/CombatHarness.BossTrial.cs` (scenario `bosstrial`), vanilla weapons, booted with `["Levy"]`, Levy 2.0.5. Generators and analysis: `docs/boss_trials/genspecs.py`, `analyze.py`. Raw rows: `docs/boss_trials/raw/` (one TSV per round, plus `.audit.txt`, `.cfgs.tsv`, `.bossstats.txt`). Tables: `docs/boss_trials/tables_corrected.md`, `tables_prefix.md`; CSV in `docs/boss_trials/csv/`.

Not run: Death Mode / Revengeance (needs the Calamity world flags on the 2026.10 toolchain; not attempted). Master mode, Calamity bosses and Thorium weapons are not covered.

## 1. Fidelity audit (read this first)

Each item is confirmed-faithful or found-different. "Corrected build" means the harness after the fixes in this section; rounds A-G ran before them (see section 3).

| # | Question | Verdict | Evidence |
|---|---|---|---|
| 1a | Eye runs its full AI | confirmed-faithful | audit counters per run: phase 2 starts at about 50% life (`ai[0]` 0 to 1 to 2 to 3), 12-15 dashes in Classic with 4 levies, 26-31 in Expert, 4-9 Servants alive over a run, 20-29 Servants spawned in Expert. |
| 1b | King Slime runs its full AI | confirmed-faithful | 33-44 slimes spawned per run, 0-14 teleports (more teleports the longer the fight, up to 32 in a 300 s timeout). |
| 1c | Brain starts invulnerable behind its creepers | confirmed-faithful | `dontTakeDamage` is true for the first 11-16 s (45-62% of the run) while 20 creepers live; it turns false only when the last creeper dies, then the Brain teleports (2-5 per run) and dashes (`ai[0]` -1/-2). The 6-14 s Brain wins with 10 levies are real kills: `bossLeft = 0`, damage dealt equals scaled max life (1306 of 1350). Result `WIN` requires `life <= 0`; anything else is `DESPAWN`. No DESPAWN row exists in any round. |
| 1d | Brain does not flee | confirmed-faithful | the harness has set `ZoneCrimson` on the commander since the first commit; the Brain stayed for every run. |
| 2 | Boss stats are vanilla | confirmed-faithful | a fresh `SetDefaults` in the same game mode matches the spawned boss before the command-point scaling: Eye 2800/def 12/dmg 15 Classic and 3640/12/30 Expert, King Slime 2000/10/40 and 2800/10/64, Brain 1250/14/30 and 2125/14/54. Scaling is +8% for 4 levies, +18% for 10, +36% for 20, +54% for 30 (about 1.8% per levy). |
| 3 | Boss targets the levies, which take real hits | confirmed-faithful for the Eye, King Slime and the Brain itself; found-different for the Brain's creepers | The boss `target` is the swap slot (255) or a levy in 100% of ticks in every run, never the player. Levies take real damage (Expert Eye 4 levies: 600-1100 damage taken, 1-2 deaths). The Brain's creepers are different: the creeper probe (`out_P.tsv.audit.txt`) shows 11 of 20 creepers targeting the spectating PLAYER by tick 150 and still 9-10 of 17 at tick 420. That matches the designer's in-game report and explains why creepers never damage a levy in any run (damage taken from adds is 0 in nearly every Brain run). It is a mod bug (not a harness setting); the spectator-fix branch owns it. Brain wins are therefore too easy until it lands. |
| 3b | Not-downed bosses target the player in real play | not reproduced in vanilla | Real-play cell (`real=1`): downed-boss record cleared, Spectator Potion drunk through `Player.ItemCheck` (drunk after 1 tick, `specLost` 0%), the buff never re-applied, commander standing still on the ground, boss spawned after the drink. Eye and King Slime still target the swap 100% and fight the levies; results match the direct-buff cells (section 2). `BossAggroUnlocked` is `AnyActive || ...` in this build, so the downed record does not matter while a Spectator is active. The harness runs on a dedicated server (net mode 2); the designer's Death Mode King Slime test was single player with Calamity. Those two differences are untested here. |
| 4a | Levy power: buffs | confirmed-faithful | the only buff is Autonomous (the designer's condition). Honey is off in the control. |
| 4b | Levy power: stations | found-different, fixed | the harness wrote `Stations = Campfire + HeartLantern` onto each soldier, but `SoldierNPC` recomputes `Stations` from the armories every update, so the flags were overwritten and campfire/lantern never applied in rounds A-G (the "no campfire" toggles in round C were no-ops). Fixed with a probe seam `StationBuffSystem.ForceFlags`; audit lines now show `stationsCampfire, HeartLantern`. Effect on results was small (section 2). |
| 4c | Levy power: mana, ammo, potions | partly an assumption | levies start with full mana (a rested levy does too) and a 993-round ammo pouch (the real pouch cap is 999). Potions: 4 per levy in the fixed cells, 3-8 in rolled cells, drunk below 50% life with the real 30 s sickness. The real issue count per levy depends on armory stock; this is an assumption. Mage mana potions (levies sip them below 30% mana) are NOT emulated, so mages are slightly worse here than in play. |
| 4d | Levy DPS against the player-reference DPS | measured | section 4. |
| 5 | Platforms | found-different | levies were above the floor in 0% of sampled ticks: they never use the platform rows. So the "big" arena is the flat arena plus ceilings. Flat versus big changes nothing for the Eye and the Brain; King Slime does WORSE on flat (4 levies: 0 of 3 wins flat, 2-3 of 3 on big), (inferred: the platform rows break up its hops; not isolated). Natural terrain is the worst arena by far (Eye 75 s versus 18 s, Brain 67 s versus 14 s, 2-3 deaths per run: uneven ground traps the pathing). |

Fixes made to the harness: stations through the system seam; per-run audit counters (phase transitions, dashes, spawns, teleports, target, invulnerability, nearest levy, specLost) written into the note column; `audit=1` verbose line log; the real-potion path (`real=1`) with the downed record cleared; post-Eye on-tier loadouts for the Brain; 4 potions in the fixed real-stat cells; melee reference standing within reach; mage reference mana 100.

Open issues found, not fixed here:
1. Brain creepers target the spectating player (above). Mod bug.
2. Blade of Grass levies deal 0 damage to a pinned dummy and 0-11 in boss fights, although the sword is classified as projectile melee. With part C it ranks as ranged-capable, so check the sword before relying on that ranking.
3. The player-reference harness cannot swing a plain broadsword (Platinum Broadsword, Muramasa, Blade of Grass read 0), so those melee ratios are unmeasured.

## 2. Corrected results (corrected build, real Spectator Potion path, not-downed bosses)

Control: big arena (4 platform rows), campfire and heart lantern, Autonomous, Free Rein, Classic primary and Expert secondary, on-tier gear, real stats (140 HP; mages 100 mana). 3 seeds per cell.

### 2a. Representative set (Eye, King Slime, Brain; 4 and 10 levies)

Stations on, Spectator drunk through the item. `ttk` is the mean over wins. 4 levies are one of each (Ranger, Mage, Melee, Summoner).

| boss | difficulty | levies | wins of 3 | ttk s | deaths per run |
|---|---|---|---|---|---|
| Eye | Classic | 4 | 3 | 45 | 0.0 |
| Eye | Classic | 10 | 3 | 14 | 0.0 |
| Eye | Expert | 4 | 3 | 101 | 1.7 |
| Eye | Expert | 10 | 3 | 37 | 0.7 |
| King Slime | Classic | 4 | 2 | 119 | 1.7 |
| King Slime | Classic | 10 | 3 | 19 | 0.3 |
| King Slime | Expert | 4 | 1 | 139 | 2.3 |
| King Slime | Expert | 10 | 3 | 43 | 3.7 |
| Brain | Classic | 4 | 3 | 26 | 0.0 |
| Brain | Classic | 10 | 3 | 9 | 0.0 |
| Brain | Expert | 4 | 3 | 41 | 0.0 |
| Brain | Expert | 10 | 3 | 16 | 0.0 |

Flat arena, same cells (stations on, direct buff): Eye identical within noise; Brain identical; King Slime 4 levies 0 of 3 wins in both difficulties and 10 levies Expert 1 of 3 (big: 3 of 3).

Before and after the fixes, same cells: station fix (H to H2) moved times by under 15% and deaths by under one per run; the real potion item path and the not-downed record (H2 to R) changed nothing beyond noise. The earlier "levies always win" is therefore mostly real for these three bosses in vanilla with a working Spectator; what inflates it is the Brain's creepers and the pre-boss gear being strong relative to a boss with 2000-3600 HP.

### 2b. Levy-count curve

Identical per-levy loadouts and stats; only the count changes. `Mix` is Ranger/Mage/Melee/Summoner in the 3:3:2:2 ratio of the 10-levy anchor. Spectator, Autonomous, Free Rein, on-tier gear, real stats. 3 seeds. Full table: `csv/corrected/count_curve.csv` (includes army DPS, deaths, survival, damage taken).

Scaled boss HP by count (Classic): Eye 4: 3024, 6: 3136, 8: 3248, 10: 3359, 12: 3472, 15: 3639, 20: 3919, 30: 4479. King Slime 2160 / 2240 / 2320 / 2399 / 2480 / 2599 / 2799 / 3199. Brain 1350 / 1400 / 1450 / 1499 / 1550 / 1624 / 1749 / 1999. Expert scales the same way on a 30% higher base.

Time to kill, Mix, seconds (win rate if under 100%):

| boss | difficulty | 4 | 6 | 8 | 10 | 12 | 15 | 20 | 30 |
|---|---|---|---|---|---|---|---|---|---|
| Eye | Classic | 44 | 30 | 21 | 13 | 12.5 | 11 | 10 | 9.6 |
| Eye | Expert | 108 | 58 | 62 | 34 | 35 | 17 | 14 | 17 |
| King Slime | Classic | 64 (67%) | 50 | 38 | 22 | 16 | 15 | 14 | 16 |
| King Slime | Expert | 149 (33%) | 70 | 47 | 59 | 24 (67%) | 23 | 17 | 14 |
| Brain | Classic | 22 | 15 | 13 | 9.6 | 7.7 | 7.2 | 7.5 | 6.6 |
| Brain | Expert | 41 | 24 | 21 | 14 | 10.5 | 8.7 | 10 | 8.6 |

Deaths per run (Mix): Eye Expert 1.7 at 4, 0.7 at 10, 0.3 at 15, 0 at 20; King Slime Expert 1.7, 3.3, 6.3 (at 12), 0 (at 15), 1.7, 0; Classic bosses almost never kill a levy at 10 or more.

Ranger-only and mage-only curves (same loadout in every slot): rangers 1 to 30 against the Eye Classic fall 136 s, 63, 36, 29, 21, 13, 10, 10, 10, 9.7, 9.9 (counts 1, 2, 3, 4, 6, 8, 10, 12, 15, 20, 30); mages 84, 44, 28, 16, 12, 9.7, 8.5, 7.4, 6.8, 6.1, 5.8. Expert Eye with mages only: 1-4 mages lose every run (0 of 3), 6 or more win; rangers only win from 1 levy. King Slime rangers-only lose every run at 1-3 levies in both difficulties; mages-only win only from 2 (Classic) or 3 (Expert, 33%).

Verdict on the count curve:
- Scaling does not cancel extra levies. Boss HP rises about 1.8% per levy while army DPS rises close to linearly up to about 10 levies, so ttk falls about 1/n: Eye Classic 44 s at 4, 13 s at 10.
- Past about 12 levies the curve flattens, then stops: Eye Classic 12 to 30 levies is 12.5 s to 9.6 s, the Brain 7.7 to 6.6 s. Per-levy DPS halves between 10 and 30 (Eye mage 40 to 25, ranger 33 to 15) because the boss is the bottleneck: its HP grows with the count, it flies out of range, and target selection spreads the army over the same boss and its adds. 20 to 30 levies buys 7% on the Eye and nothing on the Brain.
- What extra levies do buy is survival in Expert and against King Slime: Eye Expert deaths per run 1.7 (4), 0.7 (10), 0 (20); King Slime Classic win rate 67% (4), 100% (6 or more).
- The proposed default curve 10 to 20 to 40 to 60 versus 10 to 25 to 40 to 60: for these three bosses it makes no measurable difference; both curves are far past the point where the fight is decided (about 10-12 levies). Whether the curve matters later is outside this data.

### 2c. Minimum force

Smallest group with every seed of 3 won, on-tier real stats, Free Rein, big arena (corrected build):

| boss | difficulty | smallest winning group | notes |
|---|---|---|---|
| Brain | Classic | 1 mage or 1 ranger | 56 s and 78 s; creepers do not hurt (open issue 1). |
| Brain | Expert | 1 mage or 1 ranger | 95 s and 105 s; 2 mages lose a run. |
| Eye | Classic | 1 mage or 1 ranger | 84 s and 136 s, no deaths. |
| Eye | Expert | 1 ranger (119 s, no deaths); mages need 6 | 4 mixed levies win in 108 s with 1.7 deaths; 4 mages lose every run. |
| King Slime | Classic | 6 mixed levies (50 s); 2 mages win (90 s, 3 of 3) | 4 mixed 67%; rangers alone never win with 1-3. |
| King Slime | Expert | 6 mixed levies (70 s, 0.7 deaths) | 4 mixed 33%; no pure group of 3 or fewer wins reliably. |

Gear tiers (partial, rounds E, E2, E3, pre-station-fix; see `csv/prefix/fixed_tiers.csv`): Eye Classic with 10 levies: optimal 14 s, suboptimal 30 s, junk 121 s with 11 deaths. 4-levy junk groups fail or crawl (Brain Classic 4 junk 260 s or 0 of 1; Expert junk 0 of 1). Optimal-to-suboptimal costs 2-5 times the kill time, optimal-to-junk 9-20 times. Best-possible stats (400 HP, 200 mana, 6 potions) change times by under 20% for guns and 30-50% for mages.

## 3. Earlier rounds (before the station fix)

Rounds A (rolled, Classic), B (rolled, Expert), C (variables), D (count curve), E (tiers, melee), G (minimum) ran on the build where station flags were overwritten. They stand as first estimates; D and G were rerun on the corrected build (section 2b and 2c) and agree within noise. Rolled-configuration win rates (12 configurations Classic, 8 Expert, 3 trials each, Free Rein and Search and Destroy):

| boss | Classic win | Classic ttk s | Expert win | Expert ttk s |
|---|---|---|---|---|
| Eye | 100% | 28 | 75-88% | 78 |
| King Slime | 92-94% | 68 | 75-79% | 126 |
| Brain | 100% | 21 | 100% | 52 |

Which variables matter, by effect on time to kill and deaths (rolled and toggled cells; stations were inactive here so "no campfire" is a null result, and the toggles C ran on 4 configurations):

1. Terrain: natural terrain multiplies Eye time by 4 (75 s versus 18 s) and Brain by 5 (67 s versus 14 s) and adds 2-3 deaths. Largest variable.
2. Gear tier: junk gear is 9-20 times slower than optimal; suboptimal 2-5 times.
3. Class mix and count: rangers and mages carry the damage; the melee and summoner levies add 1-5 DPS each against the Eye. A ranger-heavy or mage-heavy group beats the 3:3:2:2 anchor at the same count (Eye Classic 10 levies: mage-only 8.5 s, ranger-only 10.3 s, mix 13.3 s).
4. Autonomy: without it DPS per levy falls 40% (Brain 114 to 61 total DPS, Eye 207 to 138).
5. Levy HP and mana: rolled HP buckets (100-200) and mana buckets show less than 15% change in time and under one death per run; mana matters only for mages with under 60 mana.
6. Potions, honey, campfire and lantern, accessories: no measurable effect on win rate; potions cut deaths by about 0.1-0.2 per run in Expert.
7. Boss difficulty: Expert multiplies Eye time by 3 and King Slime by 2, with 2-4 deaths per run at 4-10 levies.
8. Order: Free Rein and Search and Destroy differ by under 10%.

## 4. Levy against player: DPS by class (control 1)

Same weapon, armor, accessories and stats; a levy and a live `Player.ItemCheck` player each hit a pinned Eye-like dummy (defense 12, 1,000,000 life) for 30 s. Player mana is unlimited (potions). `csv/prefix/ref_dps.csv`, raw `out_F2.tsv`.

| class | weapon | levy DPS | player DPS | player:levy |
|---|---|---|---|---|
| Ranger | Minishark / Silver Bullet | 30 | 302 | 10 |
| Ranger | Boomstick | 22-26 | 342-350 | 13-16 |
| Ranger | Demon Bow / Tendon Bow | 30-34 | 149-175 | 5 |
| Ranger | Handgun / Phoenix Blaster | 86 / 112 | 496-500 / 570 | 5.8-6 / 5.1 |
| Ranger | Wooden Bow | 2.6 | 24 | 9 |
| Mage | Diamond Staff / Ruby Staff | 60 / 52 | 400 / 315 | 6.7 / 6.1 |
| Mage | Amethyst Staff | 9.7 | 62 | 6.4 |
| Mage | Space Gun | 40 | 99 | 2.5 |
| Mage | Aqua Scepter | 42 (100 mana) | 310 | 7.5 |
| Mage | Wand of Sparking | 6-10 | 176-180 | 18-29 |
| Melee, fires a projectile | Enchanted Sword / Starfury | 16 / 35 | 50 / 79 | 3.1 / 2.2 |
| Melee, fires a projectile | Night's Edge | 72-75 | 423-438 | 5.9-6.0 |
| Melee, contact | Copper Shortsword | 4.3 | 10 | 2.4 |
| Summoner | Slime Staff / Finch Staff | 5 / 1.6 | 15-16 / 8.7 | 3.1 / 4.7 |

Reading: guns and bows land at 5-6 times, staffs at 6-7 times, matching the designer's 5-8 times. Projectile swords (2-3 times) and summoners (3-5 times) are strong relative to the player, so a player-built group of those classes undershoots the intended gap. Pure-contact blades read 21 DPS for a Platinum Broadsword on a levy; the player value is unmeasured (open issue 3). Outliers: Wand of Sparking is weak for a levy (18-29 times), Vilethorn deals 0 (levy 0.0 against 56 for the player), Blade of Grass 0, Magic Missile and Muramasa are unmeasured on the player side.

## 5. Part C: pure-contact melee ranks lower (replaces the beam boost)

Change: `WeaponRank.PureContactFactor = 0.15` multiplies the rank of a broadsword that fires nothing, and of every shortsword (`WeaponProfile.IsPureContactBlade`), floored at 1. Spears, yoyos, flails, whips, boomerangs, held weapons and every melee weapon that launches a projectile or beam (Enchanted Sword, Starfury, Beam Sword, Terra Blade, Zenith) keep their rank. The stage discount still applies, so an outdated weapon of either kind loses to a current one: an out-of-date Chlorophyte Saber ranks at 5% or less after Plantera and falls to the floor of 1. Combat for projectile swords already stands off at range and swings when an enemy is close (`FiresProjectileMelee`). The old `BeamMeleeBoost` and its check are removed; `WeaponRank.PureContactPenalty = false` restores the 2.0.4 ranking (the baseline).

Self-checks added: every ranged-capable sword outranks every plain blade in all four modes at stages 0-3; the penalty is exactly the factor; yoyo, spear and boomerang are untouched; ranged-capable swords are unchanged. The existing price, stage and found-boost checks run with the penalty switched off so they still measure their own factors.

Melee rows against the baseline (four melee levies, corrected rules, forced weapon, 1-2 seeds; `partc.py`): the Eye Classic with 4 Enchanted Sword levies wins in 72-76 s (best/real stats); with 4 Platinum Broadsword or 4 Copper Shortsword levies it never wins in 300 s (0.3-0.8 DPS per levy: the Eye hovers 15 tiles away and dashes through). Eye Expert, Brain and King Slime follow the same split. The pool-pick comparison (Enchanted Sword, Starfury, Blade of Grass, Katana, Platinum and Gold Broadsword) selects the Enchanted Sword with and without the penalty because the sword is the most valuable on-tier weapon, so the weapon pick in these rows is identical; the change matters in the allocator when a plain blade is worth more than the beam sword (later tiers) and is covered by the self-checks, not by a fight row. Open issue 2 (Blade of Grass) applies to this ranking.

## 6. Boot results

`["Levy"]` and `["ThoriumMod","Levy"]` on a private toolchain copy: exit 124 (world select), 0 lines matching `xception|TypeLoad|MissingMethod|malformed`, 22 self-check lines, all OK.

## 7. Which variables matter most (summary)

1. The Brain's creeper targeting (mod bug): it removes all damage from the add phase.
2. Terrain, then gear tier, then count up to about 10-12 levies, then class mix. Past 12 levies the extra levies only add survival.
3. Boss difficulty and King Slime's hop pattern: platforms help, flat ground does not.
4. Autonomy: about a third of the army's DPS.
5. Everything else (levy HP 100-200, mana, potions, honey, campfire and lantern, accessories, order) is within noise for these three bosses.

## 8. Projectile-melee sweep and fixes (follow-up)

Cause of the Blade of Grass zero: it was classified as a beam sword (`FiresProjectileMelee`, reach capped at 40 tiles), so the brain stood it off at gunner range (about 28 tiles from a boss) while its vine, a spore cloud or a homing crescent only works within a few tiles. The shots spawned beside the levy and died there (debug lines: projectile 976 at 18-28 tiles from the dummy, projectile 228 parked beside the levy with a 3500-tick life). Blade of Grass also has a second, smaller problem: its vine does 25% of the sword's damage per hit.

Fix: a table of short-reach projectile swords (`ShortReachShots` in `WeaponClassifier`) sets `WeaponProfile.ShortShot`; those weapons keep `FiresProjectileMelee` (still ranged-capable for ranking) but position and acquire like a blade (`PositionsAsMelee`) and launch the shot from blade range. Listed: Blade of Grass, Light's Bane, Chlorophyte Saber and Claymore, Ice Sickle, Death Sickle, Christmas Tree Sword, Waffle's Iron.

Sweep: every vanilla melee weapon with `shoot > 0` that the classifier supports (25), one 30 s levy-only window each against the pinned dummy (`out_S.tsv` before, `out_S4.tsv` after; expected DPS is the naive swing DPS, damage minus 6 times swings per second). Under 20% of expected before: Light's Bane, Blade of Grass, Chlorophyte Claymore, Chlorophyte Saber, Ice Sickle, Death Sickle, Christmas Tree Sword, Zenith, Waffle's Iron (all 0). After: Light's Bane 98%, Blade of Grass 124%, Claymore 106%, Saber 167%, Ice Sickle 237%, Death Sickle 282%, Christmas Tree Sword 317%, Waffle's Iron 177%. Still 0: Zenith (the levy stands off at 29 tiles and spawns no sword; not a range problem, and likely a headless-server limit because the Zenith sword profile setup is client-side; unverified). Low but above 20%: Ice Blade 26%, Enchanted Sword 32%, Beam Sword 42%.

Player reference for plain broadswords: a headless server never sizes a held blade's hitbox, so a swing hits nothing. The reference now strikes the dummy by hand once per swing for contact blades (weapon damage and crit from the real equip pass; marked `swing-emulated`). Levy:player: Platinum Broadsword 4.0, Gold Broadsword 3.6-3.9, Muramasa 2.5, Copper Broadsword 10.5-11.4. Blade of Grass and Chlorophyte Saber read 0.8 and 1.4 because the player side cannot emulate their homing shots, so treat those two as unmeasured.

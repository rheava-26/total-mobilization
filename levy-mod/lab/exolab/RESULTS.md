# Exo Mechs lab (exolab): your army against Draedon's mechs

## In plain words

Your 40 regulars, wearing exactly what they wore in your Nov 21 17:19 Exo fight, lost all 81 lab fights against the Exo Mechs on Master+Death. They fought alone on the current build (Levy 3.1.10), with you invisible 150 tiles away, in an enormous open arena. They do know how to fight them: every mech went after levies the whole time, the levies always switched to whichever mech could be hurt and never wasted their aim on a mech that had gone invisible, nobody shot at Draedon, and only one levy in 81 fights wandered off the arena. They just die too fast. The whole army is usually dead within about a minute (median 47 to 78 seconds) and has dealt 4 to 5 million damage out of roughly 11 million. Your army got further when Draedon picked Artemis and Apollo first, because Ares then shows up in phase 2 and melts: they killed him in 5 of 8 of those fights. The current build's class-matched armour helped a little: 18% more damage and twice as many mechs killed, but still no win. A fight-tested "best of your own stock" army in Auric Tesla armour did about 60% more damage than yours and three times reached the last mech, but it lost too.

Four things kill them. Thanatos ploughs through the line, Ares's Gauss Nuke and his two laser beams catch big groups, and in 71 of the 81 fights 8 or more levies died inside the same 2 seconds. Levies do not step out of the Gauss Nuke's 3-second warning circle, and they have no idea where Calamity's deathrays are. When Thanatos opens alone he spends 81% of that phase under the floor, so about a third of the army stands with no target. A real bug in Levy's boss-life scaling also bends the fight: the mechs' maximum life goes up by 32 to 42% but their actual life doesn't, so Ares reaches his 70% "call the others" threshold after 2 to 3 seconds instead of 7 to 8. Your own 17:19 fight shows the same thing (+45% maximum life). Lag in the lab was 10 to 13 ms per tick on average, and up to 18 to 26 ms in the busiest phases, on a box shared with other jobs.

Your 22:25 and 21:56 Exo fights (3.1.10): the soldiers who stood at x 3,115 to 3,267 the whole time were **militia guarding their town, not regulars that failed to follow**. Details are at the end of the problem list.

## Setup

- Calamity 2.2.2 (you play 2.2.4), Master+Death, Levy 3.1.10 build of `exolab` (harness-only changes on top of worktree-levy-stardust b9e28bc). Night. A post-Yharon world (Moon Lord, Providence, Polterghast, DoG, Yharon flagged beaten).
- Arena `big`: an asphalt floor 1200 tiles wide and 4 tiles thick, platform rows 12, 24 and 36 tiles up (a 10-tile gap every 40), and 220 tiles of cleared sky above it. Normal world terrain lies under the floor.
- Commander: the harness stand-in, invulnerable, with a Spectator buff, idle, 150 tiles left of the army (`cmd=far`). The Exo Mechs count as beaten (a rematch); otherwise the Spectator rule doesn't hide you from an undowned boss. Army order Free Rein, which is what your army was on at 17:19.
- Draedon is spawned beside the commander, set to "already talked to", and given the choice the selection UI would write (`CalamityWorld.DraedonMechToSummon`). From there it's Calamity's own fight: Draedon summons the mech 3 s in, the other two arrive at 70%, then the passive and invisible interludes, berserk phases, Draedon's lines and so on. A fight ends when all three mechs are dead (a win), the army is wiped, or the 600 s cap runs out.
- Cells (fights): your army × 3 openers × 8 seeds (24); the same with class-matched armour (24); your army with Levy's boss-life scaling off (`hpscale=0`, 15, to measure the bug below); the optimal army × 3 × 6 (18). That's 81 fights, all recorded. One tML server at a time (the box was loaded), 1.2 fights per minute, about 70 minutes of fighting.
- **Your army** (`armies/eve_army.json`, `mkarmies.py` via `mkeve.py`): the last gear rows of your 40 regulars (ids 1-40) in `2026-11-21_17-19_Draedon...` (3.1.8), with weapon, prefix, sidearm, armour, 2 accessories plus wings and boots, Lunar Hook, ammo and life (465-500). By class: 13 melee, 9 ranged, 8 rogue, 8 magic, 2 summoner. Stand-ins for what the recording doesn't show: 9 mana crystals, 8 Super Healing Potions, no buff potions (your levies carried none), and the full lab station set. Your 8 militia/officer bodies from that fight (ids < 0) are left out.
- **What differs from your 3.1.8 game:** (1) Armour. Your 3.1.8 Armory gave 17 of the 40 the wrong class of armour: mages and summoners in God Slayer *melee/ranged* helms, rangers in Bloodflare *melee* helms, and a rogue in the summoner Fearmonger set. The `eve_matched` army has what 3.1.9+ deals: the same set's head for the levy's class, and full Bloodflare for mages and summoners, since God Slayer has no head for them. INFERRED: that you own Bloodflare magic and summon heads. (2) Holdouts. 3.1.10 (brokenguns) made Thread of Eradication fire properly from a levy. Your levy 28 carries it, and it was among the army's top 8 weapons here (3.5M across 24 fights; 19.3M in the optimal army). Your other weapons are not on the brokenguns list. Both cells run on 3.1.10, so the cell difference is the armour alone. (3) The recorder now reads Exo targets. In your 3.1.8 recordings the mechs' target is always you or nobody; here the recorder shows a levy in 99.8% of the mechs' frames (it reads `targetRect` correctly).
- **Optimal army** (`armies/opt_army.json`, `mkopt.py`): the best of your own stock, fight-tested. Each weapon your army carried was ranked by damage per carrier per fight over your 24 lab fights, and the top weapons fill the 40 places, at most 6 each: Plasma Grenade, Pulse Rifle, Dimension-Tearing Disk, Thread of Eradication, Vega, Time Bolt ×6, and Norfleet ×4. That's 24 rogues and 16 rangers. Everyone wears Auric Tesla in their own class (your commander wears it, so you can make it). Accessories, wings and boots copy your levy who carried that weapon. Left out because they're untested in levy hands: your own Auric weapons (Dynamic Pursuer, Eclipse's Fall, The Final Dawn). INFERRED caveat: Plasma Grenade, Pulse Rifle and Thread of Eradication each had a single carrier in your army, so their ranking is noisy.

## Problems seen (with evidence; OBSERVED unless marked INFERRED)

1. **Levy bug: boss-life scaling doesn't stick on the Exo Mechs, so their phases come early.** OBSERVED in the lab recordings: within 1.5 s of Ares spawning, his maximum life climbs from 3.08M to 4.24M (+37%) while his life stays at about 3.08M. He passes his 70% threshold with almost no real damage done. Thanatos (2.37M → 2.99M max, life unchanged) and the twins do the same. The recordings' own scaling figure reads +32 to 42%. Ares alone lasts 2-3 s with scaling on (all 24 Ares-first fights with your army or the matched one, all 6 optimal) and 7-8 s with it off (5/5). OBSERVED in your own 17:19 fight too: Ares's maximum went 3.05M → 4.43M (+45%), and he crossed 70% at t=960 with 3.10M life, roughly what he spawned with. Cause (read in Calamity's source and Levy's `BossRule.ApplyScaling`): Levy adds the extra life to the hit NPC's root only. But Calamity mirrors these mechs' life every tick: Ares's body is clamped to his first arm (`AresBody` line 437), Thanatos's head to his first segment (`ThanatosHead` 410), and Artemis and Apollo to each other. So the added life is clamped away the next tick and only the raised maximum survives. Effect: the health bar starts at about 73%, and Calamity's 70% and 40% phase thresholds trigger with 30-40% less real damage. Proposal (not fixed): apply the scaling add to every NPC in a mirrored-life group together (Ares's body and arms, the two twins, Thanatos's head and the segment it copies), or more generally, after scaling check next tick whether the life held, and if it fell back, raise the partner NPCs too. Any boss that mirrors life this way will have the same problem.
2. **Levies don't leave the Gauss Nuke's warning circle.** Gauss Explosion was the killing hit for 10-20% of all deaths. In your army's Ares-first seed 1, it killed 26 levies within 54 ticks at 30.4 s. It was the worst 2 seconds of 22 fights. Ares's nuke draws a circle about 1480 px wide for 3 s before it goes off. Cause, INFERRED from the code: Levy's ThreatField dodges a projectile's own hitbox (100×100 px for this nuke) and knows nothing about the explosion it turns into. Proposal: a generic "pending area" threat for projectiles that telegraph an explosion, with the radius read from the explosion projectile's size or a small table.
3. **Levies can't see Calamity deathrays.** Exothermal Laser (Ares's laser cannon) and Blenderbeam (Ares's spinning deathray) together made the killing hit in 11-19% of deaths in your army's cells, and the Blenderbeam killed 13-17 levies at once in 2 fights. Cause, INFERRED from the code: `ThreatField.BeamOf` treats only `IThreatBeam` projectiles and vanilla Moon Lord's ray as lines. Calamity's beams (`BaseLaserbeamProjectile`: Ares, Thanatos and Artemis beams and every other Calamity boss's) are dodged as a point at their start. Proposal: read `BaseLaserbeamProjectile.LaserLength` and the projectile's own width through reflection as a generic beam.
4. **Thanatos-first: Thanatos stays under the floor and a third of the army has nothing to shoot.** In the 16 Thanatos-first fights of your two armies, Thanatos's head was more than 3 tiles below the asphalt floor in 81% of his solo-phase frames (72-88% per fight). In that phase 44-52% of the levies had no target or weren't engaged. Across the whole fight it was 26-30%, against 11-14% when Ares or the twins open. Cause, OBSERVED in code: `SoldierTargeting.PickTarget` requires line of sight unless the levy's shots pass through tiles. INFERRED: a player with tile-ignoring weapons, or one fighting on a sky platform, wouldn't have this problem. It's partly the arena: solid ground under a surface floor.
5. **Levies keep hitting Thanatos's closed armour.** Of all levy hits on Thanatos, 31-66% landed on closed segments (damage under 1/20 of the hit's source damage; closed segments take 99.99% DR). Nothing in targeting prefers his open vents or his head. Proposal: rank a worm's segments by their current damage reduction when picking a target.
6. **Mass deaths.** In 71 of 81 fights, 8 or more levies died inside the same 2 seconds. The median worst 2 s was 10-13 levies, and the most was 26. The causes are Thanatos charging through the line (worst 2 s in 32 fights), the Gauss Nuke, and the beams. The army isn't tightly bunched: the middle 80% spans 60-96 tiles across and 29-41 tiles up, and 65-80% are airborne. These attacks are just very wide.
7. **What works.** In all 81 fights the mechs targeted levies (harness 100%; recorder `tkind` = levy in 99.8% of the frames of the four main mechs, so the `targetRect` reading works on real Exo fights). Levies aimed at a mech in its invisible interlude 0-2% of the time: they switch to the mech that can be hurt. They never aimed at Draedon (0%), who followed a levy 100% of the time at 83-97 tiles from the army. They chased off beyond 100 tiles from any open mech 0-4% of the time, except 17-21% in Thanatos-first. Off the arena: 1 levy in 1 fight (a Pulse Rifle levy past the floor's end at 166 s).
8. **Lag.** The median ms per tick per cell was 9-17, and per phase it reached 18-26 ms when two mechs are open and the army is full (aTW, ATw). Anything over 16.7 ms is slower than real time. Measured on a box running other tML servers (load 6-12 on 4 cores), so this is an upper bound.
9. **Recorder and harness attribution gaps (minor).** Ares's Exoplasma Bolt hits and kills are recorded "by world" with no source NPC. 11-26% of the killing hits have no source at all (debuff damage or an unattributed projectile). The harness's own last-attacker attribution disagrees with the recording's killing hit: it lists "no attacker" for 27-30% of deaths and puts Gauss Explosion at 1-3% where the recording says 10-20%. The tables use the recording's killing hit.
10. **Your 22:25 and 21:56 Exo fights (Levy 3.1.10, format 12): militia, not lost regulars.** In 22:25 the soldiers at x 3,116-3,267 / y 387-407 are 9 bodies with recording ids -163 to -171 and "Pvt." names. In 21:56 there are 19 more, ids -42 to -60, in the same 150-tile box. In both fights they spent 96-100% of the time in SearchDestroy (the militia patrol of their settlement) and never left that box in 338-340 s, while you fought from x 122 to 6,046. The recorder writes a body with no roster record as id `-whoAmI`; regulars carry their positive roster ids (1-40) and officers a large negative id. The header of each fight counts 1 regular levy (`army.n=1`, a single summoner) and the end record counts `armyTotal=0`. So these were militia guarding their town, which is what militia are designed to do for a boss far away. Your regulars were not in those fights at all. Why they weren't with you isn't answered by these files (INFERRED: they were left at a barracks, or not mustered).
11. **Your 17:06-17:29 fights weren't plain Calamity.** Their mod lists include WoTM (Wrath of the Machines) and NoxusBoss, and the mechs are named "XM-04 Hades" and "XS-11 Apollo", with an Ares carrying only a plasma cannon. Your one army fight with 88% army damage (17:19) was therefore against WoTM's reworked mechs. From 17:47 on, WoTM isn't loaded and the mechs are Calamity's own. The lab runs Calamity's own; WoTM isn't installed here.

## Files

- Harness (lab only): `Levy/Common/Testing/CombatHarness.ExoLab.cs` (boss lines `exoares`, `exothan`, `exotwins`; arena `big`; per-fight `out.tsv.exo.tsv` and `out.tsv.exo_events.tsv`), plus small hooks in `CombatHarness.BossBattery.cs` and `CombatHarness.CalRev.cs`.
- This folder: `mkeve.py` and `mkarmies.py` (armies from your recording), `mkopt.py` (optimal army), `mkfights.py` (specs in `specs/`), `exo_collect.py` (gzip recordings, `fights.json`, the tables below), `build_exoset.py` (replay set).
- Rerun: `LEVYLAB_HOME=… LEVYLAB_SRC_TML=/home/user/tml-bin/tml-2026.10 LEVYLAB_SRC_SAVE=/home/user/tml-save-2026.10 LEVYLAB_WORLD=<ShipWorld.wld> LEVYLAB_PORT=9210 levy-mod/lab/levylab run levy-mod/lab/exolab/specs/main.tsv --jobs 1 --per-proc 21 --rec 99`, then `exo_collect.py RUN --dest DIR --json fights.json` and `exo_collect.py --tables fights.json`. The lab install was deleted afterwards.
- Recordings (format 13, gzip, all 81 fights plus `index.txt`, one line per fight): `/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/exolab_recs/`. Names are `<army>_<opener>_s<seed>_<result>.jsonl.gz`.
- Replay set (not published): `/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/exoset_v1/` (`set.json` and `data/`, set id `exolab`, 9 fights, 17.7 MB at 8 Hz). Your army: Gauss wipe, Thanatos under the floor, Ares killed. Matched: twins killed, the closest run. No scaling. Optimal ×3.

## Tables (exo_collect.py --tables; medians per fight unless stated)

### Results by cell

| army | Draedon's first mech | fights | wins | fight length s, median (range) | mechs killed per fight | kill order seen | furthest phase | levy deaths, median | first levy death s | pool left in losses, median |
|---|---|---|---|---|---|---|---|---|---|---|
| Her army (as recorded, 3.1.8 gear) | Ares first | 8 | **0/8** | 74 (38-110) | 0.0 | - | phase 4 one berserk, two leave x4, phase 3 all three x4 | 40/40 | 8.6 | 51% |
| Her army (as recorded, 3.1.8 gear) | Thanatos first | 8 | **0/8** | 47 (38-226) | 0.0 | - | phase 3 all three x4, phase 2 other two arrive x4 | 40/40 | 8.2 | 72% |
| Her army (as recorded, 3.1.8 gear) | Artemis and Apollo first | 8 | **0/8** | 62 (49-78) | 0.6 | Ares x5 | phase 5 one down, two fight x3, phase 4 one berserk, two leave x3, phase 6 one berserk, one leaves x2 | 40/40 | 12.6 | 41% |
| Her army, class-matched armour | Ares first | 8 | **0/8** | 71 (54-92) | 0.2 | Twins x1, Ares x1 | phase 4 one berserk, two leave x4, phase 3 all three x2, phase 6 one berserk, one leaves x1 | 40/40 | 10.5 | 45% |
| Her army, class-matched armour | Thanatos first | 8 | **0/8** | 54 (36-79) | 0.0 | - | phase 2 other two arrive x4, phase 3 all three x3, phase 4 one berserk, two leave x1 | 40/40 | 8.2 | 71% |
| Her army, class-matched armour | Artemis and Apollo first | 8 | **0/8** | 69 (55-84) | 1.2 | Ares x6, Ares > Thanatos x2 | phase 6 one berserk, one leaves x4, phase 5 one down, two fight x3, phase 7 last mech x1 | 40/40 | 13.3 | 28% |
| Her army, no boss-life scaling | Ares first | 5 | **0/5** | 73 (37-108) | 0.0 | - | phase 3 all three x3, phase 2 other two arrive x1, phase 4 one berserk, two leave x1 | 40/40 | 15.0 | 56% |
| Her army, no boss-life scaling | Thanatos first | 5 | **0/5** | 52 (45-63) | 0.0 | - | phase 1 first mech alone x2, phase 2 other two arrive x2, phase 3 all three x1 | 40/40 | 8.2 | 80% |
| Her army, no boss-life scaling | Artemis and Apollo first | 5 | **0/5** | 68 (36-107) | 0.2 | Ares x1 | phase 3 all three x3, phase 5 one down, two fight x1, phase 4 one berserk, two leave x1 | 40/40 | 9.2 | 57% |
| Optimal post-Yharon army | Ares first | 6 | **0/6** | 78 (59-106) | 0.8 | Ares > Twins x2, Ares x1 | phase 4 one berserk, two leave x2, phase 7 last mech x1, phase 3 all three x1 | 40/40 | 8.0 | 36% |
| Optimal post-Yharon army | Thanatos first | 6 | **0/6** | 88 (69-109) | 0.8 | Ares x3, Thanatos x2 | phase 6 one berserk, one leaves x5, phase 4 one berserk, two leave x1 | 40/40 | 8.4 | 24% |
| Optimal post-Yharon army | Artemis and Apollo first | 6 | **0/6** | 52 (47-85) | 1.3 | Ares x4, Ares > Thanatos x2 | phase 6 one berserk, one leaves x4, phase 7 last mech x2 | 40/40 | 15.4 | 28% |

### Army damage per mech (DPS while that mech could be hurt, median per fight) and how long each mech was open

| army | first mech | Ares DPS | Ares open s | Thanatos DPS | Thanatos open s | Twins DPS | Twins open s | whole-fight army DPS | Thanatos hits on closed armour |
|---|---|---|---|---|---|---|---|---|---|
| Her army (as recorded, 3.1.8 gear) | Ares first | 36,168 | 65 | 24,255 | 47 | 19,042 | 47 | 64,153 | 31% |
| Her army (as recorded, 3.1.8 gear) | Thanatos first | 28,014 | 28 | 29,352 | 32 | 4,658 | 28 | 44,277 | 32% |
| Her army (as recorded, 3.1.8 gear) | Artemis and Apollo first | 122,976 | 33 | 33,462 | 35 | 18,827 | 28 | 103,161 | 50% |
| Her army, class-matched armour | Ares first | 41,422 | 47 | 23,824 | 48 | 31,274 | 48 | 76,062 | 32% |
| Her army, class-matched armour | Thanatos first | 35,479 | 33 | 35,796 | 26 | 15,873 | 33 | 55,089 | 31% |
| Her army, class-matched armour | Artemis and Apollo first | 167,919 | 23 | 45,251 | 43 | 26,138 | 36 | 111,730 | 66% |
| Her army, no boss-life scaling | Ares first | 22,525 | 58 | 13,425 | 63 | 27,088 | 63 | 53,317 | 52% |
| Her army, no boss-life scaling | Thanatos first | 17,380 | 19 | 23,208 | 31 | 16,097 | 19 | 25,069 | 48% |
| Her army, no boss-life scaling | Artemis and Apollo first | 33,694 | 48 | 8,351 | 31 | 38,255 | 27 | 52,029 | 48% |
| Optimal post-Yharon army | Ares first | 86,004 | 45 | 33,449 | 46 | 24,852 | 48 | 87,288 | 39% |
| Optimal post-Yharon army | Thanatos first | 75,463 | 54 | 44,429 | 44 | 26,848 | 34 | 80,226 | 55% |
| Optimal post-Yharon army | Artemis and Apollo first | 248,047 | 16 | 62,411 | 36 | 23,874 | 22 | 134,977 | 54% |

### Who the mechs and Draedon went for, and what the levies were doing (share of samples, median per fight)

| army | first mech | mechs on a levy | Draedon on a levy | Draedon from the army, tiles | levies on a mech they can hurt | on an immune mech | on Draedon | no target while a mech is open | not engaged while a mech is open | >100 tiles from any open mech | levies off the arena (fights) | ms per tick |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Her army (as recorded, 3.1.8 gear) | Ares first | 100% | 100% | 85 | 76% | 0% | 0% | 13% | 13% | 1% | 0/8 | 11.0 |
| Her army (as recorded, 3.1.8 gear) | Thanatos first | 100% | 100% | 97 | 58% | 0% | 0% | 30% | 30% | 20% | 1/8 | 9.9 |
| Her army (as recorded, 3.1.8 gear) | Artemis and Apollo first | 100% | 100% | 89 | 78% | 0% | 0% | 14% | 14% | 0% | 0/8 | 14.2 |
| Her army, class-matched armour | Ares first | 100% | 100% | 87 | 79% | 0% | 0% | 11% | 11% | 1% | 0/8 | 12.3 |
| Her army, class-matched armour | Thanatos first | 100% | 100% | 87 | 61% | 0% | 0% | 26% | 26% | 17% | 0/8 | 10.7 |
| Her army, class-matched armour | Artemis and Apollo first | 100% | 100% | 88 | 72% | 0% | 0% | 20% | 20% | 2% | 0/8 | 17.0 |
| Her army, no boss-life scaling | Ares first | 100% | 100% | 93 | 77% | 0% | 0% | 13% | 13% | 1% | 0/5 | 12.7 |
| Her army, no boss-life scaling | Thanatos first | 100% | 100% | 89 | 55% | 0% | 0% | 30% | 30% | 21% | 0/5 | 9.1 |
| Her army, no boss-life scaling | Artemis and Apollo first | 100% | 100% | 83 | 65% | 0% | 0% | 21% | 21% | 4% | 0/5 | 11.0 |
| Optimal post-Yharon army | Ares first | 100% | 100% | 89 | 86% | 0% | 0% | 2% | 2% | 1% | 0/6 | 10.6 |
| Optimal post-Yharon army | Thanatos first | 100% | 100% | 88 | 70% | 0% | 0% | 22% | 21% | 18% | 0/6 | 9.6 |
| Optimal post-Yharon army | Artemis and Apollo first | 100% | 100% | 85 | 86% | 1% | 0% | 5% | 4% | 0% | 0/6 | 11.3 |

### By phase (all fights of an army pooled): army DPS, idle levies, levies on an immune mech, ms per tick

Phase labels: one letter per mech present (A Ares, T Thanatos, W the twins); upper case = can be hurt, lower case = passive and immune (Draedon's interlude for that mech).

| army | phase | fights that reached it | seconds in it (median) | army DPS (median) | levies with no target or not engaged % | levies targeting an immune mech % | engaged % | ms per tick |
|---|---|---|---|---|---|---|---|---|
| Her army (as recorded, 3.1.8 gear) | ATW | 20 | 23.1 | 64,966 | 8 | 0 | 92 | 12.7 |
| Her army (as recorded, 3.1.8 gear) | Atw | 12 | 21.9 | 28,634 | 6 | 0 | 94 | 6.2 |
| Her army (as recorded, 3.1.8 gear) | A | 8 | 2.8 | 359,591 | 35 | 0 | 65 | 14.6 |
| Her army (as recorded, 3.1.8 gear) | aTW | 8 | 9.3 | 116,989 | 17 | 0 | 83 | 20.6 |
| Her army (as recorded, 3.1.8 gear) | T | 8 | 16.4 | 53,779 | 44 | 0 | 56 | 14.7 |
| Her army (as recorded, 3.1.8 gear) | AtW | 8 | 18.1 | 63,243 | 12 | 0 | 88 | 9.4 |
| Her army (as recorded, 3.1.8 gear) | W | 8 | 3.7 | 72,554 | 32 | 0 | 68 | 15.7 |
| Her army (as recorded, 3.1.8 gear) | ATw | 8 | 4.8 | 284,549 | 16 | 1 | 84 | 22.9 |
| Her army (as recorded, 3.1.8 gear) | Tw | 5 | 9.5 | 40,331 | 26 | 0 | 74 | 8.8 |
| Her army (as recorded, 3.1.8 gear) | TW | 5 | 14.2 | 25,180 | 6 | 0 | 94 | 7.7 |
| Her army, class-matched armour | ATW | 20 | 19.0 | 105,829 | 8 | 0 | 92 | 18.9 |
| Her army, class-matched armour | Atw | 13 | 11.6 | 144,470 | 10 | 0 | 90 | 16.4 |
| Her army, class-matched armour | Tw | 9 | 10.2 | 49,728 | 44 | 0 | 56 | 12.1 |
| Her army, class-matched armour | TW | 9 | 14.9 | 65,438 | 7 | 0 | 93 | 13.3 |
| Her army, class-matched armour | A | 8 | 2.8 | 352,872 | 36 | 0 | 64 | 15.2 |
| Her army, class-matched armour | aTW | 8 | 6.9 | 127,267 | 18 | 0 | 82 | 24.1 |
| Her army, class-matched armour | T | 8 | 15.6 | 57,478 | 46 | 0 | 54 | 15.7 |
| Her army, class-matched armour | AtW | 8 | 18.0 | 64,520 | 12 | 0 | 88 | 8.8 |
| Her army, class-matched armour | W | 8 | 4.8 | 66,222 | 30 | 0 | 70 | 16.9 |
| Her army, class-matched armour | ATw | 8 | 3.8 | 317,140 | 26 | 0 | 74 | 26.0 |
| Her army, class-matched armour | atW | 2 | 19.4 | 68,990 | 12 | 0 | 88 | 11.3 |
| Her army, no boss-life scaling | ATW | 9 | 30.1 | 36,743 | 5 | 0 | 95 | 9.7 |
| Her army, no boss-life scaling | A | 5 | 7.5 | 122,295 | 19 | 0 | 81 | 17.9 |
| Her army, no boss-life scaling | aTW | 5 | 17.9 | 62,797 | 22 | 0 | 78 | 23.2 |
| Her army, no boss-life scaling | T | 5 | 30.0 | 23,513 | 36 | 0 | 64 | 11.2 |
| Her army, no boss-life scaling | W | 5 | 13.5 | 68,112 | 32 | 0 | 68 | 14.9 |
| Her army, no boss-life scaling | ATw | 5 | 13.1 | 77,871 | 17 | 0 | 83 | 17.5 |
| Her army, no boss-life scaling | AtW | 3 | 22.1 | 42,856 | 6 | 0 | 94 | 7.4 |
| Her army, no boss-life scaling | Atw | 2 | 23.6 | 75,364 | 12 | 0 | 88 | 8.1 |
| Optimal post-Yharon army | ATW | 18 | 12.8 | 164,256 | 0 | 0 | 100 | 16.5 |
| Optimal post-Yharon army | Atw | 14 | 7.8 | 222,557 | 0 | 0 | 100 | 12.9 |
| Optimal post-Yharon army | TW | 12 | 9.4 | 94,368 | 2 | 0 | 98 | 11.6 |
| Optimal post-Yharon army | Tw | 9 | 15.4 | 38,325 | 22 | 0 | 78 | 9.6 |
| Optimal post-Yharon army | T | 8 | 12.2 | 73,904 | 52 | 0 | 48 | 12.7 |
| Optimal post-Yharon army | A | 6 | 1.9 | 420,334 | 5 | 0 | 95 | 13.0 |
| Optimal post-Yharon army | aTW | 6 | 8.2 | 143,820 | 0 | 1 | 100 | 18.3 |
| Optimal post-Yharon army | AtW | 6 | 6.4 | 167,466 | 10 | 0 | 90 | 13.2 |
| Optimal post-Yharon army | W | 6 | 2.0 | 45,197 | 4 | 0 | 96 | 11.5 |
| Optimal post-Yharon army | ATw | 6 | 2.4 | 574,948 | 2 | 2 | 98 | 18.4 |
| Optimal post-Yharon army | tW | 4 | 20.0 | 62,078 | 7 | 0 | 98 | 9.6 |
| Optimal post-Yharon army | aTw | 3 | 13.0 | 85,056 | 10 | 0 | 90 | 11.7 |
| Optimal post-Yharon army | Aw | 2 | 41.0 | 34,708 | 2 | 0 | 98 | 5.0 |
| Optimal post-Yharon army | AW | 2 | 12.2 | 85,330 | 0 | 0 | 100 | 6.7 |

### What killed levies (the killing hit in the recordings, all fights of an army pooled) and the worst 2 seconds

- Her army (as recorded, 3.1.8 gear) (960 deaths): XM-05 Thanatos 25%, Gauss Explosion 20%, no source (debuff or unattributed) 11%, Exothermal Laser 10%, Blenderbeam 9%, XS-01 Artemis 7%, Exoplasma Bolt 5%, XF-09 Ares 4%. Worst 2 s per fight, median 13 levies (max 26); fights where 8+ died inside 2 s: 23/24, by XM-05 Thanatos x10, Gauss Explosion x9, Blenderbeam x2
- Her army, class-matched armour (960 deaths): XM-05 Thanatos 28%, Gauss Explosion 16%, no source (debuff or unattributed) 14%, Exothermal Laser 11%, XS-01 Artemis 8%, Exoplasma Bolt 7%, Exo Pulse Laser 6%, Volatile Plasma Blast 3%. Worst 2 s per fight, median 11 levies (max 20); fights where 8+ died inside 2 s: 21/24, by XM-05 Thanatos x10, Gauss Explosion x6, no source (debuff or unattributed) x4
- Her army, no boss-life scaling (600 deaths): XM-05 Thanatos 33%, no source (debuff or unattributed) 16%, Gauss Explosion 13%, XS-01 Artemis 9%, Exothermal Laser 7%, Exoplasma Bolt 6%, Exo Pulse Laser 6%, XS-03 Apollo 3%. Worst 2 s per fight, median 12 levies (max 18); fights where 8+ died inside 2 s: 14/15, by XM-05 Thanatos x8, Gauss Explosion x3, no source (debuff or unattributed) x1
- Optimal post-Yharon army (720 deaths): no source (debuff or unattributed) 26%, XM-05 Thanatos 14%, XS-01 Artemis 12%, Gauss Explosion 10%, Exothermal Laser 7%, Exo Pulse Laser 6%, Exoplasma Bolt 5%, XF-09 Ares Laser Cannon 5%. Worst 2 s per fight, median 10 levies (max 15); fights where 8+ died inside 2 s: 13/18, by no source (debuff or unattributed) x5, Gauss Explosion x3, XS-01 Artemis x2

### What killed levies, harness attribution (last attacker NPC; env = no NPC)

- Her army (as recorded, 3.1.8 gear) (899 deaths): no attacker recorded 30%, Exoplasma Bolt 21%, XM-05 Thanatos 12%, XF-09 Ares Laser Cannon 11%, XS-01 Artemis 11%, XF-09 Ares 8%, Gauss Explosion 3%
- Her army, class-matched armour (900 deaths): no attacker recorded 27%, Exoplasma Bolt 26%, XS-01 Artemis 15%, XM-05 Thanatos 12%, XF-09 Ares Laser Cannon 11%, XS-03 Apollo 6%, Gauss Explosion 1%
- Her army, no boss-life scaling (575 deaths): no attacker recorded 30%, Exoplasma Bolt 21%, XM-05 Thanatos 16%, XS-01 Artemis 15%, XS-03 Apollo 9%, XF-09 Ares Laser Cannon 7%, Gauss Explosion 1%
- Optimal post-Yharon army (682 deaths): no attacker recorded 29%, XS-01 Artemis 19%, Exoplasma Bolt 16%, XF-09 Ares Laser Cannon 13%, XM-05 Thanatos 9%, XF-09 Ares 5%, XS-03 Apollo 5%

### Top weapons by damage to the mechs (summed over the army's fights; per levy carrying it)

- Her army (as recorded, 3.1.8 gear): DimensionTearingDisk@60 6.41M, DimensionTearingDisk@59 4.80M, PlasmaGrenade@Flawless 4.79M, PulseRifle@44 4.34M, Vega@Flawless 4.25M, PrimordialAncient@38 4.03M, PrimordialAncient@61 3.52M, ThreadOfEradication@20 3.52M, TimeBolt@Flawless 2.73M, Norfleet 2.66M
- Her army, class-matched armour: PrimordialAncient@38 11.30M, DimensionTearingDisk@59 7.97M, DimensionTearingDisk@60 7.80M, PrimordialAncient@61 7.77M, ThreadOfEradication@20 5.35M, PulseRifle@44 5.05M, Vega@Flawless 4.95M, PlasmaGrenade@Flawless 4.41M, Norfleet 3.94M, SDFMG@38 2.85M
- Her army, no boss-life scaling: DimensionTearingDisk@60 5.28M, DimensionTearingDisk@59 4.45M, Vega@Flawless 2.59M, PrimordialAncient@38 2.28M, PlasmaGrenade@Flawless 2.21M, ThreadOfEradication@20 2.16M, PrimordialAncient@61 2.11M, PulseRifle@44 2.05M, GhoulishGouger@Sleek 1.49M, TimeBolt@Flawless 1.43M
- Optimal post-Yharon army: DimensionTearingDisk@59 23.12M, PulseRifle@44 21.73M, ThreadOfEradication@20 19.32M, PlasmaGrenade@Flawless 18.76M, TimeBolt@Flawless 12.08M, Norfleet 5.72M, Vega@Flawless 4.93M

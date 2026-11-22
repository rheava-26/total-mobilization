# labcompare: today's levies (3.1.8) against the levies of the first labs

Branch `labcompare` (off `worktree-levy-stardust` 6ec3ec3 = Levy 3.1.8; lab tooling only, `levy-mod/Levy` untouched, `build.txt` untouched). Rerun 2026-11-21/22. OLD = the baselines as they were recorded; NEW = the same scenarios today. Every number below is OBSERVED unless it says inferred.

## The short answer

Today's levies are clearly better than the ones in the first labs, and the main reason is that they kill bosses two to four times faster with the same weapons. Against the very first lab (Nov 7, Levy 2.0.13), with the same armies weapon for weapon, the same bosses, the same arena and the same rules for the bosses, the pre-Hardmode army won 68% of its fights (81 of 120) where it used to win 29% (23 of 78), and it lost about a third fewer levies per fight (5.9 against 9.3). Fights that used to wipe the army every time now usually end in a kill: the Eater of Worlds went from 0 of 6 to 10 of 10, Queen Bee on Revengeance from 0 of 6 to 8 of 10, the Revengeance Brain of Cthulhu from 1 of 9 to 8 of 10, and Skeletron from 4 of 6 to 10 of 10, dying in 34 seconds instead of 171. Two fights did not move at all: the Desert Scourge still kills all 11 levies every time, and the Revengeance Eye of Cthulhu with the real 11 levies is still lost about 4 times in 5 (2 of 10 against 2 of 9), although the levies now get it down to 7% of its life instead of 18%. In Hardmode, against the Nov 15 battery (Levy 2.0.66), Expert+Rev was already almost always won and still is, a bit faster; on Master+Death the army went from 75% to 94% wins on the same armies (48 to 60 of 64) and lost about 40% fewer levies, and the hardest single test, the Master+Death Destroyer with one fixed army, went from 18% to 45% wins over 40 fights each. One big caveat cuts the other way: since 3.0, bosses hit levies the way they hit a player (Master's damage multiplier included), and with that rule on, as it ships, the same Destroyer test drops to 5% (1 of 20); the pre-Hardmode bosses are not affected by it (70% wins as shipped). Single boss rows have 2 to 10 fights, so a one- or two-fight difference there is noise; the totals and the Destroyer battery are the solid numbers.

## Tables

### Pre-Hardmode (OLD = boss-lab lab 1, Levy 2.0.13)

#### Pre-hardmode vs boss-lab lab 1 (Levy 2.0.13): win rate and kill time

| boss | mode | levies | OLD win rate (95% CI) | NEW matched win rate (95% CI) | p vs OLD | NEW shipped win rate (95% CI) | p vs OLD | OLD kill s | NEW matched kill s | NEW shipped kill s |
|---|---|---|---|---|---|---|---|---|---|---|
| King Slime | Expert | 10 | 0/6 = 0% (0-39) | 8/10 = 80% (49-94) | 0.01 | 5/10 = 50% (24-76) | 0.09 | - | 27 (21-94) | 35 (23-106) |
| King Slime | Rev | 10 | 5/6 = 83% (44-97) | 9/10 = 90% (60-98) | 1.00 | 8/10 = 80% (49-94) | 1.00 | 175 (132-248) | 69 (32-97) | 62 (34-128) |
| Desert Scourge | Expert | 11 | 0/6 = 0% (0-39) | 0/10 = 0% (0-28) | 1.00 | 0/10 = 0% (0-28) | 1.00 | - | - | - |
| Eye of Cthulhu | Expert | 11 | 3/3 = 100% (44-100) | 10/10 = 100% (72-100) | 1.00 | 10/10 = 100% (72-100) | 1.00 | 85 (81-86) | 28 (15-34) | 29 (16-41) |
| Eye of Cthulhu | Rev | 11 | 2/9 = 22% (6-55) | 2/10 = 20% (6-51) | 1.00 | 6/10 = 60% (31-83) | 0.17 | 209 (188-231) | 71 (50-92) | 82 (60-102) |
| Eye of Cthulhu | Death | 11 | 2/9 = 22% (6-55) | 5/10 = 50% (24-76) | 0.35 | 6/10 = 60% (31-83) | 0.17 | 187 (144-229) | 77 (62-97) | 67 (56-106) |
| Eater of Worlds | Expert | 13 | 0/6 = 0% (0-39) | 10/10 = 100% (72-100) | 0.00 | 10/10 = 100% (72-100) | 0.00 | - | 38 (26-89) | 38 (31-52) |
| Brain of Cthulhu | Expert | 13 | 6/6 = 100% (61-100) | 10/10 = 100% (72-100) | 1.00 | 10/10 = 100% (72-100) | 1.00 | 51 (22-99) | 20 (10-60) | 17 (10-27) |
| Brain of Cthulhu | Rev | 13 | 1/9 = 11% (2-44) | 8/10 = 80% (49-94) | 0.01 | 5/10 = 50% (24-76) | 0.14 | 174 (174-174) | 89 (68-139) | 120 (73-144) |
| Queen Bee | Expert | 15 | 0/6 = 0% (0-39) | 1/10 = 10% (2-40) | 1.00 | 4/10 = 40% (17-69) | 0.23 | - | 40 (40-40) | 42 (32-58) |
| Queen Bee | Rev | 15 | 0/6 = 0% (0-39) | 8/10 = 80% (49-94) | 0.01 | 10/10 = 100% (72-100) | 0.00 | - | 107 (54-125) | 103 (47-123) |
| Skeletron | Expert | 16 | 4/6 = 67% (30-90) | 10/10 = 100% (72-100) | 0.12 | 10/10 = 100% (72-100) | 0.12 | 171 (108-194) | 34 (30-47) | 35 (28-54) |
| **all cells** | - | - | 23/78 = 29% (21-40) | 81/120 = 68% (59-75) | 0.00 | 84/120 = 70% (61-77) | 0.00 | 144 (22-248) | 38 (10-139) | 45 (10-144) |

#### Pre-hardmode vs boss-lab lab 1 (Levy 2.0.13): deaths, army DPS, boss target

| boss | mode | OLD deaths/fight | NEW matched deaths/fight | NEW shipped deaths/fight | OLD army DPS | NEW matched army DPS | NEW shipped army DPS | OLD boss on levy % | NEW matched boss on levy % | NEW shipped boss on levy % | non-wins OLD | non-wins NEW matched | non-wins NEW shipped |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| King Slime | Expert | 4.0 | 2.3 | 2.7 | 6 | 91 | 44 | 100 | 100 | 100 | 6 timeout; boss left 49% | 2 timeout; boss left 36% | 5 timeout; boss left 21% |
| King Slime | Rev | 4.8 | 4.8 | 5.5 | 24 | 77 | 80 | 100 | 100 | 100 | 1 timeout; boss left 46% | 1 loss; boss left 17% | 2 loss; boss left 22% |
| Desert Scourge | Expert | 11.0 | 11.0 | 11.0 | 62 | 62 | 75 | 100 | 100 | 100 | 6 loss; boss left 53% | 10 loss; boss left 34% | 10 loss; boss left 46% |
| Eye of Cthulhu | Expert | 1.0 | 1.4 | 1.3 | 46 | 165 | 161 | 100 | 100 | 100 | - | - | - |
| Eye of Cthulhu | Rev | 10.3 | 10.0 | 8.7 | 20 | 66 | 70 | 96 | 100 | 100 | 6 loss, 1 timeout; boss left 18% | 8 loss; boss left 7% | 4 loss; boss left 7% |
| Eye of Cthulhu | Death | 9.3 | 8.4 | 8.3 | 24 | 67 | 72 | 96 | 100 | 100 | 7 loss; boss left 11% | 5 loss; boss left 13% | 4 loss; boss left 6% |
| Eater of Worlds | Expert | 13.0 | 2.1 | 1.5 | 195 | 559 | 516 | 100 | 100 | 100 | 6 loss; boss left 31% | - | - |
| Brain of Cthulhu | Expert | 0.0 | 0.1 | 0.0 | 64 | 145 | 150 | 100 | 100 | 100 | - | - | - |
| Brain of Cthulhu | Rev | 12.2 | 6.9 | 7.8 | 3 | 42 | 22 | 84 | 100 | 100 | 8 loss; boss left 100% | 2 loss; boss left 98% | 2 loss, 3 timeout; boss left 100% |
| Queen Bee | Expert | 15.0 | 14.1 | 13.0 | 22 | 80 | 106 | 100 | 100 | 100 | 6 loss; boss left 87% | 9 loss; boss left 8% | 6 loss; boss left 19% |
| Queen Bee | Rev | 13.7 | 6.4 | 6.4 | 18 | 98 | 100 | 93 | 100 | 100 | 3 loss, 3 timeout; boss left 37% | 2 loss; boss left 31% | - |
| Skeletron | Expert | 11.7 | 3.6 | 5.0 | 51 | 323 | 282 | 100 | 100 | 100 | 1 fled, 1 timeout; boss left 35% | - | - |
| **all cells** | - | 9.3 | 5.9 | 5.9 | 41 | 148 | 140 | 97 | 100 | 100 | 1 fled, 42 loss, 12 timeout; boss left 46% | 37 loss, 2 timeout; boss left 22% | 28 loss, 8 timeout; boss left 21% |

Same seeds only (the seeds lab 1 ran; the armies are identical seed for seed; lab 1 ran some seeds twice):

| boss | mode | seeds | OLD wins | NEW matched wins | NEW shipped wins | OLD deaths | NEW matched deaths | NEW shipped deaths |
|---|---|---|---|---|---|---|---|---|
| King Slime | Expert | 1,2,3,4,5,6 | 0/6 | 6/6 | 3/6 | 4.0 | 2.5 | 2.2 |
| King Slime | Rev | 1,2,3,4,5,6 | 5/6 | 5/6 | 5/6 | 4.8 | 4.8 | 4.8 |
| Desert Scourge | Expert | 1,2,3,4,5,6 | 0/6 | 0/6 | 0/6 | 11.0 | 11.0 | 11.0 |
| Eye of Cthulhu | Expert | 1,2,3 | 3/3 | 3/3 | 3/3 | 1.0 | 2.0 | 1.7 |
| Eye of Cthulhu | Rev | 1,2,3,4,5,6,7,8,9 | 2/9 | 2/9 | 5/9 | 10.3 | 9.9 | 8.6 |
| Eye of Cthulhu | Death | 1,2,3,4,5,6,7,8,9 | 2/9 | 4/9 | 5/9 | 9.3 | 8.4 | 8.8 |
| Eater of Worlds | Expert | 1,2,3,4,5,6 | 0/6 | 6/6 | 6/6 | 13.0 | 1.2 | 1.7 |
| Brain of Cthulhu | Expert | 1,2,3,4,5,6 | 6/6 | 6/6 | 6/6 | 0.0 | 0.2 | 0.0 |
| Brain of Cthulhu | Rev | 1,2,3,4,5,6 | 1/9 | 4/6 | 2/6 | 12.2 | 7.8 | 11.2 |
| Queen Bee | Expert | 1,2,3,4,5,6 | 0/6 | 0/6 | 2/6 | 15.0 | 15.0 | 13.7 |
| Queen Bee | Rev | 1,2,3,4,5,6 | 0/6 | 5/6 | 6/6 | 13.7 | 6.0 | 6.7 |
| Skeletron | Expert | 1,2,3,4,5,6 | 4/6 | 6/6 | 6/6 | 11.7 | 4.0 | 4.2 |

### Destroyer battery (OLD = hmbattery2, Levy 2.0.66)

#### Destroyer battery baseline vs hmbattery2 (Levy 2.0.66); Master+Death, 34 levies, one fixed army, seeds 1-40: win rate and kill time

| boss | mode | levies | OLD win rate (95% CI) | NEW matched win rate (95% CI) | p vs OLD | NEW shipped win rate (95% CI) | p vs OLD | OLD kill s | NEW matched kill s | NEW shipped kill s |
|---|---|---|---|---|---|---|---|---|---|---|
| The Destroyer | Master+Death | 34 | 7/40 = 18% (9-32) | 18/40 = 45% (31-60) | 0.01 | 1/20 = 5% (1-24) | 0.25 | 72 (58-91) | 55 (49-91) | 74 (74-74) |

#### Destroyer battery baseline vs hmbattery2 (Levy 2.0.66); Master+Death, 34 levies, one fixed army, seeds 1-40: deaths, army DPS, boss target

| boss | mode | OLD deaths/fight | NEW matched deaths/fight | NEW shipped deaths/fight | OLD army DPS | NEW matched army DPS | NEW shipped army DPS | OLD boss on levy % | NEW matched boss on levy % | NEW shipped boss on levy % | non-wins OLD | non-wins NEW matched | non-wins NEW shipped |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| The Destroyer | Master+Death | 32.2 | 29.2 | 33.9 | 1902 | 2600 | 1839 | 100 | 100 | 100 | 33 loss; boss left 15% | 22 loss; boss left 16% | 19 loss; boss left 35% |

Ablation, shipped hit rules with the old boss-life rule (`bat_parityonly`, seeds 1-12): **0/12 wins**, 34.0 deaths per fight.

### Hardmode broad sweep (OLD = hmbattery2, Levy 2.0.66), matched arm

#### Hardmode broad sweep vs hmbattery2 (Levy 2.0.66), matched arm; 3 on-tier seeds + 1 one-tier-below seed per boss and mode (rows marked with seeds ran only those seeds): win rate and kill time

| boss | mode | levies | OLD win rate (95% CI) | NEW matched win rate (95% CI) | p vs OLD | OLD kill s | NEW matched kill s |
|---|---|---|---|---|---|---|---|
| Crabulon (seeds 1,2) | Expert+Rev | 19 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 10 (10-11) | 10 (9-11) |
| Crabulon (seeds 1,2) | Master+Death | 19 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 13 (12-14) | 16 (11-21) |
| Hive Mind (seeds 1,2) | Expert+Rev | 22 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 20 (17-23) | 20 (16-24) |
| Hive Mind (seeds 1,2) | Master+Death | 22 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 34 (29-38) | 28 (28-29) |
| The Perforators (seeds 1,2) | Expert+Rev | 24 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 21 (19-23) | 21 (19-23) |
| The Perforators (seeds 1,2) | Master+Death | 24 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 30 (27-32) | 32 (30-34) |
| Deerclops (seeds 1,2) | Expert+Rev | 28 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 14 (14-15) | 12 (11-12) |
| Deerclops (seeds 1,2) | Master+Death | 28 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 17 (17-17) | 14 (13-15) |
| Wall of Flesh | Expert+Rev | 28 | 4/4 = 100% (51-100) | 4/4 = 100% (51-100) | 1.00 | 28 (24-33) | 22 (19-26) |
| Wall of Flesh | Master+Death | 28 | 4/4 = 100% (51-100) | 4/4 = 100% (51-100) | 1.00 | 40 (34-52) | 35 (30-39) |
| Queen Slime | Expert+Rev | 34 | 4/4 = 100% (51-100) | 4/4 = 100% (51-100) | 1.00 | 39 (30-88) | 31 (23-62) |
| Queen Slime | Master+Death | 34 | 4/4 = 100% (51-100) | 4/4 = 100% (51-100) | 1.00 | 57 (52-135) | 41 (30-84) |
| Cryogen | Expert+Rev | 34 | 4/4 = 100% (51-100) | 4/4 = 100% (51-100) | 1.00 | 44 (31-62) | 38 (33-49) |
| Cryogen | Master+Death | 34 | 4/4 = 100% (51-100) | 4/4 = 100% (51-100) | 1.00 | 57 (45-79) | 52 (40-87) |
| The Twins | Expert+Rev | 34 | 4/4 = 100% (51-100) | 4/4 = 100% (51-100) | 1.00 | 60 (50-91) | 51 (31-68) |
| The Twins | Master+Death | 34 | 3/4 = 75% (30-95) | 4/4 = 100% (51-100) | 1.00 | 118 (71-124) | 81 (55-131) |
| Aquatic Scourge | Expert+Rev | 34 | 4/4 = 100% (51-100) | 4/4 = 100% (51-100) | 1.00 | 87 (71-110) | 75 (56-86) |
| Aquatic Scourge | Master+Death | 34 | 4/4 = 100% (51-100) | 4/4 = 100% (51-100) | 1.00 | 147 (124-159) | 72 (23-131) |
| The Destroyer | Expert+Rev | 34 | 4/4 = 100% (51-100) | 4/4 = 100% (51-100) | 1.00 | 38 (28-63) | 34 (29-38) |
| The Destroyer | Master+Death | 34 | 0/4 = 0% (0-49) | 3/4 = 75% (30-95) | 0.14 | - | 65 (60-72) |
| Brimstone Elemental | Expert+Rev | 34 | 4/4 = 100% (51-100) | 4/4 = 100% (51-100) | 1.00 | 33 (32-42) | 31 (19-37) |
| Brimstone Elemental | Master+Death | 34 | 4/4 = 100% (51-100) | 4/4 = 100% (51-100) | 1.00 | 46 (43-61) | 50 (38-66) |
| Skeletron Prime | Expert+Rev | 34 | 4/4 = 100% (51-100) | 4/4 = 100% (51-100) | 1.00 | 41 (40-56) | 34 (26-37) |
| Skeletron Prime | Master+Death | 34 | 3/4 = 75% (30-95) | 4/4 = 100% (51-100) | 1.00 | 72 (60-78) | 47 (39-62) |
| Calamitas Clone | Expert+Rev | 34 | 4/4 = 100% (51-100) | 4/4 = 100% (51-100) | 1.00 | 72 (60-78) | 83 (74-93) |
| Calamitas Clone | Master+Death | 34 | 3/4 = 75% (30-95) | 4/4 = 100% (51-100) | 1.00 | 137 (88-148) | 108 (92-178) |
| Plantera (seeds 1,2) | Expert+Rev | 34 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 54 (50-59) | 44 (40-49) |
| Plantera (seeds 1,2) | Master+Death | 34 | 0/2 = 0% (0-66) | 2/2 = 100% (34-100) | 0.33 | - | 74 (58-90) |
| Leviathan and Anahita (seeds 1,2) | Expert+Rev | 34 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 66 (64-67) | 64 (61-67) |
| Leviathan and Anahita (seeds 1,2) | Master+Death | 34 | 2/2 = 100% (34-100) | 1/2 = 50% (9-91) | 1.00 | 77 (73-81) | 60 (60-60) |
| Astrum Aureus (seeds 1,2) | Expert+Rev | 34 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 36 (36-37) | 37 (36-38) |
| Astrum Aureus (seeds 1,2) | Master+Death | 34 | 0/2 = 0% (0-66) | 2/2 = 100% (34-100) | 0.33 | - | 40 (33-47) |
| Golem (seeds 1,2) | Expert+Rev | 34 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 33 (30-36) | 30 (29-30) |
| Golem (seeds 1,2) | Master+Death | 34 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 39 (38-40) | 33 (32-34) |
| Duke Fishron (seeds 1,2) | Expert+Rev | 34 | 0/2 = 0% (0-66) | 2/2 = 100% (34-100) | 0.33 | - | 56 (55-56) |
| Duke Fishron (seeds 1,2) | Master+Death | 34 | 0/2 = 0% (0-66) | 0/2 = 0% (0-66) | 1.00 | - | - |
| The Plaguebringer Goliath (seeds 1,2) | Expert+Rev | 34 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 184 (165-202) | 159 (132-186) |
| The Plaguebringer Goliath (seeds 1,2) | Master+Death | 34 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 254 (234-274) | 213 (195-231) |
| Empress of Light (seeds 1,2) | Expert+Rev | 34 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 66 (56-76) | 64 (45-83) |
| Empress of Light (seeds 1,2) | Master+Death | 34 | 1/2 = 50% (9-91) | 2/2 = 100% (34-100) | 1.00 | 66 (66-66) | 80 (58-102) |
| Lunatic Cultist (seeds 1,2) | Expert+Rev | 34 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 38 (37-40) | 29 (25-33) |
| Lunatic Cultist (seeds 1,2) | Master+Death | 34 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 62 (54-69) | 44 (34-55) |
| Ravager (seeds 1,2) | Expert+Rev | 34 | 1/2 = 50% (9-91) | 2/2 = 100% (34-100) | 1.00 | 136 (136-136) | 52 (50-53) |
| Ravager (seeds 1,2) | Master+Death | 34 | 0/2 = 0% (0-66) | 2/2 = 100% (34-100) | 0.33 | - | 152 (112-192) |
| Moon Lord (seeds 1,2) | Expert+Rev | 34 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 83 (82-84) | 54 (51-56) |
| Moon Lord (seeds 1,2) | Master+Death | 34 | 2/2 = 100% (34-100) | 2/2 = 100% (34-100) | 1.00 | 116 (115-118) | 79 (78-80) |
| **all bosses** | Expert+Rev | - | 61/64 = 95% (87-98) | 64/64 = 100% (94-100) | 0.24 | 42 (10-202) | 37 (9-186) |
| **all bosses** | Master+Death | - | 48/64 = 75% (63-84) | 60/64 = 94% (85-98) | 0.01 | 61 (12-274) | 55 (11-231) |

#### Hardmode broad sweep vs hmbattery2 (Levy 2.0.66), matched arm; 3 on-tier seeds + 1 one-tier-below seed per boss and mode (rows marked with seeds ran only those seeds): deaths, army DPS, boss target

| boss | mode | OLD deaths/fight | NEW matched deaths/fight | OLD army DPS | NEW matched army DPS | OLD boss on levy % | NEW matched boss on levy % | non-wins OLD | non-wins NEW matched |
|---|---|---|---|---|---|---|---|---|---|
| Crabulon (seeds 1,2) | Expert+Rev | 0.0 | 0.0 | 687 | 971 | 100 | 100 | - | - |
| Crabulon (seeds 1,2) | Master+Death | 0.0 | 0.0 | 707 | 843 | 100 | 100 | - | - |
| Hive Mind (seeds 1,2) | Expert+Rev | 0.0 | 0.0 | 564 | 851 | 100 | 100 | - | - |
| Hive Mind (seeds 1,2) | Master+Death | 0.0 | 0.0 | 432 | 728 | 100 | 100 | - | - |
| The Perforators (seeds 1,2) | Expert+Rev | 0.0 | 0.0 | 445 | 648 | 100 | 100 | - | - |
| The Perforators (seeds 1,2) | Master+Death | 0.0 | 0.5 | 400 | 529 | 100 | 100 | - | - |
| Deerclops (seeds 1,2) | Expert+Rev | 0.0 | 0.0 | 1149 | 2251 | 100 | 100 | - | - |
| Deerclops (seeds 1,2) | Master+Death | 0.0 | 0.5 | 1257 | 2343 | 100 | 100 | - | - |
| Wall of Flesh | Expert+Rev | 0.0 | 0.0 | 803 | 1545 | 100 | 100 | - | - |
| Wall of Flesh | Master+Death | 0.0 | 0.0 | 707 | 1269 | 100 | 100 | - | - |
| Queen Slime | Expert+Rev | 0.0 | 0.0 | 1250 | 2656 | 100 | 100 | - | - |
| Queen Slime | Master+Death | 0.2 | 0.2 | 1027 | 2614 | 100 | 100 | - | - |
| Cryogen | Expert+Rev | 1.0 | 0.5 | 1801 | 3347 | 100 | 100 | - | - |
| Cryogen | Master+Death | 2.0 | 2.0 | 1743 | 3161 | 100 | 100 | - | - |
| The Twins | Expert+Rev | 3.2 | 0.5 | 2090 | 2836 | 100 | 100 | - | - |
| The Twins | Master+Death | 19.0 | 7.2 | 1497 | 2105 | 85 | 100 | 1 loss; boss left 2% | - |
| Aquatic Scourge | Expert+Rev | 1.0 | 0.0 | 2983 | 3617 | 100 | 100 | - | - |
| Aquatic Scourge | Master+Death | 0.5 | 0.0 | 2302 | 6859 | 100 | 100 | - | - |
| The Destroyer | Expert+Rev | 7.2 | 6.8 | 5879 | 6766 | 100 | 100 | - | - |
| The Destroyer | Master+Death | 34.0 | 27.2 | 2602 | 4334 | 100 | 100 | 4 loss; boss left 16% | 1 loss; boss left 1% |
| Brimstone Elemental | Expert+Rev | 3.5 | 1.2 | 2276 | 4754 | 100 | 100 | - | - |
| Brimstone Elemental | Master+Death | 2.8 | 2.5 | 2073 | 3474 | 100 | 100 | - | - |
| Skeletron Prime | Expert+Rev | 5.8 | 3.2 | 1035 | 2396 | 100 | 100 | - | - |
| Skeletron Prime | Master+Death | 22.0 | 8.8 | 756 | 2048 | 100 | 100 | 1 loss; boss left 13% | - |
| Calamitas Clone | Expert+Rev | 0.0 | 0.2 | 1071 | 1529 | 100 | 100 | - | - |
| Calamitas Clone | Master+Death | 14.5 | 9.2 | 730 | 1409 | 100 | 100 | 1 loss; boss left 0% | - |
| Plantera (seeds 1,2) | Expert+Rev | 7.0 | 7.0 | 2217 | 4553 | 100 | 100 | - | - |
| Plantera (seeds 1,2) | Master+Death | 34.0 | 11.0 | 848 | 3627 | 92 | 100 | 2 loss; boss left 25% | - |
| Leviathan and Anahita (seeds 1,2) | Expert+Rev | 0.5 | 0.0 | 4492 | 4545 | 100 | 100 | - | - |
| Leviathan and Anahita (seeds 1,2) | Master+Death | 8.5 | 21.0 | 4886 | 4292 | 100 | 100 | - | 1 loss; boss left 55% |
| Astrum Aureus (seeds 1,2) | Expert+Rev | 2.0 | 2.0 | 5284 | 8711 | 100 | 100 | - | - |
| Astrum Aureus (seeds 1,2) | Master+Death | 34.0 | 1.0 | 1071 | 10560 | 100 | 100 | 2 loss; boss left 73% | - |
| Golem (seeds 1,2) | Expert+Rev | 0.0 | 0.0 | 5516 | 6075 | 100 | 100 | - | - |
| Golem (seeds 1,2) | Master+Death | 0.0 | 0.0 | 6215 | 7141 | 100 | 100 | - | - |
| Duke Fishron (seeds 1,2) | Expert+Rev | 34.0 | 4.5 | 758 | 5432 | 100 | 100 | 2 loss; boss left 78% | - |
| Duke Fishron (seeds 1,2) | Master+Death | 34.0 | 34.0 | -782 | 3269 | 100 | 100 | 2 loss; boss left 115% | 2 loss; boss left 37% |
| The Plaguebringer Goliath (seeds 1,2) | Expert+Rev | 0.0 | 0.0 | 924 | 1801 | 100 | 100 | - | - |
| The Plaguebringer Goliath (seeds 1,2) | Master+Death | 0.0 | 0.0 | 848 | 1691 | 100 | 100 | - | - |
| Empress of Light (seeds 1,2) | Expert+Rev | 0.0 | 0.0 | 2593 | 4717 | 100 | 100 | - | - |
| Empress of Light (seeds 1,2) | Master+Death | 25.0 | 0.0 | 1956 | 4727 | 100 | 100 | 1 timeout; boss left 5% | - |
| Lunatic Cultist (seeds 1,2) | Expert+Rev | 1.5 | 0.0 | 3763 | 8184 | 100 | 100 | - | - |
| Lunatic Cultist (seeds 1,2) | Master+Death | 12.0 | 9.5 | 3014 | 7208 | 100 | 100 | - | - |
| Ravager (seeds 1,2) | Expert+Rev | 32.5 | 2.5 | 1776 | 4378 | 100 | 100 | 1 loss; boss left 57% | - |
| Ravager (seeds 1,2) | Master+Death | 34.0 | 24.5 | 5660 | 2266 | 100 | 100 | 2 loss; boss left 40% | - |
| Moon Lord (seeds 1,2) | Expert+Rev | 0.0 | 0.0 | 5230 | 8226 | 99 | 99 | - | - |
| Moon Lord (seeds 1,2) | Master+Death | 0.0 | 0.0 | 4759 | 7249 | 100 | 100 | - | - |
| **all bosses** | Expert+Rev | 3.8 | 1.3 | 2305 | 3757 | 100 | 100 | 3 loss; boss left 66% | - |
| **all bosses** | Master+Death | 11.6 | 6.8 | 1817 | 3469 | 99 | 100 | 15 loss, 1 timeout; boss left 28% | 4 loss; boss left 37% |

## What the tables say (prose)

**Pre-Hardmode.** The change is in damage before anything else: army DPS against the boss went from 41 to 148 on average (about 3.6 times), with the same weapons, so every kill is two to five times quicker (Eye Expert 85 s to 28 s, King Slime Rev 175 s to 69 s, Skeletron 171 s to 34 s, Brain Expert 51 s to 20 s). Faster kills are most of why fewer levies die, but not all of it: the Eater of Worlds now costs 2 levies a fight instead of all 13, the boss fights that still end in wipes end much closer (median boss life left in the non-wins 46% then, 22% now). The cells that were already won (Eye and Brain Expert) are still won, just faster. The King Slime Expert stall of lab 1 (6 timeouts at 6 DPS, levies busy with the slimes) is gone: 8 of 10 wins at 91 DPS (cause not isolated). The two unchanged cells are the two lab 1 already called structural: the Desert Scourge drives through the 11-levy cluster and kills everyone in 25-125 s (it now ends with 34% life left instead of 53%), and the Rev/Death Eye of Cthulhu at 11 levies sits on the count cliff lab 1 measured (it needed 16-19 levies to win most fights); NEW Death Eye 5 of 10 against 2 of 9 is within noise (p = 0.35). Queen Bee on Expert (the vanilla AI under Calamity Expert) still wipes the army in 9 of 10 fights, although now with 8% of her life left instead of 87%. The Rev Brain losses are still the creeper-shield race lab 1 described (the Brain never becomes hittable before the army falls; 98-100% life left), but it now happens in 2 fights of 10 instead of 8 of 9.

**Matched vs shipped, pre-Hardmode.** Switching the 3.x enemy-side rules back on (and the single-player owner emulation) gives 70% wins against 68%, the same deaths (5.9) and the same DPS within noise; the per-boss swings (King Slime Expert 8/10 vs 5/10, Brain Rev 8/10 vs 5/10, Eye Rev 2/10 vs 6/10) go both ways and none is significant at 10 fights. So for the early bosses the answer does not depend on the arm.

**Hardmode.** Expert+Rev was won 61 of 64 times in OLD and 64 of 64 now (the three OLD losses were Duke Fishron twice and Ravager once, both now won), kills about 10% faster (median 42 s to 37 s) with a third of the deaths (3.8 to 1.3 per fight). Master+Death went from 48 of 64 (75%) to 60 of 64 (94%, p = 0.01), deaths 11.6 to 6.8 per fight; the bosses that flipped are the ones OLD lost (Destroyer 0/4 to 3/4, Plantera 0/2 to 2/2, Astrum Aureus 0/2 to 2/2, Ravager 0/2 to 2/2, Twins, Prime, Calamitas Clone and Empress losses gone), each too small to be significant alone. Still lost: Master+Death Duke Fishron (0/2 in both; the levies now take it to 37% instead of not hurting it), and one Master+Death Leviathan fight (1/2 against 2/2 OLD, noise). The army hits harder here too (army DPS roughly 1.5 to 2 times in most rows), but less than in pre-Hardmode, where the gap from 2.0.13 was larger. The Destroyer battery (40 fights each, one army) is the cleanest hardmode number: 7/40 to 18/40, p = 0.01, kill time 72 s to 55 s, deaths 32 to 29.

**The shipped hit rules on the Destroyer (Master+Death).** With every rule as it ships, the Destroyer battery is 1 of 20 (5%), deaths 33.9, boss left 35% in the losses: below OLD (18%; p = 0.25, within noise) and far below matched (45%; matched vs shipped p = 0.001). The ablation with the shipped hit rules but the old boss-life rule is 0 of 12, all 34 levies dead every time, so it is the 3.0 hit parity (levies now take hostile hits through a player's hit sequence: Master's 3x shot multiplier, the projectile's own hooks, the per-mode defense; `LEVY_PLAYERHIT` / `LEVY_PROJPARITY` / `LEVY_ARENABOX`), not bossscale, that makes this fight lethal. hmbattery2 had found that every Destroyer laser hit a levy for exactly 1 damage in 2.0.66; today those shots land at full Master damage. That is the game becoming correct rather than the levies getting worse (a player takes the same hits), but it means the matched Hardmode numbers above overstate how Master+Death plays today; the shipped Hardmode sweep was cut for time, so I cannot say how many other Master+Death bosses this moves.

**Caveats on the columns.** Hardmode-sweep army DPS is boss life removed per second (the OLD sweep rows carry no dealt-damage column); in losses it can be off because the harness samples the boss's maximum life before the army's engagement raises it (Duke Fishron OLD shows a negative number, boss left 115%). Boss-on-levy targeting is 96-100% everywhere in both OLD and NEW (lab 1's commander-targeting problem was already fixed by 2.0.14), so it explains nothing here. Fisher p-values are two-sided on the win counts; with 2-10 fights per row only the totals and the Destroyer battery reach p < 0.05.

## How the rerun was set up

Every NEW fight ran on Levy 3.1.8 (`worktree-levy-stardust` at 6ec3ec3, plus tooling commits on `labcompare` that do not touch `levy-mod/Levy`), Calamity 2.2.2, tML 2026.10.2.3, headless through the lab kit (`levy-mod/lab/levylab`, raw fight lines), one tML server at a time on a private install (`/home/user/lab/labcompare`, ports 9140-9141). Specs: `specs/prehm_*.tsv` here, and the hardmode specs written by the unchanged `levy-mod/lab/hmreplays/hm_mk.py` and `hm_battery.py` (wrapped by `mkspecs.py` with the arm's switches). Every fight was recorded (FightRecorder format 12).

**Pre-hardmode = boss-lab lab 1** (`levy-mod/Levy/docs/boss_lab/RESULTS.md`, Levy 2.0.12/2.0.13, overnight 11-07). The OLD fights are exactly the ones lab 1's `main_table.txt` counted (the `surf/far` lines), matched to their raw rows by `old_prehm.py`; the deaths per fight it reads back equal the main table's column. The NEW fights use lab 1's scenario line unchanged: `fight <boss> <mode> <seed> 18000 gear=mod n=<lab-1 count> cmd=far` (the C# tier roll, Levy grade, the surface arena, the commander parked outside the arena, 300 s cap, odd seeds Free Rein and even seeds Search & Destroy). Seeds 1-10 per cell, so every seed lab 1 ran is included. **The armies are identical: all 78 lab-1 fights were compared levy by levy (class and weapon) with the NEW fight of the same seed, 78 of 78 the same, in both arms** (`check_armies.py`). The two Calamity dud weapons of that time (Urchin Mace, Sahara Slicers) stay in the rolls, as in lab 1.

**Hardmode = hmbattery2** (`levy-mod/lab/hmbattery2/RESULTS.md`, Levy 2.0.66, 11-15), the run with the Spectator shot veto fixed, which `hmreplays` was not (`hmreplays` let no hostile projectile touch a levy, so its projectile numbers and the fights that depend on them are invalid; hmbattery2 is the valid one). Same army files (regenerated by the same unchanged scripts; the Destroyer battery loadout is byte-identical to `BATTERY_GEAR.md`), same fight lines (Master+Death or Expert+Rev, 34 levies or the real count, arena a3 and its biome variants, commander near, order Follow, the caps per boss). The broad sweep is 23 bosses x 2 modes x 4 fights (3 on-tier kits + 1 one-tier-below kit) in OLD; the Destroyer battery baseline is 40 fights with one fixed army, as OLD.

**Two arms.** Since the baselines, several rules on the ENEMY side changed too: boss life scaling now follows gear strength (bossscale, 11-20) and every hostile hit on a levy now runs a real player's hit sequence, including the difficulty multiplier on shots (projparity, 11-15). Those make the boss different, not the levies, so the main comparison switches them back:
- **matched** = Levy 3.1.8 with the enemy-side rules switched back where a switch exists: `LEVY_BOSSHP=legacy` (+2% boss life per engaged command point, cap 80%, the rule of both baselines), `LEVY_PLAYERHIT=0`, `LEVY_PROJPARITY=0`, `LEVY_ARENABOX=0` (the earlier hit intake), and for pre-hardmode also `LEVY_CONTACTPARITY=0`, `LEVY_CONTACTSLOTS=0` (the earlier contact check, from 2.0.64) and `LEVY_CR_SP=0` (lab 1 had no single-player owner emulation).
- **shipped** = Levy 3.1.8 exactly as it ships, every rule at its default; pre-hardmode with the single-player owner emulation on (what a real single-player game runs). This is the "what Eve plays" number.

**Cuts for wall time (the brief's ~5 hour budget), and one interruption.** The matched hardmode sweep ran all 4 fights per boss and mode for the first nine bosses of the spec (Destroyer, Aquatic Scourge, Wall of Flesh, Queen Slime, Twins, Skeletron Prime, Brimstone, Cryogen, Calamitas Clone) plus Plantera seed 1, then the on-tier seeds 1-2 only for the other fourteen (Plantera, Leviathan, Aureus, Golem, Fishron, Plaguebringer, Empress, Cultist, Ravager, Moon Lord, Crabulon, Hive Mind, Perforators, Deerclops): 128 of 184 fights. Every hardmode row compares OLD on exactly the seeds NEW ran (same armies), and the row says which seeds. The shipped hardmode sweep was not run at all; the shipped hardmode arm is the Destroyer battery only, seeds 1-20 (the container restarted after seed 12; seeds 13-20 ran after it, same build). A 12-fight ablation (`bat_parityonly`: shipped rules, boss life scaling legacy) separates the two enemy-side rules on the Destroyer.

### Differences I could not remove
1. **The world.** Lab 1 ran in `LevyTest.wld` (a Calamity world) and hmbattery2 in a copy of `ShipWorld.wld`; `LevyTest.wld` no longer exists, so both NEW sets run in `ShipWorld.wld`. The harness builds its own arena in either world; hmbattery2's control showed the world swap does not move the Destroyer baseline (30% vs 38%, inside noise). For pre-hardmode this is untested (inferred: small).
2. **Harness code drift.** The calrev harness grew by about 530 lines since lab 1 (lab kit loader, prefixes, recorder hooks, probes). The parts that make the fight (spec parsing, the tier roll, the arena builder, the far commander position, the result rule) read the same; the rolled armies are verified identical. The kit also sets `LEVY_CR_STEPS=120` (ticks per server turn, stepping only).
3. **Levy-side rule changes with no switch, all kept because they are the levies:** the fear model and the formation survivability model are the shipped ones (`LEVY_FEARMODEL=events`, `LEVY_SURV=formation`, both introduced after 2.0.13), the Levy-grade damage penalty is gone (0.8x in 2.0.13 and 2.0.66, 1.0x now), levies now regenerate as players, carry potion sickness, take fall damage and hazards (levyrules, 3.0), and wings and boots sit in their own slots (2.0.65; the same items).
4. **Enemy-side changes with no switch:** boss and minion targeting fixes (the Spectator is no longer picked by Calamity bosses, 2.0.14; boss parts read the target through the boss, 3.1.x), the despawn rule for a fight with no live player (never triggers here: the Spectator commander is alive), Calamity arena boxes and hit parity are switched off only in the matched arm.
5. **Command points** are now Regular 1 / Man-at-Arms 2 / Champion 3 (cpcost). Every levy in both baselines is Levy grade (Regular), 1 point then and now, so the levy counts and the legacy boss life scaling are unchanged.
6. **Run-to-run noise.** Fights are not bit-reproducible (lab 2 saw the same key give different outcomes; lab 1 itself ran some Brain seeds twice with different results). 10 fights per pre-hardmode cell and 4 per hardmode cell can only separate large effects; the Fisher p-values are in every table.
7. **No post-Moon Lord boss exists in any baseline** (the latest is Moon Lord in hmbattery2), so there is no old-vs-new post-ML comparison; Providence and The Devourer of Gods were not run, and the enormous-arena rule did not apply.

## What changed in between (from the build log and ROADMAP; which change explains which number is INFERRED unless a lab is cited)

**Lab 1 (2.0.13, 11-07 05:09) to 2.0.66 (11-15).** The first week after lab 1 was mostly boss-fight work aimed at exactly the failures lab 1 found. 2.0.14 made the Spectator commander hold against Calamity's Rev/Death targeting and made boss minions (servants, creepers, bees) go for the levies; lab 2 then measured the commander share of boss targeting at 0% (it was up to 46% in lab 1). 2.0.15 gave contact blades a ranged sidearm against bosses, 2.0.16 the worm spread, 2.0.17 made Calamity cursor-reading and holdout weapons work from levies, 2.0.18 brought player-parity hit immunity and the Skeletron flee fixes (lab 1 and 2 saw Skeletron fly off in most non-wins), 2.0.19 mana-aware weapon issue, 2.0.22 the event fear model and formation defense as defaults (lab 2's fear-off arm showed the old fear model roughly halved the army's damage: Eye Rev 2/12 to 7/12 with fear off), 2.0.23 best-ammo picks, 2.0.24 hook chains, row spread and the fight zone. Then the worm, movement and targeting rounds (worm dodge/hold/body steering, platform spread, ring formation, the Leviathan charge-lane read in 2.0.63, real whip projectiles and tag numbers 11-14), boss contact on the player's hitbox rules (2.0.64), the Wings and Boots slots (2.0.65) and the shot-veto fix (2.0.66).

**2.0.66 to 3.1.8 (11-15 to 11-21).** levyrules (3.0): the Levy-grade damage penalty was removed (Levy grade dealt 0.8x in both baselines, 1.0x now), and the gear dummy now runs the real equip path (vanilla gear never ran Calamity's PostUpdateEquips before; the dummy's max life was 100, so life-fraction effects read 400%); levies also got player regeneration, potion sickness, fall damage and hazards (costs, not gains). deadhooks and megagun (11-17): weapons whose Projectile.AI hooks never fired in levy hands work again (the "zero-damage held weapons"). weaponorigin (owner puppet), the whip and boomerang fixes, Star Shower and Doomsday parity. bodyguard (11-18): a levy the boss is hunting runs a decoy leg away from the army (its own Ravager A/B: 6/12 to 9/12 wins, 32.7 to 26.7 deaths). movement2, bosstarget, lostfight and the despawn rule (targeting and leave rules; with a live Spectator commander the despawn rule does not trigger). On the enemy side, projparity (3.0: shots on levies get the difficulty multiplier and the projectile's own hooks) and bossscale (3.1.4: boss life by gear strength) are switched off in the matched arm. cpcost changed Man-at-Arms and Champion costs; every levy here is Levy grade, unchanged.

None of these was isolated in this lab; the attribution above is from the commit and ROADMAP descriptions plus the earlier labs that measured some of them (lab 2's fear arm, bodyguard's A/B).

## Observed vs inferred
- Observed: every table number; the armies identical to lab 1 for all 78 lab-1 fights; the Destroyer ablation (shipped hit rules lose 0 of 12 with the old boss-life rule); zero lab-server exceptions in the pre-Hardmode logs.
- Inferred: which mechanical change explains which improvement (section above; nothing was isolated here except hit parity vs boss scaling on the Destroyer); that the world swap does not matter for pre-Hardmode; that the shipped hit rules would lower other Master+Death hardmode bosses too (not run).

## Files and how to rerun
- This folder: `mkspecs.py` (writes `specs/prehm_*.tsv` and the hardmode wrappers), `old_prehm.py` (lab-1 rows), `check_armies.py`, `compare.py` (`tables` prints every table above; `finish` gzips a run's recordings into the scratchpad and caches the boss-target values), `mkrest.py` (the wall-time cut). `levy-mod/lab/levylab` gained `# env: K=V` lines for raw specs and `--max-per-proc`.
- Run: `levylab run SPEC --jobs 1 --rec 99 --out RUN` with `LEVYLAB_HOME`/`LEVYLAB_SRC_TML`/`LEVYLAB_SRC_SAVE`/`LEVYLAB_WORLD`/`LEVYLAB_PORT` pointing at a private install (tML 2026.10 + the Calamity, Music, Thorium, Magic Storage, SerousCommonLib tmods from `/home/user/tml-save-2026.10/Mods`, read-only; `ShipWorld.wld`), then `compare.py finish` and `compare.py tables --prehm-matched RUN ... --hm-matched RUN1,RUN2 --bat-shipped RUN1,RUN2`.
- Recordings (FightRecorder format 12, gzip, 440 fights, 282 MB): `/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/labcompare_recs/` with `index.txt` (one line per fight: file, boss, mode, seed, result, seconds, deaths). Names: `<boss>_<mode>_n<levies>_cmdfar_s<seed>_<result>` (pre-Hardmode), `<boss>_<rev|mdeath>_<on|mid>_n<levies>_s<seed>_<result>` (sweep), `destroyer_mdeath_battery-baseline_n34_s<seed>_<result>` (battery). The lab install was deleted after the run.

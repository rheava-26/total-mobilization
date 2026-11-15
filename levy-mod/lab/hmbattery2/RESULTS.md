# Hardmode boss battery, rerun after the Spectator shot veto fix (hmbattery2)

Branch `hmbattery2`, Levy 2.0.66 (build 378e5b7 plus tooling commits; no mod code changed). The `hmreplays` battery is INVALID for projectile damage: its Spectator stand-in commander vetoed every hostile projectile that touched a levy (`SpectatorPlayer.CanHitNPC`), fixed in 2.0.66 (`Common/Potions/SpectatorShots.cs`, merged with `bossbugs`). OLD in every table = `/home/user/lab/hmreplays` (read-only), NEW = `/home/user/lab/hmbattery2`. Same armies (byte-identical army files, `BATTERY_GEAR.md` unchanged), same seeds, same specs, same arena builder; recordings are format 9.

## Read this first

**What changed between the two runs is more than the veto.** The OLD run was built from stardust as of Nov 15 03:53. Since then these merged: bosshitbox (levies take contact damage as a player does, 2.0.64), fieldfix (thorns break on levy contact, recorder format 9, officers avoid never-targets), **mobslots (dedicated Wings and Boots slots, 2.0.65)**, bossbugs (Deerclops and bees, the shot veto, recorder verdict, 2.0.66) and checkfix (checks only). So a difference between OLD and NEW is not automatically the veto. For the one cell where it mattered (Destroyer baseline) I ran controls, see "Attribution" below.

**Second difference, the army's wings and boots.** In the OLD run the harness put Angel Wings and Hermes Boots in ordinary accessory slots (the build had no Wings/Boots slots). Since mobslots (a3dd1b5) an accessory slot refuses wings and boots, and the harness (bossbugs 458539a) now places them in the new Wings and Boots slots, locked, without using an accessory slot. The item lists are unchanged (`Cloud in a Bottle, Shield of Cthulhu, <class item>, Band of Regeneration, Angel Wings` or `Hermes Boots`), so every levy carries the same items; the effect is that a levy now has the wings/boots the mobility code reads from the new slots, and its ordinary accessory slots hold only the non-mobility items. Observation: the reverse toggle does not exist on the new build (an old-style placement throws `levy cannot wear accessory Angel Wings`), so this change cannot be switched off for an A/B on the new build; the controls below use the old builds instead.

**Projectiles land now (proof run before anything big).** Master+Death, seed 1, 34 levies, one fight each, same setup as the battery:
- Moon Lord (won at 112 s, 0 deaths): levies took 13,905 projectile damage in 681 hits: Phantasmal Deathray 6,075/95, Phantasmal Sphere (True Eye) 4,200/129, Phantasmal Sphere (Hand) 2,483/235, Phantasmal Eye 525/79 + 402/95, Phantasmal Bolt 90/14 + 82/24 + 48/10. OLD recordings: 0 hits.
- Destroyer (lost at 69 s): Pink Laser (Probe) 57/57, Death Laser 11/11, Electric Laser 8/8, Cursed Laser 4/4 = 80 damage in 80 hits, next to 12,351 contact damage. Every Destroyer laser hit did exactly 1 damage (57/57, 11/11, 8/8, 4/4), so the veto removal cannot move that fight.

## Headline

1. **Across the 22 bosses both sweeps share, nothing moved in aggregate.** Wins: Master+Death 64/87 -> 62/88 (Fisher p = 0.74), Expert+Rev 82/88 -> 82/88. Levy deaths per fight 12.6 -> 13.5 (Master+Death) and 4.7 -> 4.8 (Expert+Rev). Contact damage taken per fight is flat (6,102 -> 6,283 and 3,493 -> 3,459); **projectile damage taken per fight went from 24 -> 1,262 (Master+Death) and 16 -> 1,539 (Expert+Rev)**. The veto removal changes what levies take far more than how fights end.
2. **Moon Lord got slower, not harder to win.** 4/4 and 4/4 in both modes with 0 levy deaths; kill time Master+Death 90 -> 113 s, Expert+Rev 76 -> 83 s; levies now take 13,348 / 9,232 projectile damage per fight. Aquatic Scourge: 8/8 in both runs, kill time Master+Death 114 -> 147 s, Expert+Rev 69 -> 87 s, first levy deaths (0.5 and 1.0 per fight) and, in Master+Death, 1,658 contact + 778 projectile damage per fight (contact was 0 before). The Aquatic Scourge battery baseline agrees: 40/40 -> 20/20, median 131 -> 137 s, deaths 0.0 -> 0.5.
3. **Destroyer Master+Death got harder: battery baseline 38% -> 18% (15/40 -> 7/40, p = 0.078)**; flat arena 20% -> 0%; 12 and 24 levies stay at 0%; 36 levies 35% -> 25%; no wings 20% -> 20%. Projectile damage is 86 per fight against 12.8k contact, and the veto-on control on the new build is 10% (4/40), so the shift is **not the veto**: see Attribution. The broad-sweep Destroyer agrees in direction (Master+Death 2/4 -> 0/4).
4. **Other bosses whose win count moved (n = 4 per cell, so each is a hint, not a result):** Plantera Master+Death 2/4 -> 0/4 (the Plantera battery fight is the visible-commander one, so its OLD projectile damage was real: 528 -> 642); Skeletron Prime Master+Death 4/4 -> 3/4 with levy deaths 9.2 -> 22.0 and contact 3,500 -> 8,370; Calamitas Clone Master+Death 4/4 -> 3/4, deaths 3.5 -> 14.5, contact 4,892 -> 9,884; Astrum Aureus Expert+Rev 4/4 -> 3/4. Easier: Lunatic Cultist Master+Death 2/4 -> 4/4 (deaths 21.2 -> 15.8; the wins are slower, 49 -> 67 s), Leviathan and Anahita Master+Death 1/4 -> 3/4, Ravager Expert+Rev 2/4 -> 3/4. Skeletron Prime and Calamitas Clone show a CONTACT rise, which the veto cannot explain; that is the bosshitbox/mobslots interval and deserves its own A/B if Eve wants those two investigated.
5. **Deerclops, new:** 4/4 in both modes (kill 16 s Expert+Rev, 18 s Master+Death), 0 deaths, 116-341 contact and ~150-165 projectile damage per fight. The crash is gone.
6. **Boss-target split (recorder format 9).** All 344 fights: the main boss's target field reads "levy lock" (kind 2) 93-100% of samples, "commander" (kind 1) 0% except The Twins (7%) and Plantera (7%, visible-commander arm), and **"spite" (kind 3) in none of the 344 fights (0.0% everywhere)**. In this harness (a Spectator commander and an army that never leaves the arena) spite never occurs, so OLD's merged field (100%) was the levy lock alone; the split does not change any earlier reading.

## Attribution of the Destroyer shift (controls, Master+Death, army and seeds 1-40 identical)

Single-change controls on the baseline cell (`hm_controls.sh`). The ladder runs OLD code -> NEW code one merge group at a time:

| run | what differs from NEW | fights | win rate (95% CI) | kill s median (p10-p90) | levy deaths/fight | contact | projectile |
|---|---|---|---|---|---|---|---|
| OLD run (hmreplays, recorded) | old build, old harness, veto on | 40 | 15/40 = 38% (24-53) | 67 (51-85) | 30.3 | 12322 | 0 |
| NEW run (hmbattery2) | - | 40 | 7/40 = 18% (9-32) | 72 (59-82) | 32.2 | 12803 | 86 |
| control: oldbuild | the hmreplays code (before bosshitbox, fieldfix, Wings/Boots slots, the shot fix), re-run on this box and world | 40 | 12/40 = 30% (18-45) | 64 (52-76) | 31.1 | 12328 | 0 |
| control: hitbox | Levy 2.0.64: + bosshitbox (contact damage as a player takes it) | 40 | 10/40 = 25% (14-40) | 64 (52-79) | 31.3 | 12943 | 0 |
| control: preslots | + fieldfix, up to the commit before the Wings/Boots slots | 40 | 12/40 = 30% (18-45) | 59 (58-75) | 30.9 | 12607 | 0 |
| control: noveto | this build with LEVY_SPECSHOTS=0: the Spectator commander vetoes hostile shots again | 40 | 4/40 = 10% (4-23) | 65 (60-75) | 32.9 | 12797 | 0 |

Fisher exact p-values: OLD recorded 15/40 vs NEW 7/40 = 0.078; re-run of the OLD code 12/40 vs NEW 7/40 = 0.29; the commit before the Wings/Boots slots (12/40) vs this build with the veto switched back on (4/40) = **0.048**; veto on (4/40) vs veto off = NEW (7/40) = 0.52.

Reading it: the OLD battery reproduces on this box and in this world (30% against the recorded 38%, inside noise), so the harness and the substituted test world are not the cause. bosshitbox (25%) and fieldfix (30%) leave the Destroyer where it was. Removing the veto does not hurt (10% -> 18%, the wrong direction for "projectiles made it harder"). The drop sits between "commit before the slots" and "this build with the veto on", an interval whose only gameplay change is mobslots (Wings and Boots slots, plus the harness now filling them) with bossbugs' Deerclops/bee and recorder fixes and checkfix's check-only changes. That is an inference from the commit titles: I did not run a switch that isolates the slots alone, because none exists, and 40 fights per arm cannot resolve differences under about 20 points. If Eve wants the slots pinned down, the next step is a build with `MobilitySlots` forced off.

## Battery, Master+Death: OLD vs NEW

Same baseline army written once and reused, only the seed varies; cells change one thing. Win rate with Wilson 95% CI, kill time of the wins (median, p10-p90), Fisher p of OLD vs NEW win counts. Aquatic Scourge was rerun at baseline only (20 fights); the other Aquatic Scourge cells have OLD data only.

### Battery, Master+Death: OLD (hmreplays, Spectator veto on projectiles) vs NEW (hmbattery2)

| boss | cell | OLD win rate (95% CI) | NEW win rate (95% CI) | Fisher p | OLD kill s median (p10-p90) | NEW kill s median (p10-p90) | OLD levy deaths/fight | NEW levy deaths/fight |
|---|---|---|---|---|---|---|---|---|
| destroyer | baseline (34 levies) | 15/40 = 38% (24-53) | 7/40 = 18% (9-32) | 0.078 | 67 (51-85) | 72 (59-82) | 30.3 | 32.2 |
| destroyer | 12 levies | 0/20 = 0% (-0-16) | 0/20 = 0% (-0-16) | 1.000 | - | - | 12.0 | 12.0 |
| destroyer | 24 levies | 0/20 = 0% (-0-16) | 0/20 = 0% (-0-16) | 1.000 | - | - | 24.0 | 24.0 |
| destroyer | 36 levies | 7/20 = 35% (18-57) | 5/20 = 25% (11-47) | 0.731 | 60 (48-67) | 58 (51-72) | 31.4 | 33.2 |
| destroyer | no wings | 4/20 = 20% (8-42) | 4/20 = 20% (8-42) | 1.000 | 74 (63-84) | 78 (66-86) | 32.3 | 32.1 |
| destroyer | flat arena | 4/20 = 20% (8-42) | 0/20 = 0% (-0-16) | 0.106 | 72 (70-80) | - | 31.2 | 34.0 |
| aqua | baseline (34 levies) | 40/40 = 100% (91-100) | 20/20 = 100% (84-100) | 1.000 | 131 (97-147) | 137 (118-152) | 0.0 | 0.5 |
| aqua | 12 levies | 13/20 = 65% (43-82) | - | nan | 385 (205-412) | - | 4.0 | - |
| aqua | 24 levies | 20/20 = 100% (84-100) | - | nan | 152 (78-188) | - | 0.0 | - |
| aqua | 36 levies | 20/20 = 100% (84-100) | - | nan | 138 (109-153) | - | 0.0 | - |
| aqua | no wings | 20/20 = 100% (84-100) | - | nan | 141 (47-151) | - | 0.0 | - |
| aqua | flat arena | 19/19 = 100% (83-100) | - | nan | 187 (166-228) | - | 2.6 | - |

### Battery damage taken by levies per fight (mean), OLD -> NEW

| boss | cell | contact OLD | contact NEW | projectile OLD (hits) | projectile NEW (hits) | NEW head / body / tail | NEW boss target: levy lock % | spite % | commander % | OLD target field (lock + spite merged) % |
|---|---|---|---|---|---|---|---|---|---|---|
| destroyer | baseline (34 levies) | 12322 | 12803 | 0 (0) | 86 (86) | 7279 / 5473 / 51 | 100 | 0 | 0 | 100 |
| destroyer | 12 levies | 4769 | 4616 | 0 (0) | 102 (102) | 2726 / 1871 / 19 | 100 | 0 | 0 | 100 |
| destroyer | 24 levies | 9229 | 8743 | 0 (0) | 120 (120) | 5012 / 3696 / 35 | 100 | 0 | 0 | 100 |
| destroyer | 36 levies | 12974 | 13383 | 0 (0) | 82 (82) | 7345 / 5964 / 71 | 100 | 0 | 0 | 100 |
| destroyer | no wings | 12974 | 12591 | 0 (0) | 88 (88) | 6962 / 5536 / 89 | 100 | 0 | 0 | 100 |
| destroyer | flat arena | 12410 | 12278 | 0 (0) | 111 (111) | 6564 / 5661 / 50 | 100 | 0 | 0 | 100 |
| aqua | baseline (34 levies) | 0 | 647 | 0 (0) | 464 (390) | 0 / 0 / 0 | 100 | 0 | 0 | 100 |
| aqua | 12 levies | 2330 | - | 0 (0) | - (-) | - / - / - | - | - | - | 100 |
| aqua | 24 levies | 0 | - | 0 (0) | - (-) | - / - / - | - | - | - | 100 |
| aqua | 36 levies | 0 | - | 0 (0) | - (-) | - / - / - | - | - | - | 100 |
| aqua | no wings | 0 | - | 0 (0) | - (-) | - / - / - | - | - | - | 100 |
| aqua | flat arena | 3775 | - | 0 (0) | - (-) | - / - / - | - | - | - | 100 |

Damage columns: contact = head + body + tail + other contact of the boss; projectile = hostile projectiles (hits in brackets). The Destroyer's head / body / tail split is in the NEW column; OLD split was 6,732 / 5,494 / 93 at baseline. The OLD target field was a single merged number; NEW separates lock, spite and commander.

## Broad sweep: OLD vs NEW

On-tier kit seeds 1-3 plus one-tier-below seed 4, per boss and mode, 34 levies except Wall of Flesh 28, Deerclops 28, Crabulon 19, Hive Mind 22, The Perforators 24. Deerclops is new (it crashed in the OLD run). OLD had 175 fights (Duke Fishron Master+Death and Expert+Rev both 0/4 in both runs; one Plaguebringer fight missing from OLD), NEW has all 184.

### Broad sweep: OLD vs NEW (3 on-tier seeds + 1 one-tier-below seed per boss and mode)

| boss | levies | mode | OLD W / L / other (n) | NEW W / L / other (n) | OLD kill s | NEW kill s | OLD deaths/fight | NEW deaths/fight | contact OLD -> NEW | projectile OLD -> NEW (hits NEW) | NEW boss target: lock / spite / cmdr % |
|---|---|---|---|---|---|---|---|---|---|---|---|
| The Destroyer | 34 | Exp+Rev | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 39 | 38 | 11.0 | 7.2 | 7812 -> 7048 | 0 -> 48 (16) | 100 / 0 / 0 |
| The Destroyer | 34 | Master+Death | 2 / 2 / 0 (4) | 0 / 4 / 0 (4) | 62 | - | 29.5 | 34.0 | 11788 -> 12868 | 0 -> 97 (96) | 100 / 0 / 0 |
| Aquatic Scourge | 34 | Exp+Rev | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 69 | 87 | 0.0 | 1.0 | 2011 -> 3212 | 0 -> 4062 (404) | 100 / 0 / 0 |
| Aquatic Scourge | 34 | Master+Death | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 114 | 147 | 0.0 | 0.5 | 0 -> 1658 | 0 -> 778 (416) | 100 / 0 / 0 |
| Wall of Flesh | 28 | Exp+Rev | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 27 | 28 | 0.0 | 0.0 | 0 -> 0 | 0 -> 35 (28) | 100 / 0 / 0 |
| Wall of Flesh | 28 | Master+Death | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 34 | 40 | 0.0 | 0.0 | 40 -> 16 | 0 -> 94 (88) | 100 / 0 / 0 |
| Queen Slime | 34 | Exp+Rev | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 33 | 39 | 0.0 | 0.0 | 384 -> 656 | 0 -> 496 (23) | 100 / 0 / 0 |
| Queen Slime | 34 | Master+Death | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 45 | 57 | 0.0 | 0.2 | 1558 -> 1247 | 0 -> 3878 (154) | 100 / 0 / 0 |
| The Twins | 34 | Exp+Rev | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 62 | 60 | 3.0 | 3.2 | 4752 -> 3996 | 0 -> 714 (138) | 100 / 0 / 0 |
| The Twins | 34 | Master+Death | 3 / 1 / 0 (4) | 3 / 1 / 0 (4) | 80 | 118 | 21.0 | 19.0 | 11492 -> 10292 | 0 -> 314 (248) | 85 / 0 / 15 |
| Skeletron Prime | 34 | Exp+Rev | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 38 | 41 | 3.5 | 5.8 | 1285 -> 2948 | 0 -> 379 (88) | 100 / 0 / 0 |
| Skeletron Prime | 34 | Master+Death | 4 / 0 / 0 (4) | 3 / 1 / 0 (4) | 59 | 72 | 9.2 | 22.0 | 3500 -> 8370 | 0 -> 150 (117) | 100 / 0 / 0 |
| Brimstone Elemental | 34 | Exp+Rev | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 35 | 33 | 1.5 | 3.5 | 1268 -> 2111 | 0 -> 373 (70) | 100 / 0 / 0 |
| Brimstone Elemental | 34 | Master+Death | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 50 | 46 | 2.8 | 2.8 | 3623 -> 3704 | 0 -> 242 (170) | 100 / 0 / 0 |
| Cryogen | 34 | Exp+Rev | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 40 | 44 | 0.2 | 1.0 | 1748 -> 1755 | 0 -> 810 (100) | 100 / 0 / 0 |
| Cryogen | 34 | Master+Death | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 52 | 57 | 1.8 | 2.0 | 3341 -> 2978 | 0 -> 173 (98) | 100 / 0 / 0 |
| Calamitas Clone | 34 | Exp+Rev | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 67 | 72 | 0.0 | 0.0 | 1073 -> 1310 | 0 -> 1336 (214) | 100 / 0 / 0 |
| Calamitas Clone | 34 | Master+Death | 4 / 0 / 0 (4) | 3 / 1 / 0 (4) | 118 | 137 | 3.5 | 14.5 | 4892 -> 9884 | 0 -> 679 (485) | 100 / 0 / 0 |
| Plantera | 34 | Exp+Rev | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 49 | 51 | 9.5 | 6.5 | 5456 -> 4716 | 346 -> 285 (98) | 99 / 0 / 1 |
| Plantera | 34 | Master+Death | 2 / 2 / 0 (4) | 0 / 4 / 0 (4) | 93 | - | 27.2 | 34.0 | 10618 -> 12033 | 528 -> 642 (446) | 88 / 0 / 12 |
| Leviathan and Anahita | 34 | Exp+Rev | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 62 | 69 | 0.2 | 0.2 | 3930 -> 2218 | 0 -> 6181 (311) | 100 / 0 / 0 |
| Leviathan and Anahita | 34 | Master+Death | 1 / 3 / 0 (4) | 3 / 1 / 0 (4) | 67 | 81 | 25.5 | 19.2 | 10823 -> 8366 | 0 -> 2097 (276) | 100 / 0 / 0 |
| Astrum Aureus | 34 | Exp+Rev | 4 / 0 / 0 (4) | 3 / 1 / 0 (4) | 42 | 37 | 8.5 | 10.0 | 6847 -> 7933 | 0 -> 492 (62) | 100 / 0 / 0 |
| Astrum Aureus | 34 | Master+Death | 1 / 3 / 0 (4) | 1 / 3 / 0 (4) | 55 | 54 | 29.5 | 32.2 | 13064 -> 12761 | 0 -> 70 (56) | 100 / 0 / 0 |
| Golem | 34 | Exp+Rev | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 32 | 35 | 1.8 | 1.5 | 2561 -> 2248 | 0 -> 766 (105) | 100 / 0 / 0 |
| Golem | 34 | Master+Death | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 37 | 39 | 6.8 | 5.8 | 6240 -> 5674 | 0 -> 361 (166) | 100 / 0 / 0 |
| Duke Fishron | 34 | Exp+Rev | 0 / 4 / 0 (4) | 0 / 4 / 0 (4) | - | - | 34.0 | 34.0 | 14980 -> 14634 | 0 -> 648 (52) | 100 / 0 / 0 |
| Duke Fishron | 34 | Master+Death | 0 / 4 / 0 (4) | 0 / 4 / 0 (4) | - | - | 34.0 | 34.0 | 13236 -> 12479 | 0 -> 73 (66) | 100 / 0 / 0 |
| The Plaguebringer Goliath | 34 | Exp+Rev | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 180 | 184 | 0.0 | 0.0 | 3304 -> 3254 | 0 -> 1050 (171) | 100 / 0 / 0 |
| The Plaguebringer Goliath | 34 | Master+Death | 3 / 0 / 0 (3) | 3 / 0 / 1 (4) | 223 | 234 | 2.3 | 0.0 | 8307 -> 7985 | 0 -> 983 (374) | 100 / 0 / 0 |
| Empress of Light | 34 | Exp+Rev | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 56 | 69 | 0.0 | 0.0 | 1659 -> 1224 | 0 -> 6742 (422) | 100 / 0 / 0 |
| Empress of Light | 34 | Master+Death | 2 / 0 / 2 (4) | 2 / 0 / 2 (4) | 96 | 89 | 26.2 | 26.0 | 6475 -> 6412 | 0 -> 3188 (666) | 100 / 0 / 0 |
| Lunatic Cultist | 34 | Exp+Rev | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 36 | 41 | 0.8 | 1.2 | 3498 -> 4177 | 0 -> 179 (132) | 100 / 0 / 0 |
| Lunatic Cultist | 34 | Master+Death | 2 / 2 / 0 (4) | 4 / 0 / 0 (4) | 49 | 67 | 21.2 | 15.8 | 10068 -> 8520 | 0 -> 366 (308) | 100 / 0 / 0 |
| Ravager | 34 | Exp+Rev | 2 / 2 / 0 (4) | 3 / 1 / 0 (4) | 55 | 136 | 29.0 | 31.0 | 13424 -> 12028 | 0 -> 14 (4) | 100 / 0 / 0 |
| Ravager | 34 | Master+Death | 0 / 4 / 0 (4) | 0 / 4 / 0 (4) | - | - | 34.0 | 34.0 | 13514 -> 11342 | 0 -> 11 (2) | 100 / 0 / 0 |
| Moon Lord | 34 | Exp+Rev | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 76 | 83 | 0.0 | 0.0 | 16 -> 0 | 0 -> 9232 (291) | 99 / 0 / 0 |
| Moon Lord | 34 | Master+Death | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 90 | 113 | 0.0 | 0.0 | 18 -> 46 | 0 -> 13348 (678) | 100 / 0 / 0 |
| Crabulon | 19 | Exp+Rev | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 12 | 12 | 0.0 | 0.0 | 246 -> 127 | 0 -> 2 (2) | 100 / 0 / 0 |
| Crabulon | 19 | Master+Death | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 15 | 14 | 0.0 | 0.0 | 678 -> 254 | 0 -> 9 (9) | 100 / 0 / 0 |
| Hive Mind | 22 | Exp+Rev | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 18 | 21 | 0.0 | 0.0 | 338 -> 286 | 0 -> 0 (0) | 100 / 0 / 0 |
| Hive Mind | 22 | Master+Death | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 32 | 34 | 0.0 | 0.0 | 553 -> 513 | 0 -> 86 (86) | 100 / 0 / 0 |
| The Perforators | 24 | Exp+Rev | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 22 | 20 | 0.0 | 0.0 | 248 -> 210 | 0 -> 5 (5) | 100 / 0 / 0 |
| The Perforators | 24 | Master+Death | 4 / 0 / 0 (4) | 4 / 0 / 0 (4) | 30 | 31 | 0.0 | 0.0 | 956 -> 821 | 0 -> 123 (123) | 100 / 0 / 0 |
| Deerclops | 28 | Exp+Rev | - | 4 / 0 / 0 (4) | - | 16 | - | 0.0 | - -> 116 | - -> 165 (59) | 100 / 0 / 0 |
| Deerclops | 28 | Master+Death | - | 4 / 0 / 0 (4) | - | 18 | - | 0.0 | - -> 341 | - -> 151 (98) | 100 / 0 / 0 |

fights: OLD 175, NEW 184

## Anything that looks like a bug

Silently-caught exceptions in the server logs over the 344 fights of the battery and sweep (no server restarted, no fight lost; the fight named is the nearest preceding fight key in the log):
- `Terraria.Projectile.Kill()` IndexOutOfRange through `Levy.Common.Owner.UseGuard.KillDetour`: once, Queen Slime Master+Death seed 3. This one is in Levy code and worth a look.
- Calamity `PlanteraAI.HookAI.AI` line 1119 (`WorldGen.SolidTile` index out of range): twice in Plantera Master+Death seed 1 (the visible-commander arm in the jungle arena). Calamity code; the tile probe leaves the world.
- Calamity `CalamityVanillaAIOverrideNPC.PreAI` NullReferenceException through Levy's `CalamityTargetingCompat.PreAiDetour`: Moon Lord, every Moon Lord fight that logged one (Expert+Rev seed 1, Master+Death seed 1, and the proof run). Possibly a Moon Lord part with a null target at death; Levy's detour is in the stack.
- Calamity `CalamityGlobalProjectile.CanHitNPC` IndexOutOfRange: once, Wall of Flesh Master+Death seed 3.
- Harness `CombatHarness.CrPinStrays` line 488 IndexOutOfRange: twice, Wall of Flesh (the Underworld arena). Harness-only.
Not a bug but worth knowing: with the veto gone, Moon Lord and Aquatic Scourge fights are slower (see Headline), and the Destroyer cells and the Skeletron Prime / Calamitas Clone Master+Death contact numbers rose with no projectile explanation.

## Files

- Recordings (format 9, gzip `.levyrec`): `/home/user/lab/hmbattery2/recordings/<boss>/` (184 sweep fights, `fights.csv`) and `/home/user/lab/hmbattery2/battery/<boss>/<cell>/` (160 battery fights, `fights.csv`). New `fights.csv` columns: `tgtLevyPct`, `tgtSpitePct`, `tgtPlayerPct`, `format`, and for the sweep `takenContact`, `takenProj`, `takenProjHits`.
- Control run dirs: `/home/user/lab/hmbattery2/work/ctl_*/run/results.csv`. Copies of both `fights.csv` files are in this folder.
- Tooling (all in `levy-mod/lab/hmreplays/`): `hm_run_all.sh` (whole run, one command), `hm_controls.sh`, `hm_compare.py` (these tables), `hm_hits.py` (shared stats and damage split), plus the existing `hm_mk.py`, `hm_battery.py`, `hm_collect.py`, `hm_bcollect.py`, `hm_site.py`, `hm_btable.py`, `hm_table.py`, `hm_check_site.js`. The lab root is the `HM_LAB` environment variable (default `/home/user/lab/hmbattery2`).
- Setup note: `/home/user/lab/hmbattery2/kitsrc` holds private copies of tML 2026.10, the Calamity/Music/SerousCommonLib/MagicStorage/Thorium tmods, and a test world. `LevyTest.wld` no longer exists after the container reset, so the lab world is a copy of `ShipWorld.wld` (the small vanilla world the ship scripts use); the harness builds its own arenas, and the OLD-code control (30%) shows the substitute does not move the Destroyer baseline. Mechanization.tmod is not in the kit (no boss-lab fight uses it).
- Site: `/home/user/lab/hmbattery2/site`, four batches `b01`-`b04`, 229 fights (all 184 sweep fights, 35 Destroyer and 10 Aquatic Scourge battery fights: 10 baseline, 5 per other cell), 6 Hz, 192 MB; publish `b01` .. `b04` in order to the same URL. Headless Chromium: 30 fights loaded clean (at least one per set, 28 sets), 0 pageerrors, no "Could not load".


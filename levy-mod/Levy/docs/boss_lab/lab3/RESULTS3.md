# Boss lab 3 (branch `lab3`, 2026-11-07): realistic arena, real stations, supplied stats, ammo, new bosses, rogue

Raw rows: `raw3/*.tsv` (one calrev row per fight) and `raw3/*.l3.tsv` (lab-3 columns: stations, buffs, vertical use, poison/irradiated seconds, hazard pay, weapons+ammo). Spec files: `specs/`. Generators and tables: `scripts/gen_specs3.py`, `scripts/tab3.py`. Harness: `Common/Testing/CombatHarness.Lab3.cs` plus small hooks in `CombatHarness.CalRev.cs` / `CalRevLab.cs`. Replays (20, with notes): `/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad/replays/lab3/` (`index.txt`); all recordings: `/home/user/lab/rec3/<spec>/`.
Labels: WIN = boss killed (all worm segments); DESPAWN = boss left; FLED = Skeletron/Queen Bee timeout whose boss travelled over 1500 tiles (an unrecorded despawn; none occurred); TIMEOUT = 300 s cap; wipe = all levies dead. Everything below is **observed** unless marked *inferred*.

## 0. Setup
- Build: Levy with the merged `worktree-levy-stardust` and `rec2` (recorder format 5, adds). Private install `/home/user/lab/tml3 + save3` and `tml4 + save4` (2 servers). Calamity 2.2.2, tML 2026.10, `LEVY_CR_SP=1`, `cmd=near`, 6 seeds, cap 300 s.
- Fear: `LEVY_FEARMODEL=events`, `LEVY_SURV=formation` for every fight (the arm-(a) default).
- **Arena (`arena=a3`, enclosed variant `a3e`):** 220 tiles wide (walls at +-110), solid floor, 5 platform rows 7 tiles apart (rows at 7,14,21,28,35 tiles above the floor), each row spans the whole width except a 14-tile gap that moves from row to row so rows connect. Open to the sky above 90 tiles; `a3e` is a sealed box in the rock layer. Levies spawn **inside** the arena, in a row 3 tiles apart around x = centre - 6 on the floor; the commander (Spectator) stands at the same spot. Worms spawn 80 tiles to the right (inside the arena). Queen Bee uses `jungle=1`. Biome flags: Hive Mind corruption, Perforators crimson, EoW corruption, Brain crimson, DS desert.
- Real command points (Calamity world): KS 10, DS 11, Eye 11, EoW/Brain 13, Hive Mind 13, Perforators 13, Queen Bee 15, Skeletron 16, Slime God 16. Vanilla-only world (no Calamity): KS 10, Eye 11, EoW/Brain 12, QB 13, Skeletron 14.
- **Bug found and fixed in the harness:** the old harness record starts at 15 life / 9 mana crystals (500 HP, 200 mana) and the first lab-3 smoke rows therefore had full stats. Lab 3 zeroes them and applies the tier targets through `RosterSystem.FeedCrystal`, one crystal at a time (the armory's own call). Lab 1 and 2 used the tier-table HP too (`LifeCrystals` from the roll), so those are comparable.

## 1. Base buffs by real availability (placed as real tiles beside three real armories)
Sources: `terraria.wiki.gg` raw pages (curl `?action=raw`). `calamitymod.wiki.gg` redirects every one of these pages to the Terraria wiki (`tgc:`), so Calamity changes none of them (observed).

| item | source (wiki) | first tier placed in the lab | why |
|---|---|---|---|
| Campfire | crafted by hand (wood + torches) | every fight | pre-boss |
| Heart Lantern | crafted by hand: Life Crystal + Chain | every fight | pre-boss (costs a Life Crystal) |
| Star in a Bottle | crafted by hand: Bottle + Fallen Star | every fight | pre-boss |
| Sunflower | Dryad sells it; also generates on grass | every fight | pre-boss (placed on grass) |
| Bast Statue | Sandstone Chests in the lower Underground Desert, Oasis/Mirage Crates, desert cabins | Eye of Cthulhu on | needs desert exploration; a player can have it earlier, Eye is my cut (*assumption*) |
| Sharpening Station | Underground Jungle cabins (Merchant sells it in Hardmode only) | Eater of Worlds / Brain on (also Hive, Perforators, QB, Skeletron) | jungle cabins, not before the evil-boss tier |
| War Table | dropped by Dark Mage / Ogre in Old One's Army (Tavernkeep appears after EoW or BoC; crystal stand 1 gold) | Slime God only (post-Skeletron) | needs the Tavernkeep + the event; I did not place it earlier |
| Bewitching Table | Dungeon (placed), Dungeon fishing; Witch Doctor sells it only in Hardmode | Slime God only (post-Skeletron) | the Dungeon opens at Skeletron |
| Ammo Box | Arms Dealer, **Hardmode** | never | Hardmode (Eve) |
| Crystal Ball | Wizard, **Hardmode** | never | Hardmode (Eve) |
| Honey | honey pool | never | Levy has no honey flag in `StationFlags` (see section 9) |

Observed, sampled on every living levy at tick 600 (`StationBuffSystem` scan of the real tiles, the same code the game runs; column `stationFlags` in the `.l3.tsv` rows; zero placement failures): KS and DS levies have Campfire, Heart Lantern, Star in a Bottle, Sunflower. Eye: + Cat Bast (defense +5). EoW through Skeletron (also Hive, Perforators, QB): + Sharpening Station. Slime God: + Bewitching, War Table. Average defense at 10 s: KS 13.7, DS 12.8, Eye 15.5, EoW/Brain 21-22, QB 23, Skeletron 22. The stations are free for the levy here: the real game needs the player to click a Sharpening Station, Bewitching Table, War Table, Ammo Box and Crystal Ball once to get the buff, Levy's scan hands them to every soldier in 40 tiles of an armory with no click (*inferred from the tile conditions in `StationBuffSystem`; not a harness difference*).

## 2. Supplied HP / mana (real supply path)
Life crystals fed per tier: KS 2, DS 4, Eye 6, EoW/Brain 8, Hive/Perforators 9, QB 10, Skeletron 12, Slime God 14, i.e. **HP 140, 180, 220, 260, 280, 300, 340, 380** (base 100, +20 each; +40 per boss tier, 340 at Skeletron, cap 500 never reached). Mana: mages (30 percent of the army) 9 crystals = 200 from the first fight; others 2/3/4 crystals = 60/80/100 at KS/DS/Eye and 200 from EoW/Brain on (observed average max mana 108, 118, 131, 205, 205, 205, 208, 211).

## 3. Gear
Tier tables of lab 2 (`LOADOUTS.md`, Levy-grade levies, one accessory slot), Boss Checklist order with Calamity. For EoW, Brain, DS, Hive Mind and Perforators ranged and magic weapons that pierce (projectile `penetrate` > 1 or -1, read from `ContentSamples`) get three times the roll weight (`L3Pierces`). Accessory: Hunter/melee/mage/summoner class accessory (Feral Claws, Band of Starpower, Hermes Boots, Band of Regeneration). **Team-buff accessory:** the only team item a player can have before Hardmode is Calamity's **Trinket of Chi** (shrine chests, Pre-Hardmode; wiki). One copy is worn by the first mage from the Eye tier on; observed buff `ChiRegenBuff` on every other levy (10 of 11 at the Eye ... 15 of 16). Vanilla team accessories (Paladin's Shield family) are Hardmode; Rampart of Deities, Affliction, Absorber are later (not placed). Levy-grade levies have one slot, so the wearer gives up his class accessory.

## 4. Ammo
Probe of the projectile data (log `HARNESS-L3: ammo`, observed):

| ammo | damage | projectile penetrate | note |
|---|---|---|---|
| Musket Ball | 7 | 1 | plain |
| Silver Bullet | 8 | 1 | |
| Meteor Shot | 8 | 2 (local immunity 15) | also bounces (aiStyle 1, tile collide) |
| Flaming Arrow | 7 | 1 | plain arrow in the old lab |
| Jester's Arrow | 10 | -1 (unlimited pierce) | chosen vs worms, Brain, DS |
| Unholy Arrow | 12 | 5 | |
| Hellfire Arrow | 13 | -1 | from the Skeletron tier on in the lab |
| Bone Arrow | 8 | 2 | |

Rules used in arm (a) "best": bullets = Silver Bullet, **Meteor Shot only in the fights after EoW and BoC are beaten (Hive Mind, Perforators, Queen Bee, Skeletron, Slime God)**; arrows = Jester's Arrow for the piercing bosses (EoW, Brain, DS, Hive Mind, Perforators), else Flaming Arrow (Hellfire from the Slime God tier). "plain" = Silver Bullet / Flaming Arrow (Musket Ball before the EoW tier).

**How the real armory picks a levy's ammo (code read, observed):** a levy's `SoldierGear.Ammo` is ONE stack of one ammo type (cap `AmmoPouchMax` 999). `ArmoryTE.TakeAmmo(ammoId = bullet/arrow family)` calls `Drain(IssueAmmo ...)` then `Drain(Storage ...)`; `Drain` takes the **first stack of the right family in slot order** and, once a pouch has a type, only refills that type. There is **no ranking by damage, piercing or boss**: the "best" ammo is whatever the player put in the first Issue Ammo slot (or hand-assigned to the pouch, which is never swapped). `AmmoShare.Limit` only splits scarce stock evenly (floor `AmmoFairShareFloor`). The weapon allocator (`ArmoryTE.Allocation.cs`) never plans a gun the levy has no ammo for, but does not choose between ammo types of that gun. So with a default armory a levy fires whatever stack sits first, often plain ammo; the "best" arm is what a player gets only by arranging the issue slots by hand.

**A/B (enclosed arena `a3e`, 6 seeds, 13 levies, same army per seed):**

| boss, mode | ammo | wins /6 | levy deaths | kill time s | boss HP left % | damage by ranged levies |
|---|---|---|---|---|---|---|
| Brain Expert | plain | 6 | 0.0 | 30 | 0 | 704 |
| Brain Expert | best | 6 | 0.0 | 29 | 0 | 570 |
| Brain Expert | Meteor Shot (not tier-legal) | 6 | 0.0 | 23 | 0 | 848 |
| Brain Rev | plain | 1 | 9.0 | 115 | 71 | 522 |
| Brain Rev | best | 4 | 5.7 | 109 | 18 | 1394 |
| Brain Rev | Meteor Shot (not tier-legal) | 5 | 5.5 | 126 | 17 | 1928 |
| EoW Expert | plain | 6 | 3.2 | 48 | 0 | 4380 |
| EoW Expert | best | 6 | 2.7 | 53 | 0 | 4630 |
| EoW Expert | Meteor Shot (not tier-legal) | 6 | 2.5 | 45 | 0 | 6037 |
| EoW Rev | plain | 0 | 13.0 | - | 42 | 3339 |
| EoW Rev | best | 0 | 13.0 | - | 43 | 3614 |
| EoW Rev | Meteor Shot (not tier-legal) | 0 | 13.0 | - | 38 | 5363 |

Observed: Expert Brain and Expert EoW are won in every arm (no signal besides ttk 29/30/23 s Brain, 53/48/45 s EoW). Brain Revengeance is where ammo shows: plain 1/6 wins, best 4/6, Meteor Shot what-if 5/6 (damage by ranged levies 522 -> 1394 -> 1928). EoW Revengeance is 0/6 in all three (boss left 42/43/38 percent). Caveat: 6 seeds, runs not bit-reproducible; the plain-to-best Brain gap (1 vs 4 wins) is large but a small sample. Meteor Shot is **not tier-legal** at Brain; it is shown as the what-if arm only.

## 5. Main table, arm (a) baseline (Calamity, Expert and Revengeance, 6 seeds each)
| boss | mode | levies (CP) | seeds | kill / despawn / fled / timeout / wipe | levy deaths | kill time s | boss HP left % (non-wins) |
|---|---|---|---|---|---|---|---|
| King Slime | Expert | 10 | 6 | 6 / 0 / 0 / 0 / 0 | 2.3 | 75 | - |
| Desert Scourge | Expert | 11 | 6 | 2 / 0 / 0 / 0 / 4 | 8.7 | 62 | 20 |
| Eye of Cthulhu | Expert | 11 | 6 | 6 / 0 / 0 / 0 / 0 | 0.0 | 39 | - |
| Eye of Cthulhu | Revengeance | 11 | 6 | 6 / 0 / 0 / 0 / 0 | 2.2 | 96 | - |
| Eater of Worlds | Expert | 13 | 6 | 6 / 0 / 0 / 0 / 0 | 2.8 | 48 | - |
| Eater of Worlds | Revengeance | 13 | 6 | 0 / 0 / 0 / 0 / 6 | 13.0 | - | 38 |
| Brain of Cthulhu | Expert | 13 | 6 | 6 / 0 / 0 / 0 / 0 | 0.0 | 28 | - |
| Brain of Cthulhu | Revengeance | 13 | 6 | 3 / 0 / 0 / 0 / 3 | 8.0 | 120 | 68 |
| Hive Mind | Expert | 13 | 6 | 6 / 0 / 0 / 0 / 0 | 0.8 | 48 | - |
| Hive Mind | Revengeance | 13 | 6 | 6 / 0 / 0 / 0 / 0 | 0.8 | 49 | - |
| Perforators | Expert | 13 | 6 | 6 / 0 / 0 / 0 / 0 | 0.0 | 35 | - |
| Perforators | Revengeance | 13 | 6 | 6 / 0 / 0 / 0 / 0 | 0.5 | 41 | - |
| Queen Bee | Expert | 15 | 6 | 6 / 0 / 0 / 0 / 0 | 1.5 | 48 | - |
| Skeletron | Expert | 16 | 6 | 6 / 0 / 0 / 0 / 0 | 0.3 | 45 | - |
| Slime God | Expert | 16 | 6 | 0 / 0 / 0 / 6 / 0 | 0.0 | - | 98 |
| Slime God | Revengeance | 16 | 6 | 0 / 0 / 0 / 6 / 0 | 0.0 | - | 98 |

Boss HP is total across parts. DS "kill time" is the time the last segment died. Observed highlights:
- Expert is won everywhere except **Desert Scourge (2/6)** and **Slime God (0/6, harness anomaly, section 7)**. Revengeance: Eye 6/6, Hive Mind 6/6, Perforators 6/6, Brain 3/6, **EoW 0/6 (all 13 dead, boss at 38 percent)**.
- These numbers are far kinder than lab 2 (Skeletron 6/6 in 45 s vs 2 kills in 12; Queen Bee 6/6 vs 2/12; Brain Rev 3/6 vs 0/6; Eye Rev 6/6 vs 2/12). **I changed several things at once** (arena, stations, Chi, ammo, piercing weapon weights, events fear + formation, supplied HP), so the cause of the difference is not isolated. *Inferred:* the biggest levers are the bigger arena (the Eye and bees cannot sit on the cluster) and event fear + formation; I did not run the one-factor ablation.

## 6. Arm effects (cells with outcome spread: KS Expert, DS Expert, Eye Rev, EoW Expert/Rev, Brain Rev, Queen Bee Expert; 6 seeds per cell per arm)
Arms: a baseline, b Well Fed (minor), c Plenty Satisfied (medium), d hazard pay (`SettlementSystem.HazardPay` on a funded Barracks; fear x0.75 for paid levies), e two rogue levies (Sulphurous set + Wulfrum Knife / tier rogue weapon replacing two summoners).

| cell | a | b | c | d | e |
|---|---|---|---|---|---|
| King Slime Expert | 6/6 kills, 2.3 dead, ttk 75, left -% | 6/6 kills, 3.2 dead, ttk 81, left -% | 6/6 kills, 2.8 dead, ttk 75, left -% | 6/6 kills, 3.0 dead, ttk 71, left -% | 6/6 kills, 2.7 dead, ttk 72, left -% |
| Desert Scourge Expert | 2/6 kills, 8.7 dead, ttk 62, left 20% | 1/6 kills, 10.2 dead, ttk 76, left 34% | 2/6 kills, 9.5 dead, ttk 63, left 41% | 0/6 kills, 11.0 dead, ttk -, left 29% | 1/6 kills, 10.2 dead, ttk 62, left 39% |
| Eye of Cthulhu Expert | 6/6 kills, 0.0 dead, ttk 39, left -% | - | - | - | - |
| Eye of Cthulhu Revengeance | 6/6 kills, 2.2 dead, ttk 96, left -% | 6/6 kills, 2.7 dead, ttk 103, left -% | 6/6 kills, 1.8 dead, ttk 107, left -% | 5/6 kills, 3.0 dead, ttk 94, left 1% | 6/6 kills, 1.2 dead, ttk 64, left -% |
| Eater of Worlds Expert | 6/6 kills, 2.8 dead, ttk 48, left -% | 6/6 kills, 3.0 dead, ttk 48, left -% | 6/6 kills, 2.5 dead, ttk 49, left -% | 5/6 kills, 3.8 dead, ttk 43, left 9% | 6/6 kills, 3.2 dead, ttk 43, left -% |
| Eater of Worlds Revengeance | 0/6 kills, 13.0 dead, ttk -, left 38% | 1/6 kills, 12.8 dead, ttk 96, left 48% | 0/6 kills, 13.0 dead, ttk -, left 35% | 0/6 kills, 13.0 dead, ttk -, left 40% | 0/6 kills, 13.0 dead, ttk -, left 50% |
| Brain of Cthulhu Expert | 6/6 kills, 0.0 dead, ttk 28, left -% | - | - | - | - |
| Brain of Cthulhu Revengeance | 3/6 kills, 8.0 dead, ttk 120, left 68% | 4/6 kills, 5.8 dead, ttk 96, left 100% | 3/6 kills, 8.5 dead, ttk 108, left 39% | 4/6 kills, 5.2 dead, ttk 99, left 99% | 6/6 kills, 3.3 dead, ttk 81, left -% |
| Hive Mind Expert | 6/6 kills, 0.8 dead, ttk 48, left -% | - | - | - | - |
| Hive Mind Revengeance | 6/6 kills, 0.8 dead, ttk 49, left -% | - | - | - | - |
| Perforators Expert | 6/6 kills, 0.0 dead, ttk 35, left -% | - | - | - | - |
| Perforators Revengeance | 6/6 kills, 0.5 dead, ttk 41, left -% | - | - | - | - |
| Queen Bee Expert | 6/6 kills, 1.5 dead, ttk 48, left -% | 6/6 kills, 3.8 dead, ttk 51, left -% | 6/6 kills, 0.3 dead, ttk 46, left -% | 6/6 kills, 1.8 dead, ttk 45, left -% | 6/6 kills, 0.8 dead, ttk 46, left -% |
| Skeletron Expert | 6/6 kills, 0.3 dead, ttk 45, left -% | - | - | - | - |
| Slime God Expert | 0/6 kills, 0.0 dead, ttk -, left 98% | - | - | - | - |
| Slime God Revengeance | 0/6 kills, 0.0 dead, ttk -, left 98% | - | - | - | - |

Totals over those 42 fights per arm (kills, mean levy deaths, mean kill time, boss damage, fear/shaken/routed percent of levy-ticks):

| arm | kills /42 | deaths | ttk s | boss damage | fear % | shaken % |
|---|---|---|---|---|---|---|
| a baseline | 29 | 5.50 | 72 | 8530 | 3.3 | 3.2 |
| b Well Fed | 30 | 5.93 | 75 | 8331 | 3.9 | 3.6 |
| c Plenty Satisfied | 29 | 5.50 | 73 | 8613 | 3.7 | 2.9 |
| d hazard pay | 26 | 5.83 | 68 | 8369 | 2.8 | 2.6 |
| e rogue levies | 31 | 4.90 | 61 | 8277 | 3.6 | 3.2 |

Observed: **food and hazard pay did nothing measurable** (29, 30, 29, 26 kills of 42 are within seed noise; hazard pay did lower the fear share 3.3 -> 2.8 percent, not the outcome). The rogue arm is the only one that moves: 31/42, fewest deaths, fastest kills, and Brain Rev **6/6** (baseline 3/6, food 4/6, 3/6, hazard 4/6). Do not read single-cell 1-2 win differences; the Brain Rev rogue result (6/6) is the one cell that is beyond the other arms' spread.

## 7. New bosses
- **Hive Mind** (corruption zone, 13 levies): Expert 6/6, 0.8 deaths, 48 s; Revengeance 6/6, 0.8 deaths, 49 s. Adds: blobs, Dank Creepers, Dark Hearts, tumors (counted as adds).
- **Perforators** (crimson zone, 13 levies): Expert 6/6, 0 deaths, 35 s; Revengeance 6/6, 0.5 deaths, 41 s. The three worms and the cysts: the worms' segments make it a piercing target.
- Both are very easy for the Boss Checklist slot (3.98 / 3.99) against an army that has the EoW/BoC tier gear plus Meteor Shot.
- **Slime God: 0 kills, harness/targeting anomaly, not a balance result.** In every one of the 13 recorded fights the Ebonian and Crimulan Paladins enter their despawn state (`ai[0]` = 4 / 3) within 6 ticks of spawning, drop through the world (about -1000 tiles) and the Core stays invulnerable (`dontTakeDamage` 100 percent) at 840/840; levies deal only 545-650 damage in 300 s (hits on the Paladins in the first seconds). Traced with `dbg=1`: the despawn is not Calamity's targeting hook (identical with `LEVY_CALCOMPAT=0`), the commander is active and alive (`p0 act True dead False`), `timeLeft` is 99999. Calamity's Paladin AI sets `ai[0]=4` when `player.dead || !player.active` after `TargetClosest`, so the AI is reading an inactive player: *inferred* Levy's slot-255 swap / Spectator hiding does not cover the Paladins. The Slime God rows are kept (`raw3/*`) but excluded from the conclusions and from the arm cells. Needed gameplay change in section 9.

## 8. Vertical space (the "levies clump near the ground" question)
Every living levy sampled every 30 ticks while the fight runs; height = tiles above the floor at the feet; "platform row" = within 1.5 tiles of rows 1-5 (7, 14, ... 35 tiles); "air" = between rows.

| boss | fights | on floor % | on platform rows % | in air % | mean height (tiles) | height sd | rows 0..5 % | boss height (tiles) |
|---|---|---|---|---|---|---|---|---|
| King Slime | 6 | 62 | 3 | 36 | 1.5 | 1.9 | 62,3,0,0,0,0 | 8.4 |
| Desert Scourge | 6 | 79 | 1 | 20 | 0.8 | 1.5 | 79,1,0,0,0,0 | -22.6 |
| Eye of Cthulhu | 12 | 67 | 2 | 31 | 1.3 | 1.8 | 67,2,0,0,0,0 | -0.1 |
| Eater of Worlds | 12 | 72 | 2 | 26 | 1.1 | 1.7 | 72,2,0,0,0,0 | 4.6 |
| Brain of Cthulhu | 12 | 69 | 2 | 29 | 1.2 | 1.7 | 69,2,0,0,0,0 | 9.8 |
| Hive Mind | 12 | 64 | 3 | 34 | 1.4 | 1.9 | 64,3,0,0,0,0 | 4.3 |
| Perforators | 12 | 62 | 3 | 35 | 1.5 | 1.9 | 62,3,0,0,0,0 | 16.4 |
| Queen Bee | 6 | 64 | 2 | 34 | 1.4 | 1.9 | 64,2,0,0,0,0 | 10.7 |
| Skeletron | 6 | 62 | 2 | 36 | 1.5 | 1.9 | 62,2,0,0,0,0 | 10.1 |
| Slime God | 12 | 94 | 0 | 6 | 0.2 | 0.6 | 94,0,0,0,0,0 | 38.0 |
| ALL (mean over bosses) | 96 | 69 | 2 | 29 | 1.2 | 1.7 | | |

Observed: **levies hug the floor.** Across 96 baseline fights they are on the floor 69 percent of the time, in the air (jumping/falling between rows) 29 percent, and **on a platform row only 2 percent**. Mean height 1.2 tiles, standard deviation 1.7 tiles; rows 2-5 are essentially never used (0 percent, the whole spread is below row 1). Vanilla table (36 fights): 63 / 3 / 34 percent, mean 1.4 tiles. The bosses are mostly above them (Brain 10, Queen Bee 11, Perforators 16 tiles up on average; Eye about at floor level; DS under the floor, -23, it tunnels). The Slime God row (94 percent floor) is the stuck-Core anomaly. The arena has five rows over 35 tiles; no levy gets above row 1 in any measurable amount, so the platform rows do not matter to the result and the next movement round is: **vertical use (climb or jump to the platform rows toward airborne bosses)**. The air share (29 percent) is jumping in place, not climbing: height sd 1.7 tiles.

## 9. Rogue levies (Calamity only) and the Irradiated armor
- Eve's set: the **Sulphurous set** (Acid Rain / Sulphurous Sea rogue armor: Acidwood + Sulphuric Scale, Pre-Hardmode; wiki and Calamity source `SulphurousHelmet.cs`). Its set bonus: +65 stealth, **hitting or being hit by an enemy inflicts Poisoned for 1 second**, and its double jump spawns a sulphurous bubble that inflicts **Irradiated** (2.5 s). Calamity's code gives the set bonus Poisoned only; Irradiated comes from the bubble, from the Urchin Stinger, Mythril Knives, Skyfin Bombers, Corrosive Spine.
- Used from the Eye tier on (Wulfrum armor before: KS, DS), weapons: Wulfrum Knife + Urchin Stinger (Eye), Wulfrum Knife + Mycoroot (EoW/Brain/Hive/Perforators), Wulfrum Knife + Rot Ball (QB and later). Two rogue levies replace the last two summoners.
- **Observed on levies:** the dummy player has `sulphurSet = true` for both rogue levies (Eye, EoW, Brain, QB rows); the boss carries **Poisoned for 37 s in the Eye Revengeance fights** (the Eye fights are long), 1.1-1.6 s in EoW/Brain rogue fights; **Irradiated 2.0 s** (Eye, from the Urchin Stinger). Poison on levy hits works; I saw no bubble-caused Irradiated because levies do not use the double jump (*inferred: zero irradiated seconds in fights without the Stinger*).
- Effect on results: section 6 (rogue arm the best arm, Brain Rev 6/6). Mycoroot throws also give the Mushy buff to teammates (`TEAM_ITEMS.md`: commander-to-levy only; the levy-thrown version does not buff the commander).

## 10. Needed gameplay changes (report only, I did not edit them)
1. **Slime God Paladins despawn at spawn when only levies/the Spectator are around** (section 7). The Paladin AI treats an inactive `Main.player[NPC.target]` as despawn; Levy's swap/hide does not cover it. Needs the owner of `Common/Targeting` to check; until then Slime God cannot be measured.
2. **The armory has no best-ammo rule** (section 4): first stack in slot order wins. A "prefer the highest-damage/piercing stack of the family" rule, or per-boss ammo presets, would turn the Brain Revengeance plain-to-best gap (1/6 -> 4/6 wins) into something players get without micro-managing.
3. **Levy vertical movement:** 69 percent floor, 2 percent platform rows (section 8). A climb/jump-to-row behavior toward airborne bosses is the next movement round.
4. **Honey** (and Crystal Ball, Ammo Box for Hardmode) station support: `StationFlags` has Ammo Box and Crystal Ball but no Honey; I placed neither since they are not tier-legal here.
5. Stations need no click in Levy; in the real game a player must activate Sharpening Station, Bewitching Table, War Table, Ammo Box and Crystal Ball. If that is meant to cost, Levy should require the commander to have activated it.
6. Levy-grade levies have one accessory slot: a team accessory (Trinket of Chi) costs the wearer his class accessory. Only one copy is available pre-Hardmode.

## 11. Observed vs inferred, limits
- Observed: all numbers in the tables, the buff/station samples, the vertical samples, the ammo probe, the despawn trace, the code reading of the armory ammo path.
- Inferred: the causes of the difference to lab 2 (several simultaneous changes), why the rogue arm wins Brain Rev, that stations are click-free vs the real game, that levies never double jump.
- 6 seeds per cell; fights are not bit-reproducible; Skeletron and Queen Bee had no despawn (no FLED). The first 66 baseline fights (KS through Hive Mind Expert) were recorded before the adds recorder merged; their outcomes are valid, their recordings lack adds. Slime God rows are anomalies.
- Vanilla table below: vanilla bosses, Expert, arm (a) (Levy + Mechanization only, no Calamity, no rogue, no Trinket of Chi, Meteor Shot after EoW/BoC the same way).

### Vanilla table (Expert, arm a)
| boss | mode | levies (CP) | seeds | kill / despawn / fled / timeout / wipe | levy deaths | kill time s | boss HP left % (non-wins) |
|---|---|---|---|---|---|---|---|
| King Slime | Expert | 10 | 6 | 5 / 0 / 0 / 1 / 0 | 2.5 | 64 | 40 |
| Eye of Cthulhu | Expert | 11 | 6 | 6 / 0 / 0 / 0 / 0 | 0.3 | 51 | - |
| Eater of Worlds | Expert | 12 | 6 | 6 / 0 / 0 / 0 / 0 | 4.5 | 56 | - |
| Brain of Cthulhu | Expert | 12 | 6 | 6 / 0 / 0 / 0 / 0 | 0.0 | 18 | - |
| Queen Bee | Expert | 13 | 6 | 6 / 0 / 0 / 0 / 0 | 1.7 | 40 | - |
| Skeletron | Expert | 14 | 6 | 6 / 0 / 0 / 0 / 0 | 1.8 | 46 | - |
Vanilla KS had one TIMEOUT (7 of 10 dead; replay in the notable set).

## 12. Notable replays
See `replays/lab3/index.txt` (copied next to this file as `notable3_index.txt`).

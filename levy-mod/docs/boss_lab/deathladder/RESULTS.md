# Death ladder: can the levies beat Death-mode Eater of Worlds, and at what gear tier?

Eve's question: Calamity Death EoW (4 worms) vs levies given "everything", tier by tier. Her prediction: ~1 win in 6 at post-Skeletron, wins in early Hardmode; "if they lose at early Hardmode that's a real problem."

## Setup (OBSERVED from the harness, not guesses)
- Harness: calrev + Lab3, new tokens `dl=A|B|C|D` (tier), `dlok=1` (okay-ish arm), `dln=N` (levy-count override, ablation only). Code: `Levy/Common/Testing/CombatHarness.DeathLadder.cs`. Mode `mdeath` = Master+Death (census in log: heads=4 bodies=56 tails=4). `mrev` = Master+Revengeance (1 head, 62 bodies) for the control.
- Arena a3 (big, 5 platform rows with gaps), cmd=near, order=follow, `LEVY_CR_SP=1`, event fear and formation survival defaults, cap 18000 ticks (5 min), worm spawned via NPC.SpawnOnPlayer. Boot logs clean. Raw rows: `raw/`.
- Tiers: **A** pre-evil (everything from King Slime, Eye, Desert Scourge, Crabulon + vanilla pre-evil), **B** post EoW/BoC, **C** post Skeletron (adds Hive Mind, Perforators, Queen Bee, Skeletron, dungeon), **D** early Hardmode just after Wall of Flesh (adds Slime God, Cobalt..Orichalcum, Angel Wings, Cursed Flames, Crystal Storm). The post-Eye tier was dropped per Eve.
- Levy count (Levy's own LevyTierMath): A 13, B 13, C 16, D 20. Every levy 400 max HP in the best arms (15 life crystals through the real FeedCrystal path), 200 mana, 5 accessory slots (config forced), 8 healing potions (Lesser/Healing/Greater by tier).
- **Best arm**: class mix 40% ranged, 40% magic, 10% melee, 10% summoner, weapons weighted x3 when they pierce, explode or burn; best legal ammo (Meteor Shot from B, Jester's Arrow pierce alternating with Hellfire Arrow explosive+DoT, Gel for flamethrower); tier armor sets; mobility first (Cloud in a Bottle, Shield of Cthulhu dash, Hermes Boots, Band of Regeneration, Cobalt Shield from C, Angel Wings at D, Sand Cloak/Fungal Clump/Molten Quiver where legal, Trinket of Chi on one mage). Buffs on every levy (verified at tick 600): Ironskin, Regeneration, Endurance, Swiftness, Wrath, Rage, Magic Power, Archery, Plenty Satisfied, Ammo Reservation, Mana Regeneration. Stations (real tiles beside real armories): campfire, heart lantern, star in a bottle, sunflower, Bast statue, + sharpening (B+), war table and bewitching table (C+), Ammo Box and Crystal Ball (D), honey emulated by the Honey station flag. Measured defense: A 32, B 35, C 37, D 45.
- **Okay-ish arm** (B and C only): vanilla weapons at equal weight, no pierce preference, plain ammo, standard armor sets, 2 accessories (Hermes Boots, Band of Regeneration), Ironskin + Regeneration + Well Fed, tier-curve HP (240 at B, 340 at C), same stations and count.
- Not done: bubbles (harness has no support), a real honey pool (levies have no pool support; flag used). Tier legality of every Calamity weapon checked against its drop source in the decompile; one illegal item (Plague Staff, Plaguebringer drop) leaked into the first C/D/Rev run and those cells were rerun without it (`raw/run1_with_illegal_PlagueStaff_bulk.tsv` kept for the record, not used). Dropped for a missing name: Fiery Greatsword. Accessories Lightning Boots and Magic Quiver were dropped as tier-uncertain and A, B, C, D were rerun.

## Results (Master + Death, 6 seeds per cell; Rev control 4 seeds)
| cell | levies/HP | wins | kill time (wins) | boss HP left (losses, of ~30k) | first levy death | levy deaths per fight |
|---|---|---|---|---|---|---|
| A pre-evil best | 13 / 400 | **2/6** | 53, 64 s | 4.1, 14.4, 11.9, 2.0 k | 9-24 s | 13,13,13,10,13,12 |
| B post-EoW/BoC best | 13 / 400 | **4/6** | 40, 48, 63, 102 s | 5.5, 2.6 k | 9-24 s | 10,13,6,13,4,7 |
| C post-Skeletron best | 16 / 400 | **6/6** | 24-36 s | none | 11-25 s | 2,1,2,1,2,1 |
| D early-Hardmode best | 20 / 400 | **6/6** | 17-22 s | none | none | 0 in all six |
| B okay-ish | 13 / 240 | 0/6 | none | 22.7-27.1 k | 5-12 s | 13 each |
| C okay-ish | 16 / 340 | 0/6 | none | 8.8-25.5 k | 7-18 s | 16 each |
| Control: C best vs Rev 1-worm | 16 / 400 | 4/4 | 22-37 s | none | 15-18 s (one fight none) | 1,0,2,1 |
Ablation (C-tier gear with the B count of 13: **6/6**, 23-40 s, 1-4 deaths. B-tier gear with 16 levies: **6/6**, 30-63 s, 2-13 deaths.) So both the levy count and the tier gear move the result; count alone lifts B from 4/6 to 6/6.

Noise warning: the sim is chaotic. A rerun of cell A with one accessory changed on one levy type went from 4/6 to 2/6 on identical seeds. Read A and B as "roughly a coin flip, 2-4 of 6", not as a precise order between them.

## Where the damage comes from
- OBSERVED: damage taken is contact damage from the worm. Head 72-88% of it, body 11-26%, tail 0-2%. Projectile/other damage (Vile Spit, shadowflame) is **under 1%** of all damage taken (takenOther 1.3k of 202k in the first full run). The recorder labels worm parts only by NPC id, so heads vs bodies come from NPC ids 13/14/15 in `raw/bulk.tsv.hit.tsv`.
- OBSERVED: every levy death, in every cell, was credited to the Eater of Worlds (head or body contact). Nothing else killed a levy.
- Damage dealt, by weapon behaviour (INFERRED class labels: P = projectile can pierce per the projectile sample, X = explosive and D = burn/poison from hand lists plus Hellfire ammo): weapons that can pierce account for 60% (A), 86% (B), 77% (C) and 88% (D) of credited boss damage (P alone 35-54%). Pure explosive-only is 6-25%. Damage-over-time that is not credited to a levy (poison, burning, Cursed Flames ticks) is 19-21% of boss damage at A and B, 29-32% at C and D, 7-8% in the okay-ish arms. Top damage dealers: Magic Missile, Aqua Scepter, Flamelash, Sandstream Scepter and Space Gun at A/B; Magic Missile, Blood Bath, Space Gun, Flesh of Infidelity and Vilethorn at C; Cursed Flames, Magic Missile, Cobalt Repeater, Aqua Scepter and Orichalcum Repeater at D. Magic dominates because 40% of the best army is mages.

## What it means
1. **They can win.** Yes, with real gear: 4/6 at post-EoW/BoC, 6/6 at post-Skeletron and early Hardmode, against a boss that took 5+ minutes by hand. That beats Eve's prediction (1/6 at Skeletron). It is not a "real problem" at early Hardmode: D is a clean 6/6 with zero levy deaths.
2. **The win needs everything.** The okay-ish arms (normal vanilla kit, 240-340 HP, three potions) lose 12 of 12, with every levy dead 5-18 s after the worms arrive, even at the post-Skeletron count of 16. The gap between "everything" and "what a normal player has" is the whole result.
3. **First deaths come at 6-25 s in the losses** and the army collapses within another 20-40 s (head contact does ~70-80% of the damage). INFERRED: survival, not firepower, decides the close cells; the losing best-arm fights get the boss to 2-14k of 30k before the last levy falls.
4. Caveats: best-arm numbers combine 400 HP, 5 slots, class mix 40/40/10/10, 11 potions and honey; no single factor was isolated except levy count. N=6 per cell, and the sim varies a lot between runs. The Rev control (1 worm) at C also wins 4/4, so at C the 4-worm split is not what decides it.

## Recordings
Scratchpad `replays/deathladder/` (index.txt): A loss, B loss, C win, D best win (0 deaths), B okay-ish loss, C okay-ish loss.

## Reproduce
`scripts/mk.py <name> 18000 1,2,3,4,5,6 A,B,C,D,Bok,Cok,RC` writes the spec (RC = Rev control); `scripts/an.py` summarises. Env as in `tools/run_harness.sh -N -m CalamityMod`, `LEVY_CR_SP=1`, `LEVY_CR_EXCLUDE=CalamityMod/UrchinMace,CalamityMod/SaharaSlicers`.

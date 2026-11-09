# combatfix lab results (Levy 2.0.38 + combatfix)

Lab: `levy-mod/lab/levylab run specs/cf_*.yaml` (Calamity world, `LEVY_CR_SP=1` single-player emulation, `LEVY_FIXPROBE=1` for the FIXPROBE-* log lines, `LEVY_CF_OFF=<mask>` for the old behaviour in the same build;
masks in `Common/Weapons/CombatFix.cs`). Wall test: weapon-trial harness with `LEVY_HARNESS_WALL=1`. Numbers are per fight (3 seeds unless noted); the sim is chaotic, so treat small differences as noise.

1. Cursor. 292 Calamity projectile types read the owner's cursor; two are flare-named (Flare Bolt, Sanguine Flare). Cursor-reading shot ticks with no target (the shot read the commander's cursor): Frigidflash Bolt 122 / 738 / 2238, Flare Bolt 29 / 0 / 1, Sanguine Flare 0 / 41 / 0 per fight before; 0 after (held point instead). Damage unchanged.
2. Ark draw. During a chain projectile's draw the owner slot held the null player 35,323 px (mean, 146 probes) from the soldier; with the draw swap it is 0 px on all 170 probes and restored each time. Every Ark projectile already starts within 30 px of the levy.
3. Through walls. 7 tile-free weapons behind a two-tile wall fired 0 / 7 before and 7 / 7 after (Teslastaff 33,767 damage, Ghastly Visage 11,839, Burning Sea 1,294); the three weapons whose bolts collide (Flare Bolt, Frost Bolt, Frigidflash Bolt) still hold fire. Classification of 518 no-ammo ranged weapons: 30 gained, 37 lost.
4. Melee. Ten Excalibur levies: median kill time Skeletron 79 s -> 25 s, Eye 38 s -> 9 s, Queen Bee 9 s -> 10 s; melee levies were 22-23 tiles from the boss and held off it 29-43% of samples on Skeletron and never engaged it; after, 24-36% of samples engaged. Levy deaths on Skeletron 0.3 -> 2.0 per 10.
5. Flares. 20 levies on the Firestorm Cannon: most soldier shots alive at once 1000 / 840 / 1000 before, 619 / 620 / 619 after; damage 9,680 vs 10,088 mean over the fight (no difference).
6. Stealth. Eight rogue levies: stealth-flagged projectiles per rogue levy-minute 36.6 / 1.5 / 0.35 before, 50 / 21 / 22 with patience; median boss kill 81 s -> 66 s.

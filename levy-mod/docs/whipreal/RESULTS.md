# Levy whips through the item's real whip projectile (sweep results, 2026-11-14)

What changed: a levy that holds a whip now throws the item's own whip projectile through the item's own Shoot hook (the same path drones and held weapons use), anchored to the soldier's dummy, instead of the hand-made `SoldierWhip` lash. The tag debuff, tip, sounds, dust and on-hit effects are the whip's own. The whip tag's damage bonus is the game's: a commander's minion gets it from the game, a levy's minion gets the game's own numbers (the game skips NPC-style shots). Old lash stays behind `LEVY_WHIPREAL=0` (and when `RealWeaponShoot` is off).

Run the sweep: `LEVY_SELFCHECK_WORLD=1 LEVY_WHIPONLY=1 LEVY_WHIPLAB=1 LEVY_WHIPREPS=4` on a world server (alone), or the quick form inside `tools/ship/shipworld.sh` (prints `[WhipReal] self-check pass=N fail=M`). `LEVY_WHIPFILTER=name` narrows the whips, `LEVY_WHIPDEBUG=1` traces lash deaths, `LEVY_WHIPRESTORE=0` shows what a hit writes on the commander.

Setup of every row: flat arena, one pinned enemy 100 px from the actor (defense 0), 600 ticks per levy run, 4 runs summed (360 ticks for the player). PLAYER = a scratch player in slot 255 using the whip through vanilla `Player.ItemCheck`. LEVY-OLD = the hand-made lash. LEVY = the real lash. Minion probe = a Hornet stinger (a vanilla minion shot, 40 damage, no damage variation) fired at the lashed enemy, from the commander and from a levy.

### Vanilla

| whip | proj | segments (levy/player; old lash) | reach px (levy/player; old) | dmg per hit (levy/player; old) | dmg/s (levy / old = x) | tag debuff on enemy | commander minion hit, untagged 40 (player's lash / levy's lash) | levy minion hit | cost us per swing (new / old) |
|---|---|---|---|---|---|---|---|---|---|
| Leather Whip | BlandWhip#841 | 21/21; 14 | 180/180; 168 | 45.7/13.7; 29.7 | 86 / 67 = x1.28 | BlandWhipEnemyDebuff | 44.0 / 44.0 | 44.0 (old 60.0) | 2270 / 1030 |
| Durendal | SwordWhip#847 | 21/21; 14 | 381/381; 168 | 173.7/55.2; 117.4 | 343 / 244 = x1.41 | SwordWhipNPCDebuff | 49.0 / 49.0 | 49.0 (old 60.0) | 1131 / 81 |
| Morning Star | MaceWhip#848 | 41/41; 14 | 434/434; 168 | 534.6/164.4; 355.0 | 842 / 710 = x1.19 | MaceWhipNPCDebuff | 48.0 / 48.0 | 48.0 (old 60.0) | 837 / 92 |
| Dark Harvest | ScytheWhip#849 | 31/31; 14 | 464/464; 168 | 320.1/104.2; 212.6 | 664 / 399 = x1.67 | ScytheWhipEnemyDebuff | 50.0 / 50.0 | 50.0 (old 60.0) | 2492 / 98 |
| Cool Whip | CoolWhip#912 | 21/21; 14 | 360/360; 168 | 142.4/44.1; 97.6 | 264 / 202 = x1.30 | CoolWhipNPCDebuff,Frostburn2 | 46.0 / 46.0 | 46.0 (old 60.0) | 1210 / 422 |
| Firecracker | FireWhip#913 | 21/21; 14 | 336/336; 168 | 115.8/37.4; 81.1 | 217 / 191 = x1.14 | FlameWhipEnemyDebuff,OnFire3 | 47.0 / 54.0 | 47.0 (old 60.0) | 2106 / 285 |
| Snapthorn | ThornWhip#914 | 21/21; 14 | 240/240; 168 | 58.4/17.5; 38.0 | 110 / 91 = x1.20 | ThornWhipNPCDebuff | 46.0 / 46.0 | 46.0 (old 60.0) | 3594 / 166 |
| Kaleidoscope | RainbowWhip#915 | 41/41; 14 | 528/528; 168 | 575.4/183.3; 387.9 | 1079 / 950 = x1.14 | RainbowWhipNPCDebuff | 60.0 / 60.0 | 60.0 (old 60.0) | 1981 / 105 |
| Spinal Tap | BoneWhip#952 | 21/21; 14 | 285/285; 208 | 86.0/26.4; 57.0 | 161 / 146 = x1.11 | BoneWhipNPCDebuff | 47.0 / 47.0 | 47.0 (old 60.0) | 1694 / 86 |


### Calamity

| whip | proj | segments (levy/player; old lash) | reach px (levy/player; old) | dmg per hit (levy/player; old) | dmg/s (levy / old = x) | tag debuff on enemy | commander minion hit, untagged 40 (player's lash / levy's lash) | levy minion hit | cost us per swing (new / old) |
|---|---|---|---|---|---|---|---|---|---|
| Leather Whip | BlandWhip#841 | 21/21; 14 | 180/180; 168 | 44.9/13.8; 30.3 | 84 / 70 = x1.20 | BlandWhipEnemyDebuff | 42.0 / 42.0 | 44.0 (old 60.0) | 3329 / 462 |
| Durendal | SwordWhip#847 | 21/21; 14 | 381/381; 168 | 175.5/54.3; 118.2 | 346 / 225 = x1.54 | SwordWhipNPCDebuff | 44.0 / 44.0 | 49.0 (old 60.0) | 3219 / 87 |
| Morning Star | MaceWhip#848 | 41/41; 14 | 434/434; 168 | 528.5/163.3; 354.8 | 832 / 568 = x1.47 | MaceWhipNPCDebuff | 40.0 / 40.0 | 48.0 (old 60.0) | 1755 / 239 |
| Dark Harvest | ScytheWhip#849 | 31/31; 14 | 464/464; 168 | 321.2/99.6; 216.0 | 667 / 438 = x1.52 | ScytheWhipEnemyDebuff | 50.0 / 50.0 | 50.0 (old 60.0) | 1570 / 63 |
| Cool Whip | CoolWhip#912 | 21/21; 14 | 360/360; 168 | 143.8/44.9; 98.1 | 266 / 154 = x1.72 | CoolWhipNPCDebuff,Frostburn2 | 46.0 / 46.0 | 46.0 (old 60.0) | 1119 / 86 |
| Firecracker | FireWhip#913 | 21/21; 14 | 336/336; 168 | 118.4/37.0; 79.8 | 222 / 152 = x1.46 | FlameWhipEnemyDebuff,OnFire3 | 40.0 / 40.0 | 48.8 (old 60.0) | 1385 / 63 |
| Snapthorn | ThornWhip#914 | 21/21; 14 | 240/240; 168 | 58.1/18.4; 38.0 | 109 / 92 = x1.18 | ThornWhipNPCDebuff | 41.0 / 41.0 | 46.0 (old 60.0) | 1015 / 106 |
| Kaleidoscope | RainbowWhip#915 | 41/41; 14 | 528/528; 168 | 572.3/182.5; 385.5 | 1073 / 713 = x1.50 | RainbowWhipNPCDebuff | 48.0 / 48.0 | 60.0 (old 60.0) | 1271 / 59 |
| Spinal Tap | BoneWhip#952 | 21/21; 14 | 285/285; 208 | 86.1/27.4; 58.1 | 162 / 116 = x1.39 | BoneWhipNPCDebuff | 44.0 / 44.0 | 47.0 (old 60.0) | 1429 / 78 |


### Thorium

| whip | proj | segments (levy/player; old lash) | reach px (levy/player; old) | dmg per hit (levy/player; old) | dmg/s (levy / old = x) | tag debuff on enemy | commander minion hit, untagged 40 (player's lash / levy's lash) | levy minion hit | cost us per swing (new / old) |
|---|---|---|---|---|---|---|---|---|---|
| Leather Whip | BlandWhip#841 | 21/21; 14 | 180/180; 168 | 44.9/14.3; 29.9 | 84 / 51 = x1.66 | BlandWhipEnemyDebuff | 44.0 / 44.0 | 44.0 (old 60.0) | 2053 / 763 |
| Durendal | SwordWhip#847 | 21/21; 14 | 381/381; 168 | 177.1/56.1; 118.3 | 350 / 299 = x1.17 | SwordWhipNPCDebuff | 49.0 / 49.0 | 49.0 (old 60.0) | 4095 / 104 |
| Morning Star | MaceWhip#848 | 41/41; 14 | 434/434; 168 | 542.5/159.4; 355.4 | 854 / 738 = x1.16 | MaceWhipNPCDebuff | 48.0 / 48.0 | 48.0 (old 60.0) | 3094 / 342 |
| Dark Harvest | ScytheWhip#849 | 31/31; 14 | 464/464; 168 | 314.9/101.0; 215.3 | 654 / 560 = x1.17 | ScytheWhipEnemyDebuff | 50.0 / 50.0 | 50.0 (old 60.0) | 2315 / 96 |
| Cool Whip | CoolWhip#912 | 21/21; 14 | 360/360; 168 | 144.0/43.4; 96.7 | 266 / 242 = x1.10 | CoolWhipNPCDebuff,Frostburn2 | 46.0 / 46.0 | 46.0 (old 60.0) | 1598 / 92 |
| Firecracker | FireWhip#913 | 21/21; 14 | 336/336; 168 | 116.8/38.3; 79.4 | 219 / 153 = x1.43 | FlameWhipEnemyDebuff,OnFire3 | 47.0 / 54.0 | 47.8 (old 60.0) | 1031 / 67 |
| Snapthorn | ThornWhip#914 | 21/21; 14 | 240/240; 168 | 57.0/17.5; 37.9 | 107 / 89 = x1.20 | ThornWhipNPCDebuff | 46.0 / 46.0 | 46.0 (old 60.0) | 3472 / 151 |
| Kaleidoscope | RainbowWhip#915 | 41/41; 14 | 528/528; 168 | 573.5/179.6; 389.1 | 1075 / 710 = x1.51 | RainbowWhipNPCDebuff | 60.0 / 60.0 | 60.0 (old 60.0) | 1999 / 104 |
| Spinal Tap | BoneWhip#952 | 21/21; 14 | 285/285; 208 | 85.6/27.5; 57.7 | 160 / 131 = x1.22 | BoneWhipNPCDebuff | 47.0 / 47.0 | 47.0 (old 60.0) | 814 / 74 |
| Thrombosis (ThoriumMod) | ThoriumMod/ThrombosisPro#2168 | 51/51; 14 | 360/360; 168 | 146.0/46.1; 100.3 | 270 / 206 = x1.31 | ThoriumMod/Gouge,ThoriumMod/ThrombosisDebuff | 46.0 / 46.0 | 60.0 (old 60.0) | 1918 / 52 |


Reading the tables. Projectile, segment count and reach of the levy's lash equal the player's in every row (the old lash was one polyline of 14 segments, 168 px for every whip, 208 for Spinal Tap). The enemy carries the same tag debuff, and Cool Whip's Frostburn, Firecracker's OnFire3 and Thorium's Gouge ride along as for the player. The commander's minion hits a levy-tagged enemy for exactly what it does off the player's own lash (the Firecracker row's second number is the average after its one-shot tag is spent; its first hit is 110 in both). A levy minion's hit equals the commander's in every vanilla row. Calamity re-works the vanilla tags into its own system (CalamityBuffSets.SummonTagDebuff), so there the commander's numbers are Calamity's and the levy's are vanilla's table; a modded whip's own tag (Thrombosis) is not readable, so a levy minion keeps the old +50% against it.

Damage. At 1.0 the real lash did 31 to 41 percent of the old lash's damage per second (the old one hit 1.35x, crit on every hit and lashed every 0.75 use times). `WhipRealDamageMult` is 4.0 in `LevyConstants.Summon.cs`: levy whip damage per second is x1.10 to x1.72 of the old lash on every whip of the three sweeps (smallest: Cool Whip on Thorium x1.10, Spinal Tap on vanilla x1.11, Leather Whip and Snapthorn on Calamity x1.20 and x1.18). The old lash's own rate varies by about a third between runs, so single runs of ratios near 1 are noise; the table above is four runs of each.

Commander safety. Every whip scene snapshots the real commander (use state, minion focus, buffs) around each tick: 0 ticks with a write, equal to the control run with a plain sword, on vanilla, Calamity and Thorium. Without the damage scope (`LEVY_WHIPRESTORE=0`) every whip writes the commander's `MinionAttackTargetNPC` on its first hit, and Thorium's Thrombosis also hands the commander a buff.

Cost per swing (quiet machine, one lashing levy, microseconds): real lash start 28 to 62, AI 270 to 640 (two AI calls a tick for about 30 ticks), damage pass 280 to 640 of which the damage scope is 27 to 58 and the rest is vanilla's own Projectile.Damage; the old lash measured start 25 to 74 plus AI 20 to 71, with its damage pass unwrapped and not counted. Roughly 20 to 40 us a tick per lashing levy, about 8 times the old lash's counted cost.

Not covered: Calamity has no whip item (the sweep finds only the nine vanilla whips there), so there is no Calamity whip to measure; Thorium's Thrombosis drops more side projectiles for a levy than for the player; nothing here was looked at on screen (the draw state is checked as geometry: the control points the draw sees match the AI's to 0.0 px); multiplayer clients run the same lash code with the animation held at 1 and were not tested.

# Warbands: time-to-kill lab (phase 1A)

Source: `Warbands/Common/TtkLab.cs`, run headless with `WARBANDS_TTK=1 WARBANDS_OUT=<file>`; raw rows in `ttk_lab.tsv` (Master first, then Classic). Pure arithmetic over the real game tables: `Item.SetDefaults` damage and use time, `NPC.SetDefaults` life, defense and contact damage with the world mode forced (Master: enemy life x3, enemy damage x3), armor defense summed from the real armor items, Levy's real constants (`GradeDamageMult[0]` 0.8, `HostileProjectileDamageMult` 2, defense effectiveness Classic 0.5 / Expert 0.75 / Master 1.0 as `LevyUtil.ModeDefenseEffectiveness`, NPC rule `damage - defense x 0.5`). No fight is simulated; there is no hostile band yet, so a "levy" is the stage's typical Levy kit as a target and as an attacker.

**What is assumed.** Weapon hit = item damage + ammo damage, no armor, accessory or buff multipliers (they scale both sides the same). Accuracy on a moving target: ranged 0.70, magic 0.75, melee 0.80 for the player. A levy fires 55% / 60% / 35% of the time (ranged / magic / melee; the biome-blade lab measured 20-300 damage per levy-second against raw figures several times higher). TTK is the average over the stage's ranged, magic and melee weapon. Enemy DPS is the sustained maximum (the player's own 40-tick i-frames cap contact at 1.5 hits per second; ranged enemies use a fixed 0.4-0.75 shots per second), so it overstates what a real enemy does by roughly 2x; use it as a ceiling and for comparisons. Defense of the player is armor + a stage accessory allowance (0 / 2 / 5 / 10).

Kits. Pre-boss: gold armor (13 def), 200 HP; Demon Bow + Flaming Arrow, Vilethorn, Blade of Grass. Post-EoW: shadow armor + 2 (21), 300 HP; Phoenix Blaster + Meteor Shot, Flamelash, Light's Bane. Early Hardmode: mythril + 5 (42), 400 HP; Mythril Repeater + Hellfire Arrow, Crystal Storm, Excalibur. Post-Plantera: turtle + 10 (75), 500 HP; Megashark + Chlorophyte Bullet, Nettle Burst, Terra Blade. Levy HP 200 / 260 / 360 / 480 (Levy's curve, never above 500), same armor.

## Master (Eve's mode), player TTK is the average over the three weapons

| Stage | Target | Life | Def | Player TTK | Its DPS on the player | Player dead in |
|---|---|---|---|---|---|---|
| pre-boss | Zombie / Demon Eye / Skeleton | 135 / 180 / 180 | 6 / 2 / 8 | 5.2 / 6.1 / 7.5 s | 44 / 62 / 70 | 4.6 / 3.3 / 2.8 s |
| | typical levy as Levy has it | 200 | 13 | 10.4 s | 3 | 77 s |
| | levy with Master x3 life and damage | 600 | 13 | 31 s | 32 | 6.3 s |
| post-EoW | Demon / Fire Imp / Hornet | 360 / 210 / 144 | 8 / 16 / 12 | 6.2 / 4.3 / 2.7 s | 56 / 52 / 57 | 5.3 / 5.8 / 5.3 s |
| | typical levy | 260 | 21 | 6.1 s | 10 | 31 s |
| | levy x3 | 780 | 21 | 18 s | 84 | 3.6 s |
| early HM | Pixie / Chaos Elemental / Corruptor | 450 / 1110 / 690 | 20 / 30 / 32 | 3.6 / 10.3 / 6.6 s | 184 / 117 / 110 | 2.2 / 3.4 / 3.6 s |
| | typical levy | 360 | 42 | 4.2 s | 8 | 52 s |
| | levy x3 | 1080 | 42 | 12.5 s | 151 | 2.6 s |
| post-Plantera | Blue Armored Bones / Necromancer / Skeleton Sniper | 1500 / 900 / 1200 | 50 / 18 / 28 | 11.5 / 4.5 / 6.7 s | 90 / 38 / 42 | 5.6 / 13 / 12 s |
| | typical levy | 480 | 75 | 5.5 s (Megashark 80 s, Nettle Burst 267 s) | 3 | 177 s |
| | levy x3 | 1440 | 75 | 16.5 s | 115 | 4.3 s |

Classic, for reference: Zombie 45 HP dies in 1.7 s; a typical pre-boss levy (200 HP) in 10.4 s; levy damage on the player 8 per second. Classic and Master TTK on a levy are identical (levy life does not scale), only the enemies change.

## What it says

1. **A levy as Levy has it is a punching bag in Master.** Master defense counts at full value (1.0) and the levy's weapon was balanced for classic fighting: 3 to 10 damage per second against a typical player, against 40 to 180 for ordinary enemies. A hostile levy needs the vanilla mode multipliers (life and damage x1 / x2 / x3) or it does nothing.
2. **Taking the multipliers verbatim overshoots.** Levy life 200 to 480 times 3 is 600 to 1,440, which is 2 to 5 times the life of the toughest ordinary enemy of the stage, and its damage lands 1.5 to 2.5 times a regular enemy's DPS at stages 1, 3 and 5. Fighting one is 3 to 4 times as long as fighting a regular enemy, and it kills a player in under 4 seconds: the scary Eve expected.
3. **Gear defense is a wall.** Post-Plantera turtle armor and accessories are 75 defense; the Megashark does 1 damage per hit (80 s to kill) and the Nettle Burst 267 s. A hostile levy must not carry its gear's real defense.
4. **Player weapons cope.** A player's TTK on a regular enemy is 1 to 12 seconds across the board, so the shape of the fight is decided by the enemy's life and defense numbers, which is what the proposal fixes.

## Proposed hostile-levy rules (implemented in `BandStats.cs`)

| Rule | Value |
|---|---|
| **Life** | Stage reference x role share x mode life multiplier (Classic 1, Expert 2, Master 3). Reference per stage (pre-boss, post-EoW, post-Skeletron, early HM, post-Mechs, post-Plantera, post-Golem): 90, 180, 320, 555, 640, 750, 940. Shooters and mages 0.8x, melee 1.2x. About 1.5x the toughest ordinary enemy of the stage, so one member takes 1.5 to 2 times as long to kill as that enemy (Master, early HM: shooter 1,332 HP, about 12 s; melee 1,998 HP, about 19 s; Chaos Elemental 10 s). Not tied to the gear's own HP curve. |
| **Defense** | The lower of the gear's real defense and Levy's own stage line `WeaponRank.StageDefense` (8, 14, 24, 34, 40, 46, 55). Armor is for looks, never a wall. |
| **Damage** | The weapon's real hit x the mode damage multiplier, then pulled down until its sustained damage on the stage's typical player stays under the DPS cap. A hostile projectile is spawned at half that damage because the player takes twice a hostile projectile's damage (Levy's `HostileProjectileDamageMult`). |
| **DPS cap per member** (Master, on the typical player) | Ranged and magic 37 / 33 / 55 / 82 / 70 / 34 / 40; melee x1.35. About 0.6x the median ordinary enemy at stages 0, 1, 3 and 5; other modes scale by their damage multiplier / 3. |
| **Band DPS cap** | At most 2 members attack in one 2-second window for bands up to 4, 3 for larger; the rest close in, flank and juke. A full band is therefore about 2 to 3 ordinary enemies' DPS. |
| **I-frames** | None beyond vanilla NPC rules: the band takes the player's weapons the way any enemy does (no 40-tick player-parity frames on the hostile side). The levy side keeps Levy's i-frames. Knockback resist 0.35. |
| **Band size** | 3 to 5 by default (config), one band at a time, 4% chance per minute near a player in the open, off until the first stage (default post-EoW). Phase 2: 3 + 1 per 10 active levies, capped at 8, so the army cannot trivialise it. |
| **Loot share** | 35% per weapon, 35% per ammo roll (stack of 15-40), 25% per armor piece. A band of 4 gives about 1.4 weapons and 3 armor pieces. |

Phase 2 verification: run the band against a real Levy army in the lab kit and read the table's damage-by-class columns before changing any constant; these numbers are from arithmetic, not fights.

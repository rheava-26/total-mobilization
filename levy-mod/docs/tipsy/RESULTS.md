# Tipsy lab: how much does "worse aim" cost, and what should the drinks do?

Eve's note (Tavern Keep): she likes the drinks, wants more distinct effects, and said "60% worse aim doesn't really seem noticeable", and that aim doesn't matter to a summoner or a melee troop unless it is a beam sword. This lab measures it. Lab only: the only game-code change on this branch is a lab hook in the test harness (`CombatHarness.TipsyLab.cs`, plus a `Path` field on `HarnessTarget` so a dummy can strafe). Nothing in gameplay changed.

## What was measured

One levy, one weapon, standing on the a3 arena floor, firing at an indestructible dummy a fixed distance away. 10 s settle, then 20 s measured. The levy is Levy grade (6 degrees of aim jitter and 25% lead error before the multiplier), no armor and no accessories, 15 life crystals, 9 mana crystals. Vanilla world, no Calamity. Three seeds per cell (the seed varies the dice and the strafing phase). Damage is the dummy's life lost, so it includes crits and everything else; hit % is the share of fired projectiles that landed.

Cells: 16 weapons, 3 distances (8, 25 and 45 tiles; magic uses 8, 25 and 38 because its range is 40), a still target and a strafing one (a Lissajous path, about 8 tiles wide and 5 tall, peak speed about 3 px per tick), a zombie-sized hitbox ("small") and an Eye-of-Cthulhu-sized hitbox ("boss"), sober against the real Barracks Ale buff ("tipsy": the whole Tipsy path runs, aim error x1.6). 1,152 fights in 14 minutes. A second run sweeps the aim multiplier (x1 to x10) at 25 tiles on the aim-sensitive weapons (792 fights), and a third compares the three real drinks (192 fights).

Weapons: guns Minishark with Musket Ball, Minishark with High Velocity Bullet, Sniper Rifle with High Velocity Bullet, Boomstick; bows Demon Bow and Molten Fury; magic bolts Ruby Staff, Space Gun, Water Bolt; beam swords Enchanted Sword and Terra Blade; true melee Muramasa and Night's Edge; summoners Slime Staff, Hornet Staff, Imp Staff.

Reproduce: `levy-mod/lab/levylab run levy-mod/lab/specs/tipsy_main.yaml` (and `tipsy_sweep.yaml`, `tipsy_drinks.yaml`); `lab/tipsy_summary.py main|sweep|drinks RUN_DIR` makes the pooled tables below. Full per-weapon tables: `table_main.md`, `table_sweep.md`, `table_drinks.md`. Raw rows: `data/*.csv`. Pooled = for each weapon the ratio of tipsy damage to sober damage, then the mean of those ratios over the weapons in the group, so one lumpy weapon (the Sniper Rifle lands about 20 shots a window) cannot decide a row.

## Result 1: Tipsy x1.6, damage kept (% of sober)

| group | target | 8 tiles | 25 tiles | 38 tiles | 45 tiles |
|---|---|---|---|---|---|
| guns, bows, magic bolts (9 weapons) | small, still | 100% | 71% | 71% | 67% |
| | small, strafing | 91% | 74% | 97% | 77% |
| | boss-size, still | 101% | 96% | 73% | 76% |
| | boss-size, strafing | 99% | 92% | 86% | 72% |
| beam swords (Enchanted, Terra) | any | 96-101% | 96-99% | | 89-95% |
| true melee (Muramasa, Night's Edge) | any | 100% | 98-102% | | 98-111% |
| summoners (Slime, Hornet, Imp) | any | 97-101% | 98-103% | | 98-101% |

(The 38-tile column is magic only. Single cells swing 10 to 20 points on three seeds; the pooled rows are the reliable ones.)

Reading it. Eve is right about the feel, and the numbers say why. The error is an angle, so what it costs depends on how wide the target looks. Against a boss-sized body inside 25 tiles it costs 4 to 8 percent, which nobody sees. It costs nothing inside about 10 tiles on anything. It bites against small things at range (29 to 34 percent lost at 25 tiles, and about the same beyond) and against everything at 38 to 45 tiles (23 to 33 percent). Melee and summoners are flat at 100% within noise, as she said, and that is by construction: nothing in the melee or minion code reads the aim error. Beam swords are mixed. The Terra Blade does respond (93 to 98% at x1.6, 49 to 60% at x4 on a small target) but its beams are fat and pierce, so x1.6 barely shows. The Enchanted Sword does not respond at all: hit rate stays 96 to 100% and damage 91 to 105% all the way to x10. That is observed; the cause is not confirmed (its aim path may not be taking the error, or its beam is simply that forgiving). It is worth a look, because Eve expected beam swords to be the melee exception.

One more thing that makes x1.6 weaker than it looks: it multiplies the grade's jitter (Levy 6 degrees, Man-at-Arms 3, Champion 1). A drunk Man-at-Arms is 4.8 degrees, which is better than a sober Levy, so for upper grades Tipsy is invisible. This follows from the numbers rather than a separate run; the lab only measured Levy grade.

## Result 2: what multiplier costs how much (25 tiles, damage kept vs x1)

| group | x1.6 | x2.5 | x4 | x6 | x10 |
|---|---|---|---|---|---|
| guns, bows, magic: small target (still and strafing pooled) | 68% | 54% | 36% | 24% | 16% |
| guns, bows, magic: boss-size target | 93% | 69% | 47% | 32% | 20% |
| guns, bows, magic: both sizes | 81% | 62% | 41% | 28% | 18% |
| Terra Blade (small / boss) | 95% / 100% | 81% / 90% | 55% / 76% | 41% / 55% | 22% / 30% |
| Enchanted Sword | ~97% | ~98% | ~98% | ~100% | ~100% |

Interpolating between those: a 15 to 25 percent loss at medium range is x1.9 to x2.3 against a boss-sized target (this is what a player watches), x1.3 to x1.5 against a small enemy, and x1.5 to x1.9 averaged over both. So the shipped x1.6 already costs about 19 percent averaged over target sizes, but only about 7 percent on the boss-sized body Eve actually looks at. For a visible 15 to 25 percent on bosses the multiplier is about x2.2 (small targets then lose about 40 percent at 25 tiles; close range stays free). Beam swords would need about x3 to land in the same band.

## Result 3: the three real drinks (25 tiles, still small target, % of sober)

| group | Ale (+8 defense) | Stout (+15% damage) | Rum (+10 crit) |
|---|---|---|---|
| guns, bows, magic | 74% | 85% | 76% |
| beam swords | 96% | 117% | 98% |
| true melee | 100% | 117% | 107% |
| summoners | 100% | 105% | 104% |

Ale does nothing to damage output by design (a defense drink), so its row is the pure Tipsy cost. Stout's +15% shows in full on melee and beam swords (117%), is more than eaten by aim on shooters at range, and does not show up on summoner minions of the Hornet and Imp staffs (98%, 99%); only the Slime Staff gained (118%). Observed, not explained: the Stout bonus may not reach minions that are already summoned. Rum's crit shows as +6 to 7% on melee. Against a boss-sized target the shooter rows are far kinder (Tipsy costs 4 to 8%), so Stout is a straight upgrade there.

## What is observed and what is inferred

Observed: every table above. Inferred: why upper grades would not feel x1.6 (arithmetic on the jitter table, not run); that the Enchanted Sword's aim path ignores the error; that Stout does not reach already-summoned minions. Caveats: one levy at a time (no crowd, no enemy fire, no cover); a flat arena with no terrain between levy and target; vanilla weapons only; Levy grade only; three seeds, 20 s windows, so a lumpy weapon varies about plus or minus 25 percent on its own. One fight (Ruby Staff, boss-size, x10, seed 1) returned a negative damage reading from a dummy-life glitch and was dropped from the pooled tables (the per-weapon table_sweep.md still averages it in and shows -33,324 for that one cell).

## Proposal: drinks with a distinct, noticeable trade-off for every class

Principle (the tenet and Eve's note together): no outright nerf to the army's power. A drink is an opt-in consumable, so its price should be a choice and a bit of fun (where to stand, who gets the keg), and the price should land on every class, not only on shooters. The data says today's price lands almost only on shooters, and only at range, so today each drink is close to a free buff for melee and summoners.

1. **Make the spray noticeable on bosses.** Raise Tipsy aim error from x1.6 to x2.2 for guns, bows and magic bolts: about 77% damage on boss-sized targets at 25 tiles, about 58% on small ones, still about 100% inside 10 tiles. Better, make it additive so it survives grade: +6 degrees of spray on top of the grade jitter, rather than a multiplier that vanishes for a 3-degree Man-at-Arms. Beam swords get x3 (Terra Blade about 85 to 70%); check the Enchanted Sword's aim path first.
2. **Melee: Haymaker.** While drunk, one swing in three is a haymaker: +50% damage, double knockback, 0.4 s of recovery, plus 0.3 s slower turning. Net damage about even, but visibly sloppy and it throws enemies about. (Design numbers, not measured: the lab has no melee-wobble hook yet.)
3. **Summoners: minion jitter.** Minion attack dashes start up to 4 tiles off the target and a tenth of them whiff, aimed at roughly 15% less minion damage on a single big target. Not measured either; a follow-up lab can check the minion drift directly.
4. **Give each drink its own identity and its own wobble**, so the choice is which keg for which line, not "three flavours of the same drink":
   - Barracks Ale, the **Bulwark**: +8 defense and no flinch from knockback; mildest spray (x1.6 as shipped) and the slower turning. For the infantry line.
   - Black Stout, the **Berserker**: +15% damage (measured +17 to 23% on melee and beam swords), full spray x2.2 and the haymaker. It is the right keg for swords and the wrong one for snipers at 25 tiles on small targets (shooters net about 91% on a boss-sized body, about 67% on small ones), which is the decision we want.
   - Spiced Rum, the **Rush**: +10% crit (measured +6 to 7% on melee) and drunk levies run toward the enemy faster, because inside 10 tiles Tipsy costs a shooter nothing. Shooters charging to point-blank is visible and funny, and its price is risk (they are in the thick of it), not a nerf.

Open questions for Eve: whether the spray should be additive (grade-proof) or a bigger multiplier (simpler); and whether she wants a drink that is a pure upside for one class on purpose.

## Lab hooks and kit changes on this branch

`CombatHarness.TipsyLab.cs` (new): `tp=` token on a calrev `fight` line (weapon, ammo, class, distance, motion, size, drink, aim multiplier, pin, warm, window), counters for fired projectiles and hits, rows to `out.tsv.tipsy.tsv`. `HarnessTarget.Path` (lab dummy path). `levylab`: a `tipsy:` spec section that expands to cells, a pivot table for it, `mods:` honored for these specs, and a fix for the first-use install copy (`shutil.copy2` was called with a `symlinks=` argument).

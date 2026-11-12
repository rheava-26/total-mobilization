# Fizzy rework and rogue stealth policy (branch fizzrogue)

## Fizzy (Fizz Can, Case of Fizz)
Quarter gravity (vanilla's floor at the top of the Space layer, `NPC.GravityMultiplier *= 0.25` each tick, so `NPC.gravity` 0.3 -> 0.075 and everything that reads the body's gravity follows), a Cloud in a Bottle double jump with a smoke puff, six times the knockback, and knockback immunity (Cobalt Shield and the like) becomes 10% damage reduction. The defense penalty and the jump-height boost are gone. Duration and range are unchanged (5 minutes, `DrinkRadiusTiles`).

A knocked body flies: the launch is capped at 7 px/tick sideways and 4.5 up (vanilla caps at 16, a 60 tile rise in a quarter gravity), the movement code stays off until it settles (`SoldierNPC.Fizz.cs`), walls/ceilings/floors bounce it (0.6/0.6/0.45), a floor slides it (x0.9 per tick), air drag x0.975. The flight ends when it is on the floor and slow, wet, in lava, or after 7 s. Combat keeps running. Fall damage is skipped while Fizzy.

Lab (`fizz_lab.yaml`, 10 seeds x 8 blows of 12 damage, Minishark levy, arena a3, order follow; old = pre-rework Fizzy: resistance 0.75, normal gravity, movement never suspended):

| | old | new |
|---|---|---|
| gravity (px/tick^2) | 0.30 | 0.075 |
| distance thrown per blow (tiles) | 0.55 | 9.9 |
| peak speed (px/tick) | 2.3 | 6.8 |
| airborne until the AI is back (ticks) | 11 | 41 |
| walks back to a commander 45 tiles away | arrives (4.5 tiles short at 15 s) | arrives (3-4 tiles) |
| Cobalt Shield levy | same as no shield | same distance, 10% less damage per blow |

The blow lands in an arena with platforms 7 tiles overhead, so the flights above are shortened by ceiling bounces; in open sky a flight is about 2.7 s. Fall test (`fizz_fall.yaml`, 70 tile drop, fall damage forced on, 10 seeds per arm): old takes 79-80 damage from one fall; new takes 0. No levy died in any run (160 fights). Stuck test: `still_max` (longest stretch with no movement) is the idle time after arriving, equal in both arms; every levy reached the commander.

## Rogue stealth
Calamity empties the stealth bar on every throw that is not a stealth strike, so a rogue either throws plain (no strikes, ever) or waits ~2 s standing still for one strike. Measured on a dummy 20 tiles away (Statigel levy, 40 s window, 3 seeds per arm for the 72-weapon screen): the winner is a property of the weapon, not of its use time, so the table in `RoguePolicy.cs` is data and an unlisted weapon gets the hedge (strike for a boss or a crowd, plain throws otherwise).

Policies: Wait (strike beats plain throws on both a small target and a boss-size target), Spam, Situational (strike for a boss or 3+ enemies within 12 tiles, plain otherwise), SmallTargets (strike except against a boss). Rules from the screen: strike/spam >= 1.05 on a small target and >= 0.95 on a boss-flagged one: Wait; >= 1.05 small but < 0.95 boss: SmallTargets; < 0.95 small and < 1.05 boss, or < 1.05 small and < 0.95 boss: Spam; the rest Situational. 12 of the 72 weapons could not be measured (boomerangs, channelled and placed weapons never fire in the lab): FishboneBoomerang, InfestedClawmerang, ElephantKiller, WalkingCane, LemonNade, Brimblade, Icebreaker, Equanimity, Supernova, MangroveChakram, DoomsdayDevice, PulseGrenade take the default.

The Calamity wiki host (calamitymod.wiki.gg) is not reachable from the builder session (not in the allowed network domains), so the table is measured, not wiki-derived. The only wiki fact available (search snippet): the strike modifier is per weapon, and a strike consumes the whole bar; it lists Spear of Destiny at 400%, which the dummy lab does not show (spam 322 dps vs strike 222), so that row was left Situational by hand instead of Spam.

Files: `rogue_policy_table.md` (all 60 measured weapons: spam dps, strike dps, ratios, small and boss-size), `rogue_before_after.md` (before = the old always-wait behaviour, after = the table; 26 weapons across every tier, 10 seeds, small dummy and boss-flagged dummy). Rows whose policy on that target is still Wait are the same code as before, so their differences are noise (e.g. Mythril Knife 0.91, Sludge Splotch 0.88 on the small dummy). The three weapons moved from Situational to Spam after the run (Feather Knife, Stellar Knife, Shard of Antumbra) were measured at x1.00 / x1.01 / x0.96 on a small target and x0.90 / x0.83 / x0.91 against a boss, so they are not in the before/after.

# Boss-battery bugs: causes, fixes, proof (branch `bossbugs`)

Same army and seed in every before/after pair (hmreplays kit, one seed, 28 to 34 levies). "Before" = `LEVY_SPECSHOTS=0 LEVY_CALOVERRIDESLOT=0` on the fixed build (the old behaviour behind an A/B switch).
Projectile damage is what the levies took from hostile projectiles (damage / hits).

| fight | before | after |
|---|---|---|
| Deerclops Exp+Rev | DESPAWN at 6.7 s, 0 / 0 | WIN at 13.9 s, 210 / 72 |
| Wall of Flesh Exp+Rev | WIN 25.1 s, 0 / 0 | WIN 22.3 s, 47 / 46 |
| Wall of Flesh Master+Death | WIN 31.6 s, 0 / 0 | WIN 35.6 s, 122 / 93 |
| Lunatic Cultist Exp+Rev | WIN 32.9 s, 0 / 0 | WIN 40.3 s, 214 / 173 |
| Lunatic Cultist Master+Death | WIN 90.7 s, 0 / 0 (22 deaths) | WIN 44.9 s, 250 / 208 (18 deaths) |
| Moon Lord Exp+Rev | WIN 78.1 s, 0 / 0 | WIN 83.5 s, 12969 / 369 |
| Moon Lord Master+Death | WIN 83.7 s, 0 / 0 | WIN 111.5 s, 15381 / 745 |

Recorder end verdict, pre-fix build vs fixed build: Cultist Exp+Rev and Master+Death "despawn / fled" -> "killed / won"; Moon Lord Master+Death "despawn / fled" -> "killed / won".

## 1. Deerclops under Calamity
Cause (observed): Calamity's `DeerclopsAI` (bound in `CalamityVanillaAIOverrideNPC.SetDefaults` from Revengeance) indexes `Main.player[NPC.target]` at its shadow-hand lines (344, 405). Deerclops is a native-targeting type (`UsesNewTargetting`), so Levy gave it `target = levy.whoAmI + 300` and the AI threw IndexOutOfRange on its first attack; the fight showed `DESPAWN` at 6.7 s.
Fix: `CalamityTargetingCompat.OverrideBound` reads the override binding; `EnemyTargeting.NativeBehindCalamityOverride` moves such a type from the native route to the Empress's flyer slot (a real index, 254, holding the levy's dummy for the AI call).
Audit (world check, every `UsesNewTargetting` type with Revengeance on): 33 native types, 3 bound to a Calamity override: Deerclops(668), Bee(210), BeeSmall(211). The other 30 are the Old One's Army and Goblin Shark types, which Calamity does not override.
Calamity's vanilla-boss overrides that index `Main.player[NPC.target]` unguarded (sites): KingSlime 11, EyeOfCthulhu 36, EaterOfWorlds 12, BrainOfCthulhu 5 (+ Illusion 1, System 1), Creeper 6, QueenBee 45, Skeletron 33, Deerclops 5 (2 guarded), WallOfFlesh 26, QueenSlime 24, Destroyer 4, Twins 41, SkeletronPrime 50, Plantera 22, Golem 49, DukeFishron 4, Cultist 5, MoonLord 17; mini-bosses Dreadnautilus 3, IceQueen 12, Mothron 1, Pumpking 19; Empress none (GetTargetData only). Only types in `UsesNewTargetting` can receive a 300+ target from Levy (the one place that writes one is `EnemyTargeting.CompeteCore`), so none of those except Deerclops can throw. Every other boss gets target 255 (swap) or 254 (flyer slot).

## 2. Moon Lord and every other boss shot never hurting levies
Cause (observed): the battery's commander is a Spectator and a single-player world fires enemy projectiles with `owner = Main.myPlayer`, i.e. the Spectator. `PlayerLoader.CanHitNPCWithProj` asks the owner's `ModPlayer.CanHitNPC` first, and `SpectatorPlayer.CanHitNPC => !Active` ("deals no damage by any means") answered no, so `Projectile.Damage` skipped the levy. `CombinedHooks.CanHitNPCWithProj` read `False/spectator` on every Moon Lord bolt, sphere and eye that overlapped a levy (recordings: 891 sphere and 45 bolt overlaps, 0 hits). Moon Lord does target levies (core locked on a levy 98 to 99 % of ticks). Plantera's hits in the battery came from `spec=0` (visible commander). A dedicated server fires with owner 255 and was never affected.
Fix: `SpectatorHostileShots` (Common/Potions) detours `PlayerLoader.CanHitNPCWithProj`: a hostile, non-friendly projectile whose owner is a Spectator gets "no opinion", so the levy's own immunity window and every other rule decide. `LEVY_SPECSHOTS=0` restores the veto.

## 3. Wall of Flesh
Same veto (its Eye Lasers). Targeting itself is fine: with a visible commander in the Underworld the eyes and Hungry lock onto levies and the mouth keeps walking at the player; with a Spectator the mouth walks at the levies too; every laser sampled was aimed 0 degrees off the levy its eye was locked on. Against defence-35 battery levies a laser does 1 damage in Master (vanilla arithmetic), and the army kills the wall in 22 to 36 s, so few hits is expected once the veto is gone.

## 4. Recorder verdict
Cause (observed from `[FightRecDbg]` lines): in the closing window the recorder wrote only the LAST boss to leave. Moon Lord's core is killed (`OnKill`) but his hands and head vanish with it with no `OnKill` and full life, and the four Lunar Pillars the Cultist's death spawns are purged a tick later, so the last entry was a non-kill. Fix: `FightRecorder.FinalEndIndex` takes the last killed entry, else the last (worms that fall one after another are all kills, unchanged). `LEVY_RECEND=0` restores the old rule.

## Proof
`BossBugsWorldCheck` (`[BossBugs]`, chained after HitParity; `LEVY_BOSSBUGS_ONLY=1` runs it alone, `LEVY_BOSSBUGS_PART=calamity|shots|ml|wof`): Calamity audit + Deerclops + bees (36 checks in the Calamity half), hostile shots under a Spectator, real Moon Lord projectiles, Wall of Flesh with a visible commander and a Spectator. With the switches off: `shots` 1 of 7 pass (6 FAIL), `ml` 3 of 6, `calamity` 4 pass 7 FAIL (Deerclops and 6 bees throw). Recorder: `FightRecChecks` (5 pure checks) and `FightRecAutoCheck` scenario 1c (a boss killed while another boss and a pillar vanish in the same tick must be `_won`; with `LEVY_RECEND=0` it is `_fled`).

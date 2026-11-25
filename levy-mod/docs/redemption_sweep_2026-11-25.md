# Redemption weapon, armor and accessory sweep (branch redesweep, 2026-11-25)

Goal: every weapon, armor set and accessory of the mod works for a levy as it does for a player. Docs only: names are allowed here, never in anything the player reads.

Measured on tML 2026.08 with the mod's own 2026.06 build and its five libraries, Levy alone, one levy against one stand-in player, the pinned boss-flagged dummy, 600 ticks per run. Full per-weapon table: `REDEMPTION_SWEEP.tsv` (before and after, player and levy damage).

Tools (all existing; small switches added):
* army sweep: `LEVY_SELFCHECK_WORLD=1 LEVY_ARMYSWEEP_ALONE=1 LEVY_ARMYSWEEP_MODS=Redemption` (the Calamity gate is lifted for a mods sweep; `LEVY_ARMYSWEEP_ONLY=Name;Name` narrows it);
* summon parity table: `LEVY_SUMMONSWEEP_ALONE=1 LEVY_SUMMONSWEEP_TABLE=1 LEVY_SUMMONSWEEP_MODS=Redemption`;
* set-bonus harness: `LEVY_HARNESS=1 LEVY_HARNESS_SCENARIO=setbonus LEVY_HARNESS_SBMODE=static LEVY_HARNESS_SBFILTER=Rede` (the mod's sets and accessories are built by reflection, `LEVY_HARNESS_SBMOD` names another mod);
* world check, only with the mod loaded: `LEVY_REDE_ALONE=1` (IdentityWorld, `IdentityWorldCheck.Redemption.cs`), and the older BossBugs part `LEVY_BOSSBUGS_PART=redemption`.

Reference player: a channel weapon is used three ways (button held, tapped 12 ticks, tapped 40) and the best is the baseline, because a swing that happens when the button goes UP never fires on a held button. The old stand-in held it forever, which is why 22 weapons read "reference did no damage".

## Result (169 weapons of the mod)

| class | before | after |
|---|---|---|
| parity (0.6 to 1.3 of the player) | 32 | 67 |
| weak (below 0.6) | 16 | 25 |
| strong (above 1.3) | 20 | 25 |
| broken (levy deals nothing) | 10 | 1 |
| benched (not issued) | 47 | 26 |
| unmeasured, levy deals damage (the stand-in cannot use it) | 22 | 20 |
| unmeasured, levy deals none | 22 | 5 |

("before" is the same code measured with the old stand-in; its 12 "weak" and 22 "no reference" rows were mostly weapons nobody could measure.)

## What was wrong and what was done (generic unless noted)

1. **Ammo placeholder read as a powder shot (12 weapons benched).** tML's convention for an ammo weapon is `shoot = 10` (the Purification Powder id) meaning "the round decides". `WorldAlter.Compute` asked what projectile 10 does (it converts tiles) and refused every such bow and gun. Skipped now when the item has `useAmmo`. `WeaponClassifier` already knew; the world-alter scan did not.
2. **Owner-drawn weapons (60 projectile classes built on the mod's `TrueMeleeProjectile`: swords, hammers, spears, held guns, beam staves).** Their swing, charge, aim and ammo pick are written around `Main.player[owner]`, `channel`, `Main.MouseWorld` and `Main.myPlayer == owner`; as plain shots owned by the commander they swung at her. They go through the existing held chain (`ModShotKind.Holdout`, the levy's dummy in slot 255) by a RULE, `ModShotKinds.AutoHoldMods` (default Redemption; `LEVY_AUTOHOLD=0` or `=Mod1,Mod2` for A/B): the item's shot, or for an ammo gun the shot its ModifyShootStats / Shoot hook names, derives from the base class; or it is a channel shot that reads the owner's button. `AutoHoldPlain` lists five where the plain shot measured better (Eaglecrest Sling, Rockslide, Slayer Fist, Infected Eye, Petridish).
3. **Charge-and-release blades never swung.** The stall rule let go of a holder only after 150 ticks and only if it had made nothing; a charging blade spawns ring visuals, which counted as "made". For a held weapon of an AutoHoldMods mod only damaging children count, and the holder is let go after 30 ticks (`ModShotKinds.BladeStall`), lingering three use times.
4. **Held-chain projectiles were gated on a clear line from the commander (`ownerHitCheck`).** The gate is kept and judged from the LEVY: vanilla's test is switched off for a modded projectile in the chain and `SoldierProjectileTag.CanHitNPC` makes the same test from the holder's body (distance within `ownerHitCheckDistance`, `Collision.CanHitLine` from his body). With `LEVY_ARMYSWEEP_WALL=1` (one solid column between shooter and enemy): Redemption blades and towers deal 0 behind it and the same damage as before without it; Calamity holdouts behind it deal no more than the stand-in player (Exoblade 32k vs 45k, Phaseslayer 22k vs 74k, Voidragon, Wingman, Vulcan 0). Caveat: the stand-in player's slot is inactive while its projectiles are damaged, so vanilla's own gate does not bite for it; the player column is an upper bound, not a proof.
5. **Towers that ask their owner for the staff buff ended on tick one (5 sentries).** `OwnerLockProbe.ReadsOwnerBuff` finds a `HasBuff` read in the tower's own AI; the tower's type is registered when a levy plants it and `SoldierProjectileTag` puts the buff on the owner in PreAI and takes it off in PostAI, so it exists only inside the tower's own AI call: no icon, no buff update, no slot effect on the commander (checked: `HasBuff` false and `numMinions` 0 after the run). The use-gate probe had also asked CanUseItem with the commander's cursor tile (a placement check), which locked Log Staff and Nest Wand; it asks about an open sky tile now.
6. **Held weapons whose right-click replaced the swing.** NEVER rows in `AltPolicies.tsv` for five (Blind Justice 0 to 761 damage, Blood Letter 0.17 to 0.44, Ghastly Recurve 0.66 to 0.80, HyperTech Revolvers 1.61 to 0.75, Forgotten Greatsword).
7. **Children that wait for their holder** (a charged bolt reading its owner's button, pointing at the holder) stay in the chain for AutoHoldMods holders.
8. **The mod's own damage class** (10 weapons): see below.

## The custom damage class (verdict: done, not big)

OBSERVED. The class (`RitualistClass`) is an empty `DamageClass` that inherits only Generic. Its ten weapons are daggers and boline-style blades whose shot is a short slash drawn at the wielder's hand (`TrueMeleeProjectile`, `ownerHitCheck`, 10-tick life). The "custom resource" the old rule feared is a spirit gauge on the owner's ModPlayer (`RitualistPlayer`): hits drop spirit pickups, touching a pickup adds 5, the gauge sets a level (cap 2) that a few weapons read for an empowered mode. No weapon spends it. Nothing in the weapons needs a pool a levy lacks.

What was done: (a) `WeaponClassifier.ExtraCombatClasses` (a list beside the Thorium opt-in, so two companions do not overwrite one another) declares the class issuable; `RedemptionCompat.WireDamageClass` registers it by type name (`LEVY_REDECLASS=0` turns it off); (b) the daggers are held blades (reach 7, `HeldBladeOfExtraClass`); (c) a hit of a levy's custom-class shot feeds the levy's own gauge with what the pickups are worth on average (5 x 1/3 x 1.5 = 2.5), and the mod's own gauge step (decay, level up and down, which a levy's player never runs) is run for the fed levies (`RedemptionCompat.FeedGauge`, `PostUpdateEverything`).
MEASURED after: 10 of 10 can be held, all ten deal damage, none leaves a projectile behind or throws (BossBugs part `rit`); against a player: Blazing Balisong 0.87, Blighted Boline 0.83, Keeper's Knife 0.64, Worn Dagger 0.65, Budding Boline 0.62 (parity), Soul Skewer 0.58, Incisor 0.52, Obsidian Tecpatl 0.52, Grave Steel Secespita 0.49, Blood Letter 0.45 (weak; a levy re-spawns a dagger after the 20-tick respawn floor, a player taps faster). INFERRED: the gauge feed is right in kind and untested in effect (the sweep's player picks up nothing either, so neither side gets an empowered mode).

## Armor set bonuses and accessories (static diff, reference player against levy)

17 armor sets: all 17 match (every Player field, ModPlayer field and class stat the set changes is changed on the levy by the same amount). 76 accessories: 26 are music boxes or event-only with nothing static to compare, 49 match, 1 differs: Trapped Soul Bauble's +20 mana, which is not a miss in play (a levy's mana pool is the record's total, `Record.GearMana`, read through a scratch player).
The set-bonus audit had a gap: it never read a ModPlayer field (the `Player.ModPlayers` lookup returned nothing on this tML), so every earlier audit compared Player and class stats only. Fixed: ModPlayers are found through the registered types, array fields (elemental damage and resistance tables) are diffed cell by cell. Worth re-running the Calamity and Thorium audits.
NOT FIXED (a levy never uses it): the sets whose bonus is a key press (the mod's special-ability key: Xenium, Xenomite, the five Hardlight helms, Common Guard I; the Shade set's double-tap) and the accessories whose effect is an event hook on the owner's ModPlayer. A press needs the machinery Calamity's set keys have (`SoldierNPC.SetBonusKey.cs`): a policy row, a ready test per set, the press on the dummy with `ModKeybind.JustPressed` forced for the frame, and the projectiles it spawns adopted. Size: about one day for the four key sets (their code is in `BuffPlayer.ProcessTriggers`), plus its own checks.
INFERRED, not measured: event accessories and elemental set bonuses that read `Main.player[projectile.owner]` (the commander) in the mod's own hit hooks apply the commander's gear to a levy's shot, as for any mod whose hooks read the owner.

## Not fixed, and why

* Benched, 26: ten Crux cards (CanUseItem asks for a quest state), Lightning Rod (needs its conductor placed by the alt first), Xenium Grenade Cannon (needs the Xenium visor mode), Hammer of Proving (usable only while airborne), Pocket Sans, Flint and Steel, Sword Remote, Portable Holo Projector, Xenium Drone, Egg Cracker, Holy Bible, Forest Nymph's Sickle (utility items or shots built on another base), Lego Pistol (a consumable joke gun), four thrown eggs (3 to 10 damage).
* Broken, 1: Pure Iron Staff. Its charged bolt is launched by letting go of the holder; the chain releases it but the bolt never connects. Cause not found.
* Weak, 25 (0.04 to 0.59): the held swords sit at 0.5 to 0.6 of a player who taps at the best cadence; Android Hologram, Lightning Stone, Magma Staff and Cystling count the same minions as a player (the summon table is PARITY on all 14 staves, 23 rows) but deal 0.2 to 0.5 of the damage, cause not found; Log Staff 0.03 (acorn bombs every 80 ticks); Sun in the Palm 0.23; Blind Justice 0.10; Dragon Slayer's Bow 0.21; Grenegg Launcher 0.22.
* Strong, 25 (above 1.3 of the player): Icefall 86x, Chernobyl 44x, Rockslide 11x, Piercing Nebula 7x, Cosmos Chains 5x ... Most are weapons whose player run is limited by the stand-in's mana or ammo; not verified weapon by weapon. Rockslide and the Dragon Slayer's Staff are the clearest: the levy's plain shot out-damages a player by 4x to 11x.

## Regression runs of the generic changes (private installs, one server at a time)

* Vanilla half of the ship world checks (Levy + Mechanization + Warbands, tML 2026.08): IdentityWorld 177/0, MobWorld 42/0, BossBugs 169/0, everything else clean except LostFight (The Destroyer, "the army rallies where the commander fell": drift 124 tiles against a limit of 120; a boundary flake, unrelated to any file touched here).
* Calamity half (tML 2026.10): MobWorld, WhipReal, BossBugs (334/0) clean; known flickers only (FacingWorld Stormfront Razor, AmmoWorld Dryad's Tear 1.34, PerfBoss x4 on a loaded box) plus one new: IdentityWorld "summon loop" read the Borealis Bomber's idle casts as 5 (limit 3). The same sweep run alone on the base build and on this build gives 3 and 3 (pass both): a boundary flake in the chained run (INFERRED; `LEVY_SUMMONLOOP_ALONE=1` now runs that sweep alone for such A/B).
* Redemption world check (`tools/ship/shiprede.sh`, or `LEVY_REDE_ALONE=1`): IdentityWorld 13/0 (the custom class held 10 of 10; 12 weapons, one per route, at or above their floors), BossBugs part `redemption` 13/0.

Generic changes that touch other mods and were exercised by those runs: the world-alter scan (`shoot == 10` with ammo), the use-gate probe (cursor tile), the classifier's list of declared classes, `ownerHitCheck` re-judged from the levy for modded projectiles in the held chain, and the staff buff given to the owner only inside the AI call of a tower that reads it. The held-route rule itself (AutoHoldMods) is Redemption only by default.

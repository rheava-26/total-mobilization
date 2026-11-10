# Warbands: Levy seams

Warbands is `warbands-mod/Warbands/`, a companion mod of Levy (`build.txt`: `modReferences = Levy`, `sortAfter = Levy`), built the way Mechanization is. Levy source is untouched in this branch. Every place Warbands touches Levy:

## Used today (all already public in Levy)

| Levy symbol | Used for |
|---|---|
| `Levy.Common.Weapons.WeaponRank.Progress()` | The world's stage on the Boss Checklist scale; `Stage.Current` maps it to 0..6, so Calamity and Thorium bosses move the stage exactly as they move Levy's weapon ranking. |
| `Levy.Common.Weapons.WeaponRank.StageDefense` | The defense line a band member's defense is held to (8, 14, 24, 34, 40, 46, 55). |
| `Levy.Common.Weapons.ProgressionTier.EnsureBuilt()` / `.Of(itemType)` | The self-check holds every roster item to its stage's tier ceiling, so a "pre-boss" band cannot carry a post-Skeletron weapon. |
| `Levy.Common.Targeting.EnemyTargeting.Bodies` (`ITargetBody`) | The live levy bodies a band member or the Cultist circle may shoot at, beside the player. |
| `Levy.Common.LevyConstants.HostileProjectileDamageMult` / `GradeDamageMult` / `MaxSoldierHp` / `TilePx`, `LevyUtil.IsServerOrSingle` | Hostile projectiles are spawned at half damage because Levy's soldiers, like players, take twice a hostile projectile's damage; load-time proof of the reference. |

Passive behaviour Warbands relies on without calling anything: `EnemyTargeting`'s swap points a hostile NPC with a Fighter aiStyle (the band soldier uses vanilla's) at the player's levies as well as the player; `SoldierNPC.ContactDamage` hurts a levy for any hostile NPC with contact damage; `SoldierTargeting.PickTarget` fires levies at any hostile NPC, so levies fight the band with no extra code.

## Not reused yet: the Levy soldier brain (phase 2)

Phase 1 band soldiers walk on vanilla's Fighter AI plus a small shooter layer (`BandSoldier.PreAI`: hold at range, step back, jump to juke, fire a hostile projectile). That is not Levy's soldier AI. `SoldierNPC` is a friendly body bound to a `SoldierRecord` (commander, barracks, armory, loadout), and its Brain, Movement, Hook, WeaponUse and Combat partials all assume that and a friendly side. Reusing them for a hostile body needs these seams in Levy, in order of size:

1. **A side flag on the soldier body** (`SoldierNPC.Hostile`, or an `ISoldierBody` interface): `NPC.friendly` false, no roster death or respawn, no commander, no home barracks, no armory lookups, no dialogue, no mutiny, no officer paths. The loot, banner and death-credit code paths need a null commander.
2. **Target selection by side.** `SoldierTargeting.PickTarget` is "nearest hostile NPC". A hostile soldier needs "nearest of the players and the levy bodies (`EnemyTargeting.Bodies`)", with the same line-of-sight and unreachable-target rules. A single `Func<SoldierNPC, int>` target provider on the body would do.
3. **Projectile and contact ownership.** `WeaponUse` fires real item projectiles owned by `SoldierMinions.TrueMyPlayer` with `friendly` set. A hostile soldier needs `hostile = true`, `friendly = false`, damage halved for the player rule (or an `ISoldierShotFilter` hook), and the shot tag (`SoldierProjectileTag`) carrying the band id.
4. **Damage intake.** `SoldierNPC` returns false from `CanBeHitByItem` and from friendly projectiles. A hostile soldier must be hit by the player's weapons as a normal enemy, with the vanilla NPC rules (no 40-tick player-parity i-frames), and take Levy's formation, fear and shield logic out of the path.
5. **A loadout without a record.** `SoldierRecord.Gear` is filled from the armory. A hostile soldier needs a `SoldierRecord` built from a roster (weapon, ammo, armor, accessories, HP from `BandStats`) that never enters `RosterSystem`, plus `RefreshStats` accepting a record-less stat budget (life, defense and damage from the caller).
6. **Mobility.** `Abilities` (double jump, dash, hook, wings) come from the dummy's gear and already work; the Cultist circle's flight can be a `Hover` movement mode on the body rather than the special case in `CultistBand.cs`.

Phase 1 stops here because (1) to (5) touch the heart of `SoldierNPC` that other builders are changing right now (movement, loot, armory, provisions). Suggested minimal change in Levy: a public `HostileSoldierHost` abstraction that the brain asks for "who are my enemies" (`IEnumerable<NPC/Player>`), "what is my side" and "who gets credit", with the default implementation the current behaviour. That keeps the diff in Levy to the target provider, the side flag and the shot filter, and leaves everything else to Warbands.

## Public Levy API Warbands would like

- `SoldierNPC.ExtraBuffStats` already exists (the companion stat hook); a matching `ExtraDefense`-style hook for hostile bodies is not needed.
- `ProgressionTier.Of` is read-only and fine. A public `WeaponRank.StageOf(float progress)` would replace `Stage.FromProgress`.

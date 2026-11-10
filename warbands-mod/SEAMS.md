# Warbands: Levy seams

Warbands is `warbands-mod/Warbands/`, a companion mod of Levy (`build.txt`: `modReferences = Levy`, `sortAfter = Levy`), built the way Mechanization is.

## Phase 1: public Levy symbols used (no Levy edits)

| Levy symbol | Used for |
|---|---|
| `WeaponRank.Progress()`, `WeaponRank.StageDefense` | The world's stage and the defense line a band member is held to. |
| `ProgressionTier.EnsureBuilt()` / `.Of(itemType)` | The self-check holds every roster item to its stage's tier ceiling. |
| `EnemyTargeting.Bodies` (`ITargetBody`) | Live levy bodies the phase-1 Fighter-AI soldiers and the Devotees' orbs aim at. |
| `LevyConstants.*`, `LevyUtil.*` | Hostile projectile halving rule, constants. |

## Phase 2: the Levy brain for enemies (what was added to Levy)

A band member is `BandLevyNPC : SoldierNPC` (Warbands). Levy's brain, movement (hooks, wings, dodging), weapon use and drawing run unchanged. The Levy edits, all marked `HOSTILE SEAM` in the source:

1. **New file `Content/NPCs/SoldierNPC.Hostile.cs`.** `SoldierNPC.Hostile` (virtual, false for every Levy body); `HostileSide` (static: while a hostile body's AI runs, `LevyUtil.IsHostileTarget` and `SoldierTargeting.IsNeverTarget` answer for the player's side: levies, officers, summoned units, drones and turrets, and the players); `IHostileSideTarget` (the NPC a companion parks on each player; Warbands' `PlayerStandIn`); the public API a band policy uses (`HostileBind`, `HostileEngage`, `HostileGoTo`, `HostileStop`, `HostileSetRouted`, `HostileNearestEnemy`, `HostileThink`, `HostileMayFire`, `HostileShotDamage`, `HostileMeleeDamage`, `HostileStandoffMult`, `HostileDefenseCap`); `TagHostileShot`; `HostileSwing`; `ReceiveHostileMelee`; the red nameplate.
2. **Side flag and AI scope.** `SoldierNPC.AI` sets `HostileSide.Active = Hostile` around the body. `PinLifeMax` and the defense clamp skip a hostile body (its life and defense come from its band).
3. **Target selection by side.** `IsNeverTarget` / `IsHostileTarget` first lines; `ComputeNeverTarget` no longer treats a hostile soldier as a levy (so levies shoot it); `ScanNpcs` reads "own side" as `n.friendly != Hostile`.
4. **Brain decision.** One line in `Brain_ThinkBody`: a hostile body calls `HostileThinkBody` (the subclass's `HostileThink`) instead of the order logic; the standoff reads `HostileStandoffMult`.
5. **Projectile ownership.** `WeaponUse.TagSoldierProjectile` hands a hostile body's shot to `TagHostileShot`: hostile-owned, damage halved by the player rule through `HostileShotDamage`, tagged with the source NPC (not a soldier index, so a hostile shot is never a levy's). `SoldierProjectileTag.HostileShot` is synced so clients flip the projectile hostile too. `WeaponUse.TryAttack` asks `HostileMayFire` first (the band's attack token and warning rule). `ProcessSwing` hands a blade swing to `HostileSwing` (player through the stand-in, levies through `ReceiveHostileMelee`).
6. **Damage intake.** A hostile body is an ordinary enemy: its `CanBeHitBy*`, `ModifyIncomingHit`, `CheckDead`, `PreKill` are overridden in the subclass (vanilla hit immunity, no player-parity i-frames). `ContactDamage`, `Potions`, `Fear_Update` and `DialogueSystem.Bark` return early for a hostile body.
7. **Not a levy anywhere else.** `RosterSystem` and `BossRule` body lists skip it (so it is never in the roster, enemy targeting, or fear); the red `Nameplates` branch; dodging reads `IsThreatShot` (a hostile body steps out of the player's side's shots).
8. **Record-less loadout.** The subclass builds a `SoldierRecord` from the band roster that is never added to `RosterSystem`; `HostileBind` binds it with the band's life and defense cap.

Still Levy's own and unchanged: pathing, hooks, wings, double jumps, platform drops, the firing-spot search, the standoff, `WeaponUse` per weapon family.

## Not done

- Summon staffs and sentries on a hostile body (rosters carry guns, bows, staves that fire, blades).
- The Cultist circle's flight is replaced by the levy brain for archers (no `Hover` movement mode was added to Levy).
- Visuals are unverified headless (the draw path cannot run on a dedicated server).

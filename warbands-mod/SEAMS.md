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

## Phase 3 (Warbands 0.2.0): three more small seams, all marked HOSTILE SEAM

1. `SoldierNPC.HostilePassive` (virtual, false): a hostile body the player's side must leave alone (a neutral band nobody has provoked). `SoldierTargeting.ComputeNeverTarget` treats it like a levy: levies, officers and turrets do not target it. Warbands also sets `NPC.chaseable` so vanilla minions skip it.
2. `SoldierNPC.HostileVanity()` (virtual, null): the vanity a hostile body is drawn in. `SoldierNPC.ResolveVanity` asks it instead of the armory for a hostile body (client draw path only).
3. `SoldierNPC.HostilePlateColor` (virtual): colour of a hostile body's name and bar (Warbands: gold while neutral).

Levy symbols newly used: `SettlementSystem.Instance.NearestBarracks` (the bounty notice turns in at a Barracks), `VanityLook` / `VanitySet`, `SoldierProjectileTag.SoldierIndex` (a levy's shot hit a band member: banter).

## Phase 4 (Warbands 0.3.0): no Levy edits

Public Levy symbols read: `DialogueTable.Lines` / `Get` / `LineCount` / `LoadedFrom` and `DialogueLine` (`Cond`, `Needs`, `Parts`, `Text`), `LineCond`, `LineNeeds`, `PlaceholderKind` (`BandChatter` borrows Levy's fighting lines, so the player's edited Levy dialogue file applies to bands too). `SoldierNPC.BodyWidth` / `BodyHeight` (the footing check). The Calamity Sunken Sea is read by name through tModLoader's `ModBiome` list; Calamity is never referenced.

## Phase 5 (Warbands 0.4.0): no Levy edits

Event bands add no Levy edit. Public Levy symbols newly read: `WeaponClassifier.Classify` and `WeaponFamily` (the self-check holds every event weapon to a family Levy's brain can fire) and `ProgressionTier.Of` (tier ceilings for signature items and drops). Vanilla seams used: `Main.invasionType`, `invasionSize`, `invasionSizeStart`, `invasionX`, `invasionDelay`, `Main.ReportInvasionProgress` and packet 78 (a dead event-band member takes its slot off the invasion like a vanilla invader; Levy's `EventPressure`, which multiplies an invasion's size when it starts, is unaffected), `Sandstorm.Happening`, `Main.slimeRain`, `NPCID.PirateShip`, `Player.ZoneSandstorm` and `ZoneOverworldHeight`.

## Phase 6 (Warbands pursuit): one additive Levy seam, `SoldierNPC.Hostile.cs`

Levy's long-route planner (`RoutePlanner`: walking, drops, hook casts and chains, wall climbs, wing arcs) was reachable only from the map-march order; a hostile body had no way to ask it for a route, so a band whose foe was not reachable by the brain's local search stood where it was. Added (all marked HOSTILE SEAM, nothing inside the planner or the executor changed):

| Symbol | Use |
|---|---|
| `HostileRouteTo(Vector2 goalBottom, out bool noWay)` | Walk to a point by the route planner. True while a route leg is under way. `noWay`: the planner gave up on the goal. Asking for a different tile resets the route. |
| `HostileRouteStatus` | 0 none, 1 searching, 2 found, 3 best-effort (the goal itself is out of reach), 4 failed. |
| `HostileRouteReset()` | Forget the route and its banned hook targets. |
| `HostileMovementBusy` | The body hangs on a hook, climbs a face or flies a planned arc: do not hand it a new goal. |

Levy symbols newly read by Warbands: `TileNav.InBounds / IsFloor / IsStandable / BottomCentre / FeetTile`, `OffscreenHop.Seen / RoomIsOpen`, `FollowerTravel.HopTo`, `WeaponProfile.RangeTiles / IsMelee`, `SoldierNPC.HasFlightKit` and `Record.Gear.Hook`.

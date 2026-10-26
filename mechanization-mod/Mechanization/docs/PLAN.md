# MECHANIZATION — Design + Build Roadmap (draft for lead refinement)

*Companion submod to **Levy** (tModLoader 1.4.4). "The metal" (turrets / drones / vehicles) to Levy's
"the people." Depends on Levy via a hard `modReferences = Levy`, `sortAfter = Levy`. Written 2026-10-25
from the live code on `worktree-levy-stardust` (Levy 1.4.4 + Mechanization 0.3.0) and the designer's
2026-10-24/25 directives. RESEARCH + PLAN only — nothing here is built.*

Guiding tenets (inherited from CLAUDE.md, confirmed by designer):
- **Tenet #1 — say YES to power.** Difficulty is a scarier enemy or a real cost (ore / station / upkeep /
  logistics), never a cap or a nerf. Every "that'd be OP" gets a "yes."
- **Surplus is the whole point.** Every Mechanization combat unit draws its armament from spare weapons in
  the Levy **Armory** — the designer explicitly rejected "mirror issued gear"; the Armory is the logistics
  hub you stock. Ore is a *chassis* cost layered on top, never a replacement for the surplus-weapon model.
- **Art is PARKED** until gameplay is solid. Everything below uses borrowed vanilla sprites as placeholders;
  art direction (v4 = angular faceted hulls, flat thin rotors, Terraria 3/4 angle, 4-frame sheets) is
  recorded for later, not planned here.

---

## 1. CURRENT STATE

### 1.1 What v0.1–v0.3 shipped
Mechanization is a real second `.tmod` at `mechanization-mod/Mechanization/`. Three slices, all around a
single content type, the **turret**:

- **v0.1 — the turret CHASSIS.** `TurretItem` (craft: 10 iron/lead + 20 wood at a Workbench) places a
  `TurretNPC`: a friendly, non-town, `aiStyle = -1` NPC that falls under its own gravity, rests on terrain,
  and stays put. 400 HP, own healthbar, destructible by contact + hostile projectiles (no friendly fire).
  Acquires nearest hostile within 32 tiles w/ line-of-sight, fires `ProjectileID.Bullet` every 30 ticks.
  Borrows the vanilla Cannon sprite. Modeled directly on Levy's `DroneNPC`.
- **v0.2 — persistence + surplus firing.** `TurretSystem : ModSystem` is a world-save registry: it stores
  each placed turret's position + HP and re-spawns the NPCs on load (the NPC body itself is transient,
  `NeedSaving()=false`); a destroyed turret's record is dropped via a reconcile pass so it never resurrects;
  cleared on world-unload so nothing leaks between worlds. The gun now **fires your surplus weapons**: every
  90 ticks it finds the nearest Armory within 80 tiles, scores the best mountable RANGED weapon on its racks
  (`WeaponClassifier.Score`), and fires that weapon's *real* projectile through Levy's own
  `DroneArsenal.BuildShot` (ammo-resolved, real damage / cadence / spread / gravity / homing, lead-aim),
  crediting kills to the nearest commander via `SoldierProjectileTag`. A **non-consuming read** (no lend
  ledger — the turret just mirrors the best gun). Falls back to the fixed bullet when nothing is in reach,
  so it is never inert.
- **v0.3 — enemy aggro + casters + weapon sprite.** (1) `TurretNPC` implements `ITargetBody`; its dummy
  Player is co-located every tick and `TurretSystem.PreUpdateNPCs` appends live turrets to
  `EnemyTargeting.Bodies` *after* Levy rebuilds that list, so hostiles actively path to and attack turrets
  and share aggro with nearby soldiers. (2) `BestSurplus` now also accepts `DamageClass.Magic` (no mana
  economy — generous by design). (3) `_weaponType` is synced (`SendExtraAI`/`ReceiveExtraAI`) so clients
  draw the real gun/staff over the cannon, rotated to aim.

**Runtime status:** every slice is reviewed + headless-boot-clean but NOT live-runtime-tested (a headless
boot never spawns a turret or loads a world). Designer's live run is the real test. Load-time log lines give
some confidence.

### 1.2 Architecture (the proven cross-mod pattern)
- **Hard dependency, one direction.** `build.txt`: `modReferences = Levy`, `sortAfter = Levy`. Mechanization
  references Levy's public types; Levy must NEVER reference Mechanization. Build-time + live runtime
  cross-assembly calls are proven (`Mechanization.Load` reads a Levy const AND calls a Levy static).
- **Two generic seams already carved into Levy (1.4.3) so a companion can extend targeting with zero
  mod-specific code in Levy core:**
  1. `EnemyTargeting.Bodies` was made `internal → public` (`static readonly List<ITargetBody>`), so a
     companion's `ModSystem` can append its own `ITargetBody` each tick exactly as Levy's own `DroneSystem`
     does. Mechanization's `TurretSystem.PreUpdateNPCs` uses this.
  2. `EnemyTargeting.LiveTarget` gained one additive branch: `if (npc.ModNPC is ITargetBody ext) return
     ext.IsAlive ? ext : null;` — a generic resolver for any cross-mod body, unreachable on a Levy-only
     install.
- **Tunables isolated.** `Common/MechConstants.cs` holds every Mechanization number (`MechConstants.Turret.*`);
  the cross-mod surface is confined to a handful of explicit call sites.
- **Toolchain gotchas (recorded for future work):** builds via `tmlbuild` (resolves the Levy ref from the
  installed `Levy.tmod`); `compile.sh` can't see Levy. Root `.gitignore` is an allow-list and needed a
  `!mechanization-mod/` entry or commits were silently empty.

### 1.3 Reusable Levy API (the surface Mechanization builds on)
These are the proven, public Levy symbols Mechanization already consumes or will consume:

| Levy symbol | What it gives |
|---|---|
| `DroneArsenal.BuildShot(weapon, ammo)` → `DroneShot` | The whole light shot pipeline: projectile, speed, knockback, BaseDamage, IntervalTicks, Count, SpreadDeg, Gravity, Homing, StepMult, Class, Crit, RangePx, `Valid`. Pure function, no world state. |
| `DroneArsenal.IsMountable / TakesAmmo / MaxHpFor / DefenseFor / DefaultAmmoSample / ResolveAmmoProjectile` | Eligibility + ammo resolution (vanilla `PickAmmo` faithful) + weapon→toughness mapping. |
| `WeaponClassifier.Score / ItemDamageClass / GetProfile / RarityTier / ProjectileSample / ProjectileHasGravity` | Rank a weapon, read its family/damage class/range profile. |
| `Aiming.SolveIntercept / ApplyError / LongRangeErrorScale` | Lead + arc + jitter aim solve. |
| `EnemyTargeting.Bodies` (public), `.LiveTarget`, `ITargetBody` | Make a unit a target hostiles path to (slot-255 dummy swap). |
| `ITargetBody` { NPC, Dummy, TargetAggro, IsAlive, SyncDummyTransform() } | The interface every targetable allied body implements. |
| `SoldierProjectileTag` (GlobalProjectile): `CommanderIndex`, `DroneShot`, `IsSoldierShot` | Kill-credit + base-safe explosive guard. |
| `LevyUtil.IsHostileTarget / IsClient / IsServerOrSingle / ModeDefenseEffectiveness` | Hostile filter + netmode + mode scaling. |
| `SoldierTargeting.IsNeverTarget` | Never-target filter (friendly-ish / critters). |
| `ArmoryTE`: `IssueWeapons`, `IssueAmmo`, `Storage`, `Center`, `Lent`, `IsLent`, `ReturnLent`, `LendDroneWeapon(reserve)`, `LendDroneAmmo(ammoId, keep)`, `ReclaimLentWeapon` | The surplus racks + the lend ledger. |
| `SoldierNPC.DummySlot` (= 255) | The slot the targeting swap uses. |
| `LevyConstants.*` (incl. `Drones.*`, `ArrowGravity`, `TilePx`, `UI.PatrolBannerSearchTiles`) | Shared constants. |

---

## 2. WORK ITEM 1 — DRONE MIGRATION + PATROL BEHAVIOR (the first concrete slice)

> **STATUS (Mechanization 0.6.0 / Levy 1.6.7): BUILT, with these deliberate departures from the plan below.** The drone moved to Mechanization (`Content/NPCs/DroneNPC.cs`, `DroneSystem.cs`, `DroneControls.cs`, `DroneWiringCheck.cs`; `Common/DroneMath.cs`, `ShotKit.cs`, `MechConstants.Drone`). `DroneArsenal` and the Armory ledger stayed in Levy, public. **A Guide to Engineering stayed in Levy** as the unlock (item + `GuideSystem.Engineering` world flag; no item save-compat loss). The patrol-banner design in §2.4 was REPLACED by a **loiter box** above the Barracks (no banners), plus **recall/launch** orders (drone right-click panel + a keybind, packets `DroneCommand`/`DroneKey`) and a **battery** (a cost, never a cap). Not built from this section: the ore-chassis cost (§2.5) and the Engineering-unlock redesign.

This is the designer's headline ask and the natural next step after "MP-client placement." Two coupled
changes: **(A) re-home the combat drones from Levy into Mechanization**, and **(B) change their behavior
from idle-hover-over-barracks to patrol-and-engage.**

### 2.1 What the drones are today (Levy 1.4.0, still living in Levy)
- `DroneNPC` — a hovering `ITargetBody` NPC that mounts ONE surplus ranged/magic weapon LENT by a Barracks'
  Armory (the actual `Item` instance sits on `ArmoryTE.Lent`, never in the drone's keeping — so nothing is
  ever lost or duped). Fires via `DroneArsenal.BuildShot`. HP/defense scale with the weapon's rarity tier.
  Hovers ~7 tiles over its Barracks in rows of 5 (`AnchorFor` + `FormationOffset`); bobs; a drone knocked
  far off-station is set back after a leash timer. Transient (`NeedSaving()=false`).
- `DroneSystem` — the garrison. Round-robin: visits ONE Barracks / 30 ticks; keeps up to 10 drones aloft
  (`PerBarracksCap`), one per surplus weapon, launching one every 600 ticks; soldiers get first pick
  (`ArmoryReserve` soft reserve + a hard recall path); feeds `ActiveBodies` to `EnemyTargeting`.
- Gated behind the Levy item **A Guide to Engineering** (`GuideSystem.Engineering` world flag).

### 2.2 The migration (which files move, which stay)
**Move OUT of Levy into Mechanization** (`mechanization-mod/Mechanization/Content/NPCs/` + `Common/`):
`DroneNPC.cs`, `DroneSystem.cs`, `LevyConstants.Drones.cs` (→ `MechConstants.Drone`), the
`GuideToEngineering` item + its recipe, and the drone-specific hook bodies in
`ArmoryTE`/`SoldierProjectileTag`/`ThrownBlastGuard`/`GuideSystem`.

**KEEP in Levy as shared, public weapon utilities** (do NOT move): `DroneArsenal` / `DroneShot`,
`WeaponClassifier`, `Aiming`, `ArmoryTE`, `EnemyTargeting`, `ITargetBody`, `SoldierProjectileTag`,
`SettlementSystem`, `RosterSystem`, `BarracksTE`.
Rationale: (a) the dependency is one-way — if `DroneArsenal` moved to Mechanization, Levy's own code could
not reference it and **Mechanization's `TurretNPC` already depends on Levy's `DroneArsenal`**; (b) these are
genuinely general (they classify/aim ANY weapon and run the Armory), not drone-specific. The name
`DroneArsenal` is now a slight misnomer (it's "the light weapon-mount pipeline") — leave the name, it's
load-bearing across the cross-mod boundary.

**Levy visibility audit needed before the move** — confirm these are `public` (some are, via the turret
work; the rest are the migration's real API change in Levy core, all GENERAL not mod-specific):
`BarracksTE` (+ `Center`, `Position`, `Manpower`, `Militia`, `ID`), `SettlementSystem.LinkedArmory` /
networked-armory link, `RosterSystem.Regulars` / `ActiveBodies`, `ArmoryTE.LendDroneWeapon` /
`LendDroneAmmo` / `ReturnLent` / `IsLent` / `Lent`, `SoldierNPC.DummySlot`, `SoldierTargeting.IsNeverTarget`,
`OfficerBrain.TownDamageScale`, `LevyConstants.ContactBroadPhasePx` / `ImmuneTicksOnHit` / `ArrowGravity` /
`HomingAmmo` / `HeldRealCadence`. Each made public is a one-line, Levy-only-safe change.

**After the move, revert Levy to pure soldiers:** delete the Levy drone content + the Engineering guide from
Levy, so a Levy-only install has no drones (consistent with "Levy = the people"). Drones now require BOTH
mods enabled.

### 2.3 Migration RISKS (call out to the lead)
1. **Save-compat — the Engineering guide ITEM.** It's a Levy `ModItem` today. Moving it to Mechanization
   changes its type; tML drops unknown items, so a save that had the guide in a chest/inventory loses it.
   The `GuideSystem.Engineering` bool is Levy *world* data. **Mitigations:** (a) keep a dummy/`Unloaded`
   passthrough, or (b) accept it for dev playtest (designer already said "a save using drones then needs
   BOTH mods enabled — fine for dev"), or (c) if ore-gating lands (§2.5) the guide may be *replaced* by a
   crafting station anyway, making the item moot. Decide the Engineering-unlock's new home (Mechanization
   world flag vs station-exists check) as part of this slice.
2. **Drone bodies are transient — no NPC save data to migrate.** The garrison re-launches from the Armory
   on load, so there is no per-drone save to port. This is the easy half.
3. **Cross-mod ownership of the lend ledger.** The LENT `Item` instances live on Levy's `ArmoryTE.Lent`.
   Once `DroneSystem` lives in Mechanization, it drives Levy's ledger across the assembly boundary. The
   conservation invariants (81 harness assertions in 1.4.0) must be re-verified cross-mod: drone death,
   dismantle, Barracks/Armory loss, soldier recall, save/reload all still return the weapon exactly once.
   This is the highest-value thing to re-test after the move.
4. **API fragility.** Single-author both sides (manageable per the designer), but every Levy symbol
   Mechanization now needs is a public contract — keep it stable; prefer additive changes.

### 2.4 The behavior change — PATROL + ACTIVE ENGAGEMENT (the designer's real ask)
Today drones hover over the Barracks. The designer wants them to **follow patrol banners and actively
attack**, like the current ladder item "rotorcraft drones = the current surplus system reskinned."

**How Levy patrol banners work (grounded in code):**
- `PatrolBannerTile` + `PatrolBannerTE` — placeable banners; a `PatrolBannerTE.Center` is where a patrolling
  body stands. Banners are enumerated from `TileEntity.ByID`.
- `UIActions.FindPatrolPair(worldPos, tiles, out Point16 a, out Point16 b)` — returns the **two nearest
  banners** within a tile radius; false if fewer than two exist. **This is the exact hook to reuse.**
- Soldiers: `SoldierRecord.PatrolA`/`PatrolB` (Point16 tile coords), set by `AssignPatrol`. The brain's
  `ThinkPatrol` walks leg A → pause (`PatrolPauseTicks`) → flip to leg B; `TryNearestPatrolPoint` + a
  `ClampToCircle(..., PostChaseLimit)` leash keeps a patrolling/militia body from wandering off its post
  while chasing. `LevyConstants.UI.PatrolBannerSearchTiles = 60`; `PatrolArriveXTiles/YTiles`.

**Proposed drone patrol design (reuses the above, flying version):**
- At garrison/launch, a drone resolves a **patrol anchor** instead of the static barracks hover anchor:
  call `FindPatrolPair` around the Barracks (or around a designated banner). If a pair exists → the drone's
  anchor becomes a **patrol route** (two world points at hover altitude). If not → it keeps the current
  hover-over-barracks behavior (graceful fallback; nothing breaks for players who place no banners).
- Give the drone a small leg state machine mirroring `ThinkPatrol`: cruise toward leg A at hover altitude
  (reuse the existing `Hover`/`SteerLerp`/`ArriveGain` steering), pause, flip to leg B. The drone flies the
  line instead of a soldier walking it.
- **Active engagement already exists** — `FindTarget` acquires the nearest hostile in weapon range every
  10 ticks and breaks off to fire. The only addition is a **patrol leash**: while engaging, clamp the
  drone's chase to a radius around the current patrol segment (the flying analogue of `PostChaseLimit`), so
  a patrol line is actually held and drones don't all peel off after one enemy. Tunable in `MechConstants`.
- **Optional (designer "actively attack" emphasis):** a `SearchDestroy`-style anchor — patrol toward the
  nearest live hostile within a generous radius of the route, so the line advances to meet threats rather
  than passively waiting. Mirrors how Levy fixed militia ("default to SearchDestroy, roam-anchored").

**Known adjacent bug to respect:** "militia only patrol ONE side of the map" (ROADMAP, not yet dispatched)
— the soldier patrol/movement AI has a live defect. Don't build drone patrol on top of a broken assumption;
either dig that first or make drone patrol self-contained (resolve its own banner pair, don't inherit the
militia leg logic wholesale).

### 2.5 Fold in the ore-chassis cost (designer "change of heart", supersedes "costs nothing but a weapon")
The designer wants drones/turrets/vehicles to cost **cheap prehardmode ore** — a second life for obsolete
ore ("melt the old world into war materiel") and the tenet-#1-correct answer to the free-firepower lever.
OPEN decisions still owed by the designer (see §7): one-time build cost vs ongoing supply drain; whether ore
ADDS to the weapon-lend model or replaces it; exact ores/amounts (proposal ~5–10 bars/drone, more for
emplacements/vehicles). **Plan assumes one-time build cost, additive to surplus** (simplest, "not
detrimental") until the designer rules. This cost is what the dedicated crafting stations consume (§4, §5).

---

## 3. TURRET TIERS

The turret is the "easy high-fun win" (no pathfinding/pilot/entity-cap pain) and the designer's "build FIRST
if anything." v0.3 shipped ONE turret. Tiers give it progression. Keep every tier surplus-fed; differences
are **chassis** stats + a special rule, not weapon nerfs.

Proposed ladder (all tunable constants in `MechConstants.Turret.<Tier>`):

| Tier | Name (placeholder) | HP | Range | Fire behavior / special | Craft gate |
|---|---|---|---|---|---|
| T1 | Emplacement | ~400 | 32 tiles | 1 surplus weapon (current v0.3 turret). | Workbench + cheap ore. |
| T2 | Bastion | ~800 | ~40 tiles | 1 weapon, +armor (higher defense), self-repair when unhurt (reuse drone `Regen`). | Drone Assembly / Anvil + more ore. |
| T3 | Redoubt | ~1400 | ~48 tiles | **Multi-mount: 2–3 surplus weapons, independent cooldown/target each** (a loop over N `DroneShot`s — the mechanic the ROADMAP's "Heavy multi-slot drone / Leviathan" wants, proven trivial: firing is already 1 weapon = a loop, N = the loop). | Dedicated station + significant ore. |
| T4 | (late / "Cyborg-tier") | ~2200 | ~56 tiles | Multi-mount + a special: e.g. a **barrage** every ~30s, or a short **shield dome** (`shieldR`/`shieldHp` analogue), or pierce. | Hardmode-tier station + hardmode bars. |

Special-variant turrets the designer already named (slot alongside, not strictly a linear tier):
- **Summoner turret** — mount a SUMMON staff (currently refused by `IsMountable`) → maintains that staff's
  minions. Needs a new mount path (summon weapons don't fire a plain projectile); scoped as its own slice.
- **Sentry platform** — mount a SENTRY staff → plants/maintains sentries ("3000 on top").
- **Caster turret** — already shipped in v0.3 (magic weapons mount + fire their bolt).

**Crafting / placement.** Each tier is a `TurretItem`-style placeable consuming ore + (higher tiers) a
dedicated station's presence. `TurretSystem` already persists arbitrary turrets by position+HP; extend the
`TurretRecord` to carry a **tier id** (and, for multi-mount, nothing extra — weapons are re-picked from the
Armory on load, not saved). Tier is the only new save field. Designer QoL asks to fold in here (from Eve's
request flood): **dismantle** (recover the turret), **check turret inventory / in-range indicator**, and a
much **bigger / network-wide Armory reach** (real reason: towns suppress enemy spawns, so turrets belong at
the frontier far from town and must still refill — make `ArmoryReachTiles` large or any-armory-on-network).

---

## 4. INTERMEDIARY VEHICLES — ROTORCRAFT (helicopter / osprey) + TANK

The ladder: **rotorcraft drones → helicopters / tanks → the piloted airship.** These are the mid tier:
bigger than a drone, genuinely mechanical, station-gated, ore-fed, still surplus-armed. Art note: rotorcraft
= ANGULAR faceted hulls + FLAT thin rotors (NOT smooth zeppelin); placeholder = a borrowed vanilla sprite.

### 4.1 Rotorcraft (Scout quad / Osprey tiltrotor / Chinook heavy) — AUTOMATED
Mechanically these are **DroneNPC with a bigger hull and more mounts** — the migrated drone, reskinned and
scaled, exactly as the ROADMAP's ladder says ("rotorcraft drones = the current 1.4.0 surplus system
reskinned"). Movement: hover + patrol (§2.4). Rotor art note the designer stressed: rotors are STATIONARY
visually (flat planes), the hull PIVOTS midair — a draw concern, not a movement-model concern. Mounts: Scout
1 weapon, Osprey 2, Chinook 2–3 (multi-mount loop, same as turret T3). Surplus-armed, ore-chassis-costed,
built at the Drone Assembly station. These are the low-risk extension of work item 1 — do them right after
the migration since they ARE the migrated drone with new constants + a bigger hull.

### 4.2 Helicopter — could be automated OR player-crewable
A heavier rotorcraft that can carry/crew. Two viable models (designer wants big vehicles pilotable too):
- **Automated gunship** — like a Chinook with more mounts + a longer patrol leash.
- **Troop-ferry / crewed** — Levy already shipped (in the HTML game, and as a concept) a transport-heli
  pattern; here the analogue is soldiers riding it. If crewed, share the airship's deck-rider mechanic
  (§5) at smaller scale. **Recommend: ship the automated gunship first; defer crewed heli until the airship
  deck mechanic is proven** (don't build the hard mount system twice).

### 4.3 Tank — ground vehicle (flagged NIGHTMARE by the designer)
The designer's own scope note: **"Tanks / mobile ground = genuine nightmare"** (Terraria terrain = 800 hills
+ 98% cave; momentum + dust won't save it). "LEAN STATIONARY; only do ground vehicles if a non-terrain-
fighting version appears." Honest recommendation to the lead:
- **Phase A (safe):** a **mobile emplacement** — a tank that moves only on flat/road tiles or very short
  hops, effectively a slow-repositioning turret. Reuses `TurretNPC` gravity + a constrained walk. Low risk,
  delivers the "tank" read without the pathfinding nightmare.
- **Phase B (risky, defer):** true tracked movement over terrain. Only attempt with a non-terrain-fighting
  movement model (e.g. the HTML game's amphibious-tracked "drive over water + climb slopes" exemption, or
  Levy's `TileNav`/`Pathfinder`/`FollowerTravel` which already move soldiers over terrain and could be
  reused rather than re-solved). **Do not let the tank block the ladder.**
All tanks: surplus-armed (ground weapons: launchers, rifles), ore-chassis, station-gated.

### 4.4 Crafting-station gating
Dedicated stations are themselves placeables (ModTile + optional TileEntity) that gate recipes by proximity
(`AddTile` / a "station nearby" recipe condition) and consume the ore sink:
- **Drone Assembly** — gates rotorcraft drones + T2/T3 turrets.
- **Motor Pool / War Foundry** — gates tanks + helicopters.
- **Airship Yard** — gates the airship (§5).
Chaining ore → station → machine is the designer's explicit "prehardmode-ore sink" loop.

---

## 5. THE AIRSHIP — endgame-prehardmode pilotable deck-carrier ("flying fortress of death")

The dream and the riskiest piece (scale / perf / pilot / entity-cap / pathfinding). Designer rulings: it is
**NOT scrapped — PROMOTED** to a distinct endgame-prehardmode SLOW giant covered in dozens of weapons; slow
is a *statement/commitment*, not a win-button; it must be genuinely BIG (dwarf a soldier, fill the screen);
station-gated (Airship Yard) + ore-fed; and it must **not block the rest of the ladder** — build it LAST.

### 5.1 Piloting (designer's steer: deploy + hold station + slow reposition, NOT twitch-pilot)
Recommended model: the airship is a **controllable NPC/vehicle entity**, not a vanilla mount. The player
deploys it, and it holds station; a **click-map / click-world "move here" order** slowly repositions it
(like the HTML game's capital ships and the designer's own "deploy + hold station + click-map to slow-
reposition" phrasing). This dodges the twitch-control + collision problems of a true player-piloted giant.
- **Alternative to weigh:** a real mount (`Player.mount` / a mounted vehicle) where the player drives
  directly. Richer but far harder (giant hitbox vs terrain, MP sync, the player becomes a single point of
  failure). **Recommend the controllable-NPC "slow waypoint" model first;** direct-pilot is a later nice-to-
  have, gated behind it working.

### 5.2 Soldiers standing / riding on the deck (the new, great idea)
- The airship exposes a **deck** = a set of platform tile positions (or an invisible solid platform region)
  that moves with the hull. Soldiers can stand on it and ride.
- Implementation options: (a) parent riding soldiers' positions to the deck each tick (teleport-follow,
  simplest, MP-syncable), or (b) a moving solid-platform collider the soldier brain treats as ground. (a) is
  the pragmatic first cut. A soldier assigned to the airship (via a Barracks "rebase" / a deck-crew order)
  rides it and crews its weapons.
- Crewed soldiers keep fighting with their own weapons from the deck (fire-from-platform), giving the
  "manned fortress" read. Carried/riding soldiers must be excluded from ground-combat movement paths while
  aboard (there is precedent: the transport-heli pattern "carried units excluded from all combat paths,
  safe emergency-drop on death").

### 5.3 Weapons draw from surplus (the whole point, at scale)
The airship mounts **dozens of weapon hardpoints**, each an independent `DroneShot` picking the best surplus
weapon from the networked Armory (the turret multi-mount loop, scaled way up). This is literally "turn your
90 duplicate weapons into a flying fortress" — the Armory surplus becomes the airship's broadside. Mounts
have independent targets + cooldowns + ceilings (anti-air vs ground). Expect this to be the perf-sensitive
part: throttle target-scan cadence per mount, reuse the proven per-mount `_tcd[]` pattern from the HTML
game's multi-turret engine as the mental model.

### 5.4 The fantasy
A slow, screen-filling, ore-gorged hull bristling with your entire scavenged arsenal, crewed by your own
levies standing on its deck — a statement that your whole society has been fed into one war machine. Tenet
#1: no cap on how many you field; the limiter is emergent (ore + Airship Yard + build time + the sheer
upkeep of crewing it), never a hard wall.

---

## 6. PHASED BUILD ORDER (small shippable slices, each a clean headless boot, easiest → hardest)

Each slice ends with: headless boot of BOTH mods, zero exceptions, load-time log lines; then the designer's
live run is the real test. Files named are relative to `mechanization-mod/Mechanization/` unless under
`levy-mod/Levy/`.

0. **MP-client turret placement** (already "next" in the ROADMAP; unblocks everyone). Add a spawn packet so
   a multiplayer client can place a turret. Touches: `Content/Items/TurretItem.cs`, a new net packet,
   `TurretSystem`. Small, isolated, no Levy change.

1. **Turret QoL batch** (designer request flood, high value, low risk). Dismantle (recover turret + show its
   mounted weapon), in-Armory-range indicator, and MUCH bigger / network-wide Armory reach. Touches:
   `TurretNPC`, `TurretSystem`, `MechConstants` (`ArmoryReachTiles`), maybe a small UI. No Levy change.

2. **Turret TIERS T2/T3** (§3). Multi-mount loop (the reusable mechanic for everything above), `TurretRecord`
   gains a tier id, per-tier `MechConstants`. Touches: `TurretNPC` (mount loop), `TurretSystem` (save tier),
   `TurretItem` (per-tier items/recipes). No Levy change (uses existing public `DroneArsenal`).

3. **Crafting stations + ore-chassis cost** (§2.5, §4.4). Drone Assembly station first (ModTile + recipe
   gating), ore cost wired into turret recipes. Resolves the designer's open ore decisions as it lands.
   Touches: new `Content/Tiles/*Station*`, recipes. No Levy change. *(Depends on designer answering §7.)*

4. **DRONE MIGRATION** (§2.1–2.3) — move `DroneNPC`/`DroneSystem`/Engineering out of Levy into Mechanization;
   make the audited Levy types public; revert Levy to pure soldiers; re-verify the lend-ledger conservation
   invariants cross-mod. Touches: Levy (`Content/NPCs/DroneNPC.cs`, `DroneSystem.cs`,
   `Common/LevyConstants.Drones.cs`, `ArmoryTE`, `SoldierProjectileTag`, `ThrownBlastGuard`, `GuideSystem`,
   visibility bumps) + Mechanization (new `Content/NPCs/DroneNPC.cs` etc., `MechConstants.Drone`). Biggest
   cross-mod slice; do it on its own so a regression is easy to localize. **Keep `DroneArsenal` in Levy.**

5. **DRONE PATROL behavior** (§2.4) — resolve patrol pair via `UIActions.FindPatrolPair`, add the flying leg
   state machine + engagement leash, fallback to hover when no banners. Touches: Mechanization `DroneNPC`,
   `DroneSystem`, `MechConstants.Drone`. (Watch the live militia-patrol bug — keep this self-contained.)

6. **ROTORCRAFT drones** (§4.1) — Scout/Osprey/Chinook = the migrated drone, bigger hull + more mounts + new
   constants, built at Drone Assembly. Touches: Mechanization NPC(s), items, constants. Low risk once 4+5
   are in.

7. **Automated helicopter / gunship** (§4.2 first model). Heavier rotorcraft, longer leash, more mounts.

8. **Tank — Phase A mobile emplacement** (§4.3). Constrained movement only; lean stationary. Defer true
   tracked terrain movement (Phase B) indefinitely unless a clean model appears.

9. **AIRSHIP — Phase 1: deploy + hold-station + slow waypoint + multi-mount surplus broadside** (§5.1, §5.3).
   No deck riders yet. Prove the big-entity + perf + waypoint-move first. Airship Yard station gates it.

10. **AIRSHIP — Phase 2: deck + soldier riders crewing weapons** (§5.2). The hard new mount system; builds on
    a working hull. Optionally backport the deck mechanic to a crewed helicopter after.

11. **(Later / optional) direct player-pilot** for the airship; summoner/sentry turrets; T4 turret specials;
    hostile-drones mode (the parked "Program 7" idea).

---

## 7. OPEN DESIGN QUESTIONS (for lead / designer)

1. **Ore cost shape** (blocking slice 3): one-time build cost [plan's lean] vs ongoing supply/upkeep drain?
   Does ore ADD to the weapon-lend model (weapon = armament + ore = chassis) or REPLACE it for some units?
   Exact ores/amounts (~5–10 prehardmode bars/drone, more for emplacements/vehicles — all tunable)?
   And the unclear "maybe give them to your soldiers" — do soldiers consume ore for upgrades / deploy
   emplacements, or does ore buff soldiers?
2. **Engineering unlock's new home** (blocking slice 4): keep the Guide-to-Engineering item (and eat the
   save-compat hit), or replace the unlock with "Drone Assembly station exists"? If the station gates
   everything anyway, the item may be redundant.
3. **Patrol semantics for drones** (slice 5): pure back-and-forth between two banners (soldier `ThinkPatrol`
   model), or SearchDestroy-anchored-to-the-route ("actively attack" leaning)? What leash radius off the
   route before a chasing drone turns back?
4. **Should drone patrol wait on the live militia-patrol bug fix**, or ship self-contained and fix militia
   separately?
5. **Airship piloting model** (slice 9): controllable-NPC slow-waypoint [plan's lean] vs real player mount?
   Is "hold station + click-to-move" enough "piloting" for the designer, or does she want hands-on drive?
6. **Deck-rider assignment** (slice 10): how does a soldier get aboard — a Barracks "rebase to airship"
   order, a deck-crew order in the roster UI, or auto-board the nearest N when the airship deploys?
7. **Tank ambition** (slice 8): is a slow mobile-emplacement "tank" acceptable, or does the designer insist
   on true tracked terrain traversal (and accept the nightmare / possible indefinite defer)?
8. **Submod name** — still open ("Levy: Mechanized / Motor Pool / War Machines" floated; mod currently ships
   as `Mechanization`). Designer's pick; affects display name, not code structure.
9. **Multi-mount weapon selection fairness** — with a 20-hardpoint airship pulling from one Armory, does it
   grab the 20 best guns (starving drones/soldiers), or is there a reserve split? (Surplus-first ordering
   like the drone `ArmoryReserve` soft reserve probably applies, scaled up.)

---

*Baseline of record: `worktree-levy-stardust` — Levy 1.4.4 + Mechanization 0.3.0. The public
`EnemyTargeting.Bodies` + the `ITargetBody` `LiveTarget` branch are the generic cross-mod seams already in
Levy; `DroneArsenal`/`WeaponClassifier`/`ArmoryTE` stay in Levy as the shared weapon-mount pipeline both
mods use.*

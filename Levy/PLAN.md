# Levy — PLAN.md (architecture, file ownership, contracts, verification)

Working title **Levy**. A tModLoader content mod: your junk arms a militia; paid Regulars follow you; a War Horn calls the town out
to hold the walls. This file is the build plan AND the brief for every implementing agent. The design spec (Build Spec v0.1)
is the source of truth for numbers and behaviour; `Common/LevyConstants.cs` holds every tunable number.

## 0. Toolchain facts (verified on this machine, 2026-10-14)

| Thing | Value |
|---|---|
| Target | **tModLoader 1.4.4.9, release v2026.08.3.0 (Steam `stable` branch)**, Terraria 1.4.4.9 |
| Runtime / language | .NET 8, C# 12 (`LangVersion 12.0`), define `TML_2026_08`, `JETBRAINS_ANNOTATIONS` |
| Not targeted | tModLoader 1.4.5 is still the *preview* line (migration guide says "once 1.4.5 is stable"); it changes .NET to 10 and breaks `Item.NewItem`, projectile draw, town chat. Porting notes live in `/home/user/tmodloader/tmodloader/MigrationGuide_1.4.5.md`. |
| Real binaries | `/home/user/tml-bin/tml/tModLoader.dll`, `Libraries/**` (FNA, ReLogic, TerrariaHooks = `On_`/`IL_` hooks), `tModLoader.xml` (API docs) |
| Decompiled vanilla+TML source (grep oracle) | `/home/user/tml-src/Terraria/*.cs` (NPC.cs, Player.cs, Projectile.cs, Main.cs, ID/*.cs, GameContent/**) |
| TML API source with doc comments | `/home/user/tmodloader/tml-1.4.4/patches/tModLoader/Terraria/ModLoader/*.cs` |
| ExampleMod (1.4.4) | `/home/user/tmodloader/tml-1.4.4/ExampleMod` (tiles, tile entities, UI, emotes, keybinds, packets, hjson) |
| Reference mod | `/home/user/refmods/TerraGuardians` (companions drawn with the player renderer; `TerraGuardian.cs` reimplements ItemCheck_Shoot / melee hitboxes; `Companion.cs` swaps `Main.player[]` slots) |
| **Compile check** | `/home/user/tools/compile.sh /path/to/levy` — Roslyn against the real assemblies, ~2 s. **Zero errors required before any agent returns.** |
| **Real build** | `/home/user/tools/tmlbuild.sh /path/to/levy` — tModLoader's own headless `-build`; produces `~/.local/share/Terraria/tModLoader/Mods/Levy.tmod`. Run at integration. |

Vanilla facts that shape the design (all read from the decompiled source, not from memory):

* `Main.player` has 256 entries; `Main.maxPlayers == 255`; **slot 255 is the vanilla "null player" and the `target == 255` sentinel for "no target"**.
* `NPC.TargetClosestUpgraded` only ever targets players, tank pets and NPC type 548 (Eternia Crystal). The `target >= 300` NPC-target scheme (`GetTargetData`, `HasNPCTarget`, `WhoAmIToTargettingIndex`) is honoured only by AIs that call `GetTargetData()` (Old One's Army enemies, Golem, Deerclops, Betsy…). **`AI_003_Fighters` reads `Main.player[target]` 97 times and never calls `GetTargetData`; Slimes 29/0; Demon eyes 6/0.** So the only general way to make zombies/goblins/slimes/eyes chase a soldier is the **dummy-player slot swap** (see §5.6).
* Hostile projectiles already damage `friendly` NPCs that do not have `dontTakeDamageFromHostiles` (Projectile.Damage, "flag10"). So a soldier body with `NPC.friendly = true` is hit by enemy projectiles for free. Enemy *contact* damage to friendly NPCs is NOT general vanilla behaviour for custom AI; the body implements it (§5.4).
* `NPC.HitModifiers.DefenseEffectiveness` defaults to 0.5. Player defense effectiveness per mode comes from `Player.VanillaBaseDefenseEffectiveness()` (Classic 0.5 / Expert 0.75 / Master 1.0). Soldiers set it in `ModifyIncomingHit`.
* `Main.PlayerRenderer.DrawPlayer(Camera, Player, Vector2 position, float rotation, Vector2 rotationOrigin, float shadow, float scale)` and `Player.PlayerFrame()` read **nothing** from `Main.player[]` / `whoAmI` (checked `PlayerDrawLayers.cs`, `PlayerDrawSet.cs`, `LegacyPlayerRenderer.cs`). A free-standing `Player` instance can be drawn directly.
* `Projectile.npcProj = true` skips the owner's on-hit effects (`flag2 = !npcProj && !trap`) and `Projectile.GetOwner()` returns null; `Projectile.friendly` projectiles hit hostile NPCs regardless of owner. Town-NPC attacks use exactly this (`npcProj = true; noDropItem = true`).
* `NPC.SimpleStrikeNPC(int damage, int hitDirection, bool crit, float knockBack, DamageClass, bool damageVariation, float luck, bool noPlayerInteraction)` exists for melee hits.
* Loot credit: `npc.playerInteraction[player]` + `npc.lastInteraction` decide vanilla drop credit; `NPC.NPCLoot` picks a player via those.

## 1. Pillars and locked decisions

See the spec. Short form: mass over quality; dead inventory gets a job; they die like players and it costs you; cinema first;
vanilla rules; fun to have around. Locked: soldier = ModNPC drawn with the player renderer; weapon families guns/bows/magic/
broadsword/shortsword/spear; ammo crafted+consumed from issue slots; loot credit to commander; boss rule (bosses chase the
player, spite targets, HP +2 %/CP cap +80 %); fear only during main-boss fights; two kinds (Regulars/Militia); grades gated
late; accessory slots are the power knob; vanilla invasions; render cap 40; no worldgen.

## 2. Layout (all paths relative to `Levy/` — the folder must be named `Levy`; tModLoader names the mod after it)

```
Levy.csproj  build.txt  description.txt  icon.png  Levy.cs
PLAN.md  TESTING.md  README.md
Localization/en-US_Mods.Levy.hjson            every UI/item/tile/config/chat string
Dialogue/en-US.txt                            TRIGGER | Temperament|Any|Veteran | line   (designer-editable, no code)
Common/LevyConstants.cs                       every tunable number (spec placeholders)
Common/Enums.cs                               Grade, Temperament, SoldierKind, SoldierState, SoldierOrder, WeaponFamily, DialogueTrigger, TombstoneMode…
Common/Config/LevyConfig.cs                   ModConfig (ServerSide)
Common/Util/LevyUtil.cs                       chat, coins, tiles, net helpers
Common/Net/LevyPackets.cs                     packet ids + dispatch
Common/Soldiers/SoldierGear.cs                Item slots (weapon, sidearm, head, body, legs, 5 accessories, 8 dyes, ammo pouch)
Common/Soldiers/SoldierRecord.cs              the persistent roster entry + TagCompound IO
Common/Systems/RosterSystem.cs                ModSystem: roster, CP, tier, dawn tick, payday, desertion, respawn queue, body deployment, orders
Common/Systems/SettlementSystem.cs            ModSystem: barracks enumeration, town-NPC membership, manpower, militia muster/stand-down/reinforcements, auto-muster
Common/Systems/EconomySystem.cs               wages, fees, treasury ops, coin formatting
Common/Weapons/WeaponClassifier.cs            family detection + WeaponProfile cache + multishot table
Common/Weapons/Aiming.cs                      intercept solve (2 iterations), ballistic solve, LOS, aim error
Common/Weapons/WeaponUse.cs                   per-family fire/swing behaviour (called by SoldierNPC.Combat)
Common/Targeting/SoldierTargeting.cs          soldier→enemy choice, never-target list, flee list
Common/Targeting/EnemyTargeting.cs            enemy→soldier (On_NPC.TargetClosest detour, allowlist, slot-255 swap GlobalNPC)
Common/Targeting/LootCredit.cs                GlobalProjectile/GlobalNPC: soldier projectile flag, playerInteraction, no player procs
Common/Boss/BossRule.cs  Common/Boss/TerrorTable.cs   spite targets, projectile redirect, HP scaling, dash allowlist, terror values
Common/Fear/FearSystem.cs                     squad-wide fear events, rally, trophies
Common/Movement/MovementAbilities.cs          abilities read from gear
Common/Movement/Pathfinder.cs                 coarse tile-grid search with jump/flight edges
Common/Movement/FollowerTravel.cs             pylon / mirror / recall / teleporter travel with the commander; off-screen teleport
Common/Dialogue/DialogueSystem.cs             table loader, triggers, placeholders, bark budget, channels
Common/UI/UISystem.cs  BarracksPanel.cs  ArmoryPanel.cs  OrdersRadial.cs  Nameplates.cs  HudStrip.cs  SoldierMapLayer.cs
Common/Globals/LevyGlobalNPC.cs               boss spawn/death, Guide hint line, trophies, Nurse
Common/Globals/LevyPlayer.cs                  ModPlayer: commander hotkeys, death → followers go home, travel detection
Content/Items/Placeables/BarracksItem.cs ArmoryItem.cs PatrolBannerItem.cs (+ .png)
Content/Items/WarHorn.cs (+png)   Content/Items/Books/GuideTo{Conscription,Drill,Plunder,Fortification,Discipline}.cs (+png)
Content/Tiles/BarracksTile.cs ArmoryTile.cs PatrolBannerTile.cs (+png)
Content/TileEntities/BarracksTE.cs ArmoryTE.cs PatrolBannerTE.cs
Content/NPCs/SoldierNPC.cs (core)  .Brain.cs  .Combat.cs  .Movement.cs  .Draw.cs  .Fear.cs  .Damage.cs  SoldierNPC.png
Content/Projectiles/SoldierThrust.cs (+png)   shared spear/shortsword thrust
```
`levy-tools/` (repo root, outside the mod): `gen_sprites.py` (placeholder art), `compile.sh`, `tmlbuild.sh` copies.

## 3. Core data

**SoldierRecord** (persisted in world data for Regulars; transient for Militia): `Id`, `Name`, `Kind`, `Grade`, `Temperament`,
`LifeCrystals`, `LifeFruit`, `ManaCrystals`, `CurrentHp`, `Mana`, `Gear` (SoldierGear), `HomeBarracksId` (tile-entity ID) +
`HomePos` (Point16 top-left), `CommanderName` (player name; Regulars belong to players), `Veteran`, `BossFightsSurvived`,
`Kills`, `DaysUnpaid`, `StoodDown`, `Order`, `HoldFire`, `HoldPos`, `PatrolA/PatrolB` (Point16 of banner tiles), visual
fields (hair, skin variant, colours). Runtime only: `BodyIndex` (NPC whoAmI or -1), `RespawnTimer`, `Dead`, `EngagedBosses`
(set of boss whoAmI the soldier damaged this fight), `Fear`.

`MaxHp = min(100 + 20*LifeCrystals + 5*LifeFruit, grade cap)` (fruit only counts for Champions). `MaxMana = min(20 + 20*ManaCrystals, 200)`.

**Settlement** = a `BarracksTE` (ModTileEntity): `Name`, `Treasury` (copper, long), `Manpower`, `AutoMuster`, `Mobilized`,
`TaxToTreasury`, `RecruitingDriveDay`, `HazardPayDay`; runtime: `Militia` (List<SoldierRecord>), `ReinforcementsPending`,
`MusterTimer`. Town NPCs whose `homeTileX/Y` are within 120×70 tiles belong to the nearest barracks; pets, Old Man (37),
Travelling Merchant (368), bound NPCs excluded; `townNPC && !IsTownPet`. Minimum 2 to raise militia.

**ArmoryTE**: `Storage[120]`, `IssueWeapons[6]` (slot 5 = sidearm), `IssueArmor[9]` (3 sets × head/body/legs),
`IssueAmmo[4]`, `IssuePotions[2]` (0 healing, 1 buff). Issue slots first, bulk storage second, best-first by damage within
a family the soldier can use. Barracks links to the nearest armory within 20 tiles (several allowed).

**Command points** are computed every time they are read from `NPC.downed*` flags (never stored). **Tier** likewise.

## 4. Soldier body — the integration point (`Content/NPCs/SoldierNPC.cs`, partial class)

* `SetStaticDefaults`: `Main.npcFrameCount = 1`, `NPCID.Sets.ActsLikeTownNPC[Type] = true` (so vanilla treats it as a
  friendly town-like NPC for immunity to the player and for hostile projectiles), `NPCID.Sets.NoTownNPCHappiness`, not in
  the bestiary (`NPCID.Sets.NPCBestiaryDrawOffset` hide), `CantTakeLunchMoney`, `TownCritter=false`.
* `SetDefaults`: 20×42 hitbox, `friendly = true`, `townNPC = false`, `aiStyle = -1`, `knockBackResist = 0`, `damage = 0`,
  `lifeMax = 100`, `npcSlots = 1`, `HitSound = SoundID.PlayerHit`, `DeathSound = SoundID.PlayerKilled`, `lavaImmune = false`,
  `noGravity = false`, `dontTakeDamageFromHostiles = false`, `immortal = false`, `rarity = 0`.
* **`Dummy`** — one free-standing `Player` per body (`new Player()`, `whoAmI = 255`, `active = true`, `dead = false`). It is
  the renderer model AND the stat engine: every tick copy `position/velocity/direction/gravDir`, equip `Gear` into
  `Dummy.armor[0..2]` (+ `[3..7]` accessories, visible), `Dummy.dye[]`, `Dummy.inventory[0] = Weapon; selectedItem = 0`, set
  `itemAnimation/itemAnimationMax/itemRotation/itemLocation` from Combat, `controlLeft/Right/Jump` from Movement for leg
  animation, then `Dummy.PlayerFrame()`. Stats: once per 60 ticks or on gear change, `Dummy.ResetEffects(); Dummy.UpdateEquips(255); Dummy.UpdateArmorSets(255)`
  (read the decompiled bodies first for side effects; wrap in try/catch; config toggle `UseVanillaStatPipeline`; manual
  fallback = sum of `item.defense`, weapon damage only). Read `statDefense`, `GetWeaponDamage(item)`, `GetWeaponCrit`,
  `moveSpeed/accRunSpeed/maxRunSpeed`, `jumpSpeedBoost`, extra jumps, `wingsLogic/wingTimeMax`, `noFallDmg`, ammo-conservation flags.
  **Never** put the dummy into `Main.player[]` except inside `EnemyTargeting`'s PreAI/PostAI swap (§5.6).
* `AI()` (runs on server + single player; clients only animate): bind record → `Damage_Update()` (immunity timers, contact
  damage, death) → every 6 ticks staggered by `whoAmI % 6`: `Brain_Think()` → every tick: `Fear_Update()`, `Movement_Update()`,
  `Combat_Update()` → `Dummy` sync → `NPC.netUpdate` when state/target/gear changed.
* `ModifyIncomingHit`: `modifiers.DefenseEffectiveness *= LevyUtil.ModeDefenseEffectiveness() / 0.5f`; `NPC.defense` is the
  gear defense; Shaken does not change incoming damage.
* `PreDraw` → `Draw_PreDraw` draws `Dummy` via `Main.PlayerRenderer.DrawPlayer(Main.Camera, Dummy, Dummy.position, Dummy.fullRotation, Dummy.fullRotationOrigin, 0f, 1f)`
  and returns false. Orange tint while Shaken (`Dummy` has no tint; draw a second pass with `Main.spriteBatch` colour? no —
  use `PlayerDrawSet`'s `colorArmorBody`? keep v1 simple: draw an orange `TextureAssets.MagicPixel` glow behind + emote).
* `PostDraw` → nameplate hook (`Nameplates.Draw`).
* `CheckDead/OnKill`: route to `RosterSystem.OnBodyDied` (server). `PreKill` returns false (no vanilla loot).
* `CanChat/GetChat`: idle dialogue; `SetChatButtons`: "Hand ammo" when the player holds ammo, "Orders".
* Partial methods (each owned by one agent, implemented in its own file): `Brain_Think`, `Movement_Update`, `Combat_Update`,
  `Fear_Update`, `Damage_Update`, `Draw_PreDraw`, `Draw_PostDraw`, `Brain_OnOrder`, `Combat_OnGearChanged`,
  `Fear_OnDamaged`, `Movement_OnTeleported`. Classic `partial void` so an unimplemented part compiles to nothing; the
  integration step greps that every one is implemented.

## 5. Module notes (what each agent must get right)

### 5.1 Tiles / tile entities / items
Barracks 3×3 (`TileObjectData.Style3x3`, solid-top? no: furniture-like, `LavaDeath=false`), Armory 2×3, Patrol Banner 1×3
hanging banner (anchor top). Recipes at Work Bench: Barracks 30 wood (any wood via `RecipeGroupID.Wood`), 10 iron/lead (`IronBar` recipe group), 1
banner of any kind (`RecipeGroup` built from all `Item.makeNPC == 0 && ItemID.Sets` banner items — simplest: items whose
`createTile == TileID.Banners`); Armory 20 iron/lead + 1 chest. War Horn: 5 any wood + 1 iron/lead at Work Bench. Patrol Banner: 1 silk + 2 wood.
Event books: `consumable`, once per world; drops added in `LevyGlobalNPC.ModifyNPCLoot` with `Conditions` for the event (1/50 from
event enemies: Blood Moon, Goblin Army, Pirates, Old One's Army, Eclipse).
Right-click on the Barracks tile opens `UISystem.OpenBarracks`; on the Armory opens `UISystem.OpenArmory`. Both TEs save/load
items with `ItemIO` and sync with `NetSend/NetReceive`. `OnKill` of the Barracks TE: stand down militia, refund nothing,
regulars re-base to the nearest other barracks (or become homeless: follow the commander).

### 5.2 Roster / Settlement / Economy
* Dawn = `Main.dayTime` false→true transition observed in `PostUpdateTime` (server). Order: manpower regen → payday → desertion
  rolls → reset daily flags → chat lines ("Paid the garrison: 1 gold 20 silver.").
* Payday: sum wages of regulars per home barracks (half if StoodDown); pay from that barracks' treasury; shortfall ⇒ all its
  regulars `DaysUnpaid++`, else reset to 0. Desertion chance 0/0/25 %/50 % by DaysUnpaid ≥ 2/3: deserter walks off (body
  flees to the map edge then despawns) with gear, chat line, removed from roster.
* Enlist: pay muster fee (5 days' wage of the grade) from the treasury; CP check; roll name (`NPC.getNewNPCName` of a random
  town type, prefixed "Pvt." … Veteran "Sgt."), temperament, looks; auto-issue gear from the linked armory.
* Militia muster: `Mobilize(barracks)`: count = min(manpower, cap − regulars present…), stream bodies out of the barracks
  muster point every 15–30 ticks (5–10 s total), each paying L/5 per head from the treasury (free if broke — never block a
  defence on money; log it); militia grab gear from issue slots; patrol the settlement edges (±120 tiles) or engage.
  `StandDown`: bodies walk home and despawn, gear returns to the armory, pool untouched.
* Reinforcements: when a militia body dies and manpower > 0, the next walks out after 2 s. When manpower hits 0: chat
  "{Town} has no one left to send." once per event.
* Auto-muster (config, default on): on invasion start, Blood Moon, Eclipse, Pumpkin/Frost Moon, Old One's Army, Martian; stand
  down when the event ends.
* Render cap: `RosterSystem` only spawns bodies while `ActiveBodies().Count < RenderCap`; priorities: regulars following >
  militia reinforcements > idle regulars.
* Regular respawn: 60 s after death if `Manpower > 0` at the home barracks (−1 on death already taken); walks out of the
  barracks, re-arms from the armory, resumes its last order. Hardcore commander ⇒ record moves to `Memorial`.
* Recruiting drive: pay 2L per point to refill instantly, once per day. Hazard pay: one day's wage for everyone deployed,
  fear gain ×0.75 for the next main-boss fight.
* CP: base 4 + table; used = Σ grade cost of regulars (alive, dead-awaiting-respawn, stood down).
* Multiplayer: all of the above runs where `Main.netMode != NetmodeID.MultiplayerClient`; UI sends `LevyPackets`.

### 5.3 Brain / orders / movement
States: Idle, Follow, Patrol, Engage, Haul, Resupply, Shaken, Routed, ReturnHome, Dead. Idle = wander near the muster point
(simple: walk a few tiles, pause, face random; sit not required). Follow: stay within 12 tiles behind the commander in a loose
cluster (slot offset by roster index); teleport only if off-screen and > 60 tiles. Patrol: walk between PatrolA/PatrolB,
pause 2 s at each end. Engage: ranged keep 8–16 tiles, strafe; melee close to 3 tiles, swing, step back half a tile after a
hit. Hold: engage in place, never chase > 15 tiles. Haul: no enemy within 25 tiles → pick up items within 6 tiles (never within
8 tiles of any player, never hearts/stars/boss bags/`ItemID.Sets.NebulaPickup`), carry to the linked armory (coins → treasury).
Resupply: pouch empty → nearest armory within 40 tiles; none → sidearm; none → Follow. Routed: run to the barracks, ignore
enemies, rally per §5.8. ReturnHome: walk to barracks → Idle (StoodDown). Orders apply to regulars within 40 tiles of the
commander, or to all of the commander's regulars when given at the Barracks panel. Thinking every 6 ticks staggered.

Movement: abilities from gear (`MovementAbilities`): walk speed, jump height, extra jumps, wings (flight time + glide).
Reflexes every tick: jump when blocked (`Collision.SolidCollision` ahead), drop through platforms toward a lower goal
(`NPC.CanFallThroughPlatforms` → return true while descending), open doors like town NPCs (`WorldGen.OpenDoor`/`AutoDoor`
pattern from AI_007), refuse lava (`Collision.LavaCollision`) and drops deeper than the jump can recover (unless winged),
stuck ≥ 2 s → turn around / jump / after 6 s teleport if following. Pathfinding only for Follow and Haul: coarse search over
tiles (walkable = solid-or-platform floor with 3 air tiles above), edges = step, jump (≤ jump height), fall (≤ 20 tiles),
flight (if wings), recomputed every 30 ticks or when the goal moves 10 tiles. Soldiers don't grapple, mine, place, ride or rope.
Followers travel with the commander: hook `On_Player.Teleport`, `On_Player.Spawn` (recall/mirror/respawn) and pylon use —
move following regulars next to the commander (off-screen teleport rules: never into lava/water, never through dungeon walls:
find a free floor within 20 tiles of the commander). Militia never travel. Soldiers never collide with players or each other
(`NPC.noTileCollide` stays false; just no NPC-NPC collision by default).

### 5.4 Combat / weapons / ammo / damage intake
Families from item fields at load (`WeaponClassifier.Classify`): Gun (`useAmmo == AmmoID.Bullet`), Bow (`useAmmo == AmmoID.Arrow`),
Magic (`mana > 0 && shoot > 0 && !channel && !summon`), Broadsword (`melee && noMelee == false && shoot == 0 || shoot is a sword beam` and
`useStyle == Swing && !ItemID.Sets.Spears`… ), Shortsword (`ItemID.Sets.Spears`? no — `useStyle == ItemUseStyleID.Rapier` or
`shoot` is a shortsword stab projectile `ProjectileID.Sets.IsAShortsword`), Spear (`shoot` projectile with `aiStyle == ProjAIStyleID.Spear`).
Everything else (yoyo `ItemID.Sets.Yoyo`, flails, boomerangs, whips (`ProjAIStyleID.Whip`), summons, channel beams, tools
`pick/axe/hammer > 0`, consumables `consumable`, bombs) = Unsupported: never issued, never equipped, stays in storage.
`WeaponProfile` caches: family, useTime, useAnimation, reuseDelay, shoot, shootSpeed, damage, knockback, crit, ammo type,
range (guns 40 tiles, bows 30, magic 25, melee 3), multishot (count, spread) from `LevyConstants.Multishot`, gravity
(`ProjectileID.Sets` or `ProjAIStyleID.Arrow`), homing (`ProjAIStyleID` for chlorophyte etc. → `ProjectileID.Sets` is not exact; use a
small table + `aiStyle == 1 && ai[?]`… keep simple: chlorophyte/jester/crystal bullets listed in constants).
Firing: cadence = `useTime` (+ `reuseDelay`) ticks as if the trigger were held; multishot spawns N projectiles with the spread;
ammo: one per shot unless the conservation chance fires (`Dummy.ammoCost80 / ammoCost75 / huntressAmmoCost90 / chloroAmmoCost80`);
projectile = ammo's `shoot` (guns/bows) or the item's `shoot` (magic/sword beams); damage = `Dummy.GetWeaponDamage(item)` +
ammo damage, × grade multiplier, × config, × 0.6 if Shaken; knockback sum; speed = item.shootSpeed + ammo.shootSpeed.
Spawn with `Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel, type, dmg, kb, owner: commanderWhoAmI or Main.myPlayer)`,
then `proj.npcProj = true; proj.noDropItem = true; proj.friendly = true; proj.hostile = false` and tag the `LootCredit`
GlobalProjectile instance with the soldier index (sync with SendExtraAI if needed). Mana: magic costs `item.mana`, regen like a
player (`manaRegen` ramp simplified: +1 per 10 ticks idle, +1 per 20 ticks in combat); empty → sidearm.
Aiming (`Aiming.SolveIntercept`): two iterations of t = |target − muzzle| / speed, aim at `target.Center + target.velocity * t`;
ballistic: add `0.5 * g * t²` upward where g is the projectile's gravity (arrows 0.1/tick after 15 ticks … use
`LevyConstants.ArrowGravity` and aim high); homing ammo skips the lead; no shot without LOS (`Collision.CanHitLine`) unless the
projectile `tileCollide == false`. Aim error: grade jitter ± deg and ± lead fraction, ×0.6 veteran, ×2 Shaken, ×0.75 militia with Drill.
Multi-part enemies: aim at the nearest segment (same `realLife`) that can take damage.
Melee (Broadsword): swing on cooldown = useAnimation; hitbox like the player's (`Player.GetItemDrawFrame` sized box in front, rotated
through the arc — borrow TerraGuardians' `ItemCheck_GetMeleeHitbox` approach); hit via `target.SimpleStrikeNPC(dmg, dir, crit, kb, DamageClass.Melee)`
+ loot-credit marks; sword beams spawn the item's `shoot`. Spear/Shortsword: one shared `SoldierThrust` ModProjectile owned by
the commander (`npcProj`), ai[0] = soldier whoAmI, ai[1] = item type; copies the item texture, reach = `shootSpeed`-based, damage,
retracts after useAnimation; anchored to the body every tick.
Damage intake (`Damage_Update`): hostile NPC contact — for every hostile, active, `damage > 0` NPC intersecting the hitbox while
`immuneTime == 0` → `NPC.SimpleStrikeNPC(npcDamage, dir, false, knockback 0, noPlayerInteraction: true)` with the player defense
formula via `ModifyIncomingHit`, set `immuneTime = 20`. Hostile projectiles are handled by vanilla (friendly NPC rule) — do not
double-apply. Regen: slow natural regen out of combat (1 HP / 60 ticks after 5 s without damage); drink the healing issue
potion below 50 % with potion sickness (30 s). Knockback none.

### 5.5 Soldier targeting / loot credit
`SoldierTargeting.PickTarget(s, range)`: within engagement range with LOS; priority 1 = any hostile currently hurting a comrade
or a player (track last attacker on bodies via OnHit hooks and on players via `LevyPlayer.OnHurt`), priority 2 = nearest.
Never: `!npc.active`, `dontTakeDamage`, `immortal`, `friendly`, `townNPC`, `CountsAsACritter`, Eternia Crystal (548), target
dummy (488), chest Mimics still disguised (`NPCID.Mimic` with `ai[0] == 0` and `ai[?]` hidden — check `NPC.AI_` for mimics: they are
`dontTakeDamage` while hidden? verify), Mysterious Tablet (`CultistTablet`), `CultistDevote`/`CultistArcherBlue/White`, Old Man (37),
bound NPCs (`NPCID.Sets.BoundNPC`? use `ai` / `type` list: BoundGoblin 105, BoundWizard 106, BoundMechanic 123, SleepingAngler 369,
BartenderUnconscious 579, GolferRescue 639, WebbedStylist 354 — check IDs in `NPCID.cs`), Plantera hooks (264/265 — `PlanterasHook`,
`PlanterasTentacle`). Flee: Dungeon Guardian (68): run away ≥ 60 tiles, never shoot.
`LootCredit`: on any soldier-caused hit `npc.playerInteraction[commander] = true; npc.lastInteraction = commander`; soldier
projectiles are `npcProj` so player procs never fire; `Projectile.GetOwner()` is null for them — anything that reads
`Main.player[proj.owner]` still gets the commander, which is what we want for loot.

### 5.6 Enemy → soldier targeting (the slot-255 swap)
Allowlist by AI style, config-extendable: Fighters (3), Flying (2 demon eyes), Bats (14), Slimes (1), Casters (8), Hornets (22),
Jellyfish (18), Spiders (… `NPCAIStyleID.Spider` wall-crawl), Flying Fish, Vulture, Archers/gunners (fighter AI with
`NPCID.Sets`…), invasion rosters (goblins, pirates, martians use fighter/flyer/caster AIs), non-boss worms (6) — grow by testing;
bosses (`boss` or `NPCID.Sets.ShouldBeCountedAsBoss`) and their `realLife` parts are never allowlisted (boss rule instead).
Mechanism: `LevyGlobalNPC` (instance per NPC via `InstancePerEntity`) holds `SoldierTarget` (NPC whoAmI or −1) and `Retarget` timer.
Detour `On_NPC.TargetClosest` (and `TargetClosestUpgraded`) in `EnemyTargeting.Load`: call `orig`; if the NPC is allowlisted and
config on: find the nearest live soldier body within the same search radius vanilla uses (compare to the chosen player's
distance; respect `player.aggro`: subtract the player's aggro from its distance exactly like vanilla; soldiers have aggro 0);
if the soldier wins → `npc.target = 255`, `npc.targetRect = soldier hitbox`, set `direction/directionY` when `faceTarget`,
store `SoldierTarget`. In `PreAI(npc)`: if `SoldierTarget` is live → `_backup = Main.player[255]; Main.player[255] = soldier.Dummy`
(`Dummy.active = true, dead = false, ghost = false, position/velocity/Center` already synced) and return true. In `PostAI`
(always, even when PreAI returned false earlier) restore `Main.player[255] = _backup`. The vanilla sentinel check
`target == 255 → TargetClosest()` re-enters the detour every tick, which keeps the choice fresh. Never leave the dummy in the
array across the AI call; wrap with try/finally. Clients: the swap also runs client-side so visuals match (NPC AI runs on
clients for prediction) — it is harmless there.
Known hazards to test and note in TESTING.md: worms switching segments, enemies chasing soldiers into lava, OOA enemies picking
between crystal and line, `Main.player[255]` reads in net code (we restore after every AI call).

### 5.7 Boss rule
Main bosses = `NPCID.Sets.ShouldBeCountedAsBoss`/`npc.boss` restricted to the vanilla list King Slime … Moon Lord (constants).
HP scaling: `LevyGlobalNPC` on a main boss tracks `EngagedCp`; when a soldier first damages it (`OnHitByProjectile`/melee hit via
LootCredit), add the soldier's CP (militia 1) once: `lifeMax += 2 % × cp × base; life += same`, cap +80 % (config). Never removed.
Spite: per boss, per soldier, track `TicksWithin6Tiles` and a 3-second damage ring buffer; spite target = any soldier with ≥ 60 ticks
within 6 tiles or ring sum > 1 % of boss `lifeMax`. While a spite target exists, in `LootCredit`/`LevyGlobalProjectile.OnSpawn`
for hostile projectiles whose source is that boss (`IEntitySource_Parent` with the boss NPC, or `proj.npcProj`? — hostile boss
projectiles: check `source is EntitySource_Parent { Entity: NPC boss }`): every third one gets its velocity re-aimed at the
spite target with lead (keep speed), for 3 s after the spite trigger. Dashes: only for EoC (4), Retinazer/Spazmatism (125/126),
Duke Fishron (370), Skeletron head (35): during their dash state (read the `ai[]` phase in the decompiled AI_ for each), set
`npc.target = 255` + swap for that NPC only (reuse §5.6 machinery with a per-boss flag). Minions (Servants of Cthulhu, bees,
Creepers, Probes, Sharknados' sharks…) use normal targeting via the allowlist.

### 5.8 Fear
Meter per body (0–100), only runs while a main boss is alive within 80 tiles (`BossRule.NearestBossInView`). Inputs and
numbers in `LevyConstants.Fear`. Shaken at ≥ 50: damage ×0.6, aim ×2, emote (`EmoteID.EmotionCry`/`EmoteFear` — check
`Terraria.GameContent.UI.EmoteID`), orange tint; Shaken + hit again → rout chance (fear−50)/50 → Routed: run to the barracks
(militia: muster point), ignore enemies; rally when the boss dies, after 20 s without the boss in view, or on the Rally order
(fear = 25 for soldiers within 30 tiles, 60 s cooldown). Player within 10 tiles halves gains; player death routs everyone;
boss death clears. Modifiers: Greenhorn +10 %, Zealot −10 %, Veteran ×0.7, hazard pay ×0.75, Guide-to-Drill none, trophy of that
boss within 40 tiles of the home barracks: terror −2 (tile types `TileID.Painting3X3` styles for trophies — check
`TileID.Sets` / `ItemID.Sets.BossTrophy`? use the trophy item→tile style map in constants). Veterancy: a regular that survives
3 main-boss fights (boss killed while the soldier was engaged and alive) gets `Veteran` if < 25 % of regulars are veterans.

### 5.9 Dialogue
`Dialogue/en-US.txt` lines `TRIGGER | Greenhorn|Sellsword|Zealot|Cynic|Veteran|Any | text`. Triggers (enum `DialogueTrigger`):
IDLE, IDLE_NIGHT, NAMEDROP, BLOOD_MOON, PARTY, GRAVEYARD, RAIN, BIOME_JUNGLE/DUNGEON/UNDERWORLD/SNOW/CORRUPTION/CRIMSON/HALLOW/OCEAN/UNDERGROUND,
PAYDAY, UNPAID, DESERTED, MUSTER, STAND_DOWN, EVENT_GOBLINS/PIRATES/ECLIPSE/PUMPKIN/FROST/MARTIANS/OLDONES/FROSTLEGION, ENGAGE, KILL, HURT,
LOW_HP, OUT_OF_AMMO, HOLD_FIRE, BOSS_SIGHTED, BOSS_<InternalName> (e.g. BOSS_EyeOfCthulhu), COMRADE_HIT, COMRADE_DOWN, SHAKEN, ROUTED,
RALLIED, VICTORY, PROMOTED, VETERAN, DEATH_MESSAGE. Placeholders `{Player} {Name} {Comrade} {Boss} {Killer} {Town} {Wage}` and
every town NPC `{Guide} {Nurse} {ArmsDealer} {Demolitionist} {Merchant} {Dryad} {Clothier} {PartyGirl} {Tavernkeep} {TaxCollector}
{Mechanic} {GoblinTinkerer} {Wizard} {Steampunker} {Cyborg} {Pirate} {Truffle} {Stylist} {Angler} {DyeTrader} {Painter} {WitchDoctor} {Golfer}
{Zoologist} {Princess} {SantaClaus}` — a line with a town-NPC placeholder only fires if that NPC is alive (`NPC.AnyNPCs`).
Channels: right-click chat (IDLE/NAMEDROP/situational), emote bubbles (combat/fear), floating text (`CombatText` over the body:
BOSS_SIGHTED, COMRADE_DOWN, ROUTED, RALLIED), chat log (deaths, desertions, muster, payday). Budget: one floating line per squad
per 4 s, one emote per soldier per 8 s, no line repeats within 2 min (per line), militia get combat barks + death messages only.
Militia deaths per config: Count (one line at the end of the event: "{Town} lost 18 militia."), Individual, None.

### 5.10 UI
`UISystem` (ModSystem): `UserInterface` instances for BarracksPanel, ArmoryPanel, OrdersRadial, HudStrip; `ModifyInterfaceLayers`
inserts after "Vanilla: Inventory"; `UpdateUI`; closes panels when the player walks > 10 tiles away (like chests) or presses Esc.
BarracksPanel: name, treasury (deposit coins from inventory / withdraw), manpower bar (pool/cap), CP meter (used/total), roster
rows (name, grade chevrons, HP, state, pay, Veteran tag; buttons: Promote, Discharge, Assign patrol, Follow/Hold/Return),
Enlist (grade buttons with fee), Recruiting drive, Hazard pay, toggles AutoMuster / TaxToTreasury, Mobilize/Stand down.
ArmoryPanel: 120-slot grid + issue rows drawn with `ItemSlot.Draw`/`ItemSlot.Handle` (context `ItemSlot.Context.ChestItem`);
"Deposit junk" button (moves non-favourited weapons/armor/accessories/ammo from the inventory, not hotbar), "Loot all".
OrdersRadial: opened by right-clicking with the War Horn; six wedges Follow / Hold / Return / Hold fire / Mobilize–Stand down / Rally.
Keybinds (`KeybindLoader.RegisterKeybind`): Follow `Y`, Hold `U`, Return `I`, Hold fire `O`, Mobilize `P`, Rally `K`, Army strip `N`.
Nameplates: in `PostDraw`, name + grade chevrons (`^`), HP bar, fear icon when fear > 0; hidden when more than 20 soldiers are
on screen or config off. Minimap: `ModMapLayer` dots (regulars gold, militia blue). HudStrip: regulars' heads
(`Main.PlayerRenderer.DrawPlayerHead`) + HP bars, toggle with `N`. All UI actions go through `LevyPackets` when on a client.

### 5.11 Config (`LevyConfig`, ServerSide)
BossHpScalingPercentPerCp (2, 0–10), BossHpScalingCapPercent (80), FearMultiplier (1.0), SoldierDamageMultiplier (1.0),
WageMultiplier (1.0), RenderCap (40, 5–100), Tombstones (Vanilla/Always/Never), EnemiesTargetSoldiers (true),
BossesPunishSoldiers (true), Nameplates (true), SoldiersTriggerPressurePlates (false), NurseHealsSoldiers (true),
MilitiaDeathMessages (Count), AutoMusterDefault (true), UseVanillaStatPipeline (true), ExtraTargetingAiStyles (list of ints).

### 5.12 Flavor
Event books; trophies lower terror (§5.8); Tax Collector: if housed in the settlement and `TaxToTreasury`, his collected tax
(`Main.player.taxMoney` is per player… use `NPC.taxCollector` + `Player.taxMoney` drain hook: on dawn move `taxMoney` to the
treasury) ; Nurse heals soldiers within 4 tiles at her price from the treasury (every 2 s, config); tombstones via
`Player.DropTombstone`-equivalent: spawn `ProjectileID.Tombstone` family projectile with the death text (see `Player.DropTombstone`
in the decompiled source); Guide hint line (`LevyGlobalNPC.GetChat` for the Guide once per world when the player holds a War Horn
or has crafted a Barracks); first-time Barracks tooltip (3 sentences).

## 6. Coding rules (every agent)
1. Compile with `/home/user/tools/compile.sh <levy dir>` before returning; zero errors. Warnings about CS1701 are noise.
2. Verify every vanilla/TML member you use by grepping `/home/user/tml-src` or the ModLoader patches. If a member does not exist,
   do not invent it — find the real one. Record anything surprising in your return summary.
3. Server-authoritative: game logic runs where `Main.netMode != NetmodeID.MultiplayerClient`; clients draw and send packets.
   Never use `Main.LocalPlayer` in logic that runs on the server; use the commander index carried by the record/body.
4. Own only your files (listed per agent). You may add `using`s and private members in your files; you may NOT edit another
   agent's files or the shared contracts. If a contract is missing something you need, add a clearly marked
   `// CONTRACT-REQUEST:` comment in your file with a local workaround and report it.
5. Every number goes in `LevyConstants` (add there if missing — additive edits to that file are allowed; never rename).
6. All player-facing text comes from `Localization/en-US_Mods.Levy.hjson` (`Language.GetTextValue("Mods.Levy.X.Y")` /
   `this.GetLocalization`) or `Dialogue/en-US.txt`. Additive edits to the hjson are allowed.
7. Player-facing text style: 1–4 short declarative sentences, no history, no comparisons, no ALL-CAPS.
8. No scope widening: no mining/building soldiers, vehicles, bosses, biomes, invader morale.
9. Prefer vanilla mechanisms (emotes, CombatText, ItemSlot, NPC name lists, loot rules) over reinvention.
10. Performance: no per-tick allocations in AI; cache `WeaponProfile`; nameplates auto-hide; think every 6 ticks.

## 7. Build order mapping (milestones → files) and gates
M0 skeleton: Levy.cs, constants, enums, records, RosterSystem (save/CP/wage/payday/respawn), tiles+TEs+items (Barracks,
Armory, War Horn), SoldierNPC core + Draw + Damage + Combat(gun) + Brain(Follow/Engage) + Movement(basic) — gate: a levy with a
Musket kills a zombie and gets paid. M1 militia: SettlementSystem, hauling, reinforcement queue, render cap. M2 families: bows,
magic, broadsword, thrust, sidearm, aim error. M3 targeting: EnemyTargeting, SoldierTargeting lists, LootCredit. M4 boss rule +
fear. M5 grades/promotion/crystals/veterancy/armor pipeline. M6 movement abilities/pathfinding/teleport/travel. M7 UI. M8 dialogue.
M9 multiplayer (packets exist; sync pass later). M10 flavor. All milestones are built in this pass; TESTING.md lists each gate.

## 8. Decisions taken by the plan (note, not re-litigated)
* Haul destination = the linked armory's bulk storage (the "Spoils Chest" of the spec); coins → treasury.
* Dialogue table is a plain `.txt` asset read with `Mod.GetFileBytes`, per spec format; UI strings are hjson.
* Soldier names come from vanilla town-NPC name lists (`NPC.getNewNPCName`) with a rank prefix.
* Militia records are not persisted across a save; an event in progress re-musters on load if still active.
* `vanilla` tombstone mode drops a tombstone only when the commander character is Hardcore.
* Settlement names are generated from a small list in the hjson when the Barracks is placed.

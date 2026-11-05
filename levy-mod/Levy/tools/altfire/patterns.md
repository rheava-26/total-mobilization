# Right-click (alt-fire) policies for levies

Research and design only. No gameplay code was changed; the prototype lives in `Common/Testing/AltPolicyLab.cs` and runs only when `LEVY_HARNESS` and `LEVY_ALT_MODE` are set.

Files in this folder:

| File | What it is |
|---|---|
| `calamity_altfire_items.tsv` | One row per Calamity item (102): pattern, policy id, parameters, what each button does, requirements, hook class |
| `thorium_altfire_items.tsv` | Same for Thorium (89, plus the Dark Gate) |
| `patterns.md` | This file |
| `harness_results.md` | Left-only vs 80% random alt vs proposed policy, on 16 Calamity weapons |
| `*.tsv` under `harness/` | Raw harness rows for those runs |

Sources: Calamity 2.2.2 (`ModSources/CalamityMod`), Thorium decompile.

## 1. How a levy fires a right-click today

`WeaponUse.TryAltAttack` runs before every primary attack of a soldier:

1. `AltEligible(profile)`: only the shoot-pipeline families **Gun, Bow, Magic**. Swords, held/channel weapons, thrown stacks, whips, boomerangs, yoyos, flails, summons and "Unsupported" items never try the alt.
2. Roll `LevyConstants.AltUseChance` (0.8). Cooldown `AltMinGapTicks` 75 plus 0..30 jitter after an alt that fired.
3. `ItemLoader.AltFunctionUse(item, dummy)`. False: probe again in 300 ticks.
4. Set `dummy.altFunctionUse = 2`, ask `ItemLoader.CanUseItem`. A refusal ("on cooldown", "projectile already out", "no coin") is a soft failure: primary now, retry in 40 ticks.
5. Magic: if short of the item's mana, hold fire up to 150 ticks to save for it.
6. Fire through the normal dispatch with `_altActive` set. If nothing spawns, back off 240 ticks, give up after 4 failures.

So today the alt is chosen by dice, not by the situation. Whether that is good depends on the weapon: the alt is a better move for some weapons and a worse one for others (the harness numbers are in `harness_results.md`).

### Gotchas found while reading the items

* **The probe has side effects.** `Hydra.AltFunctionUse` itself launches every head and then returns false. `CalamityGlobalItem.AltFunctionUse` places the Glacial Embrace ring. The probe in step 3 therefore *performs* the alt. A policy gate must run before step 3, not as a `CanUseItem` veto (a veto is too late for Hydra).
* **Many "alt" weapons never reach the alt.** 70 of the 102 Calamity rows are not in Gun/Bow/Magic, so `AltEligible` is false. Examples: all Draedon holdouts (Nidhogg, Vulcan, Phalanx Surge, Auger...), the Biome Blade family, Ark family, Exoblade, Hellkite, Ontological Despoiler, Hellborn, Drataliornus.
* **19 Calamity items use the right mouse button without `AltFunctionUse`** (they read `CalamityPlayer.mouseRight`: Omicron, Wingman, Scorpio, Halley's Inferno, Oathblades, Devil's Devastation, Molten Amputator, Nanoblack Reaper, Firestorm/Spectralstorm Cannon, Perdition, Wulfrum Controller, ...). `AltFunctionUse` is false for them, so the alt path ignores them. Reaching them needs a separate "set mouseRight on the dummy" hook (see `HOLD_VARIANT`).
* **Holdout weapons read `mouseRight` when the holdout spawns** (Nidhogg, Phalanx Surge, Hellborn, Surge Driver, Photoviscerator, Apoctosis Array, Ion Blaster...). Setting `altFunctionUse = 2` is not enough; the Calamity player's `mouseRight` flag must also be true during the Shoot call. The prototype sets it by reflection.
* **Coin weapons** (Crackshot Colt, Midas Prime) need `player.CanAfford(...)` for the alt. The dummy has no money, so the alt is always refused and silently falls back to the primary. That is harmless but the ricochet combo (toss coin, then shoot it) never happens.
* **Wulfrum Knife** has `AltFunctionUse => false`; its recall is a `controlUseTile` check in `HoldItem`. Not reachable through the alt path.
* **Dark Gate (Thorium)** does not use `AltFunctionUse` either: see section 6.

## 2. Patterns

Counts are rows in the TSVs. "Reachable" means the item's levy family is Gun/Bow/Magic today.

### Calamity (102 rows)

| Pattern | Rows | Reachable | What the right-click is | Examples |
|---|---:|---:|---|---|
| `ALT_SHOT_HEAVY` | 13 | 6 | One slower, bigger or special projectile instead of the fast primary | Frigidflash Bolt (collapsing vortex), Animosity (marking brimstone round), Seas Searing (water spout), Speed Blaster, Violence (trident, 15x), Aegis Blade (bolt) |
| `ALT_SHOT_AOE` | 12 | 9 | Grenade, spread, ring or cloud | Corinth Prime (airburst grenade), Blossom Flux (spore bomb), Leviatitan (meteor), Vesuvius (2-3 asteroids), Veering Wind (frost ring), Plague-Tainted SMG (drone swarm), Mad Alchemist's glove (flask) |
| `ALT_RATE` | 3 | 2 | Same weapon in a different cadence | Plasma Caster (turbo, 1/3 damage), Plasma Rifle (rapid non-explosive), Halley's Inferno (rapid starbursts) |
| `MODE_TOGGLE` | 8 | 1 | Flips a persistent mode; fires nothing useful | Nullification Pistol (polarity), Biome Blades (attunement), Ontological Despoiler |
| `HELD_VARIANT` | 19 | 3 | A channelled holdout; the right button picks the variant when it spawns | Nidhogg (charged crit beam), Phalanx Surge (spear), Hellborn (energy sphere), Vulcan (harpoon), Surge Driver, Photoviscerator, Ion Blaster/Apoctosis (mana refill) |
| `DETONATE_RECALL` | 7 | 5 | Acts on projectiles that are already out | Hydra (launch heads), Septic Skewer (rip out harpoons), Sacrifice (daggers return and heal), Molten Amputator, Wulfrum Knife, Galileo Gladius |
| `PLACE_TARGET` / `PLACE_ATTACH` | 8 | 2 | Put something at the cursor/target | Face Melter (amplifier), Short Circuit (electrocuting hook), Dark Master (clones), Glacial Embrace, Void Concentration Staff |
| `DEFENSIVE_PARRY` | 10 | 0 | Parry, shield or stance | Lion Heart (halves damage), Mirror Blade, Ark family parry, Stratus Sphere, Oathblade stances |
| `MOBILITY_DASH` | 3 | 0 | Dash | Exoblade, Gilded Proboscis, Superradiant Slaughterer |
| `RESOURCE_SPECIAL` | 11 | 8 | Spends a meter or a stock | Alpha Draconis (5 Starbursts), Vega (20 Starbursts), Lightspeed/Lucrecia (full energy), Pristine Fury (powder), Deep Sea Dumbbell (flex stacks), Wulfrum Prosthesis (mana drain) |
| `SETUP_COMBO` | 2 | 2 | Sets up a later left-click bonus | Crackshot Colt, Midas Prime (coin toss for ricochets) |
| `UTILITY` | 6 | 2 | No combat effect | Photon Ripper, Hydraulic Volt Crasher, Auralis (zoom), Polaris Parrotfish (pet), Firestorm Cannon (put away) |

Excluded as non-weapons (they match `AltFunctionUse` but are tools, furniture or fishing catches): Axe of Purity, Cosmolight, Crystyl Crusher, Disgusting Meat, Grax, Seismic Hampick, Super Dummy, Wulfrum Digging Turtle, Dragoon Drizzlefish, Ritual Candle, Large Ritual Candle. `CalamityGlobalItem` only adds Glacial Embrace and Profaned Soul Crystal / Andromeda transforms.

### Thorium (89 rows, the Dark Gate included)

| Pattern | Rows | Reachable | Examples |
|---|---:|---:|---|
| `ALT_SHOT_HEAVY` | 21 | 10 | Omni Cannon (slow heavy shot), Pill Popper, Terrarium Spear, Rifle Spear |
| `OTHER_SAME_AS_LEFT` | 13 | 13 | Most bard instruments: `AltFunctionUse` is true but the right-click re-aims or re-offsets the same note (Bagpipe, Fishbone, Steam Flute, Sonic Amplifier...) |
| `PLACE_TARGET` | 10 | 7 | Chrono/Forest Ocarina and Holophonor (note field at the cursor); Aura Staff, Midnight Staff, Feather Barrier Rod, War Forger (right-click *dismisses* the placed object) |
| `MODE_TOGGLE` | 10 | 4 | Snow White, Master's Libram, Rude Wand, Geomancer's Brush, Totem Caller, Wondrous Wand, Useless Staff (the right-click cycles a stage and fires nothing) |
| `ALT_SHOT_AOE` | 9 | 4 | Buffalo Launcher, Honey Recorder (bees), Terra Knife, Black Dagger |
| `RESOURCE_SPECIAL` | 9 | 2 | Bonesaw, Omega Blaster (600 charge), Quake Gauntlet, Dragon Tooth |
| `UTILITY` | 5 | 1 | Gateway Glass, Soul Anchor, Wishing Glass (teleports), Resonator's Arm |
| `DETONATE_RECALL` | 4 | 2 | Blooming Bow, Borean Fang Staff |
| `ALLY_HEAL` | 2 | 1 | Cell Reconstructor (area heal), Holy Hammer |
| `ALT_RATE` | 2 | 2 | Snarky Gun (2x rate, no ammo), Omni Cannon |
| `MOBILITY_DASH` | 2 | 1 | Blitzzard, Witchblade |
| `HELD_VARIANT` | 1 | 1 | Little Red |
| `PORTAL_USE` | 1 | 1 | Dark Gate |

The Thorium table is a first pass from reading the decompile (right-click descriptions are from code, not tooltips, and not harness-tested).

## 3. Soldier-AI context used by every policy

All of these are cheap and already computable inside `SoldierNPC`:

| Name | Definition |
|---|---|
| `Dist` | Tiles from the soldier's muzzle to the current target's centre |
| `Crowd(r)` | Hostile, active, chaseable NPCs within `r` tiles of the target (the target counts) |
| `Tough` | `target.boss` or `target.lifeMax >= 600` (param `tough`) |
| `SelfHp` | `NPC.life / lifeMax` of the soldier |
| `ManaFrac` | `Record.Mana / max mana` |
| `Out(proj)` | Active projectiles of that `ModProjectile` type owned by this soldier's owner id |
| `Ready` | `Main.GameUpdateCount >= s.AltReadyTick` plus the item's own `CanUseItem(alt)` |
| `Wounded(r, f)` | Allies (soldiers, commander) within `r` tiles with HP below `f` |
| `Meter(name)` | A named field on the dummy's Calamity/Thorium player (Starbursts, energy, charge), read by reflection once per decision |

## 4. Policies (WHEN to right-click)

One policy per pattern. All use `Ready` and, for magic, the existing mana-save rule. Parameters are in the `params` column of the TSV (`key=value;key=value`).

| Policy | Fires the alt when | Why | Fits `AltUseChance` path? |
|---|---|---|---|
| `ALT_HEAVY` | `Tough` and `minDist <= Dist <= maxDist`. Against trash: 10% | Slow/bigger shots are for armoured or boss targets; vs small enemies the fast primary kills more per second | **Yes** (replace the dice by a gate) |
| `ALT_AOE` | `Crowd(radius) >= minEnemies` and `minDist <= Dist <= maxDist`. Self-centred rings use `maxDist` small | Grenades, rings and clouds are wasted on one target | **Yes** |
| `ALT_RATE` | `Dist <= maxDist` (inside the accuracy range of the turbo mode) | A cadence alt is a strict gain inside its range | **Yes** |
| `ALT_FAR` | `Dist >= minDist` | Melee swords: the alt is a ranged poke for when the target is out of reach | **Yes**, once melee is eligible |
| `ALT_ALWAYS` | Always | The alt is the weapon's only ranged attack (Terrarian's Last Knife) | **Yes** |
| `ALT_MIX` | Probability `p` each time the dice allow it | Generic default for unlisted alts and for "same as left" alts. p = 0.4 | **Yes** (this is today's behaviour with a lower p) |
| `TOGGLE_N` | Every `afterShots` primary shots | Both polarities/modes get used; no cost | Needs a primary-shot counter on the soldier (`s.PrimaryShotsSinceAlt`): **small hook** |
| `DETONATE` | `Out(proj) >= minOut` and `Dist >= minDist`; optional `ownHpBelow` for heal-on-return | Recall/detonate only when there is something to recall; never probe otherwise (Hydra's probe fires it) | **Yes, but the gate must run before `AltFunctionUse` is probed** |
| `PLACE` | `Out(proj) < maxOut`, `Dist <= maxDist`, and `Tough` when `tough=1` | Place one object at a time; never stack | **Yes** (gate before probe) |
| `GUARD` | `SelfHp < hpBelow`, or `Crowd(near) >= enemies`; not already shielded; item off cooldown | Defensive alts are for pressure, not for idle fights | **Needs melee/held eligibility** |
| `METER_READY` | `Meter >= min` and (`Tough` or `Crowd >= 3`) | Do not burn a hard-earned meter on trash | **New hook** (read the dummy's mod player) |
| `HOLD_VARIANT` | `cond` (`heavy`: Tough and in range; `aoe`: crowd; `rate`: in range; `close`: within `maxDist`) at the moment the holdout spawns: set `CalamityPlayer.mouseRight` on the dummy for that Shoot | Holdout weapons pick their variant from the held button | **New hook** (set the flag; prototype does it by reflection) |
| `MANA_REFILL` | `ManaFrac < manaBelow` | Mana-restoring alts (Ion Blaster, Apoctosis Array, Wulfrum Prosthesis) are worth the cast time only when the pool is low | **New hook** |
| `STACK_UP` | `stacks < maxStacks`, `Tough`, in range | Deep Sea Dumbbell flex multiplies the next throw; do it only when there is time | **Yes** |
| `ALLY_HEAL` | `Wounded(radius, hpBelow) >= minWounded` | Healer alts only when allies need it | **New hook** (ally scan; the healer code has one) |
| `DASH_CLOSE` | `Dist >= minDist` and a clear path | Dashes close gaps | **New hook** (movement integration) |
| `PORTAL_USE` | See section 6 | Dark Gate | **New hook** |
| `NEVER` | Never | Mode cyclers, dismiss-only alts, utilities | n/a (skip the probe entirely) |

Summary of what needs what, over the 191 rows:

* **A** (fits the existing path with a gate, no new state): 67 rows (25 Calamity, 42 Thorium) that are reachable today.
* **B** (needs a hook): 8 reachable rows (meters, holdouts, coins, portal).
* **C** (not reachable until melee/held/summon families are alt-eligible): 116 rows.

## 5. Proposed C# shape

Data-driven, keyed by item type, so a row only exists while the item exists:

```csharp
// Common/Weapons/AltPolicy.cs
public enum AltPolicyKind { Never, Mix, Heavy, Aoe, Rate, Far, Always, ToggleN, Detonate, Place, Guard, MeterReady, HoldVariant, ManaRefill, StackUp, AllyHeal, DashClose, PortalUse }

public readonly struct AltParams {                  // parsed once from the TSV
    public readonly float P, MinDist, MaxDist, Radius, HpBelow; public readonly int MinEnemies, MinOut, MaxOut, AfterShots;
    public readonly bool Tough; public readonly int ProjType;      // resolved from "proj=Name" after content load
    public readonly string Cond, Meter;
}

public sealed class AltPolicy { public AltPolicyKind Kind; public AltParams P; }

public struct AltCtx {                               // built lazily, once per decision
    public SoldierNPC S; public NPC Target; public float Dist; public int Crowd; public bool Tough; public float SelfHp, ManaFrac;
    public int Out(int projType);                    // cached per tick
}

public static class AltPolicies {
    static readonly Dictionary<int, AltPolicy> ByItem = new();    // item.type -> policy
    static readonly AltPolicy Default = new() { Kind = AltPolicyKind.Mix, P = { P = 0.4f } };

    internal static void Load(Mod levy);             // PostSetupContent: reads Common/Weapons/AltPolicies.tsv (embedded), resolves mod/name
                                                     // through ModContent.TryFind<ModItem>; unresolved rows are skipped, so a row exists only when its mod is loaded
    public static void Register(string mod, string item, AltPolicy p);   // other mods or later data can add rows
    public static bool Want(SoldierNPC s, Item item, NPC target);        // the gate
}
```

Call site in `TryAltAttack`, replacing the dice **and placed before the `ItemLoader.AltFunctionUse` probe**:

```csharp
if (s.AltHoldSince == 0 && !AltPolicies.Want(s, item, seg ?? s.Target)) return false;   // was: Main.rand.NextFloat() >= AltUseChance
```

`Want` looks up `ByItem[item.type]`, falls back to `Default` (the 0.4 generic mix, so unlisted alt weapons still use their alt, less often than the old 0.8), then runs the kind's gate on a lazily built `AltCtx`. Rows with `NEVER` return false immediately and also set `s.AltReadyTick` far out so the probe is skipped.

The two extra hooks:

1. `HoldVariant`: `AltPolicies.BeforeShoot(s, item)` and `AfterShoot` set/clear `CalamityPlayer.mouseRight` on the dummy around `TryRealShoot` (one reflection-cached field accessor; the same shape as the existing owner-puppet bits).
2. `MeterReady`/`ManaRefill`/`AllyHeal`/`PortalUse` read soldier or ally state. Meter names come from the TSV, resolved by reflection once per item type.

Data file: embed `tools/altfire/*_altfire_items.tsv` columns `item, mod, policy, params` as `Common/Weapons/AltPolicies.tsv`. Editing policy is then a data change, not a code change. A generic ALT_MIX default means a new alt weapon works with no row at all.

Constants that stay: `AltMinGapTicks`, `AltRetryTicks`, `AltFailBackoffTicks`, `AltGiveUpFailures`, mana saving. `AltUseChance` becomes the default row's `p`.

## 6. Thorium: the Dark Gate (healer portal)

The designer's "Dark Staff" is Thorium's **Dark Gate** (`ThoriumMod.Items.Donate.DarkGate`, projectile `DarkGatePro`).

* **Left-click**: needs a clear line from you to the cursor, 50 mana. Kills your previous gateways, then places a **linked pair**: one at your position, one at the cursor.
* **Right-click is not an item alt.** `ThoriumPlayer` calls `DarkGate.DoTeleport(player)` every frame for the local player. When the cursor is within 80 px of a gateway, the player has no `ChaosState`, and `Main.mouseRight` was just pressed, the player teleports to the partner gate (`DoSpecialTeleport`), heals `10 + healBonus` life and gains `ChaosState` for `720 - 30 * healBonus` ticks (12 s at no bonus). The gateways last 7200 ticks.

So the item is a mobility tool for anyone near a gate; it works whatever the player holds.

### Policy `PORTAL_USE` (proposed)

Levies treat a gate pair as a shared transit link and right-click it:

1. **Use** a gate when all of these hold:
   * The soldier is within 3 tiles of a gate it can reach, and it has not used one in the last 720 ticks (its own `ChaosState` timer, kept on the soldier, since the dummy is not the local player).
   * Either `SelfHp < 0.5` and the partner gate is nearer the commander than the soldier (a retreat-and-heal), or the soldier is farther than `useWhenFarFromCommander` (40) tiles from the commander/rally and the partner gate is at least 25 tiles closer to it (regroup).
   * There is no `Tough` target within 12 tiles (do not leave a fight to teleport).
2. **Do not use it when the player is.** Skip all use while the commander has `ChaosState`, or is within 6 tiles of either gate, or holds a Dark Gate item (`ignoreIfPlayerHoldsGate`): the player is operating the gateways, and a levy teleporting through them would fight the player's own routing and burn the shared 12 s window.
3. **Place** (left-click) only for a healer levy that holds the item and has no pair out: gate A at the soldier, gate B within 6 tiles of the most wounded ally or the commander, as long as the player does not own a pair (the item kills the owner's previous pair, and levy projectiles are owned by the soldier owner, so a levy placing would delete the player's gates). Cooldown 720 ticks.

Implementation notes: needs a small `PortalUse` hook (find `DarkGatePro` projectiles by type, read `ai[0..1]` for the partner, `Teleport` the soldier NPC, apply `HealLife` equivalent to `statLife`). It cannot go through `TryAltAttack` because there is no alt to probe.

## 7. What to ship first

1. Move the dice into `AltPolicies.Want` with the **generic default** only (`ALT_MIX p=0.4`) and the `NEVER` list (mode cyclers and dismiss-only alts: Aura Staff, Midnight Staff, Feather Barrier Rod, War Forger, Snow White...). That alone removes the cases where a levy cancels its own support.
2. Add the A-class rows (`ALT_AOE`, `ALT_HEAVY`, `ALT_RATE`, `DETONATE`, `PLACE`) for the 33 Calamity and 42 Thorium reachable weapons, tuned by `harness_results.md`.
3. Gate-before-probe fix for Hydra and any future side-effect probe.
4. Hooks: `HoldVariant`, `MeterReady`, then `PortalUse`.
5. Only then open melee/held eligibility for `GUARD`, `DASH_CLOSE`, `ALT_FAR`.

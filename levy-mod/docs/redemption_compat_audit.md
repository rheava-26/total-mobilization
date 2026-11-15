# Large-content-mod compatibility audit (Redemption beta 0.8, 2026-11-15)

Levy-side findings only. The mod's own text and content notes live outside git.

Tested: Levy 2.0.66 + Mechanization + Warbands + the mod (and its five required libraries) on tML 2026.08, and the same plus Calamity on tML 2026.10, headless, ship world checks and the lab tile sweep. The mod is built for tML 2026.06 and loads on both. It lists no incompatibilities. Its own description says multiplayer is broken.

## Levy-side changes needed

1. **Vanilla thrown weapons turn into ammo (degrades).** A global item sets `ammo` (with `notAmmo = true`) on Grenade, Beenade, Sticky Grenade, Bouncy Grenade, Party Girl Grenade, Throwing Knife, Poisoned Knife and Bone Dagger so its own launchers can use them. `WeaponClassifier.IsThrownConsumable` rejects any item with `ammo > 0`, so levies stop throwing all eight, and five armory/wiring self-checks fail (knife issue, restock, "no levy is left bare-handed"). Fix: treat `ammo > 0 && !notAmmo` as ammo in `IsThrownConsumable` and `WeaponClassifier.IsAmmo`; `ItemFilter.IsAmmo` already does this.
2. **World-alter classifier misses two items (breaks the never-alter rule).** The sweep measured 13 altering items and 2 rounds from the mod. The shipped table has no rows for them and the compiled-code scan recalls 14 of 27 altering projectile types. Missed: a thrown plutonium bomb (converts tiles) and the toxic grenade and its launcher pairing (gas converts tiles). The sweep wrote `W/A/X/P` rows to its output file; add them to `Common/Weapons/WorldAlterTable.tsv`. Also find out why the scan misses a static helper that sets `Tile.TileType` (suspected: depth, budget, or an opcode the walker bails on).
3. **Native-target route can index `Main.player[]` with 300+ (breaks one boss, unlock only).** Three NPC types set `UsesNewTargetting`. The boss one re-runs `TargetClosest` when the real player is dead or inactive and then reads `Main.player[NPC.target]`. With Levy's native route (`target = whoAmI + 300`) this throws every tick. It only applies after that boss is downed and Bosses target soldiers is on. Fix: add that type to a "native-unsafe, use the flyer slot" list like `NativeBehindCalamityOverride`.
4. **`HasValidTarget` is false while swapped to slot 255 (degrades).** A few base classes in the mod (worm heads, one worm boss, a shared despawn helper) treat `!HasValidTarget` as "no target" and refresh `timeLeft` only when it is true. An unlocked boss or boss minion on the slot-255 swap can fail to refresh `timeLeft`. Consider the flyer slot (254) for boss-owned modded NPCs, or verify in play.
5. **Mod projectile hooks throw when the owner is the null slot (caught, harness only?).** `GetModPlayer` throws `IndexOutOfRange` inside the mod's projectile hooks for army minions on a server with no player online (owner = slot 255). Six caught exceptions in the world check, none in the no-world boot, none with Calamity. Likely not a single-player issue (owner is the commander), unverified.
6. **Levy self-check noise.** `ProgCheck: untouched recipes identical to vanilla` fails because the mod edits vanilla recipes; `raw items have no material cost` and one `WhipReal` dps comparison also fail with it loaded. None breaks play; they make the ship report unclean while it is loaded.

## Not a problem (checked)

- Custom damage systems (elements, guard points) are read from projectile and item sources; Levy fires with real `EntitySource_ItemUse` sources, so they apply to levy shots. Guard hooks read `Main.player[projectile.owner]`, which is valid for every Levy owner.
- Contact: `CanHitPlayer` overrides never touch `Main.LocalPlayer`/`myPlayer`. The mod's custom boss hitboxes also hurt friendly NPCs directly through `StrikeNPC` (levies are hit; Levy's contact parity and immunity slots are bypassed for those attacks).
- Kill hooks (alignment) fire on boss first kills and event points, never on town-NPC kills; a levy kill triggers them like a player kill.
- Subworlds: Levy has no subworld code. The levies do not enter; the roster stays in the main world's save. Not testable headless.
- Boot: zero exceptions at load on both toolchains. Ship world checks otherwise pass (identity, player safety, hit parity, boss bugs, tile sweep sample, officer, mob).

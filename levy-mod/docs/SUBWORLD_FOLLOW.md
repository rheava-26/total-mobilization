# Levies following the commander into a subworld (proposal, research only)

Status: research and proposal. Nothing here is built. Mechanism only: no boss mechanics or story.

## Why this comes up

Wrath of the Gods (NoxusBoss) runs one of its late fights in a SubworldLibrary subworld, `EternalGardenNew`, entered with `SubworldSystem.Enter<EternalGardenNew>()`. A subworld is a separate world with its own file. Levies are main-world NPCs whose roster lives in the main world's save (`RosterSystem.SaveWorldData`), so today nobody follows the commander there: she arrives alone, and the army is waiting where she left it when she comes back. Nothing is lost or duplicated today, because the main world is saved before the switch and reloaded after it.

## How SubworldLibrary moves the player (SubworldLibrary 2.3.0.1, decompiled)

### Single player

`Enter` sets `current` and runs `ExitWorldCallBack` on a worker task. In order it:

1. builds a `TagCompound copiedData` and calls `CopyMainWorldData`. That copies SubworldLibrary's own state (seed, game mode, hardmode, bestiary, journey powers, the downed flags, the special-seed flags) and then calls `CopyMainWorldData()` on every loaded `ICopyWorldData` (an `ILoadable` interface any `ModSystem` can implement) and on the subworld itself;
2. saves the player, then `WorldFile.SaveWorld()` saves the world being left (the main world, through every mod's `SaveWorldData`);
3. runs `SystemLoader.OnWorldUnload`;
4. loads the subworld. `EternalGardenNew.ShouldSave` is true, so after the first visit the subworld is read from its own file (`<world folder>/<main world id>/NoxusBoss_EternalGardenNew.wld` plus `.twld`). Reading it runs every mod's `LoadWorldData` with whatever that mod saved there, then `ReadCopiedMainWorldData()` on every `ICopyWorldData`. The first visit generates the subworld instead (no `LoadWorldData`, `ClearWorld` state), then calls the same `ReadCopiedMainWorldData()`;
5. spawns the player at the subworld's spawn point.

Leaving (`SubworldSystem.Exit`) runs the same callback in reverse: inside the subworld, `CopySubworldData` and `CopyMainWorldData` fill a fresh `copiedData` (so every `ICopyWorldData.CopyMainWorldData()` is called again, this time in the subworld), the subworld is saved, and the main world is loaded from its file. After the main world's `LoadWorldData`, SubworldLibrary calls `ReadCopiedSubworldData()` on the subworld and `ReadCopiedMainWorldData()` on every `ICopyWorldData` with that data. So one interface carries data both ways, and `SubworldSystem.Current` (null in the main world) tells the two calls apart.

Quitting to the menu from inside the subworld writes `copiedData` into the main world's `.twld` (SubworldSystem's own mod data, `CacheWorldData`) and replays it on the next load of the main world (`WorldFile.OnWorldLoad += ReadCachedData`), so the return path also runs after a quit and restart.

### Multiplayer

A dedicated server never switches worlds. Each subworld is its own server process (a "subserver", started with `-subworld N` and talking to the main server over named pipes). When the first player enters, `StartSubserver` takes one `CopyMainWorldData` snapshot on the main server and pipes it to the new process; later entries reuse the running subserver and get no new snapshot. The player's client is handed to the subserver; on the main server that player becomes inactive and `Player.Hooks.PlayerDisconnect` fires. Nothing is copied back when a player returns. The only channel back is a live packet: `SubworldSystem.SendToMainServer(mod, bytes)` from a subserver and `SendToSubserver` / `SendToAllSubservers` from the main server, delivered to the mod's `HandlePacket` with `whoAmI` 256. The main world keeps running the whole time.

## Where Levy would hook in

Levy would carry a weak reference to SubworldLibrary (like ThoriumMod and MagicStorage today) and one small `[ExtendsFromMod("SubworldLibrary")]` system that implements `ICopyWorldData`. Without the mod it is never loaded, and nothing else in Levy changes.

- **Going in (main world, `CopyMainWorldData` with `Current == null`)**: pick the followers: the commander's regulars on Follow who are alive and deployed (the levies actually standing with her; stood-down levies and barracks garrisons stay home). Write their records (`SoldierRecord.Save()`, the same tag the world save uses) under a Levy key, and mark each one `Away` in the main roster. The main world is saved right after this call, so `Away` is on disk before the switch.
- **Arriving (subworld, `ReadCopiedMainWorldData` with `Current != null`)**: build a guest roster from the copied records (same ids, flagged guests) and let the normal deploy spawn their bodies next to the commander once she is in the world (`RosterSystem.SpawnBody`). Guests never respawn there (no barracks): a guest who dies stays dead until the return.
- **Never save guests**: `RosterSystem.SaveWorldData` (and the other Levy world savers) write nothing roster-related while `SubworldSystem.Current != null`. Otherwise this subworld's own save would keep a stale copy of the army and load it on the next visit.
- **Leaving (subworld, `CopyMainWorldData` with `Current != null`)**: write the guests' results by id: dead or alive, current life and mana, ammo and consumables used, kills, fights survived, fear. Not the whole record: gear changes in the subworld are not taken back, so nothing can be created there.
- **Back home (main world, `ReadCopiedMainWorldData` with `Current == null`)**: apply the results to the `Away` records by id (a dead guest becomes a normal dead levy with the normal respawn timer), clear `Away`, and let the normal deploy bring the bodies back.
- **Recovery**: on main-world load, an `Away` record with no results to apply (a crash inside the subworld, a save copied to another machine) simply comes home as it left. That is the safe failure: nothing lost, nothing doubled; at worst the ammo spent inside is not charged.

## Risks

**Saves.** The main save is written on every switch while the roster is in a half-state, so the `Away` flag must be saved and must always clear (results applied, or recovery). The subworld's own save must never hold roster data. SubworldLibrary's `copiedData` is a plain `TagCompound`, so Levy's payload must use only tag-safe types and survive the quit-to-menu path, where it is stored in the main world's `.twld` until the next load.

**Duplication.** Single player cannot duplicate bodies, because only one world runs at a time. The real risk is records: applying results twice (the return path plus a cached replay) or to the wrong records. Results keyed by soldier id, applied only to records still marked `Away`, and cleared afterwards make the apply idempotent. In multiplayer the main world keeps running, so a follower must be despawned there and kept from redeploying while `Away`, or the same levy fights in two worlds at once.

**Death.** A levy who dies in the subworld has no barracks to respawn from. The minimal version keeps him dead until the return and starts the normal timer then. Levy's death messages already run through `LevyDeathPost`; they work unchanged on guests.

**Roster integrity.** Hiring, dismissing, promotions and the armory in the subworld would act on guest copies that are thrown away, so the minimal version blocks roster changes there (the armory and barracks tiles do not exist there anyway). Mechanization drones and Warbands are left out. Officers are town NPCs with avatars and are left out too.

**Other mods' state.** The subworld runs `OnWorldUnload` and `OnWorldLoad` for every system, so Levy statics that assume one world (settlement caches, fight recorder, boss-downed set) reset on each switch. SubworldLibrary copies the vanilla downed flags but not Levy's own beaten-boss set (`BossDownedSystem`, world data), so inside the subworld a modded boss reads as unbeaten unless Levy copies that set too. The first-kill rule handles that safely (an unbeaten boss is never drawn onto the army), but it should be copied for consistency.

**Multiplayer.** Moves are per player and one snapshot serves the whole subserver, so followers have to travel by live packets keyed to their commander (`SendToSubserver` when that player's move starts, `SendToMainServer` with results when a guest dies or the commander leaves). A subserver crash loses only those results, and recovery brings the levies home unchanged.

## Effort

The minimal single-player version is roughly one builder-day: the weak reference and the `ICopyWorldData` bridge, the `Away` flag and recovery, the guest roster with the save guard, results by id, and a world check that runs the copy, save, load and apply cycle in one process by calling the bridge directly. A dedicated test server cannot run the single-player switch itself, because `BeginEntering` does nothing on a server. A real subworld round trip needs a client session. Multiplayer is another three to five days, mostly the packet protocol, despawning followers in the running main world, and testing with a second process.

## Recommendation

Build the single-player version first, followers only, with results flowing back but no gear changes, and roster changes blocked inside. It covers Eve's actual setup (single player), keeps the main save the only source of truth, and fails safe: if anything goes wrong, the army comes home exactly as it left. Multiplayer can follow the same record format later over SubworldLibrary's packets. Until then, levies stay home during subworld fights, as they do now.

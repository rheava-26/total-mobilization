# Warbands (v0.2.0): hostile levy bands, a Levy sub-mod

Eve approved hostile levy bands as a sub-mod: enemy groups you can fight for part of their loot, biome-specific, running Levy's real soldier brain. Design tenet from `levy-mod/NORTH_STAR.md`: difficulty comes from a scarier enemy, never from nerfing the player's army.

- `docs/TTK.md`: the time-to-kill lab (phase 1) and the proposed stat rules.
- `docs/FIGHT_LAB.md`: phase 2 fight comparison (Levy-AI band vs Fighter-AI band vs ordinary enemies, Expert).
- `docs/item_preview_4x.png`: the 20 summoning items at 4x. `tools/make_art.py` redraws them.
- `docs/LORE_FACTIONS.md`: Calamity factions used for names and chatter when Calamity is loaded.
- `SEAMS.md`: every Levy symbol Warbands touches and the Levy edits (the HOSTILE SEAM).
- `Warbands/`: the mod. Build exactly like Mechanization (`modReferences = Levy`).

## What exists

- **Levy brain on the enemy side** (`BandLevyNPC : SoldierNPC`): Levy's movement (hooks, wings, double jumps, dodging), weapon use, firing-spot search and standoff run on a hostile body. Targets are the player (through an invisible `PlayerStandIn` NPC on each player), the player's levies, officers, summoned units, drones and turrets, and levies shoot them back. Shots are hostile-owned with the halving rule; damage intake is a normal enemy's (vanilla hit immunity, no player i-frames); life, defense and damage come from `BandStats`; the loadout is a record built from the roster that never enters Levy's roster.
- **20 band types** (`BandTypes`), each with a Work Bench summoning item (wood plus a few cheap materials): Forest Deserters, Iron Brigands, Dune Raiders, Snow Rangers, Corrupt Zealots, Crimson Flesh-Cult, Jungle Poachers, Meteor Marauders, Ashland Scavengers, Dungeon Wardens, Hive Raiders, Hallow Paladins, Mythril Mercenaries, Hellfire Legion, Titanium Legion, Chlorophyte Wardens, Shroomite Raiders, Frost Legion, Dungeon Ritualists, Cultist Archers. Beating a whole band pays a bounty; members drop a share of their gear (35% per weapon, 25% per armor piece).
- **Obviously enemies:** red nameplate and red life bar, a red pennant and sash, their own name list (rank + first name).
- **Brains:** morale (chip damage on an armoured player, comrades one-tapped, low life) makes a member panic and run, and 60% carry a Recall potion; band chatter over their heads in Levy's dialogue voice; class-adaptive tactics (keep range from a melee foe, close in on a gunner or caster, flank a summoner); rotating aggro for ranged bands (the attacked side falls back while the other fires); warning bands ("Turn back.") attack only if you stay or close in.
- **Cultists:** Lunatic Devotees keep chanting and cast slow homing orbs; Cultist Archers become a levy-AI band.
- **Spawner:** natural bands as before (4% per minute, post-EoW by default); band size is the config range plus one per ten active levies, at most 8.
- **Self-check:** `[Warbands] self-check done pass=N fail=0` on a dedicated server boot. `WARBANDS_LIVE=check` runs 32 scripted scenarios (Levy brain, both-way damage, no i-frames, warning, tactics, rotation, morale and Recall, chatter, all 20 items, bounty, cultists, recipes); `WARBANDS_LIVE=fight WARBANDS_OUT=f.tsv WARBANDS_SEEDS=6` runs the fight comparison.

## Not done

Summon staves and sentries on hostile bodies; the draw path is unverified headless (red plate, pennant, player-renderer body); multiplayer is exercised only through the packet path, not with a real client.

## Testing

```
build:  dotnet tModLoader.dll -server -build <abs path to the mod> -tmlsavedirectory <copy of the save dir>   (Levy first, then Warbands; enabled.json empty while building)
boot:   enabled.json ["Levy","Warbands"], -world <any world>, read tModLoader-Logs/server.log for "[Warbands]"
```

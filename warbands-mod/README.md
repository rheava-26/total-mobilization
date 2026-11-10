# Warbands (v0.1.0): hostile levy bands, a Levy sub-mod

Eve approved hostile levy bands as a sub-mod: enemy groups you can fight for part of their loot, biome-specific, and the Lunatic Cultist's circle fighting as a group that juke-flies. This folder is phase 1: the numbers, the rules, and a working skeleton. Design tenet from `levy-mod/NORTH_STAR.md`: difficulty comes from a scarier enemy, never from nerfing the player's army.

- `docs/TTK.md`: the time-to-kill lab (4 stages, Master and Classic), what it says and the proposed rules.
- `SEAMS.md`: every Levy symbol Warbands touches, and what Levy needs before the real soldier brain can run a hostile body (phase 2).
- `Warbands/`: the mod. Build exactly like Mechanization (`modReferences = Levy`).

## What exists

- **Spawner** (`BandSpawner`): once a minute per player in the open, 4% by default, one band at a time, nothing before the first stage (default post-EoW/BoC), no bands during invasions, blood moons or eclipses. Band of 3 to 5. A band appears 40 to 70 tiles to one side on solid ground. All of it is in `WarbandsConfig`.
- **Rosters** (`BandData`): 9 biomes (Forest, Underground, Corruption, Crimson, Desert, Snow, Jungle, Hallow, Underworld) x 4 stages (pre-boss, post-EoW, early Hardmode, post-Plantera); the stages in between use the nearest earlier roster. Real vanilla weapons, ammo and armor from the same tiers Levy's gear presets use (Forest deserters carry wooden bows and copper swords before the first boss). The self-check holds every item to Levy's own progression tier for its stage.
- **Soldiers** (`BandSoldier`): a hostile NPC drawn through the player renderer in its real armor and weapon. Vanilla's Fighter AI (which Levy's targeting already points at levies as well as the player) plus a shooter layer: archers and mages hold at range, step back when closed on, jump to juke, and fire real hostile arrows, bullets or bolts at the nearest of the player and the levies. Members of a band share an attack token (2 attackers at once up to 4 members). Stats from `BandStats`. On death each piece of gear drops with the configured chance (35%).
- **Cultist circle** (`CultistBandGlobal`): Cultist Archers and Lunatic Devotees within 60 tiles of a player or levy fly, strafe, weave and fire at the nearest target, with damage from the same stage cap.
- **Self-check**: `[Warbands] self-check done pass=N fail=0` on a dedicated server boot (`WARBANDS_CHECK=0` skips).
- **TTK lab**: `WARBANDS_TTK=1 WARBANDS_OUT=out.tsv` on a headless boot.

## Not done (needs Levy, see SEAMS.md)

The band does not run Levy's soldier brain (weapon use, hooks, formations, fear). The soldiers are visually unverified (the check ran headless; the player-renderer path falls back to a placeholder sprite if it throws). The spawner's timed roll is not exercised headless (a dedicated server with nobody online does not tick the world); the spawn, stats, shooting and Cultist AI are exercised directly by the self-check.

## Testing

```
build:  dotnet tModLoader.dll -server -build <abs path to warbands-mod/Warbands> -tmlsavedirectory <copy of the save dir with Levy.tmod>   (Levy built first; enabled.json empty while building)
boot:   enabled.json ["Levy","Warbands"], -world <any world>, read tModLoader-Logs/server.log for "[Warbands]"
```

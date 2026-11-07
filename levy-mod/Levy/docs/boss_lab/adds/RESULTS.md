# Boss adds: who they target, and the full-hitbox recorder (branch `perfrender`, 2026-11-07)

Harness: lab-3 arena `a3`, `cmd=near`, Calamity 2.2.2, `LEVY_CR_SP=1`, fear model events, private install. Tools: `adds2.py` / `near.py` (scratchpad `work/`), reading recorder format 5-7 files. Recordings: scratchpad `replays/adds/` (9 files, `index.txt`).
Every number is OBSERVED from a recording unless marked INFERRED.

## 1. Before vs now (Spectator commander = Eve's way of playing)

Share of recorded frames in which the add's target is a levy / the player.

| boss | add | BEFORE | NOW |
|---|---|---|---|
| Perforator Hive (Expert, Rev) | worm heads (small, medium, large) | lab-3 replays 14 and 15: 100% player, left the range (144-198 tiles from the arena) after about 3 s | 100% levy, 2 seeds; segments are drawn (below) |
| Hive Mind | Eater of Souls, Devourer | 98-100% levy | 97-100% levy |
| Hive Mind | Dank Creeper | 100% player (lab-3 replay 13) | 98-100% levy |
| Slime God | Paladins, slime spawn | no adds at all in the old recording (they despawned, run ended at the 600 s timeout) | 100% levy |
| Desert Scourge | Desert Nuisance worms | Eve's `ds_team_1-3`, `ds_solo_1`: 100% player; levy nearer in 36% of those frames | 100% levy (2 runs) |
| King Slime | Ruby Jewel | Eve's `ks_team_1`, `ks_team_3`: 100% player; a levy was nearer in 54-64% of those frames | 100% levy (Master+Death, seeds 2 and 3) |
| King Slime | Blue, Spiked, Red, Yellow slimes | Eve's team runs: 16-77% player, but a levy was nearer in only 0-1% of those frames (the player really was the nearest) | 100% levy |
| Brain | Creepers | 100% levy (lab-3, Spectator) | 100% levy |
| Eye | Servants | 100% levy (earlier run in this session) | not re-run after the final fix (scope) |
| Crabulon | Crab Shrooms | not in the scope after the change; one early run: 100% player while Crabulon itself targeted levies (cause not found) | UNVERIFIED |

Runaway check, Perforator (Spectator): before, the worms left within 3 s; now a worm lives 4-18 s and swoops up to about 180 tiles out and back, as a worm AI does. Desert Nuisance: before 1-18 s; now 33-38 s of life (they stay and fight).

## 2. Non-Spectator runs (`spec=0`, the commander is an ordinary visible player)

Adds follow their boss's rule: with no Spectator the boss itself targets the player (Desert Scourge, Hive Mind, Perforators, Slime God 100% player; Brain 68%; King Slime Master+Death 76%), so its adds do too (Nuisances, Creepers, Dank Creepers, worms, Paladins and the jewel 100% player; King Slime Expert slimes 82-88% levy). The player was the nearer target in most frames; a levy was nearer in 27-75% of the player-target frames for Nuisances, worms, Paladins. This is the existing first-kill-solo / opt-in rule (`BossAggroUnlocked`: needs the boss beaten and a Spectator, or the config). I did not change it. INFERRED: Eve's complaint "minions always go for me" matches this when she fights without the Spectator buff, and matched real bugs (below) when she used it.

## 3. What was broken, and the fixes (all in `Common/Targeting/EnemyTargeting.cs`, `Common/Globals/LevyNpcData.cs`)

1. Desert Nuisance: Calamity spawns the head with `EntitySource_BossSpawn` (no parent), so `BossOwner` stayed -1 and the worm and its segments were never allowlisted. Now a modded NPC from that source takes the live boss of the same mod as owner. OBSERVED: spawn log `src=EntitySource_BossSpawn owner=-1` before, `owner=13` after; targets 100% player to 100% levy.
2. Perforator medium worm: when its head dies, Calamity promotes the next body segment in place (`SetDefaults`), which resets every GlobalNPC instance. The promoted head lost `BossOwner`, was not allowlisted (log: `lock=-1 allow=False`), had no target and dug straight down (reached 300 tiles). Dank Creeper lost its owner the same way (log: `owner=-1` right after a spawn that logged `owner=15`). Now the owner is remembered per NPC slot (set at spawn and refreshed every tick) and handed back to the same slot when no new spawn happened in between. OBSERVED: allow=True and 100% levy afterwards.
3. Orphaned adds: `LevyNpcData.OwnerType` keeps the boss's NPC type, so worms the Perforator Hive leaves behind after its death stay army targets once that boss type is beaten and the army may draw it (Spectator or config). Before, a dead owner meant "not allowlisted". INFERRED to matter in real play (the harness fights end when the boss dies); not isolated in a recording.
4. `IsFollowerSegment`: a worm segment counts as a follower only while the head it points at is alive (a promoted head that kept a stale `realLife` could not poll for a soldier). Cheap and safe; the log did not show this case being the cause in the harness.

Do the Perforator worms count as bosses? OBSERVED: `npc.boss` is false for the worm heads (`IsBossClass` is true only through `BossOwner`); `IsFightBoss` in the recorder is false for them too, so they are adds. The Perforator Hive is the boss (`boss=True`). The BossOwner rule treats a worm with `realLife` segments through the head: the head and each segment inherit the owner at spawn; segments follow the head's lock, only heads search.

Not seen as a problem: Hive Mind adds beyond the Dank Creeper, Slime God adds, Brain Creepers.

## 4. Recorder and viewer: every hostile hitbox (format 7)

- New frame key `s`: the hitbox of every other hostile NPC that `b` (boss parts) and `a` (adds) do not carry: worm segments of the adds, damage-0 NPCs (hive blobs, the Dark Heart, the Dank Creeper's hit-box phase, jewels), anything else hostile. NO range limit any more (cap 900 rows a frame): range 600 tiles from the commander for `a` and `s`, 400 tiles and cap 500 for hostile projectiles (they were 80 tiles). The King Slime jewel that fired from off-screen is now recorded wherever it stands.
- Row: `[type,id,x,y,w,h,realLife,flags]`, integer pixels, written in full the first time a slot is seen or when its size/type/flags change, otherwise `[id,x,y]`. Flags: 1 deals contact damage, 2 does not take damage, 4 body segment. Documented in `FightRecFormat.cs`; `SchemaS`, `SFlags`.
- Viewer (`tools/fightviewer/site/index.html`, `viewer.html`): draws every `s` hitbox with its real width and height (dashed = no contact damage), draws adds with their hitboxes in the gallery viewer too, and an edge marker pointing at every hostile or projectile that is outside the view (with its distance). The HUD shows "+N other hitboxes (M off-screen)".
- `make_site.py` expands the short rows over all frames (so the 15 Hz downsample cannot lose a definition) and delta-codes `s`; data version 4. `make_gallery.py` expands the same way. Old files (format 1-6) still convert and play: round-trip tests below show 0 mismatches, and a format 5 and a format 6 file converted without an `s` key.
- Boot check: `FightRecCheck pass=13 fail=0` (the format self-check now covers a full and a short `s` row).

File size, same recording with and without `s` (OBSERVED, `sitetest.py`): raw jsonl +1% to +14% (King Slime +1%, Desert Scourge +2%, Slime God +4%, Hive Mind +5-8%, Perforator +10-14%); site JSON (what the page loads) +1% to +21% (Desert Scourge +5%, Slime God +4%, Perforator +15-21%). Largest absolute case: Perforator seed 2, site JSON 773 KB to 981 KB. The worst case is a long worm fight with 20-30 segments on screen.

## 5. Unverified

- Crabulon (Crab Shrooms): the harness gained a `crab` boss code, but the shrooms target the player while Crabulon targets levies and I dropped it from scope; cause not found.
- King Slime Master+Death with the Spectator: seed 1 spawned no adds (LOSS); the jewel numbers come from seeds 2 and 3.
- Eve's own recordings were made before the fixes; I did not replay her real play. Real-play confirmation of fixes 1-3 is INFERRED from the harness.
- Eye of Cthulhu Servants were not re-run after the final fix.

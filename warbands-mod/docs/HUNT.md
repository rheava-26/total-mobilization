# Hunt, dodge, chatter, spawn footing: before and after

Eve's report: bands do not do enough, do not seek her out (they stand still off screen or behind something), only turn on her after taking damage from something else, should dodge more, and their lines are unreadable because they talk over each other. Each point was reproduced in a lab first. "Before" and "after" come from the same build: the switches `WARBANDS_HUNT_LEGACY=1`, `LEVY_HOSTILE_DODGE=0` and `WARBANDS_CHAT_LEGACY=1` restore the old behaviour. Raw rows sit beside this file. All times are game seconds. Band stats are untouched.

## Hunt (`WARBANDS_LIVE=hunt`)

A hostile band of 4 or 5 (Forest Deserters, Meteor Marauders, Mythril Mercenaries by turns) forms D tiles from a stand-in player that cannot hurt it and is watched for 120 s. Terrains: flat, rolling hills, stepped cliffs (5 tiles every D/6, rising toward the player), a 12-wide 25-deep ravine, a wall with a closed door, a wall with a gap, a stair tunnel to a cave under the player. 3 seeds per cell, 7 terrains, 3 distances (63 trials each side). Measures: first member within 25 tiles, half the band in a firing position, members in a firing position at 30/60/120 s, members stuck 10 s with no firing position, share of member-time standing still (under 1.2 tiles in 5 s) with no firing position.

| Start distance | Reached within 25 tiles (before / after) | Median time to it | Half the band in position | Engaged at 30/60/120 s | Stuck members | Standing still |
|---|---|---|---|---|---|---|
| 60 tiles | 100% / 100% | 3.5 s / 3.5 s | 100% / 100% (2.0 s) | .83 .94 .93 / .85 .90 .87 | 14 of 84 / 15 of 84 | 3.8% / 3.7% |
| 120 tiles | 5% / 100% | 7.5 s / 7.5 s | 0% / 100% (6.5 s) | .01 .01 .01 / .86 .93 .89 | 83 of 84 / 8 of 84 | 98.7% / 1.3% |
| 200 tiles | 0% / 100% | never / 13.0 s | 0% / 100% (13.5 s) | 0 0 0 / .86 .94 .95 | 84 of 84 / 5 of 84 | 99.9% / 1.0% |
| all | 35% / 100% | | 33% / 100% | .28 .32 .31 / .86 .92 .90 | 181 of 252 / 28 of 252 | 67.4% / 2.0% |

The cause: a band with no player-side body within 120 tiles camped at its anchor, so off screen it never moved. 60 tiles is the control: unchanged. Per-terrain rows are in `hunt_before.tsv` and `hunt_after.tsv` (the door, the gap and the ravine cost nothing: Levy's planner and door handling already got bodies through them once something told them to go).

**Real surface, `WARBANDS_HUNT=world`.** 18 natural-style bands (40 to 70 tiles) on the open surface of the test world, 60 s: reached within 25 tiles 16/18 before, 18/18 after; members standing still 20.7% before, 12.5% after; engaged at 60 s 0.62 / 0.69; stuck members 32 / 36 of 72 (unchanged). Two causes were found in the first runs: bands formed in sealed caves under a surface player (now the footing prefers open ground when the player stands under open sky), and a walled-in member could not take Levy's off-screen hop because the player's view box held it though rock hid it (now a clear line from a player within 60 tiles is what counts). What remains: members with no hook (half of a pre-boss band) under a 20-tile cliff have no route and wait, hop when unseen, or drink their Recall.

## Neutral bands, unrelated damage

Check in `WARBANDS_LIVE=check`: a neutral band 150 tiles from the player stays neutral and 146+ tiles off; damage from nothing the player's side did (no player, levy or turret within 45 tiles) does not provoke it; the same hit with the player 25 tiles away does. Before, any life loss from lava, a fall or another enemy turned the band hostile ("they take damage from an unrelated source and freak out and attack you").

## Dodge (`WARBANDS_LIVE=dodge`)

The stand-in fires 40 real friendly bullets at the members (centre of the member at firing, no lead, 14 to 34 tiles); 12 seeds, 480 shots each side. A control row fires at a target dummy: 100% cross it. Cause: Levy's threat field listed only hostile projectiles, so a band stepped out of its own members' shots and never out of the player's.

| | Shots no member was crossed by | Shots the aimed member evaded | Ranged members evade | Blades evade |
|---|---|---|---|---|
| before | 9.8% | 22.7% | not split by role | not split by role |
| after | 34.0% | 74.4% | 95 to 100% | 0 to 60% |

The first column is lower because bullets cross the member standing in front of the aimed one (the arena is flat and the band stands in a line); the second counts the aimed member alone. Blades run at the player along the bullet's lane and evade least. Levy edits and constants: SEAMS.md, phase 7.

## Chatter (`WARBANDS_LIVE=chat`)

A five-member Forest Deserters band fights the stand-in for 90 s, 8 seeds. The screen is modelled every 6 ticks (old: vanilla combat text, 8.5 px a character, 210 ticks; new: `BandBubbles.Layout` with 6.3 px a character, since a server has no fonts).

| | Lines in 90 s | Most on screen | Frames with 2+ bubbles in which two overlap | Frames with two of one band speaking | Mean overlapped area |
|---|---|---|---|---|---|
| before | 10.8 | 4 | 63.7% | 100% | 43.5% |
| after | 5.5 | 1 | 0% | 0% | 0% |

The band says one line at a time, so about half the lines are dropped. Contrast (WCAG): the old text colour (240,130,110) against the world was 1.2 against a daytime sky, 1.5 against sand, 2.0 to 2.8 against snow, crimson, hellstone, hallow and jungle, 7.0 against a dark cave. New text (255,244,222) on its near-black plate: 17.9, whatever is behind the plate. Computed from colours, not rendered: the draw path is not verified headless.

## Spawn footing (`WARBANDS_LIVE=spawn`)

400 bands of 4 at the world spawn with the ship world's own test fixtures planted around it (`LEVY_SELFCHECK_WORLD=1`): 29 came up with 3 members (7.3%), every one for lack of standing room (never NPC slots, never starting inside a tile). In a clean world 0 of 400, vanilla and Calamity: the failure is not Calamity's, it is the fixtures (camera pad, rows, pylons) taking floor at the spawn, and the spawner accepting the first of its footings with room for three. It now takes the first with room for all, else the one with the most: 0 of 400 after. The self-check keeps its strict "band of 4" and gains a thirty-spawn case.

# PvP: Eve's levies against the optimal levies

Setup and fairness rules: `README.md`. Recordings: scratchpad `pvp_recs/` (`old_named/`, `big/`, `champ/`; `superseded_v1/` is the first run, before the teleport fix and without names).
Her levies carry their real names from her recordings (`addnames.py`); the optimal side is "Opt. <class> <n>". The recordings carry no temperament.

## Teleport bug ("Sgt. Alice" landing in the middle of the fight)
Cause: **Search & Destroy's catch-up hop to the commander** (`SoldierNPC.SearchCatchUp` → `CatchUpTeleport`). A levy more than 60 tiles from the commander, out of every player's sight and past the hop cooldown, hops to her side. In the lab the commander is a spectator at the arena centre, so the hop dropped levies of both armies into the middle of the fight.
Checked against the 40 big-arena recordings: 3182 position jumps over 12 tiles, every one landing on one of two spots next to the commander (x -70 and +54 tiles, level 3). Alice made 5 of them. The other candidates all came up clean:
- Respawn: no levy came back; the lab holds the respawn timer.
- Staggered entry: the 7 spawn points are fixed per side. In arena v2 two of them sat 50-80 tiles from the centre, so an entering levy could look as if it appeared "in the middle". Arena v3 moves them out.
- Stuck/off-screen hop: the lab's watched points cover the arena.
- Follow leash and Regroup: not used (order Search & Destroy).
- Teleport items: no Rod of Discord, mirror, potion or similar in any kit.
- The screen-size widening meant to blind the hop did nothing: the server resets the screen size.

Fix (harness only): the hop's cooldown is kept running for every PvP levy, every tick. After the fix the recordings show 0 hops to the commander (2 jumps of 13-14 tiles in 40 fights are hook pulls).
**Not a Levy bug.** The hop is designed behaviour: a Search & Destroy levy far from the commander and off screen joins her.

## 1-damage hits
The runs of "1" are damage-over-time ticks from debuffs, not weapon hits. The recorder writes every sampled life drop as an lhit (src other, by world, no projectile). In a sample fight, 2187 of 2493 lhits were 1-point drops: a levy losing 1 HP every 2 ticks (30 HP/s) under Elemental Mix.
Debuffs seen on her levies: Holy Flames (the optimal side's Providence weapons), Elemental Mix, Celled (Stardust Cell minions), Vermillion Flux. On the optimal side: Sage Poison, Elemental Mix, Acid Venom, Astral Infection, Hellfire.
The ticks are the levy's player-rule debuff damage (`DebuffDot`). They change life without a strike, so the lab's damage-dealt columns do not credit them; they appear only in life lost.

## I-frames
What a levy gets today when hit by another levy:
- **Lab off** (`LEVY_PVP_IFRAMES=0`): only vanilla NPC immunity, i.e. per-projectile local immunity, or `npc.immune[owner]` (about 10 ticks) shared by every shot of the other army, because all levy shots are owned by the commander's player slot. Vanilla player PvP immunity does not apply: levies are NPCs.
- **Lab on** (default): the levy's own enemy-hit rule (`AfterHit`), **30 immune ticks** after a hit. That is player parity, 40 ticks scaled by Levy's 0.75. With `longInvince` it is **60 ticks**.

A/B in the old format (arena a3, whole armies, both sides, 5 seeds each, after the teleport fix):

| arm | matchup | Eve's wins | median fight s | deaths Eve / optimal (median) | first death s |
|---|---|---|---|---|---|
| i-frames on | Nov 16 vs optimal | 3/10 | 19.3 | 34 / 31 | 2.5 |
| i-frames on | Nov 16 vs optimal ranged | 1/10 | 9.0 | 34 / 1 | 2.7 |
| i-frames on | now vs optimal | 6/10 | 15.0 | 30 / 40 | 2.0 |
| i-frames on | now vs optimal ranged | 0/10 | 6.1 | 40 / 3 | 1.6 |
| i-frames off | Nov 16 vs optimal | 1/10 | 8.4 | 34 / 4.5 | 1.8 |
| i-frames off | Nov 16 vs optimal ranged | 0/10 | 5.4 | 34 / 0 | 1.2 |
| i-frames off | now vs optimal | 6/10 | 8.9 | 19 / 40 | 1.4 |
| i-frames off | now vs optimal ranged | 2/10 | 4.2 | 40 / 0.5 | 1.2 |

I-frames roughly double fight length. They help her Nov 16 army a little against the measured optimal (1 → 3 wins of 10) and change little elsewhere.

Hit-window accessories (one 100-damage enemy hit on a levy wearing only that accessory, then the immune ticks read off):

| accessory | longInvince | immune ticks |
|---|---|---|
| none | no | 30 |
| Cross Necklace | yes | 60 |
| Star Veil | yes | 60 |
| Star Cloak | no | 30 (not an i-frame item) |
| Deific Amulet (Calamity) | yes | 60 |
| Rampart of Deities (Calamity) | yes | 60 |

All four i-frame accessories work on a levy and double the window. None of them is a dead slot. Deific Amulet and Rampart give no frames beyond Cross Necklace's in levy hands.

## Big arena, staggered (v2: 350 tiles, three levels, 7 spawn points a side, 3 a side on the field)
After the teleport fix, **32 of the 40 fights hit the 400 s cap**. Without the hop the levies rarely find each other across 350 tiles, so the winner is decided by the cap rule (more manpower left). Treat these wins as weak.

| matchup | Eve's wins | median deaths Eve / optimal |
|---|---|---|
| Nov 16 vs optimal | 4/10 | 11 / 11.5 |
| Nov 16 vs optimal ranged | 1/10 | 25 / 7.5 |
| now vs optimal | 0/10 | 39 / 10.5 |
| now vs optimal ranged | 2/10 | 39 / 17 |

Per class (damage per levy fielded, kills per levy):
- Rogues are the best class on the optimal side: Molten Amputator 5231 damage and 6.45 kills per levy now.
- Summoners lead her Nov 16 army (1327, 1.45).
- Melee is viable at the current tier: hers do 565 and 0.53, the optimal's 970 and 1.23.
- Mages are near zero on both sides.
Full table: `table_big_f3.md`.

Rogue stealth strikes: her current army throws about 4 a fight and lands 4.1 hits (1789 damage, 2.05 kills); the biggest is 1391. Her Nov 16 army's biggest is 4725. Nothing near 6000 lands on a 500-HP levy. The optimal rogues throw 3.4-7 a fight but almost none land as flagged stealth hits.

## Champions vs Regulars (N Champions against 3N Levies, the same weapons in the same order; arena a3, whole armies, 5 seeds a side)
Note: in the code a Champion costs **7** command points and a Man-at-Arms 3 (`LevyConstants.GradeCpCost`, the guide says the same). The test is N against 3N as asked. Equal command points would be N against 7N.

| matchup | Champion wins | median s | Champion damage (median) | Regular damage (median) |
|---|---|---|---|---|
| 3 vs 9, pre-Moon Lord | 2/10 | 9 | 4654 | 1984 |
| 5 vs 15, pre-Moon Lord | 3/10 | 10 | 11422 | 3426 |
| 3 vs 9, post-Providence | 5/10 | 7 | 5753 | 1770 |
| 5 vs 15, post-Providence | 5/10 | 13 | 9338 | 3471 |

Champions wore 6 accessories plus wings and boots. Per Champion: 700-6500 damage and 0.5-5.9 kills; per Regular: 60-530 damage and 0-0.5 kills. Full table: `table_champ.md`.

## Old format, first run (64 fights, before the teleport fix)
Kept for the record (`fights.json`). The commander's-facing bias was found and cancelled in that run; the catch-up hop was not yet fixed.

## Side markers for the viewer
- Header `meta`: `pvp: "1"`, `teamA`, `teamB` (army labels), `sideA: "left"`, `sideB: "right"`, `idsA` and `idsB` (the levy ids, comma lists), `label` ("A vs B").
- Names: her levies carry real names; the optimal side starts with "Opt.".
- Damage-over-time ticks show as lhit with src "other", by "world", proj -1.
